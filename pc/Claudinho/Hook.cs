using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Claudinho;

// Modo hook (plano work-hook): onde a statusLine é da empresa, o hook Stop monta o mesmo payload a partir do transcript.
internal static partial class Hook
{
    // Sempre sai com 0 e sem stdout: no Stop, o código 2 faria o Claude Code continuar a resposta.
    public static int Run(string stdin, string snapshotPath, long now, int tzMinutes, Func<bool> bridgeRunning, Action startBridge)
    {
        try
        {
            if (Payload(stdin) is not JsonObject payload) return 0;
            Snapshot.Write(snapshotPath, Contract.Build(payload, now, tzMinutes));
            if (!bridgeRunning()) startBridge();
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or Win32Exception)
        {
            // a resposta do Claude Code segue; a placa só perde esta atualização
        }
        return 0;
    }

    static JsonObject? Payload(string stdin)
    {
        JsonNode? ev = null;
        try { ev = JsonNode.Parse(stdin); } catch (JsonException) { }
        if (ev is not JsonObject || Contract.Str(ev["transcript_path"]) is not string path || !File.Exists(path)) return null;

        JsonNode? last = null;
        // o Claude Code mantém o transcript aberto enquanto escreve
        using (var reader = new StreamReader(new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)))
        {
            while (reader.ReadLine() is string line)
            {
                JsonNode? e;
                try { e = JsonNode.Parse(line); } catch (JsonException) { continue; }
                if (e is JsonObject && Contract.Str(e["type"]) == "assistant" && e["isSidechain"]?.ToJsonString() != "true"
                    && e["message"] is JsonObject msg && msg["usage"] is JsonObject
                    && Contract.Str(msg["model"]) is string model && model != "<synthetic>")
                    last = e;
            }
        }
        if (last is null) return null;

        var u = last["message"]!["usage"]!;
        var used = (Contract.Num(u["input_tokens"]) ?? 0) + (Contract.Num(u["cache_creation_input_tokens"]) ?? 0)
                   + (Contract.Num(u["cache_read_input_tokens"]) ?? 0);
        // shortcut: o transcript não diz se a sessão é de 1M; só passa a 1M quando o uso passa de 200 mil.
        var window = used > 200_000 ? 1_000_000 : 200_000;
        return new JsonObject
        {
            ["model"] = new JsonObject { ["display_name"] = Name(Contract.Str(last["message"]!["model"])!) },
            ["workspace"] = new JsonObject { ["project_dir"] = Contract.Str(ev["cwd"]) },
            ["context_window"] = new JsonObject { ["used_percentage"] = used * 100 / window },
        };
    }

    // claude-opus-4-8 → Opus 4.8, claude-sonnet-4-5-20250929 → Sonnet 4.5; outro formato sai como veio.
    internal static string Name(string id)
    {
        var m = ModelId().Match(id);
        if (!m.Success) return id;
        var family = char.ToUpperInvariant(m.Groups[1].Value[0]) + m.Groups[1].Value[1..];
        return m.Groups[3].Success ? $"{family} {m.Groups[2].Value}.{m.Groups[3].Value}" : $"{family} {m.Groups[2].Value}";
    }

    [GeneratedRegex(@"^claude-([a-z]+)-(\d+)(?:-(\d{1,2}))?(?:-\d{8})?$")]
    private static partial Regex ModelId();
}
