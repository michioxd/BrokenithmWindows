using System.Diagnostics;
using System.Net.Sockets;
using BrokenithmWindows.Core.Input;
using BrokenithmWindows.Core.Models;
using BrokenithmWindows.Core.Protocol;
using BrokenithmWindows.Networking;
using BrokenithmWindows.Utilities;

namespace BrokenithmWindows.Services;

public sealed record ConnectionStatus(
    string Message,
    bool Active = false,
    bool Busy = false,
    string? Error = null
);

public sealed class BrokenithmService(InputService input, IAppLogger logger) : IAsyncDisposable
{
    private readonly SemaphoreSlim _lifecycle = new(1, 1),
        _send = new(1, 1);
    private CancellationTokenSource? _lifetime;
    private Task _session = Task.CompletedTask;
    private IBrokenithmConnection? _connection;
    private ConnectionStatus _status = new("Disconnected");
    private LedColor[]? _leds;
    private long _sent,
        _received;
    private double _delay = -1;
    private uint _nextSequence = 1;
    private bool _disposed;
    private bool _airEnabled = true;
    public ConnectionStatus Status => Volatile.Read(ref _status);
    public LedColor[]? Leds => Volatile.Read(ref _leds);
    public long Sent => Interlocked.Read(ref _sent);
    public long Received => Interlocked.Read(ref _received);
    public double Delay => Volatile.Read(ref _delay);
    public int AirHeight { get; private set; } = 6;
    public bool AirEnabled => Volatile.Read(ref _airEnabled);

    public void SetAirEnabled(bool enabled)
    {
        Volatile.Write(ref _airEnabled, enabled);
        if (!enabled)
            AirHeight = 6;
    }

