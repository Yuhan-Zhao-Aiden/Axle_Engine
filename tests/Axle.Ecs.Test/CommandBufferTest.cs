namespace Axle.Ecs.Test;

using Axle.Ecs;
using Axle.Core.AxleMath;

// Test-local components
file struct Position : IComponent
{
    public Vector2f Value;
    public Position(float x, float y) => Value = new Vector2f(x, y);
}
file struct Velocity : IComponent { public Vector2f Value; }

public class CommandBufferTest
{
    // Registers all component types used across tests.
    private static World MakeWorld()
    {
        var w = new World();
        w.Register<Position>();
        w.Register<Velocity>();
        return w;
    }

    // Convenience: a real-entity target
    private static Target Real(EntityId e) => new Target { Entity = e };

    // Convenience: a temp-entity target
    private static Target Temp(int id) => new Target { IsTemp = true, Temp = new TempEntityId(id) };

    // ── §12 Test 1 — Deferred Add ────────────────────────────────────────────
    // Before playback the component is absent; after it is present with the
    // correct value.
    [Fact]
    public void DeferredAdd_ComponentAbsentBeforePlayback_PresentAfter()
    {
        var world = MakeWorld();
        var cb = new CommandBuffer();

        EntityId e1 = world.CreateEntity();
        cb.ForSystem(0).CreateWriter()
          .RecordAddComponent(Real(e1), new Position(3f, 7f));

        Assert.False(world.Has<Position>(e1));

        cb.Playback(world);

        Assert.True(world.Has<Position>(e1));
        Assert.Equal(new Vector2f(3f, 7f), world.Get<Position>(e1).Value);
    }

    // ── §12 Test 2 — Deferred Remove ────────────────────────────────────────
    // Component is still present while the buffer is recorded; gone after playback.
    [Fact]
    public void DeferredRemove_ComponentRemovedAfterPlayback()
    {
        var world = MakeWorld();
        var cb = new CommandBuffer();

        EntityId e1 = world.CreateEntity();
        world.Add<Position>(e1, new Position(1f, 2f));

        cb.ForSystem(0).CreateWriter()
          .RecordRemoveComponent<Position>(Real(e1));

        Assert.True(world.Has<Position>(e1)); // still present before playback

        cb.Playback(world);

        Assert.False(world.Has<Position>(e1));
    }

    // ── §12 Test 3 — Deferred Destroy ───────────────────────────────────────
    // Entity is alive while recording; dead after playback.
    [Fact]
    public void DeferredDestroy_EntityNotAliveAfterPlayback()
    {
        var world = MakeWorld();
        var cb = new CommandBuffer();

        EntityId e1 = world.CreateEntity();

        cb.ForSystem(0).CreateWriter()
          .RecordDestroyEntity(Real(e1));

        Assert.True(world.IsAlive(e1)); // still alive before playback

        cb.Playback(world);

        Assert.False(world.IsAlive(e1));
    }

    // ── §12 Test 4 — Temp Create + Add ──────────────────────────────────────
    // A temp handle created and decorated in the same flush becomes a real entity
    // with the expected component after playback.
    [Fact]
    public void TempCreate_WithAdd_ProducesRealEntityWithComponent()
    {
        var world = MakeWorld();
        var cb = new CommandBuffer();
        var writer = cb.ForSystem(0).CreateWriter();

        // RecordCreateEntity auto-assigns TempEntityId(0), (1), … per stream.
        writer.RecordCreateEntity();                              // TempEntityId(0)
        writer.RecordAddComponent(Temp(0), new Position(5f, 9f));

        int aliveBefore = world.AliveCount;

        cb.Playback(world);

        Assert.Equal(aliveBefore + 1, world.AliveCount);

        var view = world.Query<Position>();
        Assert.Equal(1, view.Count);
        Assert.Equal(new Vector2f(5f, 9f), view.Component(0).Value);
    }

