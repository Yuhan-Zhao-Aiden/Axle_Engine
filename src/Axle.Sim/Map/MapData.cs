namespace Axle.Sim.Map;

public sealed class MapData
{
    // Nullable: null element == void (unauthored cell).
    private readonly TileType?[,] _tiles;

    // The original map character at each authored cell.  Null for void cells.
    private readonly char?[,] _symbols;

    /// <summary>Maximum row length in the source file.</summary>
    public int Width { get; }

    /// <summary>Number of parsed rows.</summary>
    public int Height { get; }

    public IReadOnlyList<MapPoint> PlayerSpawns { get; }
    public IReadOnlyList<MapPoint> EnemySpawns { get; }
    public IReadOnlyList<MapPoint> CoinSpawns { get; }

    internal MapData(
        TileType?[,]    tiles,
        char?[,]        symbols,
        List<MapPoint>  playerSpawns,
        List<MapPoint>  enemySpawns,
        List<MapPoint>  coinSpawns)
    {
        _tiles   = tiles;
        _symbols = symbols;
        Width    = tiles.GetLength(0);
        Height   = tiles.GetLength(1);
        PlayerSpawns = playerSpawns.AsReadOnly();
        EnemySpawns  = enemySpawns.AsReadOnly();
        CoinSpawns   = coinSpawns.AsReadOnly();
    }

    /// <summary>
    /// Try to get the tile at (x, y).
    /// Returns false if the cell is out of bounds or is void (unauthored).
    /// </summary>
    public bool TryGetTile(int x, int y, out TileType tile)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            tile = default;
            return false;
        }

        TileType? cell = _tiles[x, y];
        if (cell is null)
        {
            tile = default;
            return false;
        }

        tile = cell.Value;
        return true;
    }

    /// <summary>
    /// Try to get the original map character at (x, y).
    /// Returns false if the cell is out of bounds or is void.
    /// </summary>
    public bool TryGetSymbol(int x, int y, out char symbol)
    {
        if (x < 0 || x >= Width || y < 0 || y >= Height)
        {
            symbol = default;
            return false;
        }

        char? cell = _symbols[x, y];
        if (cell is null)
        {
            symbol = default;
            return false;
        }

        symbol = cell.Value;
        return true;
    }
}
