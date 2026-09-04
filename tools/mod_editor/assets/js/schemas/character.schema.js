/**
 * CharacterSchema: 角色人物与宗师契约 (解耦层)
 */
export const CharacterSchema = {
  kind: 'character',
  title: '角色与宗师',
  statFields: [
    { key: 'bili', label: '臂力 (bili)', max: 200 },
    { key: 'dingli', label: '定力 (dingli)', max: 200 },
    { key: 'fuyuan', label: '福缘 (fuyuan)', max: 200 },
    { key: 'gengu', label: '根骨 (gengu)', max: 200 },
    { key: 'shenfa', label: '身法 (shenfa)', max: 200 },
    { key: 'wuxing', label: '悟性 (wuxing)', max: 200 },
    { key: 'jianfa', label: '剑法资质 (jianfa)', max: 200 },
    { key: 'daofa', label: '刀法资质 (daofa)', max: 200 },
    { key: 'quanzhang', label: '拳掌资质 (quanzhang)', max: 200 },
    { key: 'qimen', label: '奇门资质 (qimen)', max: 200 },
    { key: 'wuxue', label: '武学常识 (wuxue)', max: 500 },
    { key: 'max_hp', label: '最大气血 (max_hp)', max: 99999 },
    { key: 'max_mp', label: '最大内力 (max_mp)', max: 99999 }
  ],
  defaultCharacter: () => ({
    id: 'my_hero',
    name: '自创新少侠',
    level: 1,
    portrait: '头像.主角',
    model: 'xiake',
    gender: 'male',
    growTemplate: '主角',
    arenaEnabled: false,
    talentIds: ['异世人'],
    stats: {
      bili: 50,
      dingli: 50,
      fuyuan: 50,
      gengu: 50,
      shenfa: 50,
      wuxing: 50,
      jianfa: 50,
      daofa: 30,
      quanzhang: 30,
      qimen: 30,
      wuxue: 60,
      max_hp: 800,
      max_mp: 600
    },
    specialSkillIds: ['打鸡血'],
    internalSkills: [
      { id: '基本内功', level: 1, maxLevel: 10, equipped: true }
    ],
    equipmentIds: [],
    externalSkills: [
      { id: '野球拳', level: 1, maxLevel: 10 }
    ]
  })
};
