using System;
using System.Buffers.Binary;
using System.Net;

namespace VTStudioToolBox.Network;

public class StunAttribute
{
    public ushort Type { get; set; }
    public ushort Length { get; set; }
    public byte[] Value { get; set; } = Array.Empty<byte>();

    public int RealLength => Length == 0 ? 0 : 4 + Length + (4 - Length % 4) % 4;

    public int WriteTo(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16BigEndian(buffer, Type);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[2..], Length);
        Value.CopyTo(buffer[4..]);
        int total = 4 + Length;
        return total + (4 - total % 4) % 4;
    }

    // RFC 3489 style parsing (no XOR)
    public int TryParse(ReadOnlySpan<byte> data)
    {
        if (data.Length < 4) return 0;

        Type = BinaryPrimitives.ReadUInt16BigEndian(data);
        Length = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);

        if (data.Length < 4 + Length) return 0;

        Value = data[4..(4 + Length)].ToArray();

        return 4 + Length + (4 - Length % 4) % 4;
    }

    // RFC 5389 style parsing - stores raw value, XOR decoding deferred to GetXorEndPoint
    public int TryParseRfc5389(ReadOnlySpan<byte> data, ReadOnlySpan<byte> magicCookieAndTxId)
    {
        if (data.Length < 4) return 0;

        Type = BinaryPrimitives.ReadUInt16BigEndian(data);
        Length = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);

        if (data.Length < 4 + Length) return 0;

        Value = data[4..(4 + Length)].ToArray();

        return 4 + Length + (4 - Length % 4) % 4;
    }

    // Parse plain IP address attribute (Type 1, 5) - RFC 3489
    public IPEndPoint? GetEndPoint()
    {
        if (Value.Length < 8) return null;

        byte family = Value[1];
        ushort port = BinaryPrimitives.ReadUInt16BigEndian(Value[2..]);

        if (family == 1 && Value.Length >= 8)
        {
            IPAddress ip = new IPAddress(Value[4..8]);
            return new IPEndPoint(ip, port);
        }

        if (family == 2 && Value.Length >= 20)
        {
            IPAddress ip = new IPAddress(Value[4..20]);
            return new IPEndPoint(ip, port);
        }

        return null;
    }

    // Parse XOR-encoded address attribute (Type 0x0020) - RFC 5389
    public IPEndPoint? GetXorEndPoint(uint magicCookie, byte[] transactionId)
    {
        if (Value.Length < 8) return null;

        byte family = Value[1];
        ushort encodedPort = BinaryPrimitives.ReadUInt16BigEndian(Value[2..]);
        ushort port = (ushort)(encodedPort ^ (ushort)(magicCookie >> 16));

        if (family == 1 && Value.Length >= 8)
        {
            Span<byte> addrBytes = stackalloc byte[4];
            for (int i = 0; i < 4; i++)
                addrBytes[i] = (byte)(Value[4 + i] ^ (magicCookie >> (i * 8)) & 0xFF);

            // XOR with magic cookie bytes (big-endian: 0x21, 0x12, 0xA4, 0x42)
            addrBytes[0] = (byte)(Value[4] ^ 0x21);
            addrBytes[1] = (byte)(Value[5] ^ 0x12);
            addrBytes[2] = (byte)(Value[6] ^ 0xA4);
            addrBytes[3] = (byte)(Value[7] ^ 0x42);

            IPAddress ip = new IPAddress(addrBytes);
            return new IPEndPoint(ip, port);
        }

        if (family == 2 && Value.Length >= 20)
        {
            Span<byte> addrBytes = stackalloc byte[16];
            Value[4..20].CopyTo(addrBytes);

            // XOR with magic cookie + transaction ID
            Span<byte> key = stackalloc byte[16];
            BinaryPrimitives.WriteUInt32BigEndian(key, magicCookie);
            transactionId.CopyTo(key[4..]);

            for (int i = 0; i < 16; i++)
                addrBytes[i] ^= key[i];

            IPAddress ip = new IPAddress(addrBytes);
            return new IPEndPoint(ip, port);
        }

        return null;
    }

    public static StunAttribute BuildChangeRequest(bool changeIp, bool changePort)
    {
        return new StunAttribute
        {
            Type = 0x0003, // CHANGE-REQUEST
            Length = 4,
            Value = new byte[] { 0, 0, 0, (byte)((changeIp ? 4 : 0) | (changePort ? 2 : 0)) }
        };
    }
}
