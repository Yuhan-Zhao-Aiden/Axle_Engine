namespace Axle.Net;

public enum ConnectionState : byte
{
    Disconnected = 0,
    Connecting = 1,
    Connected = 2,
    TimedOut = 3,
}