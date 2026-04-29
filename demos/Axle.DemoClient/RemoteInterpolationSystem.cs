using Axle.Core.AxleMath;
using Axle.Ecs;

namespace Axle.Client.System;

/// <summary>
/// Render-phase system that smoothly interpolates remote entity positions between
/// buffered server snapshots.
///
/// Each remote entity accumulates SnapshotSample entries (server-tick time, float x/y).
/// Every render frame the system estimates the current server time from a one-time clock
/// anchor, subtracts the interpolation delay to get a render-behind time, then lerps
/// between the two bracketing samples and writes the result to Transform.Position.
///
/// The system owns all buffer state — no new ECS components are required.
/// </summary>
public sealed class RemoteInterpolationSystem
{
    // Tuning constants
    private const float  InterpolationDelayMs = 150f;
    private const int    MaxBufferSize        = 30;
    private const double PruneWindowMs        = 500.0;

    // Per-entity sample ring. Keyed by full EntityId (index + version) to survive
    // entity index reuse after disconnection/reconnection.
    private readonly Dictionary<EntityId, List<SnapshotSample>> _buffers = new();

    // One-time clock anchor: set on the first snapshot received, then frozen.
    // The current server time is estimated as:
    //   _anchorServerTimeMs + (Environment.TickCount64 - _anchorClientTimeMs)
    // Freezing after initialisation prevents snapshot-arrival jitter from
    // corrupting the interpolation timeline.
    private double _anchorServerTimeMs;
    private long   _anchorClientTimeMs;
    private bool   _clockInitialized;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Call once per received snapshot. Sets the clock anchor on the very first
    /// call; subsequent calls are no-ops so jitter never disturbs the estimate.
    /// </summary>
    public void UpdateClock(double serverTimeMs, long clientReceiveMs)
    {
        if (_clockInitialized) return;
        _anchorServerTimeMs = serverTimeMs;
        _anchorClientTimeMs = clientReceiveMs;
        _clockInitialized   = true;
    }

    /// <summary>
    /// Push a new server-authoritative position sample for a remote entity.
    /// Safe to call before the clock is initialised; samples accumulate and
    /// will be consumed once the clock is set.
    /// </summary>
    public void PushSample(EntityId entityId, double serverTimeMs, float x, float y)
    {
        if (!_buffers.TryGetValue(entityId, out var buffer))
        {
            buffer = new List<SnapshotSample>(MaxBufferSize + 1);
            _buffers[entityId] = buffer;
        }

        buffer.Add(new SnapshotSample(serverTimeMs, x, y));

        // Drop the oldest sample when the ring is full.
        if (buffer.Count > MaxBufferSize)
            buffer.RemoveAt(0);
    }

    /// <summary>
    /// Remove the buffer for an entity that has been destroyed. Prevents
    /// unbounded growth when remote peers disconnect.
    /// </summary>
    public void RemoveEntity(EntityId entityId) => _buffers.Remove(entityId);

    /// <summary>
    /// Render-phase update. Lerps every live remote entity toward its
    /// interpolated position and writes to Transform.Position.
    /// Must be called each render frame after SyncTransformSystem.
    /// </summary>
    public void Run(World world)
    {
        if (!_clockInitialized) return;

        double currentServerTimeMs  = _anchorServerTimeMs + (Environment.TickCount64 - _anchorClientTimeMs);
        double renderServerTimeMs   = currentServerTimeMs - InterpolationDelayMs;

        foreach (var (entityId, buffer) in _buffers)
        {
            if (buffer.Count == 0) continue;
            if (!world.IsAlive(entityId)) continue;
            if (!world.Has<Transform>(entityId)) continue;

            ref var transform = ref world.Get<Transform>(entityId);

            // Need at least two samples to interpolate; snap to latest otherwise.
            if (buffer.Count < 2)
            {
                var latest = buffer[^1];
                transform.Position = new Vector2f(latest.X, latest.Y);
                continue;
            }

            // Find s0 = last sample at or before renderTime (index of it).
            int s0Index = -1;
            for (int i = buffer.Count - 1; i >= 0; i--)
            {
                if (buffer[i].ServerTimeMs <= renderServerTimeMs)
                {
                    s0Index = i;
                    break;
                }
            }

            float outX, outY;

            if (s0Index < 0)
            {
                // renderTime is before all buffered samples — snap to oldest.
                outX = buffer[0].X;
                outY = buffer[0].Y;
            }
            else if (s0Index == buffer.Count - 1)
            {
                // renderTime is after all buffered samples — hold last known (MVP).
                outX = buffer[^1].X;
                outY = buffer[^1].Y;
            }
            else
            {
                // Bracketed — lerp between s0 and s1.
                var s0 = buffer[s0Index];
                var s1 = buffer[s0Index + 1];

                double span = s1.ServerTimeMs - s0.ServerTimeMs;
                // Guard against identical timestamps (duplicate packets, packet reorder).
                float alpha = span > 0.0
                    ? (float)Math.Clamp((renderServerTimeMs - s0.ServerTimeMs) / span, 0.0, 1.0)
                    : 0f;

                outX = s0.X + (s1.X - s0.X) * alpha;
                outY = s0.Y + (s1.Y - s0.Y) * alpha;
            }

            transform.Position = new Vector2f(outX, outY);

            // Prune old samples, keeping at least 2 so the pair is always available.
            int removeCount = 0;
            for (int i = 0; i < buffer.Count - 2; i++)
            {
                if (buffer[i].ServerTimeMs < renderServerTimeMs - PruneWindowMs)
                    removeCount++;
                else
                    break;
            }
            if (removeCount > 0)
                buffer.RemoveRange(0, removeCount);
        }
    }
}
