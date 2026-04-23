namespace Axle.Net;

public readonly struct SnapshotData
{
    public ulong TickId { get; init; }
    public ushort AckSeq { get; init; }
    public SnapshotEntry[] Entries { get; init; }
    public long ReceivedMs { get; init; }
}
