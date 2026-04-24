namespace Axle.Net;

public sealed class SimulatedTransport : ITransport
{
    private readonly ITransport _inner;
    private readonly NetSimSettings _settings;
    private readonly Random _rng = new();

    private readonly Queue<(NetEndpoint Destination, byte[] Payload, long DeliverAtMs)> _pending = new();

    public SimulatedTransport(ITransport inner, NetSimSettings settings)
    {
        _inner = inner;
        _settings = settings;
    }

    public void Start(int port) => _inner.Start(port);
    public void Stop() => _inner.Stop();

    public void Send(NetEndpoint endpoint, ReadOnlySpan<byte> payload)
    {
        if (!_settings.Enabled)
        {
            _inner.Send(endpoint, payload);
            return;
        }

        // Drop packet randomly based on loss percentage.
        if (_settings.LossPercent > 0f && _rng.NextDouble() * 100.0 < _settings.LossPercent)
            return;

        int jitter = _settings.JitterMs > 0 ? _rng.Next(-_settings.JitterMs, _settings.JitterMs + 1) : 0;
        long deliverAt = Environment.TickCount64 + _settings.BaseDelayMs + jitter;

        _pending.Enqueue((endpoint, payload.ToArray(), deliverAt));
    }

    public bool TryReceive(out TransportPacket packet)
    {
        // Flush outgoing packets wher delivery time has arrived before receiving.
        if (_settings.Enabled)
            FlushDue();

        return _inner.TryReceive(out packet);
    }

    private void FlushDue()
    {
        long now = Environment.TickCount64;
        while (_pending.Count > 0 && _pending.Peek().DeliverAtMs <= now)
        {
            var (dest, data, _) = _pending.Dequeue();
            _inner.Send(dest, data);
        }
    }
}
