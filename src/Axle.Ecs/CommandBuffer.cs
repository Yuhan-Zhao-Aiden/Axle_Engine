using Axle.Core.Dsa;

namespace Axle.Ecs;

public class CommandBuffer
{
    private readonly AxleArray<CmdStream> _streams = new();
    private readonly Dictionary<StreamKey, int> _streamLookup = new();
    private readonly Dictionary<Type, IPayloadPool> _pool = new();
    public record struct StreamKey(int SystemIndex, int JobIndex);

    public SystemRecorder ForSystem(int systemIndex)
        => new SystemRecorder(this, systemIndex);

    private int GetOrCreateStreamId(int systemIndex, int jobIndex)
    {
        var key = new StreamKey(systemIndex, jobIndex);
        if (_streamLookup.TryGetValue(key, out var id))
            return id;

        id = _streams.Append(new CmdStream());
        _streamLookup[key] = id;
        return id;
    }

    private PayloadPool<T> GetOrCreatePayloadPool<T>()
        where T : struct, IComponent
    {
        if (_pool.TryGetValue(typeof(T), out var p))
            return (PayloadPool<T>) p;

        PayloadPool<T> newPool = new();
        _pool[typeof(T)] = newPool;
        return newPool;
    }

    /// <summary>
    /// Total number of recorded commands across all streams.
    /// </summary>
    public int Count
    {
        get
        {
            int total = 0;
            for (int i = 0; i < _streams.Count; i++)
                total += _streams[i].Commands.Count;
            return total;
        }
    }


    public void Playback(World world)
    {

        // --- Phase 1: Allocate real EntityIds for every Create command (§8.1) ---
        for (int s = 0; s < _streams.Count; s++)
        {
            CmdStream stream = _streams[s];
            for (int i = 0; i < stream.Commands.Count; i++)
            {
                ref readonly Cmd cmd = ref stream.Commands[i];
                if (cmd.Op == CmdOp.Create)
                    stream.TempToReal.Append(world.CreateEntity());
            }
        }

        // Pre-scan: collect entities that will be destroyed so Destroy wins (§8.3)
        var destroyedThisFlush = new HashSet<EntityId>();
        for (int s = 0; s < _streams.Count; s++)
        {
            CmdStream stream = _streams[s];
            for (int i = 0; i < stream.Commands.Count; i++)
            {
                ref readonly Cmd cmd = ref stream.Commands[i];
                if (cmd.Op == CmdOp.Destroy)
                {
                    EntityId e = ResolveTarget(cmd.Target, stream);
                    if (world.IsAlive(e))
                        destroyedThisFlush.Add(e);
                }
            }
        }

        // --- Phase 2: Apply Add / Remove commands (§8.1) ---
        for (int s = 0; s < _streams.Count; s++)
        {
            CmdStream stream = _streams[s];
            for (int i = 0; i < stream.Commands.Count; i++)
            {
                ref readonly Cmd cmd = ref stream.Commands[i];
                if (cmd.Op != CmdOp.Add && cmd.Op != CmdOp.Remove)
                    continue;

                EntityId e = ResolveTarget(cmd.Target, stream);
                if (!world.IsAlive(e) || destroyedThisFlush.Contains(e))
                    continue;

                if (cmd.Op == CmdOp.Add)
                    _pool[cmd.ComponentType!].Apply(world, e, cmd.PayloadIndex);
                else
                    world.RemoveByType(e, cmd.ComponentType!);
            }
        }

        // --- Phase 3: Destroy entities (§8.1) ---
        for (int s = 0; s < _streams.Count; s++)
        {
            CmdStream stream = _streams[s];
            for (int i = 0; i < stream.Commands.Count; i++)
            {
                ref readonly Cmd cmd = ref stream.Commands[i];
                if (cmd.Op == CmdOp.Destroy)
                    world.DestoryEntity(ResolveTarget(cmd.Target, stream));
            }
        }

        Clear();
    }

