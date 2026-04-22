namespace Axle.Net;

/// <summary>
/// Compact input payload sent from client to server each network tick.
/// </summary>
public struct InputState
{
    public ushort Seq;

    public ushort Buttons;
}

/// <summary>
/// Bitfield for player input directions and actions.
/// </summary>
[Flags]
public enum InputButtons : ushort
{
    None  = 0,
    Up    = 1 << 0,
    Down  = 1 << 1,
    Left  = 1 << 2,
    Right = 1 << 3,
    Jump  = 1 << 4,
}
