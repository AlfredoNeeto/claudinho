using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Radios;
using Windows.Foundation;

namespace Claudinho;

// Link BLE (plano ble-link): Nordic UART Service, mesmo fluxo de bytes da serial (door 1).
internal static class Ble
{
    public static readonly Guid Service = new("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
    public static readonly Guid Rx = new("6E400002-B5A3-F393-E0A9-E50E24DCCA9E");
    public static readonly Guid Tx = new("6E400003-B5A3-F393-E0A9-E50E24DCCA9E");
    public static readonly TimeSpan ScanWindow = TimeSpan.FromSeconds(5);
    const int OpTimeoutMs = 10_000;

    static readonly JsonSerializerOptions Json = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // Door 3: o nome da pasta do projeto não vai pelo ar; o resto da linha v1 fica igual.
    // Linha que não é um objeto JSON vira nada: a placa a descartaria mesmo, e nada sai do PC sem passar por aqui.
    public static string ForAir(string text)
    {
        if (text == "?\n") return text;
        try
        {
            if (JsonNode.Parse(text) is JsonObject line)
            {
                line.Remove("p");
                return line.ToJsonString(Json) + "\n";
            }
        }
        catch (JsonException) { }
        return "";
    }

    public static IEnumerable<byte[]> Chunks(byte[] data, int size)
    {
        for (var i = 0; i < data.Length; i += size) yield return data[i..Math.Min(i + size, data.Length)];
    }

    public static bool Available()
    {
        try
        {
            var adapter = Wait(BluetoothAdapter.GetDefaultAsync());
            if (adapter is null || !adapter.IsLowEnergySupported) return false;
            return Wait(adapter.GetRadioAsync())?.State == RadioState.On;
        }
        catch (Exception e) when (e is COMException or OperationCanceledException or UnauthorizedAccessException) { return false; }
    }

    // Abre cada aparelho que anuncia o serviço, enquanto a janela de busca durar; quem consome para no primeiro que serve.
    public static IEnumerable<ISerial?> Scan(Action<string> fail)
    {
        var found = new BlockingCollection<ulong>();
        var seen = new ConcurrentDictionary<ulong, bool>();
        var watcher = new BluetoothLEAdvertisementWatcher();
        watcher.AdvertisementFilter.Advertisement.ServiceUuids.Add(Service);
        watcher.Received += (_, e) => { if (seen.TryAdd(e.BluetoothAddress, true)) found.Add(e.BluetoothAddress); };
        watcher.Start();
        try
        {
            var deadline = Environment.TickCount64 + (long)ScanWindow.TotalMilliseconds;
            while (true)
            {
                var left = deadline - Environment.TickCount64;
                if (left <= 0 || !found.TryTake(out var address, (int)left)) yield break;
                yield return BleLink.TryOpen(address, fail);
            }
        }
        finally
        {
            watcher.Stop();
        }
    }

    // Uncached pega a tabela nova depois de regravar o firmware, mas no Windows 11 26300 dá 0x80070016 com a CYD;
    // Cached lê da placa quando não há cache, então serve de reserva.
    public static T Gatt<T>(Func<BluetoothCacheMode, IAsyncOperation<T>> op)
    {
        try { return Wait(op(BluetoothCacheMode.Uncached)); }
        catch (COMException) { return Wait(op(BluetoothCacheMode.Cached)); }
    }

    public static T Wait<T>(IAsyncOperation<T> op)
    {
        using var cts = new CancellationTokenSource(OpTimeoutMs);
        return op.AsTask(cts.Token).GetAwaiter().GetResult();
    }
}

internal sealed class BleFinder(Func<bool> available, Func<Action<string>, IEnumerable<ISerial?>> scan, Action<string> log,
                                int timeoutMs = Board.HandshakeTimeoutMs)
{
    bool warned;
    string? lastFail;

    public ISerial? Find()
    {
        if (!available())
        {
            if (!warned) log("bluetooth indisponível");
            warned = true;
            return null;
        }
        var link = Board.FirstConfirmed(scan(Failed), timeoutMs);
        if (link is not null) lastFail = null;
        return link;
    }

    // A busca repete a cada 5 s: o mesmo motivo só entra no log uma vez seguida.
    void Failed(string reason)
    {
        if (reason == lastFail) return;
        lastFail = reason;
        log($"ble falhou: {reason}");
    }
}

internal sealed class BleLink : ISerial
{
    readonly BluetoothLEDevice dev;
    readonly GattDeviceService svc;
    readonly GattSession session;
    readonly GattCharacteristic rx, tx;
    readonly BlockingCollection<string> lines = new();
    readonly StringBuilder partial = new();
    volatile bool lost;

