using System.Buffers.Binary;
using Axle.Core.AxleMath;
using Axle.Net;

namespace Axle.Net.Test;

public sealed class SnapshotPacketTest
{
    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static SnapshotEntry MakeEntry(
        int idx = 0, int ver = 0,
        bool hasX = true, bool hasY = true,
        int rawX = 3 * 65536, int rawY = 7 * 65536)
    {
        var mask = ChangeMask.None;
        if (hasX) mask |= ChangeMask.PositionX;
        if (hasY) mask |= ChangeMask.PositionY;
        return new SnapshotEntry
        {
            EntityIndex   = idx,
            EntityVersion = ver,
            Mask          = mask,
            PositionX     = Fixed32.FromRaw(hasX ? rawX : 0),
            PositionY     = Fixed32.FromRaw(hasY ? rawY : 0),
        };
    }

    // -----------------------------------------------------------------------
    // Round-trip
    // -----------------------------------------------------------------------

    [Fact]
    public void WriteRead_SingleEntry_RoundTrips()
    {
        var entry = MakeEntry(idx: 5, ver: 1, rawX: 3 * 65536, rawY: 7 * 65536);
        Span<byte> buf = stackalloc byte[Packet.SnapshotMaxBytes(1)];

        Assert.True(Packet.TryWriteSnapshot(buf, tickId: 42, ackSeq: 9, [entry], out int written));

        byte[] payload = buf[..written].ToArray();
        Assert.True(Packet.TryReadSnapshot(payload, out var data));

        Assert.Equal(42UL, data.TickId);
        Assert.Equal((ushort)9, data.AckSeq);
        Assert.Single(data.Entries);
        Assert.Equal(5, data.Entries[0].EntityIndex);
        Assert.Equal(1, data.Entries[0].EntityVersion);
        Assert.Equal(3 * 65536, data.Entries[0].PositionX.RawValue);
        Assert.Equal(7 * 65536, data.Entries[0].PositionY.RawValue);
    }

    [Fact]
    public void WriteRead_ZeroEntries_RoundTrips()
    {
        Span<byte> buf = stackalloc byte[Packet.SnapshotMaxBytes(0)];
        Assert.True(Packet.TryWriteSnapshot(buf, tickId: 1, ackSeq: 0, [], out int written));

        byte[] payload = buf[..written].ToArray();
        Assert.True(Packet.TryReadSnapshot(payload, out var data));

        Assert.Equal(1UL, data.TickId);
        Assert.Empty(data.Entries);
    }

    [Fact]
    public void WriteRead_MultipleEntries_AllRoundTrip()
    {
        var entries = new[]
        {
            MakeEntry(idx: 0, ver: 0, rawX: 1 * 65536, rawY: 2 * 65536),
            MakeEntry(idx: 1, ver: 0, rawX: 3 * 65536, rawY: 4 * 65536),
            MakeEntry(idx: 2, ver: 1, rawX: 5 * 65536, rawY: 6 * 65536),
        };
        Span<byte> buf = stackalloc byte[Packet.SnapshotMaxBytes(3)];
        Assert.True(Packet.TryWriteSnapshot(buf, tickId: 99, ackSeq: 77, entries, out int written));

        byte[] payload = buf[..written].ToArray();
        Assert.True(Packet.TryReadSnapshot(payload, out var data));

        Assert.Equal(99UL, data.TickId);
        Assert.Equal((ushort)77, data.AckSeq);
        Assert.Equal(3, data.Entries.Length);
        for (int i = 0; i < 3; i++)
        {
            Assert.Equal(i, data.Entries[i].EntityIndex);
            Assert.Equal((2 * i + 1) * 65536, data.Entries[i].PositionX.RawValue);
            Assert.Equal((2 * i + 2) * 65536, data.Entries[i].PositionY.RawValue);
        }
    }

    // -----------------------------------------------------------------------
    // Wire format
    // -----------------------------------------------------------------------

    [Fact]
    public void Write_FirstByteIsSnapshotPacketType()
    {
        Span<byte> buf = stackalloc byte[Packet.SnapshotMaxBytes(0)];
        Packet.TryWriteSnapshot(buf, 0, 0, [], out _);
        Assert.Equal((byte)PacketType.Snapshot, buf[0]);
    }

