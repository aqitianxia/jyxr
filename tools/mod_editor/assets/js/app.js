import { FileSystemDriver } from './core/file-system.js';
import { ReferenceIndex } from './core/reference.js';
import { ModValidator } from './core/validator.js';
import { JsonDataAdapter } from './adapters/json-adapter.js';

import { ItemView } from './views/item-view.js';
import { SkillView } from './views/skill-view.js';
import { CharacterView } from './views/character-view.js';
import { StoryView } from './views/story-view.js';
import { HealthView } from './views/health-view.js';

class ModStudioApp {
  constructor() {
    this.fs = new FileSystemDriver();
    this.refIndex = new ReferenceIndex();
    this.adapter = new JsonDataAdapter(this.fs);
    this.validator = new ModValidator(this.fs, this.refIndex);

    this.currentTab = 'items';
    this.views = {};
    this.isLoaded = false;
  }

  init() {
    this._bindGlobalEvents();
    this._showWelcome();
  }

  showToast(message, type = 'info') {
    const toast = document.getElementById('toastNotification');
    if (!toast) return;
    toast.innerText = message;
    toast.className = `show toast-${type}`;
    setTimeout(() => {
      toast.className = '';
    }, 3000);
  }

  _bindGlobalEvents() {
    // 目录选择按钮
    const openBtn = document.getElementById('openModDirBtn');
    if (openBtn) {
      openBtn.onclick = async () => {
        openBtn.innerText = '⏳ 正在加载...';
        const res = await this.fs.selectDirectory();
        openBtn.innerText = '📂 切换 MOD 目录';

        if (res.success) {
          document.getElementById('currentModLabel').innerText = `当前项目: ${res.name} (${res.mode === 'native' ? '原生直写' : '内存模式'})`;
          this.refIndex.rebuildIndex(this.fs);
          this.isLoaded = true;

          // 初始化各个子视图
          this.views = {
            items: new ItemView(this.adapter, this.refIndex, (msg) => this.showToast(msg)),
            skills: new SkillView(this.adapter, this.refIndex, (msg) => this.showToast(msg)),
            characters: new CharacterView(this.adapter, this.refIndex, (msg) => this.showToast(msg)),
            stories: new StoryView(this.adapter, this.refIndex, (msg) => this.showToast(msg)),
            health: new HealthView(this.validator, (tab) => this.switchTab(tab))
          };

          this.switchTab(this.currentTab);
          this.showToast(`成功载入 MOD《${res.name}》，已构建全局引用网络！`);
        } else if (!res.cancelled) {
          this.showToast('加载目录失败，请重试', 'error');
        }
      };
    }

    // Tab 导航按钮
    document.querySelectorAll('.nav-tab-btn').forEach(btn => {
      btn.onclick = () => {
        if (!this.isLoaded) {
          this.showToast('请先点击右上角【打开 MOD 目录】选择你的 MOD 文件夹！');
          return;
        }
        const tab = btn.dataset.tab;
        this.switchTab(tab);
      };
    });
  }

  switchTab(tabName) {
    this.currentTab = tabName;
    document.querySelectorAll('.nav-tab-btn').forEach(b => {
      b.classList.toggle('active', b.dataset.tab === tabName);
    });

    const mainContainer = document.getElementById('mainAppContainer');
    if (!this.views[tabName]) return;

    mainContainer.innerHTML = '';
    this.views[tabName].render(mainContainer);
  }

  _showWelcome() {
    const mainContainer = document.getElementById('mainAppContainer');
    mainContainer.innerHTML = `
      <div class="welcome-hero-box">
        <div class="welcome-card">
          <div class="welcome-badge">纯前端 · 零服务依赖 · 本地安全直写</div>
          <h1>《金庸群侠传XR》MOD 创作者工坊</h1>
          <p class="welcome-subtitle">专为重构剧情、自创神兵武功、配置新侠客量身打造的可视化创作中心</p>

          <div class="feature-card-grid">
            <div class="f-card">
              <div class="f-icon">🗡️</div>
              <h4>神兵与道具工坊</h4>
              <p>自由调整攻击力、暴击率，可视化挑选神兵词条与天赋绑定。</p>
            </div>
            <div class="f-card">
              <div class="f-icon">🥋</div>
              <h4>武学与招式工坊</h4>
              <p>直观选择直线、星芒群攻范围，设置怒气消耗与附带 Buff 效果。</p>
            </div>
            <div class="f-card">
              <div class="f-icon">👤</div>
              <h4>侠客与宗师工坊</h4>
              <p>头像下拉直接选定，四维与六维雷达资质参数一键调校。</p>
            </div>
            <div class="f-card">
              <div class="f-icon">📜</div>
              <h4>剧情与对白编排器</h4>
              <p>像聊天气泡一样写剧情，轻松插入选择题分支与神兵奖赏。</p>
            </div>
          </div>

          <div class="welcome-action-box">
            <button class="btn btn-primary btn-lg" onclick="document.getElementById('openModDirBtn').click()">
              📂 点击打开本地 MOD 目录 (例如 mods/wuxia-legend)
            </button>
            <div class="welcome-hint">支持 Chrome / Edge / Safari 等现代浏览器，点击授权后可在网页中直接保存回本地文件</div>
          </div>
        </div>
      </div>
    `;
  }
}

// 启动应用
window.addEventListener('DOMContentLoaded', () => {
  const app = new ModStudioApp();
  app.init();
});
