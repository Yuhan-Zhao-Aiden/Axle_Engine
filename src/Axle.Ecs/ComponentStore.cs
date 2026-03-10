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

public class ComponentStore<T> : IComponentStore where T : struct, IComponent
{
    public delegate void RefAction<TValue>(int entity, ref TValue value);
    private readonly SparseSet<T> _set = new();
    public Type ComponentType { get; } = typeof(T);
    public int Count => _set.Count;

    internal int EntityAtDense(int i) => _set.EntityAtDense(i);
    internal ref T ComponentAtDense(int i) => ref _set.DataAtDense(i);
    
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