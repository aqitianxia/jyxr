/**
 * FileSystemDriver: 纯前端文件访问与本地磁盘直写驱动 (首要原则：全浏览器兼容、自适应层级、原生直写与极速预设)
 */
export class FileSystemDriver {
  constructor() {
    this.dirHandle = null;
    this.memoryFiles = new Map();
    this.isNativeAccessSupported = typeof window.showDirectoryPicker === 'function';
    this.currentModName = '';
    this.detectedDataPrefix = '';
    this.mode = 'idle'; // 'native' | 'memory' | 'http' | 'idle'
  }

  // 检测当前运行环境是否支持原生目录直写
  canUseNativePicker() {
    return window.isSecureContext && typeof window.showDirectoryPicker === 'function' && location.protocol !== 'file:';
  }

  async selectDirectory() {
    if (this.canUseNativePicker()) {
      try {
        const handle = await window.showDirectoryPicker({
          id: 'jyxr-mod-dir',
          mode: 'readwrite',
          startIn: 'documents'
        });
        this.dirHandle = handle;
        this.currentModName = handle.name;
        await this._scanNativeDirectory();
        this.mode = 'native';
        return { success: true, mode: 'native', name: this.currentModName };
      } catch (err) {
        if (err.name === 'AbortError') {
          return { success: false, cancelled: true };
        }
        console.warn('Native File System Access failed, fallback to input:', err);
      }
    }

    return await this._selectDirectoryViaInput();
  }

  _selectDirectoryViaInput() {
    return new Promise((resolve) => {
      let input = document.getElementById('directFolderPickerInput');
      if (!input) {
        input = document.createElement('input');
        input.id = 'directFolderPickerInput';
        input.type = 'file';
        input.webkitdirectory = true;
        input.multiple = true;
        // 关键：WebKit (Safari) 禁止对 display:none 的 input 触发 click()，采用透明脱标渲染
        input.style.position = 'fixed';
        input.style.top = '-9999px';
        input.style.left = '-9999px';
        input.style.width = '1px';
        input.style.height = '1px';
        input.style.opacity = '0.001';
        input.style.pointerEvents = 'none';
        input.setAttribute('aria-hidden', 'true');
        input.tabIndex = -1;
        document.body.appendChild(input);
      }

      // 重置 value，确保选择相同目录或重试时能正确触发 onchange
      input.value = '';

      let resolved = false;
      const finish = (result) => {
        if (resolved) return;
        resolved = true;
        window.removeEventListener('focus', onWindowFocus);
        resolve(result);
      };

      input.onchange = async (e) => {
        const files = Array.from(e.target.files || []);
        if (files.length === 0) {
          finish({ success: false, cancelled: true });
          return;
        }

        this.memoryFiles.clear();
        this.dirHandle = null;
        this.mode = 'memory';

        const firstRel = files[0].webkitRelativePath || '';
        const rootFolder = firstRel.split('/')[0] || 'MOD';
        this.currentModName = rootFolder;

        for (const file of files) {
          const lower = file.name.toLowerCase();
          if (!lower.endsWith('.json') && !lower.endsWith('.txt') && !lower.endsWith('.md')) {
            continue;
          }
          const parts = file.webkitRelativePath.split('/');
          parts.shift(); // 移除首层顶级目录名
          const relPath = parts.join('/');
          const text = await file.text();
          this.memoryFiles.set(relPath, text);
        }

        this._detectDataPrefix();
        finish({ success: true, mode: 'memory', name: this.currentModName });
      };

      input.oncancel = () => {
        finish({ success: false, cancelled: true });
      };

      // 针对部分浏览器（如 Safari）不触发 oncancel 的窗口焦点看门狗
      const onWindowFocus = () => {
        setTimeout(() => {
          if (!resolved && (!input.files || input.files.length === 0)) {
            finish({ success: false, cancelled: true });
          }
        }, 800);
      };
      window.addEventListener('focus', onWindowFocus, { once: true });

      input.click();
    });
  }

  async _scanNativeDirectory() {
    this.memoryFiles.clear();
    const scanDir = async (dirHandle, currentPath = '') => {
      for await (const entry of dirHandle.values()) {
        const entryPath = currentPath ? `${currentPath}/${entry.name}` : entry.name;
        if (entry.kind === 'file') {
          const lower = entry.name.toLowerCase();
          if (lower.endsWith('.json') || lower.endsWith('.txt') || lower.endsWith('.md')) {
            const file = await entry.getFile();
            const text = await file.text();
            this.memoryFiles.set(entryPath, text);
          }
        } else if (entry.kind === 'directory') {
          if (entry.name !== '.git' && entry.name !== '.godot' && entry.name !== 'node_modules' && entry.name !== '.gemini') {
            await scanDir(entry, entryPath);
          }
        }
      }
    };
    await scanDir(this.dirHandle);
    this._detectDataPrefix();
  }

  _detectDataPrefix() {
    this.detectedDataPrefix = '';
    for (const key of this.memoryFiles.keys()) {
      if (key === 'characters.json' || key === 'items.json') {
        this.detectedDataPrefix = '';
        return;
      }
      if (key.endsWith('/characters.json')) {
        this.detectedDataPrefix = key.slice(0, key.length - 'characters.json'.length);
        return;
      }
      if (key.endsWith('/items.json')) {
        this.detectedDataPrefix = key.slice(0, key.length - 'items.json'.length);
        return;
      }
    }
  }

