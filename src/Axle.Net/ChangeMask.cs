namespace Axle.Net;

[Flags]
public enum ChangeMask : byte
{
    None      = 0,
    PositionX = 1 << 0,
    PositionY = 1 << 1,
}
