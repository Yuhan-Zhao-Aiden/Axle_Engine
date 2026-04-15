using Axle.Core;
using Axle.Ecs;

namespace Axle.Sim;

public sealed class SimRunner : ISimStage
{
    private readonly World _world;
    private readonly ISystem[] _systems;

    public SimRunner(World world, params ISystem[] systems)
    {
        _world = world;
        _systems = systems;
    }

    public void Step(double fixedDtSeconds, ulong tickIndex)
    {
        foreach (var system in _systems)
            system.Run(_world);
    }
}
