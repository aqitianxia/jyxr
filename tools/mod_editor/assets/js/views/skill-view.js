import { SkillSchema } from '../schemas/skill.schema.js';

export class SkillView {
  constructor(adapter, refIndex, onSaveNotification) {
    this.adapter = adapter;
    this.refIndex = refIndex;
    this.notify = onSaveNotification;
    this.skills = [];
    this.selectedSkill = null;
    this.searchKeyword = '';
    this.filterType = 'all';
    this.container = null;
  }

  render(parentEl) {
    this.container = parentEl;
    this.skills = this.adapter.loadExternalSkills();
    if (this.skills.length > 0 && !this.selectedSkill) {
      this.selectedSkill = this.skills[0];
    }
    this._renderLayout();
  }

  _getFilteredSkills() {
    return this.skills.filter(s => {
      const matchKey = !this.searchKeyword || 
        (s.name && s.name.includes(this.searchKeyword)) ||
        (s.id && s.id.includes(this.searchKeyword));
      const matchType = this.filterType === 'all' || s.type === this.filterType;
      return matchKey && matchType;
    });
  }

  _renderLayout() {
    this.container.innerHTML = `
      <div class="view-split-layout">
        <!-- 左侧武学列表 -->
        <div class="sidebar-panel">
          <div class="sidebar-header">
            <input type="text" id="skillSearch" class="search-input" placeholder="🔍 搜索武学名称或ID..." value="${this.searchKeyword}">
            <div class="filter-row">
              <button class="filter-chip ${this.filterType === 'all' ? 'active' : ''}" data-type="all">全部</button>
              <button class="filter-chip ${this.filterType === 'jianfa' ? 'active' : ''}" data-type="jianfa">剑法</button>
              <button class="filter-chip ${this.filterType === 'daofa' ? 'active' : ''}" data-type="daofa">刀法</button>
              <button class="filter-chip ${this.filterType === 'quanzhang' ? 'active' : ''}" data-type="quanzhang">拳掌</button>
            </div>
            <button id="addSkillBtn" class="btn btn-primary" style="width: 100%; margin-top: 8px;">+ 新建自创武功</button>
          </div>
          <div class="item-list-scroll" id="skillList">
            ${this._renderSkillListHtml()}
          </div>
        </div>

        <!-- 右侧武学与招式表单 -->
        <div class="detail-panel" id="skillDetail">
          ${this.selectedSkill ? this._renderDetailHtml(this.selectedSkill) : '<div class="empty-hint">请选择一门武学进行研习配置</div>'}
        </div>
      </div>
    `;

    this._bindEvents();
  }

  _renderSkillListHtml() {
    const list = this._getFilteredSkills();
    if (list.length === 0) return '<div class="empty-list">未找到匹配的武学</div>';
    return list.map(s => {
      const isSelected = this.selectedSkill && this.selectedSkill.id === s.id;
      const typeLabel = (SkillSchema.skillTypes.find(t => t.value === s.type) || {}).label || s.type;
      return `
        <div class="list-item-card ${isSelected ? 'active' : ''}" data-id="${s.id}">
          <div class="item-title-row">
            <span class="item-name text-blue">${s.name || s.id}</span>
            <span class="item-badge badge-blue">${typeLabel}</span>
          </div>
          <div class="item-meta-row">
            <span class="meta-id">威力基数: ${s.powerBase || 1}</span>
            <span class="meta-level">招式数: ${(s.formSkills || []).length}</span>
          </div>
        </div>
      `;
    }).join('');
  }

