namespace Axle.Core.Test;

using Axle.Core.AxleMath;

public class Fixed32Test
{
    [Fact]
    public void Construction_UsesExpectedQ16_16RawValues()
    {
        Assert.Equal(Fixed32.Zero, Fixed32.FromInt(0));
        Assert.Equal(Fixed32.One, Fixed32.FromInt(1));
        Assert.Equal(-3 << Fixed32.FractionalBits, Fixed32.FromInt(-3).RawValue);
        Assert.Equal(32768, Fixed32.FromRaw(32768).RawValue);
    }

    [Fact]
    public void Conversion_PreservesDocumentedValues()
    {
        Assert.Equal(5, Fixed32.FromInt(5).ToInt());
        Assert.Equal(98304, Fixed32.FromFloat(1.5f).RawValue);
        Assert.Equal(0.5d, Fixed32.FromRaw(32768).ToDouble());
        Assert.Equal(-0.5d, Fixed32.FromRaw(-32768).ToDouble());
        Assert.Equal(2.25d, Fixed32.FromDouble(2.25d).ToDouble());
    }

    [Fact]
    public void FromDouble_RoundsToNearestAwayFromZero_PerSpecification()
    {
        double halfRawUnit = 1d / (Fixed32.OneRaw * 2d);

        Assert.Equal(1, Fixed32.FromDouble(halfRawUnit).RawValue);
        Assert.Equal(-1, Fixed32.FromDouble(-halfRawUnit).RawValue);
    }

    [Fact]
    public void Arithmetic_ProducesExpectedResults()
    {
        Assert.Equal(Fixed32.FromInt(3), Fixed32.FromInt(1) + Fixed32.FromInt(2));
        Assert.Equal(Fixed32.FromDouble(2.75d), Fixed32.FromDouble(2.5d) + Fixed32.FromDouble(0.25d));
        Assert.Equal(Fixed32.FromDouble(3.5d), Fixed32.FromInt(5) - Fixed32.FromDouble(1.5d));
        Assert.Equal(Fixed32.FromInt(6), Fixed32.FromInt(2) * Fixed32.FromInt(3));
        Assert.Equal(Fixed32.FromInt(3), Fixed32.FromDouble(1.5d) * Fixed32.FromInt(2));
        Assert.Equal(Fixed32.FromDouble(0.25d), Fixed32.Half * Fixed32.Half);
        Assert.Equal(Fixed32.FromInt(-6), -Fixed32.FromInt(3) * Fixed32.FromInt(2));
        Assert.Equal(Fixed32.FromInt(3), Fixed32.FromInt(6) / Fixed32.FromInt(2));
        Assert.Equal(Fixed32.FromDouble(1.5d), Fixed32.FromInt(3) / Fixed32.FromInt(2));
        Assert.Equal(Fixed32.FromDouble(0.25d), Fixed32.FromInt(1) / Fixed32.FromInt(4));
        Assert.Equal(Fixed32.FromInt(-3), Fixed32.FromInt(-6) / Fixed32.FromInt(2));
    }

    [Fact]
    public void Division_ByZero_Throws()
    {
        Assert.Throws<DivideByZeroException>(() => Fixed32.One / Fixed32.Zero);
    }

    [Fact]
    public void Determinism_RepeatedMovementIntegrationMatchesExpectedRawValue()
    {
        Fixed32 velocity = Fixed32.FromDouble(1.5d);
        Fixed32 positionA = Fixed32.Zero;
        Fixed32 positionB = Fixed32.Zero;

        for (int i = 0; i < 10; i++)
        {
            positionA += velocity;
            positionB += velocity;
        }

        Assert.Equal(Fixed32.FromInt(15), positionA);
        Assert.Equal(positionA.RawValue, positionB.RawValue);
    }
}
