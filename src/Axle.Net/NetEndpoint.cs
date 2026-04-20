using System.Net;

namespace Axle.Net;

public readonly record struct NetEndpoint(string Host, int Port)
{
    public IPEndPoint ToIPEndPoint()
        => new(IPAddress.Parse(Host), Port);
}