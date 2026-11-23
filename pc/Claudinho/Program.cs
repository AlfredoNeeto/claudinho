using System.Diagnostics;
using System.Text;
using Claudinho;

const string MutexName = @"Local\Claudinho.Bridge";

Console.OutputEncoding = new UTF8Encoding(false);
Console.InputEncoding = new UTF8Encoding(false);

var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
var appDir = Environment.GetEnvironmentVariable("CLAUDINHO_HOME")
             ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Claudinho");
var claudeDir = Environment.GetEnvironmentVariable("CLAUDE_CONFIG_DIR") ?? Path.Combine(home, ".claude");
var settings = Path.Combine(claudeDir, "settings.json");
var snapshot = Path.Combine(appDir, "snapshot.json");
var log = Path.Combine(appDir, "bridge.log");

switch (args.FirstOrDefault())
{
    case "statusline":
        return Statusline.Run(Console.In.ReadToEnd(), Console.Out, snapshot,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            (int)TimeZoneInfo.Local.GetUtcOffset(DateTime.UtcNow).TotalMinutes,
            () => BridgeHost.Running(MutexName), StartBridge);

    case "bridge":
        Action<string> logLine = line => File.AppendAllText(log, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {line}\n");
        var ble = new BleFinder(Ble.Available, Ble.Scan, logLine);
        var bridge = new Bridge(() => Snapshot.Read(snapshot), () => Board.Connect(Board.Usb, ble.Find), logLine);
        return BridgeHost.Run(MutexName, log, bridge, () => DateTime.UtcNow, () => Thread.Sleep(250));

    case "install":
        return Installer.Install(Environment.ProcessPath!, appDir, settings, home, args.Contains("--force"), Console.Out);

    case "uninstall":
        return Installer.Uninstall(appDir, settings, StopBridge, Console.Out);

    case "--version":
        Console.WriteLine($"claudinho {typeof(Bridge).Assembly.GetName().Version?.ToString(3)}");
        return 0;

    default:
        Console.WriteLine("uso: claudinho statusline | bridge | install [--force] | uninstall | --version");
        return 2;
}

// ShellExecute não herda os handles: se herdasse o stdout do statusline, o Claude Code esperaria o bridge terminar.
static void StartBridge() =>
    Process.Start(new ProcessStartInfo(Environment.ProcessPath!, "bridge") { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden });

static void StopBridge()
{
    foreach (var p in Process.GetProcessesByName("claudinho"))
    {
        if (p.Id == Environment.ProcessId) continue;
        try { p.Kill(); p.WaitForExit(2000); } catch (InvalidOperationException) { }
    }
}
