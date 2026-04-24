using Axle.Core.AxleMath;
using Axle.Ecs;
using Axle.Net;
using Axle.Sim;

namespace Axle.Client;

// Applies a server snapshot to the local world and replays unacknowledged inputs
// (client-side prediction reconciliation).
//   1. Warp player to the server-authoritative SimPosition.
//   2. Drop every input the server has already consumed (seq <= ackSeq).
//   3. Re-simulate each remaining unacked input so the predicted position
//      stays consistent with the server.
internal sealed class ReconciliationApplicator
{
    private readonly InputHistory _history;
    private readonly World _world;
    private readonly EntityId _player;
    private readonly PlayerVelocitySystem _velocitySystem;
    private readonly TileCollisionMovementSystem _movementSystem;
    private readonly EntityId? _ghost;

    // Server-assigned entity that corresponds to our local player. -1 = not yet known.
    private int _serverEntityIndex   = -1;
    private int _serverEntityVersion = -1;

    public Fixed32 LastErrorX  { get; private set; }
    public Fixed32 LastErrorY  { get; private set; }
    public ushort  LastAckedSeq { get; private set; }

    public ReconciliationApplicator(
        InputHistory history,
        World world,
        EntityId player,
        PlayerVelocitySystem velocitySystem,
        TileCollisionMovementSystem movementSystem,
        EntityId? ghost = null)
    {
        _history = history;
        _world = world;
        _player = player;
        _velocitySystem = velocitySystem;
        _movementSystem = movementSystem;
        _ghost = ghost;
    }

    public void SetServerEntity(int serverEntityIndex, int serverEntityVersion)
    {
        _serverEntityIndex   = serverEntityIndex;
        _serverEntityVersion = serverEntityVersion;
    }

    public void Apply(SnapshotData snap)
    {
        // Guard: skip reconciliation until the server entity assignment is known.
        if (_serverEntityIndex < 0) return;

        // 1. Find the entry matching our server-assigned entity and warp position.
        foreach (var entry in snap.Entries)
        {
            if (entry.EntityIndex   != _serverEntityIndex ||
                entry.EntityVersion != _serverEntityVersion)
                continue;

            ref var pos = ref _world.Get<SimPosition>(_player);

            // Capture predicted position before the warp to measure prediction error.
            Fixed32 predictedX = pos.X;
            Fixed32 predictedY = pos.Y;

            if ((entry.Mask & ChangeMask.PositionX) != 0)
            {
                LastErrorX = predictedX - entry.PositionX;
                pos.X = entry.PositionX;
            }
            if ((entry.Mask & ChangeMask.PositionY) != 0)
            {
                LastErrorY = predictedY - entry.PositionY;
                pos.Y = entry.PositionY;
            }

            // Move the ghost entity to the server-authoritative position for visual debug.
            if (_ghost.HasValue)
            {
                ref var gt = ref _world.Get<Transform>(_ghost.Value);
                float gx = (entry.Mask & ChangeMask.PositionX) != 0 ? entry.PositionX.ToFloat() : gt.Position.X;
                float gy = (entry.Mask & ChangeMask.PositionY) != 0 ? entry.PositionY.ToFloat() : gt.Position.Y;
                gt.Position = new Vector2f(gx, gy);
            }

            break;
        }

        // 2. Drop inputs the server has already consumed.
        LastAckedSeq = snap.AckSeq;
        _history.DropBefore(snap.AckSeq);

        // 3. Re-simulate each unacknowledged input on top of the corrected position.
        foreach (var record in _history.GetPending(snap.AckSeq))
        {
            ref var input = ref _world.Get<MoveInput>(_player);
            input = record.Input;
            _velocitySystem.Run(_world);
            _movementSystem.Run(_world);
        }
    }
}
