// See https://aka.ms/new-console-template for more information
using Axle.Graphics;
using Axle.Core.Dsa;
using Axle.Core;
public struct TestComp 
{
    public int Value;
}
public class Program
{
    public static void Main(string[] args)
    {
        SparseSet<TestComp> set = new();
        set.Add(2, new TestComp { Value = 2 });

        Console.WriteLine(set.Count);
        Console.WriteLine(set.Has(0));

        Console.WriteLine(set[2].Value);

        set[2].Value = 20;
        Console.WriteLine(set[2].Value);

        set.Add(2, new TestComp { Value = 100 });
        Console.WriteLine(set[2].Value);


        // var window = new WindowHost(title: "Axle Demo Client");
        // window.Run();
    }
}