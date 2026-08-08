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

随机加入写法为 `join_random(['程英', '郭襄'])`，不再注册 `random_join`。候选项的实例 ID 与 Definition ID 必须相同；需要分离两者的角色应使用明确的 `join` 或 `follow`。空列表和任意未知候选都会在消耗随机数前报错。

`definitionId` 只在首次创建时生效。若业务需要把已有角色从一个模板升级或转换为另一个模板，应建立独立的“角色转化/重建”用例，不能让 `join` 隐式完成。低级程英升级为高级模板只是这一通用规则的一个例子。

对话和选项的说话人属于展示解析；同一人物的不同模板共用显示身份时，也应优先写稳定 `characterId`。

## 2. 学习与移除指令

万能指令不再接受分类前置参数，当前签名为：

```text
learn(character_id, target_id, level=1)
remove(character_id, target_id)
```

它们按外功、内功、特技、天赋顺序匹配第一个 Definition。外功和内功使用 level；特技和天赋忽略 level 的具体数值。需要固定分类时使用 `learn_external/internal/special/talent` 及对应 `remove_*`。旧写法 `learn('external', '主角', '野球拳')` 和 `remove('external', '主角', '野球拳')` 不兼容。
