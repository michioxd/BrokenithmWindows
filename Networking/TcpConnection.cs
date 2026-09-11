using System.Net.Sockets;

namespace BrokenithmWindows.Networking;

public sealed class TcpConnection : IBrokenithmConnection
{
    private readonly TcpClient _client = new() { NoDelay = true };
    private NetworkStream? _stream;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        await _client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
        _stream = _client.GetStream();
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken) =>
        _stream!.WriteAsync(packet, cancellationToken);

    public async ValueTask<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        var stream = _stream!;
        byte[] prefix = new byte[1];
        await stream.ReadExactlyAsync(prefix, cancellationToken).ConfigureAwait(false);
        if (prefix[0] < 3)
            throw new InvalidDataException("The host sent an invalid packet length.");
        byte[] packet = new byte[prefix[0] + 1];
        packet[0] = prefix[0];
        await stream.ReadExactlyAsync(packet.AsMemory(1), cancellationToken).ConfigureAwait(false);
        return packet;
    }

    public ValueTask DisposeAsync()
    {
        _client.Dispose();
        return ValueTask.CompletedTask;
    }
}
