using System.Diagnostics;
using System.Text.Json;
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
        string settingsPath = Path.Combine(AppContext.BaseDirectory, "assets", "settings.json");
        var jsonOpts = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
        var serverSettings = File.Exists(settingsPath)
            ? JsonSerializer.Deserialize<ServerSettings>(File.ReadAllText(settingsPath), jsonOpts) ?? new ServerSettings()
            : new ServerSettings();

        var world = new World();
        world.Register<SimPosition>();
        world.Register<Velocity>();
        world.Register<MoveInput>();
        world.Register<LocalPlayer>();
        world.Register<PlayerCollider>();

        string mapPath = Path.Combine(AppContext.BaseDirectory, "assets", "test.map");
        var map = MapLoader.Load(mapPath, GameMode.TopDown);
        var tileMap = new TileCollisionMap(map);

        ITransport serverTransport = serverSettings.NetSim?.Enabled == true
            ? new SimulatedTransport(new UdpTransport(), serverSettings.NetSim)
            : new UdpTransport();
        using var server = new NetServer(serverTransport);

        // Spawn a new player entity for each connecting client.
        int spawnIndex = 0;
        server.OnClientConnected = endpoint =>
        {
            int si = spawnIndex++;
            Fixed32 sx = Fixed32.Zero;
            Fixed32 sy = Fixed32.Zero;
            if (map.PlayerSpawns.Count > 0)
            {
                var spawn = map.PlayerSpawns[Math.Min(si, map.PlayerSpawns.Count - 1)];
                sx = Fixed32.FromInt(spawn.X * 32);
                sy = Fixed32.FromInt(spawn.Y * 32);
            }
            var e = world.CreateEntity();
            world.Add(e, new SimPosition(sx, sy));
            world.Add(e, new Velocity(Fixed32.Zero, Fixed32.Zero));
            world.Add(e, new MoveInput(0, 0));
            world.Add<LocalPlayer>(e);
            world.Add(e, new PlayerCollider(Fixed32.FromInt(16), Fixed32.FromInt(16)));
            return (e.Index, e.Version);
        };

        server.Listen(7777);

        var networkInput = new NetworkInputSystem(server.InputBuffers, server.ClientEntities);
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
                    Console.WriteLine($"[Server] tick={tick}  clients={server.ConnectedPeers.Count}");
            }

            Thread.Sleep(1);
        }
    }
}
