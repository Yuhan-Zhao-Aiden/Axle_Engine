using Axle.Net;

namespace Axle.Net.Test;

// Captures every Send call so tests can inspect what was transmitted.
internal sealed class FakeTransport : ITransport
{
    public record SentPacket(NetEndpoint Endpoint, byte[] Payload);

    public List<SentPacket> Sent { get; } = [];

    public void Start(int port) { }
    public void Stop() { }

    public void Send(NetEndpoint endpoint, ReadOnlySpan<byte> payload)
        => Sent.Add(new(endpoint, payload.ToArray()));

    public bool TryReceive(out TransportPacket packet)
    {
        packet = default;
        return false;
    }
}
