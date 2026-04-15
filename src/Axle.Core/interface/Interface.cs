namespace Axle.Core;
public interface ISimStage
{
    void Step(double FixedDtSeconds, ulong TickIndex);
}

public interface IRenderStage
{
    void Draw(float alpha);
}

public interface IComponent {}