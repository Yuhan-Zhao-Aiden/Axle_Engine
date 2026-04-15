using Axle.Ecs;
using Axle.Sim;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Axle.System;

/// <summary>
/// Reads keyboard state and writes MoveInput for the local player entity.

/// </summary>
public sealed class LocalInputSystem : ISystem
{
    private KeyboardState? _keyboard;

    public void Update(KeyboardState keyboard)
    {
        _keyboard = keyboard;
    }

    public void Run(World world)
    {
        if (_keyboard is null)
            return;

        int x = 0;
        int y = 0;

        if (_keyboard.IsKeyDown(Keys.A)) x -= 1;
        if (_keyboard.IsKeyDown(Keys.D)) x += 1;
        if (_keyboard.IsKeyDown(Keys.W)) y -= 1;
        if (_keyboard.IsKeyDown(Keys.S)) y += 1;

        foreach (var item in world.Query<LocalPlayer, MoveInput>())
        {
            item.Component2.X = Math.Clamp(x, -1, 1);
            item.Component2.Y = Math.Clamp(y, -1, 1);
        }
    }
}
