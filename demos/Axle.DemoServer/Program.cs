using Axle.Net;

namespace Axle.Server;

public class Program
{
    public static void Main(string[] args)
    {
        using var server = new NetServer(new UdpTransport());
        server.Listen(7777);

        while (true)
            server.Tick();
    }
}