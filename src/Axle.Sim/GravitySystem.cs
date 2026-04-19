namespace Axle.Sim;

using Axle.Core.AxleMath;
using Axle.Ecs;

/// <summary>
/// Applies constant downward acceleration each tick, clamped to terminal velocity.
/// </summary>
public sealed class GravitySystem : ISystem
{
    private static readonly Fixed32 GravityAccel    = Fixed32.FromInt(600);
    private static readonly Fixed32 TerminalVelocity = Fixed32.FromInt(400);

    public void Run(World world)
    {
        Fixed32 delta = GravityAccel * SimTime.Dt;

        foreach (ref var vel in world.Query<Velocity>())
        {
            vel.Y += delta;
            if (vel.Y > TerminalVelocity)
                vel.Y = TerminalVelocity;
        }
    }
}
