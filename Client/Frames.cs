using System.Buffers.Binary;

namespace TestbenchClient;

public sealed record Sample(byte Sequence, uint Timestamp, ushort Reading);

public static class Frames
{
    public static ushort Crc(ReadOnlySpan<byte> payload)
    {
        ushort remainder = 0xffff;
        foreach (byte octet in payload)
        {
            remainder ^= (ushort)(octet << 8);
            for (int bit = 0; bit < 8; bit++)
                remainder = (ushort)((remainder & 0x8000) != 0
                    ? (remainder << 1) ^ 0x1021 : remainder << 1);
        }
        return remainder;
    }

    public static byte[] Request(byte sequence)
    {
        byte[] frame = [0xd3, 1, sequence, 1, 0, 0];
        BinaryPrimitives.WriteUInt16LittleEndian(frame.AsSpan(4), Crc(frame.AsSpan(0, 4)));
        return frame;
    }

    public static Sample ParseReply(string hexLine)
    {
        byte[] frame = Convert.FromHexString(hexLine);
        if (frame.Length != 12)
            throw new FormatException("Reply length must be 12 bytes");
        if (frame[0] != 0xd3 || frame[1] != 1)
            throw new FormatException("Unsupported reply header");
        if (BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(10)) != Crc(frame.AsSpan(0, 10)))
            throw new FormatException("Reply CRC mismatch");
        if (frame[3] != 0)
            throw new FormatException($"Device status {frame[3]}");
        return new Sample(frame[2], BinaryPrimitives.ReadUInt32LittleEndian(frame.AsSpan(4)),
            BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(8)));
    }
}
