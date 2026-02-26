namespace Axle.Ecs;

public readonly struct EntityId
{
    public readonly int Index;
    public readonly int Version;

    public EntityId(int index, int version) { Index = index; Version = version; }
}