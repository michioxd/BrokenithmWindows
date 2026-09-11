using System.Net;
using System.Net.Sockets;
using BrokenithmWindows.Core.Protocol;

namespace BrokenithmWindows.Networking;

public sealed class UdpConnection(int receivePort = 52468) : IBrokenithmConnection
{
    private readonly UdpClient _sender = new(AddressFamily.InterNetwork);
    private UdpClient? _receiver;
    private IPEndPoint? _remote;

    public async Task ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, cancellationToken)
            .ConfigureAwait(false);
        var address =
            addresses.FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork)
            ?? throw new IOException("UDP requires a host with an IPv4 address.");
        _remote = new(address, port);
        _sender.Connect(_remote);
        _receiver = new UdpClient(AddressFamily.InterNetwork);
        _receiver.Client.ExclusiveAddressUse = true;
        _receiver.Client.Bind(new IPEndPoint(IPAddress.Any, receivePort));
        var local = (IPEndPoint)_sender.Client.LocalEndPoint!;
        await SendAsync(PacketEncoder.Connect(local.Address, receivePort), cancellationToken)
            .ConfigureAwait(false);
    }

    public async ValueTask SendAsync(
        ReadOnlyMemory<byte> packet,
        CancellationToken cancellationToken
    ) => _ = await _sender.SendAsync(packet, cancellationToken).ConfigureAwait(false);

    public async ValueTask<byte[]> ReceiveAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            var result = await _receiver!.ReceiveAsync(cancellationToken).ConfigureAwait(false);
            if (result.RemoteEndPoint.Equals(_remote))
                return result.Buffer;
        }
    }

    public ValueTask DisposeAsync()
    {
        _sender.Dispose();
        _receiver?.Dispose();
        return ValueTask.CompletedTask;
    }
}
