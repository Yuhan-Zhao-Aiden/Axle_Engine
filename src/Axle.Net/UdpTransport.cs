using System.Net;
using System.Net.Sockets;

namespace Axle.Net;

public sealed class UdpTransport : ITransport
{
    private Socket? _socket;
    private readonly byte[] _receiveBuffer = new byte[1500];
    // Reused as an out-parameter for ReceiveFrom; updated to sender's address each call.
    private EndPoint _senderEndPoint = new IPEndPoint(IPAddress.Any, 0);

    public void Start(int port)
    {
        _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
        _socket.Bind(new IPEndPoint(IPAddress.Any, port));
        _socket.Blocking = false;
    }

    public void Send(NetEndpoint endpoint, ReadOnlySpan<byte> payload)
    {
        if (_socket == null) return;
        _socket.SendTo(payload, endpoint.ToIPEndPoint());
    }

    public bool TryReceive(out TransportPacket packet)
    {
        packet = default;
        if (_socket == null) return false;

        try
        {
            if (!_socket.Poll(0, SelectMode.SelectRead))
                return false;

            int received = _socket.ReceiveFrom(_receiveBuffer, ref _senderEndPoint);
            if (received <= 0) return false;

            byte[] data = new byte[received];
            Buffer.BlockCopy(_receiveBuffer, 0, data, 0, received);

            var ipEp = (IPEndPoint)_senderEndPoint;
            packet = new TransportPacket
            {
                Source = new NetEndpoint(ipEp.Address.ToString(), ipEp.Port),
                Payload = data,
            };
            return true;
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.WouldBlock)
        {
            return false;
        }
    }

    public void Stop()
    {
        _socket?.Close();
        _socket?.Dispose();
        _socket = null;
    }
}