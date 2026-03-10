namespace Axle.Ecs;

using Axle.Core.AxleMath;

public interface IComponent { }

public struct Position : IComponent
{
    public Vector2f Value { get; set; }

    public Position(Vector2f value) => Value = value;
    public Position(float x, float y) => Value = new Vector2f(x, y);
}