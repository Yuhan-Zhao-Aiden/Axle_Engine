using Axle.Net;

namespace Axle.Net.Test;

public sealed class InputBufferTest
{
    private static readonly NetEndpoint Client1 = new("127.0.0.1", 10001);

    // Build a 5-byte InputState payload directly via Packet.WriteInputState.
    private static byte[] MakeInputPayload(ushort seq, InputButtons buttons)
    {
        var capture = new FakeTransport();
        Packet.WriteInputState(capture, Client1, new InputState { Seq = seq, Buttons = (ushort)buttons });
        return capture.Sent[0].Payload;
    }

    // Simulate a full handshake so the client endpoint is in _connectedPeers.
    private static NetServer ConnectClient(FakeTransport transport, NetEndpoint client)
    {
        var server = new NetServer(transport);
        server.Listen(7777);
        transport.Inbox.Enqueue(new TransportPacket
        {
            Source  = client,
            Payload = [(byte)PacketType.ConnectRequest],
        });
        server.Tick(); // processes ConnectRequest → adds client to peers
        return server;
    }

    // -----------------------------------------------------------------------

    [Fact]
    public void NoInputReceived_HasInputIsFalse()
    {
        var transport = new FakeTransport();
        var server    = ConnectClient(transport, Client1);

        // No InputState packets sent — buffer should default to HasInput=false.
        Assert.False(server.InputBuffers.TryGetValue(Client1, out var buf) && buf.HasInput);
    }

    [Fact]
    public void FirstInputPacket_BufferedWithHasInputTrue()
    {
        var transport = new FakeTransport();
        var server    = ConnectClient(transport, Client1);

        transport.Inbox.Enqueue(new TransportPacket
        {
            Source  = Client1,
            Payload = MakeInputPayload(seq: 0, InputButtons.Right),
        });
        server.Tick();

        Assert.True(server.InputBuffers[Client1].HasInput);
        Assert.Equal(0, server.InputBuffers[Client1].LatestSeq);
        Assert.Equal((ushort)InputButtons.Right, server.InputBuffers[Client1].LatestState.Buttons);
    }

    [Fact]
    public void NewerSeq_Accepted_ReplacesBuffer()
    {
        var transport = new FakeTransport();
        var server    = ConnectClient(transport, Client1);

        transport.Inbox.Enqueue(new TransportPacket
        {
            Source  = Client1,
            Payload = MakeInputPayload(seq: 5, InputButtons.Left),
        });
        server.Tick();

        transport.Inbox.Enqueue(new TransportPacket
        {
            Source  = Client1,
            Payload = MakeInputPayload(seq: 6, InputButtons.Right),
        });
        server.Tick();

        Assert.Equal(6, server.InputBuffers[Client1].LatestSeq);
        Assert.Equal((ushort)InputButtons.Right, server.InputBuffers[Client1].LatestState.Buttons);
    }

    [Fact]
    public void OlderSeq_Rejected_BufferUnchanged()
    {
        var transport = new FakeTransport();
        var server    = ConnectClient(transport, Client1);

        transport.Inbox.Enqueue(new TransportPacket
        {
            Source  = Client1,
            Payload = MakeInputPayload(seq: 10, InputButtons.Up),
        });
        server.Tick();

        // Deliver a stale packet (seq 8 < 10).
        transport.Inbox.Enqueue(new TransportPacket
        {
            Source  = Client1,
            Payload = MakeInputPayload(seq: 8, InputButtons.Down),
        });
        server.Tick();

        Assert.Equal(10, server.InputBuffers[Client1].LatestSeq);
        Assert.Equal((ushort)InputButtons.Up, server.InputBuffers[Client1].LatestState.Buttons);
    }

    [Fact]
    public void DuplicateSeq_Rejected_BufferUnchanged()
    {
        var transport = new FakeTransport();
        var server    = ConnectClient(transport, Client1);

        transport.Inbox.Enqueue(new TransportPacket
        {
            Source  = Client1,
            Payload = MakeInputPayload(seq: 3, InputButtons.Jump),
        });
        server.Tick();

        // Duplicate: same seq, different buttons.
        transport.Inbox.Enqueue(new TransportPacket
        {
            Source  = Client1,
            Payload = MakeInputPayload(seq: 3, InputButtons.Left),
        });
        server.Tick();

        Assert.Equal(3, server.InputBuffers[Client1].LatestSeq);
        Assert.Equal((ushort)InputButtons.Jump, server.InputBuffers[Client1].LatestState.Buttons);
    }

    [Fact]
    public void InputFromUnknownPeer_Ignored()
    {
        var transport = new FakeTransport();
        var server    = ConnectClient(transport, Client1);

        var unknown = new NetEndpoint("192.168.1.99", 5000);
        transport.Inbox.Enqueue(new TransportPacket
        {
            Source  = unknown,
            Payload = MakeInputPayload(seq: 1, InputButtons.Right),
        });
        server.Tick();

        Assert.False(server.InputBuffers.ContainsKey(unknown));
    }
}
