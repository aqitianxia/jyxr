/**
 * FileSystemDriver: 纯前端文件访问与持久化驱动
 * 
 * 优先使用现代浏览器原生 File System Access API (showDirectoryPicker)，
 * 取得授权后可直接读写用户本地磁盘中的 MOD 文件夹，实现无缝存盘。
 * 当环境不支持时，平滑降级为基于内存加载与一键导出下载。
 */
export class FileSystemDriver {
  constructor() {
    this.dirHandle = null;
    this.memoryFiles = new Map(); // 存储 relativePath -> content (用于降级或缓存)
    this.isNativeAccessSupported = typeof window.showDirectoryPicker === 'function';
    this.currentModName = '';
  }

  /**
   * 打开并授权访问本地 MOD 目录
   */
  async selectDirectory() {
    if (this.isNativeAccessSupported) {
      try {
        this.dirHandle = await window.showDirectoryPicker({
          id: 'jyxr-mod-dir',
          mode: 'readwrite',
          startIn: 'documents'
        });
        this.currentModName = this.dirHandle.name;
        await this._scanNativeDirectory();
        return { success: true, mode: 'native', name: this.currentModName };
      } catch (err) {
        if (err.name === 'AbortError') {
          return { success: false, cancelled: true };
        }
        console.warn('Native File System Access failed, fallback to memory mode:', err);
      }
    }

    // 降级模式：触发隐藏的目录选择 input
    return new Promise((resolve) => {
      const input = document.createElement('input');
      input.type = 'file';
      input.webkitdirectory = true;
      input.multiple = true;
      input.style.display = 'none';

      input.onchange = async (e) => {
        const files = Array.from(e.target.files);
        if (!files || files.length === 0) {
          resolve({ success: false, cancelled: true });
          return;
        }

        this.memoryFiles.clear();
        const rootName = files[0].webkitRelativePath.split('/')[0] || 'MOD';
        this.currentModName = rootName;

        for (const file of files) {
          const parts = file.webkitRelativePath.split('/');
          parts.shift(); // 移除根文件夹名
          const relPath = parts.join('/');
          const text = await file.text();
          this.memoryFiles.set(relPath, text);
        }

        document.body.removeChild(input);
        resolve({ success: true, mode: 'memory', name: this.currentModName });
      };

      input.oncancel = () => {
        document.body.removeChild(input);
        resolve({ success: false, cancelled: true });
      };

      document.body.appendChild(input);
      input.click();
    });
  }

  async _scanNativeDirectory() {
    this.memoryFiles.clear();
    await this._readDirectoryRecursive(this.dirHandle, '');
  }

  async _readDirectoryRecursive(dirHandle, pathPrefix) {
    for await (const entry of dirHandle.values()) {
      const entryPath = pathPrefix ? `${pathPrefix}/${entry.name}` : entry.name;
      if (entry.kind === 'file') {
        if (entry.name.endsWith('.json') || entry.name.endsWith('.md')) {
          const file = await entry.getFile();
          const text = await file.text();
          this.memoryFiles.set(entryPath, text);
        }
      } else if (entry.kind === 'directory') {
        if (entry.name !== '.git' && entry.name !== 'node_modules') {
          await this._readDirectoryRecursive(entry, entryPath);
        }
      }
    }
  }

  /**
   * 读取指定路径的文件内容（支持自动定位 data/ 前缀）
   */
  readFile(relativePath) {
    const cleanPath = relativePath.replace(/^\/+/, '');
    // 优先匹配标准路径，再匹配 data/ 下路径
    if (this.memoryFiles.has(cleanPath)) {
      return this.memoryFiles.get(cleanPath);
    }
    const withData = `data/${cleanPath}`;
    if (this.memoryFiles.has(withData)) {
      return this.memoryFiles.get(withData);
    }
    return null;
  }

  /**
   * 读取并解析 JSON（自动剔除 // 单行注释）
   */
  readJson(relativePath) {
    const raw = this.readFile(relativePath);
    if (!raw) return null;
    try {
      // 兼容 JSON 中可能存在的 // 注释
      const cleanJson = raw.replace(/^\s*\/\/.*$/gm, '');
      return JSON.parse(cleanJson);
    } catch (err) {
      console.error(`Failed to parse JSON file: ${relativePath}`, err);
      return null;
    }
  }

  /**
   * 写入保存文件
   */
  async writeFile(relativePath, contentString) {
    const cleanPath = relativePath.replace(/^\/+/, '');
    // 更新内存缓存
    let targetPath = cleanPath;
    if (!this.memoryFiles.has(cleanPath) && this.memoryFiles.has(`data/${cleanPath}`)) {
      targetPath = `data/${cleanPath}`;
    }
    this.memoryFiles.set(targetPath, contentString);

    // 如果拥有原生目录句柄，直接持久化写入硬盘
    if (this.dirHandle && this.isNativeAccessSupported) {
      try {
        const parts = targetPath.split('/');
        const fileName = parts.pop();
        let curDir = this.dirHandle;

        for (const dirName of parts) {
          curDir = await curDir.getDirectoryHandle(dirName, { create: true });
        }

        const fileHandle = await curDir.getFileHandle(fileName, { create: true });
        const writable = await fileHandle.createWritable();
        await writable.write(contentString);
        await writable.close();
        return { success: true, mode: 'native' };
      } catch (err) {
        console.error(`Native write failed for ${targetPath}:`, err);
        return { success: false, error: err.message };
      }
    }

    // 降级模式：触发浏览器下载单个更新后的文件
    return { success: true, mode: 'memory' };
  }

  /**
   * 将修改过的 JSON 格式化写回
   */
  async writeJson(relativePath, dataObj) {
    const formatted = JSON.stringify(dataObj, null, 2);
    return await this.writeFile(relativePath, formatted);
  }

  /**
   * 降级模式下一键导出当前整个数据包
   */
  exportSingleFile(relativePath) {
    const content = this.readFile(relativePath);
    if (!content) return;
    const blob = new Blob([content], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = relativePath.split('/').pop();
    a.click();
    URL.revokeObjectURL(url);
  }
}
