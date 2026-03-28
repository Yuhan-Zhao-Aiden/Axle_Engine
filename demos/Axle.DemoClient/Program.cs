// See https://aka.ms/new-console-template for more information
namespace Axle.DemoClient;
using Axle.Ecs;
using Axle.Graphics;
using Axle.System;

public class Program
{
    public static void Main(string[] args)
    {
        // window.Run();
        CommandBuffer _cb = new();
        var window = new WindowHost();
        var world = new World();
        world.Register<Transform>();
        world.Register<RenderRect>();

        EntitySystem ent = new(_cb.ForSystem(0).CreateWriter());
        ent.CreateScene();
        _cb.Playback(world);

        RenderSystem? render = null;
        window.OnReady = () => render = new RenderSystem(window.Renderer);
        window.OnRender = camera => render!.Render(world, camera);

        window.Run();
    }
}