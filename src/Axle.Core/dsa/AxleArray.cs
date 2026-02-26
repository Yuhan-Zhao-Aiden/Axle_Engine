namespace Axle.Core.Dsa;

/// <summary>
/// AxleArray is a implementation of Array with auto resizing
/// Generic constraint: T is value type
/// </summary>
public class AxleArray<T> where T : struct
{
    private T[] _data;
    public int Length => _data.Length;

    /// <summary>
    /// Setter ensures capacity automatically, Try to keep index small
    /// </summary>
    public T this[int i]
    {
        get
        {
            if (i < 0 || i >= Length) 
                throw new IndexOutOfRangeException($"{i} not in range");

            return _data[i];
        }

        set
        {
            if (i < 0) 
                throw new IndexOutOfRangeException($"{i} not in range");
            
            EnsureCapacity(i + 1);
            _data[i] = value;
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

}