/**
 * SkillSchema: 武学与招式数据契约 (解耦层)
 */
export const SkillSchema = {
  kind: 'externalSkill',
  title: '外功武学',
  skillTypes: [
    { value: 'jianfa', label: '剑法 (jianfa)' },
    { value: 'daofa', label: '刀法 (daofa)' },
    { value: 'quanzhang', label: '拳掌 (quanzhang)' },
    { value: 'qimen', label: '奇门兵刃 (qimen)' }
  ],
  impactTypes: [
    { value: 'single', label: '单体目标 (single)' },
    { value: 'line', label: '直线穿透 (line)' },
    { value: 'cross', label: '十字波及 (cross)' },
    { value: 'star', label: '星芒大范围 (star)' }
  ],
  defaultSkill: () => ({
    id: 'my_new_skill',
    name: '自创新剑法',
    description: '这是一门自创的精妙武学。',
    icon: 'icon/icon_waigong_003',
    type: 'jianfa',
    isHarmony: false,
    affinity: 0.0,
    hard: 1.0,
    cooldown: 0,
    powerBase: 5.0,
    powerStep: 0.5,
    animation: 'baozha_cheng',
    audio: '音效.剑',
    buffs: [],
    levelOverrides: [],
    formSkills: [
      {
        id: '起手式',
        name: '起手式',
        description: '基础起手招式',
        hard: 1.0,
        cooldown: 2,
        cost: { rage: 2 },
        powerExtra: 2.0,
        animation: 'baozha_cheng',
        audio: '音效.剑',
        unlockLevel: 1,
        buffs: [],
        targeting: { castSize: 1, impactType: 'line', impactSize: 3 }
      }
    ],
    affixes: [
      { effect: { type: 'stat_modifier', stat: 'jianfa', value: { op: 'add', delta: 10 } }, minimumLevel: 10 }
    ]
  })
};
