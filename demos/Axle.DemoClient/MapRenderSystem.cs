namespace Axle.Client.System;

using Axle.Core.AxleMath;
using Axle.Core.Utility;
using Axle.Graphics;
using Axle.Sim.Map;

public sealed class MapRenderSystem
{
    private readonly MapData _map;
    private readonly QuadRenderer _renderer;
    private readonly IReadOnlyDictionary<char, Texture2D>? _textures;
    private const float TileSize = 32f;

    /// <param name="map">The map data to render.</param>
    /// <param name="renderer">The quad renderer.</param>
    /// <param name="textures">
    /// Optional per-character texture lookup. When null or when a
    /// character has no entry, the tile falls back to a colored square.
    /// </param>
    public MapRenderSystem(MapData map, QuadRenderer renderer,
        IReadOnlyDictionary<char, Texture2D>? textures = null)
    {
        _map      = map;
        _renderer = renderer;
        _textures = textures;
    }

    /// <summary>
    /// Renders all authored tiles. Void cells are skipped.
    /// Tiles with a texture entry render as sprites; others fall back to colored squares.
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

                float wx = x * TileSize;
                float wy = y * TileSize;
                var   sz = new Vector2f(TileSize, TileSize);

                // Try textured rendering first
                if (_textures is not null
                    && _map.TryGetSymbol(x, y, out char sym)
                    && _textures.TryGetValue(sym, out Texture2D? tex))
                {
                    _renderer.DrawTexturedQuad(wx, wy, sz, tex);
                    continue;
                }

                // Fallback: colored square, same colors as before
                Color4 color = tile switch
                {
                    TileType.Wall  => new Color4(0.5f, 0.5f, 0.5f),
                    TileType.Floor => new Color4(0.25f, 0.25f, 0.25f),
                    _              => new Color4(1f, 0f, 1f),
                };

                _renderer.DrawQuad(wx, wy, sz, color);
            }
        }

        _renderer.End();
    }
}