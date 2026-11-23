using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claudinho;

// Linha v1 (door 1 do plano): o que o statusline grava e o bridge manda para a placa.
internal static class Contract
{
    public static readonly string[] Keys = ["v", "t", "tz", "h5", "d7", "ctx", "tok", "usd", "m", "p"];

    public static string Build(JsonNode payload, long now, int tzMinutes)
    {
        using var ms = new MemoryStream();
        using (var w = new Utf8JsonWriter(ms, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            w.WriteStartObject();
            w.WriteNumber("v", 1);
            w.WriteNumber("t", now);
            w.WriteNumber("tz", tzMinutes);
            Window(w, "h5", payload["rate_limits"]?["five_hour"]);
            Window(w, "d7", payload["rate_limits"]?["seven_day"]);
            if (Num(payload["context_window"]?["used_percentage"]) is double ctx) w.WriteNumber("ctx", Math.Round(ctx, 1));
            var tin = Num(payload["context_window"]?["total_input_tokens"]);
            var tout = Num(payload["context_window"]?["total_output_tokens"]);
            if (tin is not null || tout is not null) w.WriteNumber("tok", (long)((tin ?? 0) + (tout ?? 0)));
            if (Num(payload["cost"]?["total_cost_usd"]) is double usd) w.WriteNumber("usd", Math.Round(usd, 2));
            if (Str(payload["model"]?["display_name"]) is string m) w.WriteString("m", m);
            // só o nome da pasta: o caminho completo nunca sai do PC
            if (Str(payload["workspace"]?["project_dir"]) is string dir) w.WriteString("p", Path.GetFileName(dir.TrimEnd('\\', '/')));
            w.WriteEndObject();
        }
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    static void Window(Utf8JsonWriter w, string key, JsonNode? win)
    {
        if (Num(win?["used_percentage"]) is not double used || Num(win?["resets_at"]) is not double resets) return;
        w.WriteStartArray(key);
        w.WriteNumberValue(Math.Round(used, 1));
        w.WriteNumberValue((long)resets);
        w.WriteEndArray();
    }

    public static double? Num(JsonNode? n) => n is JsonValue v && v.TryGetValue<double>(out var d) ? d : null;
    public static string? Str(JsonNode? n) => n is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;
}
