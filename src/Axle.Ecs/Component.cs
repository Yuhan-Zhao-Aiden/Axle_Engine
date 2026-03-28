namespace Axle.Ecs;

using Axle.Core.AxleMath;
using Axle.Core.Utility;

public interface IComponent { }

public struct Transform : IComponent
{
    public Vector2f Position;
    public float Rotation;
    public Vector2f Scale;

    public Transform(Vector2f position, float rotation = 0f)
    {
        Position = position;
        Rotation = rotation;
        Scale = new Vector2f(1f, 1f);
    }

}

public struct RenderRect : IComponent
{
    public Vector2f Dimension;
    public Color4 Color;

    public RenderRect(Vector2f dimension, Color4 color)
    {
        Dimension = dimension;
        Color = color;
    }
}