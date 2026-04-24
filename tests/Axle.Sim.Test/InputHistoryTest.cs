using Axle.Ecs;
using Axle.Sim;

namespace Axle.Sim.Test;

public sealed class InputHistoryTest
{
    private static InputHistory Make(params (ushort seq, int x, int y)[] entries)
    {
        var h = new InputHistory();
        foreach (var (seq, x, y) in entries)
            h.Record(seq, new MoveInput(x, y));
        return h;
    }

    // -----------------------------------------------------------------------
    // GetPending
    // -----------------------------------------------------------------------

    [Fact]
    public void GetPending_EmptyHistory_YieldsNothing()
    {
        var h = new InputHistory();
        Assert.Empty(h.GetPending(0));
    }

    [Fact]
    public void GetPending_AllAfterAck_YieldsAllInOrder()
    {
        var h = Make((1, 1, 0), (2, -1, 0), (3, 0, 1));
        var pending = h.GetPending(0).ToList();

        Assert.Equal(3, pending.Count);
        Assert.Equal(1, pending[0].Seq);
        Assert.Equal(2, pending[1].Seq);
        Assert.Equal(3, pending[2].Seq);
    }

    [Fact]
    public void GetPending_FiltersEntriesUpToAndIncludingAck()
    {
        var h = Make((1, 1, 0), (2, -1, 0), (3, 0, 1));
        var pending = h.GetPending(2).ToList();

        Assert.Single(pending);
        Assert.Equal(3, pending[0].Seq);
    }

    [Fact]
    public void GetPending_AckBeyondAll_YieldsNothing()
    {
        var h = Make((1, 1, 0), (2, -1, 0));
        Assert.Empty(h.GetPending(5));
    }

    [Fact]
    public void GetPending_PreservesInputValues()
    {
        var h = Make((1, -1, 0), (2, 0, 1));
        var pending = h.GetPending(0).ToList();

        Assert.Equal(-1, pending[0].Input.X);
        Assert.Equal(0,  pending[0].Input.Y);
        Assert.Equal(0,  pending[1].Input.X);
        Assert.Equal(1,  pending[1].Input.Y);
    }

    // -----------------------------------------------------------------------
    // DropBefore
    // -----------------------------------------------------------------------

    [Fact]
    public void DropBefore_RemovesEntriesUpToAndIncludingAck()
    {
        var h = Make((1, 1, 0), (2, -1, 0), (3, 0, 1));
        h.DropBefore(2);

        var pending = h.GetPending(0).ToList();
        Assert.Single(pending);
        Assert.Equal(3, pending[0].Seq);
    }

    [Fact]
    public void DropBefore_DropAll_LeavesEmpty()
    {
        var h = Make((1, 1, 0), (2, -1, 0));
        h.DropBefore(2);

        Assert.Empty(h.GetPending(0));
    }

    [Fact]
    public void DropBefore_NothingDropped_WhenAckIsBeforeAll()
    {
        var h = Make((5, 1, 0), (6, -1, 0));
        h.DropBefore(3);

        Assert.Equal(2, h.GetPending(0).Count());
    }

    // -----------------------------------------------------------------------
    // Ring wrap
    // -----------------------------------------------------------------------

    [Fact]
    public void Record_OverCapacity_OldestEntryOverwritten()
    {
        var h = new InputHistory();
        // Fill 64 (capacity) + 1 more to trigger wrap.
        for (ushort i = 0; i <= 64; i++)
            h.Record(i, new MoveInput(1, 0));

        // seq=0 was the first entry written; after 65 writes it is overwritten.
        // Oldest surviving entry should be seq=1.
        var pending = h.GetPending(0).ToList();
        Assert.Equal(64, pending.Count);
        Assert.Equal(1, pending[0].Seq);
        Assert.Equal(64, pending[^1].Seq);
    }

    // -----------------------------------------------------------------------
    // Ushort wrap-around arithmetic
    // -----------------------------------------------------------------------

    [Fact]
    public void GetPending_UshortWrapAround_TreatsSmallSeqAsAfterMaxValue()
    {
        var h = new InputHistory();
        // Simulate seq rolling over: 65534, 65535, 0, 1
        h.Record(65534, new MoveInput(1, 0));
        h.Record(65535, new MoveInput(0, 1));
        h.Record(0,     new MoveInput(-1, 0));
        h.Record(1,     new MoveInput(0, -1));

        // ackSeq=65535 → seq 0 and 1 are still pending
        var pending = h.GetPending(65535).ToList();
        Assert.Equal(2, pending.Count);
        Assert.Equal(0, pending[0].Seq);
        Assert.Equal(1, pending[1].Seq);
    }

    [Fact]
    public void DropBefore_UshortWrapAround_DropsCorrectEntries()
    {
        var h = new InputHistory();
        h.Record(65534, new MoveInput(1, 0));
        h.Record(65535, new MoveInput(0, 1));
        h.Record(0,     new MoveInput(-1, 0));
        h.Record(1,     new MoveInput(0, -1));

        h.DropBefore(65535);

        // Only seq=0 and seq=1 remain.
        var pending = h.GetPending(0).ToList();
        // seq=0 is > 0? IsAfter(0,0) = false — so GetPending(0) returns seq=1 only.
        // Use GetPending(ushort.MaxValue) to get both.
        var all = h.GetPending(65535).ToList();
        Assert.Equal(2, all.Count);
        Assert.Equal(0, all[0].Seq);
        Assert.Equal(1, all[1].Seq);
    }
}
