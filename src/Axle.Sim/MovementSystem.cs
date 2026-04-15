using Axle.Core.AxleMath;
using Axle.Ecs;

namespace Axle.Sim;

/// <summary>
/// Advances SimPosition by Velocity * dt each simulation tick.
/// Runs in the Sim stage. Uses only fixed-point math for determinism.
/// </summary>
public sealed class MovementSystem : ISystem
{
    private readonly Fixed32 _dt;

    public MovementSystem(Fixed32 dt)
    {
        _dt = dt;
    }

    public void Run(World world)
    {
        foreach (var item in world.Query<SimPosition, Velocity>())
        {
            item.Component1.X += item.Component2.X * _dt;
            item.Component1.Y += item.Component2.Y * _dt;
        }
    }
}