    [Fact]
    public void Write_TickIdAtOffset1_LittleEndian()
    {
        ulong tickId = 0x0102_0304_0506_0708UL;
        Span<byte> buf = stackalloc byte[Packet.SnapshotMaxBytes(0)];
        Packet.TryWriteSnapshot(buf, tickId, ackSeq: 0, [], out _);

        Assert.Equal(tickId, BinaryPrimitives.ReadUInt64LittleEndian(buf[1..]));
    }

    [Fact]
    public void Write_AckSeqAtOffset9_LittleEndian()
    {
        ushort ackSeq = 0x1234;
        Span<byte> buf = stackalloc byte[Packet.SnapshotMaxBytes(0)];
        Packet.TryWriteSnapshot(buf, tickId: 0, ackSeq, [], out _);

        Assert.Equal(ackSeq, BinaryPrimitives.ReadUInt16LittleEndian(buf[9..]));
    }

    [Fact]
    public void Write_CountAtOffset11_LittleEndian()
    {
        var entry = MakeEntry();
        Span<byte> buf = stackalloc byte[Packet.SnapshotMaxBytes(1)];
        Packet.TryWriteSnapshot(buf, 0, 0, [entry], out _);

        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(buf[11..]));
    }

    // -----------------------------------------------------------------------
    // ChangeMask variants
    // -----------------------------------------------------------------------

    [Fact]
    public void ChangeMask_OnlyX_EntryIsFourBytesSmallerThanBoth()
    {
        Span<byte> bufX  = stackalloc byte[Packet.SnapshotMaxBytes(1)];
        Span<byte> bufXY = stackalloc byte[Packet.SnapshotMaxBytes(1)];

        Packet.TryWriteSnapshot(bufX,  0, 0, [MakeEntry(hasX: true,  hasY: false)], out int wX);
        Packet.TryWriteSnapshot(bufXY, 0, 0, [MakeEntry(hasX: true,  hasY: true)],  out int wXY);

        Assert.Equal(wXY - 4, wX); // Y field is 4 bytes
    }

    [Fact]
    public void ChangeMask_None_PayloadIsHeaderPlusBaseEntryOnly()
    {
        Span<byte> buf = stackalloc byte[Packet.SnapshotMaxBytes(1)];
        Packet.TryWriteSnapshot(buf, 0, 0, [MakeEntry(hasX: false, hasY: false)], out int written);

        // 13 header + 9 base (4 entityIndex + 4 entityVersion + 1 mask) = 22
        Assert.Equal(22, written);
    }

    // -----------------------------------------------------------------------
    // Failure / boundary cases
    // -----------------------------------------------------------------------

    [Fact]
    public void TryWriteSnapshot_BufferTooSmall_ReturnsFalse()
    {
        Span<byte> tiny = stackalloc byte[5]; // header alone needs 13
        Assert.False(Packet.TryWriteSnapshot(tiny, 0, 0, [], out _));
    }

    [Fact]
    public void TryReadSnapshot_TruncatedPayload_ReturnsFalse()
    {
        // Only 5 bytes — header requires ≥ 13.
        byte[] truncated = [(byte)PacketType.Snapshot, 0, 1, 2, 3];
        Assert.False(Packet.TryReadSnapshot(truncated, out _));
    }

    [Fact]
    public void TryReadSnapshot_TruncatedEntryBody_ReturnsFalse()
    {
        // Write a valid packet then shorten it by cutting off mid-entry.
        var entry = MakeEntry();
        Span<byte> buf = stackalloc byte[Packet.SnapshotMaxBytes(1)];
        Packet.TryWriteSnapshot(buf, 1, 0, [entry], out int written);
        byte[] truncated = buf[..(written - 3)].ToArray(); // cut 3 bytes off the end

        Assert.False(Packet.TryReadSnapshot(truncated, out _));
    }

    // -----------------------------------------------------------------------
    // SnapshotMaxBytes
    // -----------------------------------------------------------------------

    [Fact]
    public void SnapshotMaxBytes_ZeroEntries_Returns13()
    {
        Assert.Equal(13, Packet.SnapshotMaxBytes(0));
    }

    [Fact]
    public void SnapshotMaxBytes_OneEntry_Returns30()
    {
        // 13 header + 9 base + 4 (posX) + 4 (posY) = 30
        Assert.Equal(30, Packet.SnapshotMaxBytes(1));
    }
}
