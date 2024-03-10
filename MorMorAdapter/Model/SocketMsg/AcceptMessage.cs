using MorMorAdapter.Converter;
using MorMorAdapter.Enumerates;
using Newtonsoft.Json;

namespace MorMorAdapter.Model.SocketMsg;

public class AcceptMessage
{
    [JsonProperty("type")]
    [JsonConverter(typeof(MessageTypeConverter))]
    public MessageType Type { get; set; }

    [JsonProperty("text")]
    public string Message { get; set; }

    [JsonProperty("name", DefaultValueHandling = DefaultValueHandling.Ignore)]
    public string Name { get; set; }

    [JsonProperty("color")]
    public byte[] Color { get; set; }
}
