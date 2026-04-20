using System.Buffers.Binary;
using Axle.Net;

namespace Axle.Net.Test;

public sealed class PingTrackerTest
{
    private static readonly NetEndpoint Peer = new("127.0.0.1", 7777);

    // Build a Pong payload from a previously captured Ping payload
    // (flip byte[0] to PacketType.Pong, keep seqId + sentMs unchanged).
    private static byte[] PongFromPing(byte[] pingPayload)
    {
        var pong = (byte[])pingPayload.Clone();
        pong[0] = (byte)PacketType.Pong;
        return pong;
    }

    [Fact]
    public void LatestRttMs_InitialValue_IsMinusOne()
    {
        var tracker = new PingTracker();
        Assert.Equal(-1, tracker.LatestRttMs);
    }

    [Fact]
    public void OnPong_MatchingSeqId_SetsNonNegativeRtt()
    {
        var transport = new FakeTransport();
        var tracker = new PingTracker();

        tracker.Tick(transport, Peer, tickIndex: 0);
        Assert.Single(transport.Sent); // a Ping was sent

        var pongPayload = PongFromPing(transport.Sent[0].Payload);
        tracker.OnPong(pongPayload);

        Assert.True(tracker.LatestRttMs >= 0, "RTT should be non-negative after a matched pong");
    }

    [Fact]
    public void OnPong_WrongSeqId_KeepsMinusOne()
    {
        var transport = new FakeTransport();
        var tracker = new PingTracker();

        tracker.Tick(transport, Peer, tickIndex: 0);

        // Corrupt the seqId in the pong
        var pongPayload = PongFromPing(transport.Sent[0].Payload);
        BinaryPrimitives.WriteInt32LittleEndian(pongPayload.AsSpan(1), 999);
        tracker.OnPong(pongPayload);

        Assert.Equal(-1, tracker.LatestRttMs);
    }

    [Fact]
    public void OnPong_TruncatedPayload_Ignored()
    {
        var tracker = new PingTracker();

        // Should not throw; RTT stays -1
        tracker.OnPong([]);
        tracker.OnPong([(byte)PacketType.Pong, 0x01]);

        Assert.Equal(-1, tracker.LatestRttMs);
    }

    [Fact]
    public void Tick_RespectsCadence_DoesNotSendBeforeInterval()
    {
        var transport = new FakeTransport();
        var tracker = new PingTracker();

        tracker.Tick(transport, Peer, tickIndex: 0);
        tracker.Tick(transport, Peer, tickIndex: 1);  // too soon
        tracker.Tick(transport, Peer, tickIndex: 14); // still too soon

        Assert.Single(transport.Sent); // only the first tick fires
    }

    [Fact]
    public void Tick_AtNextInterval_SendsSecondPing()
    {
        var transport = new FakeTransport();
        var tracker = new PingTracker();

        tracker.Tick(transport, Peer, tickIndex: 0);
        tracker.Tick(transport, Peer, tickIndex: 15); // exactly at interval

        Assert.Equal(2, transport.Sent.Count);
    }
}
