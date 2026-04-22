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

    private bool _disposed;

    public IReadOnlyList<NetEndpoint> ConnectedPeers => _connectedPeers;
    public IReadOnlyDictionary<NetEndpoint, BufferedInput> InputBuffers => _inputBuffers;

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
        Packet.WriteConnectAccept(_transport, packet.Source);
        Console.WriteLine($"[Server] Client connected: {packet.Source.Host}:{packet.Source.Port}");
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
}
