namespace Claudinho.Tests;

sealed class FakeSerial(params string[] replies) : ISerial
{
    readonly Queue<string> pending = new(replies);
    public List<string> Written { get; } = [];
    public bool FailWrites { get; set; }
    public bool Disposed { get; private set; }

    public void Write(string text)
    {
        if (FailWrites) throw new IOException("O dispositivo não está conectado");
        Written.Add(text);
    }

    public string? ReadLine(int timeoutMs) => pending.Count > 0 ? pending.Dequeue() : null;
    public void Dispose() => Disposed = true;
}

public class BridgeTests
{
    static readonly DateTime T0 = new(2026, 11, 23, 12, 0, 0, DateTimeKind.Utc);

    sealed class Rig
    {
        public string? Snap = "{\"v\":1,\"t\":1}";
        public FakeSerial? Next = new();
        public int Connects;
        public List<string> Log = [];
        public Bridge Bridge;

        public Rig() => Bridge = new Bridge(() => Snap, () => { Connects++; return Next; }, Log.Add);
        public FakeSerial Port => Next!;
    }

    [Fact]
    public void Second_instance_exits_zero()
    {
        var name = $@"Local\Claudinho.Test.{Guid.NewGuid():N}";
        using var held = new Mutex(true, name);
        var ticks = 0;
        var bridge = new Bridge(() => { ticks++; return null; }, () => null, _ => { });

        var run = Task.Run(() => BridgeHost.Run(name, Path.GetTempFileName(), bridge, () => T0, () => { }));

        Assert.True(run.Wait(TimeSpan.FromSeconds(1)));
        Assert.Equal(0, run.Result);
        Assert.Equal(0, ticks);
        held.ReleaseMutex();
    }

    [Fact]
    public void Picks_port_by_vid_and_handshake()
    {
        Assert.Equal(3000, Board.HandshakeTimeoutMs);
        var opened = new List<string>();
        var ports = new Dictionary<string, FakeSerial>
        {
            ["COM8"] = new("claudinho 1 cyd"),             // Bluetooth: sem VID USB
            ["COM5"] = new("claudinho 1 cyd"),             // outro VID
            ["COM3"] = new("ets Jun  8 2016", "rst:0x1", "claudinho 1 cyd"), // CYD: log de boot antes da resposta
            ["COM9"] = new("claudinho 1 amoled216"),
        };
        ISerial? Open(string p) { opened.Add(p); return ports[p]; }

        var picked = Board.Pick([("COM8", ""), ("COM5", "10C4"), ("COM3", "1A86"), ("COM9", "303A")], Open);
        Assert.Same(ports["COM3"], picked);
        Assert.Equal(["COM3"], opened);
        Assert.Contains("?\n", ports["COM3"].Written);

        // resposta errada na CH340: fecha e passa para o ESP32-S3
        opened.Clear();
        ports["COM3"] = new FakeSerial("hello");
        picked = Board.Pick([("COM3", "1a86"), ("COM9", "303A")], Open, timeoutMs: 300);
        Assert.Same(ports["COM9"], picked);
        Assert.True(ports["COM3"].Disposed);

        Assert.Null(Board.Pick([("COM8", ""), ("COM5", "10C4")], Open));
    }

    [Fact]
    public void Sends_line_when_snapshot_changes()
    {
        var r = new Rig();
        r.Bridge.Tick(T0);
        r.Snap = "{\"v\":1,\"t\":2}";
        r.Bridge.Tick(T0.AddMilliseconds(250));

        Assert.Equal(["{\"v\":1,\"t\":1}\n", "{\"v\":1,\"t\":2}\n"], r.Port.Written);
    }

    [Fact]
    public void Resends_every_60_seconds()
    {
        var r = new Rig();
        r.Bridge.Tick(T0);
        r.Bridge.Tick(T0.AddSeconds(59));
        Assert.Single(r.Port.Written);

        r.Bridge.Tick(T0.AddSeconds(60));
        Assert.Equal(2, r.Port.Written.Count);
    }

    [Fact]
    public void Sends_snapshot_after_connect()
    {
        var r = new Rig();
        var board = r.Next;
        r.Next = null;                              // placa ainda não plugada
        r.Bridge.Tick(T0);
        Assert.Equal(1, r.Connects);

        r.Next = board;                             // plugou
        r.Bridge.Tick(T0.AddSeconds(5));
        Assert.Equal(2, r.Connects);
        Assert.Equal(["{\"v\":1,\"t\":1}\n"], r.Port.Written); // no mesmo tick da conexão
    }

    [Fact]
    public void Retries_after_port_loss()
    {
        var r = new Rig();
        Assert.True(r.Bridge.Tick(T0));
        var lost = r.Port;
        lost.FailWrites = true;
        r.Snap = "{\"v\":1,\"t\":2}";

        Assert.True(r.Bridge.Tick(T0.AddSeconds(1)));
        Assert.Contains(r.Log, l => l.Contains("porta perdida"));
        Assert.True(lost.Disposed);

        r.Next = new FakeSerial();
        Assert.True(r.Bridge.Tick(T0.AddSeconds(5.9)));
        Assert.Equal(1, r.Connects);                // ainda dentro dos 5 s

        Assert.True(r.Bridge.Tick(T0.AddSeconds(6)));
        Assert.Equal(2, r.Connects);
        Assert.Equal(["{\"v\":1,\"t\":2}\n"], r.Port.Written);
    }

    [Fact]
    public void Exits_after_30_minutes_idle()
    {
        var r = new Rig();
        Assert.True(r.Bridge.Tick(T0));
        Assert.True(r.Bridge.Tick(T0.AddMinutes(29)));
        Assert.False(r.Port.Disposed);

        Assert.False(r.Bridge.Tick(T0.AddMinutes(30)));
        Assert.True(r.Port.Disposed);
    }

    [Fact]
    public void Truncates_log_over_1mb()
    {
        var big = Path.GetTempFileName();
        var small = Path.GetTempFileName();
        File.WriteAllBytes(big, new byte[BridgeHost.MaxLog + 1]);
        File.WriteAllText(small, "linha\n");

        BridgeHost.TruncateLog(big);
        BridgeHost.TruncateLog(small);

        Assert.Equal(1_048_576, BridgeHost.MaxLog);
        Assert.Equal(0, new FileInfo(big).Length);
        Assert.Equal("linha\n", File.ReadAllText(small));
    }
}
