using MorMorAdapter.DB;
using MorMorAdapter.Enumerates;
using MorMorAdapter.Model;
using MorMorAdapter.Model.Action;
using MorMorAdapter.Model.Action.Receive;
using MorMorAdapter.Model.Action.Response;
using MorMorAdapter.Model.Internet;
using MorMorAdapter.Model.PlayerMessage;
using MorMorAdapter.Model.ServerMessage;
using MorMorAdapter.Net;
using MorMorAdapter.Setting;
using ProtoBuf;
using System.Reflection;
using System.Threading.Channels;
using System.Timers;
using Terraria;
using Terraria.IO;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

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
        TShockAPI.Hooks.GeneralHooks.ReloadEvent += e => Config = Config.Read();
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
        string resourceName = $"{Assembly.GetExecutingAssembly().GetName().Name}.{new AssemblyName(args.Name).Name}.dll";
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
        Console.WriteLine("成功连接到MorMor机器人....");
    }

    private void SocketClient_OnMessage(byte[] buffer)
    {
        try
        {
            using MemoryStream ms = new(buffer);
            var baseMsg = Serializer.Deserialize<BaseAction>(ms);
            if (baseMsg != null && baseMsg.Token == Config.Token)
            {
                switch (baseMsg.MessageType)
                {
                    case PostMessageType.Action:
                        ActionAdapter(baseMsg, ms);
                        break;
                       
                }
            }
        }
        catch(Exception ex)
        {
            TShock.Log.ConsoleError($"[SocketClient] 接受到无法解析的字符串 {ex}");
        }
    }

    private void ActionAdapter(BaseAction baseMsg, MemoryStream stream)
    {
        stream.Position = 0;
        switch(baseMsg.ActionType)
        {
            case ActionType.PluginMsg:
                {
                    var data = Serializer.Deserialize<BroadcastArgs>(stream);
                    TShock.Utils.Broadcast(data.Text, data.Color[0], data.Color[1], data.Color[2]);
                    var res = new BaseActionResponse()
                    {
                        Status = true,
                        Message = "发送成功",
                        Echo = data.Echo
                    };
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    break;
                }
            case ActionType.PrivateMsg:
                {
                    var data = Serializer.Deserialize<PrivatMsgArgs>(stream);
                    TShock.Players.FirstOrDefault(x => x != null && x.Name == data.Name && x.Active)
                        ?.SendMessage(data.Text, data.Color[0], data.Color[1], data.Color[2]);
                    var res = new BaseActionResponse()
                    {
                        Status = true,
                        Message = "发送成功",
                        Echo = data.Echo
                    };
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    break;
                }
            case ActionType.Command:
                {
                    var data = Serializer.Deserialize<ServerCommandArgs>(stream);
                    var player = new OneBotPlayer("MorMorBot");
                    Commands.HandleCommand(player, data.Text);
                    var res = new ServerCommand(player.CommandOutput)
                    {
                        Status = true,
                        Message = "执行成功",
                        Echo = data.Echo
                    };
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    break;
                }
            case ActionType.WorldMap:
                {
                    var data = Serializer.Deserialize<MapImageArgs>(stream);
                    var buffer = Utils.CreateMapBytes(data.ImageType);
                    var res = new MapImage(buffer)
                    {
                        Status = true,
                        Message = "地图生成成功",
                        Echo = data.Echo
                    };
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    break;
                }
            case ActionType.GameProgress:
                {
                    var res = new GameProgress(Utils.GetGameProgress())
                    {
                        Status = true,
                        Message = "进度查询成功",
                        Echo = baseMsg.Echo
                    };
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    break;
                }
            case ActionType.OnlineRank:
                {
                    var res = new PlayerOnlineRank(Onlines)
                    {
                        Status = true,
                        Message = "在线排行查询成功",
                        Echo = baseMsg.Echo
                    };
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    break;
                }

            case ActionType.DeadRank:
                {
                    var res = new DeadRank(Deaths)
                    {
                        Status = true,
                        Message = "死亡排行查询成功",
                        Echo = baseMsg.Echo
                    };
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    break;
                }
            case ActionType.Inventory:
                {
                    var data = Serializer.Deserialize<QueryPlayerInventoryArgs>(stream);
                    var inventory = Utils.BInvSee(data.Name);
                    var res = new PlayerInventory(inventory)
                    {
                        Status = inventory != null,
                        Message = "",
                        Echo = data.Echo
                    };
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    break;
                }
            case ActionType.ServerOnline:
                {
                    var players = TShock.Players.Where(x => x != null && x.Active).Select(x => new PlayerInfo(x)).ToList();
                    var res = new ServerOnline(players)
                    {
                        Status = true,
                        Message = "查询成功",
                        Echo = baseMsg.Echo
                    };
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    break;
                }
            case ActionType.RegisterAccount:
                {
                    var res = new BaseActionResponse()
                    {
                        Echo = baseMsg.Echo
                    };
                    var data = Serializer.Deserialize<RegisterAccountArgs>(stream);
                    try
                    {
                        var account = new UserAccount()
                        {
                            Name = data.Name,
                            Group = data.Group
                        };
                        account.CreateBCryptHash(data.Password);
                        TShock.UserAccounts.AddUserAccount(account);
                        res.Status = true;
                        res.Message = "注册成功";
                    }
                    catch (Exception ex)
                    {
                        res.Status = false;
                        res.Message = ex.Message;
                    }
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    break;
                }
            case ActionType.UpLoadWorld:
                {
                    WorldFile.SaveWorld();
                    var buffer = File.ReadAllBytes(Main.worldPathName);
                    var res = new UpLoadWorldFile()
                    {
                        Status = true,
                        Message = "成功",
                        Echo = baseMsg.Echo,
                        WorldBuffer = buffer,
                        WorldName = Main.worldName
                    };
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    break;
                }
            case ActionType.RestServer:
                {
                    var data = Serializer.Deserialize<RestServerArgs>(stream);
                    var res = new BaseActionResponse()
                    {
                        Status = true,
                        Message = "正在重置",
                        Echo = data.Echo
                    };
                    WebSocketReceive.SendMessage(Utils.SerializeObj(res));
                    Utils.RestServer(data);
                    break;
                }
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

    private void OnLeave(LeaveEventArgs args)
    {
        var player = TShock.Players[args.Who];
        if (player != null)
        {
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
        if (player != null)
        {
            ServerPlayers.Add(player);

        }
    }
}
