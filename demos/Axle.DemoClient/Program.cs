// See https://aka.ms/new-console-template for more information
using Axle.Ecs;
public struct TestComp : IComponent
{
    public int Value;
}
public class Program
{
    public static void Main(string[] args)
    {
        // window.Run();

        World world = new();
        Console.WriteLine(world.AliveCount);

        EntityId e1 = world.CreateEntity();
        Console.WriteLine($"Index: {e1.Index}, Version: {e1.Version}, Alive : {world.IsAlive(e1)}");

        world.Register<TestComp>();
        world.Add<TestComp>(e1);
    }
}