    // ── §12 Test 5 — Conflict: Add then Remove ───────────────────────────────
    // Both commands target the same entity in the same flush.
    // Stable playback order applies both: Add runs first, Remove runs after →
    // component is absent after playback.
    [Fact]
    public void AddThenRemove_SameFlush_ComponentAbsentAfterPlayback()
    {
        var world = MakeWorld();
        var cb = new CommandBuffer();

        EntityId e1 = world.CreateEntity();
        var writer = cb.ForSystem(0).CreateWriter();

        writer.RecordAddComponent(Real(e1), new Position(1f, 1f));
        writer.RecordRemoveComponent<Position>(Real(e1));

        cb.Playback(world);

        Assert.False(world.Has<Position>(e1));
    }

    // ── §12 Test 6 — Destroy wins ────────────────────────────────────────────
    // Destroy and Add recorded for the same entity in the same flush.
    // The Add should be silently skipped and no exception thrown.
    [Fact]
    public void DestroyWins_AddIgnoredAndNoException()
    {
        var world = MakeWorld();
        var cb = new CommandBuffer();

        EntityId e1 = world.CreateEntity();
        var writer = cb.ForSystem(0).CreateWriter();

        writer.RecordDestroyEntity(Real(e1));
        writer.RecordAddComponent(Real(e1), new Position(1f, 2f));

        cb.Playback(world);

        Assert.False(world.IsAlive(e1));
    }

    // ── §14 Example Timeline — Multi-system smoke test ───────────────────────
    // System 0 (Spawner) creates a temp entity with Position + Velocity.
    // System 1 (Cleanup) destroys an existing entity.
    // After playback: old entity gone, new entity alive with both components.
    [Fact]
    public void MultiSystem_SpawnerAndCleanup_DeterministicResult()
    {
        var world = MakeWorld();
        var cb = new CommandBuffer();

        EntityId e2 = world.CreateEntity();
        world.Add<Position>(e2, new Position(99f, 99f));

        // System 0 – Spawner
        var spawner = cb.ForSystem(0).CreateWriter();
        spawner.RecordCreateEntity();                                             // T1 = TempEntityId(0)
        spawner.RecordAddComponent(Temp(0), new Position(1f, 2f));
        spawner.RecordAddComponent(Temp(0), new Velocity { Value = new Vector2f(3f, 4f) });

        // System 1 – Cleanup
        var cleanup = cb.ForSystem(1).CreateWriter();
        cleanup.RecordDestroyEntity(Real(e2));

        cb.Playback(world);

        Assert.False(world.IsAlive(e2));
        Assert.Equal(1, world.AliveCount);   // only the newly spawned entity

        var posView = world.Query<Position>();
        var velView = world.Query<Velocity>();
        Assert.Equal(1, posView.Count);
        Assert.Equal(1, velView.Count);
        Assert.Equal(new Vector2f(1f, 2f), posView.Component(0).Value);
        Assert.Equal(new Vector2f(3f, 4f), velView.Component(0).Value);
    }

    // ── Buffer is reset after Playback ───────────────────────────────────────
    [Fact]
    public void Playback_ResetsCount()
    {
        var world = MakeWorld();
        var cb = new CommandBuffer();

        EntityId e = world.CreateEntity();
        cb.ForSystem(0).CreateWriter()
          .RecordAddComponent(Real(e), new Position(1f, 1f));

        Assert.Equal(1, cb.Count);

        cb.Playback(world);

        Assert.Equal(0, cb.Count);
    }

    // ── Multiple temp entities in one flush ──────────────────────────────────
    [Fact]
    public void TempCreate_MultipleTemps_AllResolveCorrectly()
    {
        var world = MakeWorld();
        var cb = new CommandBuffer();
        var writer = cb.ForSystem(0).CreateWriter();

        // Create two temp entities and give each distinct positions
        writer.RecordCreateEntity();                                  // TempEntityId(0)
        writer.RecordCreateEntity();                                  // TempEntityId(1)
        writer.RecordAddComponent(Temp(0), new Position(10f, 0f));
        writer.RecordAddComponent(Temp(1), new Position(20f, 0f));

        cb.Playback(world);

        Assert.Equal(2, world.AliveCount);

        // Both positions must exist — collect X values as a set
        var xs = new HashSet<float>();
        var view = world.Query<Position>();
        for (int i = 0; i < view.Count; i++)
            xs.Add(view.Component(i).Value.X);

        Assert.Contains(10f, xs);
        Assert.Contains(20f, xs);
    }
}
