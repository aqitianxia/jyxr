import { ItemSchema } from '../schemas/item.schema.js';

export class ItemView {
  constructor(adapter, refIndex, onSaveNotification) {
    this.adapter = adapter;
    this.refIndex = refIndex;
    this.notify = onSaveNotification;
    this.items = [];
    this.selectedItem = null;
    this.searchKeyword = '';
    this.filterCategory = 'all';
    this.container = null;
  }

  render(parentEl) {
    this.container = parentEl;
    this.items = this.adapter.loadItems();
    if (this.items.length > 0 && !this.selectedItem) {
      this.selectedItem = this.items[0];
    }
    this._renderLayout();
  }

  _getFilteredItems() {
    return this.items.filter(item => {
      const matchKey = !this.searchKeyword || 
        (item.name && item.name.includes(this.searchKeyword)) ||
        (item.id && item.id.includes(this.searchKeyword));
      const matchCat = this.filterCategory === 'all' || 
        (this.filterCategory === 'equipment' ? item.category === 'equipment' : item.category !== 'equipment');
      return matchKey && matchCat;
    });
  }

  _renderLayout() {
    this.container.innerHTML = `
      <div class="view-split-layout">
        <!-- 左侧导航列表 -->
        <div class="sidebar-panel">
          <div class="sidebar-header">
            <input type="text" id="itemSearch" class="search-input" placeholder="🔍 搜索神兵/物品名称或ID..." value="${this.searchKeyword}">
            <div class="filter-row">
              <button class="filter-chip ${this.filterCategory === 'all' ? 'active' : ''}" data-cat="all">全部 (${this.items.length})</button>
              <button class="filter-chip ${this.filterCategory === 'equipment' ? 'active' : ''}" data-cat="equipment">神兵装备</button>
              <button class="filter-chip ${this.filterCategory === 'normal' ? 'active' : ''}" data-cat="normal">道具/秘籍</button>
            </div>
            <button id="addItemBtn" class="btn btn-primary" style="width: 100%; margin-top: 8px;">+ 新建神兵/物品</button>
          </div>
          <div class="item-list-scroll" id="itemList">
            ${this._renderItemListHtml()}
          </div>
        </div>

        <!-- 右侧属性详情表单 -->
        <div class="detail-panel" id="itemDetail">
          ${this.selectedItem ? this._renderDetailHtml(this.selectedItem) : '<div class="empty-hint">请从左侧选择或新建一件神兵装备</div>'}
        </div>
      </div>
    `;

    this._bindEvents();
  }

  _renderItemListHtml() {
    const list = this._getFilteredItems();
    if (list.length === 0) {
      return '<div class="empty-list">未找到匹配的物品</div>';
    }
    return list.map(item => {
      const isSelected = this.selectedItem && this.selectedItem.id === item.id;
      const isEquip = item.category === 'equipment';
      return `
        <div class="list-item-card ${isSelected ? 'active' : ''}" data-id="${item.id}">
          <div class="item-title-row">
            <span class="item-name ${isEquip ? 'text-gold' : ''}">${item.name || item.id}</span>
            <span class="item-badge ${isEquip ? 'badge-gold' : 'badge-gray'}">${isEquip ? (item.slotType || '装备') : (item.type || '常规')}</span>
          </div>
          <div class="item-meta-row">
            <span class="meta-id">ID: ${item.id}</span>
            <span class="meta-level">Lv.${item.level || 1}</span>
          </div>
        </div>
      `;
    }).join('');
  }

