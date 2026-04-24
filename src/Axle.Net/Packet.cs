using System.Buffers.Binary;
using Axle.Core.AxleMath;

namespace Axle.Net;

// Wire format:
//   All packets: [PacketType: 1 byte] [payload...]
//   ConnectRequest/Accept/Reject: no payload (1 byte total)
//   Ping / Pong: [PacketType: 1][seqId: int32 LE 4][sentMs: int64 LE 8] = 13 bytes
//   InputState: [PacketType: 1][seq: ushort LE 2][buttons: ushort LE 2] = 5 bytes
//   Snapshot: [PacketType: 1][tickId: ulong LE 8][ackSeq: ushort LE 2]
//                   [count: ushort LE 2][entries...] = 13 bytes header
//     Entry: [entityIndex: int32 LE 4][entityVersion: int32 LE 4][mask: byte 1]
//                   [posX: int32 LE 4 if bit0][posY: int32 LE 4 if bit1] = 9–17 bytes each

internal static class Packet
{
    private const int PingPongLength = 13;
    private const int InputStateLength = 5;

    public static void WriteConnectRequest(ITransport transport, NetEndpoint endpoint)
    {
        Span<byte> buf = stackalloc byte[1];
        buf[0] = (byte)PacketType.ConnectRequest;
        transport.Send(endpoint, buf);
    }

    // ConnectAccept wire format (9 bytes): [type:1][entityIndex:i32 LE][entityVersion:i32 LE]
    public static void WriteConnectAccept(ITransport transport, NetEndpoint endpoint, int entityIndex, int entityVersion)
    {
        Span<byte> buf = stackalloc byte[9];
        buf[0] = (byte)PacketType.ConnectAccept;
        BinaryPrimitives.WriteInt32LittleEndian(buf[1..], entityIndex);
        BinaryPrimitives.WriteInt32LittleEndian(buf[5..], entityVersion);
        transport.Send(endpoint, buf);
    }

    public static bool TryReadConnectAccept(byte[] payload, out int entityIndex, out int entityVersion)
    {
        entityIndex = -1;
        entityVersion = -1;
        if (payload is not { Length: >= 9 }) return false;
        entityIndex = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(1));
        entityVersion = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(5));
        return true;
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

    public static void WriteInputState(ITransport transport, NetEndpoint endpoint, InputState state)
    {
        Span<byte> buf = stackalloc byte[InputStateLength];
        buf[0] = (byte)PacketType.InputState;
        BinaryPrimitives.WriteUInt16LittleEndian(buf[1..], state.Seq);
        BinaryPrimitives.WriteUInt16LittleEndian(buf[3..], state.Buttons);
        transport.Send(endpoint, buf);
    }

    public static bool TryReadInputState(byte[] payload, out InputState state)
    {
        state = default;
        if (payload is not { Length: >= InputStateLength }) return false;
        state.Seq     = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(1));
        state.Buttons = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(3));
        return true;
    }

    // --- Snapshot ---

    private const int SnapshotHeaderSize = 13; // 1 + 8 + 2 + 2
    private const int SnapshotEntryBase  = 9;  // 4 (index) + 4 (version) + 1 (mask)
    private const int SnapshotEntryField = 4;  // 4 bytes per optional position field

    // Writes a snapshot packet into caller-provided buf; returns false if buf is too small.
    public static bool TryWriteSnapshot(
        Span<byte> buf,
        ulong tickId,
        ushort ackSeq,
        ReadOnlySpan<SnapshotEntry> entries,
        out int written)
    {
        written = 0;

        // compute required size.
        int needed = SnapshotHeaderSize;
        foreach (var e in entries)
        {
            needed += SnapshotEntryBase;
            if ((e.Mask & ChangeMask.PositionX) != 0) needed += SnapshotEntryField;
            if ((e.Mask & ChangeMask.PositionY) != 0) needed += SnapshotEntryField;
        }
        if (buf.Length < needed) return false;

        buf[0] = (byte)PacketType.Snapshot;
        BinaryPrimitives.WriteUInt64LittleEndian(buf[1..], tickId);
        BinaryPrimitives.WriteUInt16LittleEndian(buf[9..], ackSeq);
        BinaryPrimitives.WriteUInt16LittleEndian(buf[11..], (ushort)entries.Length);

        int offset = SnapshotHeaderSize;
        foreach (var e in entries)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buf[offset..], e.EntityIndex);
            offset += 4;
            BinaryPrimitives.WriteInt32LittleEndian(buf[offset..], e.EntityVersion);
            offset += 4;
            buf[offset] = (byte)e.Mask;
            offset += 1;

            if ((e.Mask & ChangeMask.PositionX) != 0)
            {
                BinaryPrimitives.WriteInt32LittleEndian(buf[offset..], e.PositionX.RawValue);
                offset += 4;
            }
            if ((e.Mask & ChangeMask.PositionY) != 0)
            {
                BinaryPrimitives.WriteInt32LittleEndian(buf[offset..], e.PositionY.RawValue);
                offset += 4;
            }
        }

        written = offset;
        return true;
    }

    // Computes the maximum byte count for a snapshot with the given entry count
    public static int SnapshotMaxBytes(int entryCount)
        => SnapshotHeaderSize + entryCount * (SnapshotEntryBase + SnapshotEntryField * 2);

    public static bool TryReadSnapshot(byte[] payload, out SnapshotData data)
    {
        data = default;
        if (payload is not { Length: >= SnapshotHeaderSize }) return false;

        ulong tickId = BinaryPrimitives.ReadUInt64LittleEndian(payload.AsSpan(1));
        ushort ackSeq = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(9));
        ushort count = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(11));

        var entries = new SnapshotEntry[count];
        int offset = SnapshotHeaderSize;

        for (int i = 0; i < count; i++)
        {
            if (payload.Length - offset < SnapshotEntryBase) return false;

            int entityIndex = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset));
            offset += 4;
            int entityVersion = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset));
            offset += 4;
            var mask = (ChangeMask)payload[offset];
            offset += 1;

            Fixed32 posX = Fixed32.Zero;
            Fixed32 posY = Fixed32.Zero;

            if ((mask & ChangeMask.PositionX) != 0)
            {
                if (payload.Length - offset < 4) return false;
                posX = Fixed32.FromRaw(BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset)));
                offset += 4;
            }
            if ((mask & ChangeMask.PositionY) != 0)
            {
                if (payload.Length - offset < 4) return false;
                posY = Fixed32.FromRaw(BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(offset)));
                offset += 4;
            }

            entries[i] = new SnapshotEntry
            {
                EntityIndex = entityIndex,
                EntityVersion = entityVersion,
                Mask = mask,
                PositionX = posX,
                PositionY = posY,
            };
        }

        data = new SnapshotData
        {
            TickId = tickId,
            AckSeq = ackSeq,
            Entries = entries,
        };
        return true;
    }
}
