namespace Axle.Sim.Map;

/// <summary>
/// A complete tile mapping loaded from a <c>.tiles.json</c> file.
/// Maps each map character to its <see cref="TileDefinition"/>.
/// </summary>
public sealed class TileSet
{
    /// <summary>Expected pixel size of every tile image. Must be 32 for MVP.</summary>
    public int TileSize { get; init; } = 32;

    /// <summary>
    /// All tile definitions keyed by their single-character map symbol.
    /// </summary>
    public Dictionary<char, TileDefinition> Tiles { get; } = new();
}
