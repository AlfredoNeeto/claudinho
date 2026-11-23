using System.Text;
using System.Text.Json.Nodes;

namespace Claudinho.Tests;

public class BleTests
{
    [Fact]
    public void Connect_tries_usb_before_ble()
    {
        Assert.Equal(TimeSpan.FromSeconds(5), Ble.ScanWindow);

        var calls = new List<string>();
        var usb = new FakeSerial();
        var ble = new FakeSerial();

        Assert.Same(usb, Board.Connect(() => { calls.Add("usb"); return usb; }, () => { calls.Add("ble"); return ble; }));
        Assert.Equal(["usb"], calls);

        calls.Clear();
        Assert.Same(ble, Board.Connect(() => { calls.Add("usb"); return null; }, () => { calls.Add("ble"); return ble; }));
        Assert.Equal(["usb", "ble"], calls);
    }

    [Fact]
    public void Ble_candidate_needs_handshake()
    {
        var other = new FakeSerial("hello");
        var board = new FakeSerial("claudinho 1 cyd");
        var log = new List<string>();
        var finder = new BleFinder(() => true, _ => [other, board], log.Add, timeoutMs: 300);

        Assert.Same(board, finder.Find());
        Assert.True(other.Disposed);
        Assert.False(board.Disposed);
        Assert.Contains("?\n", board.Written);
        Assert.Empty(log);
    }

    [Fact]
    public void Air_line_drops_only_project()
    {
        const string line = "{\"v\":1,\"t\":1763870000,\"tz\":-180,\"h5\":[23.5,1763880000],\"d7\":[41,1764300000],"
                          + "\"ctx\":37,\"tok\":152340,\"usd\":4.12,\"m\":\"Opus ção\",\"p\":\"monitor\"}\n";

        var air = Ble.ForAir(line);

        Assert.EndsWith("}\n", air);
        var sent = JsonNode.Parse(air)!.AsObject();
        var full = JsonNode.Parse(line)!.AsObject();
        Assert.False(sent.ContainsKey("p"));
        Assert.Equal(["v", "t", "tz", "h5", "d7", "ctx", "tok", "usd", "m"], sent.Select(kv => kv.Key));
        foreach (var (key, value) in sent) Assert.True(JsonNode.DeepEquals(full[key], value), key);
        Assert.Contains("Opus ção", air);           // acento cru, não ç

        Assert.Equal("?\n", Ble.ForAir("?\n"));
    }

    [Fact]
    public void Chunks_cover_line_in_order()
    {
        var data = Encoding.UTF8.GetBytes(new string('x', 300) + "ção\n");
        foreach (var size in new[] { 20, 182, 244, 1000 })
        {
            var chunks = Ble.Chunks(data, size).ToList();
            Assert.All(chunks, c => Assert.InRange(c.Length, 1, size));
            Assert.Equal(data, chunks.SelectMany(c => c).ToArray());
        }
    }

    [Fact]
    public void Unavailable_bluetooth_logs_once()
    {
        var log = new List<string>();
        var scans = 0;
        var finder = new BleFinder(() => false, _ => { scans++; return []; }, log.Add);

        for (var i = 0; i < 3; i++) Assert.Null(finder.Find());

        Assert.Equal(0, scans);
        Assert.Equal(["bluetooth indisponível"], log);
    }

    [Fact]
    public void Ble_failure_reason_logged_when_it_changes()
    {
        var log = new List<string>();
        var reasons = new Queue<string?>(["0x80070016 A", "0x80070016 A", "0x80070079 B", null, "0x80070079 B"]);
        var finder = new BleFinder(() => true, fail =>
        {
            var r = reasons.Dequeue();
            if (r is null) return [new FakeSerial("claudinho 1 cyd")];  // conectou: zera o último motivo
            fail(r);
            return [null];
        }, log.Add, timeoutMs: 300);

        for (var i = 0; i < 5; i++) finder.Find();

        Assert.Equal(["ble falhou: 0x80070016 A", "ble falhou: 0x80070079 B", "ble falhou: 0x80070079 B"], log);
    }
}
