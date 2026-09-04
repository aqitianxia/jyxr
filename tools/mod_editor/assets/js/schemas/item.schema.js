/**
 * ItemSchema: 物品与神兵装备数据契约 (解耦层)
 * 
 * 所有的 UI 表单、类型限制、枚举项均由该契约驱动。
 * 若底层 JSON 属性重命名，只需在此处更新映射，表现层无需任何重构。
 */
export const ItemSchema = {
  kind: 'item',
  title: '物品与神兵',
  categories: [
    { value: 'equipment', label: '神兵装备' },
    { value: 'normal', label: '常规道具/药品/秘籍' }
  ],
  types: [
    { value: 'equipment', label: '装备' },
    { value: 'consumable', label: '消耗品' },
    { value: 'skill_book', label: '武学秘籍' }
  ],
  slotTypes: [
    { value: 'weapon', label: '武器' },
    { value: 'armor', label: '防具/重甲' },
    { value: 'accessory', label: '饰品/配饰' }
  ],
  statOptions: [
    { value: 'attack', label: '攻击力 (attack)' },
    { value: 'defense', label: '防御力 (defense)' },
    { value: 'crit_chance', label: '暴击率 (crit_chance)' },
    { value: 'crit_damage', label: '暴击伤害 (crit_damage)' },
    { value: 'speed', label: '身法移动/速度 (speed)' },
    { value: 'max_hp', label: '生命上限 (max_hp)' },
    { value: 'max_mp', label: '内力上限 (max_mp)' },
    { value: 'jianfa', label: '御剑能力 (jianfa)' },
    { value: 'daofa', label: '耍刀能力 (daofa)' },
    { value: 'quanzhang', label: '拳掌能力 (quanzhang)' },
    { value: 'qimen', label: '奇门能力 (qimen)' }
  ],
  defaultItem: () => ({
    category: 'equipment',
    id: 'my_new_weapon',
    name: '自创新武器',
    type: 'equipment',
    consumeOnUse: false,
    level: 1,
    price: 500,
    cooldown: 0,
    canDrop: true,
    description: '一把自创的利器。',
    picture: '物品.阔剑',
    requirements: [],
    useEffects: [],
    slotType: 'weapon',
    affixes: [
      { type: 'stat_modifier', stat: 'attack', value: { op: 'add', delta: 50 } }
    ],
    tagIds: ['weapon']
  })
};
