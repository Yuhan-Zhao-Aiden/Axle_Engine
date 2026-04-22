using Axle.Net;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Axle.Client;

/// <summary>
/// Samples the current keyboard state and sends an ImputState packet
/// to the server at approximately 60 Hz. Call Update every render frame.
/// </summary>
public sealed class ClientInputSender
{
    private const long SendIntervalMs = 16; // 60 Hz

    private readonly NetClient _client;
    private ushort _seq;
    private long _lastSentMs;

    public ClientInputSender(NetClient client)
    {
        _client = client;
    }

    public void Update(KeyboardState ks, long nowMs)
    {
        if (nowMs - _lastSentMs < SendIntervalMs) return;

        InputButtons buttons = InputButtons.None;
        if (ks.IsKeyDown(Keys.W)) buttons |= InputButtons.Up;
        if (ks.IsKeyDown(Keys.S)) buttons |= InputButtons.Down;
        if (ks.IsKeyDown(Keys.A)) buttons |= InputButtons.Left;
        if (ks.IsKeyDown(Keys.D)) buttons |= InputButtons.Right;
        if (ks.IsKeyDown(Keys.Space)) buttons |= InputButtons.Jump;

        _client.SendInput(new InputState { Seq = _seq++, Buttons = (ushort)buttons });
        _lastSentMs = nowMs;
    }
}
