namespace Axle.Net;

/// <summary>
/// Per-client input buffer stored on the server.
/// </summary>
public struct BufferedInput
{
    /// <summary>Sequence number of the most recently accepted packet.</summary>
    public ushort LatestSeq;

    /// <summary>The most recently accepted input state.</summary>
    public InputState LatestState;

    /// <summary>True once at least one valid input packet has been received.</summary>
    public bool HasInput;
}
