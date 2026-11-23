using System.Text.Json.Nodes;

namespace Claudinho.Tests;

public class InstallTests : IDisposable
{
    const string Original = """
    {
      "model": "opus",
      "theme": "dark",
      "enabledPlugins": { "ponytail@ponytail": true },
      "autoMode": { "environment": ["### Org-wide", "**Organization**: None"] }
    }
    """;

    readonly string home = Directory.CreateTempSubdirectory("claudinho-home-").FullName;
    string Target => Path.Combine(home, "AppData", "Local", "Claudinho");
    string Settings => Path.Combine(home, ".claude", "settings.json");
    string Exe => Path.Combine(home, "download", "claudinho.exe");
    public void Dispose() => Directory.Delete(home, true);

    public InstallTests()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Settings)!);
        Directory.CreateDirectory(Path.GetDirectoryName(Exe)!);
        File.WriteAllText(Settings, Original);
        File.WriteAllText(Exe, "EXE");
    }

    (int code, string text) Install(bool force = false, bool hook = false)
    {
        var o = new StringWriter();
        return (Installer.Install(Exe, Target, Settings, home, force, hook, o), o.ToString());
    }

    int Uninstall() => Installer.Uninstall(Target, Settings, () => { }, new StringWriter());

    const string OurHook = "~/AppData/Local/Claudinho/claudinho.exe hook";
    static readonly JsonNode OurEntry = JsonNode.Parse($$"""{"hooks":[{"type":"command","command":"{{OurHook}}"}]}""")!;
    static readonly JsonNode ForeignStop = JsonNode.Parse("""{"hooks":[{"type":"command","command":"say pronto"}]}""")!;
    static readonly JsonNode PreToolUse = JsonNode.Parse("""[{"matcher":"Bash","hooks":[{"type":"command","command":"echo pre"}]}]""")!;

    // O original com hooks alheios em Stop e PreToolUse.
    string WithForeignHooks()
    {
        var o = JsonNode.Parse(Original)!.AsObject();
        o["hooks"] = new JsonObject { ["Stop"] = new JsonArray(ForeignStop.DeepClone()), ["PreToolUse"] = PreToolUse.DeepClone() };
        var text = o.ToJsonString();
        File.WriteAllText(Settings, text);
        return text;
    }

    JsonArray? Stop() => Read()["hooks"]?["Stop"]?.AsArray();
    int OurEntries() => Stop()?.Count(e => JsonNode.DeepEquals(e, OurEntry)) ?? 0;

    [Fact]
    public void Install_hook_adds_stop_entry()
    {
        var original = WithForeignHooks();

        var (code, _) = Install(hook: true);

        Assert.Equal(0, code);
        Assert.Equal(original, File.ReadAllText(Settings + ".claudinho.bak"));
        var s = Read();
        Assert.Null(s["statusLine"]);
        Assert.Equal(2, Stop()!.Count);
        Assert.Contains(Stop()!, e => JsonNode.DeepEquals(e, ForeignStop));
        Assert.Equal(1, OurEntries());
        Assert.True(JsonNode.DeepEquals(PreToolUse, s["hooks"]!["PreToolUse"]));
        Assert.Equal("EXE", File.ReadAllText(Path.Combine(Target, "claudinho.exe")));
    }

    [Fact]
    public void Install_hook_twice_is_idempotent()
    {
        WithForeignHooks();
        Install(hook: true);
        var first = Read();

        var (code, _) = Install(hook: true);

        Assert.Equal(0, code);
        Assert.True(JsonNode.DeepEquals(first, Read()));
        Assert.Equal(1, OurEntries());
    }

    [Fact]
    public void Modes_replace_each_other()
    {
        WithForeignHooks();

        Install();
        Assert.NotNull(Read()["statusLine"]);
        Install(hook: true);
        Assert.Null(Read()["statusLine"]);
        Assert.Equal(1, OurEntries());

        Install();
        Assert.Equal(0, OurEntries());
        Assert.Contains(Stop()!, e => JsonNode.DeepEquals(e, ForeignStop));
        Assert.Contains("claudinho.exe statusline", Read()["statusLine"]!["command"]!.GetValue<string>());

        var foreign = JsonNode.Parse(Original)!.AsObject();
        var line = new JsonObject { ["type"] = "command", ["command"] = "bash ~/.claude/statusline.sh" };
        foreign["statusLine"] = line.DeepClone();
        File.WriteAllText(Settings, foreign.ToJsonString());
        var (code, _) = Install(hook: true);
        Assert.Equal(0, code);
        Assert.True(JsonNode.DeepEquals(line, Read()["statusLine"]));
        Assert.Equal(1, OurEntries());
    }

    [Fact]
    public void Uninstall_removes_hook_entry()
    {
        Install(hook: true);
        Assert.Equal(0, Uninstall());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Original), Read()));

        WithForeignHooks();
        Install(hook: true);
        Assert.Equal(0, Uninstall());
        Assert.Equal(0, OurEntries());
        Assert.Single(Stop()!);
        Assert.True(JsonNode.DeepEquals(ForeignStop, Stop()![0]));
        Assert.True(JsonNode.DeepEquals(PreToolUse, Read()["hooks"]!["PreToolUse"]));
    }

    [Fact]
    public void Never_touches_managed_settings()
    {
        var claude = Path.GetDirectoryName(Settings)!;
        var remote = Path.Combine(claude, "remote-settings.json");
        var managed = Path.Combine(claude, "managed-settings.json");
        File.WriteAllText(remote, """{"statusLine":{"type":"command","command":"curl empresa"}}""");
        File.WriteAllText(managed, """{"allowManagedHooksOnly":false}""");
        var remoteBytes = File.ReadAllBytes(remote);
        var managedBytes = File.ReadAllBytes(managed);
        string[] Tree() => Directory.GetFileSystemEntries(home, "*", SearchOption.AllDirectories)
            .Select(p => Path.GetRelativePath(home, p)).Order().ToArray();
        var before = Tree();
        string[] ours = [Path.Combine(".claude", "settings.json.claudinho.bak"), Path.Combine("AppData"),
                         Path.Combine("AppData", "Local"), Path.Combine("AppData", "Local", "Claudinho"),
                         Path.Combine("AppData", "Local", "Claudinho", "claudinho.exe")];

        Install(hook: true);
        Assert.Equal(before.Concat(ours).Order().ToArray(), Tree());
        Install();
        Uninstall();

        Assert.Equal(remoteBytes, File.ReadAllBytes(remote));
        Assert.Equal(managedBytes, File.ReadAllBytes(managed));
        Assert.Equal(before.Append(Path.Combine(".claude", "settings.json.claudinho.bak")).Order().ToArray(),
                     Tree().Where(p => !p.StartsWith("AppData")).ToArray());
    }

    JsonObject Read() => JsonNode.Parse(File.ReadAllText(Settings))!.AsObject();

    static JsonObject WithoutStatusLine(JsonObject o)
    {
        var c = JsonNode.Parse(o.ToJsonString())!.AsObject();
        c.Remove("statusLine");
        return c;
    }

    [Fact]
    public void Install_writes_statusline_and_backup()
    {
        var (code, _) = Install();

        Assert.Equal(0, code);
        Assert.Equal("EXE", File.ReadAllText(Path.Combine(Target, "claudinho.exe")));
        Assert.Equal(Original, File.ReadAllText(Settings + ".claudinho.bak"));
        var s = Read();
        Assert.Equal("command", s["statusLine"]!["type"]!.GetValue<string>());
        Assert.Equal("~/AppData/Local/Claudinho/claudinho.exe statusline", s["statusLine"]!["command"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Original), WithoutStatusLine(s)));
    }

    [Fact]
    public void Refuses_foreign_statusline()
    {
        var foreign = JsonNode.Parse(Original)!.AsObject();
        foreign["statusLine"] = new JsonObject { ["type"] = "command", ["command"] = "bash ~/.claude/statusline.sh" };
        File.WriteAllText(Settings, foreign.ToJsonString());
        var before = File.ReadAllBytes(Settings);

        var (code, text) = Install();
        Assert.Equal(1, code);
        Assert.Contains("statusLine já configurado; use --force para substituir", text);
        Assert.Equal(before, File.ReadAllBytes(Settings));

        (code, _) = Install(force: true);
        Assert.Equal(0, code);
        Assert.Contains("claudinho.exe statusline", Read()["statusLine"]!["command"]!.GetValue<string>());
    }

    [Fact]
    public void Install_twice_is_idempotent()
    {
        Install();
        var first = Read();
        var (code, _) = Install();

        Assert.Equal(0, code);
        Assert.True(JsonNode.DeepEquals(first, Read()));
        Assert.Equal(Original, File.ReadAllText(Settings + ".claudinho.bak"));
    }

    [Fact]
    public void Uninstall_removes_only_claudinho()
    {
        Install();
        var stopped = 0;
        var code = Installer.Uninstall(Target, Settings, () => stopped++, new StringWriter());

        Assert.Equal(0, code);
        Assert.Equal(1, stopped);
        Assert.False(Directory.Exists(Target));
        var s = Read();
        Assert.Null(s["statusLine"]);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(Original), s));
    }
}
