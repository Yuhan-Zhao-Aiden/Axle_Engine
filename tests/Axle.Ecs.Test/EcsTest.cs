namespace Axle.Ecs.Test;

using Axle.Ecs;
using Axle.Core.AxleMath;

public class EcsTest 
{
    [Fact]
    public void RemoveEntityTest()
    {
        World world = new();
        EntityId id = world.CreateEntity();
        Assert.Equal(0, id.Index);
        Assert.Equal(0, id.Version);

        world.DestoryEntity(id);
        Assert.False(world.IsAlive(id));

        EntityId newId = world.CreateEntity();
        Assert.Equal(0, newId.Index);
        Assert.Equal(1, newId.Version);
        Assert.True(world.IsAlive(newId));
    }

    [Fact]
    public void DenseViewTest()
    {
        
    }  

    [Fact]
    public void EntityCreationAndDenseViewSmokeTest()
    {
        World world = new();
        world.Register<Position>();
        List<EntityId> ids = new();

        for (int i = 0; i < 100; i++)
        {
            EntityId newEntity = world.CreateEntity();
            ids.Add(newEntity);

            ref var pos = ref world.Add<Position>(newEntity);
            pos.Value = new Vector2f(1, 2);
        }

        Assert.Equal(100, world.AliveCount);
        Assert.Equal(100, world.Store<Position>().Count);

        foreach (var e in ids)
        {
            Assert.True(world.Has<Position>(e));
            ref var p = ref world.Get<Position>(e);
            Assert.Equal(1, p.Value.X);
        }

        // DenseView
        DenseView<Position> view = world.Query<Position>();
        Assert.Equal(100, view.Count);
    }
}
