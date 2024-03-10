namespace MorMorAdapter.Model;

internal class PlayerData
{
    //在线状态
    public bool OnlineStatu { get; set; }
    //玩家名字
    public string Username { get; set; }
    //最大生命
    public int statLifeMax { get; set; }
    //当前生命
    public int statLife { get; set; }
    //最大法力 
    public int statManaMax { get; set; }
    //当前法力
    public int statMana { get; set; }
    //buff
    public int[] buffType { get; set; }
    //buff时间
    public int[] buffTime { get; set; }
    //背包
    public Item[] inventory { get; set; }
    //宠物坐骑等
    public Item[] miscEquip { get; set; }
    //宠物坐骑染料
    public Item[] miscDye { get; set; }
    //套装
    public Suits[] Loadout { get; set; }
    //垃圾桶
    public Item[] trashItem { get; set; }
    //猪猪存钱罐
    public Item[] Piggiy { get; set; }
    //保险箱
    public Item[] safe { get; set; }
    //护卫熔炉
    public Item[] Forge { get; set; }
    //虚空保险箱
    public Item[] VoidVault { get; set; }
}
