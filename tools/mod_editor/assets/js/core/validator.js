/**
 * ModValidator: MOD 数据健康检查与悬空引用排查器
 */
export class ModValidator {
  constructor(fs, refIndex) {
    this.fs = fs;
    this.refIndex = refIndex;
  }

  runFullAudit() {
    const issues = []; // { level: 'error'|'warn', module: string, title: string, message: string }

    const portraitMap = new Set(this.refIndex.portraits.map(p => p.id));
    const skillMap = new Set(this.refIndex.skills.map(s => s.id));
    const itemMap = new Set(this.refIndex.items.map(i => i.id));
    const talentMap = new Set(this.refIndex.talents.map(t => t.id));
    const segmentMap = new Set(this.refIndex.storySegments.map(s => s.segmentName));

    // 1. 检查角色 (characters.json)
    const characters = this.fs.readJson('characters.json') || [];
    for (const char of characters) {
      if (!char.id) {
        issues.push({ level: 'error', module: '角色', title: '缺少 ID', message: '发现没有 ID 的角色对象。' });
        continue;
      }

      // 检查头像引用
      if (char.portrait && !portraitMap.has(char.portrait)) {
        issues.push({
          level: 'warn',
          module: '角色',
          title: `角色【${char.name || char.id}】头像悬空`,
          message: `引用的头像别名 '${char.portrait}' 未在 resources.json 中注册。`
        });
      }

      // 检查武功引用
      if (Array.isArray(char.externalSkills)) {
        for (const es of char.externalSkills) {
          if (es && es.id && !skillMap.has(es.id)) {
            issues.push({
              level: 'error',
              module: '角色',
              title: `角色【${char.name || char.id}】外功缺失`,
              message: `初始外功 '${es.id}' 未在 external-skills.json 中定义。`
            });
          }
        }
      }

      if (Array.isArray(char.internalSkills)) {
        for (const is of char.internalSkills) {
          if (is && is.id && !skillMap.has(is.id)) {
            issues.push({
              level: 'error',
              module: '角色',
              title: `角色【${char.name || char.id}】内功缺失`,
              message: `初始内功 '${is.id}' 未在 internal-skills.json 中定义。`
            });
          }
        }
      }

      // 检查天赋引用
      if (Array.isArray(char.talentIds)) {
        for (const tid of char.talentIds) {
          if (tid && !talentMap.has(tid)) {
            issues.push({
              level: 'warn',
              module: '角色',
              title: `角色【${char.name || char.id}】天赋不存在`,
              message: `天赋 '${tid}' 未在 talents.json 中找到。`
            });
          }
        }
      }
    }

    // 2. 检查装备词条 (items.json)
    const items = this.fs.readJson('items.json') || [];
    for (const item of items) {
      if (Array.isArray(item.affixes)) {
        for (const aff of item.affixes) {
          if (aff && aff.type === 'grant_talent' && aff.talentId) {
            if (!talentMap.has(aff.talentId)) {
              issues.push({
                level: 'warn',
                module: '装备',
                title: `装备【${item.name || item.id}】词条天赋缺失`,
                message: `装备词条附赠的天赋 '${aff.talentId}' 未在 talents.json 中定义。`
              });
            }
          }
        }
      }
    }

    // 3. 检查剧情对白与命令 (stories/main.story.json)
    const mainStory = this.fs.readJson('stories/main.story.json');
    if (mainStory && Array.isArray(mainStory.segments)) {
      for (const seg of mainStory.segments) {
        if (!seg || !Array.isArray(seg.steps)) continue;
        for (const step of seg.steps) {
          // 检查 jump 段落
          if (step.kind === 'jump' && step.target) {
            if (!segmentMap.has(step.target)) {
              issues.push({
                level: 'error',
                module: '剧情',
                title: `段落【${seg.name}】跳转目标缺失`,
                message: `jump 目标段落 '${step.target}' 不存在。`
              });
            }
          }
          // 检查 item 命令
          if (step.kind === 'command' && typeof step.call === 'string') {
            const match = step.call.match(/item\(\s*['"]([^'"]+)['"]/);
            if (match && match[1]) {
              const itemId = match[1];
              if (!itemMap.has(itemId)) {
                issues.push({
                  level: 'warn',
                  module: '剧情',
                  title: `段落【${seg.name}】发放物品不存在`,
                  message: `命令 '${step.call}' 中派发的物品 '${itemId}' 在 items.json 中未找到。`
                });
              }
            }
          }
        }
      }
    }

    return issues;
  }
}
