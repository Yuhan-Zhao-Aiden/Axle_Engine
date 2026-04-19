namespace Axle.Sim.Test;

using Axle.Core.AxleMath;
using Axle.Ecs;

public class EntityOverlapSystemTest
{
    // Shared world factory — registers the component stores used by the overlap system.
    private static World CreateWorld()
    {
        var world = new World();
        world.Register<SimPosition>();
        world.Register<CollisionLayer>();
        world.Register<Collectible>();
        return world;
    }

    private static EntityOverlapSystem CreateSystem(World world)
    {
        var sys = new EntityOverlapSystem();
        sys.Register(CollisionLayers.Player, CollisionLayers.Collectible, new CollectibleHandler(world));
        return sys;
    }

    // -----------------------------------------------------------------------
    // Overlap destroys the collectible
    // -----------------------------------------------------------------------

    [Fact]
    public void Overlap_DestroyesCollectible()
    {
        var world = CreateWorld();

        // Player at (0, 0), half-extents 16
        EntityId player = world.CreateEntity();
        world.Add(player, new SimPosition(Fixed32.Zero, Fixed32.Zero));
        world.Add(player, new CollisionLayer(CollisionLayers.Player, Fixed32.FromInt(16), Fixed32.FromInt(16)));

        // Collectible at (0, 0) — fully overlapping
        EntityId coin = world.CreateEntity();
        world.Add(coin, new SimPosition(Fixed32.Zero, Fixed32.Zero));
        world.Add(coin, new CollisionLayer(CollisionLayers.Collectible, Fixed32.FromInt(16), Fixed32.FromInt(16)));
        world.Add<Collectible>(coin);

        CreateSystem(world).Run(world);

        Assert.False(world.IsAlive(coin));
    }

    // -----------------------------------------------------------------------
    // No overlap — collectible survives
    // -----------------------------------------------------------------------

    [Fact]
    public void NoOverlap_CollectibleSurvives()
    {
        var world = CreateWorld();

        EntityId player = world.CreateEntity();
        world.Add(player, new SimPosition(Fixed32.Zero, Fixed32.Zero));
        world.Add(player, new CollisionLayer(CollisionLayers.Player, Fixed32.FromInt(16), Fixed32.FromInt(16)));

        // Collectible far away — no overlap
        EntityId coin = world.CreateEntity();
        world.Add(coin, new SimPosition(Fixed32.FromInt(1000), Fixed32.FromInt(1000)));
        world.Add(coin, new CollisionLayer(CollisionLayers.Collectible, Fixed32.FromInt(16), Fixed32.FromInt(16)));
        world.Add<Collectible>(coin);

        CreateSystem(world).Run(world);

        Assert.True(world.IsAlive(coin));
    }

    // -----------------------------------------------------------------------
    // Multiple collectibles — only overlapping one is destroyed
    // -----------------------------------------------------------------------

    [Fact]
    public void MultipleCollectibles_OnlyOverlappingOneDestroyed()
    {
        var world = CreateWorld();

        EntityId player = world.CreateEntity();
        world.Add(player, new SimPosition(Fixed32.Zero, Fixed32.Zero));
        world.Add(player, new CollisionLayer(CollisionLayers.Player, Fixed32.FromInt(16), Fixed32.FromInt(16)));

        // coin1 overlaps player
        EntityId coin1 = world.CreateEntity();
        world.Add(coin1, new SimPosition(Fixed32.Zero, Fixed32.Zero));
        world.Add(coin1, new CollisionLayer(CollisionLayers.Collectible, Fixed32.FromInt(16), Fixed32.FromInt(16)));
        world.Add<Collectible>(coin1);

        // coin2 is far away
        EntityId coin2 = world.CreateEntity();
        world.Add(coin2, new SimPosition(Fixed32.FromInt(500), Fixed32.FromInt(500)));
        world.Add(coin2, new CollisionLayer(CollisionLayers.Collectible, Fixed32.FromInt(16), Fixed32.FromInt(16)));
        world.Add<Collectible>(coin2);

        CreateSystem(world).Run(world);

        Assert.False(world.IsAlive(coin1));
        Assert.True(world.IsAlive(coin2));
    }

    // -----------------------------------------------------------------------
    // Unregistered pair — no handler, no error, no destruction
    // -----------------------------------------------------------------------

    [Fact]
    public void UnregisteredPair_NoError_NoDestruction()
    {
        var world = CreateWorld();

        // Two collectibles overlapping each other — no Collectible-vs-Collectible handler.
        EntityId coin1 = world.CreateEntity();
        world.Add(coin1, new SimPosition(Fixed32.Zero, Fixed32.Zero));
        world.Add(coin1, new CollisionLayer(CollisionLayers.Collectible, Fixed32.FromInt(16), Fixed32.FromInt(16)));

        EntityId coin2 = world.CreateEntity();
        world.Add(coin2, new SimPosition(Fixed32.Zero, Fixed32.Zero));
        world.Add(coin2, new CollisionLayer(CollisionLayers.Collectible, Fixed32.FromInt(16), Fixed32.FromInt(16)));

        // Only Player-vs-Collectible registered (no player in world either — that's fine).
        var sys = new EntityOverlapSystem();
        sys.Register(CollisionLayers.Player, CollisionLayers.Collectible, new CollectibleHandler(world));
        sys.Run(world);

        Assert.True(world.IsAlive(coin1));
        Assert.True(world.IsAlive(coin2));
    }

    // -----------------------------------------------------------------------
    // Empty world — completes without exception
    // -----------------------------------------------------------------------

    [Fact]
    public void EmptyWorld_CompletesWithoutException()
    {
        var world = CreateWorld();
        CreateSystem(world).Run(world);
        // No assertion needed — must not throw.
    }
}
