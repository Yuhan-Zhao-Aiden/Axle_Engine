namespace Axle.Ecs;

using Axle.Core.AxleMath;

/// <summary>
/// Entity-vs-entity collision layer. Carries layer ID and AABB half-extents
/// used exclusively by the overlap system (independent of PlayerCollider).
/// </summary>
public struct CollisionLayer : IComponent
{
    public byte LayerId;
    public Fixed32 HalfW;
    public Fixed32 HalfH;

    public CollisionLayer(byte layerId, Fixed32 halfW, Fixed32 halfH)
    {
        LayerId = layerId;
        HalfW = halfW;
        HalfH = halfH;
    }
}


public static class CollisionLayers
{
    public const byte Player = 0;
    public const byte Collectible = 1;
    public const byte Projectile = 2;
    public const byte Enemy = 3;
}
