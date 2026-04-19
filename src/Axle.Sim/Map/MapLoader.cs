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
                        tiles[x, y] = TileType.Wall;
                        authoredCount++;
                        break;

                    case '.':
                        tiles[x, y] = TileType.Floor;
                        authoredCount++;
                        break;

                    case 'A':
                        if (mode == GameMode.TopDown) { tiles[x, y] = TileType.Floor; authoredCount++; }
                        playerSpawns.Add(new MapPoint(x, y));
                        break;

                    case 'C':
                        if (mode == GameMode.TopDown) { tiles[x, y] = TileType.Floor; authoredCount++; }
                        coinSpawns.Add(new MapPoint(x, y));
                        break;

                    case 'E':
                        if (mode == GameMode.TopDown) { tiles[x, y] = TileType.Floor; authoredCount++; }
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

        return new MapData(tiles, playerSpawns, enemySpawns, coinSpawns);
    }
}
