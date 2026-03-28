namespace Axle.Graphics;

/// <summary>
/// Describes the viewport through which the world is rendered.
/// Passed to <see cref="QuadRenderer.Begin"/> each frame.
/// </summary>
public sealed class Camera
{
    public int ViewportWidth  { get; }
    public int ViewportHeight { get; }

    public Camera(int viewportWidth, int viewportHeight)
    {
        ViewportWidth  = viewportWidth;
        ViewportHeight = viewportHeight;
    }
}
