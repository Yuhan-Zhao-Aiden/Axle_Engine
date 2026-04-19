using Axle.Core.Dsa;

namespace Axle.Ecs;

public class World
{
    private int[] _versions = [];
    private bool[] _alive = [];
    private readonly AxleStack<int> _free = new();
    public int AliveCount { get; private set; }
    private int _maxIndex;
    private readonly Dictionary<Type, IComponentStore> _stores = new();

    private void EnsureCapacity(int index)
    {
        if (index + 1 >= _versions.Length)
        {
            int newLength = Math.Max(index + 1, _versions.Length * 2);
            Array.Resize(ref _versions, newLength);
            Array.Resize(ref _alive, newLength);
        }
    }

    // ---- Lifecycle ----

    public EntityId CreateEntity()
    {
        int index;
        AliveCount++;

        // If we can reuse index
        if (_free.Count != 0)
        {
            index = _free.Pop();
            _alive[index] = true;
            return new EntityId(index, _versions[index]);
        }

        // If _free is empty
        index = _maxIndex++;
        EnsureCapacity(index + 1);

        _alive[index] = true;
        return new EntityId(index, 0);
    }

    // mark _alive[index] = false
    // bump _versions[index]++
    // push index to _free
    // Remove all components
    public bool DestoryEntity(EntityId e)
    {
        // If not alive or version mismatch return false
        if (!_alive[e.Index] || e.Version != _versions[e.Index])
            return false;

        foreach (var store in _stores.Values)
            store.RemoveByEntityIndex(e.Index);

        _alive[e.Index] = false;
        _versions[e.Index]++;
        _free.Push(e.Index);
        AliveCount--;
        return true;
    }

    // ---- Validation Helper ----

    public bool IsAlive(EntityId e) => 
        (uint) e.Index < (uint) _versions.Length &&
        _alive[e.Index] && 
        e.Version == _versions[e.Index];

    /// <summary>
    /// Resolves a full <see cref="EntityId"/> from the raw entity index returned by
    /// query iterators. Allows overlap systems to pass the correct (index, version)
    /// pair to <see cref="DestoryEntity"/>.
    /// </summary>
    public EntityId GetEntityId(int index) => new EntityId(index, _versions[index]);


    // ---- Store Registration / Access -----
    // Create new component store, return existing store
    public ComponentStore<T> Register<T>()
        where T : struct, IComponent
    {
        if (_stores.TryGetValue(typeof(T), out IComponentStore? store))
            return (ComponentStore<T>) store;

        ComponentStore<T> newStore = new();
        _stores[typeof(T)] = newStore;
        return newStore;
    }

    /// <summary>
    /// Gets existing component store, throw if store not registered
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <returns></returns>
    /// <exception cref="StoreNotRegisteredException"></exception>
    public ComponentStore<T> Store<T>()
        where T : struct, IComponent
    {
        var type = typeof(T);

        if (!_stores.TryGetValue(type, out var store))
            throw new StoreNotRegisteredException(
                $"Component store for {type.Name} is not registered."
            );

        return (ComponentStore<T>) store;
    }

    public IComponentStore Store(Type type)
    {
        if (!_stores.TryGetValue(type, out var store))
             throw new StoreNotRegisteredException(
                $"Component store for {type.Name} is not registered."
            );
        return store;           
    }

    // ---- Components ----
    public ref T Add<T>(EntityId e) where T : struct, IComponent
    {
        if (!IsAlive(e))
            throw new InvalidEntityException($"Entity {e.Index} is not alive");

        return ref Store<T>().GetOrAdd(e.Index);
    }

    public ref T Add<T>(EntityId e, in T value) where T : struct, IComponent
    {
        if (!IsAlive(e))
            throw new InvalidEntityException($"Entity {e.Index} is not alive");

        return ref Store<T>().Set(e.Index, value);
    }

    public bool Remove<T>(EntityId e) where T : struct, IComponent
    {
        if (!IsAlive(e))
            throw new InvalidEntityException($"Entity {e.Index} is not alive");

        return Store<T>().RemoveByEntityIndex(e.Index);
    }

    public bool RemoveByType(EntityId e, Type componentType)
    {
        if (!IsAlive(e))
            throw new InvalidEntityException($"Entity {e.Index} is not alive");

        return Store(componentType).RemoveByEntityIndex(e.Index);
    }

    public bool Has<T>(EntityId e) where T : struct, IComponent
    {
         if (!IsAlive(e))
            throw new InvalidEntityException($"Entity {e.Index} is not alive");

        return Store<T>().Has(e.Index);
    }

    public ref T Get<T>(EntityId e) where T : struct, IComponent
    {
        if (!IsAlive(e))
            throw new InvalidEntityException($"Entity {e.Index} is not alive");

        return ref Store<T>().Get(e.Index);
    }

    // ---- Iteration ----
    public DenseView<T> Query<T>() where T : struct, IComponent
        => Store<T>().Dense();

    public JoinView<T1, T2> Query<T1, T2>()
        where T1 : struct, IComponent
        where T2 : struct, IComponent
        => new JoinView<T1, T2>(Store<T1>(), Store<T2>());
}