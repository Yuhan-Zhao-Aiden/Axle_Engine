namespace Axle.Client;

using Axle.Graphics;

/// <summary>
/// A named sequence of textures played at a fixed frame rate.
/// </summary>
public sealed class AnimationClip
{
    public string Name { get; }
    public Texture2D[] Frames { get; }
    public float Fps { get; }
    public bool Loop { get; }

    public AnimationClip(string name, Texture2D[] frames, float fps, bool loop)
    {
        Name = name;
        Frames = frames;
        Fps = fps;
        Loop = loop;
    }
}
