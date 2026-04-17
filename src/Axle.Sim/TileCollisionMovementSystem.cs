namespace Axle.Sim;

using Axle.Core.AxleMath;
using Axle.Ecs;

/// <summary>
/// Algorithm per tick:
///   1. Compute per-tick delta from velocity × dt.
///   2. Split into substeps of at most MaxSubstep sim units.
///   3. For each substep: resolve X axis, then Y axis.
///   4. Write back final position.
///
/// </summary>
public sealed class TileCollisionMovementSystem : ISystem
{
    // Maximum substep distance per axis (tileSize / 4 = 8 sim units).
    // Ensures the player cannot skip over a wall thinner than a full tile.
    private static readonly Fixed32 TileSize   = TileCollisionMap.TileSize;
    private static readonly Fixed32 MaxSubstep = TileSize / Fixed32.FromInt(4);

    private readonly Fixed32          _dt;
    private readonly TileCollisionMap _map;

    public TileCollisionMovementSystem(Fixed32 dt, TileCollisionMap map)
    {
        _dt  = dt;
        _map = map;
    }

    public void Run(World world)
    {
        var colliderStore = world.Store<PlayerCollider>();

        foreach (var item in world.Query<SimPosition, Velocity>())
        {
            if (!colliderStore.HasEntityIndex(item.Entity)) continue;

            ref var pos = ref item.Component1;
            ref var vel = ref item.Component2;
            ref var col = ref colliderStore.Get(item.Entity);

            Fixed32 deltaX = vel.X * _dt;
            Fixed32 deltaY = vel.Y * _dt;

            // Determine substep count so each step is within MaxSubstep.
            Fixed32 absX    = deltaX < Fixed32.Zero ? -deltaX : deltaX;
            Fixed32 absY    = deltaY < Fixed32.Zero ? -deltaY : deltaY;
            Fixed32 maxDelta = absX > absY ? absX : absY;

            int steps = 1;
            if (maxDelta > MaxSubstep)
            {
                // steps = ceil(maxDelta / MaxSubstep) using integer arithmetic only
                int raw   = (maxDelta / MaxSubstep).RawValue;
                int floor = raw >> Fixed32.FractionalBits;
                int frac  = raw & ((1 << Fixed32.FractionalBits) - 1);
                steps = frac > 0 ? floor + 1 : floor;
                if (steps < 1) steps = 1;
            }

            Fixed32 stepX = deltaX / Fixed32.FromInt(steps);
            Fixed32 stepY = deltaY / Fixed32.FromInt(steps);

            // SimPosition is the top-left corner of the entity (matches DrawQuad convention).
            // Convert to AABB centre for collision math, then convert back.
            Fixed32 cx = pos.X + col.HalfWidth;
            Fixed32 cy = pos.Y + col.HalfHeight;

            for (int i = 0; i < steps; i++)
            {
                cx = ResolveAxisX(cx, cy, col.HalfWidth, col.HalfHeight, stepX);
                cy = ResolveAxisY(cx, cy, col.HalfWidth, col.HalfHeight, stepY);
            }

            pos.X = cx - col.HalfWidth;
            pos.Y = cy - col.HalfHeight;
        }
    }

    // ---- Axis resolution helpers ----

    /// <summary>
    /// Moves the centre X by deltaX"
    /// </summary>
    private Fixed32 ResolveAxisX(
        Fixed32 cx, Fixed32 cy,
        Fixed32 halfW, Fixed32 halfH,
        Fixed32 deltaX)
    {
        if (deltaX == Fixed32.Zero) return cx;

        Fixed32 newCx = cx + deltaX;

        // Tile rows spanned by the player's Y extent.
        // Max edge is exclusive: subtract 1 raw unit so a flush-contact edge
        int tileMinY = FloorTileIndex(cy - halfH);
        int tileMaxY = FloorTileIndex(cy + halfH - Fixed32.FromRaw(1));

        if (deltaX > Fixed32.Zero)
        {
            // Moving right — check the leading (right) edge.
            Fixed32 playerMaxX = newCx + halfW;
            int tileX = FloorTileIndex(playerMaxX);

            for (int ty = tileMinY; ty <= tileMaxY; ty++)
            {
                if (!_map.IsSolid(tileX, ty)) continue;

                Fixed32 tileLeft = Fixed32.FromInt(tileX) * TileSize;
                if (playerMaxX > tileLeft)
                    return tileLeft - halfW; // snap flush to wall left edge
            }
        }
        else
        {
            // Moving left — check the leading (left) edge.
            Fixed32 playerMinX = newCx - halfW;
            int tileX = FloorTileIndex(playerMinX);

            for (int ty = tileMinY; ty <= tileMaxY; ty++)
            {
                if (!_map.IsSolid(tileX, ty)) continue;

                Fixed32 tileRight = Fixed32.FromInt(tileX + 1) * TileSize;
                if (playerMinX < tileRight)
                    return tileRight + halfW; // snap flush to wall right edge
            }
        }

        return newCx;
    }

    private Fixed32 ResolveAxisY(
        Fixed32 cx, Fixed32 cy,
        Fixed32 halfW, Fixed32 halfH,
        Fixed32 deltaY)
    {
        if (deltaY == Fixed32.Zero) return cy;

        Fixed32 newCy = cy + deltaY;

        int tileMinX = FloorTileIndex(cx - halfW);
        int tileMaxX = FloorTileIndex(cx + halfW - Fixed32.FromRaw(1));

        if (deltaY > Fixed32.Zero)
        {
            // Moving down — check the leading (bottom) edge.
            Fixed32 playerMaxY = newCy + halfH;
            int tileY = FloorTileIndex(playerMaxY);

            for (int tx = tileMinX; tx <= tileMaxX; tx++)
            {
                if (!_map.IsSolid(tx, tileY)) continue;

                Fixed32 tileTop = Fixed32.FromInt(tileY) * TileSize;
                if (playerMaxY > tileTop)
                    return tileTop - halfH; // snap flush to tile top edge
            }
        }
        else
        {
            // Moving up — check the leading (top) edge.
            Fixed32 playerMinY = newCy - halfH;
            int tileY = FloorTileIndex(playerMinY);

            for (int tx = tileMinX; tx <= tileMaxX; tx++)
            {
                if (!_map.IsSolid(tx, tileY)) continue;

                Fixed32 tileBottom = Fixed32.FromInt(tileY + 1) * TileSize;
                if (playerMinY < tileBottom)
                    return tileBottom + halfH; // snap flush to tile bottom edge
            }
        }

        return newCy;
    }


    private static int FloorTileIndex(Fixed32 simPos)
    {
        // Divide by TileSize (32) in fixed-point, then extract integer part.
        // (simPos / TileSize).RawValue >> FractionalBits == floor(simPos / 32)
        return (simPos / TileSize).RawValue >> Fixed32.FractionalBits;
    }
}
