using Axle.Core.AxleMath;
using Axle.Ecs;
using Axle.Net;
using Axle.Server;
using Axle.Sim;

namespace Axle.Server.Test;

public sealed class SnapshotBroadcastSystemTest
{
    // Minimal ITransport that captures every Send call.
    private sealed class FakeTransport : ITransport
    {
        public List<(NetEndpoint Endpoint, byte[] Payload)> Sent { get; } = [];
        public Queue<TransportPacket> Inbox { get; } = new();

        public void Start(int port) { }
        public void Stop() { }

        public void Send(NetEndpoint endpoint, ReadOnlySpan<byte> payload)
            => Sent.Add((endpoint, payload.ToArray()));

        public bool TryReceive(out TransportPacket packet)
        {
            if (Inbox.Count > 0) { packet = Inbox.Dequeue(); return true; }
            packet = default;
            return false;
        }
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private static World CreateWorld(out EntityId player)
    {
        var world = new World();
        world.Register<SimPosition>();
        world.Register<Velocity>();
        world.Register<MoveInput>();
        world.Register<PlayerCollider>();
        world.Register<LocalPlayer>();

        player = world.CreateEntity();
        world.Add(player, new SimPosition(Fixed32.FromInt(10), Fixed32.FromInt(20)));
        world.Add(player, new Velocity(Fixed32.Zero, Fixed32.Zero));
        world.Add(player, new MoveInput(0, 0));
        world.Add<LocalPlayer>(player);
        return world;
    }

    // Creates a NetServer with one connected peer and clears the ConnectAccept noise.
    private static (NetServer server, FakeTransport transport) CreateConnectedServer()
    {
        var transport = new FakeTransport();
        var server    = new NetServer(transport);

        var peer = new NetEndpoint("127.0.0.1", 9000);
        transport.Inbox.Enqueue(new TransportPacket
        {
            Source  = peer,
            Payload = [(byte)PacketType.ConnectRequest],
        });
        server.Tick(); // process ConnectRequest → peer is now connected
        transport.Sent.Clear(); // discard ConnectAccept
        return (server, transport);
    }

    // -----------------------------------------------------------------------
    // Tick rate gating
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_Tick1And2_NoBroadcast()
    {
        var world = CreateWorld(out _);
        var (server, transport) = CreateConnectedServer();
        var sys = new SnapshotBroadcastSystem(server);

        sys.Run(world); // tick 1
        sys.Run(world); // tick 2

        Assert.Empty(transport.Sent);
    }

    [Fact]
    public void Run_Tick3_BroadcastsExactlyOnce()
    {
        var world = CreateWorld(out _);
        var (server, transport) = CreateConnectedServer();
        var sys = new SnapshotBroadcastSystem(server);

        sys.Run(world);
        sys.Run(world);
        sys.Run(world); // tick 3 — should broadcast

        Assert.Single(transport.Sent);
    }

    [Fact]
    public void Run_Tick3_PayloadStartsWithSnapshotType()
    {
        var world = CreateWorld(out _);
        var (server, transport) = CreateConnectedServer();
        var sys = new SnapshotBroadcastSystem(server);

        sys.Run(world); sys.Run(world); sys.Run(world);

        Assert.Equal((byte)PacketType.Snapshot, transport.Sent[0].Payload[0]);
    }

    [Fact]
    public void Run_Tick6_BroadcastsTwice()
    {
        var world = CreateWorld(out _);
        var (server, transport) = CreateConnectedServer();
        var sys = new SnapshotBroadcastSystem(server);

        for (int i = 0; i < 6; i++)
        {
            if (i == 3) transport.Sent.Clear(); // clear tick-3 broadcast
            sys.Run(world);
        }

        Assert.Empty(transport.Sent); // tick 6 — position unchanged → no entries → no send
    }

    // -----------------------------------------------------------------------
    // Snapshot content
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_Tick3_PlayerPositionInPayload()
    {
        var world = CreateWorld(out _);
        var (server, transport) = CreateConnectedServer();
        var sys = new SnapshotBroadcastSystem(server);

        sys.Run(world); sys.Run(world); sys.Run(world);

        Assert.True(Packet.TryReadSnapshot(transport.Sent[0].Payload, out var data));
        Assert.Single(data.Entries);
        Assert.Equal(10 * 65536, data.Entries[0].PositionX.RawValue); // Fixed32.FromInt(10)
        Assert.Equal(20 * 65536, data.Entries[0].PositionY.RawValue);
    }

    // -----------------------------------------------------------------------
    // Delta behaviour
    // -----------------------------------------------------------------------

    [Fact]
    public void Run_PositionUnchanged_NoBroadcastOnSecondCycle()
    {
        var world = CreateWorld(out _);
        var (server, transport) = CreateConnectedServer();
        var sys = new SnapshotBroadcastSystem(server);

        sys.Run(world); sys.Run(world); sys.Run(world); // first broadcast
        transport.Sent.Clear();

        sys.Run(world); sys.Run(world); sys.Run(world); // position unchanged

        Assert.Empty(transport.Sent);
    }

    [Fact]
    public void Run_PositionChanged_BroadcastsOnNextCycle()
    {
        var world = CreateWorld(out var player);
        var (server, transport) = CreateConnectedServer();
        var sys = new SnapshotBroadcastSystem(server);

        sys.Run(world); sys.Run(world); sys.Run(world); // first broadcast
        transport.Sent.Clear();

        ref var pos = ref world.Get<SimPosition>(player);
        pos = new SimPosition(Fixed32.FromInt(50), Fixed32.FromInt(60));

        sys.Run(world); sys.Run(world); sys.Run(world); // second broadcast

        Assert.Single(transport.Sent);
        Assert.True(Packet.TryReadSnapshot(transport.Sent[0].Payload, out var data));
        Assert.Single(data.Entries);
        Assert.Equal(50 * 65536, data.Entries[0].PositionX.RawValue);
        Assert.Equal(60 * 65536, data.Entries[0].PositionY.RawValue);
    }

    [Fact]
    public void Run_NoPeersConnected_NoBroadcast()
    {
        var world = CreateWorld(out _);
        // Server with no connected peer.
        var transport = new FakeTransport();
        var server    = new NetServer(transport);
        var sys       = new SnapshotBroadcastSystem(server);

        sys.Run(world); sys.Run(world); sys.Run(world);

        Assert.Empty(transport.Sent);
    }
}
