# TShockAdapter

## 适配于[MorMor](https://github.com/dalaoshus/MorMor)机器人的 TShock 插件仓库

### 该插件实现了 TShock 没有的启动参数

- -seed (设置创建世界时的世界种子)
- -mode (设置世界难度)

## 配置说明

> 配置文件名称 MorMorAdapter.json

```json
{
  "阻止未注册进入": true,
  "阻止语句": "未注禁止进入服务器！",
  "Socket": {
    "套字节地址": "127.0.0.1",
    "服务器名称": "玄荒", //服务器名称 要与MorMor机器人配置中的服务器名称一致
    "端口": 6000,
    "心跳包间隔": 60000,
    "重连间隔": 5000,
    "空指令注册": ["购买", "抽"],
    "验证令牌": "123456" // 通信令牌要与MorMor 机器人 配置中对应服务器的令牌一致
  },
  "重置设置": {
    "删除地图": true,
    "删除日志": true,
    "执行命令": ["/skill reset", "/deal reset", "/礼包 重置", "/level reset"],
    "删除表": [
      "boss数据统计",
      "economics",
      "economicsskill",
      "learnt",
      "OnlineDuration",
      "BotOnlineDuration",
      "BotDeath",
      "onlybaniplist",
      "permabuff",
      "permabuffs",
      "regions",
      "user",
      "Death",
      "rememberedpos",
      "research",
      "stronger",
      "synctable",
      "tscharacter",
      "users",
      "warps",
      "weapons",
      "使用日志"
    ]
  }
}
```
