using Axle.Net;

namespace Axle.Net.Test;

public sealed class HandshakeTest
{
    private static readonly NetEndpoint ServerEndpoint = new("127.0.0.1", 7777);

    private static TransportPacket MakePacket(PacketType type, NetEndpoint source = default)
        => new() { Source = source, Payload = [(byte)type] };

    // --- State transitions ---

    [Fact]
    public void BeginConnect_SetsConnecting()
    {
        var transport = new FakeTransport();
        var hs = new HandshakeManager();

        hs.BeginConnect(transport, ServerEndpoint, tickIndex: 0);

        Assert.Equal(ConnectionState.Connecting, hs.State);
    }

    [Fact]
    public void BeginConnect_SendsConnectRequest()
    {
        var transport = new FakeTransport();
        var hs = new HandshakeManager();

        hs.BeginConnect(transport, ServerEndpoint, tickIndex: 0);

        Assert.Single(transport.Sent);
        Assert.Equal((byte)PacketType.ConnectRequest, transport.Sent[0].Payload[0]);
    }

    [Fact]
    public void OnPacket_ConnectAccept_SetsConnected()
    {
        var transport = new FakeTransport();
        var hs = new HandshakeManager();
        hs.BeginConnect(transport, ServerEndpoint, tickIndex: 0);

        hs.OnPacket(MakePacket(PacketType.ConnectAccept));

        Assert.Equal(ConnectionState.Connected, hs.State);
    }

    [Fact]
    public void OnPacket_ConnectReject_SetsDisconnected()
    {
        var transport = new FakeTransport();
        var hs = new HandshakeManager();
        hs.BeginConnect(transport, ServerEndpoint, tickIndex: 0);

        hs.OnPacket(MakePacket(PacketType.ConnectReject));

        Assert.Equal(ConnectionState.Disconnected, hs.State);
    }

    // --- Timeout ---

    [Fact]
    public void Tick_BeforeTimeout_StaysConnecting()
    {
        var transport = new FakeTransport();
        var hs = new HandshakeManager();
        hs.BeginConnect(transport, ServerEndpoint, tickIndex: 0);

        hs.Tick(tickIndex: 89); // one tick before threshold

        Assert.Equal(ConnectionState.Connecting, hs.State);
    }

    [Fact]
    public void Tick_AtTimeout_SetsTimedOut()
    {
        var transport = new FakeTransport();
        var hs = new HandshakeManager();
        hs.BeginConnect(transport, ServerEndpoint, tickIndex: 0);

        hs.Tick(tickIndex: 90); // threshold = 90 ticks

        Assert.Equal(ConnectionState.TimedOut, hs.State);
    }

    // --- Malformed / ignored packets ---

    [Fact]
    public void OnPacket_EmptyPayload_StateUnchanged()
    {
        var transport = new FakeTransport();
        var hs = new HandshakeManager();
        hs.BeginConnect(transport, ServerEndpoint, tickIndex: 0);

        var badPacket = new TransportPacket { Source = ServerEndpoint, Payload = [] };
        hs.OnPacket(badPacket);

        Assert.Equal(ConnectionState.Connecting, hs.State);
    }

    [Fact]
    public void OnPacket_UnknownByte_StateUnchanged()
    {
        var transport = new FakeTransport();
        var hs = new HandshakeManager();
        hs.BeginConnect(transport, ServerEndpoint, tickIndex: 0);

        var badPacket = new TransportPacket { Source = ServerEndpoint, Payload = [0xFF] };
        hs.OnPacket(badPacket);

        Assert.Equal(ConnectionState.Connecting, hs.State);
    }

    [Fact]
    public void OnPacket_WhenAlreadyConnected_StateUnchanged()
    {
        var transport = new FakeTransport();
        var hs = new HandshakeManager();
        hs.BeginConnect(transport, ServerEndpoint, tickIndex: 0);
        hs.OnPacket(MakePacket(PacketType.ConnectAccept));

        // Second accept should have no effect
        hs.OnPacket(MakePacket(PacketType.ConnectAccept));

        Assert.Equal(ConnectionState.Connected, hs.State);
    }
}
