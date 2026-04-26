namespace Axle.Sim.Test;

using Axle.Sim;
using Axle.Sim.Map;

/// <summary>
/// Unit tests for <see cref="TileSetLoader"/>.
/// Sprite-existence checks are only executed when <c>contentRoot</c> is non-empty,
/// so most tests pass an empty string to keep tests hermetic (no disk I/O).
/// </summary>
public class TileSetLoaderTest
{
    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    /// <summary>Minimal valid JSON with one solid wall tile.</summary>
    private static string MinimalJson(
        int tileSize = 32,
        string key = "#",
        string name = "Wall",
        string sprite = "assets/tiles/wall.png",
        bool solid = true) =>
        $$"""
        {
            "tileSize": {{tileSize}},
            "tiles": {
                "{{key}}": { "name": "{{name}}", "sprite": "{{sprite}}", "solid": {{(solid ? "true" : "false")}} }
            }
        }
        """;

    // ------------------------------------------------------------------
    // Happy path
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_ValidJson_ReturnsTileSet()
    {
        string json = """
        {
            "tileSize": 32,
            "tiles": {
                "#": { "name": "Wall",  "sprite": "assets/wall.png",  "solid": true  },
                ".": { "name": "Floor", "sprite": "assets/floor.png", "solid": false }
            }
        }
        """;

        TileSet ts = TileSetLoader.Parse(json, contentRoot: "");

        Assert.Equal(32, ts.TileSize);
        Assert.Equal(2, ts.Tiles.Count);

        Assert.True(ts.Tiles.TryGetValue('#', out TileDefinition? wall));
        Assert.Equal("Wall",            wall!.Name);
        Assert.Equal("assets/wall.png", wall.SpritePath);
        Assert.True(wall.Solid);

        Assert.True(ts.Tiles.TryGetValue('.', out TileDefinition? floor));
        Assert.False(floor!.Solid);
    }

    [Fact]
    public void Parse_OptionalSpawnAndEntityFields_Parsed()
    {
        string json = """
        {
            "tileSize": 32,
            "tiles": {
                "A": { "name": "PlayerSpawn", "sprite": "assets/spawn.png", "solid": false,
                       "spawn": "player" },
                "C": { "name": "Coin",        "sprite": "assets/coin.png",  "solid": false,
                       "entity": "coin" }
            }
        }
        """;

        TileSet ts = TileSetLoader.Parse(json, contentRoot: "");

        Assert.Equal("player", ts.Tiles['A'].Spawn);
        Assert.Null(ts.Tiles['A'].Entity);

        Assert.Equal("coin", ts.Tiles['C'].Entity);
        Assert.Null(ts.Tiles['C'].Spawn);
    }

    [Fact]
    public void Parse_CommentsAndTrailingCommas_Tolerated()
    {
        // System.Text.Json with the configured options should accept both.
        string json = """
        {
            // tile size comment
            "tileSize": 32,
            "tiles": {
                "#": { "name": "Wall", "sprite": "assets/w.png", "solid": true, },
            },
        }
        """;

        TileSet ts = TileSetLoader.Parse(json, contentRoot: "");
        Assert.Single(ts.Tiles);
    }

    // ------------------------------------------------------------------
    // tileSize validation
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_TileSizeNot32_Throws()
    {
        string json = MinimalJson(tileSize: 16);
        Assert.Throws<TileSetLoadException>(() => TileSetLoader.Parse(json, ""));
    }

    [Fact]
    public void Parse_MissingTileSize_Throws()
    {
        string json = """{ "tiles": { "#": { "name": "W", "sprite": "a.png", "solid": true } } }""";
        Assert.Throws<TileSetLoadException>(() => TileSetLoader.Parse(json, ""));
    }

