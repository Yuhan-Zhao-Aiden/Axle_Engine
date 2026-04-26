namespace Axle.Client;

using Axle.Core.AxleMath;
using Axle.Ecs;
using Axle.Graphics;

public struct RenderSprite : IComponent
{
    public Texture2D Texture;
    public Vector2f  Dimension;
    public bool FlipX;

    public RenderSprite(Texture2D texture, Vector2f dimension, bool flipX = false)
    {
        Texture = texture;
        Dimension = dimension;
        FlipX = flipX;
    }
}