    public string Name { get; }

    BleLink(BluetoothLEDevice dev, GattDeviceService svc, GattSession session, GattCharacteristic rx, GattCharacteristic tx)
    {
        (this.dev, this.svc, this.session, this.rx, this.tx) = (dev, svc, session, rx, tx);
        Name = "ble " + string.Join(":", BitConverter.GetBytes(dev.BluetoothAddress).Take(6).Reverse().Select(b => b.ToString("X2")));
        tx.ValueChanged += OnNotify;
        dev.ConnectionStatusChanged += OnStatus;
    }

    public static ISerial? TryOpen(ulong address, Action<string> fail)
    {
        BluetoothLEDevice? dev = null;
        try
        {
            dev = Ble.Wait(BluetoothLEDevice.FromBluetoothAddressAsync(address));
            if (dev is null) return null;
            var svc = Ble.Gatt(m => dev.GetGattServicesForUuidAsync(Ble.Service, m)).Services.FirstOrDefault();
            if (svc is null) { dev.Dispose(); return null; }
            var rx = Ble.Gatt(m => svc.GetCharacteristicsForUuidAsync(Ble.Rx, m)).Characteristics.FirstOrDefault();
            var tx = Ble.Gatt(m => svc.GetCharacteristicsForUuidAsync(Ble.Tx, m)).Characteristics.FirstOrDefault();
            var session = Ble.Wait(GattSession.FromDeviceIdAsync(dev.BluetoothDeviceId));
            if (rx is null || tx is null) { session.Dispose(); svc.Dispose(); dev.Dispose(); return null; }
            var link = new BleLink(dev, svc, session, rx, tx);
            if (Ble.Wait(tx.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify))
                != GattCommunicationStatus.Success)
            {
                link.Dispose();
                return null;
            }
            return link;
        }
        catch (Exception e) when (e is COMException or OperationCanceledException or UnauthorizedAccessException or ObjectDisposedException)
        {
            fail($"0x{e.HResult:X8} {e.Message}");
            dev?.Dispose();
            return null;
        }
    }

    void OnNotify(GattCharacteristic _, GattValueChangedEventArgs e)
    {
        lock (partial)
        {
            partial.Append(Encoding.UTF8.GetString(e.CharacteristicValue.ToArray()));
            for (var nl = partial.ToString().IndexOf('\n'); nl >= 0; nl = partial.ToString().IndexOf('\n'))
            {
                lines.Add(partial.ToString(0, nl).TrimEnd('\r'));
                partial.Remove(0, nl + 1);
            }
        }
    }

    // shortcut: a queda só é notada na próxima escrita (mudança ou reenvio de 60 s); se a placa reinicia, a tela espera até lá.
    void OnStatus(BluetoothLEDevice d, object _)
    {
        if (d.ConnectionStatus == BluetoothConnectionStatus.Disconnected) lost = true;
    }

    public void Write(string text)
    {
        if (lost) throw new IOException("ble desconectado");
        var chunk = Math.Max(20, session.MaxPduSize - 3);
        try
        {
            foreach (var part in Ble.Chunks(Encoding.UTF8.GetBytes(Ble.ForAir(text)), chunk))
            {
                var r = Ble.Wait(rx.WriteValueWithResultAsync(part.AsBuffer(), GattWriteOption.WriteWithResponse));
                if (r.Status != GattCommunicationStatus.Success) throw new IOException($"ble {r.Status}");
            }
        }
        catch (Exception e) when (e is COMException or OperationCanceledException or ObjectDisposedException)
        {
            throw new IOException($"ble {e.Message}", e);
        }
    }

    public string? ReadLine(int timeoutMs) => lines.TryTake(out var line, timeoutMs) ? line : null;

    public void Dispose()
    {
        tx.ValueChanged -= OnNotify;
        dev.ConnectionStatusChanged -= OnStatus;
        session.Dispose();
        svc.Dispose();
        dev.Dispose();
    }
}
