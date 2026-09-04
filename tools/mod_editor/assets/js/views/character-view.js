import { CharacterSchema } from '../schemas/character.schema.js';

export class CharacterView {
  constructor(adapter, refIndex, onSaveNotification) {
    this.adapter = adapter;
    this.refIndex = refIndex;
    this.notify = onSaveNotification;
    this.characters = [];
    this.selectedChar = null;
    this.searchKeyword = '';
    this.container = null;
  }

  render(parentEl) {
    this.container = parentEl;
    this.characters = this.adapter.loadCharacters();
    if (this.characters.length > 0 && !this.selectedChar) {
      this.selectedChar = this.characters[0];
    }
    this._renderLayout();
  }

  _getFilteredChars() {
    return this.characters.filter(c => {
      return !this.searchKeyword || 
        (c.name && c.name.includes(this.searchKeyword)) ||
        (c.id && c.id.includes(this.searchKeyword));
    });
  }

  _renderLayout() {
    this.container.innerHTML = `
      <div class="view-split-layout">
        <!-- 左侧人物列表 -->
        <div class="sidebar-panel">
          <div class="sidebar-header">
            <input type="text" id="charSearch" class="search-input" placeholder="🔍 搜索少侠/宗师名或ID..." value="${this.searchKeyword}">
            <button id="addCharBtn" class="btn btn-primary" style="width: 100%; margin-top: 8px;">+ 新建原创侠客</button>
          </div>
          <div class="item-list-scroll" id="charList">
            ${this._renderCharListHtml()}
          </div>
        </div>

        <!-- 右侧人物表单 -->
        <div class="detail-panel" id="charDetail">
          ${this.selectedChar ? this._renderDetailHtml(this.selectedChar) : '<div class="empty-hint">请选择一位侠客进行调校</div>'}
        </div>
      </div>
    `;

    this._bindEvents();
  }

  _renderCharListHtml() {
    const list = this._getFilteredChars();
    if (list.length === 0) return '<div class="empty-list">未找到匹配的侠客</div>';
    return list.map(c => {
      const isSelected = this.selectedChar && this.selectedChar.id === c.id;
      return `
        <div class="list-item-card ${isSelected ? 'active' : ''}" data-id="${c.id}">
          <div class="item-title-row">
            <span class="item-name text-green">${c.name || c.id}</span>
            <span class="item-badge badge-green">Lv.${c.level || 1}</span>
          </div>
          <div class="item-meta-row">
            <span class="meta-id">ID: ${c.id}</span>
            <span class="meta-level">${c.portrait || '无头像'}</span>
          </div>
        </div>
      `;
    }).join('');
  }

  _renderDetailHtml(c) {
    const stats = c.stats || {};
    const portraits = this.refIndex.portraits || [];

    return `
      <div class="form-scroll-wrapper">
        <div class="detail-header-bar">
          <div>
            <h2 class="detail-title text-green">${c.name || c.id}</h2>
            <div class="detail-subtitle">ID: <code>${c.id}</code> · 门派/模板: ${c.growTemplate || '无'}</div>
          </div>
          <div class="btn-group">
            <button id="duplicateCharBtn" class="btn btn-secondary">克隆人物</button>
            <button id="deleteCharBtn" class="btn btn-danger">删除</button>
            <button id="saveCharsBtn" class="btn btn-primary">💾 保存更改</button>
          </div>
        </div>

        <div class="form-grid">
          <!-- 基础与头像 -->
          <div class="form-card">
            <h3>生平与容貌</h3>
            <div class="form-row">
              <div class="form-group">
                <label>侠客姓名 (name)</label>
                <input type="text" class="input-field" id="ch_name" value="${c.name || ''}">
              </div>
              <div class="form-group">
                <label>唯一标识 (id)</label>
                <input type="text" class="input-field" id="ch_id" value="${c.id || ''}">
              </div>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>等级 (level)</label>
                <input type="number" class="input-field" id="ch_level" value="${c.level || 1}" min="1" max="100">
              </div>
              <div class="form-group">
                <label>性别 (gender)</label>
                <select class="input-field" id="ch_gender">
                  <option value="male" ${c.gender === 'male' ? 'selected' : ''}>男儿身</option>
                  <option value="female" ${c.gender === 'female' ? 'selected' : ''}>女儿娇</option>
                </select>
              </div>
            </div>

            <!-- 头像下拉智能选择 -->
            <div class="form-group">
              <label>立绘/头像绑定 (portrait)</label>
              <div class="select-with-preview">
                <select class="input-field" id="ch_portrait">
                  <option value="">-- 选择或输入头像 --</option>
                  ${portraits.map(p => `<option value="${p.id}" ${c.portrait === p.id ? 'selected' : ''}>${p.id} (${p.path})</option>`).join('')}
                </select>
                <input type="text" class="input-field" id="ch_portrait_custom" value="${c.portrait || ''}" placeholder="或直接手工输入头像别名" style="margin-top: 6px;">
              </div>
            </div>

            <div class="form-group">
              <label>成长模板 (growTemplate)</label>
              <input type="text" class="input-field" id="ch_growTemplate" value="${c.growTemplate || '主角'}">
            </div>
          </div>

          <!-- 资质与四维属性数值 -->
          <div class="form-card">
            <h3>武学资质与气血数值</h3>
            <div class="stat-grid-inputs">
              ${CharacterSchema.statFields.map(f => `
                <div class="stat-cell">
                  <label>${f.label}</label>
                  <input type="number" class="input-field stat-input" data-stat="${f.key}" value="${stats[f.key] || 0}" min="0">
                </div>
              `).join('')}
            </div>
          </div>
        </div>
      </div>
    `;
  }

