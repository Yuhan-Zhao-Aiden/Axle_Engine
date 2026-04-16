namespace Axle.Client;
using Axle.Core;
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
        // Resolve relative to the output directory so the path is correct
        // regardless of which directory dotnet run is invoked from.
        string mapPath = Path.Combine(AppContext.BaseDirectory, "assets", "test.map");
        MapData map = MapLoader.Load(mapPath);

        var window = new WindowHost();
        var world = new World();

        // Register stores
        world.Register<Transform>();
        world.Register<RenderRect>();
        world.Register<SimPosition>();
        world.Register<Velocity>();
        world.Register<MoveInput>();
        world.Register<LocalPlayer>();

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

        // Systems order declares the pipeline
        var input = new LocalInputSystem();
        var sync = new SyncTransformSystem();
        var renderRunner = new RenderRunner(world, sync, () => new Camera(window.ClientSize.X, window.ClientSize.Y));
        var simRunner = new SimRunner(world, input, new PlayerVelocitySystem(), new MovementSystem(SimTime.Dt));

        EngineLoop? loop = null;
        window.OnReady = () =>
        {
            renderRunner.Initialize(window.Renderer, map);
            loop = new EngineLoop(simRunner, renderRunner);
        };

        window.OnFrame = () => loop?.Frame();
        window.OnInput = ks => input.Update(ks);

        window.Run();
    }
}