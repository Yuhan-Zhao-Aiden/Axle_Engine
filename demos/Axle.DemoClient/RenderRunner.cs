using Axle.Core;
using Axle.Ecs;
using Axle.Graphics;
using Axle.Sim;
using Axle.Sim.Map;

namespace Axle.Client.System;

/// <summary>
/// Implements IRenderStage for use with EngineLoop.
/// </summary>
public sealed class RenderRunner : IRenderStage
{
    private readonly World _world;
    private readonly SyncTransformSystem _sync;
    private readonly Func<Camera> _getCamera;
    private RenderSystem? _render;
    private MapRenderSystem? _mapRender;

    public RenderRunner(World world, SyncTransformSystem sync, Func<Camera> getCamera)
    {
        _world = world;
        _sync = sync;
        _getCamera = getCamera;
    }

    public void Initialize(QuadRenderer renderer, MapData? map = null)
    {
        _render = new RenderSystem(renderer);
        if (map is not null)
            _mapRender = new MapRenderSystem(map, renderer);
    }

    public void Draw(float alpha)
    {
        var camera = _getCamera();
        _mapRender?.Render(camera);
        _sync.Run(_world);
        _render!.Render(_world, camera);
    }
}
