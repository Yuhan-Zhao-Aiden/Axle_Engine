using System.Runtime.InteropServices;
using Axle.Ecs;
using Axle.Net;
using Axle.Sim;

namespace Axle.Server;

// Runs at sim rate (30 Hz) but only broadcasts every 3rd tick (~10 Hz).
// Builds a delta snapshot — only entities whose SimPosition changed since the
// last broadcast are included. On the very first broadcast every entity is dirty.
public sealed class SnapshotBroadcastSystem : ISystem
{
    private readonly NetServer _netServer;
    private readonly Dictionary<int, (int rawX, int rawY)> _lastSent = new();
    private ulong _tick;

    public SnapshotBroadcastSystem(NetServer netServer)
    {
        _netServer = netServer;
    }

    public void Run(World world)
    {
        _tick++;
        if (_tick % 3 != 0) return;

        // Build delta: collect entries for entities with changed SimPosition.
        var entries = new List<SnapshotEntry>();
        var view = world.Query<SimPosition>();

        for (int i = 0; i < view.Count; i++)
        {
            int entityIdx = view.Entity(i);
            ref SimPosition pos = ref view.Component(i);

            int rawX = pos.X.RawValue;
            int rawY = pos.Y.RawValue;

            if (_lastSent.TryGetValue(entityIdx, out var last)
                && last.rawX == rawX && last.rawY == rawY)
                continue; // unchanged — skip

            EntityId id = world.GetEntityId(entityIdx);
            entries.Add(new SnapshotEntry
            {
                EntityIndex = id.Index,
                EntityVersion = id.Version,
                Mask = ChangeMask.PositionX | ChangeMask.PositionY,
                PositionX = pos.X,
                PositionY = pos.Y,
            });
            _lastSent[entityIdx] = (rawX, rawY);
        }

        if (entries.Count == 0) return;

        // Send each peer its own snapshot with the ackSeq for their specific input stream.
        var span = CollectionsMarshal.AsSpan(entries);
        foreach (var peer in _netServer.ConnectedPeers)
        {
            ushort peerAck = 0;
            if (_netServer.InputBuffers.TryGetValue(peer, out var buf) && buf.HasInput)
                peerAck = buf.LatestSeq;
            _netServer.SendSnapshotTo(peer, _tick, peerAck, span);
        }
    }
}
