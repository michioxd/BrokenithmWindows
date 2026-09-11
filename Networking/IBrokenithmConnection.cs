namespace BrokenithmWindows.Networking;

public interface IBrokenithmConnection : IAsyncDisposable
{
    Task ConnectAsync(string host, int port, CancellationToken cancellationToken);
    ValueTask SendAsync(ReadOnlyMemory<byte> packet, CancellationToken cancellationToken);
    ValueTask<byte[]> ReceiveAsync(CancellationToken cancellationToken);
}
