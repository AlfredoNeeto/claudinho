using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claudinho;

internal static class Statusline
{
    // Sempre sai com 0: um código diferente apaga a status line do Claude Code.
    public static int Run(string stdin, TextWriter output, string snapshotPath, long now, int tzMinutes,
                          Func<bool> bridgeRunning, Action startBridge)
    {
        JsonNode? payload = null;
        try { payload = JsonNode.Parse(stdin); } catch (JsonException) { }
        if (payload is not JsonObject)
        {
            output.Write("claudinho: sem dados");
            return 0;
        }

        output.Write(Text(payload));
        try
        {
            Snapshot.Write(snapshotPath, Contract.Build(payload, now, tzMinutes));
            if (!bridgeRunning()) startBridge();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception)
        {
            // a status line continua; a placa só perde esta atualização
        }
        return 0;
    }

    internal static string Text(JsonNode p) =>
        $"5h {Pct(p["rate_limits"]?["five_hour"]?["used_percentage"])} · " +
        $"7d {Pct(p["rate_limits"]?["seven_day"]?["used_percentage"])} · " +
        $"ctx {Pct(p["context_window"]?["used_percentage"])}";

    static string Pct(JsonNode? n) =>
        Contract.Num(n) is double d ? $"{Math.Round(d, MidpointRounding.AwayFromZero)}%" : "--";
}

internal static class Snapshot
{
    // Grava num temporário e troca de uma vez: duas sessões do Claude Code podem gravar juntas.
    public static void Write(string path, string content)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = $"{path}.{Environment.ProcessId}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(tmp, content);
        try
        {
            for (var i = 0; ; i++)
            {
                try { File.Move(tmp, path, overwrite: true); return; }
                catch (Exception e) when (i < 20 && e is IOException or UnauthorizedAccessException) { Thread.Sleep(5); }
            }
        }
        finally
        {
            if (File.Exists(tmp)) File.Delete(tmp);
        }
    }

    public static string? Read(string path)
    {
        try { return File.ReadAllText(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return null; }
    }
}