    public async Task ConnectAsync(AppSettings settings)
    {
        settings.Validate();
        await _lifecycle.WaitAsync();
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await StopSessionAsync();
            input.Configure(settings.Input);
            SetAirEnabled(settings.Input.EnableAir);
            _sent = _received = 0;
            _delay = -1;
            _leds = null;
            _nextSequence = 1;
            _lifetime = new();
            _status = new("Connecting…", Busy: true);
            _session = Task.Run(() => RunAsync(settings, _lifetime.Token));
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    public async Task DisconnectAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            await StopSessionAsync();
            _status = new("Disconnected");
        }
        finally
        {
            _lifecycle.Release();
        }
    }

    private async Task StopSessionAsync()
    {
        _lifetime?.Cancel();
        input.Clear();
        await _session;
        _lifetime?.Dispose();
        _lifetime = null;
    }

    private async Task RunAsync(AppSettings settings, CancellationToken token)
    {
        await using IBrokenithmConnection connection = settings.Tcp
            ? new TcpConnection()
            : new UdpConnection();
        bool connected = false;
        using var loops = CancellationTokenSource.CreateLinkedTokenSource(token);
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(5));
            await connection.ConnectAsync(settings.Host, settings.Port, timeout.Token);
            connected = true;
            _connection = connection;
            _status = new(
                settings.Tcp ? "TCP connected" : "UDP active · awaiting host feedback",
                Active: true
            );
            Task sender = SendLoopAsync(connection, settings, loops.Token);
            Task receiver = ReceiveLoopAsync(connection, settings.Tcp, loops.Token);
            await Task.WhenAny(sender, receiver);
            loops.Cancel();
            await Task.WhenAll(sender, receiver);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex)
        {
            logger.Error("Connection session", ex);
            string message = ex switch
            {
                OperationCanceledException => "The connection timed out. Check the host and port.",
                SocketException { SocketErrorCode: SocketError.AddressAlreadyInUse } =>
                    "UDP receive port 52468 is in use. Close the other client and reconnect.",
                SocketException =>
                    "The host could not be reached or the connection was lost. Check the address, network and firewall.",
                EndOfStreamException => "The host closed the connection.",
                InvalidDataException => "The host sent an invalid protocol packet.",
                _ => "The connection failed. See debug output for details.",
            };
            _status = new("Disconnected", Error: message);
        }
        finally
        {
            loops.Cancel();
            _connection = null;
            input.Clear();
            AirHeight = 6;
            if (connected)
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
                try
                {
                    byte[] neutral = new byte[48];
                    int length = PacketEncoder.Input(
                        neutral,
                        InputState.Empty,
                        _nextSequence++,
                        AirEnabled,
                        settings.Tcp
                    );
                    await SendPacketAsync(connection, neutral.AsMemory(0, length), deadline.Token);
                    await SendPacketAsync(connection, PacketEncoder.Disconnect(), deadline.Token);
                }
                catch (Exception ex)
                {
                    logger.Error("Send final release/disconnect", ex);
                }
            }
            if (token.IsCancellationRequested)
                _status = new("Disconnected");
        }
    }

    private async Task SendLoopAsync(
        IBrokenithmConnection connection,
        AppSettings settings,
        CancellationToken token
    )
    {
        var air = new AirProcessor();
        byte[] packet = new byte[48],
            ping = new byte[12];
        byte[] card = PacketEncoder.Card(false, false, [], settings.Tcp);
        long lastPing = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            long now = Environment.TickCount64;
            if (settings.ShowLatency && now - lastPing >= 100)
            {
                PacketEncoder.Ping(ping, Nanoseconds());
                await SendPacketAsync(connection, ping, token);
                lastPing = now;
            }
            var state = input.Snapshot;
            bool airEnabled = AirEnabled;
            AirHeight = airEnabled ? air.Update(state.AirHeight, now) : 6;
            int length = PacketEncoder.Input(
                packet,
                state with
                {
                    AirHeight = AirHeight,
                },
                _nextSequence++,
                airEnabled,
                settings.Tcp
            );
            await SendPacketAsync(connection, packet.AsMemory(0, length), token);
            if (settings.SendEmptyCard)
                await SendPacketAsync(connection, card, token);
            await Task.Delay(1, token);
        }
    }

    private async Task ReceiveLoopAsync(
        IBrokenithmConnection connection,
        bool tcp,
        CancellationToken token
    )
    {
        while (true)
        {
            byte[] packet = await connection.ReceiveAsync(token);
            if (!PacketDecoder.IsValid(packet))
            {
                if (tcp)
                    throw new InvalidDataException();
                logger.Error("Ignore malformed UDP packet", new InvalidDataException());
                continue;
            }
            Interlocked.Increment(ref _received);
            var leds = PacketDecoder.Leds(packet);
            if (leds != null)
                Volatile.Write(ref _leds, leds);
            var echoed = PacketDecoder.Pong(packet);
            if (echoed is long timestamp)
            {
                double delay = (Nanoseconds() - timestamp) / 2_000_000.0;
                if (delay >= 0 && delay < 60_000)
                    Volatile.Write(ref _delay, delay);
            }
            if (!tcp)
                _status = new("UDP active - host feedback received", Active: true);
        }
    }

    private async ValueTask SendPacketAsync(
        IBrokenithmConnection connection,
        ReadOnlyMemory<byte> packet,
        CancellationToken token
    )
    {
        await _send.WaitAsync(token);
        try
        {
            await connection.SendAsync(packet, token);
            Interlocked.Increment(ref _sent);
        }
        finally
        {
            _send.Release();
        }
    }

    public async Task SendFunctionAsync(bool card)
    {
        var connection = _connection;
        if (connection == null || !Status.Active)
            throw new InvalidOperationException("Connect before sending a function key.");
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        await SendPacketAsync(connection, PacketEncoder.Function(card), timeout.Token);
    }

    private static long Nanoseconds() =>
        (long)(Stopwatch.GetTimestamp() * (1_000_000_000.0 / Stopwatch.Frequency));

    public async ValueTask DisposeAsync()
    {
        await _lifecycle.WaitAsync();
        try
        {
            _disposed = true;
            await StopSessionAsync();
            _status = new("Disconnected");
        }
        finally
        {
            _lifecycle.Release();
        }
    }
}
