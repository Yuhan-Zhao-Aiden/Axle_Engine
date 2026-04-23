using Axle.Ecs;

namespace Axle.Sim;

/// <summary>
/// Fixed-capacity ring buffer of the last 64 sent inputs.
/// Used by ReconciliationApplicator to replay inputs after a server correction.
/// Sequence numbers use ushort serial-number arithmetic (RFC 1982 half-window).
/// </summary>
public sealed class InputHistory
{
    private const int Capacity = 64;

    private readonly InputRecord[] _buf = new InputRecord[Capacity];
    private int _head;  // index of the next write slot
    private int _count; // number of valid (non-overwritten) entries

    /// <summary>Record an input that was just sent to the server.</summary>
    public void Record(ushort seq, MoveInput input)
    {
        _buf[_head] = new InputRecord { Seq = seq, Input = input };
        _head = (_head + 1) % Capacity;
        if (_count < Capacity) _count++;
    }

    /// <summary>Remove all entries whose sequence number is &lt;= ackSeq (ushort half-window).</summary>
    public void DropBefore(ushort ackSeq)
    {
        while (_count > 0)
        {
            int tail = (_head - _count + Capacity) % Capacity;
            if (!IsAfter(_buf[tail].Seq, ackSeq))
                _count--;
            else
                break;
        }
    }

    /// <summary>Returns all entries with seq &gt; ackSeq in ascending-seq order.</summary>
    public IEnumerable<InputRecord> GetPending(ushort ackSeq)
    {
        int tail = (_head - _count + Capacity) % Capacity;
        for (int i = 0; i < _count; i++)
        {
            int idx = (tail + i) % Capacity;
            if (IsAfter(_buf[idx].Seq, ackSeq))
                yield return _buf[idx];
        }
    }

    // True if 'a' is strictly after 'b' in ushort sequence space (half-window wrap).
    private static bool IsAfter(ushort a, ushort b)
        => unchecked((ushort)(a - b)) is > 0 and < 32768;
}
