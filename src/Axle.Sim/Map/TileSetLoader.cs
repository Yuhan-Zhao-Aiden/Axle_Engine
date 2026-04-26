namespace Axle.Sim.Map;

using System.Text.Json;
using System.Text.Json.Nodes;

/// <summary>
/// Loads and validates a <c>.tiles.json</c> tile mapping file into a <see cref="TileSet"/>.
///
/// JSON format:
/// <code>
/// {
///   "tileSize": 32,
///   "tiles": {
///     "#": { "name": "Wall",  "sprite": "assets/tiles/wall.png",  "solid": true  },
///     ".": { "name": "Floor", "sprite": "assets/tiles/grass.png", "solid": false }
///   }
/// }
/// </code>
///
/// Validation rules (fast-fail):
///   - <c>tileSize</c> must equal 32.
///   - Every key in <c>tiles</c> must be exactly one character.
///   - Every tile entry must have <c>name</c>, <c>sprite</c>, and <c>solid</c>.
///   - Every referenced sprite file must exist relative to <paramref name="contentRoot"/>.
/// </summary>
public static class TileSetLoader
{
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    /// <summary>
    /// Load a <c>.tiles.json</c> from <paramref name="tilesJsonPath"/> and validate it.
    /// </summary>
    /// <param name="tilesJsonPath">Absolute or relative path to the <c>.tiles.json</c> file.</param>
    /// <param name="contentRoot">
    /// Root directory used to resolve sprite paths declared inside the JSON.
    /// Typically the directory that contains the <c>assets/</c> folder.
    /// </param>
    /// <exception cref="IOException">If the file cannot be read.</exception>
    /// <exception cref="TileSetLoadException">If the file fails any validation rule.</exception>
    public static TileSet Load(string tilesJsonPath, string contentRoot)
    {
        string json = File.ReadAllText(tilesJsonPath);
        return Parse(json, contentRoot);
    }

    /// <summary>
    /// Parse a raw JSON string. Useful for unit tests and for loading tile mappings
    /// from sources other than files (e.g. embedded resources).
    /// Pass an empty string for <paramref name="contentRoot"/> to skip sprite-existence checks.
    /// </summary>
    public static TileSet Parse(string json, string contentRoot)
    {
        JsonNode root;
        try
        {
            root = JsonNode.Parse(json, nodeOptions: null,
                documentOptions: new JsonDocumentOptions
                {
                    CommentHandling   = JsonCommentHandling.Skip,
                    AllowTrailingCommas = true,
                })!;
        }
        catch (JsonException ex)
        {
            throw new TileSetLoadException($"Tile mapping JSON is malformed: {ex.Message}");
        }

        // --- tileSize ---
        int tileSize = root["tileSize"]?.GetValue<int>()
            ?? throw new TileSetLoadException("Tile mapping missing required field: tileSize.");

        if (tileSize != 32)
            throw new TileSetLoadException($"Tile size must be 32 for MVP, got {tileSize}.");

        // --- tiles object ---
        JsonNode? tilesNode = root["tiles"];
        if (tilesNode is not JsonObject tilesObj)
            throw new TileSetLoadException("Tile mapping missing required field: tiles.");

        var tileSet = new TileSet { TileSize = tileSize };

        foreach (var kvp in tilesObj)
        {
            string key = kvp.Key;

            // Key must be exactly one character
            if (key.Length != 1)
                throw new TileSetLoadException(
                    $"Tile key \"{key}\" must be exactly one character.");

            char symbol = key[0];
            JsonNode? entry = kvp.Value
                ?? throw new TileSetLoadException($"Tile '{symbol}' entry is null.");

            // Required: name
            string name = entry["name"]?.GetValue<string>()
                ?? throw new TileSetLoadException(
                    $"Tile '{symbol}' missing required field: name.");

            // Required: sprite
            string sprite = entry["sprite"]?.GetValue<string>()
                ?? throw new TileSetLoadException(
                    $"Tile '{symbol}' missing required field: sprite.");

            // Required: solid
            bool? solidNullable = entry["solid"]?.GetValue<bool>();
            if (solidNullable is null)
                throw new TileSetLoadException(
                    $"Tile '{symbol}' missing required field: solid.");
            bool solid = solidNullable.Value;

            // Optional fields
            string? spawn  = entry["spawn"]?.GetValue<string>();
            string? entity = entry["entity"]?.GetValue<string>();

            // Validate sprite file exists (if a contentRoot is given and sprite is non-empty)
            if (!string.IsNullOrEmpty(sprite) && !string.IsNullOrEmpty(contentRoot))
            {
                string absoluteSprite = Path.Combine(contentRoot, sprite);
                if (!File.Exists(absoluteSprite))
                    throw new TileSetLoadException(
                        $"Tile '{symbol}' references missing sprite: {sprite}.");
            }

            tileSet.Tiles[symbol] = new TileDefinition
            {
                Symbol     = symbol,
                Name       = name,
                SpritePath = sprite,
                Solid      = solid,
                Spawn      = spawn,
                Entity     = entity,
            };
        }

        if (tileSet.Tiles.Count == 0)
            throw new TileSetLoadException("Tile mapping defines no tiles.");

        return tileSet;
    }
}
