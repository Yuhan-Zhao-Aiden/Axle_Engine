namespace Axle.Sim.Test;

using Axle.Core.AxleMath;
using Axle.Ecs;

public class MovementSystemTest
{
    // ---- Helpers ----

    private static World SetupMovingWorld(Fixed32 velX, Fixed32 velY, out EntityId entity)
    {
        var world = new World();
        world.Register<SimPosition>();
        world.Register<Velocity>();

        entity = world.CreateEntity();
        world.Add(entity, new SimPosition(Fixed32.Zero, Fixed32.Zero));
        world.Add(entity, new Velocity(velX, velY));
        return world;
    }

    // ---- MovementSystem ----

    [Fact]
    public void MovementSystem_AdvancesPosition_ByVelocityTimesDt()
    {
        // Q16.16 cannot represent 1/30 exactly, so the expected value is the
        // deterministic accumulation of (velocity * truncated_dt) over TickRate steps —
        // not a rounded integer. This test asserts the exact fixed-point result.
        var vel = Fixed32.FromInt(3);
        var world = SetupMovingWorld(vel, Fixed32.Zero, out var e);
        var system = new MovementSystem(SimTime.Dt);

        for (int i = 0; i < SimTime.TickRate; i++)
            system.Run(world);

        Fixed32 step = vel * SimTime.Dt;
        Fixed32 expected = Fixed32.Zero;
        for (int i = 0; i < SimTime.TickRate; i++)
            expected += step;

        var pos = world.Get<SimPosition>(e);
        Assert.Equal(expected, pos.X);
        Assert.Equal(Fixed32.Zero, pos.Y);
    }

    [Fact]
    public void MovementSystem_ZeroVelocity_DoesNotMove()
    {
        var world = SetupMovingWorld(Fixed32.Zero, Fixed32.Zero, out var e);
        world.Get<SimPosition>(e).X = Fixed32.FromInt(5);
        world.Get<SimPosition>(e).Y = Fixed32.FromInt(2);

        var system = new MovementSystem(SimTime.Dt);
        for (int i = 0; i < 100; i++)
            system.Run(world);

        var pos = world.Get<SimPosition>(e);
        Assert.Equal(Fixed32.FromInt(5), pos.X);
        Assert.Equal(Fixed32.FromInt(2), pos.Y);
    }

    [Fact]
    public void MovementSystem_NegativeVelocity_DecreasesPosition()
    {
        var vel = Fixed32.FromInt(-2);
        var world = SetupMovingWorld(vel, Fixed32.Zero, out var e);
        var system = new MovementSystem(SimTime.Dt);

        for (int i = 0; i < SimTime.TickRate; i++)
            system.Run(world);

        Fixed32 step = vel * SimTime.Dt;
        Fixed32 expected = Fixed32.Zero;
        for (int i = 0; i < SimTime.TickRate; i++)
            expected += step;

        var pos = world.Get<SimPosition>(e);
        Assert.Equal(expected, pos.X);
        Assert.True(pos.X < Fixed32.Zero, "Position should be negative after moving left");
    }

    [Fact]
    public void MovementSystem_Determinism_SameInputsProduceIdenticalRawValues()
    {
        // Run the same movement scenario twice from fresh worlds.
        // Final raw values must be bit-identical.
        var worldA = SetupMovingWorld(Fixed32.FromDouble(1.5), Fixed32.Zero, out var eA);
        var worldB = SetupMovingWorld(Fixed32.FromDouble(1.5), Fixed32.Zero, out var eB);

        var sysA = new MovementSystem(SimTime.Dt);
        var sysB = new MovementSystem(SimTime.Dt);

        for (int i = 0; i < 60; i++) sysA.Run(worldA);
        for (int i = 0; i < 60; i++) sysB.Run(worldB);

        Assert.Equal(
            worldA.Get<SimPosition>(eA).X.RawValue,
            worldB.Get<SimPosition>(eB).X.RawValue);
    }

    // ---- PlayerVelocitySystem ----

    private static World SetupPlayerWorld(int inputX, int inputY, out EntityId entity)
    {
        var world = new World();
        world.Register<LocalPlayer>();
        world.Register<MoveInput>();
        world.Register<Velocity>();

        entity = world.CreateEntity();
        world.Add<LocalPlayer>(entity);
        world.Add(entity, new MoveInput(inputX, inputY));
        world.Add(entity, new Velocity(Fixed32.Zero, Fixed32.Zero));
        return world;
    }

    [Fact]
    public void PlayerVelocitySystem_RightInput_SetsPositiveXVelocity()
    {
        var world = SetupPlayerWorld(1, 0, out var e);
        new PlayerVelocitySystem().Run(world);

        var vel = world.Get<Velocity>(e);
        Assert.Equal(PlayerVelocitySystem.MoveSpeed, vel.X);
        Assert.Equal(Fixed32.Zero, vel.Y);
    }

    [Fact]
    public void PlayerVelocitySystem_ZeroInput_ProducesZeroVelocity()
    {
        // Equivalent to opposite keys cancelling — MoveInput is already resolved to 0
        var world = SetupPlayerWorld(0, 0, out var e);

        // Give it a pre-existing velocity to ensure it is overwritten
        world.Get<Velocity>(e).X = Fixed32.FromInt(99);
        new PlayerVelocitySystem().Run(world);

        var vel = world.Get<Velocity>(e);
        Assert.Equal(Fixed32.Zero, vel.X);
        Assert.Equal(Fixed32.Zero, vel.Y);
    }

    [Fact]
    public void PlayerVelocitySystem_NegativeInput_SetsNegativeVelocity()
    {
        var world = SetupPlayerWorld(-1, -1, out var e);
        new PlayerVelocitySystem().Run(world);

        var vel = world.Get<Velocity>(e);
        Assert.Equal(-PlayerVelocitySystem.MoveSpeed, vel.X);
        Assert.Equal(-PlayerVelocitySystem.MoveSpeed, vel.Y);
    }

    [Fact]
    public void PlayerVelocitySystem_EntityWithoutLocalPlayer_IsNotAffected()
    {
        var world = SetupPlayerWorld(1, 0, out var _);

        // Spawn a second entity with MoveInput + Velocity but no LocalPlayer tag
        world.Register<LocalPlayer>(); // already registered, idempotent
        var other = world.CreateEntity();
        world.Add(other, new MoveInput(1, 0));
        world.Add(other, new Velocity(Fixed32.Zero, Fixed32.Zero));

        new PlayerVelocitySystem().Run(world);

        // The non-tagged entity's velocity must remain zero
        var vel = world.Get<Velocity>(other);
        Assert.Equal(Fixed32.Zero, vel.X);
    }
}
