using System.Buffers.Binary;

namespace Axle.Net;

// Wire format:
//   All packets: [PacketType: 1 byte] [payload...]
//   ConnectRequest/Accept/Reject: no payload (1 byte total)
//   Ping / Pong: [PacketType: 1][seqId: int32 LE 4][sentMs: int64 LE 8] = 13 bytes total

internal static class Packet
{
    private const int PingPongLength = 13;

    public static void WriteConnectRequest(ITransport transport, NetEndpoint endpoint)
    {
        Span<byte> buf = stackalloc byte[1];
        buf[0] = (byte)PacketType.ConnectRequest;
        transport.Send(endpoint, buf);
    }

    public static void WriteConnectAccept(ITransport transport, NetEndpoint endpoint)
    {
        Span<byte> buf = stackalloc byte[1];
        buf[0] = (byte)PacketType.ConnectAccept;
        transport.Send(endpoint, buf);
    }

    public static void WriteConnectReject(ITransport transport, NetEndpoint endpoint)
    {
        Span<byte> buf = stackalloc byte[1];
        buf[0] = (byte)PacketType.ConnectReject;
        transport.Send(endpoint, buf);
    }

    public static void WritePing(ITransport transport, NetEndpoint endpoint, int seqId, long sentMs)
    {
        Span<byte> buf = stackalloc byte[PingPongLength];
        buf[0] = (byte)PacketType.Ping;
        BinaryPrimitives.WriteInt32LittleEndian(buf[1..], seqId);
        BinaryPrimitives.WriteInt64LittleEndian(buf[5..], sentMs);
        transport.Send(endpoint, buf);
    }

    public static void WritePong(ITransport transport, NetEndpoint endpoint, int seqId, long sentMs)
    {
        Span<byte> buf = stackalloc byte[PingPongLength];
        buf[0] = (byte)PacketType.Pong;
        BinaryPrimitives.WriteInt32LittleEndian(buf[1..], seqId);
        BinaryPrimitives.WriteInt64LittleEndian(buf[5..], sentMs);
        transport.Send(endpoint, buf);
    }

    // Returns null if payload is empty or the type byte is unrecognised.
    public static PacketType? TryReadType(byte[] payload)
    {
        if (payload is not { Length: > 0 }) return null;
        var type = (PacketType)payload[0];
        return Enum.IsDefined(type) ? type : null;
    }

    // Reads the seqId and sentMs fields from a Ping or Pong payload.
    public static bool TryReadPingPong(byte[] payload, out int seqId, out long sentMs)
    {
        seqId = 0;
        sentMs = 0;
        if (payload is not { Length: >= PingPongLength }) return false;
        seqId = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1));
        sentMs = BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(5));
        return true;
    }
}
