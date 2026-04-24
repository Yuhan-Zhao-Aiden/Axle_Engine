namespace Axle.Net;

// High-level server: composes ITransport, accepts incoming connections, echoes pings.
// Clients are tracked by their NetEndpoint. Duplicate ConnectRequests from the same
// endpoint are safely ignored.
// Usage:
//   var server = new NetServer(new UdpTransport());
//   server.Listen(7777);
//   while (running) { server.Tick(); }
//   server.Dispose();
public sealed class NetServer : IDisposable
{
    private readonly ITransport _transport;
    private readonly List<NetEndpoint> _connectedPeers = [];
    private readonly Dictionary<NetEndpoint, BufferedInput> _inputBuffers = new();
    private readonly Dictionary<NetEndpoint, (int Index, int Version)> _clientEntities = new();

    private bool _disposed;

    public IReadOnlyList<NetEndpoint> ConnectedPeers => _connectedPeers;
    public IReadOnlyDictionary<NetEndpoint, BufferedInput> InputBuffers => _inputBuffers;
    public IReadOnlyDictionary<NetEndpoint, (int Index, int Version)> ClientEntities => _clientEntities;

    public Func<NetEndpoint, (int Index, int Version)>? OnClientConnected { get; set; }

    public NetServer(ITransport transport)
    {
        _transport = transport;
    }

    public void Listen(int port)
    {
        _transport.Start(port);
        Console.WriteLine($"[Server] Listening on port {port}");
    }

    // Drain the receive queue and respond to handshake + ping packets.
    public void Tick()
    {
        while (_transport.TryReceive(out var packet))
        {
            var type = Packet.TryReadType(packet.Payload);
            if (type is null) continue; 

            switch (type)
            {
                case PacketType.ConnectRequest:
                    OnConnectRequest(packet);
                    break;

                case PacketType.Ping:
                    OnPing(packet);
                    break;

                case PacketType.InputState:
                    OnInputState(packet);
                    break;
            }
        }
    }

    private void OnConnectRequest(TransportPacket packet)
    {
        if (_connectedPeers.Contains(packet.Source))
            return;

        _connectedPeers.Add(packet.Source);

        (int Index, int Version) assigned = OnClientConnected != null
            ? OnClientConnected(packet.Source)
            : (0, 0);
        _clientEntities[packet.Source] = assigned;

        Packet.WriteConnectAccept(_transport, packet.Source, assigned.Index, assigned.Version);
        Console.WriteLine($"[Server] Client connected: {packet.Source.Host}:{packet.Source.Port} → entity {assigned.Index}");
    }

    private void OnPing(TransportPacket packet)
    {
        if (!Packet.TryReadPingPong(packet.Payload, out int seqId, out long sentMs))
            return;

        Packet.WritePong(_transport, packet.Source, seqId, sentMs);
    }

    private void OnInputState(TransportPacket packet)
    {
        if (!_connectedPeers.Contains(packet.Source)) return;

        if (!Packet.TryReadInputState(packet.Payload, out InputState state)) return;

        if (_inputBuffers.TryGetValue(packet.Source, out BufferedInput buf))
        {
            // Reject stale or duplicate packets.
            if (buf.HasInput && state.Seq <= buf.LatestSeq) return;
        }

        _inputBuffers[packet.Source] = new BufferedInput
        {
            LatestSeq   = state.Seq,
            LatestState = state,
            HasInput    = true,
        };
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _transport.Stop();
    }

    public void Broadcast(ReadOnlySpan<byte> payload)
    {
        foreach (var peer in _connectedPeers)
            _transport.Send(peer, payload);
    }

    // Serialises and broadcasts a snapshot packet to all connected peers.
    public void BroadcastSnapshot(ulong tickId, ushort ackSeq, ReadOnlySpan<SnapshotEntry> entries)
    {
        Span<byte> buf = stackalloc byte[Packet.SnapshotMaxBytes(entries.Length)];
        if (Packet.TryWriteSnapshot(buf, tickId, ackSeq, entries, out int written))
            Broadcast(buf[..written]);
    }

    // Serialises and sends a snapshot to a single peer with its own ackSeq.
    public void SendSnapshotTo(NetEndpoint peer, ulong tickId, ushort ackSeq, ReadOnlySpan<SnapshotEntry> entries)
    {
        Span<byte> buf = stackalloc byte[Packet.SnapshotMaxBytes(entries.Length)];
        if (Packet.TryWriteSnapshot(buf, tickId, ackSeq, entries, out int written))
            _transport.Send(peer, buf[..written]);
    }
}
