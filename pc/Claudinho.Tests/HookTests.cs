using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claudinho.Tests;

public class HookTests : IDisposable
{
    readonly string dir = Directory.CreateTempSubdirectory("claudinho-hook-").FullName;
    string Snap => Path.Combine(dir, "snapshot.json");
    string Transcript => Path.Combine(dir, "session.jsonl");
    public void Dispose() => Directory.Delete(dir, true);

    // Formato das linhas do transcript do Claude Code (~/.claude/projects/<projeto>/<sessão>.jsonl).
    static string Assistant(string model = "claude-opus-4-8", long input = 2, long creation = 907, long read = 109294,
                            bool sidechain = false, bool usage = true)
    {
        var message = new JsonObject { ["model"] = model, ["role"] = "assistant" };
        if (usage)
            message["usage"] = new JsonObject
            {
                ["input_tokens"] = input, ["cache_creation_input_tokens"] = creation,
                ["cache_read_input_tokens"] = read, ["output_tokens"] = 2941,
            };
        return new JsonObject { ["type"] = "assistant", ["isSidechain"] = sidechain, ["message"] = message, ["cwd"] = "D:\\monitor" }
            .ToJsonString();
    }

    const string User = """{"type":"user","isSidechain":false,"message":{"role":"user","content":"oi"}}""";

    string Event(string cwd = "D:\\monitor\\") =>
        new JsonObject { ["session_id"] = "abc", ["transcript_path"] = Transcript, ["cwd"] = cwd, ["hook_event_name"] = "Stop" }.ToJsonString();

    int Run(string stdin, Func<bool>? running = null, Action? start = null) =>
        Hook.Run(stdin, Snap, 1738420000, -180, running ?? (() => true), start ?? (() => { }));

    JsonObject ReadSnap() => JsonNode.Parse(File.ReadAllText(Snap))!.AsObject();

    [Fact]
    public void Writes_snapshot_from_last_main_assistant()
    {
        File.WriteAllLines(Transcript, [
            Assistant(),
            User,
            Assistant(model: "claude-haiku-4-5", read: 5000, sidechain: true),
            Assistant(model: "claude-sonnet-4-5", usage: false),
            Assistant(model: "<synthetic>", input: 0, creation: 0, read: 0),
        ]);

        Assert.Equal(0, Run(Event()));

        var s = ReadSnap();
        Assert.Equal(["v", "t", "tz", "ctx", "m", "p"], s.Select(kv => kv.Key).ToArray());
        Assert.Equal(1, s["v"]!.GetValue<int>());
        Assert.Equal(1738420000, s["t"]!.GetValue<long>());
        Assert.Equal(-180, s["tz"]!.GetValue<int>());
        Assert.Equal(55.1, s["ctx"]!.GetValue<double>());
        Assert.Equal("Opus 4.8", s["m"]!.GetValue<string>());
        Assert.Equal("monitor", s["p"]!.GetValue<string>());
        foreach (var k in new[] { "h5", "d7", "tok", "usd" }) Assert.Null(s[k]);
    }

    [Theory]
    [InlineData(2, 907, 109294, 55.1)]
    [InlineData(0, 0, 200000, 100)]
    [InlineData(0, 50000, 200000, 25)]
    [InlineData(1000, 0, 0, 0.5)]
    public void Context_percentage(long input, long creation, long read, double expected)
    {
        File.WriteAllLines(Transcript, [Assistant(input: input, creation: creation, read: read)]);
        Assert.Equal(0, Run(Event()));
        Assert.Equal(expected, ReadSnap()["ctx"]!.GetValue<double>());
    }

    [Theory]
    [InlineData("claude-opus-4-8", "Opus 4.8")]
    [InlineData("claude-sonnet-4-5-20250929", "Sonnet 4.5")]
    [InlineData("claude-haiku-4-5", "Haiku 4.5")]
    [InlineData("claude-opus-5-5", "Opus 5.5")]
    public void Model_display_name(string id, string expected)
    {
        File.WriteAllLines(Transcript, [Assistant(model: id)]);
        Assert.Equal(0, Run(Event()));
        Assert.Equal(expected, ReadSnap()["m"]!.GetValue<string>());
    }

