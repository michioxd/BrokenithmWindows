using System.Buffers.Binary;
using System.Net;
using BrokenithmWindows.Core.Models;

namespace BrokenithmWindows.Core.Protocol;

public static class PacketEncoder
{
    private static readonly int[] AirOrder = [4, 5, 2, 3, 0, 1];

    public static int Input(Span<byte> buffer, InputState input, uint sequence, bool air, bool tcp)
    {
        buffer[..48].Clear();
        buffer[0] = air ? (byte)47 : (byte)41;
        (air ? "INP"u8 : "IPT"u8).CopyTo(buffer[1..]);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[4..], sequence);
        if (air)
            for (int i = Math.Clamp(input.AirHeight, 0, 6); i < 6; i++)
                buffer[8 + AirOrder[i]] = 1;
        int offset = air ? 14 : 8;
        for (int i = 0; i < 32; i++)
            buffer[offset + 31 - i] = (input.Slider & (1u << i)) != 0 ? (byte)0x80 : (byte)0;
        buffer[offset + 32] = input.Test ? (byte)1 : (byte)0;
        buffer[offset + 33] = input.Service ? (byte)1 : (byte)0;
        return tcp ? 48 : buffer[0] + 1;
    }

    public static byte[] Connect(IPAddress localAddress, int receivePort)
    {
        byte[] ip = localAddress.GetAddressBytes();
        if (ip.Length != 4)
            throw new ArgumentException("UDP requires IPv4.");
        byte[] packet = new byte[21];
        packet[0] = 10;
        "CON"u8.CopyTo(packet.AsSpan(1));
        packet[4] = 1;
        BinaryPrimitives.WriteUInt16BigEndian(packet.AsSpan(5), checked((ushort)receivePort));
        ip.CopyTo(packet, 7);
        return packet;
    }

    public static byte[] Disconnect() => [3, (byte)'D', (byte)'I', (byte)'S'];

    public static byte[] Function(bool card) =>
        [4, (byte)'F', (byte)'N', (byte)'C', card ? (byte)2 : (byte)1];

    public static void Ping(Span<byte> packet, long nanoseconds)
    {
        packet[0] = 11;
        "PIN"u8.CopyTo(packet[1..]);
        BinaryPrimitives.WriteInt64BigEndian(packet[4..], nanoseconds);
    }

    public static byte[] Card(bool present, bool felica, ReadOnlySpan<byte> id, bool tcp)
    {
        if (present && id.Length != (felica ? 8 : 10))
            throw new ArgumentException("Invalid card ID length.");
        byte[] packet = new byte[tcp ? 24 : 16];
        packet[0] = 15;
        "CRD"u8.CopyTo(packet.AsSpan(1));
        packet[4] = present ? (byte)1 : (byte)0;
        packet[5] = felica ? (byte)1 : (byte)0;
        if (present)
            id.CopyTo(packet.AsSpan(6));
        return packet;
    }
}
