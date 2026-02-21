using Axle.Core.Time;

namespace Axle.Core;

public sealed class EngineLoop
{
    private readonly Clock _clock = new();
    private double _accumulator;

    public double FixedDtSeconds { get; } = 1.0 / 30.0; // Fixed
    public int MaxStepsPerFrame { get; set; } = 8;
    public int StepsPerFrame { get; private set; }

    public ulong TickIndex { get; private set; }

    private readonly ISimStage _sim;
    private readonly IRenderStage _render;

    public EngineLoop(ISimStage sim, IRenderStage render)
    {
        _sim = sim;
        _render = render;
        _clock.Reset();
    }

    public void Frame()
    {
        _clock.Tick();
        _accumulator += _clock.RealDtSeconds;

        StepsPerFrame = 0;
        while (_accumulator >= FixedDtSeconds && StepsPerFrame < MaxStepsPerFrame)
        {
            _sim.Step(FixedDtSeconds, TickIndex);
            TickIndex++;
            _accumulator -= FixedDtSeconds;
            StepsPerFrame++;
        }

        if (StepsPerFrame == MaxStepsPerFrame && _accumulator >= FixedDtSeconds)
        {
            _accumulator = Math.Min(_accumulator, FixedDtSeconds);
        }

        float alpha = (float) (_accumulator / FixedDtSeconds);
        _render.Draw(alpha);
    }
}
// FixedDt = 33ms, Assume real dt = 40ms, _accumulator changes like this
// 40ms -> 7ms
// 47ms -> 14ms
// 54ms -> 21ms
// 61ms -> 28ms
// 68ms -> 35ms -> 2ms

// Running the while loop twice taks CPU 80ms... adding to _accumulator

// 82ms -> 49ms -> 16ms
// 96ms...
// We are now generating "debt" faster than we can pay it off.