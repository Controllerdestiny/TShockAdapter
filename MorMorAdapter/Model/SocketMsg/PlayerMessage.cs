using Newtonsoft.Json;
using TShockAPI;

namespace MorMorAdapter.Model.SocketMsg;

public class PlayerMessage : BaseMessage
{
    [JsonProperty("player_name")]
    public string Name { get; set; }

    [JsonProperty("player_group")]
    public string Group { get; set; }

    [JsonProperty("player_prefix")]
    public string Prefix { get; set; }

    [JsonProperty("player_login")]
    public bool IsLogin { get; set; }

    public PlayerMessage(TSPlayer player)
    {
        Name = player.Name;
        Group = player.Group.Name;
        Prefix = player.Group.Prefix;
        IsLogin = player.IsLoggedIn;
    }
}
