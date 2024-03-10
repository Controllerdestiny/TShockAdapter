using MorMorAdapter.Enumerates;
using TShockAPI;

namespace MorMorAdapter.Model.SocketMsg;

internal class PlayerLeaveMessage : PlayerMessage
{
    public PlayerLeaveMessage(TSPlayer player) : base(player)
    {
        MessageType = MessageType.PlayerLeave;
    }
}
