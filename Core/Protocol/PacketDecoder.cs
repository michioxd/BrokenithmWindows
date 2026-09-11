using System.Buffers.Binary;

namespace BrokenithmWindows.Core.Protocol;

public readonly record struct LedColor(byte Red, byte Green, byte Blue);

public static class PacketDecoder
{
    public static bool IsValid(ReadOnlySpan<byte> packet) =>
        packet.Length >= 4 && packet[0] >= 3 && packet.Length == packet[0] + 1;

    public static LedColor[]? Leds(ReadOnlySpan<byte> packet)
    {
        if (!IsValid(packet) || packet.Length != 100 || !packet.Slice(1, 3).SequenceEqual("LED"u8))
            return null;
        var colors = new LedColor[32];
        for (int i = 0; i < 32; i++)
        {
            int offset = 4 + (31 - i) * 3;
            colors[i] = new(packet[offset + 1], packet[offset + 2], packet[offset]);
        }
        return colors;
    }

    public static long? Pong(ReadOnlySpan<byte> packet) =>
        IsValid(packet) && packet.Length == 12 && packet.Slice(1, 3).SequenceEqual("PON"u8)
            ? BinaryPrimitives.ReadInt64BigEndian(packet[4..])
            : null;
}
