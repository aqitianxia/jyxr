/**
 * JsonDataAdapter: 底层数据格式适配器
 * 
 * 职责：
 * 1. 从 FileSystemDriver 读取原始 JSON，清洗并转换为规范的内存数组；
 * 2. 在用户修改后，将数据模型序列化为与原版完全兼容的标准 JSON 格式；
 * 3. 隔离底层格式差异，使上层 UI 视图与存储细节解耦。
 */
export class JsonDataAdapter {
  constructor(fs) {
    this.fs = fs;
  }

  loadItems() {
    return this.fs.readJson('items.json') || [];
  }

  async saveItems(items) {
    return await this.fs.writeJson('items.json', items);
  }

  loadExternalSkills() {
    return this.fs.readJson('external-skills.json') || [];
  }

  async saveExternalSkills(skills) {
    return await this.fs.writeJson('external-skills.json', skills);
  }

  loadCharacters() {
    return this.fs.readJson('characters.json') || [];
  }

  async saveCharacters(characters) {
    return await this.fs.writeJson('characters.json', characters);
  }

  loadMainStory() {
    return this.fs.readJson('stories/main.story.json') || { version: 3, segments: [] };
  }

  async saveMainStory(storyObj) {
    return await this.fs.writeJson('stories/main.story.json', storyObj);
  }
}
