using System.Text.Json.Serialization;
using Axle.Sim;

namespace Axle.Client;

public sealed class GameSettings
{
    [JsonPropertyName("mode")]
    [JsonConverter(typeof(JsonStringEnumConverter<GameMode>))]
    public GameMode Mode { get; init; } = GameMode.TopDown;

    [JsonPropertyName("camera")]
    public CameraSettings Camera { get; init; } = new();
}
