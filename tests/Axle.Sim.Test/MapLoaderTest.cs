namespace Axle.Sim.Test;

using Axle.Sim;
using Axle.Sim.Map;

public class MapLoaderTest
{
    // -----------------------------------------------------------------------
    // Tile storage
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_RectangularMap_StoresCorrectTiles()
    {
        string[] lines =
        [
            "#####",
            "#...#",
            "#####",
        ];

        MapData map = MapLoader.Parse(lines);

        Assert.Equal(5, map.Width);
        Assert.Equal(3, map.Height);

        // Corners and edges are walls
        Assert.True(map.TryGetTile(0, 0, out TileType t00)); Assert.Equal(TileType.Wall,  t00);
        Assert.True(map.TryGetTile(4, 2, out TileType t42)); Assert.Equal(TileType.Wall,  t42);

        // Interior is floor
        Assert.True(map.TryGetTile(1, 1, out TileType t11)); Assert.Equal(TileType.Floor, t11);
        Assert.True(map.TryGetTile(3, 1, out TileType t31)); Assert.Equal(TileType.Floor, t31);
    }

    // -----------------------------------------------------------------------
    // Void / non-rectangular
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_IrregularMap_VoidForMissingCells()
    {
        // Row 1 is shorter than row 0 — cells beyond its length are void
        string[] lines =
        [
            "##########",
            "#....#",
            "#..A",
            "##########",
        ];

        MapData map = MapLoader.Parse(lines);

        Assert.Equal(10, map.Width);

        // Cells within the short rows are authored
        Assert.True(map.TryGetTile(0, 1, out TileType wall)); Assert.Equal(TileType.Wall, wall);
        Assert.True(map.TryGetTile(1, 1, out TileType floor)); Assert.Equal(TileType.Floor, floor);

        // Cells beyond the short row's length are void
        Assert.False(map.TryGetTile(7, 1, out _));
        Assert.False(map.TryGetTile(5, 2, out _));
        Assert.False(map.TryGetTile(9, 2, out _));
    }

    [Fact]
    public void TryGetTile_OutOfBounds_ReturnsFalse()
    {
        MapData map = MapLoader.Parse(["#"]);

        Assert.False(map.TryGetTile(-1,  0, out _));
        Assert.False(map.TryGetTile( 0, -1, out _));
        Assert.False(map.TryGetTile( 1,  0, out _));
        Assert.False(map.TryGetTile( 0,  1, out _));
    }

    [Fact]
    public void TryGetTile_VoidCell_ReturnsFalse()
    {
        // Row 1 is a single '#'; column 1+ should be void
        string[] lines = ["##", "#"];
        MapData map = MapLoader.Parse(lines);

        Assert.True (map.TryGetTile(0, 1, out _)); // authored
        Assert.False(map.TryGetTile(1, 1, out _)); // void — outside row length
    }

    // -----------------------------------------------------------------------
    // Spawn extraction
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_ExtractsPlayerSpawn()
    {
        string[] lines = ["#A#"];
        MapData map = MapLoader.Parse(lines);

        Assert.Single(map.PlayerSpawns);
        Assert.Equal(new MapPoint(1, 0), map.PlayerSpawns[0]);
    }

    [Fact]
    public void Parse_PlayerSpawnTile_IsFloor()
    {
        // 'A' should place a Floor tile, not leave the cell void
        MapData map = MapLoader.Parse(["A"]);

        Assert.True(map.TryGetTile(0, 0, out TileType tile));
        Assert.Equal(TileType.Floor, tile);
    }

    [Fact]
    public void Parse_ExtractsCoinAndEnemySpawns()
    {
        string[] lines = ["CE"];
        MapData map = MapLoader.Parse(lines);

        Assert.Single(map.CoinSpawns);
        Assert.Equal(new MapPoint(0, 0), map.CoinSpawns[0]);

        Assert.Single(map.EnemySpawns);
        Assert.Equal(new MapPoint(1, 0), map.EnemySpawns[0]);
    }

    // -----------------------------------------------------------------------
    // Preprocessing
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_StripsBlanksAndComments()
    {
        string[] lines =
        [
            "",
            "   ",
            "; this is a semicolon comment",
            "// this is a double-slash comment",
            "##",
            "",
            "#.",
        ];

        MapData map = MapLoader.Parse(lines);

        // Only the two '#' rows survive — 2 rows, 2 wide
        Assert.Equal(2, map.Height);
        Assert.Equal(2, map.Width);
        Assert.True(map.TryGetTile(0, 0, out TileType t)); Assert.Equal(TileType.Wall, t);
        Assert.True(map.TryGetTile(1, 1, out TileType f)); Assert.Equal(TileType.Floor, f);
    }

    // -----------------------------------------------------------------------
    // Validation — must throw
    // -----------------------------------------------------------------------

    [Fact]
    public void Parse_SpaceChar_IsInlineVoid()
    {
        // A space inside a row is explicit inline void — no tile, no error.
        MapData map = MapLoader.Parse(["# #"]);

        Assert.True (map.TryGetTile(0, 0, out _)); // '#' authored
        Assert.False(map.TryGetTile(1, 0, out _)); // ' ' is void
        Assert.True (map.TryGetTile(2, 0, out _)); // '#' authored
    }

    [Fact]
    public void Parse_InvalidCharacter_Throws()
    {
        Assert.Throws<MapParseException>(() => MapLoader.Parse(["#..X..#"]));
    }

    [Fact]
    public void Parse_EmptyInput_Throws()
    {
        Assert.Throws<MapParseException>(() => MapLoader.Parse([]));
    }

    [Fact]
    public void Parse_OnlyBlanksAndComments_Throws()
    {
        string[] lines = ["", "  ", "; comment", "// another"];
        Assert.Throws<MapParseException>(() => MapLoader.Parse(lines));
    }

    // -----------------------------------------------------------------------
    // Platformer mode — spawn chars leave tiles void
    // -----------------------------------------------------------------------

    [Fact]
    public void Platformer_SpawnChars_TileIsVoid()
    {
        // In Platformer mode, A / C / E must NOT place a Floor tile.
        string[] lines = ["#ACE#"];
        MapData map = MapLoader.Parse(lines, GameMode.Platformer);

        Assert.False(map.TryGetTile(1, 0, out _)); // 'A' → void
        Assert.False(map.TryGetTile(2, 0, out _)); // 'C' → void
        Assert.False(map.TryGetTile(3, 0, out _)); // 'E' → void

        // Walls still present
        Assert.True(map.TryGetTile(0, 0, out TileType wall)); Assert.Equal(TileType.Wall, wall);
    }

    [Fact]
    public void Platformer_SpawnChars_StillRecorded()
    {
        // Spawn lists must still be populated even though tiles are void.
        string[] lines = ["#ACE#"];
        MapData map = MapLoader.Parse(lines, GameMode.Platformer);

        Assert.Single(map.PlayerSpawns);  Assert.Equal(new MapPoint(1, 0), map.PlayerSpawns[0]);
        Assert.Single(map.CoinSpawns);    Assert.Equal(new MapPoint(2, 0), map.CoinSpawns[0]);
        Assert.Single(map.EnemySpawns);   Assert.Equal(new MapPoint(3, 0), map.EnemySpawns[0]);
    }
}
