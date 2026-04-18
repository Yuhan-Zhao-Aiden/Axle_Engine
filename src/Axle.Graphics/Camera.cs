namespace Axle.Graphics;

/// <summary>
/// Describes the viewport through which the world is rendered.
/// Passed to QuadRender.begin each frame.
/// </summary>
public sealed class Camera
{
    public int ViewportWidth { get; }
    public int ViewportHeight { get; }
    public float WorldX { get; }
    public float WorldY { get; }

    public Camera(int viewportWidth, int viewportHeight, float worldX = 0f, float worldY = 0f)
    {
        ViewportWidth = viewportWidth;
        ViewportHeight = viewportHeight;
        WorldX = worldX;
        WorldY = worldY;
    }
}
