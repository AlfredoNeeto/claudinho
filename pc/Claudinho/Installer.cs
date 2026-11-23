using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claudinho;

internal static class Installer
{
    public const string ExeName = "claudinho.exe";
    static readonly JsonSerializerOptions Pretty = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static int Install(string exeSource, string targetDir, string settingsPath, string home, bool force, TextWriter o)
    {
        var root = Load(settingsPath);
        if (root["statusLine"] is JsonNode existing && !Ours(existing) && !force)
        {
            o.WriteLine("statusLine já configurado; use --force para substituir");
            return 1;
        }

        Directory.CreateDirectory(targetDir);
        var exe = Path.Combine(targetDir, ExeName);
        if (!string.Equals(Path.GetFullPath(exeSource), Path.GetFullPath(exe), StringComparison.OrdinalIgnoreCase))
        {
            try { File.Copy(exeSource, exe, overwrite: true); }
            catch (IOException)
            {
                o.WriteLine($"{exe} está em uso; feche o Claude Code e rode de novo");
                return 1;
            }
        }

        var bak = settingsPath + ".claudinho.bak";
        if (File.Exists(settingsPath) && !File.Exists(bak)) File.Copy(settingsPath, bak); // guarda o original da primeira vez

        var command = Command(exe, home);
        root["statusLine"] = new JsonObject { ["type"] = "command", ["command"] = command };
        Save(settingsPath, root);
        o.WriteLine($"Claudinho instalado: {command}");
        return 0;
    }

    public static int Uninstall(string targetDir, string settingsPath, Action stopBridge, TextWriter o)
    {
        stopBridge();
        if (File.Exists(settingsPath))
        {
            var root = Load(settingsPath);
            if (root["statusLine"] is JsonNode s && Ours(s))
            {
                root.Remove("statusLine");
                Save(settingsPath, root);
            }
        }
        if (Directory.Exists(targetDir))
        {
            try { Directory.Delete(targetDir, recursive: true); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                // o próprio exe está rodando de dentro da pasta: apaga depois que ele sair
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe",
                    $"/c timeout /t 2 >nul & rmdir /s /q \"{targetDir}\"") { CreateNoWindow = true, UseShellExecute = false });
            }
        }
        o.WriteLine("Claudinho removido");
        return 0;
    }

    // "~/..." porque o Claude Code expande ~ tanto no Git Bash quanto no PowerShell, e o caminho com espaço quebraria num dos dois.
    internal static string Command(string exe, string home)
    {
        var full = Path.GetFullPath(exe);
        var rel = Path.GetRelativePath(home, full);
        if (!rel.StartsWith("..") && !Path.IsPathRooted(rel) && !rel.Contains(' '))
            return $"~/{rel.Replace('\\', '/')} statusline";
        return $"\"{full.Replace('\\', '/')}\" statusline";
    }

    static bool Ours(JsonNode statusLine) =>
        Contract.Str(statusLine["command"])?.Contains(ExeName, StringComparison.OrdinalIgnoreCase) == true;

    static JsonObject Load(string path) =>
        File.Exists(path) && JsonNode.Parse(File.ReadAllText(path)) is JsonObject o ? o : new JsonObject();

    static void Save(string path, JsonObject root)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, root.ToJsonString(Pretty));
    }
}