  _renderDetailHtml(item) {
    const isEquip = item.category === 'equipment';
    return `
      <div class="form-scroll-wrapper">
        <div class="detail-header-bar">
          <div>
            <h2 class="detail-title ${isEquip ? 'text-gold' : ''}">${item.name || item.id}</h2>
            <div class="detail-subtitle">ID: <code>${item.id}</code> · 类别: ${item.category}</div>
          </div>
          <div class="btn-group">
            <button id="duplicateItemBtn" class="btn btn-secondary">克隆复制</button>
            <button id="deleteItemBtn" class="btn btn-danger">删除</button>
            <button id="saveItemsBtn" class="btn btn-primary">💾 保存更改</button>
          </div>
        </div>

        <div class="form-grid">
          <div class="form-card">
            <h3>基础信息</h3>
            <div class="form-row">
              <div class="form-group">
                <label>物品名称 (name)</label>
                <input type="text" class="input-field" id="f_name" value="${item.name || ''}">
              </div>
              <div class="form-group">
                <label>唯一标识 (id)</label>
                <input type="text" class="input-field" id="f_id" value="${item.id || ''}">
              </div>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>品级分类 (category)</label>
                <select class="input-field" id="f_category">
                  ${ItemSchema.categories.map(c => `<option value="${c.value}" ${item.category === c.value ? 'selected' : ''}>${c.label}</option>`).join('')}
                </select>
              </div>
              <div class="form-group">
                <label>子类型 (type)</label>
                <select class="input-field" id="f_type">
                  ${ItemSchema.types.map(t => `<option value="${t.value}" ${item.type === t.value ? 'selected' : ''}>${t.label}</option>`).join('')}
                </select>
              </div>
            </div>

            <div class="form-row">
              <div class="form-group">
                <label>品质等阶 (level: 1~10)</label>
                <input type="number" class="input-field" id="f_level" value="${item.level || 1}" min="1" max="10">
              </div>
              <div class="form-group">
                <label>价值银两 (price)</label>
                <input type="number" class="input-field" id="f_price" value="${item.price || 100}" min="0">
              </div>
            </div>

            <div class="form-group">
              <label>图鉴/立绘别名 (picture)</label>
              <input type="text" class="input-field" id="f_picture" value="${item.picture || ''}" placeholder="如：物品.倚天剑">
            </div>

            <div class="form-group">
              <label>描述文案 (description)</label>
              <textarea class="input-field" id="f_description" rows="3">${item.description || ''}</textarea>
            </div>
          </div>

          <!-- 装备特有面板 -->
          <div class="form-card ${isEquip ? '' : 'disabled-card'}">
            <h3>神兵属性与词条 (Affixes)</h3>
            <div class="form-group">
              <label>穿戴部位 (slotType)</label>
              <select class="input-field" id="f_slotType" ${isEquip ? '' : 'disabled'}>
                ${ItemSchema.slotTypes.map(s => `<option value="${s.value}" ${item.slotType === s.value ? 'selected' : ''}>${s.label}</option>`).join('')}
              </select>
            </div>

            <div class="affix-section">
              <div class="section-title-row">
                <label>附加词条列表 (${(item.affixes || []).length})</label>
                <button id="addAffixBtn" class="btn btn-sm btn-secondary" ${isEquip ? '' : 'disabled'}>+ 添加词条</button>
              </div>
              <div id="affixList" class="affix-box-list">
                ${(item.affixes || []).map((aff, index) => this._renderAffixRow(aff, index)).join('')}
              </div>
            </div>
          </div>
        </div>
      </div>
    `;
  }

  _renderAffixRow(aff, index) {
    if (aff.type === 'stat_modifier') {
      return `
        <div class="affix-chip-row" data-index="${index}">
          <span class="affix-tag">属性增益</span>
          <select class="affix-stat-select">
            ${ItemSchema.statOptions.map(o => `<option value="${o.value}" ${aff.stat === o.value ? 'selected' : ''}>${o.label}</option>`).join('')}
          </select>
          <span class="affix-delta-label">数值:</span>
          <input type="number" class="affix-delta-input input-field" value="${(aff.value && aff.value.delta) || 0}">
          <button class="btn btn-sm btn-icon remove-affix-btn" data-index="${index}">✕</button>
        </div>
      `;
    }
    if (aff.type === 'grant_talent') {
      return `
        <div class="affix-chip-row" data-index="${index}">
          <span class="affix-tag text-purple">天赋附带</span>
          <input type="text" class="affix-talent-input input-field" value="${aff.talentId || ''}" placeholder="天赋名称/ID">
          <button class="btn btn-sm btn-icon remove-affix-btn" data-index="${index}">✕</button>
        </div>
      `;
    }
    return `
      <div class="affix-chip-row" data-index="${index}">
        <span class="affix-tag">通用词条</span>
        <code>${JSON.stringify(aff)}</code>
        <button class="btn btn-sm btn-icon remove-affix-btn" data-index="${index}">✕</button>
      </div>
    `;
  }

  _bindEvents() {
    // 搜索
    const searchInput = this.container.querySelector('#itemSearch');
    if (searchInput) {
      searchInput.oninput = (e) => {
        this.searchKeyword = e.target.value.trim();
        this.container.querySelector('#itemList').innerHTML = this._renderItemListHtml();
        this._bindListClickEvents();
      };
    }

    // 分类过滤
    this.container.querySelectorAll('.filter-chip').forEach(btn => {
      btn.onclick = () => {
        this.filterCategory = btn.dataset.cat;
        this._renderLayout();
      };
    });

    // 新增
    const addBtn = this.container.querySelector('#addItemBtn');
    if (addBtn) {
      addBtn.onclick = () => {
        const newItem = ItemSchema.defaultItem();
        newItem.id = `item_${Date.now()}`;
        this.items.unshift(newItem);
        this.selectedItem = newItem;
        this._renderLayout();
        this.notify('已新建一件神兵草稿，请在右侧修改并保存');
      };
    }

    this._bindListClickEvents();
    this._bindDetailFormEvents();
  }

