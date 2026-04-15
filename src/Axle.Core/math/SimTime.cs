namespace Axle.Core.AxleMath;

public static class SimTime
{
    public const int TickRate = 30;
    public static readonly Fixed32 Dt = Fixed32.One / Fixed32.FromInt(TickRate);
}
