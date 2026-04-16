namespace Axle.Sim.Map;

[Flags]
public enum TileFlags : byte
{
    None     = 0,
    Solid    = 1 << 0,  // blocks movement / collision
    Walkable = 1 << 1,  // valid surface for entities to stand on
}

public static class TileFlagsLookup
{
    private static readonly TileFlags[] _flags =
    [
        TileFlags.Solid,
        TileFlags.Walkable,
    ];

    public static TileFlags For(TileType type)
    {
        int index = (int)type;
        return (uint)index < (uint)_flags.Length ? _flags[index] : TileFlags.None;
    }

    public static bool Is(TileType type, TileFlags flags) => (For(type) & flags) == flags;
}
