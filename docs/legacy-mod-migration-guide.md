# 旧 MOD 迁移指南

本文面向从 legacy 版本迁移到当前 MOD 内容格式的作者，记录会改变剧情语义的数据差异和迁移要求。

迁移时应修改源内容，使其直接符合当前数据模型。不要依赖按显示名回退、旧字段别名或运行期兼容逻辑。

## 1. 分离角色实例 ID 与人物 Definition ID

人物实例的 `characterId` 是剧情指令、队伍状态和存档引用角色的稳定身份；`CharacterDefinition.id` 只标识首次创建实例时使用的静态模板；`name` 只用于显示，可以重复，也可能在运行时被改名。

`join`、`follow` 的当前格式为：

```text
join <characterId> [definitionId]
follow <characterId> [definitionId]
```

省略 `definitionId` 时默认与 `characterId` 相同。实例已存在于当前队伍、跟随池或后备池时，指令只移动并复用原实例，不更换其 Definition，也不丢失成长状态。

迁移前，应汇总 `game-config.json` 初始队伍、`join`、`follow`、`join_random` 及 MOD 自定义入队入口。如果多个 Definition 表达同一个可持续成长的剧情人物，只是初始阶段或强度不同，应为它们选择同一个稳定 `characterId`，并仅在首次加入命令中区分模板。

例如：

```json
[
  {"id": "程英.初级", "name": "程英"},
  {"id": "程英.高级", "name": "程英"}
]
```

应迁移为：

```text
join 程英 程英.初级
join 程英 程英.高级
```

地图条件、离队指令、人物成长、武学学习、属性判断和霹雳堂等按人物实例运行的剧情统一引用 `程英`：

```json
{"type": "not_in_team", "value": "程英"}
```

队伍条件只保留 `in_team(characterId)` 和 `not_in_team(characterId)`。legacy 的 `key_in_team`、`key_not_in_team` 应分别迁移为 `in_team`、`not_in_team`，当前运行时不再注册 `key_*` 条件。

如果两个同名 Definition 确实表示需要同时存在、分别成长或分别参与条件判断的角色实例，则继续使用不同的 `characterId`，例如 `袁承志` 与 `儿时袁承志`。不要仅因为显示名相同就合并实例身份。

`definitionId` 只在首次创建时生效。若业务需要把已有角色从一个模板升级或转换为另一个模板，应建立独立的“角色转化/重建”用例，不能让 `join` 隐式完成。低级程英升级为高级模板只是这一通用规则的一个例子。

对话和选项的说话人属于展示解析；同一人物的不同模板共用显示身份时，也应优先写稳定 `characterId`。

## 2. 地图剧情入口与一次性去重

地图事件的入口身份、`story(...)` 的 target 和 `repeatMode == "once"` 的去重身份是三个不同概念。迁移器不能机械地为每条地图事件记录分配彼此独立的 once 去重 ID，也不能仅因多个入口指向同一个 story target，就把所有入口合并成同一种重复模式。

### 同一 story 同时存在 once 与 infinite 入口

同一个 story target 可能同时拥有 once 入口和未声明 `repeatMode` 的 infinite 入口，而且 infinite 入口可能先于 once 入口执行。infinite 入口的执行不能消耗 once 入口的去重状态；入口顺序也不能作为迁移前提。

已知案例：

- 太湖的 `tlbb.dy_阿朱阿碧` 有两个 50% 概率入口。once 入口与 infinite 入口位于同一地点，非 once 入口可能先命中，之后 once 入口仍可能命中；这是确定存在的反序风险。
- 雁门关穆人清的 `bixuejianShyou_闯王剧情2` 同时存在 once 与 infinite 入口，两个入口由不同剧情阶段控制。但这种可能是没问题的。
- 这种组合还可能跨地图出现。`original_新手地图.送镖` 的大地图入口是 once，龙门镖局 NPC 入口是 infinite。正常路线通常先经过大地图 once，因此风险较低；但传送、MOD 修改入口或剧情跳转后仍可能反序触发。

### 多个 once 入口共享 target

多个 once 入口也可能有意共享同一个 story target，并依靠 target 级去重表达“同一剧情可从多个位置触发，但全局只播放一次”。如果迁移后每条入口记录都使用自己的 once 去重 ID，这些剧情会重复播放。

已知案例：

- `碧血剑_穆人清`：2 个 NPC 入口。
- `笑傲江湖_昆仑山雪莲`：4 个山洞入口。
- `original_华山论剑`：华山同一地点的两组条件入口。
- `original_剑魔荒冢独孤求败`：大地图入口和荒冢内 NPC 入口。
- `original_同福客栈`：4 个 NPC 入口。
- `tlbb.dy_西夏驸马.酒楼传闻`：3 家酒楼入口。

因此，迁移后的模型必须显式保留以下语义：地图事件 ID 唯一标识入口记录；story target 标识要执行的剧情；once 去重键允许多个 once 入口共享，但 infinite 入口不参与也不消耗该去重键。若未来允许作者显式配置 once 去重键，未配置时应从旧数据的 target 语义迁移，而不是默认使用入口事件 ID。
