using System.Diagnostics;
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
    private AnimationSystem? _animation;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private double _lastFrameTime;

    public RenderRunner(World world, SyncTransformSystem sync, Func<Camera> getCamera)
    {
        _world = world;
        _sync = sync;
        _getCamera = getCamera;
    }

    public void Initialize(QuadRenderer renderer, MapData? map = null,
        IReadOnlyDictionary<char, Texture2D>? textures = null,
        AnimationSystem? animation = null)
    {
        _render = new RenderSystem(renderer);
        if (map is not null)
            _mapRender = new MapRenderSystem(map, renderer, textures);
        _animation = animation;
    }

    public void Draw(float alpha)
    {
        double now = _stopwatch.Elapsed.TotalSeconds;
        float dt = Math.Clamp((float)(now - _lastFrameTime), 0f, 0.1f);
        _lastFrameTime = now;

        _animation?.Run(_world, dt);

        var camera = _getCamera();
        _mapRender?.Render(camera);
        _sync.Run(_world);
        _render!.Render(_world, camera);
    }
}
