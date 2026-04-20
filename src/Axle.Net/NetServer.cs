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

    private bool _disposed;

    public IReadOnlyList<NetEndpoint> ConnectedPeers => _connectedPeers;

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

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _transport.Stop();
    }
}
