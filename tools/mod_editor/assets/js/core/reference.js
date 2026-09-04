/**
 * ReferenceIndex: 全局引用关系与 ID 索引网络
 * 
 * 自动从加载的多个数据文件中提取所有的 ID、名称与分类，
 * 供各个工坊在选择头像、装备、武功、天赋、人物时进行下拉智能联想，
 * 避免创作者手工拼写错误。
 */
export class ReferenceIndex {
  constructor() {
    this.portraits = [];    // { id, name, path }
    this.skills = [];       // { id, name, type, category }
    this.items = [];        // { id, name, category, slotType }
    this.talents = [];      // { id, name, description }
    this.characters = [];   // { id, name, portrait }
    this.storySegments = [];// { segmentName, storyFile }
    this.buffs = [];        // { id, name }
  }

  /**
   * 基于 FileSystemDriver 加载的数据构建索引
   */
  rebuildIndex(fs) {
    this.portraits = [];
    this.skills = [];
    this.items = [];
    this.talents = [];
    this.characters = [];
    this.storySegments = [];
    this.buffs = [];

    // 1. 头像资源索引 (resources.json)
    const resData = fs.readJson('resources.json');
    if (Array.isArray(resData)) {
      for (const item of resData) {
        if (item && item.group === '头像') {
          this.portraits.push({
            id: item.id,
            name: item.id.replace(/^头像\./, ''),
            path: item.value
          });
        }
      }
    }

    // 2. 物品/装备索引 (items.json)
    const itemData = fs.readJson('items.json');
    if (Array.isArray(itemData)) {
      for (const item of itemData) {
        if (item && item.id) {
          this.items.push({
            id: item.id,
            name: item.name || item.id,
            category: item.category || 'normal',
            type: item.type || 'normal',
            slotType: item.slotType || ''
          });
        }
      }
    }

    // 3. 武学技能索引 (external-skills.json / internal-skills.json)
    const extSkills = fs.readJson('external-skills.json');
    if (Array.isArray(extSkills)) {
      for (const s of extSkills) {
        if (s && s.id) {
          this.skills.push({
            id: s.id,
            name: s.name || s.id,
            type: s.type || 'quanzhang',
            category: 'external'
          });
        }
      }
    }

    const intSkills = fs.readJson('internal-skills.json');
    if (Array.isArray(intSkills)) {
      for (const s of intSkills) {
        if (s && s.id) {
          this.skills.push({
            id: s.id,
            name: s.name || s.id,
            type: 'neigong',
            category: 'internal'
          });
        }
      }
    }

    // 4. 天赋索引 (talents.json)
    const talentData = fs.readJson('talents.json');
    if (Array.isArray(talentData)) {
      for (const t of talentData) {
        if (t && t.id) {
          this.talents.push({
            id: t.id,
            name: t.name || t.id,
            description: t.description || ''
          });
        }
      }
    }

    // 5. 角色索引 (characters.json)
    const charData = fs.readJson('characters.json');
    if (Array.isArray(charData)) {
      for (const c of charData) {
        if (c && c.id) {
          this.characters.push({
            id: c.id,
            name: c.name || c.id,
            portrait: c.portrait || ''
          });
        }
      }
    }

    // 6. 状态增益索引 (buffs.json)
    const buffData = fs.readJson('buffs.json');
    if (Array.isArray(buffData)) {
      for (const b of buffData) {
        if (b && b.id) {
          this.buffs.push({
            id: b.id,
            name: b.name || b.id
          });
        }
      }
    }

    // 7. 剧情段落索引 (stories/main.story.json 等)
    const mainStory = fs.readJson('stories/main.story.json');
    if (mainStory && Array.isArray(mainStory.segments)) {
      for (const seg of mainStory.segments) {
        if (seg && seg.name) {
          this.storySegments.push({
            segmentName: seg.name,
            storyFile: 'stories/main.story.json'
          });
        }
      }
    }
  }

  getWeapons() {
    return this.items.filter(i => i.slotType === 'weapon' || i.category === 'equipment');
  }

  getExternalSkills() {
    return this.skills.filter(s => s.category === 'external');
  }

  getInternalSkills() {
    return this.skills.filter(s => s.category === 'internal');
  }
}