    // ------------------------------------------------------------------
    // Key validation
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_MultiCharKey_Throws()
    {
        string json = """
        {
            "tileSize": 32,
            "tiles": { "##": { "name": "Wall", "sprite": "a.png", "solid": true } }
        }
        """;
        Assert.Throws<TileSetLoadException>(() => TileSetLoader.Parse(json, ""));
    }

    // ------------------------------------------------------------------
    // Required field validation
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_MissingName_Throws()
    {
        string json = """
        {
            "tileSize": 32,
            "tiles": { "#": { "sprite": "a.png", "solid": true } }
        }
        """;
        Assert.Throws<TileSetLoadException>(() => TileSetLoader.Parse(json, ""));
    }

    [Fact]
    public void Parse_MissingSprite_Throws()
    {
        string json = """
        {
            "tileSize": 32,
            "tiles": { "#": { "name": "Wall", "solid": true } }
        }
        """;
        Assert.Throws<TileSetLoadException>(() => TileSetLoader.Parse(json, ""));
    }

    [Fact]
    public void Parse_MissingSolid_Throws()
    {
        string json = """
        {
            "tileSize": 32,
            "tiles": { "#": { "name": "Wall", "sprite": "a.png" } }
        }
        """;
        Assert.Throws<TileSetLoadException>(() => TileSetLoader.Parse(json, ""));
    }

    // ------------------------------------------------------------------
    // Empty tiles
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_EmptyTilesObject_Throws()
    {
        string json = """{ "tileSize": 32, "tiles": {} }""";
        Assert.Throws<TileSetLoadException>(() => TileSetLoader.Parse(json, ""));
    }

    [Fact]
    public void Parse_MissingTilesField_Throws()
    {
        string json = """{ "tileSize": 32 }""";
        Assert.Throws<TileSetLoadException>(() => TileSetLoader.Parse(json, ""));
    }

    // ------------------------------------------------------------------
    // Malformed JSON
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_MalformedJson_Throws()
    {
        Assert.Throws<TileSetLoadException>(() => TileSetLoader.Parse("{ not json }", ""));
    }

    // ------------------------------------------------------------------
    // Sprite file existence — requires real temp directory
    // ------------------------------------------------------------------

    [Fact]
    public void Parse_MissingSpriteFile_WhenContentRootProvided_Throws()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tempDir);
        try
        {
            // sprite path "assets/missing.png" does not exist in tempDir
            string json = MinimalJson(sprite: "assets/missing.png");
            Assert.Throws<TileSetLoadException>(() => TileSetLoader.Parse(json, tempDir));
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Parse_PresentSpriteFile_WhenContentRootProvided_DoesNotThrow()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        string spriteRelPath = "assets/wall.png";
        string spriteAbsPath = Path.Combine(tempDir, spriteRelPath);
        Directory.CreateDirectory(Path.GetDirectoryName(spriteAbsPath)!);
        File.WriteAllBytes(spriteAbsPath, []); // empty placeholder file
        try
        {
            string json = MinimalJson(sprite: spriteRelPath);
            TileSet ts = TileSetLoader.Parse(json, tempDir); // should not throw
            Assert.Single(ts.Tiles);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void Parse_EmptyContentRoot_SkipsSpriteExistenceCheck()
    {
        // sprite path "nonexistent/path.png" would fail if contentRoot were provided,
        // but with contentRoot="" the check is skipped entirely.
        string json = MinimalJson(sprite: "nonexistent/path.png");
        TileSet ts = TileSetLoader.Parse(json, contentRoot: ""); // must not throw
        Assert.Single(ts.Tiles);
    }

    // ------------------------------------------------------------------
    // Load() — file not found propagates IOException
    // ------------------------------------------------------------------

    [Fact]
    public void Load_NonExistentFile_ThrowsIOException()
    {
        // On Windows, a path with non-existent parent directories raises
        // DirectoryNotFoundException (a subclass of IOException), not FileNotFoundException.
        Assert.ThrowsAny<IOException>(
            () => TileSetLoader.Load("/does/not/exist.tiles.json", ""));
    }
}
