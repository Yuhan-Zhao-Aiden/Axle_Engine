namespace Axle.Sim;

using Axle.Core.AxleMath;
using Axle.Sim.Map;

public sealed class TileCollisionMap
{
    /// <summary>Side length of one tile in simulation units.</summary>
    public static readonly Fixed32 TileSize = Fixed32.FromInt(32);

    private readonly MapData _map;

    public TileCollisionMap(MapData map)
    {
        _map = map;
    }

    /// <summary>
    /// Returns true if the tile at grid position is solid
    /// </summary>
    public bool IsSolid(int tileX, int tileY)
    {
        if (tileX < 0 || tileX >= _map.Width || tileY < 0 || tileY >= _map.Height)
            return true;

        if (!_map.TryGetTile(tileX, tileY, out TileType tile))
            return false; // void cell

        return TileFlagsLookup.Is(tile, TileFlags.Solid);
    }
}
