using Axle.Ecs;
using Axle.Net;
using Axle.Server;

namespace Axle.Server.Test;

public sealed class NetworkInputSystemTest
{
    private static readonly NetEndpoint Peer = new("127.0.0.1", 10001);

    // Build a World with the minimum stores the system touches.
    private static (World world, EntityId player) CreateWorld()
    {
        var world = new World();
        world.Register<LocalPlayer>();
        world.Register<MoveInput>();

        var player = world.CreateEntity();
        world.Add<LocalPlayer>(player);
        world.Add(player, new MoveInput(0, 0));
        return (world, player);
    }

    private static MoveInput GetInput(World world, EntityId player)
    {
        foreach (var item in world.Query<LocalPlayer, MoveInput>())
            return item.Component2;
        throw new InvalidOperationException("player entity not found");
    }

    // -----------------------------------------------------------------------

    [Fact]
    public void NoInput_HasInputFalse_NeutralMoveInput()
    {
        var (world, player) = CreateWorld();
        var buffers = new Dictionary<NetEndpoint, BufferedInput>(); // empty

        new NetworkInputSystem(buffers).Run(world);

        var input = GetInput(world, player);
        Assert.Equal(0, input.X);
        Assert.Equal(0, input.Y);
    }

    [Fact]
    public void HasInputFalse_NeutralMoveInput()
    {
        var (world, player) = CreateWorld();
        var buffers = new Dictionary<NetEndpoint, BufferedInput>
        {
            [Peer] = new BufferedInput { HasInput = false, LatestState = new InputState { Buttons = (ushort)InputButtons.Right } },
        };

        new NetworkInputSystem(buffers).Run(world);

        var input = GetInput(world, player);
        Assert.Equal(0, input.X);
        Assert.Equal(0, input.Y);
    }

    [Fact]
    public void RightButton_SetsMoveInputX_Positive1()
    {
        var (world, player) = CreateWorld();
        var buffers = MakeBuffers(InputButtons.Right);

        new NetworkInputSystem(buffers).Run(world);

        Assert.Equal(1, GetInput(world, player).X);
        Assert.Equal(0, GetInput(world, player).Y);
    }

    [Fact]
    public void LeftButton_SetsMoveInputX_Negative1()
    {
        var (world, player) = CreateWorld();
        var buffers = MakeBuffers(InputButtons.Left);

        new NetworkInputSystem(buffers).Run(world);

        Assert.Equal(-1, GetInput(world, player).X);
        Assert.Equal(0, GetInput(world, player).Y);
    }

    [Fact]
    public void UpButton_SetsMoveInputY_Negative1()
    {
        var (world, player) = CreateWorld();
        var buffers = MakeBuffers(InputButtons.Up);

        new NetworkInputSystem(buffers).Run(world);

        Assert.Equal(0, GetInput(world, player).X);
        Assert.Equal(-1, GetInput(world, player).Y);
    }

    [Fact]
    public void DownButton_SetsMoveInputY_Positive1()
    {
        var (world, player) = CreateWorld();
        var buffers = MakeBuffers(InputButtons.Down);

        new NetworkInputSystem(buffers).Run(world);

        Assert.Equal(0, GetInput(world, player).X);
        Assert.Equal(1, GetInput(world, player).Y);
    }

    [Fact]
    public void DiagonalButtons_SetsBothAxes()
    {
        var (world, player) = CreateWorld();
        var buffers = MakeBuffers(InputButtons.Down | InputButtons.Right);

        new NetworkInputSystem(buffers).Run(world);

        var input = GetInput(world, player);
        Assert.Equal(1, input.X);
        Assert.Equal(1, input.Y);
    }

    [Fact]
    public void OpposingButtons_CancelOut()
    {
        var (world, player) = CreateWorld();
        var buffers = MakeBuffers(InputButtons.Left | InputButtons.Right);

        new NetworkInputSystem(buffers).Run(world);

        Assert.Equal(0, GetInput(world, player).X);
    }

    // -----------------------------------------------------------------------

    private static Dictionary<NetEndpoint, BufferedInput> MakeBuffers(InputButtons buttons)
        => new()
        {
            [Peer] = new BufferedInput
            {
                HasInput    = true,
                LatestSeq   = 1,
                LatestState = new InputState { Seq = 1, Buttons = (ushort)buttons },
            },
        };
}
