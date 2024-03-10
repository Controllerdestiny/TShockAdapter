using MorMorAdapter.Converter;
using MorMorAdapter.Enumerates;
using Newtonsoft.Json;

namespace MorMorAdapter.Model.SocketMsg;

public class BaseMessage
{
    //消息类型
    [JsonProperty("message_type")]
    [JsonConverter(typeof(MessageTypeConverter))]
    public MessageType MessageType { get; set; }

    [JsonProperty("server_name")]
    public string ServerName { get; set; } = Plugin.Config.SocketConfig.ServerName;

    public string ToJson()
    {
        return JsonConvert.SerializeObject(this, Formatting.Indented);
    }
}