  async loadPresetMod(modId = 'wuxia-legend') {
    const modFiles = [
      'mod.json',
      'data/resources.json',
      'data/items.json',
      'data/characters.json',
      'data/external-skills.json',
      'data/internal-skills.json',
      'data/special-skills.json',
      'data/legend-skills.json',
      'data/battles.json',
      'data/buffs.json',
      'data/talents.json',
      'data/scoped-battle-effects.json',
      'data/item-tags.json',
      'data/random-affix-tables.json',
      'data/sects.json',
      'data/grow-templates.json',
      'data/towers.json',
      'data/game-tips.json',
      'data/world-triggers.json',
      'data/maps/large.json',
      'data/maps/small.json',
      'data/stories/main.story.json',
      'data/stories/starting-quiz.story.json',
      'data/stories/jyxr.story.json',
      'data/stories/debug.story.json',
      'data/stories/py.story.json',
      'data/stories/cg.story.json'
    ];

    this.memoryFiles.clear();
    this.dirHandle = null;
    this.mode = 'http';
    this.currentModName = modId === 'wuxia-legend' ? '武侠传奇 (wuxia-legend)' : modId;
    this.detectedDataPrefix = 'data/';

    let loadedCount = 0;
    const baseUrl = `../../mods/${modId}`;

    const fetchPromises = modFiles.map(async (fileRelPath) => {
      try {
        const resp = await fetch(`${baseUrl}/${fileRelPath}?_t=${Date.now()}`);
        if (resp.ok) {
          const text = await resp.text();
          this.memoryFiles.set(fileRelPath, text);
          loadedCount++;
        }
      } catch (e) {
        // 可选文件缺失跳过
      }
    });

    await Promise.all(fetchPromises);

    if (loadedCount === 0) {
      return {
        success: false,
        error: '未能通过 HTTP 获取预设 MOD。若您在本地运行，请双击运行 tools/mod_editor/启动MOD编辑器.command；若在外部环境请直接点击【打开 MOD 目录】选择文件夹。'
      };
    }

    return { success: true, mode: 'http', name: this.currentModName, count: loadedCount };
  }

  readFile(relativePath) {
    if (!relativePath) return null;
    const clean = relativePath.replace(/^[\/]+/, '').replace(/\\/g, '/');

    // 1. 直接全路径命中
    if (this.memoryFiles.has(clean)) {
      return this.memoryFiles.get(clean);
    }

    // 2. 去除 data/ 前缀命中
    const noData = clean.replace(/^data\//, '');
    if (this.memoryFiles.has(noData)) {
      return this.memoryFiles.get(noData);
    }

    // 3. 补齐 data/ 前缀命中
    const withData = `data/${noData}`;
    if (this.memoryFiles.has(withData)) {
      return this.memoryFiles.get(withData);
    }

    // 4. 使用自动检测的前缀命中
    if (this.detectedDataPrefix) {
      const withDetected = `${this.detectedDataPrefix}${noData}`;
      if (this.memoryFiles.has(withDetected)) {
        return this.memoryFiles.get(withDetected);
      }
    }

    // 5. 后缀安全匹配
    for (const [k, v] of this.memoryFiles.entries()) {
      if (k.endsWith('/' + noData) || k === noData) {
        return v;
      }
    }

    return null;
  }

  readJson(relativePath) {
    const raw = this.readFile(relativePath);
    if (!raw) return null;
    try {
      const cleanJson = raw.replace(/^\s*\/\/.*$/gm, '');
      return JSON.parse(cleanJson);
    } catch (err) {
      console.error(`Failed to parse JSON file: ${relativePath}`, err);
      return null;
    }
  }

  async writeFile(relativePath, contentString) {
    if (!relativePath) return { success: false, error: 'Empty path' };
    const clean = relativePath.replace(/^[\/]+/, '').replace(/\\/g, '/');
    const noData = clean.replace(/^data\//, '');

    let targetKey = clean;
    if (this.memoryFiles.has(clean)) {
      targetKey = clean;
    } else if (this.memoryFiles.has(`data/${noData}`)) {
      targetKey = `data/${noData}`;
    } else if (this.memoryFiles.has(noData)) {
      targetKey = noData;
    } else if (this.detectedDataPrefix && this.memoryFiles.has(`${this.detectedDataPrefix}${noData}`)) {
      targetKey = `${this.detectedDataPrefix}${noData}`;
    } else {
      for (const k of this.memoryFiles.keys()) {
        if (k.endsWith('/' + noData) || k === noData) {
          targetKey = k;
          break;
        }
      }
    }

    this.memoryFiles.set(targetKey, contentString);

    if (this.dirHandle && this.isNativeAccessSupported) {
      try {
        const parts = targetKey.split('/');
        const fileName = parts.pop();
        let curDir = this.dirHandle;

        for (const dirName of parts) {
          curDir = await curDir.getDirectoryHandle(dirName, { create: true });
        }

        const fileHandle = await curDir.getFileHandle(fileName, { create: true });
        const writable = await fileHandle.createWritable();
        await writable.write(contentString);
        await writable.close();
        return { success: true, mode: 'native', path: targetKey };
      } catch (err) {
        console.error(`Native write failed for ${targetKey}:`, err);
        return { success: false, error: err.message, path: targetKey };
      }
    }

    return { success: true, mode: this.mode, path: targetKey };
  }

  async writeJson(relativePath, dataObj) {
    const formatted = JSON.stringify(dataObj, null, 2);
    return await this.writeFile(relativePath, formatted);
  }

  exportFile(relativePath) {
    const content = this.readFile(relativePath);
    if (!content) return false;
    const blob = new Blob([content], { type: 'application/json;charset=utf-8' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = relativePath.split('/').pop() || 'mod_data.json';
    document.body.appendChild(a);
    a.click();
    setTimeout(() => {
      document.body.removeChild(a);
      URL.revokeObjectURL(url);
    }, 100);
    return true;
  }
}
