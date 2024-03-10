using MorMorAdapter.Enumerates;
using Newtonsoft.Json;
using TShockAPI;

namespace MorMorAdapter.Model.SocketMsg;

public class PlayerChatMessage : PlayerMessage
{
    [JsonProperty("text")]
    public string Text { get; set; }

    public PlayerChatMessage(TSPlayer player, string text) : base(player)
    {
        MessageType = MessageType.PlayerMessage;
        Text = text;
    }
}
