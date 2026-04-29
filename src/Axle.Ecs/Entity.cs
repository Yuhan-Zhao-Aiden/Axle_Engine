namespace Axle.Ecs;

public readonly struct EntityId : IEquatable<EntityId>
{
    public readonly int Index;
    public readonly int Version;

    public EntityId(int index, int version) { Index = index; Version = version; }

    public bool Equals(EntityId other) => Index == other.Index && Version == other.Version;
    public override bool Equals(object? obj) => obj is EntityId other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Index, Version);
    public static bool operator ==(EntityId a, EntityId b) => a.Equals(b);
    public static bool operator !=(EntityId a, EntityId b) => !a.Equals(b);
}