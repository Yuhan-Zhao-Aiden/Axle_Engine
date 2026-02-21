namespace Axle.Core;

public sealed class TickRateMonitor
{
    private double _windowSeconds;
    private ulong _windowTicks;

    public void OnSimTick(double fixedDt)
    {
        _windowSeconds += fixedDt;
        _windowTicks++;

        if (_windowSeconds >= 1.0)
        {
            double hz = _windowTicks / _windowSeconds;
            Console.WriteLine($"[SIM] {hz:F2} Hz over {_windowSeconds:F2}s");

            _windowSeconds = 0;
            _windowTicks = 0;
        }
    }
}