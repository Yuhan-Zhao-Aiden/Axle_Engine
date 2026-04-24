using System.Text.Json;

namespace Axle.Client;
using Axle.Core;
using Axle.Net;
using Axle.Core.AxleMath;
using Axle.Core.Utility;
using Axle.Ecs;
using Axle.Graphics;
using Axle.Sim;
using Axle.Sim.Map;
using Axle.Client.System;

public class Program
{
    public static void Main(string[] args)
    {
        bool useNet = args.Contains("--net");

        // Resolve relative to the output directory so the path is correct
        // regardless of which directory dotnet run is invoked from.
        string mapPath      = Path.Combine(AppContext.BaseDirectory, "assets", "test.map");
        string settingsPath = Path.Combine(AppContext.BaseDirectory, "assets", "settings.json");

        var jsonOpts = new JsonSerializerOptions { ReadCommentHandling = JsonCommentHandling.Skip };
        var settings = JsonSerializer.Deserialize<GameSettings>(File.ReadAllText(settingsPath), jsonOpts) ?? new GameSettings();

        MapData map = MapLoader.Load(mapPath, settings.Mode);

        var camController = new CameraController(settings.Camera, map);

        var window = new WindowHost();
        var world = new World();

        // Register stores
        world.Register<Transform>();
        world.Register<RenderRect>();
        world.Register<SimPosition>();
        world.Register<Velocity>();
        world.Register<MoveInput>();
        world.Register<LocalPlayer>();
        world.Register<PlayerCollider>();
        world.Register<CollisionLayer>();
        world.Register<Collectible>();

        // Spawn player at first A spawn, or origin if none present
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
        world.Add(player, new Transform(new Vector2f(spawnX.ToFloat(), spawnY.ToFloat())));
        world.Add(player, new RenderRect(new Vector2f(32f, 32f), new Color4(1f, 1f, 0f)));
        world.Add(player, new PlayerCollider(Fixed32.FromInt(16), Fixed32.FromInt(16)));
        world.Add(player, new CollisionLayer(CollisionLayers.Player, Fixed32.FromInt(16), Fixed32.FromInt(16)));

        // Spawn collectibles at each 'C' position from the map
        var gold = new Color4(1f, 0.85f, 0f);
        foreach (var spawn in map.CoinSpawns)
        {
            Fixed32 cx = Fixed32.FromInt(spawn.X * 32);
            Fixed32 cy = Fixed32.FromInt(spawn.Y * 32);
            var coin = world.CreateEntity();
            world.Add(coin, new SimPosition(cx, cy));
            world.Add(coin, new CollisionLayer(CollisionLayers.Collectible, Fixed32.FromInt(16), Fixed32.FromInt(16)));
            world.Add<Collectible>(coin);
            world.Add(coin, new Transform(new Vector2f(cx.ToFloat(), cy.ToFloat())));
            world.Add(coin, new RenderRect(new Vector2f(32f, 32f), gold));
        }

        // Build generalized overlap system
        var overlapSys = new EntityOverlapSystem();
        overlapSys.Register(CollisionLayers.Player, CollisionLayers.Collectible, new CollectibleHandler(world));

        // Systems order declares the pipeline
        var input = new LocalInputSystem();
        var sync = new SyncTransformSystem();
        var renderRunner = new RenderRunner(world, sync, () =>
        {
            int vpW = window.ClientSize.X;
            int vpH = window.ClientSize.Y;
            var (cx2, cy2) = camController.Update(world, vpW, vpH);
            return new Camera(vpW, vpH, cx2, cy2);
        });
        var tileMap = new TileCollisionMap(map);

        // Pre-instantiate the two systems that ReconciliationApplicator needs to replay
        // unacknowledged inputs — same instances run inside the SimRunner.
        var velocitySystem = new PlayerVelocitySystem();
        var movementSystem = new TileCollisionMovementSystem(SimTime.Dt, tileMap);
        var history        = new InputHistory();

        // Build system pipeline — ClientNetInputSystem injected after LocalInputSystem in --net mode.
        List<ISystem> systems = [input];

        // Optional network client — created when --net flag is provided.
        NetClient? netClient = null;
        ReconciliationApplicator? reconciliator = null;
        ClientNetInputSystem? netInputSystem = null;
        if (useNet)
        {
            ITransport clientTransport = settings.NetSim?.Enabled == true
                ? new SimulatedTransport(new UdpTransport(), settings.NetSim)
                : new UdpTransport();
            netClient = new NetClient(clientTransport);
            netClient.Connect(new NetEndpoint("127.0.0.1", 7777));
            netInputSystem = new ClientNetInputSystem(netClient, history);
            systems.Add(netInputSystem);

            var ghost = world.CreateEntity();
            world.Add(ghost, new Transform(new Vector2f(spawnX.ToFloat(), spawnY.ToFloat())));
            world.Add(ghost, new RenderRect(new Vector2f(32f, 32f), new Color4(1f, 0f, 0f, 0.45f)));

            reconciliator = new ReconciliationApplicator(history, world, player, velocitySystem, movementSystem, ghost);
            Console.WriteLine("[Client] Connecting to 127.0.0.1:7777...");
        }

        systems.Add(velocitySystem);
        if (settings.Mode == GameMode.Platformer)
            systems.Add(new GravitySystem());
        systems.Add(movementSystem);
        systems.Add(overlapSys);
        var simRunner = new SimRunner(world, [.. systems]);

        EngineLoop? loop = null;
        ulong netTick = 0;
        bool serverEntitySet = false;
        var remoteEntities = new Dictionary<(int index, int version), EntityId>();
        ulong debugLogTick = 0;

        window.OnReady = () =>
        {
            renderRunner.Initialize(window.Renderer, map);
            loop = new EngineLoop(simRunner, renderRunner);
        };

        window.OnFrame = () =>
        {
            netClient?.Tick(netTick++);

            // One-time: tell the reconciliator which server entity is ours.
            if (!serverEntitySet && netClient?.State == ConnectionState.Connected)
            {
                reconciliator?.SetServerEntity(netClient.AssignedEntityIndex, netClient.AssignedEntityVersion);
                serverEntitySet = true;
                Console.WriteLine($"[Client] Connected — server entity {netClient.AssignedEntityIndex}");
            }

            while (netClient is not null && netClient.TryDequeueSnapshot(out var snap))
            {
                reconciliator?.Apply(snap);

                // Update or create visual entities for remote players.
                if (netClient.AssignedEntityIndex >= 0)
                {
                    foreach (var entry in snap.Entries)
                    {
                        // Skip our own server entity — reconciliator handles it.
                        if (entry.EntityIndex == netClient.AssignedEntityIndex &&
                            entry.EntityVersion == netClient.AssignedEntityVersion)
                            continue;

                        var key = (entry.EntityIndex, entry.EntityVersion);
                        if (!remoteEntities.TryGetValue(key, out var remoteId))
                        {
                            float rx = (entry.Mask & ChangeMask.PositionX) != 0 ? entry.PositionX.ToFloat() : 0f;
                            float ry = (entry.Mask & ChangeMask.PositionY) != 0 ? entry.PositionY.ToFloat() : 0f;
                            remoteId = world.CreateEntity();
                            world.Add(remoteId, new Transform(new Vector2f(rx, ry)));
                            world.Add(remoteId, new RenderRect(new Vector2f(32f, 32f), new Color4(0f, 0.5f, 1f, 0.8f)));
                            remoteEntities[key] = remoteId;
                        }
                        else
                        {
                            ref var rt = ref world.Get<Transform>(remoteId);
                            float rx = (entry.Mask & ChangeMask.PositionX) != 0 ? entry.PositionX.ToFloat() : rt.Position.X;
                            float ry = (entry.Mask & ChangeMask.PositionY) != 0 ? entry.PositionY.ToFloat() : rt.Position.Y;
                            rt.Position = new Vector2f(rx, ry);
                        }
                    }
                }

                if (reconciliator is not null &&
                    (reconciliator.LastErrorX.RawValue != 0 || reconciliator.LastErrorY.RawValue != 0))
                    Console.WriteLine($"[Predict] tick={snap.TickId} ackSeq={snap.AckSeq} errX={reconciliator.LastErrorX} errY={reconciliator.LastErrorY}");
            }

            // Periodic debug log every 90 frames (~3 s at 30 Hz).
            if (netClient is not null && netClient.State == ConnectionState.Connected && ++debugLogTick % 90 == 0)
            {
                int pending = reconciliator is not null ? history.PendingCount(reconciliator.LastAckedSeq) : 0;
                ushort sent = netInputSystem?.LastSentSeq ?? 0;
                ushort acked = reconciliator?.LastAckedSeq ?? 0;
                float errTiles = reconciliator is not null
                    ? MathF.Sqrt(MathF.Pow(reconciliator.LastErrorX.ToFloat(), 2) +
                                 MathF.Pow(reconciliator.LastErrorY.ToFloat(), 2)) / 32f
                    : 0f;
                Console.WriteLine($"[Debug] RTT={netClient.LatestRttMs}ms | SentSeq={sent} AckedSeq={acked} Pending={pending} | Err={errTiles:F3}tiles");
            }
            loop?.Frame();
        };

        window.OnInput = ks => input.Update(ks);

        window.Run();
        netClient?.Dispose();
    }
}