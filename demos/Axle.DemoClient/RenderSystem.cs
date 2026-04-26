namespace Axle.Client.System;

using Axle.Client;
using Axle.Ecs;
using Axle.Graphics;

public class RenderSystem
{
    private readonly QuadRenderer _renderer;

    public RenderSystem(QuadRenderer renderer)
    {
        _renderer = renderer;
    }

    public void Render(World world, Camera camera)
    {
        _renderer.Begin(camera);

        // Textured entities (e.g. player with a sprite sheet)
        foreach (var item in world.Query<Transform, RenderSprite>())
        {
            _renderer.DrawTexturedQuad(
                x: item.Component1.Position.X,
                y: item.Component1.Position.Y,
                size: item.Component2.Dimension,
                texture: item.Component2.Texture,
                flipX: item.Component2.FlipX);
        }

        // Colored-square entities (ghost, coins, enemies, …)
        foreach (var item in world.Query<Transform, RenderRect>())
        {
            _renderer.DrawQuad(
                x: item.Component1.Position.X,
                y: item.Component1.Position.Y,
                size: item.Component2.Dimension,
                color: item.Component2.Color);
        }

        _renderer.End();
    }
}