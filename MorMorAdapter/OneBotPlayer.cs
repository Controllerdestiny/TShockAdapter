using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TShockAPI;

namespace MorMorAdapter;

public class OneBotPlayer : TSPlayer
{
    public List<string> CommandOutput = new();

    public OneBotPlayer(string playerName) : base(playerName)
    {
        Account = new() { Name = playerName };
        Group = new SuperAdminGroup();
        AwaitingResponse = new Dictionary<string, Action<object>>();
    }

    public override void SendMessage(string msg, Color color)
    {
        SendMessage(msg, color.R, color.G, color.B);
    }

    public override void SendMessage(string msg, byte red, byte green, byte blue)
    {
        this.CommandOutput.Add(msg);
    }

    public override void SendInfoMessage(string msg)
    {
        SendMessage(msg, Color.Yellow);
    }

    public override void SendSuccessMessage(string msg)
    {
        SendMessage(msg, Color.Green);
    }

    public override void SendWarningMessage(string msg)
    {
        SendMessage(msg, Color.OrangeRed);
    }

    public override void SendErrorMessage(string msg)
    {
        SendMessage(msg, Color.Red);
    }

    public List<string> GetCommandOutput()
    {
        return this.CommandOutput;
    }
}
