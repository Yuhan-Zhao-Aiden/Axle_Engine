namespace Axle.Core.AxleMath;

public readonly struct Fixed32 : IEquatable<Fixed32>, IComparable<Fixed32>
{
    private readonly int _raw;

    public const int FractionalBits = 16;
    public const int OneRaw = 1 << FractionalBits;

    public static readonly Fixed32 Zero;
    public static readonly Fixed32 One = new Fixed32(OneRaw);
    public static readonly Fixed32 Half = FromFloat(0.5f);

    private Fixed32(int raw)
    {
        _raw = raw;
    }

    public static Fixed32 FromRaw(int raw) => new Fixed32(raw);
    public static Fixed32 FromInt(int value) => new Fixed32(value << FractionalBits);
    public static Fixed32 FromFloat(float value)
        => new Fixed32((int)Math.Round(value * OneRaw, MidpointRounding.AwayFromZero));
    public static Fixed32 FromDouble(double value)
        => new Fixed32((int)Math.Round(value * OneRaw, MidpointRounding.AwayFromZero));

    public int RawValue => _raw;
    public int ToInt() => _raw >> FractionalBits;
    public float ToFloat() => (float)_raw / OneRaw;
    public double ToDouble() => (double)_raw / OneRaw;

    public bool Equals(Fixed32 other) => _raw == other._raw;

    public override bool Equals(object? obj) => obj is Fixed32 other && Equals(other);
    public override int GetHashCode() => _raw;

    public int CompareTo(Fixed32 other) => _raw.CompareTo(other._raw);

    // ---- Arithmetic ----
    public static Fixed32 operator +(Fixed32 a, Fixed32 b)
        => new Fixed32(a._raw + b._raw);

    public static Fixed32 operator -(Fixed32 a, Fixed32 b)
        => new Fixed32(a._raw - b._raw);

    public static Fixed32 operator *(Fixed32 a, Fixed32 b)
        => new Fixed32((int)(((long)a._raw * b._raw) >> FractionalBits));

    public static Fixed32 operator /(Fixed32 a, Fixed32 b)
    {
        if (b._raw == 0)
            throw new DivideByZeroException();
        
        return new Fixed32((int)(((long)a._raw << FractionalBits) / b._raw));
    }

    public static Fixed32 operator -(Fixed32 a)
        => new Fixed32(-a._raw);

    // ---- Comparison ----
    public static bool operator ==(Fixed32 a, Fixed32 b) => a._raw == b._raw;
    public static bool operator !=(Fixed32 a, Fixed32 b) => a._raw != b._raw;
    public static bool operator  <(Fixed32 a, Fixed32 b) => a._raw  < b._raw;
    public static bool operator <=(Fixed32 a, Fixed32 b) => a._raw <= b._raw;
    public static bool operator  >(Fixed32 a, Fixed32 b) => a._raw  > b._raw;
    public static bool operator >=(Fixed32 a, Fixed32 b) => a._raw >= b._raw;

    public override string ToString() 
        => ToDouble().ToString();
}
