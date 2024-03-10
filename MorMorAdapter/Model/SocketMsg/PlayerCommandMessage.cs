using MorMorAdapter.Enumerates;
using Newtonsoft.Json;
using TShockAPI;

namespace MorMorAdapter.Model.SocketMsg;

internal class PlayerCommandMessage : PlayerMessage
{
    [JsonProperty("command")]
    public string Command { get; set; }

    public PlayerCommandMessage(TSPlayer player, string command) : base(player)
    {
        Command = command;
        MessageType = MessageType.PlayerCommand;
    }
}
