namespace Axle.Sim.Map;

/// <summary>
/// Describes how a single map character should be treated for rendering and simulation.
/// </summary>
public sealed class TileDefinition
{
    /// <summary>The single character used in <c>.map</c> files to represent this tile.</summary>
    public char Symbol { get; init; }

    /// <summary>Human-readable name shown in validation error messages.</summary>
    public string Name { get; init; } = "";

    /// <summary>
    /// Relative path to the tile's PNG sprite, from the game content root.
    /// Empty when no custom sprite is configured (fallback to colored square).
    /// </summary>
    public string SpritePath { get; init; } = "";

    /// <summary>Whether this tile blocks movement / participates in collision.</summary>
    public bool Solid { get; init; }

    /// <summary>
    /// Optional spawn marker, e.g. <c>"player"</c> or <c>"enemy"</c>.
    /// <see langword="null"/> when this tile is not a spawn point.
    /// </summary>
    public string? Spawn { get; init; }

    /// <summary>
    /// Optional entity extraction marker, e.g. <c>"coin"</c>.
    /// <see langword="null"/> when this tile does not produce a world entity.
    /// </summary>
    public string? Entity { get; init; }
}
