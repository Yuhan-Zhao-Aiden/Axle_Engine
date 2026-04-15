using Axle.Core;
using Axle.Ecs;
using Axle.Graphics;
using Axle.Sim;

namespace Axle.System;

/// <summary>
/// Implements IRenderStage for use with EngineLoop.
/// </summary>
public sealed class RenderRunner : IRenderStage
{
    private readonly World _world;
    private readonly SyncTransformSystem _sync;
    private readonly Func<Camera> _getCamera;
    private RenderSystem? _render;

    public RenderRunner(World world, SyncTransformSystem sync, Func<Camera> getCamera)
    {
        _world = world;
        _sync = sync;
        _getCamera = getCamera;
    }

    public void Initialize(QuadRenderer renderer)
    {
        _render = new RenderSystem(renderer);
    }

    public void Draw(float alpha)
    {
        _sync.Run(_world);
        _render!.Render(_world, _getCamera());
    }
}