  _bindEvents() {
    const searchInput = this.container.querySelector('#charSearch');
    if (searchInput) {
      searchInput.oninput = (e) => {
        this.searchKeyword = e.target.value.trim();
        this.container.querySelector('#charList').innerHTML = this._renderCharListHtml();
        this._bindListClickEvents();
      };
    }

    const addBtn = this.container.querySelector('#addCharBtn');
    if (addBtn) {
      addBtn.onclick = () => {
        const newChar = CharacterSchema.defaultCharacter();
        newChar.id = `hero_${Date.now()}`;
        this.characters.unshift(newChar);
        this.selectedChar = newChar;
        this._renderLayout();
        this.notify('已创立新侠客名册草稿');
      };
    }

    this._bindListClickEvents();
    this._bindDetailEvents();
  }

  _bindListClickEvents() {
    this.container.querySelectorAll('.list-item-card').forEach(card => {
      card.onclick = () => {
        const id = card.dataset.id;
        this.selectedChar = this.characters.find(c => c.id === id);
        this._renderLayout();
      };
    });
  }

  _bindDetailEvents() {
    if (!this.selectedChar) return;

    const bindVal = (id, prop, isNum = false) => {
      const el = this.container.querySelector(`#${id}`);
      if (el) {
        el.oninput = () => {
          this.selectedChar[prop] = isNum ? Number(el.value) : el.value;
          if (prop === 'name' || prop === 'id') {
            const t = this.container.querySelector(`.list-item-card[data-id="${this.selectedChar.id}"] .item-name`);
            if (t) t.innerText = this.selectedChar.name || this.selectedChar.id;
          }
        };
      }
    };

    bindVal('ch_name', 'name');
    bindVal('ch_id', 'id');
    bindVal('ch_level', 'level', true);
    bindVal('ch_growTemplate', 'growTemplate');

    const genderSel = this.container.querySelector('#ch_gender');
    if (genderSel) {
      genderSel.onchange = () => {
        this.selectedChar.gender = genderSel.value;
      };
    }

    const portraitSel = this.container.querySelector('#ch_portrait');
    const portraitCustom = this.container.querySelector('#ch_portrait_custom');
    if (portraitSel && portraitCustom) {
      portraitSel.onchange = () => {
        if (portraitSel.value) {
          portraitCustom.value = portraitSel.value;
          this.selectedChar.portrait = portraitSel.value;
        }
      };
      portraitCustom.oninput = () => {
        this.selectedChar.portrait = portraitCustom.value.trim();
      };
    }

    // 属性矩阵绑定
    this.container.querySelectorAll('.stat-input').forEach(inp => {
      inp.oninput = () => {
        const k = inp.dataset.stat;
        if (!this.selectedChar.stats) this.selectedChar.stats = {};
        this.selectedChar.stats[k] = Number(inp.value);
      };
    });

    // 保存
    const saveBtn = this.container.querySelector('#saveCharsBtn');
    if (saveBtn) {
      saveBtn.onclick = async () => {
        saveBtn.innerText = '正在保存...';
        const res = await this.adapter.saveCharacters(this.characters);
        saveBtn.innerText = '💾 保存更改';
        if (res.success) {
          this.notify(`已成功保存侠客大名册 (共 ${this.characters.length} 人)`);
        } else {
          this.notify(`保存失败: ${res.error || '未知错误'}`, 'error');
        }
      };
    }

    // 删除
    const delBtn = this.container.querySelector('#deleteCharBtn');
    if (delBtn) {
      delBtn.onclick = () => {
        if (confirm(`确定要移除侠客【${this.selectedChar.name || this.selectedChar.id}】吗？`)) {
          const idx = this.characters.findIndex(c => c.id === this.selectedChar.id);
          if (idx !== -1) {
            this.characters.splice(idx, 1);
            this.selectedChar = this.characters[0] || null;
            this._renderLayout();
            this.notify('侠客已移除');
          }
        }
      };
    }

    // 克隆
    const dupBtn = this.container.querySelector('#duplicateCharBtn');
    if (dupBtn) {
      dupBtn.onclick = () => {
        const copy = JSON.parse(JSON.stringify(this.selectedChar));
        copy.id = `${copy.id}_clone_${Date.now().toString().slice(-4)}`;
        copy.name = `${copy.name || copy.id} (分身)`;
        this.characters.unshift(copy);
        this.selectedChar = copy;
        this._renderLayout();
        this.notify(`已克隆侠客【${copy.name}】`);
      };
    }
  }
}
