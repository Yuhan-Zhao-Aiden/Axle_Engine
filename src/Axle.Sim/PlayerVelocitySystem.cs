using Axle.Core.AxleMath;
using Axle.Ecs;

namespace Axle.Sim;

/// <summary>
/// Converts MoveInput into Velocity for the local player entity.
/// Runs in the PreSim stage. Diagonal movement is unnormalized (Option A).
/// </summary>
public sealed class PlayerVelocitySystem : ISystem
{
    public static readonly Fixed32 MoveSpeed = Fixed32.FromInt(120);

    public void Run(World world)
    {
        var velStore = world.Store<Velocity>();

        foreach (var item in world.Query<LocalPlayer, MoveInput>())
        {
            ref var vel = ref velStore.Get(item.Entity);
            vel.X = Fixed32.FromInt(item.Component2.X) * MoveSpeed;
            vel.Y = Fixed32.FromInt(item.Component2.Y) * MoveSpeed;
        }
    }
}
