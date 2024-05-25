using MorMorAdapter.DB;
using MorMorAdapter.Enumerates;
using MorMorAdapter.Model;
using MorMorAdapter.Model.Action;
using MorMorAdapter.Model.PlayerMessage;
using MorMorAdapter.Model.ServerMessage;
using MorMorAdapter.Net;
using MorMorAdapter.Setting;
using ProtoBuf;
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
        if (!Directory.Exists("world"))
            Directory.CreateDirectory("world");
        Onlines = new();
        Deaths = new();
        Utils.MapingCommand();
        Utils.MapingRest();
        WebSocketReceive.OnConnect += SkocketConnect;
        WebSocketReceive.OnMessage += SocketClient_OnMessage;
        WebSocketReceive.Start(Config.SocketConfig.IP, Config.SocketConfig.Port);
        ServerApi.Hooks.GamePostInitialize.Register(this, OnInit);
        ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
        ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
        ServerApi.Hooks.ServerChat.Register(this, OnChat);
        ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
        GetDataHandlers.KillMe.Register(OnKill);
        Config.SocketConfig.EmptyCommand.ForEach(x =>
        {
            Commands.ChatCommands.Add(new("", (_) => { }, x));
        });
        TShockAPI.Hooks.GeneralHooks.ReloadEvent += e =>
        {
            Config.SocketConfig.EmptyCommand.ForEach(cmd =>
            {
                Commands.ChatCommands.RemoveAll(x => x.Names.Contains(cmd));
            });
            Config = Config.Read();
            Config.SocketConfig.EmptyCommand.ForEach(x =>
            {
                Commands.ChatCommands.Add(new("", (_) => { }, x));
            });
        };
        Utils.HandleCommandLine(Environment.GetCommandLineArgs());
        Timer.AutoReset = true;
        Timer.Enabled = true;
        Timer.Interval = Config.SocketConfig.HeartBeatTimer;
        Timer.Elapsed += (_, _) =>
        {
            var obj = new BaseMessage()
            {
                MessageType = PostMessageType.HeartBeat
            };
            WebSocketReceive.SendMessage(Utils.SerializeObj(obj));
        };
    }

    private Assembly? CurrentDomain_AssemblyResolve(object? sender, ResolveEventArgs args)
    {
        string resourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.lib.{new AssemblyName(args.Name).Name}.dll";
        using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
        {
            if (stream == null)
                throw new NullReferenceException("无法加载程序集:" + args.Name);
            byte[] assemblyData = new byte[stream.Length];
            stream.Read(assemblyData, 0, assemblyData.Length);
            return Assembly.Load(assemblyData);
        }
    }

    private void SkocketConnect()
    {
        var obj = new BaseMessage() { MessageType = PostMessageType.Connect };
        WebSocketReceive.SendMessage(Utils.SerializeObj(obj));
    }

    private void SocketClient_OnMessage(byte[] buffer)
    {
        try
        {
            using MemoryStream ms = new(buffer);
            var baseMsg = Serializer.Deserialize<BaseAction>(ms);
            if (baseMsg.Token == Config.SocketConfig.Token || baseMsg.ActionType == ActionType.ConnectStatus)
            {
                switch (baseMsg.MessageType)
                {
                    case PostMessageType.Action:
                        ActionHandler.Adapter(baseMsg, ms);
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            TShock.Log.ConsoleError($"[MorMorAdapter] 接受到无法解析的字符串， 错误信息: {ex.Message}");
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
        var obj = new GameInitMessage();
        WebSocketReceive.SendMessage(Utils.SerializeObj(obj));
    }

    private void OnChat(ServerChatEventArgs args)
    {
        var player = TShock.Players[args.Who];
        if (player != null)
        {
            if (args.Text.StartsWith(TShock.Config.Settings.CommandSilentSpecifier)
                || args.Text.StartsWith(TShock.Config.Settings.CommandSpecifier))
            {
                var prefix = args.Text.StartsWith(TShock.Config.Settings.CommandSilentSpecifier) ? TShock.Config.Settings.CommandSilentSpecifier : TShock.Config.Settings.CommandSpecifier;
                var obj = new PlayerCommandMessage()
                {
                    MessageType = PostMessageType.PlayerCommand,
                    Name = player.Name,
                    Group = player.Group.Name,
                    Prefix = player.Group.Prefix,
                    IsLogin = player.IsLoggedIn,
                    Command = args.Text,
                    CommandPrefix = prefix,
                };
                WebSocketReceive.SendMessage(Utils.SerializeObj(obj));
            }
            else
            {
                var obj = new PlayerChatMessage()
                {
                    MessageType = PostMessageType.PlayerMessage,
                    Name = player.Name,
                    Group = player.Group.Name,
                    Prefix = player.Group.Prefix,
                    IsLogin = player.IsLoggedIn,
                    Text = args.Text
                };
                var stream = Utils.SerializeObj(obj);
                WebSocketReceive.SendMessage(stream);
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
        }
    }

    private void OnLeave(LeaveEventArgs args)
    {
        var player = TShock.Players[args.Who];
        if (player != null)
        {
            if (!ServerPlayers.Contains(player))
                return;
            ServerPlayers.Remove(player);
            var obj = new PlayerLeaveMessage()
            {
                MessageType = PostMessageType.PlayerLeave,
                Name = player.Name,
                Group = player.Group.Name,
                Prefix = player.Group.Prefix,
                IsLogin = player.IsLoggedIn,
            };
            WebSocketReceive.SendMessage(Utils.SerializeObj(obj));
        }
        Onlines.UpdateAll();
    }

    private void OnGreet(GreetPlayerEventArgs args)
    {
        var player = TShock.Players[args.Who];
        if (player != null && player.Active)
        {
            ServerPlayers.Add(player);
            var obj = new PlayerJoinMessage()
            {
                MessageType = PostMessageType.PlayerJoin,
                Name = player.Name,
                Group = player.Group.Name,
                Prefix = player.Group.Prefix,
                IsLogin = player.IsLoggedIn,
            };
            WebSocketReceive.SendMessage(Utils.SerializeObj(obj));
        }
    }
}
