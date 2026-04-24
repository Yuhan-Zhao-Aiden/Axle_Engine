using Axle.Ecs;

namespace Axle.Sim;

/// <summary>
/// One client input snapshot stored for client-side prediction replay.
/// </summary>
public readonly struct InputRecord
{
    public ushort    Seq   { get; init; }
    public MoveInput Input { get; init; }
}
