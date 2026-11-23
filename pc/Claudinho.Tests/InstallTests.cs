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

    (int code, string text) Install(bool force = false)
    {
        var o = new StringWriter();
        return (Installer.Install(Exe, Target, Settings, home, force, o), o.ToString());
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
