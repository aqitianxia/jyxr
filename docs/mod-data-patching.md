# MOD 数据补丁

运行时按主 MOD、依赖 addon、用户排序 addon 的顺序装配内容。每个 MOD 先注册 `data` 中的完整新定义和 `stories/**/*.story.json`，再执行自己的 `patches/**/*.patch.json`。补丁文件按规范化相对路径排序，文件内操作按声明顺序执行。

已有定义不能通过同 ID 的完整 JSON 覆盖；这种情况会中止加载。新增内容继续写入对应的普通数据文件，修改已有内容使用补丁。

## Map 数据目录

Map 定义使用 `data/maps/**/*.json` 目录协议，不读取旧的 `data/maps.json`。主 MOD 必须提供 `data/maps` 目录，目录可以为空；addon 可以省略该目录。

目录会递归扫描，文件按规范化相对路径排序。每个 JSON 文件可以保存单个 Map 对象，也可以保存 Map 数组：

```text
data/maps/large.json
data/maps/small.json
data/maps/sects/wudang.json
```

文件路径只用于内容组织和错误诊断，不参与 Map ID。所有文件中的 `MapDefinition.id` 仍然全局唯一；addon 若要修改已有 Map，必须使用 `map` 补丁目标。

大地图使用固定的 `1920×1080` 逻辑坐标空间，不接受旧的 `800×600` 坐标，也暂不支持自定义逻辑尺寸。地点的 `position` 表示图标下方中点所落在地图上的地理锚点，不是图片左上角；坐标必须落在逻辑范围内。大地图还必须声明正数 `travelSpeed`，表示每个时辰可移动的逻辑距离；小地图必须省略该字段或写为 `0`。移动耗时按 `floor(distance / travelSpeed)` 计算。

```json
{
  "id": "world",
  "name": "World",
  "kind": "large",
  "travelSpeed": 22,
  "locations": [
    {"id": "town", "position": {"x": 1098, "y": 717}, "events": []}
  ]
}
```

剧情脚本使用 `data/stories/**/*.story.json`，目录同样支持任意层级嵌套。脚本 ID 是相对于 `stories` 目录的路径去掉 `.story.json` 后缀；目录名称不进入脚本 ID。

## 文件格式

```json
{
  "format": 2,
  "operations": [
    {
      "op": "merge",
      "target": {"kind": "character", "id": "主角"},
      "value": {
        "name": "新的名字",
        "stats": {"bili": 50}
      }
    }
  ]
}
```

支持以下目标：`battle`、`scopedBattleEffect`、`character`、`externalSkill`、`gameTip`、`growTemplate`、`internalSkill`、`legendSkill`、`map`、`worldTrigger`、`resource`、`sect`、`shop`、`specialSkill`、`item`、`itemTag`、`equipmentRandomAffixTable`、`buff`、`talent`、`tower`、`storySegment` 和 `gameConfig`。

普通目标必须提供稳定 `id`；`storySegment` 的 ID 是 segment 的 `name`；`gameConfig` 不提供 ID。

## 结构化路径

`path` 是从目标根节点开始的数组：

- 字符串表示对象字段。
- `{"id": "..."}` 表示从当前数组中选择具有该 `id` 的元素。
- 省略 `path` 表示操作整个目标。

```json
{
  "op": "merge",
  "target": {"kind": "externalSkill", "id": "玄冥神掌"},
  "path": ["formSkills", {"id": "玄冥神掌.九幽归天"}],
  "value": {"cooldown": 2}
}
```

ID 选择必须恰好找到一个元素；找不到或出现重复 ID 都会中止加载。它适用于任意由带 `id` 对象组成的数组，不需要为具体字段单独登记。无稳定 ID 的数组只能整体设置或使用 `append`、`prepend`。

## 对象和值操作

### merge

递归合并对象。没有声明的字段保持不变；标量、`null` 和数组替换原值。

```json
{
  "op": "merge",
  "target": {"kind": "character", "id": "主角"},
  "value": {
    "name": "新的名字",
    "stats": {"bili": 50}
  }
}
```

### set

设置或整体替换目标节点。最后一个路径段是字段名时允许创建该字段；中间路径必须存在。整体替换实体或带 ID 的数组元素时必须保留原 ID。

```json
{
  "op": "set",
  "target": {"kind": "externalSkill", "id": "玄冥神掌"},
  "path": ["formSkills", {"id": "玄冥神掌.九幽归天"}, "buffs"],
  "value": []
}
```

### remove

删除完整定义、story segment、对象字段或按 ID 选中的数组元素。不能删除 `gameConfig`，也不能删除身份字段。

```json
{
  "op": "remove",
  "target": {"kind": "map", "id": "大地图"},
  "path": ["locations", {"id": "昆仑山"}]
}
```

### test

要求当前节点与 `value` JSON 深度相等，否则中止加载。它适合在依赖基础内容具体原值时显式保护兼容性。

```json
{
  "op": "test",
  "target": {"kind": "character", "id": "主角"},
  "path": ["name"],
  "value": "小虾米"
}
```

`null` 始终表示 JSON null，不表示删除；删除必须使用 `remove`。

## 数组操作

### append 和 prepend

```json
{
  "op": "prepend",
  "target": {"kind": "storySegment", "id": "获得元宝"},
  "path": ["steps"],
  "values": [
    {"kind": "command", "name": "get_money", "args": [100]}
  ]
}
```

如果数组元素包含 `id`，操作完成后会检查 ID 不重复。

### insertBefore 和 insertAfter

在带 ID 的锚点前后插入新元素。新元素必须包含未被占用的 `id`。

```json
{
  "op": "insertAfter",
  "target": {"kind": "tower", "id": "炼狱"},
  "path": ["stages"],
  "anchor": {"id": "已有阶段"},
  "value": {"id": "新增阶段", "name": "新增阶段"}
}
```

### moveBefore 和 moveAfter

移动已有的带 ID 元素：

```json
{
  "op": "moveBefore",
  "target": {"kind": "tower", "id": "炼狱"},
  "path": ["stages"],
  "item": {"id": "要移动的阶段"},
  "anchor": {"id": "目标阶段"}
}
```

数组字面量仍表示整体替换，`[]` 表示清空。

## 冲突与身份约束

- 定义的 `id`、story segment 的 `name` 和按 ID 选中的列表元素 `id` 不能被修改。
- `item.category` 是反序列化判别字段，不能通过 `merge` 修改；需要整体 `set` 该 item。
- 不同 addon 修改同一字段时后加载者生效，同时产生包含双方 MOD 和字段路径的警告。
- 所有操作结束后，合并结果仍会经过完整的强类型反序列化、引用解析和仓储校验。
