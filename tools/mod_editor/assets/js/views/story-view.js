import { StorySchema } from '../schemas/story.schema.js';

export class StoryView {
  constructor(adapter, refIndex, onSaveNotification) {
    this.adapter = adapter;
    this.refIndex = refIndex;
    this.notify = onSaveNotification;
    this.storyDoc = null;
    this.selectedSegment = null;
    this.searchKeyword = '';
    this.container = null;
  }

  render(parentEl) {
    this.container = parentEl;
    this.storyDoc = this.adapter.loadMainStory();
    if (this.storyDoc && Array.isArray(this.storyDoc.segments) && this.storyDoc.segments.length > 0) {
      if (!this.selectedSegment) {
        this.selectedSegment = this.storyDoc.segments[0];
      }
    }
    this._renderLayout();
  }

  _getFilteredSegments() {
    if (!this.storyDoc || !Array.isArray(this.storyDoc.segments)) return [];
    return this.storyDoc.segments.filter(s => {
      return !this.searchKeyword || (s.name && s.name.includes(this.searchKeyword));
    });
  }

  _renderLayout() {
    this.container.innerHTML = `
      <div class="view-split-layout">
        <!-- 左侧段落列表 -->
        <div class="sidebar-panel">
          <div class="sidebar-header">
            <input type="text" id="segSearch" class="search-input" placeholder="🔍 搜索剧情段落名称..." value="${this.searchKeyword}">
            <button id="addSegBtn" class="btn btn-primary" style="width: 100%; margin-top: 8px;">+ 新建故事段落</button>
          </div>
          <div class="item-list-scroll" id="segList">
            ${this._renderSegmentListHtml()}
          </div>
        </div>

        <!-- 右侧对白时间线画布 -->
        <div class="detail-panel" id="storyTimeline">
          ${this.selectedSegment ? this._renderTimelineHtml(this.selectedSegment) : '<div class="empty-hint">请选择一个剧情段落展开对话流</div>'}
        </div>
      </div>
    `;

    this._bindEvents();
  }

  _renderSegmentListHtml() {
    const list = this._getFilteredSegments();
    if (list.length === 0) return '<div class="empty-list">未找到匹配的剧情段落</div>';
    return list.map(s => {
      const isSelected = this.selectedSegment && this.selectedSegment.name === s.name;
      return `
        <div class="list-item-card ${isSelected ? 'active' : ''}" data-name="${s.name}">
          <div class="item-title-row">
            <span class="item-name text-purple">${s.name}</span>
            <span class="item-badge badge-purple">${(s.steps || []).length} 幕</span>
          </div>
        </div>
      `;
    }).join('');
  }

  _renderTimelineHtml(seg) {
    const steps = seg.steps || [];
    return `
      <div class="form-scroll-wrapper">
        <div class="detail-header-bar">
          <div>
            <h2 class="detail-title text-purple">段落：${seg.name}</h2>
            <div class="detail-subtitle">共计 ${steps.length} 幕剧本流</div>
          </div>
          <div class="btn-group">
            <button id="addDialogueBtn" class="btn btn-secondary">+ 加对白</button>
            <button id="addChoiceBtn" class="btn btn-secondary">+ 加选项分支</button>
            <button id="addCommandBtn" class="btn btn-secondary">+ 加命令</button>
            <button id="saveStoryBtn" class="btn btn-primary">💾 保存整个剧本</button>
          </div>
        </div>

        <div class="timeline-container">
          ${steps.map((step, idx) => this._renderStepBubble(step, idx)).join('')}
        </div>
      </div>
    `;
  }

