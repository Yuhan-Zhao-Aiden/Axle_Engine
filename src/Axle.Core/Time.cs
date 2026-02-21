using System.Diagnostics;

namespace Axle.Core.Time;

public sealed class Clock
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private long _lastTicks;

    public double RealDtSeconds { get; private set; }
    public double TotalSeconds { get; private set; }

    public void Reset()
    {
        _sw.Restart();
        _lastTicks = _sw.ElapsedTicks;
        RealDtSeconds = 0;
        TotalSeconds = 0;
    }

    public void Tick(double maxDtSeconds = 0.25)
    {
        long now = _sw.ElapsedTicks;
        long deltaTicks = now - _lastTicks;
        _lastTicks = now;

        double dt = (double)deltaTicks / Stopwatch.Frequency;

        if (dt > maxDtSeconds) dt = maxDtSeconds;
        RealDtSeconds = dt;
        TotalSeconds += dt;
    }
}