  _renderDetailHtml(skill) {
    return `
      <div class="form-scroll-wrapper">
        <div class="detail-header-bar">
          <div>
            <h2 class="detail-title text-blue">${skill.name || skill.id}</h2>
            <div class="detail-subtitle">ID: <code>${skill.id}</code> · 类型: ${skill.type}</div>
          </div>
          <div class="btn-group">
            <button id="duplicateSkillBtn" class="btn btn-secondary">克隆武学</button>
            <button id="deleteSkillBtn" class="btn btn-danger">删除</button>
            <button id="saveSkillsBtn" class="btn btn-primary">💾 保存更改</button>
          </div>
        </div>

        <div class="form-grid">
          <!-- 核心属性 -->
          <div class="form-card">
            <h3>武功纲要</h3>
            <div class="form-row">
              <div class="form-group">
                <label>武功名称 (name)</label>
                <input type="text" class="input-field" id="sk_name" value="${skill.name || ''}">
              </div>
              <div class="form-group">
                <label>唯一标识 (id)</label>
                <input type="text" class="input-field" id="sk_id" value="${skill.id || ''}">
              </div>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>兵刃派系 (type)</label>
                <select class="input-field" id="sk_type">
                  ${SkillSchema.skillTypes.map(t => `<option value="${t.value}" ${skill.type === t.value ? 'selected' : ''}>${t.label}</option>`).join('')}
                </select>
              </div>
              <div class="form-group">
                <label>释放音效 (audio)</label>
                <input type="text" class="input-field" id="sk_audio" value="${skill.audio || '音效.剑'}">
              </div>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>基础威力 (powerBase)</label>
                <input type="number" step="0.1" class="input-field" id="sk_powerBase" value="${skill.powerBase || 1}">
              </div>
              <div class="form-group">
                <label>每级威力成长 (powerStep)</label>
                <input type="number" step="0.05" class="input-field" id="sk_powerStep" value="${skill.powerStep || 0.3}">
              </div>
            </div>

            <div class="form-group">
              <label>武功口诀/描述 (description)</label>
              <textarea class="input-field" id="sk_description" rows="3">${skill.description || ''}</textarea>
            </div>
          </div>

          <!-- 招式树 -->
          <div class="form-card">
            <div class="section-title-row">
              <h3>演练招式 (${(skill.formSkills || []).length} 招)</h3>
              <button id="addFormBtn" class="btn btn-sm btn-primary">+ 新增出招式</button>
            </div>

            <div id="formSkillList" class="form-skill-container">
              ${(skill.formSkills || []).map((f, idx) => this._renderFormSkillCard(f, idx)).join('')}
            </div>
          </div>
        </div>
      </div>
    `;
  }

  _renderFormSkillCard(form, idx) {
    const targeting = form.targeting || { impactType: 'line', impactSize: 3, castSize: 1 };
    return `
      <div class="form-sub-card" data-idx="${idx}">
        <div class="sub-card-header">
          <span class="sub-card-title">招式 ${idx + 1}：${form.name || form.id}</span>
          <button class="btn btn-sm btn-danger remove-form-btn" data-idx="${idx}">删除此招</button>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>招式名</label>
            <input type="text" class="input-field form-name-input" value="${form.name || ''}">
          </div>
          <div class="form-group">
            <label>怒气消耗 (rage)</label>
            <input type="number" class="input-field form-rage-input" value="${(form.cost && form.cost.rage) || 2}" min="0">
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>攻击范围模式 (impactType)</label>
            <select class="input-field form-impact-select">
              ${SkillSchema.impactTypes.map(it => `<option value="${it.value}" ${targeting.impactType === it.value ? 'selected' : ''}>${it.label}</option>`).join('')}
            </select>
          </div>
          <div class="form-group">
            <label>范围格数 (impactSize)</label>
            <input type="number" class="input-field form-size-input" value="${targeting.impactSize || 3}" min="1" max="6">
          </div>
        </div>
        <div class="form-group">
          <label>额外伤害倍率 (powerExtra)</label>
          <input type="number" step="0.5" class="input-field form-power-input" value="${form.powerExtra || 1.5}">
        </div>
      </div>
    `;
  }

  _bindEvents() {
    const searchInput = this.container.querySelector('#skillSearch');
    if (searchInput) {
      searchInput.oninput = (e) => {
        this.searchKeyword = e.target.value.trim();
        this.container.querySelector('#skillList').innerHTML = this._renderSkillListHtml();
        this._bindListClickEvents();
      };
    }

    this.container.querySelectorAll('.filter-chip').forEach(btn => {
      btn.onclick = () => {
        this.filterType = btn.dataset.type;
        this._renderLayout();
      };
    });

    const addBtn = this.container.querySelector('#addSkillBtn');
    if (addBtn) {
      addBtn.onclick = () => {
        const newSk = SkillSchema.defaultSkill();
        newSk.id = `skill_${Date.now()}`;
        this.skills.unshift(newSk);
        this.selectedSkill = newSk;
        this._renderLayout();
        this.notify('已创立新武功框架，请在右侧完善招式');
      };
    }

    this._bindListClickEvents();
    this._bindDetailEvents();
  }

  _bindListClickEvents() {
    this.container.querySelectorAll('.list-item-card').forEach(card => {
      card.onclick = () => {
        const id = card.dataset.id;
        this.selectedSkill = this.skills.find(s => s.id === id);
        this._renderLayout();
      };
    });
  }

