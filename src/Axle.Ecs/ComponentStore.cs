namespace Axle.Ecs;

using Axle.Core.Dsa;


public interface IComponentStore
{
    Type ComponentType { get; }
    int Count { get; }
    bool RemoveByEntityIndex(int entityIndex);
    bool HasEntityIndex(int entityIndex);
    void EnsureEntityCapacity(int entityCapacity);
}

public readonly ref struct DenseView<T> where T : struct, IComponent
{
    private readonly ComponentStore<T> _store;
    public int Count => _store.Count;
    internal DenseView(ComponentStore<T> store) => _store = store;

    public int Entity(int denseIndex) => _store.EntityAtDense(denseIndex);
    public ref T Component(int denseIndex) => ref _store.ComponentAtDense(denseIndex);

    /// <summary>
    /// Element-by-element iteration: <c>foreach (ref T comp in view)</c>.
    /// Prefer <see cref="Pages"/> when processing large sets for better vectorization.
    /// </summary>
    public SparseSet<T>.Enumerator GetEnumerator() => _store.GetEnumerator();

    /// <summary>
    /// Page-by-page iteration. Each page yields a contiguous <see cref="Span{T}"/>
    /// that the JIT can vectorize, plus a matching <see cref="ReadOnlySpan{int}"/>
    /// of entity IDs.
    /// <code>
    /// foreach (var page in view.Pages)
    /// {
    ///     Span&lt;T&gt; comps = page.CurrentData;
    ///     ReadOnlySpan&lt;int&gt; entities = page.CurrentEntities;
    ///     for (int i = 0; i &lt; comps.Length; i++) { ... }
    /// }
    /// </code>
    /// </summary>
    public SparseSet<T>.PageEnumerator Pages => _store.GetPageEnumerator();
}

/// <summary>
/// The iterated value yielded by <see cref="JoinView{T1,T2}"/>.
/// Holds <c>ref</c> fields pointing directly into the stores — no copies.
/// </summary>
public ref struct JoinItem<T1, T2>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
{
    public ref T1 Component1;
    public ref T2 Component2;
    public int Entity;
}

public readonly ref struct JoinView<T1, T2>
    where T1 : struct, IComponent
    where T2 : struct, IComponent
{
    private readonly ComponentStore<T1> _s1;
    private readonly ComponentStore<T2> _s2;
    private readonly bool _drivenByT1;

    internal JoinView(ComponentStore<T1> s1, ComponentStore<T2> s2)
    {
        _s1 = s1;
        _s2 = s2;
        _drivenByT1 = s1.Count <= s2.Count;
    }

    public Enumerator GetEnumerator() => new(_s1, _s2, _drivenByT1);

    public ref struct Enumerator
    {
        private readonly ComponentStore<T1> _s1;
        private readonly ComponentStore<T2> _s2;
        private readonly bool _drivenByT1;
        private readonly int _count;
        private int _denseIndex;
        private int _currentEntity;

        internal Enumerator(ComponentStore<T1> s1, ComponentStore<T2> s2, bool drivenByT1)
        {
            _s1 = s1;
            _s2 = s2;
            _drivenByT1 = drivenByT1;
            _count = drivenByT1 ? s1.Count : s2.Count;
            _denseIndex = -1;
            _currentEntity = -1;
        }

        public bool MoveNext()
        {
            while (++_denseIndex < _count)
            {
                _currentEntity = _drivenByT1
                    ? _s1.EntityAtDense(_denseIndex)
                    : _s2.EntityAtDense(_denseIndex);

                if (_drivenByT1 ? _s2.HasEntityIndex(_currentEntity)
                                : _s1.HasEntityIndex(_currentEntity))
                    return true;
            }
            return false;
        }

        /// <summary>Entity index (sparse key) of the current matched entity.</summary>
        public int CurrentEntity => _currentEntity;

        /// <summary>
        /// The current matched pair. Both component refs point directly into the stores.
        /// </summary>
        public JoinItem<T1, T2> Current
        {
            get
            {
                JoinItem<T1, T2> item = new() { Entity = _currentEntity };
                if (_drivenByT1)
                {
                    item.Component1 = ref _s1.ComponentAtDense(_denseIndex);
                    item.Component2 = ref _s2.GetByEntityIndex(_currentEntity);
                }
                else
                {
                    item.Component1 = ref _s1.GetByEntityIndex(_currentEntity);
                    item.Component2 = ref _s2.ComponentAtDense(_denseIndex);
                }
                return item;
            }
        }

        /// <summary>Ref to the T1 component — mutate directly, no copy.</summary>
        public ref T1 Component1 => ref (_drivenByT1
            ? ref _s1.ComponentAtDense(_denseIndex)
            : ref _s1.GetByEntityIndex(_currentEntity));

        /// <summary>Ref to the T2 component — mutate directly, no copy.</summary>
        public ref T2 Component2 => ref (_drivenByT1
            ? ref _s2.GetByEntityIndex(_currentEntity)
            : ref _s2.ComponentAtDense(_denseIndex));
    }
}