  _bindListClickEvents() {
    this.container.querySelectorAll('.list-item-card').forEach(card => {
      card.onclick = () => {
        const id = card.dataset.id;
        this.selectedItem = this.items.find(i => i.id === id);
        this._renderLayout();
      };
    });
  }

  _bindDetailFormEvents() {
    if (!this.selectedItem) return;

    // 绑定表单改动写回 selectedItem
    const bindVal = (id, prop, isNum = false) => {
      const el = this.container.querySelector(`#${id}`);
      if (el) {
        el.oninput = () => {
          this.selectedItem[prop] = isNum ? Number(el.value) : el.value;
          // 同步左侧列表标题
          if (prop === 'name' || prop === 'id') {
            const cardTitle = this.container.querySelector(`.list-item-card[data-id="${this.selectedItem.id}"] .item-name`);
            if (cardTitle) cardTitle.innerText = this.selectedItem.name || this.selectedItem.id;
          }
        };
      }
    };

    bindVal('f_name', 'name');
    bindVal('f_id', 'id');
    bindVal('f_level', 'level', true);
    bindVal('f_price', 'price', true);
    bindVal('f_picture', 'picture');
    bindVal('f_description', 'description');

    const catEl = this.container.querySelector('#f_category');
    if (catEl) {
      catEl.onchange = () => {
        this.selectedItem.category = catEl.value;
        this._renderLayout();
      };
    }

    const slotEl = this.container.querySelector('#f_slotType');
    if (slotEl) {
      slotEl.onchange = () => {
        this.selectedItem.slotType = slotEl.value;
      };
    }

    // 添加词条
    const addAffixBtn = this.container.querySelector('#addAffixBtn');
    if (addAffixBtn) {
      addAffixBtn.onclick = () => {
        if (!Array.isArray(this.selectedItem.affixes)) {
          this.selectedItem.affixes = [];
        }
        this.selectedItem.affixes.push({
          type: 'stat_modifier',
          stat: 'attack',
          value: { op: 'add', delta: 50 }
        });
        this._renderLayout();
      };
    }

    // 移除词条与修改词条
    this.container.querySelectorAll('.remove-affix-btn').forEach(btn => {
      btn.onclick = () => {
        const idx = Number(btn.dataset.index);
        this.selectedItem.affixes.splice(idx, 1);
        this._renderLayout();
      };
    });

    this.container.querySelectorAll('.affix-chip-row').forEach(row => {
      const idx = Number(row.dataset.index);
      const statSel = row.querySelector('.affix-stat-select');
      const deltaInput = row.querySelector('.affix-delta-input');
      const talentInput = row.querySelector('.affix-talent-input');

      if (statSel && deltaInput) {
        statSel.onchange = () => {
          this.selectedItem.affixes[idx].stat = statSel.value;
        };
        deltaInput.oninput = () => {
          if (!this.selectedItem.affixes[idx].value) this.selectedItem.affixes[idx].value = { op: 'add', delta: 0 };
          this.selectedItem.affixes[idx].value.delta = Number(deltaInput.value);
        };
      }
      if (talentInput) {
        talentInput.oninput = () => {
          this.selectedItem.affixes[idx].talentId = talentInput.value.trim();
        };
      }
    });

    // 保存
    const saveBtn = this.container.querySelector('#saveItemsBtn');
    if (saveBtn) {
      saveBtn.onclick = async () => {
        saveBtn.innerText = '正在保存...';
        const res = await this.adapter.saveItems(this.items);
        saveBtn.innerText = '💾 保存更改';
        if (res.success) {
          this.notify(`已成功保存所有神兵与物品配置 (共 ${this.items.length} 件)`);
        } else {
          this.notify(`保存失败: ${res.error || '未知错误'}`, 'error');
        }
      };
    }

    // 删除
    const delBtn = this.container.querySelector('#deleteItemBtn');
    if (delBtn) {
      delBtn.onclick = () => {
        if (confirm(`确定要删除【${this.selectedItem.name || this.selectedItem.id}】吗？`)) {
          const idx = this.items.findIndex(i => i.id === this.selectedItem.id);
          if (idx !== -1) {
            this.items.splice(idx, 1);
            this.selectedItem = this.items[0] || null;
            this._renderLayout();
            this.notify('物品已删除，请记得点击保存写回磁盘');
          }
        }
      };
    }

    // 克隆
    const dupBtn = this.container.querySelector('#duplicateItemBtn');
    if (dupBtn) {
      dupBtn.onclick = () => {
        const copy = JSON.parse(JSON.stringify(this.selectedItem));
        copy.id = `${copy.id}_copy_${Date.now().toString().slice(-4)}`;
        copy.name = `${copy.name || copy.id} (副本)`;
        this.items.unshift(copy);
        this.selectedItem = copy;
        this._renderLayout();
        this.notify(`已成功复制克隆出【${copy.name}】`);
      };
    }
  }
}
