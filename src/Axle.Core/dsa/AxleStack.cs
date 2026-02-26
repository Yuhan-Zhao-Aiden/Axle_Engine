namespace Axle.Core.Dsa;

public class AxleStack<T> where T : struct
{
    private T[] _data;
    public int Count { get; private set; }

    public AxleStack(int size = 16)
    {
        _data = new T[size];
        Count = 0;
    }

    public void Push(T value)
    {
        // If full, Resize
        if (Count >= _data.Length)
        {
            Array.Resize(ref _data, _data.Length * 2);
        }

        _data[Count++] = value;
    }

    public T Pop()
    {
        if (Count <= 0) throw new InvalidOperationException("Stack empty");
        return _data[Count--];
    }

    public T Peek()
    {
        if (Count <= 0) throw new InvalidOperationException("Stack empty");
        return _data[Count];
    }
}