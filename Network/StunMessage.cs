using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Security.Cryptography;

namespace VTStudioToolBox.Network;

public class StunMessage
{
    public const int HeaderLength = 20;
    public const uint Rfc5389MagicCookie = 0x2112A442;

    public ushort MessageType { get; set; }
    public uint MagicCookie { get; set; } = 0;
    public byte[] TransactionId { get; } = new byte[12];
    public List<StunAttribute> Attributes { get; set; } = new();

    public ushort MessageLength => (ushort)Attributes.Sum(a => a.RealLength);
    public int Length => HeaderLength + MessageLength;

    public StunMessage()
    {
        RandomNumberGenerator.Fill(TransactionId);
    }

    public int WriteTo(Span<byte> buffer)
    {
        BinaryPrimitives.WriteUInt16BigEndian(buffer, MessageType);
        BinaryPrimitives.WriteUInt16BigEndian(buffer[2..], MessageLength);
        BinaryPrimitives.WriteUInt32BigEndian(buffer[4..], MagicCookie);
        TransactionId.CopyTo(buffer[8..]);

        int offset = HeaderLength;
        foreach (var attr in Attributes)
            offset += attr.WriteTo(buffer[offset..]);
        return offset;
    }

    public bool TryParse(ReadOnlySpan<byte> data)
    {
        if (data.Length < HeaderLength) return false;

        MessageType = BinaryPrimitives.ReadUInt16BigEndian(data);
        ushort length = BinaryPrimitives.ReadUInt16BigEndian(data[2..]);
        MagicCookie = BinaryPrimitives.ReadUInt32BigEndian(data[4..]);

        if (data.Length < HeaderLength + length) return false;

        data[8..20].CopyTo(TransactionId);

        Attributes.Clear();
        var attrData = data[HeaderLength..(HeaderLength + length)];

        bool isRfc5389 = MagicCookie == Rfc5389MagicCookie;
        Span<byte> magicCookieAndTxId = stackalloc byte[16];
        data[4..20].CopyTo(magicCookieAndTxId);

        while (attrData.Length >= 4)
        {
            var attr = new StunAttribute();
            int consumed = isRfc5389
                ? attr.TryParseRfc5389(attrData, magicCookieAndTxId)
                : attr.TryParse(attrData);
            if (consumed <= 0) break;
            Attributes.Add(attr);
            attrData = attrData[consumed..];
        }

        return true;
    }

    public bool IsSameTransaction(StunMessage other)
        => MagicCookie == other.MagicCookie && TransactionId.AsSpan().SequenceEqual(other.TransactionId);

    // MAPPED-ADDRESS (Type 1)
    public IPEndPoint? GetMappedAddress()
    {
        var attr = Attributes.FirstOrDefault(a => a.Type == 0x0001);
        return attr?.GetEndPoint();
    }

    // CHANGED-ADDRESS (Type 5)
    public IPEndPoint? GetChangedAddress()
    {
        var attr = Attributes.FirstOrDefault(a => a.Type == 0x0005);
        return attr?.GetEndPoint();
    }

    // XOR-MAPPED-ADDRESS (Type 0x0020) - RFC 5389
    public IPEndPoint? GetXorMappedAddress()
    {
        var attr = Attributes.FirstOrDefault(a => a.Type == 0x0020);
        if (attr != null) return attr.GetXorEndPoint(MagicCookie, TransactionId);

        // Fallback to MAPPED-ADDRESS
        return GetMappedAddress();
    }

    // OTHER-ADDRESS (Type 0x802C) - RFC 5780
    public IPEndPoint? GetOtherAddress()
    {
        var attr = Attributes.FirstOrDefault(a => a.Type == 0x802C);
        if (attr != null) return attr.GetEndPoint();

        // Fallback to CHANGED-ADDRESS
        return GetChangedAddress();
    }
}
