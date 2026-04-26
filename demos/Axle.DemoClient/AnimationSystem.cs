namespace Axle.Client.System;

using Axle.Client;
using Axle.Ecs;
using Axle.Sim;

/// <summary>
/// Updates SpriteAnimator components each render frame.

/// </summary>
public sealed class AnimationSystem
{
    private readonly GameMode _mode;

    public AnimationSystem(GameMode mode) => _mode = mode;

    /// <summary>Advance all animated entities. Call once per render frame with wall-clock dt.</summary>
    public void Run(World world, float dt)
    {
        var moveInputStore = world.Store<MoveInput>();
        var velStore       = world.Store<Velocity>();
        var spriteStore    = world.Store<RenderSprite>();

        foreach (var item in world.Query<LocalPlayer, SpriteAnimator>())
        {
            ref var animator = ref item.Component2;
            int entity = item.Entity;

            var moveInput = moveInputStore.Get(entity);
            var vel       = velStore.Get(entity);

            animator.TransitionTo(DeterminePlayerClip(moveInput, vel));
            animator.Advance(dt);

            var tex = animator.CurrentTexture;
            if (tex is null) continue;

            ref var sprite = ref spriteStore.Get(entity);
            sprite.Texture = tex;

            if (moveInput.X != 0)
                sprite.FlipX = moveInput.X < 0;
        }

        foreach (var item in world.Query<Collectible, SpriteAnimator>())
        {
            ref var animator = ref item.Component2;
            animator.Advance(dt);

            var tex = animator.CurrentTexture;
            if (tex is null) continue;

            ref var sprite = ref spriteStore.Get(item.Entity);
            sprite.Texture = tex;
        }
    }

    private string DeterminePlayerClip(MoveInput input, Velocity velocity)
    {
        if (_mode == GameMode.Platformer)
        {
            if (velocity.Y.RawValue < 0) return "jump";
            if (velocity.Y.RawValue > 0) return "fall";
        }
        if (input.X != 0 || input.Y != 0) return "walk";
        return "idle";
    }
}
