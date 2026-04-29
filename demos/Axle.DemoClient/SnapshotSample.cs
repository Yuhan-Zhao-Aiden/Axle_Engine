namespace Axle.Client;

/// <summary>
/// A single timestamped position sample from the server, stored per remote entity
/// in the snapshot buffer used by RemoteInterpolationSystem.
/// </summary>
public readonly struct SnapshotSample
{
    /// <summary>Server-derived time in milliseconds: TickId * (1000.0 / 30.0).</summary>
    public readonly double ServerTimeMs;
    public readonly float X;
    public readonly float Y;

    public SnapshotSample(double serverTimeMs, float x, float y)
    {
        ServerTimeMs = serverTimeMs;
        X = x;
        Y = y;
    }
}
