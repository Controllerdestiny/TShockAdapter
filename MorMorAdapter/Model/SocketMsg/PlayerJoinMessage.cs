using MorMorAdapter.Enumerates;
using TShockAPI;

namespace MorMorAdapter.Model.SocketMsg;

internal class PlayerJoinMessage : PlayerMessage
{
    public PlayerJoinMessage(TSPlayer player) : base(player)
    {
        MessageType = MessageType.PlayerJoin;
    }
}
