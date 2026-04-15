using Axle.Ecs;

namespace Axle.Sim;

public interface ISystem
{
    void Run(World world);
}
