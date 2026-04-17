namespace Axle.Sim.Test;

using Axle.Core.AxleMath;
using Axle.Ecs;
using Axle.Sim.Map;

/// <summary>
/// Tests for <see cref="TileCollisionMovementSystem"/>.
///
/// Coordinate model (matches demo client):
///   - Each tile is 32 × 32 sim units.
///   - Player collider: HalfWidth = 11, HalfHeight = 14.
///   - Tile (tx, ty) occupies [tx*32 .. (tx+1)*32) × [ty*32 .. (ty+1)*32).
///   - Player position is the TOP-LEFT corner of the entity (matches DrawQuad convention).
///   - The collision AABB is therefore [pos.X, pos.X+2*HW] × [pos.Y, pos.Y+2*HH].
///   - Stop positions: moving right into wall at x=W  → pos.X = W - 2*HW
///                     moving left  into wall at x=W  → pos.X = W  (flush)
///                     moving down  into wall at y=W  → pos.Y = W - 2*HH
///                     moving up    into wall at y=W  → pos.Y = W  (flush)
/// </summary>
public class TileCollisionMovementSystemTest
{
    // ---- Collider constants matching the demo ----
    private static readonly Fixed32 HalfW = Fixed32.FromInt(11);
    private static readonly Fixed32 HalfH = Fixed32.FromInt(14);

    // ---- Helpers ----

    /// <summary>
    /// Builds a <see cref="TileCollisionMap"/> from an array of map row strings
    /// using <see cref="MapLoader.Parse"/>. '#' = solid wall, '.' = floor, ' ' = void.
    /// </summary>
    private static TileCollisionMap MakeMap(string[] rows) =>
        new TileCollisionMap(MapLoader.Parse(rows));

    /// <summary>
    /// Creates a world with all required stores and a single player entity
    /// at the given sim-unit position, moving at the given velocity.
    /// </summary>
    private static (World world, EntityId entity, TileCollisionMovementSystem system)
        Setup(TileCollisionMap map, Fixed32 posX, Fixed32 posY, Fixed32 velX, Fixed32 velY)
    {
        var world = new World();
        world.Register<SimPosition>();
        world.Register<Velocity>();
        world.Register<PlayerCollider>();

        var e = world.CreateEntity();
        world.Add(e, new SimPosition(posX, posY));
        world.Add(e, new Velocity(velX, velY));
        world.Add(e, new PlayerCollider(HalfW, HalfH));

        var system = new TileCollisionMovementSystem(SimTime.Dt, map);
        return (world, e, system);
    }

    // ---- Test A: horizontal wall (right) ----

    [Fact]
    public void PlayerMovingRight_StopsFlushAgainstWall()
    {
        // Map: ".#" — tile (0,0) = floor, tile (1,0) = wall.
        // Wall left edge = 32.  AABB right = pos.X + 22.
        // Start at pos.X = 4  (AABB right = 26, clear of wall).
        // Stop:  AABB right = 32 → pos.X = 32 - 22 = 10.
        var map = MakeMap([".#"]);

        Fixed32 startX = Fixed32.FromInt(4);
        Fixed32 startY = Fixed32.FromInt(2);
        Fixed32 speed  = Fixed32.FromInt(200);

        var (world, e, system) = Setup(map, startX, startY, speed, Fixed32.Zero);

        for (int i = 0; i < SimTime.TickRate * 2; i++)
            system.Run(world);

        ref var pos = ref world.Get<SimPosition>(e);
        Fixed32 expectedX = Fixed32.FromInt(32) - Fixed32.FromInt(22); // 10

        Assert.Equal(expectedX, pos.X);
        Assert.Equal(startY, pos.Y);
    }

    // ---- Test B: vertical wall (down) ----

    [Fact]
    public void PlayerMovingDown_StopsFlushAgainstWall()
    {
        // Map: two rows — floor then wall.
        //   "."  row 0 (tile y=0)
        //   "#"  row 1 (tile y=1)
        // Wall top edge = 32.  AABB bottom = pos.Y + 28.
        // Start at pos.Y = 2  (AABB bottom = 30, clear of wall).
        // Stop:  AABB bottom = 32 → pos.Y = 32 - 28 = 4.
        var map = MakeMap([".", "#"]);

        Fixed32 startX = Fixed32.FromInt(2);
        Fixed32 startY = Fixed32.FromInt(2);
        Fixed32 speed  = Fixed32.FromInt(200);

        var (world, e, system) = Setup(map, startX, startY, Fixed32.Zero, speed);

        for (int i = 0; i < SimTime.TickRate * 2; i++)
            system.Run(world);

        ref var pos = ref world.Get<SimPosition>(e);
        Fixed32 expectedY = Fixed32.FromInt(32) - Fixed32.FromInt(28); // 4

        Assert.Equal(startX, pos.X);
        Assert.Equal(expectedY, pos.Y);
    }

