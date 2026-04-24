using Axle.Ecs;
using Axle.Net;
using Axle.Sim;

namespace Axle.Client.System;

internal sealed class ClientNetInputSystem : ISystem
{
    private readonly NetClient _netClient;
    private readonly InputHistory _history;
    private ushort _seq;

    public ushort LastSentSeq => (ushort)(_seq - 1);

    public ClientNetInputSystem(NetClient netClient, InputHistory history)
    {
        _netClient = netClient;
        _history = history;
    }

    public void Run(World world)
    {
        foreach (var item in world.Query<LocalPlayer, MoveInput>())
        {
            MoveInput move = item.Component2;

            ushort buttons = 0;
            if (move.X == -1) buttons |= (ushort)InputButtons.Left;
            if (move.X ==  1) buttons |= (ushort)InputButtons.Right;
            if (move.Y == -1) buttons |= (ushort)InputButtons.Up;
            if (move.Y ==  1) buttons |= (ushort)InputButtons.Down;

            var state = new InputState { Seq = _seq, Buttons = buttons };
            _netClient.SendInput(state);
            _history.Record(_seq, move);
            _seq++;

            break; 
        }
    }
}
