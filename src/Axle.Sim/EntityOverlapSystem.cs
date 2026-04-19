namespace Axle.Sim;

using Axle.Core.AxleMath;
using Axle.Ecs;

/// <summary>
/// Generic entity-vs-entity AABB overlap system.
///   var sys = new EntityOverlapSystem();
///   sys.Register(CollisionLayers.Player, CollisionLayers.Collectible, new CollectibleHandler(world));
/// </summary>
public sealed class EntityOverlapSystem : ISystem
{
    private readonly CommandBuffer _cb = new();

    private readonly List<(int idx, byte layerId, Fixed32 cx, Fixed32 cy, Fixed32 halfW, Fixed32 halfH)> _entities = new();
    private readonly List<(int a, int b)> _scratch = new();

    private readonly Dictionary<(byte layerA, byte layerB), IOverlapHandler> _handlers = new();

    public void Register(byte layerA, byte layerB, IOverlapHandler handler)
    {
        _handlers[(layerA, layerB)] = handler;
    }

    public void Run(World world)
    {
        if (_handlers.Count == 0) return;

        _entities.Clear();
        foreach (var item in world.Query<CollisionLayer, SimPosition>())
        {
            ref var layer = ref item.Component1;
            ref var pos   = ref item.Component2;

            Fixed32 cx = pos.X + layer.HalfW;
            Fixed32 cy = pos.Y + layer.HalfH;
            _entities.Add((item.Entity, layer.LayerId, cx, cy, layer.HalfW, layer.HalfH));
        }

        foreach (var (key, handler) in _handlers)
        {
            byte layerA = key.layerA;
            byte layerB = key.layerB;

            foreach (var a in _entities)
            {
                if (a.layerId != layerA) continue;

                foreach (var b in _entities)
                {
                    if (b.layerId != layerB) continue;

                    Fixed32 dx    = a.cx - b.cx;
                    Fixed32 dy    = a.cy - b.cy;
                    Fixed32 absDx = dx < Fixed32.Zero ? -dx : dx;
                    Fixed32 absDy = dy < Fixed32.Zero ? -dy : dy;

                    if (absDx < a.halfW + b.halfW && absDy < a.halfH + b.halfH)
                        _scratch.Add((a.idx, b.idx));
                }
            }

            foreach (var (idxA, idxB) in _scratch)
                handler.Handle(idxA, idxB, _cb);

            _scratch.Clear();
        }

        _cb.Playback(world);
    }
}
