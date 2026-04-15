// See https://aka.ms/new-console-template for more information
namespace Axle.DemoClient;
using Axle.Core;
using Axle.Core.AxleMath;
using Axle.Core.Utility;
using Axle.Ecs;
using Axle.Graphics;
using Axle.Sim;
using Axle.System;

public class Program
{
    public static void Main(string[] args)
    {
        CommandBuffer _cb = new();
        var window = new WindowHost();
        var world = new World();

        // Register stores
        world.Register<Transform>();
        world.Register<RenderRect>();
        world.Register<SimPosition>();
        world.Register<Velocity>();
        world.Register<MoveInput>();
        world.Register<LocalPlayer>();

        // Build static scene
        EntitySystem ent = new(_cb.ForSystem(0).CreateWriter());
        ent.CreateScene();
        _cb.Playback(world);

        // Spawn player entity (yellow square, starts at origin)
        var player = world.CreateEntity();
        world.Add(player, new SimPosition(Fixed32.Zero, Fixed32.Zero));
        world.Add(player, new Velocity(Fixed32.Zero, Fixed32.Zero));
        world.Add(player, new MoveInput(0, 0));
        world.Add<LocalPlayer>(player);
        world.Add(player, new Transform(new Vector2f(0f, 0f)));
        world.Add(player, new RenderRect(new Vector2f(32f, 32f), new Color4(255, 255, 0)));

        // Systems order declares the pipeline
        var input = new LocalInputSystem();
        var sync = new SyncTransformSystem();
        var renderRunner = new RenderRunner(world, sync, () => new Camera(window.ClientSize.X, window.ClientSize.Y));
        var simRunner = new SimRunner(world, input, new PlayerVelocitySystem(), new MovementSystem(SimTime.Dt));

        EngineLoop? loop = null;
        window.OnReady = () =>
        {
            renderRunner.Initialize(window.Renderer);
            loop = new EngineLoop(simRunner, renderRunner);
        };

        window.OnFrame = () => loop?.Frame();
        window.OnInput = ks => input.Update(ks);

        window.Run();
    }
}