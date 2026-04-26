using System.Text.Json.Serialization;

namespace Axle.Client;

/// <summary>
/// Per-state settings declared in sprites.json.
/// </summary>
public sealed class AnimStateConfig
{
    [JsonPropertyName("fps")]
    public float Fps { get; init; } = 8f;

    [JsonPropertyName("loop")]
    public bool Loop { get; init; } = true;
}

/// <summary>
/// Config block for one entity type (e.g. "player", "coin") in sprites.json.
/// BaseFolder is relative to the content root (e.g. "assets/sprites/character").
/// Each key in States maps to a subfolder: {BaseFolder}/{stateName}/1.png, 2.png, ...
/// </summary>
public sealed class EntityAnimConfig
{
    [JsonPropertyName("baseFolder")]
    public string BaseFolder { get; init; } = string.Empty;

    [JsonPropertyName("states")]
    public Dictionary<string, AnimStateConfig> States { get; init; } = new();
}
