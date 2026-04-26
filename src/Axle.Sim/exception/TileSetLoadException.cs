namespace Axle.Sim;

/// <summary>
/// Thrown when a <c>.tiles.json</c> file fails validation during loading.
/// </summary>
public class TileSetLoadException : Exception
{
    public TileSetLoadException(string msg) : base(msg) { }
}
