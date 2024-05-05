using MorMorAdapter.DB;
using MorMorAdapter.Enumerates;
using MorMorAdapter.Model.SocketMsg;
using MorMorAdapter.Setting;
using MorMorAdapter.SocketReceive;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Reflection;
using System.Threading.Channels;
using System.Timers;
using Terraria;
using TerrariaApi.Server;
using TShockAPI;

namespace MorMorAdapter;

[ApiVersion(2, 1)]
public class Plugin : TerrariaPlugin
{
    public override string Author => "少司命";

    public override string Description => "适配插件";

    public override string Name => "机器人适配插件";

    public override Version Version => new(1, 0, 0, 0);

    internal static readonly List<TSPlayer> ServerPlayers = new();

    private static SocketClient SocketClient;

    internal static Config Config { get; set; }

    internal static PlayerOnline Onlines { get; set; }

    internal static PlayerDeath Deaths { get; set; }

    private long TimerCount = 0;

    private readonly System.Timers.Timer Timer = new();

    internal static Channel<int> Channeler = Channel.CreateBounded<int>(1);

    public Plugin(Main game) : base(game)
    {
        AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
    }

    public override void Initialize()
    {
        Config = new Config().Read();
        SocketClient = new(IPAddress.Parse(Config.SocketConfig.IP), Config.SocketConfig.Port);
        SocketClient.Start();
        Onlines = new();
        Deaths = new();
        Utils.MapingCommand();
        Utils.MapingRest();
        SocketClient.OnConnect += SkocketConnect;
        SocketClient.OnMessage += SocketClient_OnMessage;
        ServerApi.Hooks.GamePostInitialize.Register(this, OnInit);
        ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
        ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
        ServerApi.Hooks.ServerChat.Register(this, OnChat);
        ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
        GetDataHandlers.KillMe.Register(OnKill);
        TShockAPI.Hooks.GeneralHooks.ReloadEvent += e => Config = Config.Read();
        Utils.HandleCommandLine(Environment.GetCommandLineArgs());
        Timer.AutoReset = true;
        Timer.Enabled = true;
        Timer.Interval = Config.SocketConfig.HeartBeatTimer;
        Timer.Elapsed += (_, _) =>
        {
            SocketClient.SendMessgae(new BaseMessage()
            {
                MessageType = MessageType.HeartBeat
            }.ToJson());
        };
        
    }

    private Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        string resourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.{new AssemblyName(args.Name).Name}.dll";
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
        {
            byte[] assemblyData = new byte[stream.Length];
            stream.Read(assemblyData, 0, assemblyData.Length);
            return Assembly.Load(assemblyData);
        }
    }

    private void SkocketConnect()
    {
        SocketClient.SendMessgae(new BaseMessage() { MessageType = MessageType.Connect }.ToJson());
    }

    private void SocketClient_OnMessage(string e)
    {
        try
        {
            var obj = JObject.Parse(e).ToObject<AcceptMessage>();
            if (obj != null)
            {
                switch (obj.Type)
                {
                    case MessageType.PluginMsg:
                        TShock.Utils.Broadcast(obj.Message, obj.Color[0], obj.Color[1], obj.Color[2]);
                        break;
                    case MessageType.PrivateMsg:
                        TShock.Players.FirstOrDefault(x => x != null && x.Name == obj.Name && x.Active)
                            ?.SendInfoMessage(obj.Message, obj.Color[0], obj.Color[1], obj.Color[2]);
                        break;
                }
            }
        }
        catch
        {
            TShock.Log.ConsoleError($"[SocketClient] 接受到无法解析的字符串:{e}");
        }

    }

    private void OnKill(object? sender, GetDataHandlers.KillMeEventArgs e)
    {
        Deaths.Add(e.Player.Name);
    }

    private void OnUpdate(EventArgs args)
    {
        TimerCount++;
        if (TimerCount % 60 == 0)
        {
            ServerPlayers.ForEach(p =>
            {
                if (p != null && p.Active)
                    Onlines[p.Name] += 1;
            });
        }
    }

    private void OnInit(EventArgs args)
    {
        if (Channeler.Reader.TryRead(out var mode))
        {
            Main.GameMode = mode;
            TSPlayer.All.SendData(PacketTypes.WorldInfo);
        }
        SocketClient.SendMessgae(new GameInitMessage().ToJson());
    }

    private void OnChat(ServerChatEventArgs args)
    {
        var player = TShock.Players[args.Who];
        if (player != null)
        {
            if (args.Text.StartsWith(TShock.Config.Settings.CommandSilentSpecifier)
                || args.Text.StartsWith(TShock.Config.Settings.CommandSpecifier))
            {
                SocketClient.SendMessgae(new PlayerCommandMessage(player, args.Text).ToJson());
            }
            else
            {
                SocketClient.SendMessgae(new PlayerChatMessage(player, args.Text).ToJson());
            }
        }
    }

    private void OnJoin(JoinEventArgs args)
    {
        var player = TShock.Players[args.Who];
        if (player != null)
        {
            if (Config.LimitJoin && TShock.UserAccounts.GetUserAccountByName(player.Name) == null)
            {
                player.Disconnect(Config.DisConnentFormat);
            }
            SocketClient.SendMessgae(new PlayerJoinMessage(player).ToJson());
        }
    }

    private void OnLeave(LeaveEventArgs args)
    {
        var player = TShock.Players[args.Who];
        if (player != null)
        {
            ServerPlayers.Remove(player);
            SocketClient.SendMessgae(new PlayerLeaveMessage(player).ToJson());
        }
        Onlines.UpdateAll();
    }

    private void OnGreet(GreetPlayerEventArgs args)
    {
        var player = TShock.Players[args.Who];
        if (player != null)
        {
            ServerPlayers.Add(player);

        }
    }
}
