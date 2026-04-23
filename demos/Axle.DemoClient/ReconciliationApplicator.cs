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

    public ReconciliationApplicator(
        InputHistory history,
        World world,
        EntityId player,
        PlayerVelocitySystem velocitySystem,
        TileCollisionMovementSystem movementSystem)
    {
        _history = history;
        _world = world;
        _player = player;
        _velocitySystem = velocitySystem;
        _movementSystem = movementSystem;
    }

    public void Apply(SnapshotData snap)
    {
        // 1. Find the player entry and warp position to server authority.
        foreach (var entry in snap.Entries)
        {
            if (entry.EntityIndex   != _player.Index ||
                entry.EntityVersion != _player.Version)
                continue;

            ref var pos = ref _world.Get<SimPosition>(_player);
            if ((entry.Mask & ChangeMask.PositionX) != 0) pos.X = entry.PositionX;
            if ((entry.Mask & ChangeMask.PositionY) != 0) pos.Y = entry.PositionY;
            break;
        }

        // 2. Drop inputs the server has already consumed.
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
