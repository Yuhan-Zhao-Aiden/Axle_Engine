namespace Axle.Net;

// High-level client: composes ITransport, HandshakeManager, and PingTracker.
// Usage:
//   var client = new NetClient(new UdpTransport());
//   client.Connect(new NetEndpoint("127.0.0.1", 7777));
//   while (running) { client.Tick(tickIndex++); }
//   client.Dispose();
public sealed class NetClient : IDisposable
{
    private readonly ITransport _transport;
    private readonly HandshakeManager _handshake = new();
    private readonly PingTracker _ping = new();
    private readonly Queue<SnapshotData> _snapshots = new();

    private NetEndpoint _server;
    private bool _disposed;

    public ConnectionState State => _handshake.State;

    // -1 until the first round-trip is measured.
    public long LatestRttMs => _ping.LatestRttMs;

    public int AssignedEntityIndex   => _handshake.AssignedEntityIndex;
    public int AssignedEntityVersion => _handshake.AssignedEntityVersion;

    public NetClient(ITransport transport)
    {
        _transport = transport;
    }

    public void Connect(NetEndpoint server, int localPort = 0, ulong initialTick = 0)
    {
        _server = server;
        _transport.Start(localPort);
        _handshake.BeginConnect(_transport, server, initialTick);
    }

    public void Tick(ulong tickIndex)
    {
        _handshake.Tick(tickIndex);

        while (_transport.TryReceive(out var packet))
        {
            var type = Packet.TryReadType(packet.Payload);
            if (type is null) continue; // malformed 

            _handshake.OnPacket(packet);

            if (type == PacketType.Pong && State == ConnectionState.Connected)
                _ping.OnPong(packet.Payload);

            if (type == PacketType.Snapshot && State == ConnectionState.Connected)
            {
                if (Packet.TryReadSnapshot(packet.Payload, out var snap))
                    _snapshots.Enqueue(snap with { ReceivedMs = Environment.TickCount64 });
            }
        }

        if (State == ConnectionState.Connected)
            _ping.Tick(_transport, _server, tickIndex);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _transport.Stop();
    }

    /// <summary>
    /// Send an input state packet to the server.
    /// </summary>
    public void SendInput(InputState state)
    {
        if (State != ConnectionState.Connected) return;
        Packet.WriteInputState(_transport, _server, state);
    }

    /// <summary>
    /// Dequeues the next snapshot received from the server.
    /// Returns false when the queue is empty.
    /// </summary>
    public bool TryDequeueSnapshot(out SnapshotData data)
    {
        if (_snapshots.Count > 0)
        {
            data = _snapshots.Dequeue();
            return true;
        }
        data = default;
        return false;
    }
}
