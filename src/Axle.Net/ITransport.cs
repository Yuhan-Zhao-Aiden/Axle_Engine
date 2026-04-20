namespace Axle.Net;

public interface ITransport
{
    void Start(int port);
    void Stop();
    void Send(NetEndpoint endpoint, ReadOnlySpan<byte> payload);
    bool TryReceive(out TransportPacket packet);
}
