﻿using MorMorAdapter.Enumerates;
using MorMorAdapter.Model.Action;
using MorMorAdapter.Model.Action.Receive;
using MorMorAdapter.Model.Action.Response;
using MorMorAdapter.Net;
using Terraria;
using ProtoBuf;
using Terraria.IO;
using MorMorAdapter.Model.Internet;
using TShockAPI;
using TShockAPI.DB;
using TerrariaApi.Server;

namespace MorMorAdapter;

public class ActionHandler
{
    public delegate void ActionHandlerArgs(BaseAction action, MemoryStream stream);

    private static readonly Dictionary<ActionType, ActionHandlerArgs> _action = new()
    {
        { ActionType.DeadRank , DeadRankHandler },
        { ActionType.OnlineRank , OnlineRankHandler },
        { ActionType.WorldMap , WorldMapHandler },
        { ActionType.GameProgress , GameProgressHandler },
        { ActionType.UpLoadWorld , UploadWorldHandler },
        { ActionType.Inventory , InventoryHandler },
        { ActionType.RestServer , RestServerHandler },
        { ActionType.ServerOnline , ServerOnlineHandler },
        { ActionType.RegisterAccount , RegisterAccountHandler },
        { ActionType.PluginMsg , PluginMsgHandler },
        { ActionType.PrivateMsg , PrivateMsgHandler },
        { ActionType.Command , CommandHandler },
        { ActionType.ReStartServer , ReStartServerHandler },
        { ActionType.ServerStatus , ServerStatusHandler }
    };

    public static void Adapter(BaseAction action, MemoryStream stream)
    { 
        stream.Position = 0;
        if(_action.TryGetValue(action.ActionType, out var Handler))
            Handler(action, stream);
    }

    private static void ServerStatusHandler(BaseAction action, MemoryStream stream)
    {
        var res = new ServerStatus()
        {
            Status = true,
            Message = "获取成功",
            Echo = action.Echo,
            WorldName = Main.worldName,
            WorldID = Main.worldID,
            WorldMode = Main.GameMode,
            WorldSeed = WorldGen.currentWorldSeed,
            Plugins = ServerApi.Plugins.Select(x => new PluginInfo()
            {
                Name = x.Plugin.Name,
                Author = x.Plugin.Author,
                Description = x.Plugin.Description,
            }).ToList(),
            WorldHeight = Main.maxTilesY,
            WorldWidth = Main.maxTilesX,
            RunTime = (DateTime.Now - System.Diagnostics.Process.GetCurrentProcess().StartTime)
        };
        WebSocketReceive.SendMessage(Utils.SerializeObj(res));
    }

    private static void ReStartServerHandler(BaseAction action, MemoryStream stream)
    {
        var data = Serializer.Deserialize<ReStartServerArgs>(stream);
        var res = new BaseActionResponse()
        {
            Status = true,
            Message = "正在进行重启",
            Echo = action.Echo
        };
        WebSocketReceive.SendMessage(Utils.SerializeObj(res));
        Utils.ReStarServer(data.StartArgs, true);
    }

    private static void CommandHandler(BaseAction action, MemoryStream stream)
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
    }

    private static void PrivateMsgHandler(BaseAction action, MemoryStream stream)
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
    }

    private static void PluginMsgHandler(BaseAction action, MemoryStream stream)
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
    }

    private static void RegisterAccountHandler(BaseAction action, MemoryStream stream)
    {
        var res = new BaseActionResponse()
        {
            Echo = action.Echo
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
    }

    private static void ServerOnlineHandler(BaseAction action, MemoryStream stream)
    {
        var players = TShock.Players.Where(x => x != null && x.Active).Select(x => new PlayerInfo(x)).ToList();
        var res = new ServerOnline(players)
        {
            Status = true,
            Message = "查询成功",
            Echo = action.Echo
        };
        WebSocketReceive.SendMessage(Utils.SerializeObj(res));
    }

    private static void RestServerHandler(BaseAction action, MemoryStream stream)
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
    }

    private static void InventoryHandler(BaseAction action, MemoryStream stream)
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
    }

    private static void UploadWorldHandler(BaseAction action, MemoryStream stream)
    {
        WorldFile.SaveWorld();
        var buffer = File.ReadAllBytes(Main.worldPathName);
        var res = new UpLoadWorldFile()
        {
            Status = true,
            Message = "成功",
            Echo = action.Echo,
            WorldBuffer = buffer,
            WorldName = Main.worldName
        };
        WebSocketReceive.SendMessage(Utils.SerializeObj(res));
    }

    private static void GameProgressHandler(BaseAction action, MemoryStream stream)
    {
        var res = new GameProgress(Utils.GetGameProgress())
        {
            Status = true,
            Message = "进度查询成功",
            Echo = action.Echo
        };
        WebSocketReceive.SendMessage(Utils.SerializeObj(res));
    }

    private static void WorldMapHandler(BaseAction action, MemoryStream stream)
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
    }

    private static void OnlineRankHandler(BaseAction action, MemoryStream stream)
    {
        var res = new PlayerOnlineRank(Plugin.Onlines)
        {
            Status = true,
            Message = "在线排行查询成功",
            Echo = action.Echo
        };
        WebSocketReceive.SendMessage(Utils.SerializeObj(res));
    }

    private static void DeadRankHandler(BaseAction action, MemoryStream stream)
    {
        var res = new DeadRank(Plugin.Deaths)
        {
            Status = true,
            Message = "死亡排行查询成功",
            Echo = action.Echo
        };
        WebSocketReceive.SendMessage(Utils.SerializeObj(res));
    }
}