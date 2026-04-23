using Axle.Core.AxleMath;

namespace Axle.Net;

public readonly struct SnapshotEntry
{
    public int EntityIndex { get; init; }
    public int EntityVersion { get; init; }
    public ChangeMask Mask { get; init; }
    public Fixed32 PositionX { get; init; }
    public Fixed32 PositionY { get; init; }
}