    // ---- Test C: diagonal slide — vertical wall ----

    [Fact]
    public void PlayerMovingDownRight_SlidesAlongVerticalWall()
    {
        // Map: floor column left of a wall column.
        //   ".#"  row 0
        //   ".#"  row 1
        //   ".#"  row 2
        // Start at (4, 2): AABB right = 26, clear of wall at x=32.
        // X should stop: AABB right = 32 → pos.X = 10.
        // Y should continue moving (slide downward).
        var map = MakeMap([".#", ".#", ".#"]);

        Fixed32 startX = Fixed32.FromInt(4);
        Fixed32 startY = Fixed32.FromInt(2);
        Fixed32 speed  = Fixed32.FromInt(60);

        var (world, e, system) = Setup(map, startX, startY, speed, speed);

        for (int i = 0; i < SimTime.TickRate; i++)
            system.Run(world);

        ref var pos = ref world.Get<SimPosition>(e);
        Fixed32 wallStop = Fixed32.FromInt(32) - Fixed32.FromInt(22); // 10

        // X is clamped at the wall.
        Assert.Equal(wallStop, pos.X);
        // Y has advanced beyond start (slide continued).
        Assert.True(pos.Y > startY, $"Expected Y > {startY} but got {pos.Y}");
    }

    // ---- Test D: diagonal slide — horizontal wall ----

    [Fact]
    public void PlayerMovingDownRight_SlidesAlongHorizontalWall()
    {
        // Map: open row then wall row.
        //   "..."  row 0
        //   "###"  row 1
        // Start at (2, 2): AABB bottom = 30, clear of wall at y=32.
        // Y should stop: AABB bottom = 32 → pos.Y = 4.
        // X should continue sliding rightward.
        var map = MakeMap(["...", "###"]);

        Fixed32 startX = Fixed32.FromInt(2);
        Fixed32 startY = Fixed32.FromInt(2);
        Fixed32 speed  = Fixed32.FromInt(60);

        var (world, e, system) = Setup(map, startX, startY, speed, speed);

        for (int i = 0; i < SimTime.TickRate; i++)
            system.Run(world);

        ref var pos = ref world.Get<SimPosition>(e);
        Fixed32 floorStop = Fixed32.FromInt(32) - Fixed32.FromInt(28); // 4

        // Y is clamped at the wall top.
        Assert.Equal(floorStop, pos.Y);
        // X has advanced beyond start (slide continued).
        Assert.True(pos.X > startX, $"Expected X > {startX} but got {pos.X}");
    }

    // ---- Test E: inner corner block ----

    [Fact]
    public void PlayerMovingIntoDiagonalCorner_StaysOutsideSolids()
    {
        // Map: 3×3, walls on right and bottom of centre open cell.
        //   "..."
        //   "..#"
        //   ".##"
        // Player starts in tile (0,0). Moves down-right into the corner at (2,2).
        var map = MakeMap(["...", "..#", ".##"]);

        Fixed32 startX = Fixed32.FromInt(16);
        Fixed32 startY = Fixed32.FromInt(16);
        Fixed32 speed  = Fixed32.FromInt(200);

        var (world, e, system) = Setup(map, startX, startY, speed, speed);

        for (int i = 0; i < SimTime.TickRate * 3; i++)
            system.Run(world);

        ref var pos = ref world.Get<SimPosition>(e);

        // Player must not overlap any solid tile.
        AssertNotOverlappingSolids(pos.X + HalfW, pos.Y + HalfH, HalfW, HalfH, map);
    }

    // ---- Test F: corridor traversal ----

    [Fact]
    public void PlayerTraversesCorridor_WithoutJitter()
    {
        // Horizontal corridor: 1 tile high (floor row), walls above and below.
        //   "#########"  row 0
        //   ".........". row 1  — 9 open floor tiles
        //   "#########"  row 2
        // Player starts at tile (1,1) centre, moves rightward through the corridor.
        var map = MakeMap(["#########", ".........", "#########"]);

        // Corridor occupies tile row y=1: sim y in [32, 64].
        // AABB fits when: pos.Y ≥ 32  AND  pos.Y + 28 ≤ 64  → pos.Y in [32, 36].
        // Use pos.Y = 34 so the AABB centre (48) sits at the corridor midpoint.
        Fixed32 startX = Fixed32.FromInt(42);  // comfortably inside floor column 1
        Fixed32 startY = Fixed32.FromInt(34);  // AABB = [34, 62], clear of both walls
        Fixed32 speed  = Fixed32.FromInt(60);

        var (world, e, system) = Setup(map, startX, startY, speed, Fixed32.Zero);

        for (int i = 0; i < SimTime.TickRate * 3; i++)
            system.Run(world);

        ref var pos = ref world.Get<SimPosition>(e);

        // Y must remain at start (walls above and below hold it).
        Assert.Equal(startY, pos.Y);
        // X has advanced (player traversed the corridor).
        Assert.True(pos.X > startX, $"Expected X > {startX} but got {pos.X}");
        // Player is not inside any solid.
        AssertNotOverlappingSolids(pos.X + HalfW, pos.Y + HalfH, HalfW, HalfH, map);
    }

