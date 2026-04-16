using Axle.Core.AxleMath;
using Axle.Ecs;
using Axle.Sim;

namespace Axle.Client.System;

public sealed class SyncTransformSystem : ISystem
{
    public void Run(World world)
    {
        foreach (var item in world.Query<SimPosition, Transform>())
        {
            item.Component2.Position = new Vector2f(
                item.Component1.X.ToFloat(),
                item.Component1.Y.ToFloat());
        }
    }
}
