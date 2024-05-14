using ProtoBuf;
using TShockAPI;

namespace MorMorAdapter.Model.Internet;

[ProtoContract]
public class PlayerInfo
{
    [ProtoMember(1)] public int Index { get; set; }

    [ProtoMember(2)] public string Name { get; set; }

    [ProtoMember(3)] public string Group { get; set; }

    [ProtoMember(4)] public string Prefix { get; set; }

    [ProtoMember(5)] public bool IsLogin { get; set; }

    public PlayerInfo(TSPlayer ply)
    {
        Index = ply.Index;
        Name = ply.Name;
        Group = ply.Group.Name;
        Prefix = ply.Group.Prefix;
        IsLogin = ply.IsLoggedIn;
    }
}
