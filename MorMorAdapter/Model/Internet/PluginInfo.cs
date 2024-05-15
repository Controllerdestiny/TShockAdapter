using ProtoBuf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MorMorAdapter.Model.Internet;

[ProtoContract]
public class PluginInfo
{
    [ProtoMember(1)] public string Name { get; set; }

    [ProtoMember(2)] public string Author { get; set; }

    [ProtoMember(3)] public string Description { get; set; }
}
