using System.Text.Json.Serialization;

namespace Axle.Client;

[JsonConverter(typeof(JsonStringEnumConverter<CameraMode>))]
public enum CameraMode
{
    Fixed,
    Follow,
}

public sealed class CameraSettings
{
    [JsonPropertyName("mode")]
    public CameraMode Mode { get; init; } = CameraMode.Fixed;

    [JsonPropertyName("smoothing")]
    public bool Smoothing { get; init; } = false;

    [JsonPropertyName("fixedX")]
    public float FixedX { get; init; } = 0f;

    [JsonPropertyName("fixedY")]
    public float FixedY { get; init; } = 0f;
}