  _renderStepBubble(step, idx) {
    if (step.kind === 'dialogue') {
      const isHero = step.speaker === '主角';
      return `
        <div class="timeline-step-row ${isHero ? 'row-hero' : 'row-npc'}" data-idx="${idx}">
          <div class="dialogue-bubble ${isHero ? 'bubble-hero' : 'bubble-npc'}">
            <div class="bubble-speaker-bar">
              <input type="text" class="bubble-speaker-input" value="${step.speaker || '神秘人'}">
              <div class="bubble-actions">
                <button class="btn-icon step-up-btn" data-idx="${idx}">↑</button>
                <button class="btn-icon step-down-btn" data-idx="${idx}">↓</button>
                <button class="btn-icon step-del-btn" data-idx="${idx}">✕</button>
              </div>
            </div>
            <textarea class="bubble-text-input" rows="2">${step.text || ''}</textarea>
          </div>
        </div>
      `;
    }

    if (step.kind === 'choice') {
      const options = (step.blocks && step.blocks[0] && step.blocks[0].options) || [];
      return `
        <div class="timeline-step-row row-center" data-idx="${idx}">
          <div class="choice-card-box">
            <div class="choice-header">
              <span class="choice-title">🔀 抉择分支 (Choice)</span>
              <div class="bubble-actions">
                <button class="btn-icon step-up-btn" data-idx="${idx}">↑</button>
                <button class="btn-icon step-down-btn" data-idx="${idx}">↓</button>
                <button class="btn-icon step-del-btn" data-idx="${idx}">✕</button>
              </div>
            </div>
            <div class="choice-prompt">
              <label>提问说话人与问题：</label>
              <input type="text" class="choice-speaker-input input-field" value="${(step.prompt && step.prompt.speaker) || ''}" placeholder="说话人">
              <input type="text" class="choice-prompt-input input-field" value="${(step.prompt && step.prompt.text) || ''}" placeholder="选项提示语" style="margin-top:4px;">
            </div>
            <div class="choice-options-list">
              <label>候选分支项 (${options.length})：</label>
              ${options.map((opt, oIdx) => `
                <div class="choice-option-chip">
                  <span>选项 ${oIdx + 1}: ${opt.text}</span>
                  <small>(${ (opt.steps || []).length } 项后续动作)</small>
                </div>
              `).join('')}
            </div>
          </div>
        </div>
      `;
    }

    if (step.kind === 'command') {
      return `
        <div class="timeline-step-row row-center" data-idx="${idx}">
          <div class="command-pill-box">
            <span class="cmd-icon">⚡ 执行命令:</span>
            <input type="text" class="cmd-call-input input-field" value="${step.call || ''}">
            <div class="bubble-actions">
              <button class="btn-icon step-up-btn" data-idx="${idx}">↑</button>
              <button class="btn-icon step-down-btn" data-idx="${idx}">↓</button>
              <button class="btn-icon step-del-btn" data-idx="${idx}">✕</button>
            </div>
          </div>
        </div>
      `;
    }

    if (step.kind === 'jump') {
      return `
        <div class="timeline-step-row row-center" data-idx="${idx}">
          <div class="jump-pill-box">
            <span class="jump-icon">➔ 跳转至段落:</span>
            <input type="text" class="jump-target-input input-field" value="${step.target || ''}">
            <div class="bubble-actions">
              <button class="btn-icon step-up-btn" data-idx="${idx}">↑</button>
              <button class="btn-icon step-down-btn" data-idx="${idx}">↓</button>
              <button class="btn-icon step-del-btn" data-idx="${idx}">✕</button>
            </div>
          </div>
        </div>
      `;
    }

    return `
      <div class="timeline-step-row row-center" data-idx="${idx}">
        <div class="command-pill-box">
          <span>未识别动作 (${step.kind})</span>
          <code>${JSON.stringify(step)}</code>
          <button class="btn-icon step-del-btn" data-idx="${idx}">✕</button>
        </div>
      </div>
    `;
  }

  _bindEvents() {
    const searchInput = this.container.querySelector('#segSearch');
    if (searchInput) {
      searchInput.oninput = (e) => {
        this.searchKeyword = e.target.value.trim();
        this.container.querySelector('#segList').innerHTML = this._renderSegmentListHtml();
        this._bindListClickEvents();
      };
    }

    const addSegBtn = this.container.querySelector('#addSegBtn');
    if (addSegBtn) {
      addSegBtn.onclick = () => {
        const name = prompt('请输入新故事段落名称 (例如：江湖奇遇_天山之战)：');
        if (name && name.trim()) {
          const segName = name.trim();
          const newSeg = {
            name: segName,
            steps: [
              { kind: 'dialogue', speaker: '主角', text: '风萧萧兮易水寒……' }
            ]
          };
          this.storyDoc.segments.unshift(newSeg);
          this.selectedSegment = newSeg;
          this._renderLayout();
          this.notify(`已创建全新故事段落【${segName}】`);
        }
      };
    }

    this._bindListClickEvents();
    this._bindTimelineEvents();
  }

