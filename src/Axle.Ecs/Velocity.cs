namespace Axle.Ecs;

using Axle.Core.AxleMath;

/// <summary>
/// Simulation velocity in units per second. Uses Fixed32 for determinism.
/// </summary>
public struct Velocity : IComponent
{
    public Fixed32 X;
    public Fixed32 Y;

    public Velocity(Fixed32 x, Fixed32 y)
    {
        X = x;
        Y = y;
    }
}
