namespace Axle.Net;

public enum PacketType : byte
{
    ConnectRequest = 1,
    ConnectAccept = 2,
    ConnectReject = 3,
    Ping = 4,
    Pong = 5,
}