namespace Axle.Client.System;

using Axle.Core.AxleMath;
using Axle.Core.Utility;
using Axle.Graphics;
using Axle.Sim.Map;

public sealed class MapRenderSystem
{
    private readonly MapData _map;
    private readonly QuadRenderer _renderer;
    private const float TileSize = 32f;

    public MapRenderSystem(MapData map, QuadRenderer renderer)
    {
        _map = map;
        _renderer = renderer;
    }

    /// <summary>
    /// Renders all authored tiles. Void cells are skipped.
    /// Pixel (0,0) is screen centre; positive Y goes down 
    /// </summary>
    public void Render(Camera camera)
    {
        _renderer.Begin(camera);

        for (int y = 0; y < _map.Height; y++)
        {
            for (int x = 0; x < _map.Width; x++)
            {
                if (!_map.TryGetTile(x, y, out TileType tile))
                    continue;

                // Color chosen per tile type. New TileTypes only need a case here.
                Color4 color = tile switch
                {
                    TileType.Wall => new Color4(0.5f, 0.5f, 0.5f),
                    TileType.Floor => new Color4(0.25f, 0.25f, 0.25f),
                    _ => new Color4(1f, 0f, 1f),
                };

                _renderer.DrawQuad(
                    x: x * TileSize,
                    y: y * TileSize,
                    size: new Vector2f(TileSize, TileSize),
                    color: color);
            }
        }

        _renderer.End();
    }
}