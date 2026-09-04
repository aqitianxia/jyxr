/**
 * StorySchema: 剧情段落与对白动作契约 (解耦层)
 */
export const StorySchema = {
  kind: 'story',
  title: '剧情剧本',
  stepTypes: [
    { value: 'dialogue', label: '对白 (dialogue)' },
    { value: 'choice', label: '选择题 (choice)' },
    { value: 'command', label: '执行命令 (command)' },
    { value: 'jump', label: '段落跳转 (jump)' }
  ],
  commandTemplates: [
    { label: '发放物品/装备', template: "item('物品名称', 1)" },
    { label: '扣除物品', template: "cost_item('物品名称', 1)" },
    { label: '获得银两', template: "get_money(1000)" },
    { label: '获得元宝', template: "change_yuanbao(10)" },
    { label: '切换背景', template: "background('地图.农舍内')" },
    { label: '播放音乐', template: "music('音乐.室内_清新')" },
    { label: '切换大地图', template: "map('小村')" },
    { label: '触发战斗', template: "battle('战斗ID')" }
  ]
};
