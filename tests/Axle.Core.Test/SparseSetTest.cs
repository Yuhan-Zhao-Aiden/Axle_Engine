namespace Axle.Core.Test;
using Axle.Core.Dsa;

public record struct TestComponent(int Value);

public class SparseSetTest
{
    // -------------------------------------------------------------------------
    // Add
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_NewEntry_ReturnsTrueAndIncrementsCount()
    {
        var set = new SparseSet<TestComponent>();

        bool inserted = set.Add(0, new TestComponent(2));

        Assert.True(inserted);
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Add_NewEntry_CanBeRetrieved()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(5, new TestComponent(42));

        Assert.True(set.Has(5));
        Assert.Equal(42, set[5].Value);
    }

    [Fact]
    public void Add_ExistingEntry_ReturnsFalseAndCountUnchanged()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(3, new TestComponent(1));

        bool inserted = set.Add(3, new TestComponent(99));

        Assert.False(inserted);
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Add_ExistingEntry_UpdatesValue()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(3, new TestComponent(1));
        set.Add(3, new TestComponent(99));

        Assert.Equal(99, set[3].Value);
    }

    [Fact]
    public void Add_MultipleEntries_CountTracksCorrectly()
    {
        var set = new SparseSet<TestComponent>();
        for (int i = 0; i < 10; i++)
            set.Add(i, new TestComponent(i));

        Assert.Equal(10, set.Count);
    }

    // -------------------------------------------------------------------------
    // Has
    // -------------------------------------------------------------------------

    [Fact]
    public void Has_AbsentEntry_ReturnsFalse()
    {
        var set = new SparseSet<TestComponent>();
        Assert.False(set.Has(7));
    }

    [Fact]
    public void Has_PresentEntry_ReturnsTrue()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(7, new TestComponent(0));
        Assert.True(set.Has(7));
    }

    // -------------------------------------------------------------------------
    // Remove
    // -------------------------------------------------------------------------

    [Fact]
    public void Remove_ExistingEntry_ReturnsTrueAndDecrementsCount()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(1, new TestComponent(10));

        bool removed = set.Remove(1);

        Assert.True(removed);
        Assert.Equal(0, set.Count);
        Assert.False(set.Has(1));
    }

    [Fact]
    public void Remove_AbsentEntry_ReturnsFalse()
    {
        var set = new SparseSet<TestComponent>();
        Assert.False(set.Remove(99));
    }

    [Fact]
    public void Remove_AbsentEntry_CountUnchanged()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(0, new TestComponent(1));
        set.Remove(99);
        Assert.Equal(1, set.Count);
    }

    [Fact]
    public void Remove_MiddleEntry_SwapPreservesRemainingValues()
    {
        // Dense layout before remove: [A=10, B=20, C=30]
        // Remove B (middle): C swaps into B's slot → [A=10, C=30]
        var set = new SparseSet<TestComponent>();
        set.Add(10, new TestComponent(10)); // entity 10 → dense[0]
        set.Add(20, new TestComponent(20)); // entity 20 → dense[1]
        set.Add(30, new TestComponent(30)); // entity 30 → dense[2]

        set.Remove(20);

        Assert.Equal(2, set.Count);
        Assert.True(set.Has(10));
        Assert.True(set.Has(30));
        Assert.False(set.Has(20));
        Assert.Equal(10, set[10].Value);
        Assert.Equal(30, set[30].Value);
    }

    [Fact]
    public void Remove_LastEntry_NoSwapNeeded()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(0, new TestComponent(1));
        set.Add(1, new TestComponent(2));

        set.Remove(1); // last element

        Assert.Equal(1, set.Count);
        Assert.True(set.Has(0));
        Assert.False(set.Has(1));
    }

    [Fact]
    public void Remove_AllEntries_CountIsZero()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(0, new TestComponent(1));
        set.Add(1, new TestComponent(2));
        set.Add(2, new TestComponent(3));

        set.Remove(0);
        set.Remove(1);
        set.Remove(2);

        Assert.Equal(0, set.Count);
    }

    [Fact]
    public void Remove_ThenReAdd_WorksCorrectly()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(5, new TestComponent(1));
        set.Remove(5);
        set.Add(5, new TestComponent(99));

        Assert.Equal(1, set.Count);
        Assert.True(set.Has(5));
        Assert.Equal(99, set[5].Value);
    }

    // -------------------------------------------------------------------------
    // Ref mutation through indexer
    // -------------------------------------------------------------------------

    [Fact]
    public void IndexerRef_MutationIsReflectedInSet()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(2, new TestComponent(0));

        set[2] = new TestComponent(55);

        Assert.Equal(55, set[2].Value);
    }

    // -------------------------------------------------------------------------
    // Ref stability — core guarantee of the paged dense array
    // -------------------------------------------------------------------------

    [Fact]
    public void RefStability_RefRemainsValidAfterMoreAdds()
    {
        // Take a ref to an early component, then fill past a page boundary
        // (DenseArray page size = 64). The ref must still read the correct value.
        var set = new SparseSet<TestComponent>();
        set.Add(0, new TestComponent(7));

        ref TestComponent held = ref set.GetUnsafe(0);

        // Add enough entries to cross two dense pages.
        for (int i = 1; i <= 128; i++)
            set.Add(i, new TestComponent(i));

        Assert.Equal(7, held.Value);
    }

    [Fact]
    public void RefStability_MutationThroughHeldRefIsVisible()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(0, new TestComponent(0));

        ref TestComponent held = ref set.GetUnsafe(0);

        for (int i = 1; i <= 128; i++)
            set.Add(i, new TestComponent(i));

        held = new TestComponent(999);

        Assert.Equal(999, set[0].Value);
    }

    // -------------------------------------------------------------------------
    // Large / cross-page indices (sparse page size = 128)
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_LargeEntityId_CrossesSparsePageBoundary()
    {
        var set = new SparseSet<TestComponent>();
        // Entity IDs that span multiple sparse pages (each page covers 128 IDs).
        int[] ids = [0, 127, 128, 255, 256, 1000, 99999];
        foreach (int id in ids)
            set.Add(id, new TestComponent(id));

        foreach (int id in ids)
        {
            Assert.True(set.Has(id), $"Has({id}) should be true");
            Assert.Equal(id, set[id].Value);
        }
        Assert.Equal(ids.Length, set.Count);
    }

    [Fact]
    public void Remove_LargeEntityId_WorksCorrectly()
    {
        var set = new SparseSet<TestComponent>();
        set.Add(99999, new TestComponent(42));

        set.Remove(99999);

        Assert.False(set.Has(99999));
        Assert.Equal(0, set.Count);
    }

    // -------------------------------------------------------------------------
    // Dense page boundary (dense page size = 64)
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_CrossesDensePageBoundary_AllValuesRetrievable()
    {
        var set = new SparseSet<TestComponent>();
        const int count = 200; // well past the 64-element first page

        for (int i = 0; i < count; i++)
            set.Add(i, new TestComponent(i * 2));

        Assert.Equal(count, set.Count);
        for (int i = 0; i < count; i++)
            Assert.Equal(i * 2, set[i].Value);
    }

    [Fact]
    public void Remove_AfterCrossingDensePageBoundary_SwapCorrect()
    {
        var set = new SparseSet<TestComponent>();
        const int count = 70;

        for (int i = 0; i < count; i++)
            set.Add(i, new TestComponent(i));

        // Remove entity 5, which should swap entity 69 (last) into its slot.
        set.Remove(5);

        Assert.Equal(count - 1, set.Count);
        Assert.False(set.Has(5));
        Assert.True(set.Has(69));
        Assert.Equal(69, set[69].Value);
    }

    // -------------------------------------------------------------------------
    // Guard: negative index
    // -------------------------------------------------------------------------

    [Fact]
    public void Add_NegativeIndex_Throws()
    {
        var set = new SparseSet<TestComponent>();
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Add(-1, new TestComponent(0)));
    }

    [Fact]
    public void Has_NegativeIndex_Throws()
    {
        var set = new SparseSet<TestComponent>();
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Has(-1));
    }

    [Fact]
    public void Remove_NegativeIndex_Throws()
    {
        var set = new SparseSet<TestComponent>();
        Assert.Throws<ArgumentOutOfRangeException>(() => set.Remove(-1));
    }
}
