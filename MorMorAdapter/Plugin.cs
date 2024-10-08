﻿using MorMorAdapter.DB;
using MorMorAdapter.Enumerates;
using MorMorAdapter.Model;
using MorMorAdapter.Model.Action;
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

    internal static readonly Dictionary<int, KillNpc> DamageBoss = new();

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
        ServerApi.Hooks.GamePostInitialize.Register(this, OnInit);
        ServerApi.Hooks.NetGreetPlayer.Register(this, OnGreet);
        ServerApi.Hooks.ServerLeave.Register(this, OnLeave);
        ServerApi.Hooks.ServerJoin.Register(this, OnJoin);
        ServerApi.Hooks.ServerChat.Register(this, OnChat);
        ServerApi.Hooks.GameUpdate.Register(this, OnUpdate);
        ServerApi.Hooks.NpcSpawn.Register(this, OnSpawn);
        ServerApi.Hooks.NpcStrike.Register(this, OnStrike);
        ServerApi.Hooks.NpcKilled.Register(this, OnKillNpc);
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
        Task.Run(async () => await WebSocketReceive.Start(Config.SocketConfig.IP, Config.SocketConfig.Port));
    }

    private void OnKillNpc(NpcKilledEventArgs args)
    {
        if (args.npc != null && args.npc.active && args.npc.boss)
        {
            if (DamageBoss.TryGetValue(args.npc.netID, out var KillNpc))
            {
                KillNpc.IsAlive = false;
                KillNpc.KillTime = DateTime.Now;
            }
        }
    }

    private void OnStrike(NpcStrikeEventArgs args)
    {
        if (args.Npc != null && args.Npc.active && args.Npc.boss)
        {
            if (DamageBoss.TryGetValue(args.Npc.netID, out var KillNpc) && KillNpc != null)
            {
                var damage = KillNpc.Strikes.Find(x => x.Player == args.Player.name);
                if (damage != null)
                {
                    damage.Damage += args.Damage;
                }
                else
                {
                    KillNpc.Strikes.Add(new()
                    {
                        Player = args.Player.name,
                        Damage = args.Damage
                    });
                }
            }
        }
    }

    private void OnSpawn(NpcSpawnEventArgs args)
    {
        var npc = Main.npc[args.NpcId];
        if (npc != null && npc.active && npc.boss)
        {
            DamageBoss[npc.netID] = new()
            { 
                Id = npc.netID,
                Name = npc.FullName,
                MaxLife = npc.lifeMax
            };
        }
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
        var obj = new BaseMessage()
        {
            MessageType = PostMessageType.Connect,
        };
        var stream = Utils.SerializeObj(obj);
        WebSocketReceive.SendMessage(stream);
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
            TShock.Log.ConsoleError($"[MorMorAdapter] 解析通信时出错: {ex}");
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
                    Mute = player.mute
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
                    Text = args.Text,
                    Mute = player.mute
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
