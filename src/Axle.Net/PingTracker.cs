namespace Axle.Net;

// Tracks outgoing pings and computes RTT from matching pong replies.
internal sealed class PingTracker
{
    private const int PingIntervalTicks = 15;

    private int _nextSeqId;
    private int _pendingSeqId = -1;
    private long _pendingSentMs;
    private ulong _lastPingTick;
    private bool _hasSentPing;

    public long LatestRttMs { get; private set; } = -1;

    public void Tick(ITransport transport, NetEndpoint endpoint, ulong tickIndex)
    {
        if (_hasSentPing && tickIndex - _lastPingTick < PingIntervalTicks)
            return;

        int seqId = _nextSeqId++;
        long nowMs = Environment.TickCount64;
        Packet.WritePing(transport, endpoint, seqId, nowMs);

        _pendingSeqId = seqId;
        _pendingSentMs = nowMs;
        _lastPingTick = tickIndex;
        _hasSentPing = true;
    }

    // Call when a Pong packet is received; ignores sequence mismatches silently.
    public void OnPong(byte[] payload)
    {
        if (!Packet.TryReadPingPong(payload, out int seqId, out _))
            return;
        if (seqId != _pendingSeqId) return;

        LatestRttMs = Environment.TickCount64 - _pendingSentMs;
        _pendingSeqId = -1;
    }
}