  _bindListClickEvents() {
    this.container.querySelectorAll('.list-item-card').forEach(card => {
      card.onclick = () => {
        const name = card.dataset.name;
        this.selectedSegment = this.storyDoc.segments.find(s => s.name === name);
        this._renderLayout();
      };
    });
  }

  _bindTimelineEvents() {
    if (!this.selectedSegment) return;
    const steps = this.selectedSegment.steps;

    // 对白内容双向绑定
    this.container.querySelectorAll('.timeline-step-row').forEach(row => {
      const idx = Number(row.dataset.idx);
      const step = steps[idx];
      if (!step) return;

      const speakerInp = row.querySelector('.bubble-speaker-input');
      const textInp = row.querySelector('.bubble-text-input');
      const cmdInp = row.querySelector('.cmd-call-input');
      const jumpInp = row.querySelector('.jump-target-input');

      if (speakerInp) speakerInp.oninput = () => { step.speaker = speakerInp.value.trim(); };
      if (textInp) textInp.oninput = () => { step.text = textInp.value; };
      if (cmdInp) cmdInp.oninput = () => { step.call = cmdInp.value.trim(); };
      if (jumpInp) jumpInp.oninput = () => { step.target = jumpInp.value.trim(); };
    });

    // 步骤上移/下移/删除
    this.container.querySelectorAll('.step-up-btn').forEach(btn => {
      btn.onclick = () => {
        const idx = Number(btn.dataset.idx);
        if (idx > 0) {
          const temp = steps[idx];
          steps[idx] = steps[idx - 1];
          steps[idx - 1] = temp;
          this._renderLayout();
        }
      };
    });

    this.container.querySelectorAll('.step-down-btn').forEach(btn => {
      btn.onclick = () => {
        const idx = Number(btn.dataset.idx);
        if (idx < steps.length - 1) {
          const temp = steps[idx];
          steps[idx] = steps[idx + 1];
          steps[idx + 1] = temp;
          this._renderLayout();
        }
      };
    });

    this.container.querySelectorAll('.step-del-btn').forEach(btn => {
      btn.onclick = () => {
        const idx = Number(btn.dataset.idx);
        steps.splice(idx, 1);
        this._renderLayout();
      };
    });

    // 快捷增加对白
    const addDiaBtn = this.container.querySelector('#addDialogueBtn');
    if (addDiaBtn) {
      addDiaBtn.onclick = () => {
        steps.push({
          kind: 'dialogue',
          speaker: '主角',
          text: '新的对话台词内容……'
        });
        this._renderLayout();
      };
    }

    // 快捷增加命令
    const addCmdBtn = this.container.querySelector('#addCommandBtn');
    if (addCmdBtn) {
      addCmdBtn.onclick = () => {
        steps.push({
          kind: 'command',
          call: "item('天机神剑', 1)"
        });
        this._renderLayout();
      };
    }

    // 快捷增加选择题
    const addChoiceBtn = this.container.querySelector('#addChoiceBtn');
    if (addChoiceBtn) {
      addChoiceBtn.onclick = () => {
        steps.push({
          kind: 'choice',
          prompt: { speaker: '世外高人', text: '少年人，何去何从？' },
          blocks: [
            {
              kind: 'options',
              options: [
                { text: '向前行', steps: [{ kind: 'dialogue', speaker: '主角', text: '勇往直前！' }] },
                { text: '暂退避', steps: [{ kind: 'dialogue', speaker: '主角', text: '暂避锋芒。' }] }
              ]
            }
          ]
        });
        this._renderLayout();
      };
    }

    // 保存
    const saveBtn = this.container.querySelector('#saveStoryBtn');
    if (saveBtn) {
      saveBtn.onclick = async () => {
        saveBtn.innerText = '正在写入...';
        const res = await this.adapter.saveMainStory(this.storyDoc);
        saveBtn.innerText = '💾 保存整个剧本';
        if (res.success) {
          this.notify(`已成功保存主线剧情全本 (共 ${this.storyDoc.segments.length} 个段落)`);
        } else {
          this.notify(`保存失败: ${res.error || '未知错误'}`, 'error');
        }
      };
    }
  }
}