  _bindDetailEvents() {
    if (!this.selectedSkill) return;

    const bindVal = (id, prop, isNum = false) => {
      const el = this.container.querySelector(`#${id}`);
      if (el) {
        el.oninput = () => {
          this.selectedSkill[prop] = isNum ? Number(el.value) : el.value;
          if (prop === 'name' || prop === 'id') {
            const t = this.container.querySelector(`.list-item-card[data-id="${this.selectedSkill.id}"] .item-name`);
            if (t) t.innerText = this.selectedSkill.name || this.selectedSkill.id;
          }
        };
      }
    };

    bindVal('sk_name', 'name');
    bindVal('sk_id', 'id');
    bindVal('sk_powerBase', 'powerBase', true);
    bindVal('sk_powerStep', 'powerStep', true);
    bindVal('sk_audio', 'audio');
    bindVal('sk_description', 'description');

    const typeSel = this.container.querySelector('#sk_type');
    if (typeSel) {
      typeSel.onchange = () => {
        this.selectedSkill.type = typeSel.value;
      };
    }

    // 新增招式
    const addFormBtn = this.container.querySelector('#addFormBtn');
    if (addFormBtn) {
      addFormBtn.onclick = () => {
        if (!Array.isArray(this.selectedSkill.formSkills)) {
          this.selectedSkill.formSkills = [];
        }
        const fIdx = this.selectedSkill.formSkills.length + 1;
        this.selectedSkill.formSkills.push({
          id: `招式${fIdx}`,
          name: `精妙第${fIdx}招`,
          description: '',
          hard: 1.0,
          cooldown: 3,
          cost: { rage: 2 },
          powerExtra: 2.0,
          animation: 'baozha_cheng',
          audio: '音效.剑',
          unlockLevel: fIdx * 2,
          buffs: [],
          targeting: { castSize: 1, impactType: 'star', impactSize: 3 }
        });
        this._renderLayout();
      };
    }

    // 招式内字段绑定
    this.container.querySelectorAll('.form-sub-card').forEach(card => {
      const idx = Number(card.dataset.idx);
      const form = this.selectedSkill.formSkills[idx];
      if (!form) return;

      const nameInp = card.querySelector('.form-name-input');
      const rageInp = card.querySelector('.form-rage-input');
      const impactSel = card.querySelector('.form-impact-select');
      const sizeInp = card.querySelector('.form-size-input');
      const powerInp = card.querySelector('.form-power-input');

      if (nameInp) nameInp.oninput = () => { form.name = nameInp.value; form.id = nameInp.value; };
      if (rageInp) rageInp.oninput = () => { if (!form.cost) form.cost = {}; form.cost.rage = Number(rageInp.value); };
      if (powerInp) powerInp.oninput = () => { form.powerExtra = Number(powerInp.value); };
      if (impactSel || sizeInp) {
        if (!form.targeting) form.targeting = { castSize: 1, impactType: 'line', impactSize: 3 };
        if (impactSel) impactSel.onchange = () => { form.targeting.impactType = impactSel.value; };
        if (sizeInp) sizeInp.oninput = () => { form.targeting.impactSize = Number(sizeInp.value); };
      }
    });

    // 删除招式
    this.container.querySelectorAll('.remove-form-btn').forEach(btn => {
      btn.onclick = () => {
        const idx = Number(btn.dataset.idx);
        this.selectedSkill.formSkills.splice(idx, 1);
        this._renderLayout();
      };
    });

    // 保存
    const saveBtn = this.container.querySelector('#saveSkillsBtn');
    if (saveBtn) {
      saveBtn.onclick = async () => {
        saveBtn.innerText = '正在保存...';
        const res = await this.adapter.saveExternalSkills(this.skills);
        saveBtn.innerText = '💾 保存更改';
        if (res.success) {
          this.notify(`已成功保存所有武学技能 (共 ${this.skills.length} 门)`);
        } else {
          this.notify(`保存失败: ${res.error || '未知错误'}`, 'error');
        }
      };
    }

    // 删除武功
    const delBtn = this.container.querySelector('#deleteSkillBtn');
    if (delBtn) {
      delBtn.onclick = () => {
        if (confirm(`确定要废除武功【${this.selectedSkill.name || this.selectedSkill.id}】吗？`)) {
          const idx = this.skills.findIndex(s => s.id === this.selectedSkill.id);
          if (idx !== -1) {
            this.skills.splice(idx, 1);
            this.selectedSkill = this.skills[0] || null;
            this._renderLayout();
            this.notify('武学已移除，请点击保存写回磁盘');
          }
        }
      };
    }

    // 克隆
    const dupBtn = this.container.querySelector('#duplicateSkillBtn');
    if (dupBtn) {
      dupBtn.onclick = () => {
        const copy = JSON.parse(JSON.stringify(this.selectedSkill));
        copy.id = `${copy.id}_copy_${Date.now().toString().slice(-4)}`;
        copy.name = `${copy.name || copy.id} (衍化)`;
        this.skills.unshift(copy);
        this.selectedSkill = copy;
        this._renderLayout();
        this.notify(`已成功衍化复制【${copy.name}】`);
      };
    }
  }
}
