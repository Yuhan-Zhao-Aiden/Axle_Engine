using System.Text.Json.Serialization;
using Axle.Net;

namespace Axle.Server;

public sealed class ServerSettings
{
    [JsonPropertyName("netSim")]
    public NetSimSettings? NetSim { get; init; }
}
