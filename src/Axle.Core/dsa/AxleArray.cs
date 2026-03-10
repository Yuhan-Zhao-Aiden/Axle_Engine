namespace Axle.Core.Dsa;

/// <summary>
/// AxleArray is a implementation of List with auto resizing
/// Generic constraint: T is value type
/// </summary>
public class AxleArray<T> 
{
    private T[] _data;
    public int Length => _data.Length;
    public int Count { get; private set; }

    /// <summary>
    /// Setter only modifies existing value, does not add new data to the array
    /// </summary>
    public ref T this[int i]
    {
        get
        {
            if (i < 0 || i >= Length) 
                throw new IndexOutOfRangeException($"{i} not in range");

            return ref _data[i];
        }
 
    }

    public AxleArray(int size = 16)
    {
        _data = new T[size];
    }

    public void EnsureCapacity(int size)
    {
        if (size <= Length) return;
        int newSize = Math.Max(size, Length * 2);
        Array.Resize(ref _data, newSize);
    }

    public void Set(int index, T value)
    {
        if (index < 0 || index >= Count) 
            throw new IndexOutOfRangeException($"{index} not in range");
        _data[index] = value;
    }

    public int Append(in T data)
    {
        EnsureCapacity(Count + 1);
        _data[Count] = data;
        return Count++;
    }

    ///<summary>
    /// High performance append, return ref to the next empty slot
    /// for modification
    /// </summary>
    public ref T AllocateNext()
    {
        EnsureCapacity(Count + 1);
        return ref _data[Count++];
    }

    public void Clear() => Count = 0;
}