    /// <summary>
    /// Resets all recorded commands and temp maps, keeping stream slots alive.
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < _streams.Count; i++)
            _streams[i].Reset();
        foreach (var pool in _pool.Values)
            pool.Clear();
    }

    private static EntityId ResolveTarget(in Target target, CmdStream stream)
        => target.IsTemp ? stream.TempToReal[target.Temp.Value] : target.Entity;

    public struct Writer
    {
        private readonly CommandBuffer _cb;
        private readonly int _streamIndex;
        private int _sequence;

        internal Writer(CommandBuffer cb, int streamIndex) 
        { 
            _cb = cb;
            _streamIndex = streamIndex;
        }

        /// Create TempEntityId
        /// Cmd
        /// access _cb._streams[_streamIndex]
        public TempEntityId RecordCreateEntity()
        {
            CmdStream stream = _cb._streams[_streamIndex];
            TempEntityId tempId = new() { 
                Value = stream.NextTempId++
            };
            stream.Commands.Append(Cmd.CreateEntity(_sequence++, tempId));
            return tempId;
        }

        public void RecordDestroyEntity(Target target)
        {
            CmdStream stream = _cb._streams[_streamIndex];
            stream.Commands.Append(Cmd.DestroyEntity(_sequence++, target));
        }
         
        public void RecordAddComponent<T>(Target target, T comp)
            where T : struct, IComponent
        {
            CmdStream stream = _cb._streams[_streamIndex];
            PayloadPool<T> p = _cb.GetOrCreatePayloadPool<T>();
            int payloadIndex = p.Add(comp);
            stream.Commands.Append(Cmd.AddComponent(_sequence++, target, typeof(T), payloadIndex));
        }

        public void RecordRemoveComponent<T>(Target target)
            where T : struct, IComponent
        {
            CmdStream stream = _cb._streams[_streamIndex];
            stream.Commands.Append(
                Cmd.RemoveComponent(_sequence++, target, typeof(T))
            );
        }
    }
    public readonly struct SystemRecorder
    {
        private readonly CommandBuffer _cb;
        private readonly int _systemIndex;
        internal SystemRecorder(CommandBuffer cb, int systemIndex) {
            _cb = cb;
            _systemIndex = systemIndex;
        }
        public Writer CreateWriter(int jobIndex = 0)
        {
            int streamId = _cb.GetOrCreateStreamId(_systemIndex, jobIndex);
            return new Writer(_cb, streamId);
        }
    } 
}

internal sealed class CmdStream
{
    public readonly AxleArray<Cmd> Commands = new();
    public readonly AxleArray<EntityId> TempToReal = new();
    public int NextTempId;

    internal void Reset()
    {
        Commands.Clear();
        TempToReal.Clear();
        NextTempId = 0;
    }
}


public readonly record struct TempEntityId(int Value);

internal enum CmdOp : byte { Create, Destroy, Add, Remove }

internal readonly struct Cmd
{
    public readonly CmdOp Op;
    public readonly int Sequence { get; init; }
    //public readonly int TypeId;
    public readonly Type? ComponentType { get; init; }
    public readonly Target Target { get; init; }
    public readonly int PayloadIndex { get; init; }

    private Cmd
        (CmdOp op, int seq, Target target, Type? type = null, int payload = -1)
    {
        Op = op;
        Sequence = seq;
        ComponentType = type;
        Target = target;
        PayloadIndex = payload;
    }

    public static Cmd CreateEntity(int seq, TempEntityId id)
        => new(CmdOp.Create, seq, new Target { IsTemp = true, Temp = id });

    public static Cmd DestroyEntity(int seq, Target target)
        => new(CmdOp.Destroy, seq, target);

    public static Cmd AddComponent(int seq, Target target, Type type, int payload)
        => new(CmdOp.Add, seq, target, type, payload);

    public static Cmd RemoveComponent(int seq, Target target, Type type)
        => new(CmdOp.Remove, seq, target, type);
}

internal interface IPayloadPool
{
    int Count { get; }
    void Apply(World world, EntityId e, int payloadIndex);
    void Clear();
}

internal sealed class PayloadPool<T> : IPayloadPool where T : struct, IComponent
{
    private readonly AxleArray<T> _items = new();
    public int Count => _items.Count;
    public ref T this[int i] => ref _items[i];
    public int Add(T component) => _items.Append(component);

    public void Apply(World world, EntityId e, int payloadIndex)
        => world.Register<T>().Set(e.Index, _items[payloadIndex]);

    public void Clear() => _items.Clear();
}

public readonly struct Target
{
    public readonly bool IsTemp { get; init; }
    public readonly EntityId Entity { get; init; }
    public readonly TempEntityId Temp { get; init; }

    public static Target CreateReal(EntityId real)
        => new Target { Entity = real };
    public static Target CreateTemp(TempEntityId temp)
        => new Target { IsTemp = true, Temp = temp };
}