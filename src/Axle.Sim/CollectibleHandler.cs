namespace Axle.Sim;

using Axle.Ecs;

/// <summary>
/// Destroys the collectible entity (entity B) on overlap with a player (entity A).
/// </summary>
public sealed class CollectibleHandler : IOverlapHandler
{
    private readonly World _world;

    public CollectibleHandler(World world)
    {
        _world = world;
    }

    public void Handle(int entityA, int entityB, CommandBuffer buffer)
    {
        EntityId collectible = _world.GetEntityId(entityB);

        if (!_world.IsAlive(collectible))
            return;

        buffer.ForSystem(0).CreateWriter(0).RecordDestroyEntity(Target.CreateReal(collectible));
    }
}
