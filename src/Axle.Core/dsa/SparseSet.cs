namespace Axle.Core.Dsa;

public class SparseSet<T> where T : struct
{
    private SparseArray _sparse;
    private DenseArray _dense;

    public int Count => _dense.Count;

    public ref T this[int i] => ref GetUnsafe(i);

    public SparseSet()
    {
        _sparse = new();
        _dense = new();
    }

    public bool Has(int index) => _sparse.TryGet(index, out _);

    /// <summary>Unsafe – call Has() first to confirm the index exists.</summary>
    public ref T GetUnsafe(int index)
    {
        _sparse.TryGet(index, out int denseIndex);
        return ref _dense[denseIndex];
    }

    /// <summary>
    /// Returns true if the entry was inserted, false if it was replaced.
    /// </summary>
    public bool Add(int index, T component)
    {
        if (_sparse.TryGet(index, out int existingDense))
        {
            // Single lookup – no double-search, assign through the stable ref.
            _dense[existingDense] = component;
            return false;
        }
        int denseIndex = _dense.Add(index, component);
        _sparse.Add(index, denseIndex);
        return true;
    }

    public bool Remove(int index)
    {
        if (!_sparse.TryGet(index, out int removedDense)) return false;

        int moved = _dense.Remove(removedDense, out bool swapped);
        if (swapped) _sparse[moved] = removedDense;
        _sparse.Remove(index);
        return true;
    }

    // Paged dense array
    // Pages are never reallocated or moved, so a ref T into any page is permanently
    // stable regardless of how many elements are added later.
    private sealed class DenseArray
    {
        private const int PageShift = 6;
        private const int PageSize = 1 << PageShift; // 64 elements per page
        private const int PageMask = PageSize - 1;   // 63


        private int[][] _entityIds = new int[4][];
        private T[][] _data = new T[4][];

        public int Count { get; private set; }

        // ref T return is stable: the underlying page is never moved.
        public ref T this[int denseIndex]
            => ref _data[denseIndex >> PageShift][denseIndex & PageMask];

        private void EnsurePage(int pageIdx)
        {
            if (pageIdx >= _entityIds.Length)
            {
                int newLen = Math.Max(pageIdx + 1, _entityIds.Length * 2);
                Array.Resize(ref _entityIds, newLen);
                Array.Resize(ref _data, newLen);
            }
            if (_entityIds[pageIdx] != null) return;
            _entityIds[pageIdx] = new int[PageSize];
            _data[pageIdx] = new T[PageSize];
        }

        public int Add(int entityId, T value)
        {
            int denseIndex = Count;
            int pageIdx = denseIndex >> PageShift;
            int offset = denseIndex & PageMask;

            EnsurePage(pageIdx);

            _entityIds[pageIdx][offset] = entityId;
            _data[pageIdx][offset] = value;
            Count++;
            return denseIndex;
        }

        // Swap-remove. Returns the entity ID of the element moved into the gap.
        public int Remove(int denseIndex, out bool didSwap)
        {
            if ((uint)denseIndex >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(denseIndex));

            int last = Count - 1;
            int lastPage = last >> PageShift;
            int lastOffset = last & PageMask;
            int movedId = _entityIds[lastPage][lastOffset];

            didSwap = denseIndex != last;
            if (didSwap)
            {
                int removePage = denseIndex >> PageShift;
                int removeOffset = denseIndex & PageMask;
                _entityIds[removePage][removeOffset] = movedId;
                _data[removePage][removeOffset] = _data[lastPage][lastOffset];
            }

            Count--;
            return movedId;
        }
    }

    // Paged sparse array  (entity ID → dense index, -1 = absent)
    private sealed class SparseArray
    {
        private const int PageShift = 7;
        private const int PageSize  = 1 << PageShift; // 128 slots per page
        private const int PageMask  = PageSize - 1;   // 127

        private int[][] _pages = new int[4][];

        public int this[int index]
        {
            get { TryGet(index, out int v); return v; }
            set => Set(index, value);
        }

        private static (int page, int slot) Decompose(int index)
        {
            if (index < 0)
                throw new ArgumentOutOfRangeException(nameof(index), "Entity index must be non-negative.");
            return (index >> PageShift, index & PageMask);
        }

        public void Add(int index, int value)
        {
            var (page, slot) = Decompose(index);

            if (page >= _pages.Length)
            {
                int newLen = Math.Max(page + 1, _pages.Length * 2);
                Array.Resize(ref _pages, newLen);
            }

            if (_pages[page] == null)
            {
                _pages[page] = new int[PageSize];
                Array.Fill(_pages[page], -1);
            }

            _pages[page][slot] = value;
        }

        // Updates an existing entry; silently no-ops if the entry is absent.
        public void Set(int index, int value)
        {
            var (page, slot) = Decompose(index);
            if (page >= _pages.Length || _pages[page] == null || _pages[page][slot] == -1)
                return;
            _pages[page][slot] = value;
        }

        public bool TryGet(int index, out int value)
        {
            var (page, slot) = Decompose(index);

            if (page >= _pages.Length || _pages[page] == null)
            {
                value = -1;
                return false;
            }

            value = _pages[page][slot];
            return value != -1;
        }

        public void Remove(int index)
        {
            var (page, slot) = Decompose(index);
            if (page >= _pages.Length || _pages[page] == null) return;
            _pages[page][slot] = -1;
        }
    }
}