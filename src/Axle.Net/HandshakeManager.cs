namespace Axle.Net;

// Client-side handshake state machine.
// Call BeginConnect() to initiate, Tick() each engine tick, and OnPacket() for every
// received packet. State transitions:
//   Disconnected → (BeginConnect) → Connecting
//   Connecting → ConnectAccept → Connected
//   Connecting → ConnectReject → Disconnected
//   Connecting → timeout → TimedOut
internal sealed class HandshakeManager
{
    private const ulong TimeoutTicks = 90; 

    private ulong _connectRequestedAt;

    public ConnectionState State { get; private set; } = ConnectionState.Disconnected;

    public int AssignedEntityIndex   { get; private set; } = -1;
    public int AssignedEntityVersion { get; private set; } = -1;

    public void BeginConnect(ITransport transport, NetEndpoint server, ulong tickIndex)
    {
        State = ConnectionState.Connecting;
        _connectRequestedAt = tickIndex;
        Packet.WriteConnectRequest(transport, server);
    }

    // Drive timeout detection — call once per tick.
    public void Tick(ulong tickIndex)
    {
        if (State == ConnectionState.Connecting &&
            tickIndex - _connectRequestedAt >= TimeoutTicks)
        {
            State = ConnectionState.TimedOut;
        }
    }

    // Process an incoming packet that may advance the state machine.
    public void OnPacket(TransportPacket packet)
    {
        if (State != ConnectionState.Connecting) return;

        var type = Packet.TryReadType(packet.Payload);
        if (type == PacketType.ConnectAccept)
        {
            Packet.TryReadConnectAccept(packet.Payload, out int idx, out int ver);
            AssignedEntityIndex   = idx;
            AssignedEntityVersion = ver;
            State = ConnectionState.Connected;
        }
        else if (type == PacketType.ConnectReject)
        {
            State = ConnectionState.Disconnected;
        }
    }
}
