using MorMorAdapter.Attributes;
using MorMorAdapter.Enumerates;
using MorMorAdapter.Extension;
using MorMorAdapter.Model.Action.Receive;
using MorMorAdapter.Model.Internet;
using ProtoBuf;
using ProtoBuf.Meta;
using Rests;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using System.Diagnostics;
using System.Reflection;
using Terraria;
using Terraria.GameContent.Creative;
using Terraria.IO;
using Terraria.Map;
using TerrariaApi.Server;
using TShockAPI;
using TShockAPI.DB;

namespace MorMorAdapter;

internal class Utils
{


    public static T ReadProtobufItem<T>(MemoryStream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);
        var mode = RuntimeTypeModel.Create();
        mode.Add(typeof(T), true);
        return mode.Deserialize<T>(stream);
    }

    public static MemoryStream SerializeObj<T>(T obj)
    {
        MemoryStream stream = new();
        Serializer.Serialize(stream, obj);
        return stream;
    }
    public static bool SetGameProgress(string type, bool enable)
    {
        var fields = typeof(ProgressType).GetFields().Where(x => x.FieldType == typeof(ProgressType));
        foreach (var field in fields)
        {
            var match = field.GetCustomAttribute<ProgressMatch>();
            if (match?.Name == type)
            {
                var target = match.Type.GetField(match.FieldName);
                if (target != null)
                {
                    target.SetValue(null, enable);
                    return true;
                }
            }
        }
        return false;
    }

    public static Dictionary<string, bool> GetGameProgress()
    {
        var prog = new Dictionary<string, bool>();
        var fields = typeof(ProgressType).GetFields().Where(x => x.FieldType == typeof(ProgressType));
        foreach (var field in fields)
        {
            var match = field.GetCustomAttribute<ProgressMatch>();
            if (match != null)
            {
                var val = match.Type.GetField(match.FieldName)?.GetValue(null);
                if (val != null)
                {
                    prog[match.Name] = Convert.ToBoolean(val);
                }
            }
        }
        return prog;
    }

    /// <summary>
    /// 设置旅途模式的难度
    /// </summary>
    /// <param name="diffName">难度</param>
    /// <returns></returns>
    public static bool SetJourneyDiff(string diffName)
    {
        float diff;
        switch (diffName.ToLower())
        {
            case "master":
                diff = 1f;
                break;
            case "journey":
                diff = 0f;
                break;
            case "normal":
                diff = 0.33f;
                break;
            case "expert":
                diff = 0.66f;
                break;
            default:
                return false;
        }
        var power = CreativePowerManager.Instance.GetPower<CreativePowers.DifficultySliderPower>();
        power._sliderCurrentValueCache = diff;
        power.UpdateInfoFromSliderValueCache();
        power.OnPlayerJoining(0);
        return true;
    }


    /// <summary>
    /// 匹配此程序集的方法
    /// </summary>
    /// <typeparam name="T">返回特性类型</typeparam>
    /// <param name="paramType">参数类型</param>
    /// <returns></returns>
    public static Dictionary<MethodInfo, (object, T)> MatchAssemblyMethodByAttribute<T>(params Type[] paramType) where T : Attribute
    {
        var methods = new Dictionary<MethodInfo, (object, T)>();
        Dictionary<Type, object> types = new();
        Assembly.GetExecutingAssembly().GetTypes().ForEach(x =>
        {
            if (!x.IsAbstract && !x.IsInterface)
            {
                var flag = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public;
                x.GetMethods(flag).ForEach(m =>
                {
                    if (m.ParamsMatch(paramType))
                    {
                        var attribute = m.GetCustomAttribute<T>();
                        if (attribute != null)
                        {
                            if (!m.IsStatic)
                            {
                                var instance = types.TryGetValue(x, out var obj) && obj != null ? obj : Activator.CreateInstance(x);
                                types[x] = instance;
                                var method = instance?.GetType().GetMethod(m.Name, flag);
                                if (method != null)
                                {
                                    methods.Add(method, (instance, attribute));
                                }
                            }
                            else
                            {
                                methods.Add(m, (null, attribute));
                            }
                        }
                    }
                });
            }
        });
        return methods;
    }

    /// <summary>
    /// 加载指令
    /// </summary>
    public static void MapingCommand()
    {
        var methods = MatchAssemblyMethodByAttribute<CommandMatch>(typeof(CommandArgs));
        foreach (var (method, tuple) in methods)
        {
            (object Instance, CommandMatch attr) = tuple;
            Commands.ChatCommands.Add(new(attr.Permission, method.CreateDelegate<CommandDelegate>(Instance), attr.Name));
        }
    }

    /// <summary>
    /// 加载rest API
    /// </summary>
    public static void MapingRest()
    {
        var methods = MatchAssemblyMethodByAttribute<RestMatch>(typeof(RestRequestArgs));
        foreach (var (method, tuple) in methods)
        {
            (object Instance, RestMatch attr) = tuple;
            TShock.RestApi.Register(new(attr.ApiPath, method.CreateDelegate<RestCommandD>(Instance)));
        }
    }

    /// <summary>
    /// 将Terraria Item[] 转为Model.item[]
    /// </summary>
    /// <param name="items">物品数组</param>
    /// <param name="slots">背包格</param>
    /// <returns></returns>
    public static Model.Internet.Item[] GetInventoryData(Terraria.Item[] items, int slots)
    {
        Model.Internet.Item[] info = new Model.Internet.Item[slots];
        for (int i = 0; i < slots; i++)
        {
            info[i] = new Model.Internet.Item(items[i].netID, items[i].prefix, items[i].stack);
        }
        return info;
    }


    public static Model.Internet.PlayerData? BInvSee(string playerName)
    {
        var tsplayer = new Player();
        var players = TSPlayer.FindByNameOrID(playerName);
        if (players.Count != 0)
        {
            tsplayer = players[0].TPlayer;
        }
        else
        {
            var offline = TShock.UserAccounts.GetUserAccountByName(playerName);
            if (offline == null)
            {
                return null;
            }
            playerName = offline.Name;
            var data = TShock.CharacterDB.GetPlayerData(new TSPlayer(-1), offline.ID);
            tsplayer = Utils.ModifyData(playerName, data);
        }
        var retObject = new Model.Internet.PlayerData
        {
            //在线状态
            OnlineStatu = false,
            //玩家名称
            Username = playerName,
            //最大生命
            statLifeMax = tsplayer.statLifeMax,
            //当前生命
            statLife = tsplayer.statLife,
            //最大法力
            statManaMax = tsplayer.statManaMax,
            //当前法力
            statMana = tsplayer.statMana,
            //buff
            buffType = tsplayer.buffType,
            //buff 时间
            buffTime = tsplayer.buffTime,
            //背包
            inventory = Utils.GetInventoryData(tsplayer.inventory, NetItem.InventorySlots),
            //宠物坐骑的染料
            miscDye = Utils.GetInventoryData(tsplayer.miscDyes, NetItem.MiscDyeSlots),
            //宠物坐骑等
            miscEquip = Utils.GetInventoryData(tsplayer.miscEquips, NetItem.MiscEquipSlots),
            //套装
            Loadout = new Suits[tsplayer.Loadouts.Length]
        };
        for (int i = 0; i < tsplayer.Loadouts.Length; i++)
        {
            if (i == tsplayer.CurrentLoadoutIndex)
            {
                retObject.Loadout[i] = new Suits()
                {
                    armor = Utils.GetInventoryData(tsplayer.armor, NetItem.ArmorSlots),
                    dye = Utils.GetInventoryData(tsplayer.dye, NetItem.DyeSlots),
                };
            }
            else
            {
                retObject.Loadout[i] = new Suits()
                {
                    armor = Utils.GetInventoryData(tsplayer.Loadouts[i].Armor, tsplayer.Loadouts[i].Armor.Length),
                    dye = Utils.GetInventoryData(tsplayer.Loadouts[i].Dye, tsplayer.Loadouts[i].Dye.Length)
                };
            }
        }
        //垃圾桶
        retObject.trashItem = new Model.Internet.Item[1]
        {
            new(tsplayer.trashItem.netID, tsplayer.trashItem.prefix, tsplayer.trashItem.stack)
        };
        //猪猪存钱罐
        retObject.Piggiy = GetInventoryData(tsplayer.bank.item, NetItem.PiggySlots);
        //保险箱
        retObject.safe = GetInventoryData(tsplayer.bank2.item, NetItem.SafeSlots);
        //护卫熔炉
        retObject.Forge = GetInventoryData(tsplayer.bank3.item, NetItem.ForgeSlots);
        //虚空保险箱
        retObject.VoidVault = GetInventoryData(tsplayer.bank4.item, NetItem.VoidSlots);

        return retObject;
    }

    /// <summary>
    /// 从数据库加载一个Player
    /// </summary>
    /// <param name="name">玩家名</param>
    /// <param name="data">数据</param>
    /// <returns></returns>
    public static Player ModifyData(string name, TShockAPI.PlayerData data)
    {
        Player player = new();
        if (data != null)
        {
            player.name = name;
            player.SpawnX = data.spawnX;
            player.SpawnY = data.spawnY;

            player.hideVisibleAccessory = data.hideVisuals;
            player.skinVariant = data.skinVariant ?? default;
            player.statLife = data.health;
            player.statLifeMax = data.maxHealth;
            player.statMana = data.mana;
            player.statManaMax = data.maxMana;
            player.extraAccessory = data.extraSlot == 1;

            player.difficulty = (byte)Main.GameModeInfo.Id;

            // 火把神
            player.unlockedBiomeTorches = data.unlockedBiomeTorches == 1;

            player.hairColor = data.hairColor ?? default;
            player.skinColor = data.skinColor ?? default;
            player.eyeColor = data.eyeColor ?? default;
            player.shirtColor = data.shirtColor ?? default;
            player.underShirtColor = data.underShirtColor ?? default;
            player.pantsColor = data.pantsColor ?? default;
            player.shoeColor = data.shoeColor ?? default;
            player.hair = data.hair ?? default;
            player.hairDye = data.hairDye;
            player.anglerQuestsFinished = data.questsCompleted;
            player.CurrentLoadoutIndex = data.currentLoadoutIndex;

            for (int i = 0; i < NetItem.MaxInventory; i++)
            {
                //  0~49 背包   5*10
                //  50、51、52、53 钱
                //  54、55、56、57 弹药
                // 59 ~68  饰品栏
                // 69 ~78  社交栏
                // 79 ~88  染料1
                // 89 ~93  宠物、照明、矿车、坐骑、钩爪
                // 94 ~98  染料2
                // 99~138 储蓄罐
                // 139~178 保险箱（商人）
                // 179 垃圾桶
                // 180~219 护卫熔炉
                // 220~259 虚空保险箱
                // 260~350 装备123
                if (i < 59) player.inventory[i] = NetItem2Item(data.inventory[i]);
                else if (i >= 59 && i < 79) player.armor[i - 59] = NetItem2Item(data.inventory[i]);
                else if (i >= 79 && i < 89) player.dye[i - 79] = NetItem2Item(data.inventory[i]);
                else if (i >= 89 && i < 94) player.miscEquips[i - 89] = NetItem2Item(data.inventory[i]);
                else if (i >= 94 && i < 99) player.miscDyes[i - 94] = NetItem2Item(data.inventory[i]);
                else if (i >= 99 && i < 139) player.bank.item[i - 99] = NetItem2Item(data.inventory[i]);
                else if (i >= 139 && i < 179) player.bank2.item[i - 139] = NetItem2Item(data.inventory[i]);
                else if (i == 179) player.trashItem = NetItem2Item(data.inventory[i]);
                else if (i >= 180 && i < 220) player.bank3.item[i - 180] = NetItem2Item(data.inventory[i]);
                else if (i >= 220 && i < 260) player.bank4.item[i - 220] = NetItem2Item(data.inventory[i]);

                else if (i >= 260 && i < 280) player.Loadouts[0].Armor[i - 260] = NetItem2Item(data.inventory[i]);
                else if (i >= 280 && i < 290) player.Loadouts[0].Dye[i - 280] = NetItem2Item(data.inventory[i]);

                else if (i >= 290 && i < 310) player.Loadouts[1].Armor[i - 290] = NetItem2Item(data.inventory[i]);
                else if (i >= 310 && i < 320) player.Loadouts[1].Dye[i - 310] = NetItem2Item(data.inventory[i]);

                else if (i >= 320 && i < 340) player.Loadouts[2].Armor[i - 320] = NetItem2Item(data.inventory[i]);
                else if (i >= 340 && i < 350) player.Loadouts[2].Dye[i - 340] = NetItem2Item(data.inventory[i]);
            }
        }
        return player;
    }

    public static Terraria.Item NetItem2Item(NetItem netItem)
    {
        var item = new Terraria.Item
        {
            netID = netItem.NetId,
            stack = netItem.Stack,
            prefix = netItem.PrefixId
        };
        return item;
    }
    public static byte[] CreateMapBytes(ImageType type)
    {
        var image = CreateMapImage();
        using var stream = new MemoryStream();
        image.Save(stream, type == ImageType.Png ? new PngEncoder() : new JpegEncoder());
        return stream.ToArray();
    }
    public static SixLabors.ImageSharp.Image CreateMapImage()
    {
        Image<Rgba32> image = new(Main.maxTilesX, Main.maxTilesY);

        MapHelper.Initialize();
        if (Main.Map == null)
        {
            Main.Map = new WorldMap(0, 0);
        }
        for (var x = 0; x < Main.maxTilesX; x++)
        {
            for (var y = 0; y < Main.maxTilesY; y++)
            {
                var tile = MapHelper.CreateMapTile(x, y, byte.MaxValue);
                var col = MapHelper.GetMapTileXnaColor(ref tile);
                image[x, y] = new Rgba32(col.R, col.G, col.B, col.A);
            }
        }

        return image;
    }

    public static void ReStarServer(string startArgs, bool save = false)
    {
        if (save)
        {
            TShock.Utils.StopServer(true);
        }
        else
        {
            Netplay.SaveOnServerExit = false;
            Netplay.Disconnect = true;
        }
        var currentProcess = Process.GetCurrentProcess();
        Process.Start(currentProcess.MainModule!.FileName!, startArgs);
        Environment.Exit(0);
    }

    public static void RestServer(ResetServerArgs args)
    {
        WorldFile.SaveWorld();
        ClearDB();
        foreach (var cmd in Plugin.Config.ResetConfig.Commands)
        {
            Commands.HandleCommand(TSPlayer.Server, cmd);
        }
        //关闭rest
        TShock.RestApi.Stop();
        //关闭Tshock日志
        TShock.Log.Dispose();
        //关闭terrariaApi日志
        var obj = (ServerLogWriter)ServerApi.LogWriter
            .GetType()
            .GetProperty("DefaultLogWriter", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(ServerApi.LogWriter)!;
        obj.Dispose();
        if (Plugin.Config.ResetConfig.ClearLogs)
            new DirectoryInfo(TShock.Config.Settings.LogPath)
                .GetFiles()
                .ForEach(x => x.Delete());

        if (Plugin.Config.ResetConfig.ClearMap && File.Exists(Main.worldPathName))
            File.Delete(Main.worldPathName);
        var dir = Path.GetDirectoryName(Main.worldPathName);
        if (args.UseFile)
        {
            File.WriteAllBytes(Path.Combine(dir, args.FileName), args.FileBuffer);
        }
        ReStarServer(args.StartArgs);
    }

    public static void ClearDB()
    {
        Plugin.Config.ResetConfig.ClearTable.ForEach(x =>
        {
            try
            {
                TShock.DB.Query($"delete from {x}");
            }
            catch { }
        });
    }

    internal static void HandleCommandLine(string[] param)
    {
        Dictionary<string, string> args = Terraria.Utils.ParseArguements(param);
        foreach (var (key, value) in args)
        {
            switch (key.ToLower())
            {
                case "-seed":
                    Main.AutogenSeedName = value;
                    break;
                case "-mode":

                    if (int.TryParse(value, out int mode))
                        Plugin.Channeler.Writer.TryWrite(mode);
                    break;
            }
        }
    }
}
