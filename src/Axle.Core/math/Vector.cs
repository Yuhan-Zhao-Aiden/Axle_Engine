namespace Axle.Core.AxleMath;

public record struct Vector2f
{
    public float X { get; init; }
    public float Y { get; init; }

    public Vector2f(float x, float y)
    {
        X = x;
        Y = y;
    }

    public static Vector2f operator+(Vector2f a, Vector2f b)
        => new Vector2f { X = a.X + b.X, Y = a.Y + b.Y };

    public static Vector2f operator*(Vector2f a, float c)
        => new Vector2f { X = a.X * c, Y = a.Y * c };

    public static Vector2f operator*(float c, Vector2f a)
        => a * c;
}