    // ---- Test G: fast movement — no tunnelling ----

    [Fact]
    public void FastMovingPlayer_DoesNotTunnelThroughWall()
    {
        // Map: ".#." — wall at tile (1,0). Player starts at tile (0,0).
        // Wall left edge = 32.  Start at pos.X = 4 (AABB right = 26, clear of wall).
        // At speed 2000 and 30 Hz, unclamped delta ≈ 66 sim units.
        // Without substeps the naive check would land in tile (2,0) and miss the wall.
        // With substeps (each ≤ 8 sim units) the wall is caught in the first substep.
        var map = MakeMap([".#."]);

        Fixed32 startX = Fixed32.FromInt(4);
        Fixed32 startY = Fixed32.FromInt(2);
        Fixed32 speed  = Fixed32.FromInt(2000);

        var (world, e, system) = Setup(map, startX, startY, speed, Fixed32.Zero);

        // Single tick — without substeps the player would skip past the wall.
        system.Run(world);

        ref var pos = ref world.Get<SimPosition>(e);

        // Must be stopped flush: AABB right = 32 → pos.X = 32 - 22 = 10.
        Fixed32 expectedX = Fixed32.FromInt(32) - Fixed32.FromInt(22); // 10
        Assert.Equal(expectedX, pos.X);
        Assert.Equal(startY, pos.Y);
    }

    // ---- Test H: determinism ----

    [Fact]
    public void Determinism_SameInputsProduceIdenticalRawPositions()
    {
        var map = MakeMap(["...#", "...#", "...#"]);

        Fixed32 startX = Fixed32.FromInt(16);
        Fixed32 startY = Fixed32.FromInt(16);
        Fixed32 speed  = Fixed32.FromInt(90);

        var (worldA, eA, sysA) = Setup(map, startX, startY, speed, speed);
        var (worldB, eB, sysB) = Setup(map, startX, startY, speed, speed);

        for (int i = 0; i < 60; i++) sysA.Run(worldA);
        for (int i = 0; i < 60; i++) sysB.Run(worldB);

        var posA = worldA.Get<SimPosition>(eA);
        var posB = worldB.Get<SimPosition>(eB);

        Assert.Equal(posA.X.RawValue, posB.X.RawValue);
        Assert.Equal(posA.Y.RawValue, posB.Y.RawValue);
    }

    // ---- Collision sanity helper ----

    /// <summary>
    /// Asserts that the player AABB does not overlap any solid tile.
    /// Uses strict inequality so exact boundary contact is allowed.
    /// </summary>
    private static void AssertNotOverlappingSolids(
        Fixed32 cx, Fixed32 cy,
        Fixed32 halfW, Fixed32 halfH,
        TileCollisionMap map)
    {
        Fixed32 minX = cx - halfW;
        Fixed32 maxX = cx + halfW;
        Fixed32 minY = cy - halfH;
        Fixed32 maxY = cy + halfH;

        Fixed32 tileSize = TileCollisionMap.TileSize;

        int txMin = (minX / tileSize).RawValue >> Fixed32.FractionalBits;
        int txMax = (maxX / tileSize).RawValue >> Fixed32.FractionalBits;
        int tyMin = (minY / tileSize).RawValue >> Fixed32.FractionalBits;
        int tyMax = (maxY / tileSize).RawValue >> Fixed32.FractionalBits;

        for (int tx = txMin; tx <= txMax; tx++)
        for (int ty = tyMin; ty <= tyMax; ty++)
        {
            if (!map.IsSolid(tx, ty)) continue;

            Fixed32 tileL = Fixed32.FromInt(tx)     * tileSize;
            Fixed32 tileR = Fixed32.FromInt(tx + 1) * tileSize;
            Fixed32 tileT = Fixed32.FromInt(ty)     * tileSize;
            Fixed32 tileB = Fixed32.FromInt(ty + 1) * tileSize;

            bool overlap = maxX > tileL && minX < tileR
                        && maxY > tileT && minY < tileB;

            Assert.False(overlap,
                $"Player AABB [{minX},{minY}..{maxX},{maxY}] " +
                $"overlaps solid tile ({tx},{ty}) [{tileL},{tileT}..{tileR},{tileB}]");
        }
    }
}
