using Axle.Ecs;
using Axle.Net;
using Axle.Sim;

namespace Axle.Server;

public sealed class NetworkInputSystem : ISystem
{
    private readonly IReadOnlyDictionary<NetEndpoint, BufferedInput> _inputBuffers;

    public NetworkInputSystem(IReadOnlyDictionary<NetEndpoint, BufferedInput> inputBuffers)
    {
        _inputBuffers = inputBuffers;
    }

    public void Run(World world)
    {
        int dx = 0;
        int dy = 0;

        foreach (var buf in _inputBuffers.Values)
        {
            if (!buf.HasInput) continue;

            var buttons = (InputButtons)buf.LatestState.Buttons;

            if ((buttons & InputButtons.Left)  != 0) dx -= 1;
            if ((buttons & InputButtons.Right) != 0) dx += 1;
            if ((buttons & InputButtons.Up)    != 0) dy -= 1;
            if ((buttons & InputButtons.Down)  != 0) dy += 1;

            break;
        }

        foreach (var item in world.Query<LocalPlayer, MoveInput>())
        {
            item.Component2.X = dx;
            item.Component2.Y = dy;
        }
    }
}
