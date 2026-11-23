using System.IO.Ports;
using Microsoft.Win32;

namespace Claudinho;

internal interface ISerial : IDisposable
{
    void Write(string text);
    string? ReadLine(int timeoutMs);
}

// Decide o que fazer a cada tick; o tempo vem de fora para os testes controlarem.
internal sealed class Bridge(Func<string?> readSnapshot, Func<ISerial?> connect, Action<string> log)
{
    public static readonly TimeSpan Resend = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan Retry = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan Idle = TimeSpan.FromMinutes(30);

    ISerial? link;
    string? last, sent;
    DateTime lastSend, lastChange, nextTry;
    bool started;

    // false = encerrar o processo
    public bool Tick(DateTime now)
    {
        if (!started) { started = true; lastChange = now; nextTry = now; }

        var snap = readSnapshot();
        if (snap != last) { last = snap; lastChange = now; }

        if (now - lastChange >= Idle)
        {
            link?.Dispose();
            link = null;
            return false;
        }

        if (link is null)
        {
            if (now < nextTry) return true;
            link = connect();
            if (link is null) { nextTry = now + Retry; return true; }
            sent = null; // acabou de conectar: manda o snapshot atual já
        }

        if (last is not null && (last != sent || now - lastSend >= Resend))
        {
            try
            {
                link.Write(last + "\n");
                sent = last;
                lastSend = now;
            }
            catch (Exception e) when (e is IOException or InvalidOperationException or UnauthorizedAccessException or TimeoutException)
            {
                log($"porta perdida: {e.Message}");
                link.Dispose();
                link = null;
                nextTry = now + Retry;
            }
        }
        return true;
    }
}

internal static class BridgeHost
{
    public const long MaxLog = 1_048_576;

    public static int Run(string mutexName, string logPath, Bridge bridge, Func<DateTime> clock, Action sleep)
    {
        using var mutex = new Mutex(false, mutexName);
        bool owned;
        try { owned = mutex.WaitOne(0); }
        catch (AbandonedMutexException) { owned = true; }
        if (!owned) return 0; // já tem um bridge rodando

        try
        {
            TruncateLog(logPath);
            while (bridge.Tick(clock())) sleep();
            return 0;
        }
        finally
        {
            mutex.ReleaseMutex();
        }
    }

    public static bool Running(string mutexName)
    {
        if (!Mutex.TryOpenExisting(mutexName, out var m)) return false;
        m.Dispose();
        return true;
    }

    public static void TruncateLog(string path)
    {
        var f = new FileInfo(path);
        if (f.Exists && f.Length > MaxLog) File.WriteAllText(path, "");
    }
}

internal static class Board
{
    public static readonly string[] Vids = ["1A86", "303A"]; // CH340 da CYD, USB nativo do ESP32-S3
    public const int HandshakeTimeoutMs = 3000;

    public static ISerial? Pick(IEnumerable<(string Port, string Vid)> candidates, Func<string, ISerial?> open,
                                int timeoutMs = HandshakeTimeoutMs)
    {
        foreach (var (port, vid) in candidates)
        {
            if (!Vids.Contains(vid, StringComparer.OrdinalIgnoreCase)) continue;
            var s = open(port);
            if (s is null) continue;
            if (Handshake(s, timeoutMs)) return s;
            s.Dispose();
        }
        return null;
    }

    // A CH340 pode reiniciar a placa ao abrir a porta; repete o "?" até o prazo e ignora o log de boot.
    static bool Handshake(ISerial s, int timeoutMs)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        var nextAsk = 0L;
        while (Environment.TickCount64 < deadline)
        {
            if (Environment.TickCount64 >= nextAsk)
            {
                try { s.Write("?\n"); } catch (Exception e) when (e is IOException or TimeoutException or InvalidOperationException) { return false; }
                nextAsk = Environment.TickCount64 + 500;
            }
            var line = s.ReadLine(200);
            if (line is null) continue;
            if (line.StartsWith("claudinho 1 ", StringComparison.Ordinal)) return true;
        }
        return false;
    }

    public static ISerial? Connect() => Pick(UsbPorts(), ComPort.TryOpen);

    // O registro guarda o VID de cada porta USB; portas Bluetooth e da placa-mãe nem aparecem aqui.
    public static IEnumerable<(string Port, string Vid)> UsbPorts()
    {
        var present = SerialPort.GetPortNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
        using var usb = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB");
        if (usb is null) yield break;
        foreach (var dev in usb.GetSubKeyNames())
        {
            if (!dev.StartsWith("VID_", StringComparison.OrdinalIgnoreCase) || dev.Length < 8) continue;
            var vid = dev.Substring(4, 4);
            using var devKey = usb.OpenSubKey(dev);
            if (devKey is null) continue;
            foreach (var inst in devKey.GetSubKeyNames())
            {
                using var p = devKey.OpenSubKey($@"{inst}\Device Parameters");
                if (p?.GetValue("PortName") is string port && present.Contains(port)) yield return (port, vid);
            }
        }
    }
}

internal sealed class ComPort : ISerial
{
    readonly SerialPort sp;

    ComPort(string name)
    {
        // DTR/RTS desligados: com eles ligados a CH340 reinicia o ESP32
        sp = new SerialPort(name, 115200) { DtrEnable = false, RtsEnable = false, NewLine = "\n", WriteTimeout = 1000, Encoding = System.Text.Encoding.UTF8 };
        sp.Open();
    }

    public static ISerial? TryOpen(string name)
    {
        try { return new ComPort(name); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException) { return null; }
    }

    public void Write(string text) => sp.Write(text);

    public string? ReadLine(int timeoutMs)
    {
        sp.ReadTimeout = timeoutMs;
        try { return sp.ReadLine().TrimEnd('\r'); }
        catch (TimeoutException) { return null; }
    }

    public void Dispose() => sp.Dispose();
}
