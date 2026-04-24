using Axle.Ecs;
using Axle.Net;
using Axle.Sim;

namespace Axle.Server;

public sealed class NetworkInputSystem : ISystem
{
    private readonly IReadOnlyDictionary<NetEndpoint, BufferedInput> _inputBuffers;
    private readonly IReadOnlyDictionary<NetEndpoint, (int Index, int Version)>? _clientEntities;

    public NetworkInputSystem(
        IReadOnlyDictionary<NetEndpoint, BufferedInput> inputBuffers,
        IReadOnlyDictionary<NetEndpoint, (int Index, int Version)>? clientEntities = null)
    {
        _inputBuffers   = inputBuffers;
        _clientEntities = clientEntities;
    }

    public void Run(World world)
    {
        if (_clientEntities != null)
        {
            // Per-entity routing: each client's input goes to their own entity.
            foreach (var (endpoint, entity) in _clientEntities)
            {
                int dx = 0, dy = 0;
                if (_inputBuffers.TryGetValue(endpoint, out var buf) && buf.HasInput)
                {
                    var buttons = (InputButtons)buf.LatestState.Buttons;
                    if ((buttons & InputButtons.Left)  != 0) dx -= 1;
                    if ((buttons & InputButtons.Right) != 0) dx += 1;
                    if ((buttons & InputButtons.Up)    != 0) dy -= 1;
                    if ((buttons & InputButtons.Down)  != 0) dy += 1;
                }
                ref var input = ref world.Get<MoveInput>(new EntityId(entity.Index, entity.Version));
                input.X = dx;
                input.Y = dy;
            }
        }
        else
        {
            // Fallback: apply first client's input to all LocalPlayer entities.
            int dx = 0, dy = 0;
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
}
