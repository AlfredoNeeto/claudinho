using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Claudinho;

// Plano Team: o statusLine não traz rate_limits, então o bridge completa h5/d7 com o endpoint que o /usage do Claude Code consulta (AD-006).
internal sealed class Usage(Func<string?> readCredentials, Func<string, string?> fetch, Action<string> log)
{
    public static readonly TimeSpan Poll = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan Backoff = TimeSpan.FromMinutes(15); // depois de 401/403/429/erro
    public static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(15);  // cache mais velho que isso é descartado

    static readonly JsonSerializerOptions Relaxed = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    JsonArray? h5, d7;
    DateTime cachedAt, nextPoll;
    bool cached;
    string? lastLog;

    // Devolve a linha com h5/d7 preenchidos se ela não tiver nenhum dos dois; senão devolve igual.
    public string? Merge(string? line, DateTime now)
    {
        if (line is null) return null;
        JsonObject? o = null;
        try { o = JsonNode.Parse(line) as JsonObject; } catch (JsonException) { }
        if (o is null || o.ContainsKey("h5") || o.ContainsKey("d7")) return line;

        if (now >= nextPoll) Refresh(now);
        if (!cached || now - cachedAt > MaxAge || (h5 is null && d7 is null)) return line;

        var merged = new JsonObject();
        foreach (var k in Contract.Keys)
        {
            if (k == "h5") { if (h5 is not null) merged[k] = h5.DeepClone(); }
            else if (k == "d7") { if (d7 is not null) merged[k] = d7.DeepClone(); }
            else if (o.TryGetPropertyValue(k, out var v)) merged[k] = v?.DeepClone();
        }
        foreach (var (k, v) in o)
            if (!merged.ContainsKey(k)) merged[k] = v?.DeepClone();
        return merged.ToJsonString(Relaxed);
    }

    void Refresh(DateTime now)
    {
        if (Token(now) is not string token)
        {
            Log("sem token");
            nextPoll = now + Poll;
            return;
        }

        string? body;
        try { body = fetch(token); }
        catch (HttpRequestException e) { Fail(now, e.StatusCode is { } s ? ((int)s).ToString() : "erro de rede"); return; }
        catch (OperationCanceledException) { Fail(now, "timeout"); return; }
        catch (IOException) { Fail(now, "erro de rede"); return; } // conexão caída no meio da leitura do corpo
        if (body is null) { Fail(now, "erro"); return; }

        JsonObject? r = null;
        try { r = JsonNode.Parse(body) as JsonObject; } catch (JsonException) { }
        if (r is null || !r.ContainsKey("five_hour")) { Fail(now, "resposta sem five_hour"); return; }

        var utc = new DateTimeOffset(now, TimeSpan.Zero);
        h5 = Window(r["five_hour"], utc);
        d7 = Window(r["seven_day"], utc);
        cached = true;
        cachedAt = now;
        nextPoll = now + Poll;
        Log($"5h {Pct(h5)} 7d {Pct(d7)}");
    }

    void Fail(DateTime now, string why)
    {
        Log(why);
        nextPoll = now + Backoff;
    }

    // Só lê o arquivo do Claude Code: nunca grava, nunca usa o refreshToken, nunca loga o token.
    string? Token(DateTime now)
    {
        try
        {
            if (readCredentials() is not string text || JsonNode.Parse(text) is not JsonObject root
                || root["claudeAiOauth"] is not JsonObject oauth) return null;
            if (Contract.Str(oauth["accessToken"]) is not string token || token.Length == 0) return null;
            if (Contract.Num(oauth["expiresAt"]) is double exp && exp <= new DateTimeOffset(now, TimeSpan.Zero).ToUnixTimeMilliseconds()) return null;
            return token;
        }
        catch (JsonException) { return null; }
    }

    static JsonArray? Window(JsonNode? w, DateTimeOffset now)
    {
        if (w is not JsonObject || Contract.Num(w["utilization"]) is not double used) return null;
        if (Contract.Str(w["resets_at"]) is not string s
            || !DateTimeOffset.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.None, out var resets)
            || resets <= now) return null;
        return [Math.Round(used, 1), resets.ToUnixTimeSeconds()];
    }

    static string Pct(JsonArray? w) => w is null ? "--" : $"{w[0]!.GetValue<double>().ToString(CultureInfo.InvariantCulture)}%";

    void Log(string what)
    {
        if (what == lastLog) return;
        lastLog = what;
        log($"cota via /usage: {what}");
    }

    static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(5) };

    // Devolve o corpo do 200; outro status vira HttpRequestException com o StatusCode, e o Usage aplica o Backoff.
    public static string? Fetch(string token)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "https://api.anthropic.com/api/oauth/usage");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Add("anthropic-beta", "oauth-2025-04-20");
        req.Headers.UserAgent.ParseAdd($"claudinho/{typeof(Usage).Assembly.GetName().Version?.ToString(3)}");
        using var res = Http.Send(req);
        res.EnsureSuccessStatusCode();
        using var reader = new StreamReader(res.Content.ReadAsStream());
        return reader.ReadToEnd();
    }
}
