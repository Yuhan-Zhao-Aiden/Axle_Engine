namespace Axle.Client;

using Axle.Ecs;
using Axle.Sim.Map;

/// <summary>
/// Computes the camera world-space centre each render frame.
/// </summary>
public sealed class CameraController
{
    private readonly CameraSettings _settings;
    private readonly float _mapWorldW;
    private readonly float _mapWorldH;

    private float _currentX;
    private float _currentY;

    private const float TileSize = 32f;
    private const float SmoothFactor = 0.1f;

    public CameraController(CameraSettings settings, MapData map)
    {
        _settings = settings;
        _mapWorldW = map.Width * TileSize;
        _mapWorldH = map.Height * TileSize;
        _currentX = settings.FixedX;
        _currentY = settings.FixedY;
    }

    /// <summary>
    /// Returns the clamped camera world-centre for this render frame.
    /// </summary>
    public (float X, float Y) Update(World world, int viewportW, int viewportH)
    {
        float targetX, targetY;

        if (_settings.Mode == CameraMode.Follow)
        {
            // Fall back to current position in case no local player exists yet.
            targetX = _currentX;
            targetY = _currentY;

            foreach (var item in world.Query<SimPosition, LocalPlayer>())
            {
                // SimPosition is top-left of a 32×32 entity; offset to its centre.
                targetX = item.Component1.X.ToFloat() + TileSize * 0.5f;
                targetY = item.Component1.Y.ToFloat() + TileSize * 0.5f;
                break;
            }
        }
        else
        {
            targetX = _settings.FixedX;
            targetY = _settings.FixedY;
        }

        if (_settings.Smoothing)
        {
            _currentX += (targetX - _currentX) * SmoothFactor;
            _currentY += (targetY - _currentY) * SmoothFactor;
        }
        else
        {
            _currentX = targetX;
            _currentY = targetY;
        }

        // Clamp so no outside-map space is visible.
        float halfW = viewportW * 0.5f;
        float halfH = viewportH * 0.5f;

        _currentX = _mapWorldW >= viewportW
            ? Math.Clamp(_currentX, halfW, _mapWorldW - halfW)
            : _mapWorldW * 0.5f;

        _currentY = _mapWorldH >= viewportH
            ? Math.Clamp(_currentY, halfH, _mapWorldH - halfH)
            : _mapWorldH * 0.5f;

        return (_currentX, _currentY);
    }
}
