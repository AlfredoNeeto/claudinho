using System.Net;
using System.Text.Json.Nodes;

namespace Claudinho.Tests;

public class UsageTests
{
    // Antes dos resets do fixture (22:20 de hoje e 27/11 às 11:00).
    static readonly DateTime T0 = new(2026, 11, 23, 20, 0, 0, DateTimeKind.Utc);
    static readonly string Body = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "fixtures", "usage.json"));
    const string Line = """{"v":1,"t":1795460000,"tz":-180,"ctx":37,"m":"Opus 4.8","p":"monitor"}""";

    static string Credentials(DateTime expires) =>
        new JsonObject
        {
            ["claudeAiOauth"] = new JsonObject
            {
                ["accessToken"] = "tok-123", ["refreshToken"] = "refresh-456",
                ["expiresAt"] = new DateTimeOffset(expires).ToUnixTimeMilliseconds(),
            },
        }.ToJsonString();

    sealed class Rig
    {
        public string? Creds = Credentials(T0.AddHours(8));
        public Func<string, string?> Reply = _ => Body;
        public List<string> Tokens = [];
        public List<string> Log = [];
        public Usage Usage;

        public Rig() => Usage = new Usage(() => Creds, t => { Tokens.Add(t); return Reply(t); }, Log.Add);
        public JsonObject Merge(DateTime now, string line = Line) => JsonNode.Parse(Usage.Merge(line, now)!)!.AsObject();
    }

    [Fact]
    public void Fills_quota_and_keeps_other_keys()
    {
        var rig = new Rig();
        var merged = rig.Usage.Merge(Line, T0);

        Assert.Equal(
            """{"v":1,"t":1795460000,"tz":-180,"h5":[6,1795472400],"d7":[15,1795777200],"ctx":37,"m":"Opus 4.8","p":"monitor"}""",
            merged);
        Assert.Equal(["tok-123"], rig.Tokens);
        Assert.Equal(["cota via /usage: 5h 6% 7d 15%"], rig.Log);
    }

    [Fact]
    public void Line_with_quota_is_untouched_and_not_fetched()
    {
        var rig = new Rig();
        const string withQuota = """{"v":1,"t":1,"h5":[23.5,1795472400],"ctx":37}""";

        Assert.Equal(withQuota, rig.Usage.Merge(withQuota, T0));
        Assert.Empty(rig.Tokens);
    }

    [Fact]
    public void Expired_token_is_not_used()
    {
        var rig = new Rig { Creds = Credentials(T0.AddMinutes(-1)) };

        Assert.Equal(Line, rig.Usage.Merge(Line, T0));
        Assert.Empty(rig.Tokens);
        Assert.Equal(["cota via /usage: sem token"], rig.Log);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{quebrado")]
    [InlineData("""{"claudeAiOauth":{}}""")]
    [InlineData("[]")]
    public void Missing_or_broken_credentials_leave_line_without_quota(string? creds)
    {
        var rig = new Rig { Creds = creds };

        Assert.Equal(Line, rig.Usage.Merge(Line, T0));
        Assert.Empty(rig.Tokens);
    }

    [Fact]
    public void Missing_token_waits_poll_not_backoff()
    {
        var rig = new Rig { Creds = null };
        rig.Usage.Merge(Line, T0);
        rig.Creds = Credentials(T0.AddHours(8));

        rig.Usage.Merge(Line, T0 + Usage.Poll - TimeSpan.FromSeconds(1));
        Assert.Empty(rig.Tokens);
        rig.Usage.Merge(Line, T0 + Usage.Poll);
        Assert.Single(rig.Tokens);
    }

    public static TheoryData<string, Func<string, string?>> Failures => new()
    {
        { "erro", _ => null },
        { "401", _ => throw new HttpRequestException("x", null, HttpStatusCode.Unauthorized) },
        { "429", _ => throw new HttpRequestException("x", null, HttpStatusCode.TooManyRequests) },
        { "timeout", _ => throw new TaskCanceledException() },
        { "erro de rede", _ => throw new HttpRequestException("sem rede") },
    };

    [Theory]
    [MemberData(nameof(Failures))]
    public void Failure_waits_backoff(string why, Func<string, string?> reply)
    {
        var rig = new Rig { Reply = reply };

        Assert.Equal(Line, rig.Usage.Merge(Line, T0));
        rig.Usage.Merge(Line, T0 + Usage.Backoff - TimeSpan.FromSeconds(1));
        Assert.Single(rig.Tokens);
        rig.Usage.Merge(Line, T0 + Usage.Backoff);
        Assert.Equal(2, rig.Tokens.Count);
        Assert.Equal([$"cota via /usage: {why}"], rig.Log); // a mesma falha não repete no log
    }

    [Fact]
    public void One_fetch_per_poll()
    {
        var rig = new Rig();
        for (var s = 0; s < 300; s++) rig.Usage.Merge(Line, T0.AddSeconds(s)); // um Poll inteiro de ticks

        Assert.Single(rig.Tokens);
        rig.Usage.Merge(Line, T0 + Usage.Poll);
        Assert.Equal(2, rig.Tokens.Count);
        Assert.Single(rig.Log);
    }

    [Fact]
    public void Stale_cache_is_not_merged()
    {
        var rig = new Rig();
        rig.Usage.Merge(Line, T0);
        rig.Reply = _ => throw new HttpRequestException("x", null, HttpStatusCode.Unauthorized);

        Assert.NotNull(rig.Merge(T0 + Usage.Poll)["h5"]); // falhou agora, mas o cache tem 5 min
        Assert.NotNull(rig.Merge(T0 + Usage.MaxAge)["h5"]);
        Assert.Equal(Line, rig.Usage.Merge(Line, T0 + Usage.MaxAge + TimeSpan.FromSeconds(1)));
    }

    [Fact]
    public void Null_utilization_or_past_reset_drops_only_that_window()
    {
        var body = JsonNode.Parse(Body)!.AsObject();
        body["five_hour"]!["utilization"] = null;
        var rig = new Rig { Reply = _ => body.ToJsonString() };
        var s = rig.Merge(T0);
        Assert.Null(s["h5"]);
        Assert.Equal("[15,1795777200]", s["d7"]!.ToJsonString());

        body = JsonNode.Parse(Body)!.AsObject();
        body["seven_day"]!["resets_at"] = "2026-11-23T19:00:00+00:00";
        rig = new Rig { Reply = _ => body.ToJsonString() };
        s = rig.Merge(T0);
        Assert.Equal("[6,1795472400]", s["h5"]!.ToJsonString());
        Assert.Null(s["d7"]);

        body = JsonNode.Parse(Body)!.AsObject();
        body["seven_day"]!["resets_at"] = "amanhã";
        rig = new Rig { Reply = _ => body.ToJsonString() };
        Assert.Null(rig.Merge(T0)["d7"]);
    }

    [Theory]
    [InlineData("""{"seven_day":{"utilization":15.0,"resets_at":"2026-11-27T11:00:00+00:00"}}""")]
    [InlineData("não é json")]
    [InlineData("[]")]
    public void Changed_response_format_logs_and_leaves_line(string body)
    {
        var rig = new Rig { Reply = _ => body };

        Assert.Equal(Line, rig.Usage.Merge(Line, T0));
        Assert.Equal(["cota via /usage: resposta sem five_hour"], rig.Log);
    }

    [Fact]
    public void Never_logs_or_writes_the_token()
    {
        var rig = new Rig();
        var creds = rig.Creds;
        rig.Usage.Merge(Line, T0);
        rig.Reply = _ => throw new HttpRequestException("x", null, HttpStatusCode.Forbidden);
        rig.Usage.Merge(Line, T0 + Usage.Poll);

        Assert.Equal(creds, rig.Creds);
        Assert.DoesNotContain(rig.Log, l => l.Contains("tok-123") || l.Contains("refresh-456"));
        Assert.Equal(["tok-123", "tok-123"], rig.Tokens); // o accessToken, nunca o refreshToken
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{quebrado")]
    public void Null_or_broken_line_passes_through_without_fetch(string? line)
    {
        var rig = new Rig();
        Assert.Equal(line, rig.Usage.Merge(line, T0));
        Assert.Empty(rig.Tokens);
    }
}