public class ComponentStore<T> : IComponentStore where T : struct, IComponent
{
    public delegate void RefAction<TValue>(int entity, ref TValue value);
    private readonly SparseSet<T> _set = new();
    public Type ComponentType { get; } = typeof(T);
    public int Count => _set.Count;

    internal int EntityAtDense(int i) => _set.EntityAtDense(i);
    internal ref T ComponentAtDense(int i) => ref _set.DataAtDense(i);

    /// <summary>
    /// Unchecked sparse lookup — caller must have confirmed HasEntityIndex first.
    /// </summary>
    internal ref T GetByEntityIndex(int entityIndex) => ref _set.GetUnsafe(entityIndex);
    
    public bool RemoveByEntityIndex(int entityIndex)
        => _set.Remove(entityIndex);

    public bool HasEntityIndex(int entityIndex)
        => _set.Has(entityIndex);

    public void EnsureEntityCapacity(int entityCapacity) {}

    public bool Has(int entityIndex) => _set.Has(entityIndex);

    public ref T Get(int entityIndex)
    {
        if (!_set.Has(entityIndex))
            throw new ComponentAbsentException($"Component {typeof(T).Name} missing on entity index {entityIndex}");
        return ref _set[entityIndex];
    }

    /// <summary>
    /// output a copy of the component, not the reference
    /// </summary>
    /// <param name="entityIndex"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public bool TryGet(int entityIndex, out T value)
    {
        if (!_set.Has(entityIndex)) { value = default; return false; }
        value = _set.GetUnsafe(entityIndex);
        return true;
    }

    /// <summary>
    /// Add if missing, returns reference to the stored component
    /// </summary>
    /// <param name="entityIndex"></param>
    /// <returns></returns>
    public ref T GetOrAdd(int entityIndex)
    {
        if (!_set.Has(entityIndex))
            _set.Add(entityIndex, default);
        return ref _set.GetUnsafe(entityIndex);
    }

    /// <summary>
    /// Add new or replace existing, Return ref to stored component
    /// </summary>
    /// <param name="entityIndex"></param>
    /// <param name="value"></param>
    /// <returns></returns>
    public ref T Set(int entityIndex, in T value)
    {
        _set.Add(entityIndex, value);
        return ref _set.GetUnsafe(entityIndex);
    }

    // ---- Iterator base access ----
    // Enable foreach (ref T comp in store) {...}
    public SparseSet<T>.Enumerator GetEnumerator() => _set.GetEnumerator();

    // Enable page-by-page span iteration
    public SparseSet<T>.PageEnumerator GetPageEnumerator() => _set.GetPageEnumerator();

    /// <summary>
    /// Allow systems to do entity aware iteration
    /// ForEach((int entity, ref T comp) => {...})
    /// </summary>
    /// <param name="fn"></param>
    public void ForEach(RefAction<T> fn)
    {
        var it = _set.GetEnumerator();
        while (it.MoveNext())
            fn(it.CurrentEntity, ref it.Current);
    }

    // ---- Index base access ----
    public DenseView<T> Dense() => new DenseView<T>(this);


}