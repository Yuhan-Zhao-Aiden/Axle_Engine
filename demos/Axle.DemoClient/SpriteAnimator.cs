namespace Axle.Client;

using Axle.Core.AxleMath;
using Axle.Ecs;
using Axle.Graphics;


public struct SpriteAnimator : IComponent
{
    public Dictionary<string, AnimationClip> Clips;

    public string CurrentClip;

    public int FrameIndex;

    public float FrameTimer;

    public Vector2f Dimension;

    public SpriteAnimator(
        Dictionary<string, AnimationClip> clips,
        string defaultClip,
        Vector2f dimension)
    {
        Clips = clips;
        CurrentClip = clips.ContainsKey(defaultClip) ? defaultClip : (clips.Count > 0 ? clips.Keys.First() : string.Empty);
        FrameIndex = 0;
        FrameTimer = 0f;
        Dimension = dimension;
    }

    /// <summary>
    /// Switch to ClipName if it differs from the current clip.
    /// Resets frame and timer on transition. No-ops if the clip is unknown.
    /// </summary>
    public void TransitionTo(string clipName)
    {
        if (clipName == CurrentClip) return;
        if (!Clips.ContainsKey(clipName)) return;

        CurrentClip = clipName;
        FrameIndex = 0;
        FrameTimer = 0f;
    }

    /// <summary>Advances the animation timer by <paramref name="dt"/> seconds.</summary>
    public void Advance(float dt)
    {
        if (!Clips.TryGetValue(CurrentClip, out var clip) || clip.Frames.Length == 0)
            return;

        FrameTimer += dt;
        float frameDuration = 1f / clip.Fps;

        while (FrameTimer >= frameDuration)
        {
            FrameTimer -= frameDuration;
            FrameIndex++;

            if (FrameIndex >= clip.Frames.Length)
                FrameIndex = clip.Loop ? 0 : clip.Frames.Length - 1;
        }
    }

    /// <summary>The texture to display this frame, or null if no clip is loaded.</summary>
    public readonly Texture2D? CurrentTexture
    {
        get
        {
            if (!Clips.TryGetValue(CurrentClip, out var clip) || clip.Frames.Length == 0)
                return null;
            return clip.Frames[FrameIndex];
        }
    }
}
