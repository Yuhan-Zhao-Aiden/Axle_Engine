using System.Text.Json.Serialization;

namespace Axle.Net;

public sealed class NetSimSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; }

    [JsonPropertyName("baseDelayMs")]
    public int BaseDelayMs { get; init; } = 50;

    [JsonPropertyName("jitterMs")]
    public int JitterMs { get; init; } = 15;

    [JsonPropertyName("lossPercent")]
    public float LossPercent { get; init; } = 2f;
}
