using System.Diagnostics;
using Axle.Core.AxleMath;
using Axle.Ecs;
using Axle.Net;
using Axle.Sim;
using Axle.Sim.Map;

namespace Axle.Server;

public class Program
{
    public static void Main(string[] args)
    {
        var world = new World();
        world.Register<SimPosition>();
        world.Register<Velocity>();
        world.Register<MoveInput>();
        world.Register<LocalPlayer>();
        world.Register<PlayerCollider>();

        string mapPath = Path.Combine(AppContext.BaseDirectory, "assets", "test.map");
        var map = MapLoader.Load(mapPath, GameMode.TopDown);
        var tileMap = new TileCollisionMap(map);

        Fixed32 spawnX = Fixed32.Zero;
        Fixed32 spawnY = Fixed32.Zero;
        if (map.PlayerSpawns.Count > 0)
        {
            spawnX = Fixed32.FromInt(map.PlayerSpawns[0].X * 32);
            spawnY = Fixed32.FromInt(map.PlayerSpawns[0].Y * 32);
        }

        var player = world.CreateEntity();
        world.Add(player, new SimPosition(spawnX, spawnY));
        world.Add(player, new Velocity(Fixed32.Zero, Fixed32.Zero));
        world.Add(player, new MoveInput(0, 0));
        world.Add<LocalPlayer>(player);
        world.Add(player, new PlayerCollider(Fixed32.FromInt(16), Fixed32.FromInt(16)));

        using var server = new NetServer(new UdpTransport());
        server.Listen(7777);

        var networkInput = new NetworkInputSystem(server.InputBuffers);
        var simRunner = new SimRunner(world,
            networkInput,
            new PlayerVelocitySystem(),
            new TileCollisionMovementSystem(SimTime.Dt, tileMap),
            new SnapshotBroadcastSystem(server));

        const double fixedDt = 1.0 / SimTime.TickRate;
        const int logIntervalTicks = SimTime.TickRate; // log once per second
        var clock = Stopwatch.StartNew();
        double accumulator = 0.0;
        ulong tick = 0;

        while (true)
        {
            double elapsed = clock.Elapsed.TotalSeconds;
            clock.Restart();
            // Cap to avoid spiral-of-death after a long hitch.
            if (elapsed > 0.25) elapsed = 0.25;
            accumulator += elapsed;

            server.Tick();

            while (accumulator >= fixedDt)
            {
                simRunner.Step(fixedDt, tick);
                tick++;
                accumulator -= fixedDt;

                if (tick % (ulong)logIntervalTicks == 0)
                {
                    var pos = world.Store<SimPosition>().Get(player.Index);
                    Console.WriteLine($"[Server] tick={tick}  player=({pos.X}, {pos.Y})");
                }
            }

            Thread.Sleep(1);
        }
    }
}
