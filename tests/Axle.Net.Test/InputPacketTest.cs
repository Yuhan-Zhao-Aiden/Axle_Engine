using System.Buffers.Binary;
using Axle.Net;

namespace Axle.Net.Test;

public sealed class InputPacketTest
{
    private static readonly NetEndpoint Peer = new("127.0.0.1", 7777);

    // -----------------------------------------------------------------------
    // Round-trip: WriteInputState → TryReadInputState
    // -----------------------------------------------------------------------

    [Fact]
    public void WriteInputState_ThenTryRead_RoundTripsSeqAndButtons()
    {
        var transport = new FakeTransport();
        var state = new InputState { Seq = 42, Buttons = (ushort)(InputButtons.Right | InputButtons.Up) };

        Packet.WriteInputState(transport, Peer, state);

        Assert.Single(transport.Sent);
        byte[] payload = transport.Sent[0].Payload;

        Assert.True(Packet.TryReadInputState(payload, out InputState read));
        Assert.Equal(state.Seq, read.Seq);
        Assert.Equal(state.Buttons, read.Buttons);
    }

    [Fact]
    public void WriteInputState_FirstByteIsInputStatePacketType()
    {
        var transport = new FakeTransport();
        Packet.WriteInputState(transport, Peer, new InputState { Seq = 1, Buttons = 0 });

        Assert.Equal((byte)PacketType.InputState, transport.Sent[0].Payload[0]);
    }

    [Fact]
    public void WriteInputState_PayloadIsExactlyFiveBytes()
    {
        var transport = new FakeTransport();
        Packet.WriteInputState(transport, Peer, new InputState());

        Assert.Equal(5, transport.Sent[0].Payload.Length);
    }

    // -----------------------------------------------------------------------
    // TryReadInputState — failure cases
    // -----------------------------------------------------------------------

    [Fact]
    public void TryReadInputState_TruncatedPayload_ReturnsFalse()
    {
        // Only 4 bytes instead of the required 5.
        byte[] truncated = [(byte)PacketType.InputState, 0, 1, 0];
        Assert.False(Packet.TryReadInputState(truncated, out _));
    }

    [Fact]
    public void TryReadInputState_EmptyPayload_ReturnsFalse()
    {
        Assert.False(Packet.TryReadInputState([], out _));
    }

    [Fact]
    public void TryReadInputState_WrongPacketType_StillReadsBytes()
    {
        // TryReadInputState reads from byte offsets 1..4 regardless of byte 0.
        // We verify the data bytes are correctly decoded even if the caller
        // does not pre-validate PacketType (caller responsibility to match first).
        var transport = new FakeTransport();
        Packet.WriteInputState(transport, Peer, new InputState { Seq = 7, Buttons = (ushort)InputButtons.Down });
        byte[] payload = transport.Sent[0].Payload;

        // Corrupt type byte
        payload[0] = (byte)PacketType.Ping;

        // TryReadInputState does not validate the type byte itself —
        // TryReadType + switch in the server does that.
        Assert.True(Packet.TryReadInputState(payload, out InputState read));
        Assert.Equal(7, read.Seq);
    }

    // -----------------------------------------------------------------------
    // Seq encoding uses little-endian ushort
    // -----------------------------------------------------------------------

    [Fact]
    public void WriteInputState_SeqEncodedLittleEndian()
    {
        var transport = new FakeTransport();
        Packet.WriteInputState(transport, Peer, new InputState { Seq = 0x0102, Buttons = 0 });

        byte[] payload = transport.Sent[0].Payload;
        ushort decoded = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(1));
        Assert.Equal(0x0102, decoded);
    }
}