    [Fact]
    public void Starts_bridge_only_when_not_running()
    {
        File.WriteAllLines(Transcript, [Assistant()]);
        var started = 0;

        Assert.Equal(0, Run(Event(), () => false, () => started++));
        Assert.Equal(1, started);

        Assert.Equal(0, Run(Event(), () => true, () => started++));
        Assert.Equal(1, started);

        File.Delete(Snap);
        Assert.Equal(0, Run(Event(), () => false, () => throw new Win32Exception(2)));
        Assert.True(File.Exists(Snap));
    }

    public static TheoryData<string> BadInputs => new()
    {
        "lixo", "[1]", "{}", "missing-file", "no-valid-entry",
    };

    [Theory]
    [MemberData(nameof(BadInputs))]
    public void Bad_input_keeps_snapshot(string kind)
    {
        File.WriteAllLines(Transcript, [User, Assistant(usage: false), Assistant(model: "<synthetic>")]);
        var stdin = kind switch
        {
            "missing-file" => new JsonObject { ["transcript_path"] = Path.Combine(dir, "nao-existe.jsonl"), ["cwd"] = "D:\\monitor" }.ToJsonString(),
            "no-valid-entry" => Event(),
            _ => kind,
        };
        File.WriteAllText(Snap, "anterior");
        var started = 0;

        Assert.Equal(0, Run(stdin, () => false, () => started++));
        Assert.Equal("anterior", File.ReadAllText(Snap));
        Assert.Equal(0, started);
    }

    [Fact]
    public void Skips_bad_lines()
    {
        var last = Assistant(model: "claude-sonnet-4-5", input: 0, creation: 0, read: 20000);
        File.WriteAllText(Transcript, string.Join("\n", Assistant(), "{isso não é json", last, last[..(last.Length / 2)]));

        Assert.Equal(0, Run(Event()));
        var s = ReadSnap();
        Assert.Equal("Sonnet 4.5", s["m"]!.GetValue<string>());
        Assert.Equal(10, s["ctx"]!.GetValue<double>());
    }

    [Fact]
    public void Reads_transcript_open_for_writing()
    {
        File.WriteAllLines(Transcript, [Assistant()]);
        using var writer = new FileStream(Transcript, FileMode.Append, FileAccess.Write, FileShare.ReadWrite);

        Assert.Equal(0, Run(Event()));
        Assert.Equal(55.1, ReadSnap()["ctx"]!.GetValue<double>());
    }

    // O exe que o build copia para a saída dos testes; o mutex fica aberto aqui para o hook não subir um bridge de verdade.
    (int code, string stdout) Exe(string? args, string stdin)
    {
        using var mutex = new Mutex(false, @"Local\Claudinho.Bridge");
        var psi = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "claudinho.exe"), args ?? "")
        {
            RedirectStandardInput = true, RedirectStandardOutput = true, UseShellExecute = false,
        };
        psi.Environment["CLAUDINHO_HOME"] = dir;
        using var p = Process.Start(psi)!;
        p.StandardInput.Write(stdin);
        p.StandardInput.Close();
        var stdout = p.StandardOutput.ReadToEnd();
        Assert.True(p.WaitForExit(10000));
        return (p.ExitCode, stdout);
    }

    [Fact]
    public void Exe_hook_is_silent()
    {
        File.WriteAllLines(Transcript, [Assistant()]);

        var (code, stdout) = Exe("hook", Event());
        Assert.Equal(0, code);
        Assert.Equal("", stdout);
        Assert.Equal("Opus 4.8", ReadSnap()["m"]!.GetValue<string>());

        (code, stdout) = Exe("hook", "lixo");
        Assert.Equal(0, code);
        Assert.Equal("", stdout);
    }

    [Fact]
    public void Exe_usage_lists_hook()
    {
        var (code, stdout) = Exe(null, "");
        Assert.Equal(2, code);
        Assert.Contains("hook", stdout);
        Assert.Contains("install [--hook] [--force]", stdout);
    }
}
