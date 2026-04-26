namespace Axle.Sim.Map;

using Axle.Sim;

/// <summary>
/// Loads and parses <c>.map</c> text files into <see cref="MapData"/>.
///
/// Format rules:
///   - Blank lines are ignored.
///   - Lines starting with <c>;</c> or <c>//</c> are comments and are ignored.
///   - Remaining lines are map rows (top → bottom).
///   - Map width = max row length across all rows.
///   - Missing cells within a row shorter than map width are Void.
///
/// Legend:
///   <c>#</c>  Wall
///   <c>.</c>  Floor
///   <c>A</c>  Player spawn  (tile becomes Floor)
///   <c>C</c>  Coin spawn    (tile becomes Floor)
///   <c>E</c>  Enemy spawn   (tile becomes Floor)
/// </summary>
public static class MapLoader
{
    /// <summary>
    /// Read a <c>.map</c> file from disk and parse it.
    /// Throws <see cref="IOException"/> if the file cannot be read.
    /// Throws <see cref="MapParseException"/> if the content is invalid.
    /// </summary>
    public static MapData Load(string filePath, GameMode mode = GameMode.TopDown)
    {
        string[] lines = File.ReadAllLines(filePath);
        return Parse(lines, mode);
    }

    /// <summary>
    /// Parse an array of raw lines (e.g. from a file or a test fixture).
    /// Throws <see cref="MapParseException"/> if the content is invalid.
    /// </summary>
    public static MapData Parse(string[] lines, GameMode mode = GameMode.TopDown)
    {
        // --- Preprocessing: strip blanks and comments ---
        List<string> rows = new(lines.Length);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))         continue;
            if (line.StartsWith(';'))                    continue;
            if (line.StartsWith("//", StringComparison.Ordinal)) continue;
            rows.Add(line);
        }

        if (rows.Count == 0)
            throw new MapParseException("Map contains no valid rows.");

        // --- Determine dimensions ---
        int height   = rows.Count;
        int width    = 0;
        foreach (string r in rows)
            if (r.Length > width) width = r.Length;

        // --- Allocate storage ---
        TileType?[,]  tiles        = new TileType?[width, height];
        char?[,]      symbols      = new char?[width, height];
        List<MapPoint> playerSpawns = [];
        List<MapPoint> enemySpawns  = [];
        List<MapPoint> coinSpawns   = [];
        int            authoredCount = 0;

        // --- Parsing ---
        for (int y = 0; y < height; y++)
        {
            string row = rows[y];
            for (int x = 0; x < row.Length; x++)
            {
                char ch = row[x];
                switch (ch)
                {
                    case '#':
                        tiles[x, y]   = TileType.Wall;
                        symbols[x, y] = ch;
                        authoredCount++;
                        break;

                    case '.':
                        tiles[x, y]   = TileType.Floor;
                        symbols[x, y] = ch;
                        authoredCount++;
                        break;

                    case 'A':
                        if (mode == GameMode.TopDown) { tiles[x, y] = TileType.Floor; authoredCount++; }
                        symbols[x, y] = ch;
                        playerSpawns.Add(new MapPoint(x, y));
                        break;

                    case 'C':
                        if (mode == GameMode.TopDown) { tiles[x, y] = TileType.Floor; authoredCount++; }
                        symbols[x, y] = ch;
                        coinSpawns.Add(new MapPoint(x, y));
                        break;

                    case 'E':
                        if (mode == GameMode.TopDown) { tiles[x, y] = TileType.Floor; authoredCount++; }
                        symbols[x, y] = ch;
                        enemySpawns.Add(new MapPoint(x, y));
                        break;

                    case ' ':
                        break;

                    default:
                        throw new MapParseException(
                            $"Unknown character '{ch}' at row {y + 1}, column {x + 1}.");
                }
            }
        }

        if (authoredCount == 0)
            throw new MapParseException("Map contains no authored tiles.");

        return new MapData(tiles, symbols, playerSpawns, enemySpawns, coinSpawns);
    }

    // -----------------------------------------------------------------------
    // TileSet-driven overload
    // -----------------------------------------------------------------------

    /// <summary>
    /// Load a <c>.map</c> file using a <see cref="TileSet"/> to determine which
    /// characters are valid and how they map to collision / spawn data.
    /// Throws <see cref="IOException"/> if the file cannot be read.
    /// Throws <see cref="MapParseException"/> if the content is invalid.
    /// </summary>
    public static MapData Load(string filePath, TileSet tileSet, GameMode mode = GameMode.TopDown)
    {
        string[] lines = File.ReadAllLines(filePath);
        return Parse(lines, tileSet, mode);
    }

    /// <summary>
    /// Parse map lines using a <see cref="TileSet"/> for character validation and
    /// tile-type resolution.
    ///
    /// Rules:
    ///   - Only characters present in <paramref name="tileSet"/> are valid.
    ///   - <c>TileDefinition.Solid == true</c>  → <see cref="TileType.Wall"/>.
    ///   - <c>TileDefinition.Solid == false</c> → <see cref="TileType.Floor"/>.
    ///   - <c>TileDefinition.Spawn == "player"</c>  → added to <see cref="MapData.PlayerSpawns"/>.
    ///   - <c>TileDefinition.Spawn == "enemy"</c>   → added to <see cref="MapData.EnemySpawns"/>.
    ///   - <c>TileDefinition.Entity == "coin"</c>   → added to <see cref="MapData.CoinSpawns"/>.
    ///   - Spaces are always skipped (void).
    ///   - Blank lines and comment lines (<c>;</c> / <c>//</c>) are ignored.
    /// </summary>
    public static MapData Parse(string[] lines, TileSet tileSet, GameMode mode = GameMode.TopDown)
    {
        // --- Preprocessing: strip blanks and comments ---
        List<string> rows = new(lines.Length);
        foreach (string line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))                              continue;
            if (line.StartsWith(';'))                                         continue;
            if (line.StartsWith("//", StringComparison.Ordinal))              continue;
            rows.Add(line);
        }

        if (rows.Count == 0)
            throw new MapParseException("Map contains no valid rows.");

        // --- Determine dimensions ---
        int height = rows.Count;
        int width  = 0;
        foreach (string r in rows)
            if (r.Length > width) width = r.Length;

        // --- Allocate storage ---
        TileType?[,]   tiles        = new TileType?[width, height];
        char?[,]       symbols      = new char?[width, height];
        List<MapPoint> playerSpawns = [];
        List<MapPoint> enemySpawns  = [];
        List<MapPoint> coinSpawns   = [];
        int            authoredCount = 0;

        // --- Parsing ---
        for (int y = 0; y < height; y++)
        {
            string row = rows[y];
            for (int x = 0; x < row.Length; x++)
            {
                char ch = row[x];

                if (ch == ' ')
                    continue;   // spaces are always void

                if (!tileSet.Tiles.TryGetValue(ch, out TileDefinition? def))
                    throw new MapParseException(
                        $"Unknown character '{ch}' at row {y + 1}, column {x + 1}.");

                // Always record the symbol for texture lookup
                symbols[x, y] = ch;

                // Resolve tile type from solid flag
                TileType tileType = def.Solid ? TileType.Wall : TileType.Floor;

                // For non-solid spawn tiles in Platformer mode, skip placing a tile
                // (mirrors existing behaviour: spawn chars don't floor-fill in Platformer).
                bool isSpawn  = !string.IsNullOrEmpty(def.Spawn);
                bool isEntity = !string.IsNullOrEmpty(def.Entity);

                if (isSpawn || isEntity)
                {
                    // Collect spawn / entity positions
                    if (!string.IsNullOrEmpty(def.Spawn))
                    {
                        switch (def.Spawn)
                        {
                            case "player": playerSpawns.Add(new MapPoint(x, y)); break;
                            case "enemy":  enemySpawns.Add(new MapPoint(x, y));  break;
                        }
                    }

                    if (!string.IsNullOrEmpty(def.Entity))
                    {
                        if (def.Entity == "coin")
                            coinSpawns.Add(new MapPoint(x, y));
                    }

                    // Place an underlying tile only in TopDown mode (same rule as original loader)
                    if (!def.Solid && mode == GameMode.TopDown)
                    {
                        tiles[x, y] = tileType;
                        authoredCount++;
                    }
                    else if (def.Solid)
                    {
                        tiles[x, y] = tileType;
                        authoredCount++;
                    }
                }
                else
                {
                    tiles[x, y] = tileType;
                    authoredCount++;
                }
            }
        }

        if (authoredCount == 0)
            throw new MapParseException("Map contains no authored tiles.");

        return new MapData(tiles, symbols, playerSpawns, enemySpawns, coinSpawns);
    }
}
