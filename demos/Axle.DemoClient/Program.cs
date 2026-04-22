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

        // Build system pipeline — GravitySystem only active in Platformer mode
        List<ISystem> systems = [input, new PlayerVelocitySystem()];
        if (settings.Mode == GameMode.Platformer)
            systems.Add(new GravitySystem());
        systems.Add(new TileCollisionMovementSystem(SimTime.Dt, tileMap));
        systems.Add(overlapSys);
        var simRunner = new SimRunner(world, [.. systems]);

        // Optional network client — created when --net flag is provided.
        NetClient? netClient = null;
        ClientInputSender? sender = null;
        if (useNet)
        {
            netClient = new NetClient(new UdpTransport());
            netClient.Connect(new NetEndpoint("127.0.0.1", 7777));
            sender = new ClientInputSender(netClient);
            Console.WriteLine("[Client] Connecting to 127.0.0.1:7777...");
        }

        EngineLoop? loop = null;
        ulong netTick = 0;

        window.OnReady = () =>
        {
            renderRunner.Initialize(window.Renderer, map);
            loop = new EngineLoop(simRunner, renderRunner);
        };

        window.OnFrame = () =>
        {
            netClient?.Tick(netTick++);
            loop?.Frame();
        };

        window.OnInput = ks =>
        {
            input.Update(ks);
            sender?.Update(ks, Environment.TickCount64);
        };

        window.Run();
        netClient?.Dispose();
    }
}