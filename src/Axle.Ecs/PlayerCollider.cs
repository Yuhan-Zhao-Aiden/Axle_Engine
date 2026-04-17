namespace Axle.Ecs;

using Axle.Core.AxleMath;

/// <summary>
/// Axis-aligned bounding box collider.
/// </summary>
public struct PlayerCollider : IComponent
{
    public Fixed32 HalfWidth;
    public Fixed32 HalfHeight;

    public PlayerCollider(Fixed32 halfWidth, Fixed32 halfHeight)
    {
        HalfWidth  = halfWidth;
        HalfHeight = halfHeight;
    }
}
