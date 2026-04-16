namespace Axle.Sim.Map;

/// <summary>
/// Integer tile-grid coordinate.  Origin is top-left; X increases right, Y increases down.
/// </summary>
public readonly struct MapPoint : IEquatable<MapPoint>
{
    public readonly int X;
    public readonly int Y;

    public MapPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    public bool Equals(MapPoint other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is MapPoint p && Equals(p);
    public override int GetHashCode() => HashCode.Combine(X, Y);
    public override string ToString() => $"({X}, {Y})";

    public static bool operator ==(MapPoint a, MapPoint b) => a.Equals(b);
    public static bool operator !=(MapPoint a, MapPoint b) => !a.Equals(b);
}
