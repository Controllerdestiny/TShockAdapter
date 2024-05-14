using MorMorAdapter.Setting.Configs;
using Newtonsoft.Json;
using TShockAPI;

namespace MorMorAdapter.Setting;

public class Config
{
    [JsonProperty("阻止未注册进入")]
    public bool LimitJoin { get; set; }

    [JsonProperty("阻止语句")]
    public string DisConnentFormat { get; set; } = "未注禁止进入服务器！";

    [JsonProperty("验证令牌")]
    public string Token { get; set; } = string.Empty;

    [JsonProperty("Socket")]
    public SocketConfig SocketConfig { get; set; } = new();

    [JsonProperty("重置设置")]
    public ResetConfig ResetConfig { get; set; } = new();

    [JsonIgnore]
    public string PATH => Path.Combine(TShock.SavePath, "MorMorAdapter.json");
    public void Write()
    {
        var str = JsonConvert.SerializeObject(this, Formatting.Indented);
        File.WriteAllText(PATH, str);
    }

    public void Write(Config config)
    {
        var str = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(PATH, str);
    }

    public Config Read()
    {
        if (!File.Exists(PATH))
        {
            Write();
            return this;
        }
        var str = File.ReadAllText(PATH);
        var ret = JsonConvert.DeserializeObject<Config>(str) ?? new();
        Write(ret);
        return ret;
    }
}