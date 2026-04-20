namespace Axle.Net;

public readonly struct TransportPacket
{
    public NetEndpoint Source { get; init; }
    public byte[] Payload { get; init; }
}