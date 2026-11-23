using System.Text.Json.Nodes;

namespace Claudinho.Tests;

public class StatuslineTests : IDisposable
{
    // Campos e formatos da documentação do statusLine do Claude Code.
    internal const string Full = """
    {
      "session_id": "abc", "cwd": "D:\\monitor",
      "model": { "id": "claude-opus-5-5", "display_name": "Opus" },
      "workspace": { "current_dir": "D:\\monitor", "project_dir": "D:\\monitor" },
      "cost": { "total_cost_usd": 4.1234 },
      "context_window": { "total_input_tokens": 150000, "total_output_tokens": 2340, "used_percentage": 37 },
      "rate_limits": {
        "five_hour": { "used_percentage": 23.5, "resets_at": 1738425600 },
        "seven_day": { "used_percentage": 41, "resets_at": 1738857600 }
      }
    }
    """;

    readonly string dir = Directory.CreateTempSubdirectory("claudinho-").FullName;
    string Snap => Path.Combine(dir, "snapshot.json");
    public void Dispose() => Directory.Delete(dir, true);

    (int code, string text) Run(string stdin, Func<bool>? running = null, Action? start = null)
    {
        var o = new StringWriter();
        var code = Statusline.Run(stdin, o, Snap, 1738420000, -180, running ?? (() => true), start ?? (() => { }));
        return (code, o.ToString());
    }

    [Fact]
    public void Prints_text_for_full_payload()
    {
        var (code, text) = Run(Full);
        Assert.Equal(0, code);
        Assert.Equal("5h 24% · 7d 41% · ctx 37%", text);
    }

    [Theory]
    [InlineData("rate_limits", "5h -- · 7d -- · ctx 37%")]
    [InlineData("five_hour", "5h -- · 7d 41% · ctx 37%")]
    [InlineData("seven_day", "5h 24% · 7d -- · ctx 37%")]
    public void Prints_dashes_when_window_missing(string removed, string expected)
    {
        var p = JsonNode.Parse(Full)!.AsObject();
        if (removed == "rate_limits") p.Remove("rate_limits");
        else p["rate_limits"]!.AsObject().Remove(removed);

        var (code, text) = Run(p.ToJsonString());
        Assert.Equal(0, code);
        Assert.Equal(expected, text);
    }

    [Theory]
    [InlineData("isso não é json")]
    [InlineData("")]
    [InlineData("[1,2]")]
    public void Invalid_json_keeps_snapshot(string stdin)
    {
        File.WriteAllText(Snap, "anterior");
        var started = 0;
        var (code, text) = Run(stdin, () => false, () => started++);

        Assert.Equal(0, code);
        Assert.Equal("claudinho: sem dados", text);
        Assert.Equal("anterior", File.ReadAllText(Snap));
        Assert.Equal(0, started);
    }

    [Theory]
    [InlineData(@"D:\monitor", "monitor")]
    [InlineData(@"C:\Users\Fulano\projetos\relatório\", "relatório")]
    [InlineData("/home/fulano/app/", "app")]
    public void Snapshot_has_only_contract_fields(string projectDir, string expectedP)
    {
        var p = JsonNode.Parse(Full)!.AsObject();
        p["workspace"]!["project_dir"] = projectDir;
        Run(p.ToJsonString());

        var snap = JsonNode.Parse(File.ReadAllText(Snap))!.AsObject();
        Assert.Equal(Contract.Keys.OrderBy(k => k), snap.Select(kv => kv.Key).OrderBy(k => k));
        Assert.Equal(1, snap["v"]!.GetValue<int>());
        Assert.Equal(1738420000, snap["t"]!.GetValue<long>());
        Assert.Equal(-180, snap["tz"]!.GetValue<int>());
        Assert.Equal(23.5, snap["h5"]![0]!.GetValue<double>());
        Assert.Equal(1738425600, snap["h5"]![1]!.GetValue<long>());
        Assert.Equal(41, snap["d7"]![0]!.GetValue<double>());
        Assert.Equal(37, snap["ctx"]!.GetValue<double>());
        Assert.Equal(152340, snap["tok"]!.GetValue<long>());
        Assert.Equal(4.12, snap["usd"]!.GetValue<double>());
        Assert.Equal("Opus", snap["m"]!.GetValue<string>());
        Assert.Equal(expectedP, snap["p"]!.GetValue<string>());
        Assert.DoesNotContain("\\", snap["p"]!.GetValue<string>());
        Assert.DoesNotContain("/", snap["p"]!.GetValue<string>());
    }

    [Fact]
    public void Starts_bridge_only_when_absent()
    {
        var started = 0;
        Run(Full, () => false, () => started++);
        Assert.Equal(1, started);

        Run(Full, () => true, () => started++);
        Assert.Equal(1, started);
    }

    [Fact]
    public void Concurrent_writes_leave_valid_snapshot()
    {
        var contents = Enumerable.Range(0, 20).Select(i => $"{{\"v\":1,\"t\":{i}}}").ToArray();
        Parallel.ForEach(contents, new ParallelOptions { MaxDegreeOfParallelism = 20 }, c => Snapshot.Write(Snap, c));

        var final = File.ReadAllText(Snap);
        Assert.NotNull(JsonNode.Parse(final));
        Assert.Contains(final, contents);
        Assert.Empty(Directory.GetFiles(dir, "*.tmp"));
    }
}
