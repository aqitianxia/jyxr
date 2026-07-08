const state = {
  mode: "data",
  workspace: null,
  mods: [],
  activeModId: "",
  dataFiles: [],
  assetFiles: [],
  currentPath: "",
  dirty: false,
  searchMatches: [],
  searchIndex: -1,
  viewMode: "json",
  formRecords: [],
  selectedRecordIndex: 0,
  formSearch: "",
  formFilter: "all",
  characterTab: "talents",
  itemTab: "requirements",
  portraitPicker: {
    open: false,
    search: "",
    selectedAssetPath: "",
  },
  itemPicturePicker: {
    open: false,
    search: "",
    selectedAssetPath: "",
  },
  assetImageInfo: new Map(),
  resourceValues: new Map(),
  portraitCheck: null,
  storyGraph: null,
  selectedStoryGroupId: "",
  selectedStoryNodeId: "",
  contentIndex: {
    ready: false,
    definitionsById: new Map(),
    fileSummaries: new Map(),
    duplicateDefinitions: [],
    parseErrors: [],
    resourcesById: new Map(),
    resourcesByGroup: new Map(),
    charactersByIdOrName: new Map(),
    itemsById: new Map(),
    storySpeakers: new Map(),
  },
};

const storageKeys = {
  lastDataPath: "jyxr-json-editor:last-data-path",
  activeModId: "jyxr-json-editor:active-mod-id",
};

const elements = {
  workspacePath: document.getElementById("workspacePath"),
  modSelect: document.getElementById("modSelect"),
  formatButton: document.getElementById("formatButton"),
  validateButton: document.getElementById("validateButton"),
  saveButton: document.getElementById("saveButton"),
  dataTab: document.getElementById("dataTab"),
  storyTab: document.getElementById("storyTab"),
  assetsTab: document.getElementById("assetsTab"),
  fileSearch: document.getElementById("fileSearch"),
  fileList: document.getElementById("fileList"),
  formModeButton: document.getElementById("formModeButton"),
  jsonModeButton: document.getElementById("jsonModeButton"),
  currentPath: document.getElementById("currentPath"),
  dirtyState: document.getElementById("dirtyState"),
  contentSearch: document.getElementById("contentSearch"),
  findPreviousButton: document.getElementById("findPreviousButton"),
  findNextButton: document.getElementById("findNextButton"),
  searchState: document.getElementById("searchState"),
  formView: document.getElementById("formView"),
  storyView: document.getElementById("storyView"),
  editor: document.getElementById("editor"),
  cursorState: document.getElementById("cursorState"),
  saveState: document.getElementById("saveState"),
  validationBox: document.getElementById("validationBox"),
  indexBox: document.getElementById("indexBox"),
  selectionBox: document.getElementById("selectionBox"),
  speakerToolBox: document.getElementById("speakerToolBox"),
  portraitCheckBox: document.getElementById("portraitCheckBox"),
  characterCheckBox: document.getElementById("characterCheckBox"),
  assetPreview: document.getElementById("assetPreview"),
};

elements.dataTab.addEventListener("click", () => setMode("data"));
elements.storyTab.addEventListener("click", () => setMode("story"));
elements.assetsTab.addEventListener("click", () => setMode("assets"));
elements.fileSearch.addEventListener("input", renderFileList);
elements.formModeButton.addEventListener("click", () => setViewMode("form"));
elements.jsonModeButton.addEventListener("click", () => setViewMode("json"));
elements.formatButton.addEventListener("click", formatCurrentJson);
elements.validateButton.addEventListener("click", validateContent);
elements.saveButton.addEventListener("click", saveCurrentFile);
elements.modSelect.addEventListener("change", () => switchMod(elements.modSelect.value));
elements.contentSearch.addEventListener("input", () => {
  if (state.mode === "story") {
    renderStoryView();
  } else {
    updateSearchMatches();
  }
});
elements.contentSearch.addEventListener("keydown", (event) => {
  if (event.key === "Enter") {
    event.preventDefault();
    jumpSearch(event.shiftKey ? -1 : 1);
  }
});
elements.findPreviousButton.addEventListener("click", () => jumpSearch(-1));
elements.findNextButton.addEventListener("click", () => jumpSearch(1));
elements.editor.addEventListener("input", () => {
  state.dirty = true;
  updateSearchMatches();
  renderDirtyState();
  renderCursorState();
});
elements.editor.addEventListener("click", renderCursorState);
elements.editor.addEventListener("keyup", renderCursorState);
elements.editor.addEventListener("select", () => {
  renderCursorState();
  renderSelectionLookup();
});

window.addEventListener("keydown", handleGlobalKeydown);
window.addEventListener("beforeunload", (event) => {
  if (!state.dirty) {
    return;
  }

  event.preventDefault();
  event.returnValue = "";
});

boot();

async function boot() {
  await loadWorkspace();
  await Promise.all([loadDataFiles(), loadAssetFiles()]);
  await rebuildContentIndex();
  await loadStoryGraph();
  renderFileList();
  await openLastDataFile();
  await validateContent();
}

async function loadWorkspace() {
  state.workspace = await requestJson("/api/workspace");
  state.mods = Array.isArray(state.workspace.mods) ? state.workspace.mods : [];
  const savedModId = localStorage.getItem(storageKeys.activeModId);
  const defaultModId = state.workspace.defaultModId || "jyxr-expansion";
  state.activeModId = state.mods.some((mod) => mod.id === savedModId)
    ? savedModId
    : state.mods.some((mod) => mod.id === defaultModId)
      ? defaultModId
      : state.mods[0]?.id || defaultModId;
  renderModSelect();
  renderWorkspacePath();
  const activeMod = getActiveMod();
  if (activeMod && !activeMod.dataExists) {
    showValidation(false, `缺少 MOD data 目录：${activeMod.path}/data`);
  }
  if (!state.workspace.assetsExists) {
    elements.assetPreview.textContent = `缺少 assets 目录：${state.workspace.assetsPath}`;
  }
}

function renderModSelect() {
  elements.modSelect.replaceChildren();
  for (const mod of state.mods) {
    const option = document.createElement("option");
    option.value = mod.id;
    option.textContent = `${mod.name || mod.id} (${mod.id})`;
    option.selected = mod.id === state.activeModId;
    elements.modSelect.appendChild(option);
  }

  if (state.mods.length === 0) {
    const option = document.createElement("option");
    option.value = state.activeModId;
    option.textContent = state.activeModId;
    elements.modSelect.appendChild(option);
  }
}

function renderWorkspacePath() {
  const activeMod = getActiveMod();
  const modText = activeMod
    ? `${activeMod.name || activeMod.id} · ${activeMod.path}/data`
    : state.activeModId;
  elements.workspacePath.textContent = `正在编辑：${modText}  |  共享资产：${state.workspace.assetsPath}`;
}

async function switchMod(modId) {
  if (modId === state.activeModId) {
    return;
  }

  if (!(await confirmDiscardChanges())) {
    elements.modSelect.value = state.activeModId;
    return;
  }

  state.activeModId = modId;
  localStorage.setItem(storageKeys.activeModId, modId);
  state.currentPath = "";
  state.dirty = false;
  state.formRecords = [];
  state.selectedRecordIndex = 0;
  state.selectedStoryGroupId = "";
  state.selectedStoryNodeId = "";
  elements.editor.value = "";
  elements.currentPath.textContent = "未选择文件";
  elements.saveState.textContent = "";
  renderWorkspacePath();
  await loadDataFiles();
  await rebuildContentIndex();
  await loadStoryGraph();
  renderFileList();
  renderDirtyState();
  renderCursorState();
  if (state.mode === "story") {
    renderStoryView();
  } else {
    await openLastDataFile();
  }
  await validateContent();
}

function getActiveMod() {
  return state.mods.find((mod) => mod.id === state.activeModId) || null;
}

async function loadDataFiles() {
  state.dataFiles = await requestJson("/api/data/files");
}

async function loadAssetFiles() {
  state.assetFiles = await requestJson("/api/assets/files");
}

async function loadStoryGraph() {
  try {
    state.storyGraph = await requestJson("/api/story/graph");
    if (!state.selectedStoryGroupId && state.storyGraph.groups.length > 0) {
      state.selectedStoryGroupId = state.storyGraph.groups[0].id;
    }
  } catch (error) {
    state.storyGraph = null;
    showValidation(false, error.message);
  }
}

function setMode(mode) {
  state.mode = mode;
  document.body.classList.toggle("story-mode", mode === "story");
  elements.dataTab.classList.toggle("active", mode === "data");
  elements.storyTab.classList.toggle("active", mode === "story");
  elements.assetsTab.classList.toggle("active", mode === "assets");
  elements.fileSearch.value = "";
  elements.fileSearch.placeholder = mode === "story"
    ? "搜索剧情线"
    : mode === "assets"
      ? "搜索资产"
      : "搜索文件";
  elements.saveButton.disabled = mode !== "data";
  elements.formatButton.disabled = mode !== "data";
  if (mode === "story") {
    state.dirty = false;
    elements.currentPath.textContent = "剧情图谱";
    elements.saveState.textContent = "";
    elements.assetPreview.textContent = "剧情视图不预览资产";
    elements.formModeButton.disabled = true;
    elements.jsonModeButton.disabled = true;
    elements.formView.classList.add("hidden");
    elements.editor.classList.add("hidden");
    elements.storyView.classList.remove("hidden");
    renderDirtyState();
    renderStoryView();
  } else {
    elements.storyView.classList.add("hidden");
    elements.formModeButton.disabled = state.formRecords.length === 0;
    elements.jsonModeButton.disabled = false;
    elements.formView.classList.toggle("hidden", state.viewMode !== "form");
    elements.editor.classList.toggle("hidden", state.viewMode !== "json");
  }

  renderFileList();
}

function renderFileList() {
  const query = elements.fileSearch.value.trim().toLowerCase();
  if (state.mode === "story") {
    renderStoryGroupList(query);
    return;
  }

  const files = state.mode === "data" ? state.dataFiles : state.assetFiles;
  elements.fileList.replaceChildren();

  for (const file of files) {
    if (query && !file.path.toLowerCase().includes(query)) {
      continue;
    }

    const item = document.createElement("button");
    item.type = "button";
    item.className = "file-item";
    item.title = file.path;
    item.classList.toggle("active", state.currentPath === file.path);
    const summary = state.contentIndex.fileSummaries.get(file.path);
    const title = document.createElement("div");
    title.className = "file-title";
    title.textContent = file.path;
    const meta = document.createElement("div");
    meta.className = "file-meta";
    meta.textContent = state.mode === "data" && summary
      ? `${summary.type} · ${summary.definitions} 条 · ${formatFileSize(file.size)}`
      : formatFileSize(file.size);
    item.append(title, meta);
    item.addEventListener("click", () => {
      if (state.mode === "data") {
        openDataFile(file.path);
      } else {
        openAssetFile(file.path);
      }
    });
    elements.fileList.appendChild(item);
  }
}

function renderStoryGroupList(query) {
  elements.fileList.replaceChildren();
  const graph = state.storyGraph;
  if (!graph) {
    const item = document.createElement("div");
    item.className = "file-item muted";
    item.textContent = "剧情图谱未加载";
    elements.fileList.appendChild(item);
    return;
  }

  for (const group of graph.groups) {
    const searchable = `${group.name} ${group.id}`.toLowerCase();
    if (query && !searchable.includes(query)) {
      continue;
    }

    const item = document.createElement("button");
    item.type = "button";
    item.className = "file-item";
    item.classList.toggle("active", state.selectedStoryGroupId === group.id);
    const title = document.createElement("div");
    title.className = "file-title";
    title.textContent = group.name;
    const meta = document.createElement("div");
    meta.className = "file-meta";
    meta.textContent = `${group.nodeCount} 段 · ${group.entrypointCount} 入口 · ${group.diagnosticCount} 问题`;
    item.append(title, meta);
    item.addEventListener("click", () => {
      state.selectedStoryGroupId = group.id;
      state.selectedStoryNodeId = "";
      renderFileList();
      renderStoryView();
    });
    elements.fileList.appendChild(item);
  }
}

function renderStoryView() {
  elements.storyView.replaceChildren();
  const graph = state.storyGraph;
  if (!graph) {
    const empty = document.createElement("div");
    empty.className = "story-empty";
    empty.textContent = "剧情图谱未加载。";
    elements.storyView.appendChild(empty);
    return;
  }

  if (!state.selectedStoryGroupId && graph.groups.length > 0) {
    state.selectedStoryGroupId = graph.groups[0].id;
  }

  const selectedGroup = graph.groups.find((group) => group.id === state.selectedStoryGroupId) || graph.groups[0];
  if (!selectedGroup) {
    const empty = document.createElement("div");
    empty.className = "story-empty";
    empty.textContent = "没有可显示的剧情段落。";
    elements.storyView.appendChild(empty);
    return;
  }

  const groupNodes = graph.nodes
    .filter((node) => node.groupId === selectedGroup.id)
    .sort((a, b) =>
      b.externalEntrypoints - a.externalEntrypoints ||
      b.outgoing - a.outgoing ||
      a.id.localeCompare(b.id, "zh-Hans-CN"));
  const query = elements.contentSearch.value.trim().toLowerCase();
  const visibleNodes = query
    ? groupNodes.filter((node) => node.id.toLowerCase().includes(query))
    : groupNodes;

  if (!state.selectedStoryNodeId || !groupNodes.some((node) => node.id === state.selectedStoryNodeId)) {
    state.selectedStoryNodeId = visibleNodes[0]?.id || groupNodes[0]?.id || "";
  }

  const selectedNode = graph.nodes.find((node) => node.id === state.selectedStoryNodeId) || visibleNodes[0] || groupNodes[0];
  renderStorySummary(graph, selectedGroup);

  const layout = document.createElement("div");
  layout.className = "story-layout";

  const nodePanel = document.createElement("section");
  nodePanel.className = "story-panel story-node-panel";
  const nodeTitle = document.createElement("div");
  nodeTitle.className = "story-panel-title";
  nodeTitle.textContent = `段落 · ${visibleNodes.length}/${groupNodes.length}`;
  nodePanel.appendChild(nodeTitle);

  const nodeList = document.createElement("div");
  nodeList.className = "story-node-list";
  for (const node of visibleNodes) {
    nodeList.appendChild(createStoryNodeButton(node));
  }
  nodePanel.appendChild(nodeList);

  const detailPanel = document.createElement("section");
  detailPanel.className = "story-panel story-detail-panel";
  renderStoryNodeDetail(detailPanel, graph, selectedNode);

  layout.append(nodePanel, detailPanel);
  elements.storyView.appendChild(layout);

  renderStoryEdgeTable(graph, selectedGroup, query);
}

function renderStorySummary(graph, selectedGroup) {
  const header = document.createElement("div");
  header.className = "story-header";
  const titleWrap = document.createElement("div");
  const title = document.createElement("div");
  title.className = "story-title";
  title.textContent = selectedGroup.name;
  const subtitle = document.createElement("div");
  subtitle.className = "story-subtitle";
  subtitle.textContent = `全库 ${graph.summary.nodeCount} 段 · ${graph.summary.edgeCount} 条流向 · ${graph.summary.entrypointCount} 入口`;
  titleWrap.append(title, subtitle);
  header.appendChild(titleWrap);

  const search = document.createElement("input");
  search.className = "story-search";
  search.type = "search";
  search.placeholder = "搜索当前剧情线";
  search.value = elements.contentSearch.value;
  search.addEventListener("input", () => {
    elements.contentSearch.value = search.value;
    renderStoryView();
  });

  const metrics = document.createElement("div");
  metrics.className = "story-metrics";
  metrics.append(
    createStoryMetric("本线段落", selectedGroup.nodeCount),
    createStoryMetric("入口", selectedGroup.entrypointCount),
    createStoryMetric("流向", selectedGroup.outgoingCount),
    createStoryMetric("提醒", selectedGroup.diagnosticCount),
    createStoryMetric("错误", graph.summary.errors));
  header.append(search, metrics);
  elements.storyView.appendChild(header);
}

function createStoryMetric(label, value) {
  const item = document.createElement("div");
  item.className = "story-metric";
  const number = document.createElement("div");
  number.className = "story-metric-value";
  number.textContent = String(value);
  const text = document.createElement("div");
  text.className = "story-metric-label";
  text.textContent = label;
  item.append(number, text);
  return item;
}

function createStoryNodeButton(node) {
  const button = document.createElement("button");
  button.type = "button";
  button.className = "story-node";
  button.classList.toggle("active", state.selectedStoryNodeId === node.id);

  const title = document.createElement("div");
  title.className = "story-node-title";
  title.textContent = node.id;
  const meta = document.createElement("div");
  meta.className = "story-node-meta";
  meta.textContent = `${node.stepCount} 步 · 来源 ${node.incoming} · 去向 ${node.outgoing}`;
  const tags = document.createElement("div");
  tags.className = "story-node-tags";
  appendStoryTag(tags, `对白 ${node.dialogueCount}`);
  appendStoryTag(tags, `命令 ${node.commandCount}`);
  if (node.choiceCount > 0) appendStoryTag(tags, `选择 ${node.choiceCount}`);
  if (node.branchCount > 0) appendStoryTag(tags, `条件 ${node.branchCount}`);
  if (node.battleCount > 0) appendStoryTag(tags, `战斗 ${node.battleCount}`);
  if (node.externalEntrypoints > 0) appendStoryTag(tags, `入口 ${node.externalEntrypoints}`, "entry");
  button.append(title, meta, tags);
  button.addEventListener("click", () => {
    state.selectedStoryNodeId = node.id;
    renderStoryView();
  });
  return button;
}

function renderStoryNodeDetail(panel, graph, node) {
  panel.replaceChildren();
  if (!node) {
    const empty = document.createElement("div");
    empty.className = "story-empty";
    empty.textContent = "选择一个剧情段落。";
    panel.appendChild(empty);
    return;
  }

  const title = document.createElement("div");
  title.className = "story-panel-title";
  title.textContent = node.id;
  panel.appendChild(title);

  const actions = document.createElement("div");
  actions.className = "story-actions";
  actions.appendChild(createStoryOpenButton("打开 JSON", node.path, node.line));
  panel.appendChild(actions);

  const stats = document.createElement("div");
  stats.className = "story-detail-stats";
  stats.append(
    createStoryMetric("对白", node.dialogueCount),
    createStoryMetric("命令", node.commandCount),
    createStoryMetric("选择", node.choiceCount),
    createStoryMetric("条件", node.branchCount),
    createStoryMetric("战斗", node.battleCount),
    createStoryMetric("跳转", node.jumpCount));
  panel.appendChild(stats);

  const entries = graph.entrypoints.filter((entry) => entry.targetId === node.id);
  appendStoryDetailList(panel, "地图/世界入口", entries, (entry) => {
    const item = document.createElement("div");
    item.className = "story-row";
    item.append(
      createStoryKindBadge(entry.kind),
      createStoryTextBlock(entry.label, entry.conditions.length > 0 ? entry.conditions.join("；") : entry.sourceId),
      createStoryOpenButton("定位", entry.sourcePath, entry.line));
    return item;
  });

  const incoming = graph.edges.filter((edge) => edge.toId === node.id);
  appendStoryDetailList(panel, "从这些段落进入", incoming, (edge) => createStoryEdgeRow(edge, true));

  const outgoing = graph.edges.filter((edge) => edge.fromId === node.id);
  appendStoryDetailList(panel, "会继续到", outgoing, (edge) => createStoryEdgeRow(edge, false));

  const diagnostics = graph.diagnostics.filter((diagnostic) => diagnostic.segmentId === node.id);
  appendStoryDetailList(panel, "结构提醒", diagnostics, (diagnostic) => {
    const item = document.createElement("div");
    item.className = `story-row ${diagnostic.severity}`;
    item.append(
      createStoryKindBadge(diagnostic.severity),
      createStoryTextBlock(diagnostic.message, `${diagnostic.path}:${diagnostic.line || 1}`),
      createStoryOpenButton("定位", diagnostic.path, diagnostic.line || 1));
    return item;
  });
}

function appendStoryDetailList(panel, titleText, items, renderItem) {
  const title = document.createElement("div");
  title.className = "story-section-title";
  title.textContent = `${titleText} (${items.length})`;
  panel.appendChild(title);

  const list = document.createElement("div");
  list.className = "story-row-list";
  if (items.length === 0) {
    const empty = document.createElement("div");
    empty.className = "story-empty small";
    empty.textContent = "无";
    list.appendChild(empty);
  } else {
    for (const item of items.slice(0, 80)) {
      list.appendChild(renderItem(item));
    }
  }

  panel.appendChild(list);
}

function renderStoryEdgeTable(graph, selectedGroup, query) {
  const groupNodeIds = new Set(graph.nodes.filter((node) => node.groupId === selectedGroup.id).map((node) => node.id));
  const edges = graph.edges
    .filter((edge) => groupNodeIds.has(edge.fromId) || groupNodeIds.has(edge.toId))
    .filter((edge) => !query || `${edge.fromId} ${edge.toId} ${edge.label} ${edge.condition || ""}`.toLowerCase().includes(query))
    .slice(0, 500);

  const panel = document.createElement("section");
  panel.className = "story-panel story-edge-panel";
  const title = document.createElement("div");
  title.className = "story-panel-title";
  title.textContent = `本剧情线流向 · ${edges.length}`;
  panel.appendChild(title);

  const list = document.createElement("div");
  list.className = "story-edge-list";
  if (edges.length === 0) {
    const empty = document.createElement("div");
    empty.className = "story-empty small";
    empty.textContent = "当前筛选下没有跨段流向。";
    list.appendChild(empty);
  } else {
    for (const edge of edges) {
      list.appendChild(createStoryEdgeRow(edge, false));
    }
  }

  panel.appendChild(list);
  elements.storyView.appendChild(panel);
}

function createStoryEdgeRow(edge, incoming) {
  const item = document.createElement("div");
  item.className = `story-row edge-${edge.kind}`;
  const mainText = incoming ? edge.fromId : edge.toId;
  const subText = formatStoryEdgeSubtitle(edge);
  item.append(
    createStoryKindBadge(edge.kind),
    createStoryTextBlock(mainText, subText),
    createStoryOpenButton("定位", edge.sourcePath, edge.line || 1));
  return item;
}

function createStoryKindBadge(kind) {
  const badge = document.createElement("span");
  badge.className = `story-badge ${kind}`;
  badge.textContent = translateStoryKind(kind);
  return badge;
}

function formatStoryEdgeSubtitle(edge) {
  if (!edge.condition) {
    return edge.label;
  }

  if (edge.label.includes(edge.condition)) {
    return edge.label;
  }

  return `${edge.label} · ${edge.condition}`;
}

function translateStoryKind(kind) {
  switch (kind) {
    case "jump":
      return "跳转";
    case "time_key":
      return "限时";
    case "dynamic":
      return "动态";
    case "map":
      return "地图";
    case "world":
      return "世界";
    case "initial":
      return "开局";
    case "error":
      return "错误";
    case "warn":
      return "提醒";
    case "info":
      return "信息";
    default:
      return kind;
  }
}

function createStoryTextBlock(titleText, metaText) {
  const wrap = document.createElement("div");
  wrap.className = "story-text-block";
  const title = document.createElement("div");
  title.className = "story-row-title";
  title.textContent = titleText;
  const meta = document.createElement("div");
  meta.className = "story-row-meta";
  meta.textContent = metaText || "";
  wrap.append(title, meta);
  return wrap;
}

function createStoryOpenButton(label, path, line) {
  const button = document.createElement("button");
  button.type = "button";
  button.textContent = label;
  button.addEventListener("click", async () => {
    await revealStoryLocation(path, line || 1);
  });
  return button;
}

function appendStoryTag(container, text, variant = "") {
  const tag = document.createElement("span");
  tag.className = variant ? `story-tag ${variant}` : "story-tag";
  tag.textContent = text;
  container.appendChild(tag);
}

async function openDataFile(path) {
  if (!(await confirmDiscardChanges())) {
    return;
  }

  const file = await requestJson(`/api/data/file?path=${encodeURIComponent(path)}`);
  state.currentPath = file.path;
  state.dirty = false;
  elements.editor.value = file.content;
  elements.editor.readOnly = false;
  elements.currentPath.textContent = file.path;
  elements.assetPreview.textContent = "未选择资产";
  elements.saveState.textContent = "";
  localStorage.setItem(getLastDataPathStorageKey(), file.path);
  refreshFormFromEditor({ preferForm: true });
  updateSearchMatches();
  renderIndexPanel();
  renderSelectionLookup();
  renderDirtyState();
  renderCursorState();
  renderFileList();
}

function openAssetFile(path) {
  state.currentPath = path;
  state.dirty = false;
  elements.editor.value = `assets/${path}`;
  elements.editor.readOnly = true;
  elements.currentPath.textContent = path;
  elements.saveState.textContent = "";
  setViewMode("json");
  elements.formModeButton.disabled = true;
  updateSearchMatches();
  renderIndexPanel();
  renderSelectionLookup();
  renderDirtyState();
  renderCursorState();
  renderFileList();
  previewAsset(path);
}

function previewAsset(path) {
  const url = `/api/assets/file?path=${encodeURIComponent(path)}`;
  const lower = path.toLowerCase();
  elements.assetPreview.replaceChildren();

  if (isImage(lower)) {
    const image = document.createElement("img");
    image.src = url;
    image.alt = path;
    elements.assetPreview.appendChild(image);
  } else if (isAudio(lower)) {
    const audio = document.createElement("audio");
    audio.controls = true;
    audio.src = url;
    elements.assetPreview.appendChild(audio);
  } else {
    const text = document.createElement("div");
    text.className = "muted";
    text.textContent = "可复制资产路径";
    elements.assetPreview.appendChild(text);
  }

  const pathLine = document.createElement("div");
  pathLine.className = "asset-path";
  pathLine.textContent = path;
  elements.assetPreview.appendChild(pathLine);

  const copyButton = document.createElement("button");
  copyButton.type = "button";
  copyButton.textContent = "复制路径";
  copyButton.addEventListener("click", async () => {
    await navigator.clipboard.writeText(path);
  });
  elements.assetPreview.appendChild(copyButton);
}

async function saveCurrentFile() {
  if (!state.currentPath || state.mode !== "data") {
    showValidation(false, "请选择 data JSON 文件。");
    return;
  }

  elements.saveButton.disabled = true;
  try {
    JSON.parse(elements.editor.value);
    const result = await requestJson("/api/data/file", {
      method: "PUT",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        path: state.currentPath,
        content: elements.editor.value,
      }),
    });

    elements.editor.value = result.content;
    state.dirty = false;
    refreshFormFromEditor({ preferForm: state.viewMode === "form" });
    renderDirtyState();
    renderCursorState();
    elements.saveState.textContent = result.backupPath
      ? `已保存，备份：${result.backupPath}`
      : "已保存";
    showValidation(result.validation.ok, result.validation.message);
    await loadDataFiles();
    await rebuildContentIndex();
    await loadStoryGraph();
    renderFileList();
  } catch (error) {
    showValidation(false, error instanceof SyntaxError ? formatJsonError(error) : error.message);
  } finally {
    elements.saveButton.disabled = false;
  }
}

function formatCurrentJson() {
  if (state.mode !== "data" || elements.editor.readOnly) {
    return;
  }

  try {
    elements.editor.value = `${JSON.stringify(JSON.parse(elements.editor.value), null, 2)}\n`;
    state.dirty = true;
    elements.saveState.textContent = "已格式化，尚未保存";
    showValidation(true, "JSON format is valid.");
    refreshFormFromEditor({ preferForm: state.viewMode === "form" });
    updateSearchMatches();
    renderDirtyState();
    renderCursorState();
  } catch (error) {
    showValidation(false, formatJsonError(error));
  }
}

async function validateContent() {
  try {
    const result = await requestJson("/api/validate");
    showValidation(result.ok, result.message);
  } catch (error) {
    showValidation(false, error.message);
  }
}

function renderDirtyState() {
  elements.dirtyState.textContent = state.dirty ? "未保存" : "";
}

function renderCursorState() {
  const position = elements.editor.selectionStart ?? 0;
  const textBeforeCursor = elements.editor.value.slice(0, position);
  const lines = textBeforeCursor.split("\n");
  const line = lines.length;
  const column = lines[lines.length - 1].length + 1;
  elements.cursorState.textContent = `行 ${line}，列 ${column}`;
  renderSelectionLookup();
}

function showValidation(ok, message) {
  elements.validationBox.className = `message ${ok ? "ok" : "bad"}`;
  elements.validationBox.textContent = message;
}

function setViewMode(mode) {
  if (mode === "form" && !refreshFormFromEditor({ preferForm: false })) {
    showValidation(false, "当前 JSON 暂不支持表单视图。");
    mode = "json";
  }

  state.viewMode = mode;
  elements.formModeButton.classList.toggle("active", mode === "form");
  elements.jsonModeButton.classList.toggle("active", mode === "json");
  elements.formView.classList.toggle("hidden", mode !== "form");
  elements.editor.classList.toggle("hidden", mode !== "json");
  renderFormView();
}

function refreshFormFromEditor({ preferForm }) {
  try {
    const json = JSON.parse(elements.editor.value);
    if (!Array.isArray(json) || !json.every((record) => record && typeof record === "object" && !Array.isArray(record))) {
      state.formRecords = [];
      elements.formModeButton.disabled = true;
      if (state.viewMode === "form") {
        state.viewMode = "json";
      }

      setViewModeButtons();
      elements.formView.classList.add("hidden");
      elements.editor.classList.remove("hidden");
      return false;
    }

    state.formRecords = json;
    state.selectedRecordIndex = Math.min(state.selectedRecordIndex, Math.max(0, state.formRecords.length - 1));
    elements.formModeButton.disabled = false;
    if (preferForm) {
      state.viewMode = "form";
    }

    setViewModeButtons();
    elements.formView.classList.toggle("hidden", state.viewMode !== "form");
    elements.editor.classList.toggle("hidden", state.viewMode !== "json");
    renderFormView();
    return true;
  } catch {
    state.formRecords = [];
    elements.formModeButton.disabled = true;
    if (state.viewMode === "form") {
      state.viewMode = "json";
    }

    setViewModeButtons();
    elements.formView.classList.add("hidden");
    elements.editor.classList.remove("hidden");
    return false;
  }
}

function setViewModeButtons() {
  elements.formModeButton.classList.toggle("active", state.viewMode === "form");
  elements.jsonModeButton.classList.toggle("active", state.viewMode === "json");
}

function renderFormView() {
  const scrollState = captureFormViewScrollState();
  elements.formView.replaceChildren();
  if (state.viewMode !== "form") {
    renderCharacterCheckTool();
    renderPortraitPicker();
    renderItemPicturePicker();
    return;
  }

  if (state.formRecords.length === 0) {
    const error = document.createElement("div");
    error.className = "form-error";
    error.textContent = "当前 JSON 没有可表单化的顶层数组条目。";
    elements.formView.appendChild(error);
    renderCharacterCheckTool();
    renderPortraitPicker();
    renderItemPicturePicker();
    return;
  }

  if (isCharacterFile()) {
    renderCharacterFormView();
  } else if (isItemFile()) {
    renderItemFormView();
  } else {
    renderGenericFormView();
  }

  renderCharacterCheckTool();
  renderPortraitPicker();
  renderItemPicturePicker();
  restoreFormViewScrollState(scrollState);
}

function captureFormViewScrollState() {
  return {
    recordListScrollTop: elements.formView.querySelector(".record-list")?.scrollTop ?? 0,
    detailScrollTop: elements.formView.querySelector(".form-detail")?.scrollTop ?? 0,
  };
}

function restoreFormViewScrollState(scrollState) {
  if (!scrollState) {
    return;
  }

  requestAnimationFrame(() => {
    const recordList = elements.formView.querySelector(".record-list");
    const detail = elements.formView.querySelector(".form-detail");
    if (recordList) {
      recordList.scrollTop = scrollState.recordListScrollTop;
    }

    if (detail) {
      detail.scrollTop = scrollState.detailScrollTop;
    }
  });
}

function renderGenericFormView() {
  const recordPanel = document.createElement("aside");
  recordPanel.className = "record-panel";

  const recordHeader = document.createElement("div");
  recordHeader.className = "record-panel-header";
  recordHeader.innerHTML = `<div class="record-panel-title">条目</div><div class="record-panel-subtitle">${state.formRecords.length} 条</div>`;

  const recordSearch = document.createElement("input");
  recordSearch.className = "record-search";
  recordSearch.type = "search";
  recordSearch.placeholder = "搜索 id、名称、类型";
  recordSearch.value = state.formSearch;
  recordSearch.addEventListener("input", () => {
    state.formSearch = recordSearch.value;
    renderGenericRecordCards(recordList);
  });

  const recordList = document.createElement("div");
  recordList.className = "record-list";
  renderGenericRecordCards(recordList);

  recordPanel.append(recordHeader, recordSearch, recordList);

  const detail = document.createElement("section");
  detail.className = "form-detail";
  renderGenericRecordDetail(detail);

  elements.formView.append(recordPanel, detail);
}

function renderCharacterFormView() {
  if (!getCharacterFilters().some((filter) => filter.value === state.formFilter)) {
    state.formFilter = "all";
  }

  const recordPanel = document.createElement("aside");
  recordPanel.className = "record-panel character-record-panel";

  const recordHeader = document.createElement("div");
  recordHeader.className = "record-panel-header";
  const headerTitle = document.createElement("div");
  headerTitle.className = "record-panel-title";
  headerTitle.textContent = "角色";
  const headerSubtitle = document.createElement("div");
  headerSubtitle.className = "record-panel-subtitle";
  recordHeader.append(headerTitle, headerSubtitle);

  const recordSearch = document.createElement("input");
  recordSearch.className = "record-search";
  recordSearch.type = "search";
  recordSearch.placeholder = "搜索 id、姓名、头像、门派、标签";
  recordSearch.value = state.formSearch;
  recordSearch.addEventListener("input", () => {
    state.formSearch = recordSearch.value;
    renderCharacterRecordCards(recordList, headerSubtitle);
  });

  const filterRow = document.createElement("div");
  filterRow.className = "character-filter-row";
  for (const filter of getCharacterFilters()) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "character-filter-button";
    button.classList.toggle("active", state.formFilter === filter.value);
    button.textContent = filter.label;
    button.addEventListener("click", () => {
      state.formFilter = filter.value;
      renderFormView();
    });
    filterRow.appendChild(button);
  }

  const recordList = document.createElement("div");
  recordList.className = "record-list";
  renderCharacterRecordCards(recordList, headerSubtitle);

  recordPanel.append(recordHeader, recordSearch, filterRow, recordList);

  const detail = document.createElement("section");
  detail.className = "form-detail character-form-detail";
  renderCharacterDetail(detail);

  elements.formView.append(recordPanel, detail);
}

function renderGenericRecordCards(parent) {
  parent.replaceChildren();
  const query = state.formSearch.trim().toLowerCase();
  let visibleCount = 0;

  state.formRecords.forEach((record, index) => {
    const title = getRecordTitle(record, index);
    const subtitle = getRecordSubtitle(record, index);
    const haystack = `${title} ${subtitle} ${JSON.stringify(record)}`.toLowerCase();
    if (query && !haystack.includes(query)) {
      return;
    }

    visibleCount += 1;
    const card = document.createElement("button");
    card.type = "button";
    card.className = "record-card";
    card.classList.toggle("active", index === state.selectedRecordIndex);
    card.addEventListener("click", () => {
      state.selectedRecordIndex = index;
      renderFormView();
    });

    const text = document.createElement("div");
    const titleNode = document.createElement("div");
    titleNode.className = "record-title";
    titleNode.textContent = title;
    const subtitleNode = document.createElement("div");
    subtitleNode.className = "record-subtitle";
    subtitleNode.textContent = subtitle;
    text.append(titleNode, subtitleNode);
    card.append(createRecordThumb(record), text);
    parent.appendChild(card);
  });

  if (visibleCount === 0) {
    const empty = document.createElement("div");
    empty.className = "record-empty";
    empty.textContent = "没有匹配的条目";
    parent.appendChild(empty);
  }
}

function renderCharacterRecordCards(parent, subtitleNode) {
  parent.replaceChildren();
  const query = state.formSearch.trim().toLowerCase();
  let visibleCount = 0;

  state.formRecords.forEach((record, index) => {
    if (!matchesCharacterSearch(record, query) || !matchesCharacterFilter(record, state.formFilter)) {
      return;
    }

    visibleCount += 1;
    const card = document.createElement("button");
    card.type = "button";
    card.className = "record-card character-record-card";
    card.classList.toggle("active", index === state.selectedRecordIndex);
    card.addEventListener("click", () => {
      state.selectedRecordIndex = index;
      renderFormView();
    });

    const portraitInfo = getCharacterPortraitInfo(record);
    const classification = getCharacterUiClassification(record);
    const issues = getCharacterValidationIssues(record);

    const text = document.createElement("div");
    text.className = "character-record-text";

    const titleRow = document.createElement("div");
    titleRow.className = "character-record-title-row";
    const title = document.createElement("div");
    title.className = "record-title";
    title.textContent = typeof record.name === "string" && record.name.trim() ? record.name.trim() : (record.id || `#${index + 1}`);
    const badge = document.createElement("span");
    badge.className = `character-role-badge ${classification.key}`;
    badge.textContent = classification.label;
    titleRow.append(title, badge);

    const subtitle = document.createElement("div");
    subtitle.className = "record-subtitle";
    subtitle.textContent = typeof record.id === "string" && record.id.trim() ? record.id.trim() : `#${index + 1}`;

    const meta = document.createElement("div");
    meta.className = "character-record-meta";
    meta.textContent = [
      `Lv.${getDisplayNumber(record.level, 1)}`,
      portraitInfo.assetExists ? "有头像" : portraitInfo.portraitId ? "缺头像" : "无 portrait",
      issues.length > 0 ? `${issues.length} 项待处理` : "配置正常",
    ].join(" · ");

    text.append(titleRow, subtitle, meta);
    card.append(createRecordThumb(record), text);
    parent.appendChild(card);
  });

  subtitleNode.textContent = `${visibleCount} / ${state.formRecords.length} 条`;

  if (visibleCount === 0) {
    const empty = document.createElement("div");
    empty.className = "record-empty";
    empty.textContent = "没有匹配的角色";
    parent.appendChild(empty);
  }
}

function renderGenericRecordDetail(parent) {
  const record = state.formRecords[state.selectedRecordIndex];
  if (!record) {
    parent.innerHTML = `<div class="form-error">未选择条目。</div>`;
    return;
  }

  const title = getRecordTitle(record, state.selectedRecordIndex);
  const subtitle = getRecordSubtitle(record, state.selectedRecordIndex);
  const header = document.createElement("div");
  header.className = "form-detail-header";
  const titleGroup = document.createElement("div");
  titleGroup.innerHTML = `<div class="form-detail-title">${escapeHtml(title)}</div><div class="form-detail-subtitle">${escapeHtml(subtitle)}</div>`;
  const actions = document.createElement("div");
  actions.className = "record-actions";
  actions.append(
    createActionButton("新增", addRecord),
    createActionButton("复制", duplicateRecord),
    createActionButton("删除", deleteRecord)
  );
  header.append(titleGroup, actions);

  const summary = document.createElement("div");
  summary.className = "form-summary";
  summary.textContent = "常用字段可直接编辑；复杂字段会保留为 JSON 文本。保存仍使用顶部保存按钮。";

  const fieldset = document.createElement("section");
  fieldset.className = "form-fieldset";
  const fieldsetTitle = document.createElement("div");
  fieldsetTitle.className = "form-fieldset-title";
  fieldsetTitle.textContent = "字段";
  const grid = document.createElement("div");
  grid.className = "form-grid";

  for (const [key, value] of Object.entries(record)) {
    grid.appendChild(createFieldEditor(record, key, value));
  }

  fieldset.append(fieldsetTitle, grid);
  parent.append(header, summary, fieldset);
}

function renderCharacterDetail(parent) {
  const record = state.formRecords[state.selectedRecordIndex];
  if (!record) {
    parent.innerHTML = `<div class="form-error">未选择角色。</div>`;
    return;
  }

  ensureCharacterShape(record);
  const portraitInfo = getCharacterPortraitInfo(record);
  const classification = getCharacterUiClassification(record);
  const issues = getCharacterValidationIssues(record);

  const shell = document.createElement("div");
  shell.className = "character-detail-shell";

  const summaryCard = document.createElement("section");
  summaryCard.className = "character-summary-card";

  const portrait = createCharacterPortraitHero(record, portraitInfo);
  const content = document.createElement("div");
  content.className = "character-summary-content";

  const titleRow = document.createElement("div");
  titleRow.className = "character-summary-title-row";
  const titleGroup = document.createElement("div");
  const title = document.createElement("div");
  title.className = "form-detail-title";
  title.textContent = typeof record.name === "string" && record.name.trim() ? record.name.trim() : (record.id || "未命名角色");
  const subtitle = document.createElement("div");
  subtitle.className = "form-detail-subtitle";
  subtitle.textContent = typeof record.id === "string" && record.id.trim() ? record.id.trim() : `#${state.selectedRecordIndex + 1}`;
  titleGroup.append(title, subtitle);
  const actions = document.createElement("div");
  actions.className = "record-actions";
  actions.append(
    createActionButton("新增", addRecord),
    createActionButton("复制", duplicateRecord),
    createActionButton("删除", deleteRecord)
  );
  titleRow.append(titleGroup, actions);

  const badgeRow = document.createElement("div");
  badgeRow.className = "character-summary-badges";
  badgeRow.append(
    createPill(`等级 Lv.${getDisplayNumber(record.level, 1)}`),
    createPill(`性别 ${formatGenderLabel(record.gender)}`),
    createPill(`用途 ${classification.label}`, classification.key),
    createPill(issues.length > 0 ? `待处理 ${issues.length}` : "配置正常", issues.length > 0 ? "warn" : "ok")
  );

  content.append(titleRow, badgeRow);
  summaryCard.append(portrait, content);
  shell.appendChild(summaryCard);

  if (issues.length > 0) {
    shell.appendChild(createCharacterIssueSummary(issues));
  }

  const intro = document.createElement("div");
  intro.className = "form-summary character-form-summary";
  intro.textContent = "底层仍保存原 JSON；这里把角色常用字段整理成可直接编辑的专用界面。复杂字段保留在“高级 JSON”里兜底。";
  shell.appendChild(intro);

  const basicSection = createCharacterSection("基础信息", "Basic");
  const basicGrid = document.createElement("div");
  basicGrid.className = "character-form-grid";
  basicGrid.append(
    createCharacterTextField(record, "角色ID", "id", {
      list: null,
      placeholder: "例如：骆冰",
      rerenderOnChange: true,
    }),
    createCharacterTextField(record, "显示名", "name", {
      placeholder: "例如：骆冰",
      rerenderOnChange: true,
    }),
    createCharacterNumberField(record, "等级", "level", {
      min: 1,
      fallback: 1,
      rerenderOnChange: true,
    }),
    createCharacterSelectField(record, "性别", "gender", [
      { value: "neutral", label: "neutral 中立" },
      { value: "male", label: "male 男" },
      { value: "female", label: "female 女" },
    ]),
    createCharacterTextField(record, "头像", "portrait", {
      list: ensureResourceIdDatalist("头像"),
      placeholder: "例如：头像.骆冰",
      rerenderOnChange: true,
    }),
    createCharacterTextField(record, "模型", "model", {
      placeholder: "例如：luobing",
      rerenderOnChange: true,
    }),
    createCharacterTextField(record, "成长模板", "growTemplate", {
      list: ensureDefinitionIdDatalist("grow-templates"),
      placeholder: "例如：主角",
      nullable: true,
      rerenderOnChange: true,
    }),
    createCharacterCheckboxField(record, "可上战场", "arenaEnabled", "arenaEnabled")
  );
  basicSection.appendChild(basicGrid);
  shell.appendChild(basicSection);

  const portraitSection = createCharacterSection("头像", "Portrait");
  portraitSection.appendChild(createCharacterPortraitSection(record, portraitInfo));
  shell.appendChild(portraitSection);

  const statsSection = createCharacterSection("属性", "Stats");
  const statsGrid = document.createElement("div");
  statsGrid.className = "character-stats-grid";
  for (const stat of CHARACTER_STAT_FIELDS) {
    statsGrid.appendChild(createCharacterStatField(record, stat));
  }
  statsSection.appendChild(statsGrid);
  shell.appendChild(statsSection);

  const skillsSection = createCharacterSection("技能与装备", "Skills & Equipment");
  skillsSection.appendChild(createCharacterTabBar());
  skillsSection.appendChild(createCharacterTabContent(record));
  shell.appendChild(skillsSection);

  const advancedSection = createCharacterAdvancedJsonSection(record);
  shell.appendChild(advancedSection);

  parent.appendChild(shell);
}

const CHARACTER_STAT_FIELDS = [
  { key: "bili", label: "臂力" },
  { key: "dingli", label: "定力" },
  { key: "fuyuan", label: "福缘" },
  { key: "gengu", label: "根骨" },
  { key: "jianfa", label: "剑法" },
  { key: "daofa", label: "刀法" },
  { key: "quanzhang", label: "拳掌" },
  { key: "qimen", label: "奇门" },
  { key: "shenfa", label: "身法" },
  { key: "wuxing", label: "悟性" },
  { key: "wuxue", label: "武学" },
  { key: "max_hp", label: "最大生命" },
  { key: "max_mp", label: "最大内力" },
];

const ITEM_TYPE_CHOICES = [
  { value: "consumable", label: "consumable 消耗品" },
  { value: "equipment", label: "equipment 装备" },
  { value: "skill_book", label: "skill_book 武学书" },
  { value: "special_skill_book", label: "special_skill_book 绝技书" },
  { value: "talent_book", label: "talent_book 天赋书" },
  { value: "quest_item", label: "quest_item 剧情物品" },
  { value: "booster", label: "booster 强化道具" },
  { value: "utility", label: "utility 功能物品" },
];

const ITEM_CATEGORY_CHOICES = [
  { value: "normal", label: "normal 普通" },
  { value: "equipment", label: "equipment 装备" },
];

const ITEM_SLOT_TYPE_CHOICES = [
  { value: "weapon", label: "weapon 武器" },
  { value: "armor", label: "armor 护甲" },
  { value: "accessory", label: "accessory 饰品" },
];

const ITEM_REQUIREMENT_TYPE_CHOICES = [
  { value: "stat", label: "stat 属性要求" },
  { value: "talent", label: "talent 天赋要求" },
];

const ITEM_EFFECT_TYPE_CHOICES = [
  { value: "add_hp", label: "add_hp 回复生命" },
  { value: "add_mp", label: "add_mp 回复内力" },
  { value: "add_hp_percent", label: "add_hp_percent 生命百分比" },
  { value: "add_mp_percent", label: "add_mp_percent 内力百分比" },
  { value: "add_maxhp", label: "add_maxhp 提升生命上限" },
  { value: "add_maxmp", label: "add_maxmp 提升内力上限" },
  { value: "add_rage", label: "add_rage 增加怒气" },
  { value: "detoxify", label: "detoxify 解毒" },
  { value: "add_buff", label: "add_buff 添加 Buff" },
  { value: "external_skill", label: "external_skill 学会外功" },
  { value: "internal_skill", label: "internal_skill 学会内功" },
  { value: "special_skill", label: "special_skill 学会绝技" },
  { value: "grant_talent", label: "grant_talent 获得天赋" },
];

const ITEM_AFFIX_TYPE_CHOICES = [
  { value: "stat_modifier", label: "stat_modifier 属性修正" },
  { value: "grant_talent", label: "grant_talent 赋予天赋" },
  { value: "grant_model", label: "grant_model 赋予模型" },
  { value: "skill_bonus_modifier", label: "skill_bonus_modifier 技能威力修正" },
  { value: "weapon_bonus_modifier", label: "weapon_bonus_modifier 武学类别修正" },
  { value: "legend_skill_chance_modifier", label: "legend_skill_chance_modifier 传奇招式触发率" },
];

const ITEM_AFFIX_STAT_CHOICES = [
  { value: "attack", label: "attack 攻击" },
  { value: "defence", label: "defence 防御" },
  { value: "crit_chance", label: "crit_chance 暴击率" },
  { value: "anti_crit_chance", label: "anti_crit_chance 抗暴率" },
  { value: "crit_mult", label: "crit_mult 暴击倍率" },
  { value: "lifesteal", label: "lifesteal 吸血" },
  { value: "anti_debuff", label: "anti_debuff 抗异常" },
  { value: "bili", label: "bili 臂力" },
  { value: "dingli", label: "dingli 定力" },
  { value: "fuyuan", label: "fuyuan 福缘" },
  { value: "gengu", label: "gengu 根骨" },
  { value: "jianfa", label: "jianfa 剑法" },
  { value: "daofa", label: "daofa 刀法" },
  { value: "quanzhang", label: "quanzhang 拳掌" },
  { value: "qimen", label: "qimen 奇门" },
  { value: "shenfa", label: "shenfa 身法" },
  { value: "wuxing", label: "wuxing 悟性" },
];

const ITEM_WEAPON_TYPE_CHOICES = [
  { value: "quanzhang", label: "quanzhang 拳掌" },
  { value: "jianfa", label: "jianfa 剑法" },
  { value: "daofa", label: "daofa 刀法" },
  { value: "qimen", label: "qimen 奇门" },
  { value: "internal_skill", label: "internal_skill 内功" },
];

function isCharacterFile() {
  return state.currentPath === "characters.json";
}

function isItemFile() {
  return state.currentPath === "items.json";
}

function renderItemFormView() {
  if (!getItemFilters().some((filter) => filter.value === state.formFilter)) {
    state.formFilter = "all";
  }

  if (!["requirements", "effects", "affixes"].includes(state.itemTab)) {
    state.itemTab = "requirements";
  }

  const recordPanel = document.createElement("aside");
  recordPanel.className = "record-panel character-record-panel";

  const recordHeader = document.createElement("div");
  recordHeader.className = "record-panel-header";
  const headerTitle = document.createElement("div");
  headerTitle.className = "record-panel-title";
  headerTitle.textContent = "物品";
  const headerSubtitle = document.createElement("div");
  headerSubtitle.className = "record-panel-subtitle";
  recordHeader.append(headerTitle, headerSubtitle);

  const recordSearch = document.createElement("input");
  recordSearch.className = "record-search";
  recordSearch.type = "search";
  recordSearch.placeholder = "搜索 id、名称、图片、类型、分类";
  recordSearch.value = state.formSearch;
  recordSearch.addEventListener("input", () => {
    state.formSearch = recordSearch.value;
    renderItemRecordCards(recordList, headerSubtitle);
  });

  const filterRow = document.createElement("div");
  filterRow.className = "character-filter-row";
  for (const filter of getItemFilters()) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "character-filter-button";
    button.classList.toggle("active", state.formFilter === filter.value);
    button.textContent = filter.label;
    button.addEventListener("click", () => {
      state.formFilter = filter.value;
      renderFormView();
    });
    filterRow.appendChild(button);
  }

  const recordList = document.createElement("div");
  recordList.className = "record-list";
  renderItemRecordCards(recordList, headerSubtitle);

  recordPanel.append(recordHeader, recordSearch, filterRow, recordList);

  const detail = document.createElement("section");
  detail.className = "form-detail character-form-detail";
  renderItemDetail(detail);

  elements.formView.append(recordPanel, detail);
}

function getItemFilters() {
  return [
    { value: "all", label: "全部" },
    { value: "consumable", label: "消耗/强化" },
    { value: "equipment", label: "装备" },
    { value: "books", label: "秘籍" },
    { value: "talentBook", label: "天赋书" },
    { value: "quest", label: "剧情物品" },
    { value: "missingPicture", label: "缺图片" },
    { value: "incomplete", label: "配置待补" },
  ];
}

function matchesItemSearch(record, query) {
  if (!query) {
    return true;
  }

  const haystack = [
    record.id,
    record.name,
    record.picture,
    record.type,
    record.category,
    record.slotType,
    record.description,
    JSON.stringify(record),
  ].filter(Boolean).join(" ").toLowerCase();

  return haystack.includes(query);
}

function matchesItemFilter(record, filter) {
  const pictureInfo = getItemPictureInfo(record);
  const issues = getItemValidationIssues(record);

  switch (filter) {
    case "consumable":
      return ["consumable", "booster", "utility"].includes(record.type);
    case "equipment":
      return record.type === "equipment";
    case "books":
      return ["skill_book", "special_skill_book"].includes(record.type);
    case "talentBook":
      return record.type === "talent_book";
    case "quest":
      return record.type === "quest_item";
    case "missingPicture":
      return !pictureInfo.resourceExists || !pictureInfo.assetExists;
    case "incomplete":
      return issues.length > 0;
    case "all":
    default:
      return true;
  }
}

function renderItemRecordCards(parent, subtitleNode) {
  parent.replaceChildren();
  const query = state.formSearch.trim().toLowerCase();
  let visibleCount = 0;

  state.formRecords.forEach((record, index) => {
    if (!matchesItemSearch(record, query) || !matchesItemFilter(record, state.formFilter)) {
      return;
    }

    visibleCount += 1;
    const card = document.createElement("button");
    card.type = "button";
    card.className = "record-card character-record-card item-record-card";
    card.classList.toggle("active", index === state.selectedRecordIndex);
    card.addEventListener("click", () => {
      state.selectedRecordIndex = index;
      renderFormView();
    });

    const pictureInfo = getItemPictureInfo(record);
    const issues = getItemValidationIssues(record);

    const text = document.createElement("div");
    text.className = "character-record-text";

    const titleRow = document.createElement("div");
    titleRow.className = "character-record-title-row";
    const title = document.createElement("div");
    title.className = "record-title";
    title.textContent = typeof record.name === "string" && record.name.trim() ? record.name.trim() : (record.id || `#${index + 1}`);
    const badge = document.createElement("span");
    badge.className = `character-role-badge item-type-badge ${record.type || "unknown"}`;
    badge.textContent = getItemTypeShortLabel(record.type);
    titleRow.append(title, badge);

    const subtitle = document.createElement("div");
    subtitle.className = "record-subtitle";
    subtitle.textContent = typeof record.id === "string" && record.id.trim() ? record.id.trim() : `#${index + 1}`;

    const meta = document.createElement("div");
    meta.className = "character-record-meta";
    meta.textContent = [
      `Lv.${getDisplayNumber(record.level, 1)}`,
      getItemCategoryLabel(record.category),
      pictureInfo.assetExists ? "有图片" : pictureInfo.pictureId ? "缺图片" : "无 picture",
      issues.length > 0 ? `${issues.length} 项待处理` : "配置正常",
    ].join(" · ");

    text.append(titleRow, subtitle, meta);
    card.append(createRecordThumb(record), text);
    parent.appendChild(card);
  });

  subtitleNode.textContent = `${visibleCount} / ${state.formRecords.length} 条`;

  if (visibleCount === 0) {
    const empty = document.createElement("div");
    empty.className = "record-empty";
    empty.textContent = "没有匹配的物品";
    parent.appendChild(empty);
  }
}

function renderItemDetail(parent) {
  const record = state.formRecords[state.selectedRecordIndex];
  if (!record) {
    parent.innerHTML = `<div class="form-error">未选择物品。</div>`;
    return;
  }

  ensureItemShape(record);
  const pictureInfo = getItemPictureInfo(record);
  const issues = getItemValidationIssues(record);

  const shell = document.createElement("div");
  shell.className = "character-detail-shell item-detail-shell";

  const summaryCard = document.createElement("section");
  summaryCard.className = "character-summary-card item-summary-card";

  const picture = createItemSummaryPicture(record, pictureInfo);
  const content = document.createElement("div");
  content.className = "character-summary-content";

  const titleRow = document.createElement("div");
  titleRow.className = "character-summary-title-row";
  const titleGroup = document.createElement("div");
  const title = document.createElement("div");
  title.className = "form-detail-title";
  title.textContent = typeof record.name === "string" && record.name.trim() ? record.name.trim() : (record.id || "未命名物品");
  const subtitle = document.createElement("div");
  subtitle.className = "form-detail-subtitle";
  subtitle.textContent = typeof record.id === "string" && record.id.trim() ? record.id.trim() : `#${state.selectedRecordIndex + 1}`;
  titleGroup.append(title, subtitle);
  const actions = document.createElement("div");
  actions.className = "record-actions";
  actions.append(
    createActionButton("新增", addRecord),
    createActionButton("复制", duplicateRecord),
    createActionButton("删除", deleteRecord)
  );
  titleRow.append(titleGroup, actions);

  const badgeRow = document.createElement("div");
  badgeRow.className = "character-summary-badges";
  badgeRow.append(
    createPill(`类型 ${getItemTypeShortLabel(record.type)}`),
    createPill(`分类 ${getItemCategoryLabel(record.category)}`),
    createPill(`等级 Lv.${getDisplayNumber(record.level, 1)}`),
    createPill(`价格 ${getDisplayNumber(record.price, 0)}`),
    createPill(record.canDrop === true ? "可掉落" : "不可掉落", record.canDrop === true ? "ok" : ""),
    createPill(issues.length > 0 ? `待处理 ${issues.length}` : "配置正常", issues.length > 0 ? "warn" : "ok")
  );

  content.append(titleRow, badgeRow);
  summaryCard.append(picture, content);
  shell.appendChild(summaryCard);

  if (issues.length > 0) {
    shell.appendChild(createCharacterIssueSummary(issues));
  }

  const intro = document.createElement("div");
  intro.className = "form-summary character-form-summary";
  intro.textContent = "底层仍保存原 JSON；这里把物品常用字段整理成更容易理解的专用界面。复杂结构仍可在“高级 JSON”里直接调整。";
  shell.appendChild(intro);

  const basicSection = createCharacterSection("基础信息", "Basic");
  const basicGrid = document.createElement("div");
  basicGrid.className = "character-form-grid";
  basicGrid.append(
    createItemCategoryField(record),
    createItemTypeField(record),
    createCharacterTextField(record, "物品ID", "id", {
      placeholder: "例如：止血草",
      rerenderOnChange: true,
    }),
    createCharacterTextField(record, "显示名", "name", {
      placeholder: "例如：止血草",
      rerenderOnChange: true,
    }),
    createCharacterNumberField(record, "等级", "level", {
      min: 1,
      fallback: 1,
      rerenderOnChange: true,
    }),
    createCharacterNumberField(record, "价格", "price", {
      fallback: 0,
      rerenderOnChange: true,
    }),
    createCharacterNumberField(record, "冷却", "cooldown", {
      min: 0,
      fallback: 0,
      rerenderOnChange: true,
    }),
    createCharacterCheckboxField(record, "可掉落", "canDrop", "canDrop"),
    createCharacterTextField(record, "图片资源", "picture", {
      list: ensureResourceIdDatalist("物品"),
      placeholder: "例如：物品.止血草",
      rerenderOnChange: true,
    }),
    createItemSlotTypeField(record),
    createItemTextareaField(record, "描述", "description")
  );
  basicSection.appendChild(basicGrid);
  shell.appendChild(basicSection);

  const pictureSection = createCharacterSection("图片", "Picture");
  pictureSection.appendChild(createItemPictureSection(record, pictureInfo));
  shell.appendChild(pictureSection);

  const configSection = createCharacterSection("使用与配置", "Logic");
  configSection.appendChild(createItemTabBar(record));
  configSection.appendChild(createItemTabContent(record));
  shell.appendChild(configSection);

  shell.appendChild(createItemAdvancedJsonSection(record));
  parent.appendChild(shell);
}

function getItemTypeLabel(type) {
  return ITEM_TYPE_CHOICES.find((choice) => choice.value === type)?.label || `${type || "unknown"} 未知类型`;
}

function getItemTypeShortLabel(type) {
  const label = getItemTypeLabel(type);
  return label.split(" ").slice(1).join(" ") || label;
}

function getItemCategoryLabel(category) {
  return ITEM_CATEGORY_CHOICES.find((choice) => choice.value === category)?.label.split(" ").slice(1).join(" ") || (category || "未分类");
}

function createItemSummaryPicture(record, pictureInfo) {
  const box = document.createElement("div");
  box.className = "character-summary-portrait item-summary-picture";
  if (pictureInfo.previewPath) {
    const image = document.createElement("img");
    image.src = `/api/assets/file?path=${encodeURIComponent(pictureInfo.previewPath)}&v=${Date.now()}`;
    image.alt = typeof record.name === "string" ? record.name : (record.id || "物品图片");
    box.appendChild(image);
  } else {
    const placeholder = document.createElement("div");
    placeholder.className = "character-summary-portrait-placeholder";
    placeholder.textContent = "无图片";
    box.appendChild(placeholder);
  }
  return box;
}

function createItemTextareaField(record, labelCn, key) {
  const field = createCharacterFieldShell(labelCn, key, true);
  const textarea = document.createElement("textarea");
  textarea.value = record[key] == null ? "" : String(record[key]);
  textarea.rows = 4;
  textarea.addEventListener("input", () => updateRecordField(record, key, textarea.value));
  field.appendChild(textarea);
  return field;
}

function createItemCategoryField(record) {
  return createCharacterSelectField(record, "物品分类", "category", ITEM_CATEGORY_CHOICES);
}

function createItemTypeField(record) {
  const field = createCharacterFieldShell("物品类型", "type");
  const select = document.createElement("select");
  for (const choice of ITEM_TYPE_CHOICES) {
    const option = document.createElement("option");
    option.value = choice.value;
    option.textContent = choice.label;
    option.selected = String(record.type ?? "") === choice.value;
    select.appendChild(option);
  }

  select.addEventListener("change", () => {
    applyItemTypeDefaults(record, select.value);
    syncFormToEditor();
    renderFormView();
  });

  field.appendChild(select);
  return field;
}

function createItemSlotTypeField(record) {
  const field = createCharacterFieldShell("装备部位", "slotType");
  const select = document.createElement("select");
  const placeholder = document.createElement("option");
  placeholder.value = "";
  placeholder.textContent = record.type === "equipment" ? "请选择部位" : "仅装备类型需要";
  placeholder.selected = !record.slotType;
  select.appendChild(placeholder);

  for (const choice of ITEM_SLOT_TYPE_CHOICES) {
    const option = document.createElement("option");
    option.value = choice.value;
    option.textContent = choice.label;
    option.selected = String(record.slotType ?? "") === choice.value;
    select.appendChild(option);
  }

  select.disabled = record.type !== "equipment";
  select.addEventListener("change", () => {
    updateRecordField(record, "slotType", select.value || null, { rerender: true });
  });

  field.appendChild(select);
  return field;
}

function applyItemTypeDefaults(record, type) {
  record.type = type;
  ensureItemShape(record);

  if (type === "equipment") {
    record.category = "equipment";
    if (!record.slotType) {
      record.slotType = "weapon";
    }
  } else {
    if (record.category === "equipment") {
      record.category = "normal";
    }
    if (!record.useEffects) {
      record.useEffects = [];
    }
  }
}

function ensureItemShape(record) {
  if (!Array.isArray(record.requirements)) {
    record.requirements = [];
  }
  if (!Array.isArray(record.useEffects)) {
    record.useEffects = [];
  }
  if (record.type === "equipment" && !Array.isArray(record.affixes)) {
    record.affixes = [];
  }

  if (!Number.isFinite(record.level)) {
    record.level = 1;
  }
  if (!Number.isFinite(record.price)) {
    record.price = 0;
  }
  if (!Number.isFinite(record.cooldown)) {
    record.cooldown = 0;
  }
  if (typeof record.canDrop !== "boolean") {
    record.canDrop = false;
  }
  if (typeof record.description !== "string") {
    record.description = "";
  }
  if (typeof record.category !== "string" || !record.category.trim()) {
    record.category = record.type === "equipment" ? "equipment" : "normal";
  }
}

function getItemPictureInfo(record) {
  const pictureId = typeof record.picture === "string" ? record.picture.trim() : "";
  const resource = pictureId ? state.contentIndex.resourcesById.get(pictureId) : null;
  const resourceExists = Boolean(resource);
  const assetValue = typeof resource?.value === "string" ? resource.value.trim() : "";
  const assetPath = resourceExists ? resolveResourceAssetPath(resource) : "";
  const detectedAssetValue = detectItemPictureAssetValue(record.id || "", record.name || "", pictureId);
  const detectedAssetPath = detectedAssetValue ? findAssetPath(detectedAssetValue, { art: true }) : "";
  const previewPath = assetPath || detectedAssetPath || "";

  return {
    pictureId,
    resource,
    resourceExists,
    assetValue,
    assetPath,
    assetExists: Boolean(assetPath),
    detectedAssetValue,
    detectedAssetPath,
    previewPath,
  };
}

function getAssetImageInfo(path) {
  if (!path || !isImage(path.toLowerCase())) {
    return null;
  }

  const cached = state.assetImageInfo.get(path);
  if (cached) {
    return cached;
  }

  const loading = { status: "loading" };
  state.assetImageInfo.set(path, loading);
  loadImage(`/api/assets/file?path=${encodeURIComponent(path)}&v=${Date.now()}`)
    .then((image) => {
      state.assetImageInfo.set(path, {
        status: "ready",
        width: image.naturalWidth,
        height: image.naturalHeight,
      });
      renderFormView();
    })
    .catch(() => {
      state.assetImageInfo.set(path, { status: "error" });
      renderFormView();
    });

  return loading;
}

function formatAssetImageInfo(info) {
  if (!info) {
    return "无图片";
  }

  if (info.status === "ready") {
    const suffix = info.width === 512 && info.height === 512 ? " · 标准 512" : "";
    return `${info.width} x ${info.height}${suffix}`;
  }

  if (info.status === "error") {
    return "读取失败";
  }

  return "读取中...";
}

function detectItemPictureAssetValue(id, name, pictureId = "") {
  const candidates = [];
  const strippedPictureId = pictureId.replace(/^物品\./, "");
  for (const value of [strippedPictureId, id, name, toPinyinSlug(strippedPictureId), toPinyinSlug(id), toPinyinSlug(name)]) {
    const normalized = normalizeToolSearchValue(value);
    if (!normalized) {
      continue;
    }

    candidates.push(`item/${normalized}`);
  }

  for (const candidate of candidates) {
    const found = findAssetPath(candidate, { art: true });
    if (found) {
      return normalizeToolAssetValue(found);
    }
  }

  const wanted = new Set(candidates.map((candidate) => normalizeToolSearchValue(candidate.split("/").pop() || "")));
  for (const file of state.assetFiles) {
    if (!file.path.toLowerCase().startsWith("art/item/") || !isImage(file.path.toLowerCase())) {
      continue;
    }

    const basename = normalizeToolSearchValue((file.name || file.path.split("/").pop() || "").replace(/\.[^.]+$/i, ""));
    if (wanted.has(basename)) {
      return normalizeToolAssetValue(file.path);
    }
  }

  return "";
}

function getItemValidationIssues(record) {
  const issues = [];
  const pictureInfo = getItemPictureInfo(record);

  if (!pictureInfo.pictureId) {
    issues.push(createCharacterIssue("error", "图片资源为空，请填写 picture。"));
  } else if (!pictureInfo.resourceExists) {
    issues.push(createCharacterIssue("error", `图片资源不存在：${pictureInfo.pictureId}`));
  } else if (pictureInfo.resource?.group !== "物品") {
    issues.push(createCharacterIssue("warn", `图片资源分组不是“物品”：${pictureInfo.pictureId}`));
  } else if (!pictureInfo.assetExists) {
    issues.push(createCharacterIssue("error", `物品图片不存在：${pictureInfo.assetValue || pictureInfo.pictureId}`));
  }

  if (record.type === "equipment" && record.category !== "equipment") {
    issues.push(createCharacterIssue("warn", "当前物品是 equipment，但 category 不是 equipment。"));
  }

  if (record.type !== "equipment" && record.category === "equipment") {
    issues.push(createCharacterIssue("warn", "当前物品不是 equipment，但 category 仍然是 equipment。"));
  }

  if (record.type === "equipment" && !String(record.slotType || "").trim()) {
    issues.push(createCharacterIssue("error", "装备类型物品缺少 slotType。"));
  }

  for (const [key, label] of [["level", "等级"], ["price", "价格"], ["cooldown", "冷却"]]) {
    if (!Number.isFinite(record[key])) {
      issues.push(createCharacterIssue("error", `${label} 不是数字：${key}`));
    }
  }

  appendItemRequirementIssues(issues, record.requirements);
  appendItemEffectIssues(issues, record.useEffects);
  if (record.type === "equipment") {
    appendItemAffixIssues(issues, record.affixes);
  }

  return issues;
}

function appendItemRequirementIssues(issues, requirements) {
  if (!Array.isArray(requirements)) {
    issues.push(createCharacterIssue("error", "requirements 不是数组。"));
    return;
  }

  for (const requirement of requirements) {
    if (requirement?.type === "stat") {
      if (!isKnownCharacterStatId(requirement.statId)) {
        issues.push(createCharacterIssue("error", `属性要求 statId 不存在：${requirement.statId || "空"}`));
      }
      if (!Number.isFinite(requirement.value)) {
        issues.push(createCharacterIssue("error", `属性要求数值不是数字：${requirement.statId || "stat"}`));
      }
      continue;
    }

    if (requirement?.type === "talent") {
      const talentId = typeof requirement.talentId === "string" ? requirement.talentId.trim() : "";
      if (!talentId) {
        issues.push(createCharacterIssue("error", "天赋要求缺少 talentId。"));
      } else if (!hasDefinitionOfType(talentId, "talents")) {
        issues.push(createCharacterIssue("error", `天赋要求不存在：${talentId}`, talentId, ["talents"]));
      }
      continue;
    }

    issues.push(createCharacterIssue("warn", `未识别的 requirements 类型：${requirement?.type || "空"}`));
  }
}

function appendItemEffectIssues(issues, effects) {
  if (!Array.isArray(effects)) {
    issues.push(createCharacterIssue("error", "useEffects 不是数组。"));
    return;
  }

  for (const effect of effects) {
    const effectType = effect?.type || "";
    if (effectType === "external_skill" && !hasDefinitionOfType(effect.skillId, "external-skills")) {
      issues.push(createCharacterIssue("error", `外功不存在：${effect.skillId || "空"}`, effect.skillId || "", ["external-skills"]));
      continue;
    }

    if (effectType === "internal_skill" && !hasDefinitionOfType(effect.skillId, "internal-skills")) {
      issues.push(createCharacterIssue("error", `内功不存在：${effect.skillId || "空"}`, effect.skillId || "", ["internal-skills"]));
      continue;
    }

    if (effectType === "special_skill" && !hasDefinitionOfType(effect.skillId, "special-skills")) {
      issues.push(createCharacterIssue("error", `绝技不存在：${effect.skillId || "空"}`, effect.skillId || "", ["special-skills"]));
      continue;
    }

    if (effectType === "grant_talent") {
      if (!hasDefinitionOfType(effect.talentId, "talents")) {
        issues.push(createCharacterIssue("error", `天赋不存在：${effect.talentId || "空"}`, effect.talentId || "", ["talents"]));
      }
      continue;
    }

    if (effectType === "add_buff") {
      if (!hasDefinitionOfType(effect.buffId, "buffs")) {
        issues.push(createCharacterIssue("error", `Buff 不存在：${effect.buffId || "空"}`, effect.buffId || "", ["buffs"]));
      }
      if (!Number.isFinite(effect.duration)) {
        issues.push(createCharacterIssue("error", `Buff 持续时间不是数字：${effect.buffId || "add_buff"}`));
      }
      continue;
    }

    if (effectType === "detoxify") {
      const values = Array.isArray(effect.values) ? effect.values : [];
      if (values.length !== 2 || values.some((value) => !Number.isFinite(value))) {
        issues.push(createCharacterIssue("error", "detoxify 需要两个数字 values。"));
      }
      continue;
    }

    if ([
      "add_hp",
      "add_mp",
      "add_hp_percent",
      "add_mp_percent",
      "add_maxhp",
      "add_maxmp",
      "add_rage",
    ].includes(effectType)) {
      if (!Number.isFinite(effect.value)) {
        issues.push(createCharacterIssue("error", `${effectType} 的 value 不是数字。`));
      }
      continue;
    }

    if (!ITEM_EFFECT_TYPE_CHOICES.some((choice) => choice.value === effectType)) {
      issues.push(createCharacterIssue("warn", `未识别的 useEffect 类型：${effectType || "空"}`));
    }
  }
}

function appendItemAffixIssues(issues, affixes) {
  if (!Array.isArray(affixes)) {
    issues.push(createCharacterIssue("error", "affixes 不是数组。"));
    return;
  }

  for (const affix of affixes) {
    switch (affix?.type) {
      case "stat_modifier":
        if (!ITEM_AFFIX_STAT_CHOICES.some((choice) => choice.value === affix.stat)) {
          issues.push(createCharacterIssue("error", `装备词缀属性不存在：${affix.stat || "空"}`));
        }
        if (!Number.isFinite(affix?.value?.delta)) {
          issues.push(createCharacterIssue("error", `装备词缀 delta 不是数字：${affix.stat || "stat_modifier"}`));
        }
        break;
      case "grant_talent":
        if (!hasDefinitionOfType(affix.talentId, "talents")) {
          issues.push(createCharacterIssue("error", `装备词缀天赋不存在：${affix.talentId || "空"}`, affix.talentId || "", ["talents"]));
        }
        break;
      case "skill_bonus_modifier":
        if (!hasDefinitionOfType(affix.skillId, "external-skills")) {
          issues.push(createCharacterIssue("error", `技能加成对应外功不存在：${affix.skillId || "空"}`, affix.skillId || "", ["external-skills"]));
        }
        if (!Number.isFinite(affix?.value?.delta)) {
          issues.push(createCharacterIssue("error", `技能加成 delta 不是数字：${affix.skillId || "skill_bonus_modifier"}`));
        }
        break;
      case "legend_skill_chance_modifier":
        if (!hasDefinitionOfType(affix.skillId, "legend-skills")) {
          issues.push(createCharacterIssue("error", `传奇招式不存在：${affix.skillId || "空"}`, affix.skillId || "", ["legend-skills"]));
        }
        if (!Number.isFinite(affix?.value?.delta)) {
          issues.push(createCharacterIssue("error", `传奇招式 delta 不是数字：${affix.skillId || "legend_skill_chance_modifier"}`));
        }
        break;
      case "weapon_bonus_modifier":
        if (!ITEM_WEAPON_TYPE_CHOICES.some((choice) => choice.value === affix.weaponType)) {
          issues.push(createCharacterIssue("error", `武学类别不存在：${affix.weaponType || "空"}`));
        }
        if (!Number.isFinite(affix?.value?.delta)) {
          issues.push(createCharacterIssue("error", `武学类别加成 delta 不是数字：${affix.weaponType || "weapon_bonus_modifier"}`));
        }
        break;
      case "grant_model":
        if (!String(affix.modelId || "").trim()) {
          issues.push(createCharacterIssue("error", "grant_model 缺少 modelId。"));
        }
        break;
      default:
        issues.push(createCharacterIssue("warn", `未识别的 affix 类型：${affix?.type || "空"}`));
        break;
    }
  }
}

function isKnownCharacterStatId(statId) {
  return CHARACTER_STAT_FIELDS.some((stat) => stat.key === statId);
}

function createItemPictureSection(record, pictureInfo) {
  const wrapper = document.createElement("div");
  wrapper.className = "character-portrait-section";
  const imageInfo = getAssetImageInfo(pictureInfo.previewPath);

  const preview = document.createElement("div");
  preview.className = `character-portrait-preview item-picture-preview ${pictureInfo.assetExists ? "ok" : "missing"}`;

  if (pictureInfo.previewPath) {
    const image = document.createElement("img");
    image.src = `/api/assets/file?path=${encodeURIComponent(pictureInfo.previewPath)}&v=${Date.now()}`;
    image.alt = pictureInfo.pictureId || "物品图片";
    preview.appendChild(image);
  } else {
    const empty = document.createElement("div");
    empty.className = "character-portrait-preview-empty";
    empty.textContent = "缺图";
    preview.appendChild(empty);
  }

  const meta = document.createElement("div");
  meta.className = "character-portrait-meta";
  meta.append(
    createCharacterMetaRow("图片资源", pictureInfo.pictureId || "未填写"),
    createCharacterMetaRow("真实图片", pictureInfo.assetPath || "未找到"),
    createCharacterMetaRow("当前尺寸", formatAssetImageInfo(imageInfo)),
    createCharacterMetaRow("自动检测路径", pictureInfo.detectedAssetValue || "未检测到"),
    createCharacterMetaRow("资源状态", pictureInfo.resourceExists ? "已存在" : "resources.json 中不存在"),
  );

  if (!pictureInfo.assetExists) {
    const warning = document.createElement("div");
    warning.className = "character-portrait-warning";
    warning.textContent = pictureInfo.pictureId
      ? `缺图提示：${pictureInfo.pictureId} 还没有可用图片。`
      : "缺图提示：当前物品还没有填写 picture。";
    meta.appendChild(warning);
  }

  const actions = document.createElement("div");
  actions.className = "character-portrait-actions";
  const uploadInput = document.createElement("input");
  uploadInput.type = "file";
  uploadInput.accept = ".png,.jpg,.jpeg,.webp,image/png,image/jpeg,image/webp";
  uploadInput.hidden = true;
  uploadInput.addEventListener("change", async () => {
    const [file] = Array.from(uploadInput.files || []);
    uploadInput.value = "";
    if (!file) {
      return;
    }

    uploadButton.disabled = true;
    try {
      const pictureId = getBindableItemPictureId(record);
      const result = await uploadItemImageAndBind(record, file, pictureId);
      await loadAssetFiles();
      await loadDataFiles();
      await rebuildContentIndex();
      record.picture = result.pictureId;
      syncFormToEditor();
      showValidation(result.validation.ok, `已上传并绑定：${result.pictureId} -> ${result.assetPath}`);
      renderFormView();
    } catch (error) {
      showValidation(false, error instanceof Error ? error.message : String(error));
    } finally {
      uploadButton.disabled = false;
    }
  });

  const pickerButton = document.createElement("button");
  pickerButton.type = "button";
  pickerButton.className = "primary";
  pickerButton.textContent = "从图库选择";
  pickerButton.addEventListener("click", () => {
    openItemPicturePicker(pictureInfo.previewPath);
  });

  const openButton = document.createElement("button");
  openButton.type = "button";
  openButton.textContent = "打开图片";
  openButton.disabled = !pictureInfo.previewPath;
  openButton.addEventListener("click", () => {
    if (!pictureInfo.previewPath) {
      return;
    }

    setMode("assets");
    openAssetFile(pictureInfo.previewPath);
  });

  const normalizeButton = document.createElement("button");
  normalizeButton.type = "button";
  normalizeButton.textContent = "手动规范512";
  normalizeButton.disabled = !pictureInfo.previewPath || !pictureInfo.previewPath.toLowerCase().endsWith(".png");
  normalizeButton.addEventListener("click", async () => {
    normalizeButton.disabled = true;
    try {
      await normalizePortraitAsset(pictureInfo.previewPath);
      await loadAssetFiles();
      renderFormView();
      showValidation(true, `已规范化图片：${pictureInfo.previewPath}`);
    } catch (error) {
      showValidation(false, formatNormalizePortraitError(error));
    } finally {
      normalizeButton.disabled = !pictureInfo.previewPath || !pictureInfo.previewPath.toLowerCase().endsWith(".png");
    }
  });

  const uploadButton = document.createElement("button");
  uploadButton.type = "button";
  uploadButton.className = "primary";
  uploadButton.textContent = "上传图片并绑定当前物品";
  uploadButton.addEventListener("click", () => {
    uploadInput.click();
  });

  const createResourceButton = document.createElement("button");
  createResourceButton.type = "button";
  createResourceButton.className = "primary";
  createResourceButton.textContent = "一键创建物品资源";
  createResourceButton.disabled = pictureInfo.resourceExists || !pictureInfo.pictureId || !pictureInfo.detectedAssetValue;
  createResourceButton.addEventListener("click", async () => {
    createResourceButton.disabled = true;
    try {
      const result = await createItemResource(pictureInfo.pictureId, pictureInfo.detectedAssetValue);
      await loadDataFiles();
      await rebuildContentIndex();
      showValidation(result.validation.ok, result.validation.message);
      renderFormView();
    } catch (error) {
      showValidation(false, error instanceof Error ? error.message : String(error));
    } finally {
      createResourceButton.disabled = pictureInfo.resourceExists || !pictureInfo.pictureId || !pictureInfo.detectedAssetValue;
    }
  });

  actions.append(pickerButton, uploadButton, openButton, normalizeButton, createResourceButton, uploadInput);
  preview.append(meta, actions);
  wrapper.appendChild(preview);
  return wrapper;
}

function createItemTabBar(record) {
  const tabBar = document.createElement("div");
  tabBar.className = "character-tab-bar";
  const tabs = [
    { value: "requirements", label: "使用条件" },
    { value: "effects", label: "使用效果" },
    { value: "affixes", label: record.type === "equipment" ? "装备词缀" : "装备词缀（仅装备）" },
  ];

  for (const tab of tabs) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "character-tab-button";
    button.classList.toggle("active", state.itemTab === tab.value);
    button.textContent = tab.label;
    button.addEventListener("click", () => {
      state.itemTab = tab.value;
      renderFormView();
    });
    tabBar.appendChild(button);
  }

  return tabBar;
}

function createItemTabContent(record) {
  const wrapper = document.createElement("div");
  wrapper.className = "character-tab-content";

  switch (state.itemTab) {
    case "effects":
      wrapper.appendChild(createItemUseEffectEditor(record));
      break;
    case "affixes":
      wrapper.appendChild(createItemAffixEditor(record));
      break;
    case "requirements":
    default:
      wrapper.appendChild(createItemRequirementEditor(record));
      break;
  }

  return wrapper;
}

function createItemRequirementEditor(record) {
  ensureItemShape(record);
  const wrapper = document.createElement("div");
  wrapper.className = "character-table-editor";

  const summary = document.createElement("div");
  summary.className = "character-list-summary";
  summary.textContent = record.requirements.length > 0
    ? record.requirements.map(formatItemRequirementSummary).join(" / ")
    : "未配置使用条件";

  const list = document.createElement("div");
  list.className = "item-array-list";
  for (const requirement of record.requirements) {
    list.appendChild(createItemRequirementCard(record, requirement));
  }

  const addRow = document.createElement("div");
  addRow.className = "character-add-row";
  const addStat = document.createElement("button");
  addStat.type = "button";
  addStat.textContent = "+ 属性要求";
  addStat.addEventListener("click", () => {
    record.requirements.push(createDefaultItemRequirement("stat"));
    syncFormToEditor();
    renderFormView();
  });
  const addTalent = document.createElement("button");
  addTalent.type = "button";
  addTalent.textContent = "+ 天赋要求";
  addTalent.addEventListener("click", () => {
    record.requirements.push(createDefaultItemRequirement("talent"));
    syncFormToEditor();
    renderFormView();
  });
  addRow.append(addStat, addTalent);

  wrapper.append(summary, list, addRow);
  return wrapper;
}

function createItemRequirementCard(record, requirement) {
  normalizeItemRequirement(requirement);
  const card = createItemArrayCard(record.requirements, requirement, formatItemRequirementSummary(requirement));
  const grid = document.createElement("div");
  grid.className = "item-array-grid";
  grid.append(
    createNestedSelectField(requirement, "条件类型", "type", ITEM_REQUIREMENT_TYPE_CHOICES, {
      rerenderOnChange: true,
      onAfterChange: () => {
        replacePlainObject(requirement, createDefaultItemRequirement(requirement.type));
      },
    })
  );

  if (requirement.type === "talent") {
    grid.append(
      createNestedTextField(requirement, "天赋", "talentId", {
        list: ensureDefinitionIdDatalist("talents"),
        placeholder: "例如：毒圣",
      })
    );
  } else {
    grid.append(
      createNestedSelectField(requirement, "属性", "statId", CHARACTER_STAT_FIELDS.map((stat) => ({
        value: stat.key,
        label: `${stat.key} ${stat.label}`,
      }))),
      createNestedNumberField(requirement, "要求值", "value", {
        fallback: 0,
      })
    );
  }

  card.body.appendChild(grid);
  return card.root;
}

function createItemUseEffectEditor(record) {
  ensureItemShape(record);
  const wrapper = document.createElement("div");
  wrapper.className = "character-table-editor";

  const summary = document.createElement("div");
  summary.className = "character-list-summary";
  summary.textContent = record.useEffects.length > 0
    ? record.useEffects.map(formatItemEffectSummary).join(" / ")
    : "未配置使用效果";

  const list = document.createElement("div");
  list.className = "item-array-list";
  for (const effect of record.useEffects) {
    list.appendChild(createItemUseEffectCard(record, effect));
  }

  const addRow = document.createElement("div");
  addRow.className = "character-add-row";
  const addButton = document.createElement("button");
  addButton.type = "button";
  addButton.textContent = "+ 添加效果";
  addButton.addEventListener("click", () => {
    record.useEffects.push(createDefaultItemEffect("add_hp"));
    syncFormToEditor();
    renderFormView();
  });
  addRow.appendChild(addButton);

  wrapper.append(summary, list, addRow);
  return wrapper;
}

function createItemUseEffectCard(record, effect) {
  normalizeItemEffect(effect);
  const card = createItemArrayCard(record.useEffects, effect, formatItemEffectSummary(effect));
  const grid = document.createElement("div");
  grid.className = "item-array-grid";
  grid.append(
    createNestedSelectField(effect, "效果类型", "type", ITEM_EFFECT_TYPE_CHOICES, {
      rerenderOnChange: true,
      onAfterChange: () => {
        replacePlainObject(effect, createDefaultItemEffect(effect.type));
      },
    })
  );

  switch (effect.type) {
    case "external_skill":
      grid.append(
        createNestedTextField(effect, "外功", "skillId", {
          list: ensureDefinitionIdDatalist("external-skills"),
          placeholder: "例如：罗汉拳",
        }),
        createNestedNumberField(effect, "等级", "level", { fallback: 1 })
      );
      break;
    case "internal_skill":
      grid.append(
        createNestedTextField(effect, "内功", "skillId", {
          list: ensureDefinitionIdDatalist("internal-skills"),
          placeholder: "例如：基本内功",
        }),
        createNestedNumberField(effect, "等级", "level", { fallback: 1 })
      );
      break;
    case "special_skill":
      grid.append(
        createNestedTextField(effect, "绝技", "skillId", {
          list: ensureDefinitionIdDatalist("special-skills"),
          placeholder: "例如：笑傲江湖曲",
        })
      );
      break;
    case "grant_talent":
      grid.append(
        createNestedTextField(effect, "天赋", "talentId", {
          list: ensureDefinitionIdDatalist("talents"),
          placeholder: "例如：毒圣",
        })
      );
      break;
    case "add_buff":
      grid.append(
        createNestedTextField(effect, "Buff", "buffId", {
          list: ensureDefinitionIdDatalist("buffs"),
          placeholder: "例如：中毒",
        }),
        createNestedNumberField(effect, "持续时间", "duration", { fallback: 1 })
      );
      break;
    case "detoxify":
      if (!Array.isArray(effect.values)) {
        effect.values = [0, 0];
      }
      grid.append(
        createArrayValueNumberField(effect.values, 0, "数值 1"),
        createArrayValueNumberField(effect.values, 1, "数值 2")
      );
      break;
    default:
      grid.append(createNestedNumberField(effect, "数值", "value", { fallback: 0 }));
      break;
  }

  card.body.appendChild(grid);
  return card.root;
}

function createItemAffixEditor(record) {
  ensureItemShape(record);
  const wrapper = document.createElement("div");
  wrapper.className = "character-table-editor";

  if (record.type !== "equipment") {
    const note = document.createElement("div");
    note.className = "form-summary character-form-summary";
    note.textContent = "当前物品不是 equipment，装备词缀区域仅在装备类型下生效。切换 type 为 equipment 后可直接编辑 slotType 与 affixes。";
    wrapper.appendChild(note);
    return wrapper;
  }

  const summary = document.createElement("div");
  summary.className = "character-list-summary";
  summary.textContent = record.affixes.length > 0
    ? record.affixes.map(formatItemAffixSummary).join(" / ")
    : "未配置装备词缀";

  const list = document.createElement("div");
  list.className = "item-array-list";
  for (const affix of record.affixes) {
    list.appendChild(createItemAffixCard(record, affix));
  }

  const addRow = document.createElement("div");
  addRow.className = "character-add-row";
  const addButton = document.createElement("button");
  addButton.type = "button";
  addButton.textContent = "+ 添加词缀";
  addButton.addEventListener("click", () => {
    record.affixes.push(createDefaultItemAffix("stat_modifier"));
    syncFormToEditor();
    renderFormView();
  });
  addRow.appendChild(addButton);

  wrapper.append(summary, list, addRow);
  return wrapper;
}

function createItemAffixCard(record, affix) {
  normalizeItemAffix(affix);
  const card = createItemArrayCard(record.affixes, affix, formatItemAffixSummary(affix));
  const grid = document.createElement("div");
  grid.className = "item-array-grid";
  grid.append(
    createNestedSelectField(affix, "词缀类型", "type", ITEM_AFFIX_TYPE_CHOICES, {
      rerenderOnChange: true,
      onAfterChange: () => {
        replacePlainObject(affix, createDefaultItemAffix(affix.type));
      },
    })
  );

  switch (affix.type) {
    case "grant_talent":
      grid.append(createNestedTextField(affix, "天赋", "talentId", {
        list: ensureDefinitionIdDatalist("talents"),
        placeholder: "例如：刀系装备",
      }));
      break;
    case "grant_model":
      grid.append(
        createNestedTextField(affix, "模型", "modelId", {
          placeholder: "例如：fanseng",
        }),
        createNestedNumberField(affix, "优先级", "priority", { fallback: 0 }),
        createNestedTextField(affix, "描述", "description", {
          placeholder: "例如：血刀门弟子",
        })
      );
      break;
    case "skill_bonus_modifier":
      grid.append(
        createNestedTextField(affix, "外功", "skillId", {
          list: ensureDefinitionIdDatalist("external-skills"),
          placeholder: "例如：八卦刀法",
        }),
        createNestedNumberField(affix.value, "加成", "delta", { fallback: 0 })
      );
      break;
    case "weapon_bonus_modifier":
      grid.append(
        createNestedSelectField(affix, "武学类别", "weaponType", ITEM_WEAPON_TYPE_CHOICES),
        createNestedNumberField(affix.value, "加成", "delta", { fallback: 0 })
      );
      break;
    case "legend_skill_chance_modifier":
      grid.append(
        createNestedTextField(affix, "传奇招式", "skillId", {
          list: ensureDefinitionIdDatalist("legend-skills"),
          placeholder: "例如：小人物的愤怒",
        }),
        createNestedNumberField(affix.value, "触发率加成", "delta", { fallback: 0 })
      );
      break;
    case "stat_modifier":
    default:
      grid.append(
        createNestedSelectField(affix, "属性", "stat", ITEM_AFFIX_STAT_CHOICES),
        createNestedNumberField(affix.value, "数值", "delta", { fallback: 0 })
      );
      break;
  }

  card.body.appendChild(grid);
  return card.root;
}

function createItemArrayCard(collection, entry, summaryText) {
  const root = document.createElement("div");
  root.className = "item-array-card";
  const header = document.createElement("div");
  header.className = "item-array-card-header";
  const title = document.createElement("div");
  title.className = "item-array-card-title";
  title.textContent = summaryText;
  const remove = document.createElement("button");
  remove.type = "button";
  remove.textContent = "删除";
  remove.addEventListener("click", () => {
    const index = collection.indexOf(entry);
    if (index >= 0) {
      collection.splice(index, 1);
      syncFormToEditor();
      renderFormView();
    }
  });
  header.append(title, remove);
  const body = document.createElement("div");
  body.className = "item-array-card-body";
  root.append(header, body);
  return { root, body };
}

function createNestedTextField(target, labelCn, key, options = {}) {
  const field = createCharacterFieldShell(labelCn, key, options.full === true);
  const input = document.createElement("input");
  input.type = "text";
  input.value = target[key] == null ? "" : String(target[key]);
  input.placeholder = options.placeholder || "";
  if (options.list) {
    input.setAttribute("list", options.list);
  }

  input.addEventListener("input", () => {
    target[key] = input.value;
    syncFormToEditor();
    renderCharacterCheckTool();
  });
  if (options.rerenderOnChange) {
    input.addEventListener("change", () => renderFormView());
  }

  field.appendChild(input);
  return field;
}

function createNestedNumberField(target, labelCn, key, options = {}) {
  const field = createCharacterFieldShell(labelCn, key, options.full === true);
  const input = document.createElement("input");
  input.type = "number";
  input.value = Number.isFinite(target[key]) ? String(target[key]) : String(options.fallback ?? 0);
  input.addEventListener("input", () => {
    target[key] = parseNumberInputValue(input.value, options.fallback ?? 0);
    syncFormToEditor();
    renderCharacterCheckTool();
  });
  if (options.rerenderOnChange) {
    input.addEventListener("change", () => renderFormView());
  }
  field.appendChild(input);
  return field;
}

function createNestedSelectField(target, labelCn, key, choices, options = {}) {
  const field = createCharacterFieldShell(labelCn, key, options.full === true);
  const select = document.createElement("select");
  for (const choice of choices) {
    const option = document.createElement("option");
    option.value = choice.value;
    option.textContent = choice.label;
    option.selected = String(target[key] ?? "") === choice.value;
    select.appendChild(option);
  }
  select.addEventListener("change", () => {
    target[key] = select.value;
    if (typeof options.onAfterChange === "function") {
      options.onAfterChange();
    }
    syncFormToEditor();
    if (options.rerenderOnChange) {
      renderFormView();
    } else {
      renderCharacterCheckTool();
    }
  });
  field.appendChild(select);
  return field;
}

function createArrayValueNumberField(values, index, labelCn) {
  const field = createCharacterFieldShell(labelCn, `values[${index}]`);
  const input = document.createElement("input");
  input.type = "number";
  input.value = Number.isFinite(values[index]) ? String(values[index]) : "0";
  input.addEventListener("input", () => {
    values[index] = parseNumberInputValue(input.value, 0);
    syncFormToEditor();
    renderCharacterCheckTool();
  });
  field.appendChild(input);
  return field;
}

function replacePlainObject(target, next) {
  for (const key of Object.keys(target)) {
    delete target[key];
  }
  Object.assign(target, next);
}

function createDefaultItemRequirement(type) {
  return type === "talent"
    ? { type: "talent", talentId: "" }
    : { type: "stat", statId: "wuxing", value: 10 };
}

function normalizeItemRequirement(requirement) {
  if (!requirement || typeof requirement !== "object") {
    return;
  }

  if (requirement.type === "talent") {
    if (typeof requirement.talentId !== "string") {
      requirement.talentId = "";
    }
    return;
  }

  requirement.type = "stat";
  if (!isKnownCharacterStatId(requirement.statId)) {
    requirement.statId = "wuxing";
  }
  if (!Number.isFinite(requirement.value)) {
    requirement.value = 10;
  }
}

function formatItemRequirementSummary(requirement) {
  if (requirement?.type === "talent") {
    return `需天赋 ${requirement.talentId || "未填写"}`;
  }

  const statLabel = CHARACTER_STAT_FIELDS.find((stat) => stat.key === requirement?.statId)?.label || requirement?.statId || "属性";
  return `${statLabel} >= ${getDisplayNumber(requirement?.value, 0)}`;
}

function createDefaultItemEffect(type) {
  switch (type) {
    case "external_skill":
      return { type, skillId: "", level: 1 };
    case "internal_skill":
      return { type, skillId: "", level: 1 };
    case "special_skill":
      return { type, skillId: "" };
    case "grant_talent":
      return { type, talentId: "" };
    case "add_buff":
      return { type, buffId: "", duration: 1 };
    case "detoxify":
      return { type, values: [5, 5] };
    default:
      return { type, value: 0 };
  }
}

function normalizeItemEffect(effect) {
  if (!effect || typeof effect !== "object") {
    return;
  }

  const normalized = createDefaultItemEffect(effect.type || "add_hp");
  for (const [key, value] of Object.entries(normalized)) {
    if (effect[key] == null || (Array.isArray(value) && !Array.isArray(effect[key]))) {
      effect[key] = structuredCloneCompat(value);
    }
  }
}

function formatItemEffectSummary(effect) {
  switch (effect?.type) {
    case "external_skill":
      return `外功 ${effect.skillId || "未填写"} Lv${getDisplayNumber(effect.level, 1)}`;
    case "internal_skill":
      return `内功 ${effect.skillId || "未填写"} Lv${getDisplayNumber(effect.level, 1)}`;
    case "special_skill":
      return `绝技 ${effect.skillId || "未填写"}`;
    case "grant_talent":
      return `获得天赋 ${effect.talentId || "未填写"}`;
    case "add_buff":
      return `Buff ${effect.buffId || "未填写"} ${getDisplayNumber(effect.duration, 1)} 回合`;
    case "detoxify":
      return `解毒 ${Array.isArray(effect.values) ? effect.values.join(" / ") : "未填写"}`;
    default:
      return `${effect?.type || "效果"} ${getDisplayNumber(effect?.value, 0)}`;
  }
}

function createDefaultItemAffix(type) {
  switch (type) {
    case "grant_talent":
      return { type, talentId: "" };
    case "grant_model":
      return { type, modelId: "", priority: 0, description: "" };
    case "skill_bonus_modifier":
      return { type, skillId: "", value: { op: "add", delta: 0 } };
    case "weapon_bonus_modifier":
      return { type, weaponType: "qimen", value: { op: "add", delta: 0 } };
    case "legend_skill_chance_modifier":
      return { type, skillId: "", value: { op: "add", delta: 0 } };
    case "stat_modifier":
    default:
      return { type: "stat_modifier", stat: "attack", value: { op: "add", delta: 0 } };
  }
}

function normalizeItemAffix(affix) {
  if (!affix || typeof affix !== "object") {
    return;
  }

  const normalized = createDefaultItemAffix(affix.type || "stat_modifier");
  for (const [key, value] of Object.entries(normalized)) {
    if (affix[key] == null || (typeof value === "object" && value !== null && (typeof affix[key] !== "object" || affix[key] === null))) {
      affix[key] = structuredCloneCompat(value);
    }
  }

  if (!affix.value || typeof affix.value !== "object" || Array.isArray(affix.value)) {
    affix.value = { op: "add", delta: 0 };
  }
  if (typeof affix.value.op !== "string") {
    affix.value.op = "add";
  }
  if (!Number.isFinite(affix.value.delta)) {
    affix.value.delta = 0;
  }
}

function formatItemAffixSummary(affix) {
  switch (affix?.type) {
    case "grant_talent":
      return `赋予天赋 ${affix.talentId || "未填写"}`;
    case "grant_model":
      return `模型 ${affix.modelId || "未填写"}`;
    case "skill_bonus_modifier":
      return `外功加成 ${affix.skillId || "未填写"} +${getDisplayNumber(affix?.value?.delta, 0)}`;
    case "weapon_bonus_modifier":
      return `类别加成 ${affix.weaponType || "未填写"} +${getDisplayNumber(affix?.value?.delta, 0)}`;
    case "legend_skill_chance_modifier":
      return `传奇招式 ${affix.skillId || "未填写"} +${getDisplayNumber(affix?.value?.delta, 0)}`;
    case "stat_modifier":
    default:
      return `${affix.stat || "属性"} +${getDisplayNumber(affix?.value?.delta, 0)}`;
  }
}

function createItemAdvancedJsonSection(record) {
  const section = createCharacterSection("高级 JSON", "Advanced");
  const details = document.createElement("details");
  details.className = "character-advanced-json";
  const summary = document.createElement("summary");
  summary.textContent = "展开原始物品 JSON";
  const textarea = document.createElement("textarea");
  textarea.value = JSON.stringify(record, null, 2);
  textarea.addEventListener("change", () => {
    try {
      const parsed = JSON.parse(textarea.value);
      state.formRecords[state.selectedRecordIndex] = parsed;
      syncFormToEditor();
      renderFormView();
    } catch (error) {
      textarea.setCustomValidity(error instanceof Error ? error.message : String(error));
      textarea.reportValidity();
    }
  });
  details.append(summary, textarea);
  section.appendChild(details);
  return section;
}

function getCharacterFilters() {
  return [
    { value: "all", label: "全部" },
    { value: "dialogue", label: "仅对白" },
    { value: "joinable", label: "可入队" },
    { value: "battle", label: "可战斗" },
    { value: "missingPortrait", label: "缺头像" },
    { value: "incomplete", label: "配置不完整" },
  ];
}

function matchesCharacterSearch(record, query) {
  if (!query) {
    return true;
  }

  const haystack = [
    record.id,
    record.name,
    record.portrait,
    record.model,
    record.growTemplate,
    record.gender,
    JSON.stringify(record),
  ].filter(Boolean).join(" ").toLowerCase();

  return haystack.includes(query);
}

function matchesCharacterFilter(record, filter) {
  const classification = getCharacterUiClassification(record);
  const issues = getCharacterValidationIssues(record);
  const portraitInfo = getCharacterPortraitInfo(record);

  switch (filter) {
    case "dialogue":
      return classification.key === "dialogue";
    case "joinable":
      return classification.key === "joinable" || classification.key === "partner";
    case "battle":
      return record.arenaEnabled === true;
    case "missingPortrait":
      return !portraitInfo.resourceExists || !portraitInfo.assetExists;
    case "incomplete":
      return issues.length > 0;
    case "all":
    default:
      return true;
  }
}

function createCharacterSection(titleCn, titleEn) {
  const section = document.createElement("section");
  section.className = "character-section";
  const heading = document.createElement("div");
  heading.className = "character-section-title";
  heading.innerHTML = `${escapeHtml(titleCn)} <span>${escapeHtml(titleEn)}</span>`;
  section.appendChild(heading);
  return section;
}

function createCharacterFieldShell(labelCn, key, full = false) {
  const field = document.createElement("label");
  field.className = `form-field character-field ${full ? "full" : ""}`;
  const label = document.createElement("span");
  label.className = "character-field-label";
  label.innerHTML = `${escapeHtml(labelCn)} <code>${escapeHtml(key)}</code>`;
  field.appendChild(label);
  return field;
}

function createCharacterTextField(record, labelCn, key, options = {}) {
  const field = createCharacterFieldShell(labelCn, key, options.full === true);
  const input = document.createElement("input");
  input.type = "text";
  input.value = record[key] == null ? "" : String(record[key]);
  input.placeholder = options.placeholder || "";
  if (options.list) {
    input.setAttribute("list", options.list);
  }

  input.addEventListener("input", () => {
    const value = input.value.trim();
    updateRecordField(record, key, options.nullable && !value ? null : input.value);
  });
  if (options.rerenderOnChange) {
    input.addEventListener("change", () => renderFormView());
  }

  field.appendChild(input);
  return field;
}

function createCharacterNumberField(record, labelCn, key, options = {}) {
  const field = createCharacterFieldShell(labelCn, key, options.full === true);
  const input = document.createElement("input");
  input.type = "number";
  input.value = String(getDisplayNumber(record[key], options.fallback ?? 0));
  if (Number.isFinite(options.min)) {
    input.min = String(options.min);
  }
  if (Number.isFinite(options.max)) {
    input.max = String(options.max);
  }

  input.addEventListener("input", () => {
    updateRecordField(record, key, parseNumberInputValue(input.value, options.fallback ?? 0));
  });
  if (options.rerenderOnChange) {
    input.addEventListener("change", () => renderFormView());
  }

  field.appendChild(input);
  return field;
}

function createCharacterSelectField(record, labelCn, key, choices) {
  const field = createCharacterFieldShell(labelCn, key);
  const select = document.createElement("select");
  for (const choice of choices) {
    const option = document.createElement("option");
    option.value = choice.value;
    option.textContent = choice.label;
    option.selected = String(record[key] ?? "") === choice.value;
    select.appendChild(option);
  }

  select.addEventListener("change", () => {
    updateRecordField(record, key, select.value, { rerender: true });
  });

  field.appendChild(select);
  return field;
}

function createCharacterCheckboxField(record, labelCn, key, displayKey) {
  const field = createCharacterFieldShell(labelCn, displayKey || key);
  const wrapper = document.createElement("label");
  wrapper.className = "character-checkbox";
  const input = document.createElement("input");
  input.type = "checkbox";
  input.checked = record[key] === true;
  const text = document.createElement("span");
  text.textContent = record[key] === true ? "是" : "否";
  input.addEventListener("change", () => {
    text.textContent = input.checked ? "是" : "否";
    updateRecordField(record, key, input.checked, { rerender: true });
  });
  wrapper.append(input, text);
  field.appendChild(wrapper);
  return field;
}

function ensureCharacterShape(record) {
  if (!record.stats || typeof record.stats !== "object" || Array.isArray(record.stats)) {
    record.stats = {};
  }

  for (const stat of CHARACTER_STAT_FIELDS) {
    if (!Number.isFinite(record.stats[stat.key])) {
      record.stats[stat.key] = stat.key.startsWith("max_") ? 100 : 10;
    }
  }

  if (!Array.isArray(record.talentIds)) {
    record.talentIds = [];
  }
  if (!Array.isArray(record.specialSkillIds)) {
    record.specialSkillIds = [];
  }
  if (!Array.isArray(record.equipmentIds)) {
    record.equipmentIds = [];
  }
  if (!Array.isArray(record.externalSkills)) {
    record.externalSkills = [];
  }
  if (!Array.isArray(record.internalSkills)) {
    record.internalSkills = [];
  }
}

function createCharacterPortraitHero(record, portraitInfo) {
  const box = document.createElement("div");
  box.className = "character-summary-portrait";
  const previewPath = portraitInfo.previewPath;
  if (previewPath) {
    const image = document.createElement("img");
    image.src = `/api/assets/file?path=${encodeURIComponent(previewPath)}&v=${Date.now()}`;
    image.alt = typeof record.name === "string" ? record.name : (record.id || "角色头像");
    box.appendChild(image);
  } else {
    const placeholder = document.createElement("div");
    placeholder.className = "character-summary-portrait-placeholder";
    placeholder.textContent = "无头像";
    box.appendChild(placeholder);
  }
  return box;
}

function createPill(text, tone = "") {
  const pill = document.createElement("span");
  pill.className = `character-pill ${tone}`;
  pill.textContent = text;
  return pill;
}

function getCharacterUiClassification(record) {
  const hasGrowTemplate = typeof record.growTemplate === "string" && record.growTemplate.trim().length > 0;
  const hasAnySkills = Array.isArray(record.externalSkills) && record.externalSkills.length > 0
    || Array.isArray(record.internalSkills) && record.internalSkills.length > 0
    || Array.isArray(record.specialSkillIds) && record.specialSkillIds.length > 0;
  const hasTalents = Array.isArray(record.talentIds) && record.talentIds.length > 0;
  const hasEquipment = Array.isArray(record.equipmentIds) && record.equipmentIds.length > 0;

  if (hasGrowTemplate && (hasAnySkills || hasTalents || hasEquipment)) {
    return { key: "partner", label: "完整伙伴" };
  }

  if (record.arenaEnabled === true) {
    return { key: "battle", label: "可战斗 NPC" };
  }

  if (hasGrowTemplate) {
    return { key: "joinable", label: "可入队角色" };
  }

  return { key: "dialogue", label: "对白角色" };
}

function formatGenderLabel(gender) {
  switch (gender) {
    case "male":
      return "男";
    case "female":
      return "女";
    default:
      return "中立";
  }
}

function getDisplayNumber(value, fallback) {
  return Number.isFinite(value) ? value : fallback;
}

function parseNumberInputValue(value, fallback) {
  const number = Number(value);
  return Number.isFinite(number) ? number : fallback;
}

function getCharacterPortraitInfo(record) {
  const portraitId = typeof record.portrait === "string" ? record.portrait.trim() : "";
  const resource = portraitId ? state.contentIndex.resourcesById.get(portraitId) : null;
  const resourceExists = Boolean(resource);
  const assetValue = typeof resource?.value === "string" ? resource.value.trim() : "";
  const assetPath = resourceExists ? resolveResourceAssetPath(resource) : "";
  const detectedAssetValue = detectSpeakerPortraitAssetValue(record.id || "", record.name || "");
  const detectedAssetPath = detectedAssetValue ? findAssetPath(detectedAssetValue, { art: true }) : "";
  const previewPath = assetPath || detectedAssetPath || "";

  return {
    portraitId,
    resource,
    resourceExists,
    assetValue,
    assetPath,
    assetExists: Boolean(assetPath),
    detectedAssetValue,
    detectedAssetPath,
    previewPath,
  };
}

function getCharacterValidationIssues(record) {
  const issues = [];
  const portraitInfo = getCharacterPortraitInfo(record);
  const classification = getCharacterUiClassification(record);
  const stats = record.stats && typeof record.stats === "object" && !Array.isArray(record.stats) ? record.stats : {};

  if (!portraitInfo.portraitId) {
    issues.push(createCharacterIssue("error", "头像资源为空，请填写 portrait。"));
  } else if (!portraitInfo.resourceExists) {
    issues.push(createCharacterIssue("error", `头像资源不存在：${portraitInfo.portraitId}`));
  } else if (!portraitInfo.assetExists) {
    issues.push(createCharacterIssue("error", `头像图片不存在：${portraitInfo.assetValue || portraitInfo.portraitId}`));
  }

  if (record.arenaEnabled === true && !String(record.model || "").trim()) {
    issues.push(createCharacterIssue("error", "此角色可战斗，但没有模型 model。"));
  }

  if ((classification.key === "joinable" || classification.key === "partner") && !String(record.growTemplate || "").trim()) {
    issues.push(createCharacterIssue("error", "此角色可入队，但没有成长模板 growTemplate。"));
  }

  if (String(record.growTemplate || "").trim() && !hasDefinitionOfType(record.growTemplate, "grow-templates")) {
    issues.push(createCharacterIssue("error", `成长模板不存在：${record.growTemplate}`, record.growTemplate, ["grow-templates"]));
  }

  for (const stat of CHARACTER_STAT_FIELDS) {
    if (!Number.isFinite(stats[stat.key])) {
      issues.push(createCharacterIssue("error", `数值字段不是数字：${stat.label} ${stat.key}`));
    }
  }

  const internalSkills = Array.isArray(record.internalSkills) ? record.internalSkills : [];
  const equippedCount = internalSkills.filter((entry) => entry?.equipped === true).length;
  if (equippedCount > 1) {
    issues.push(createCharacterIssue("error", "内功存在多个 equipped = true。"));
  }

  appendMissingReferenceIssues(issues, record.talentIds, "天赋不存在", ["talents"]);
  appendMissingReferenceIssues(issues, record.specialSkillIds, "绝技不存在", ["special-skills"]);
  appendMissingReferenceIssues(issues, record.equipmentIds, "装备不存在", ["items"], (id) => isEquipmentId(id));
  appendSkillEntryIssues(issues, record.externalSkills, "外功不存在", ["external-skills"]);
  appendSkillEntryIssues(issues, record.internalSkills, "内功不存在", ["internal-skills"]);

  if (record.id && record.name && record.id !== record.name
    && state.contentIndex.storySpeakers.has(record.id)
    && state.contentIndex.storySpeakers.has(record.name)) {
    issues.push(createCharacterIssue("warn", "剧情对白同时使用了角色 id 和 name 作为 speaker，建议统一命名。"));
  }

  return issues;
}

function createCharacterIssue(severity, message, definitionId = "", types = []) {
  return { severity, message, definitionId, types };
}

function appendMissingReferenceIssues(issues, values, prefix, types, extraValidator = null) {
  if (!Array.isArray(values)) {
    return;
  }

  for (const value of values) {
    if (typeof value !== "string" || !value.trim()) {
      continue;
    }

    const valid = extraValidator ? extraValidator(value) : hasDefinitionInTypes(value, types);
    if (!valid) {
      issues.push(createCharacterIssue("error", `${prefix}：${value}`, value, types));
    }
  }
}

function appendSkillEntryIssues(issues, entries, prefix, types) {
  if (!Array.isArray(entries)) {
    return;
  }

  for (const entry of entries) {
    const id = typeof entry?.id === "string" ? entry.id.trim() : "";
    if (!id) {
      issues.push(createCharacterIssue("error", `${prefix.replace("不存在", "")}条目缺少 id。`));
      continue;
    }

    if (!hasDefinitionInTypes(id, types)) {
      issues.push(createCharacterIssue("error", `${prefix}：${id}`, id, types));
    }
  }
}

function hasDefinitionOfType(id, type) {
  const definitions = state.contentIndex.definitionsById.get(String(id).trim()) || [];
  return definitions.some((definition) => definition.type === type);
}

function hasDefinitionInTypes(id, types) {
  return types.some((type) => hasDefinitionOfType(id, type));
}

function isEquipmentId(id) {
  const item = state.contentIndex.itemsById.get(id);
  return Boolean(item && item.type === "equipment");
}

function createCharacterIssueSummary(issues) {
  const box = document.createElement("div");
  box.className = "character-issue-summary";
  const title = document.createElement("div");
  title.className = "character-issue-summary-title";
  title.textContent = "实时提示";
  const list = document.createElement("div");
  list.className = "character-inline-issue-list";
  for (const issue of issues.slice(0, 6)) {
    const row = document.createElement("div");
    row.className = `character-inline-issue ${issue.severity || "warn"}`;
    row.textContent = issue.message;
    list.appendChild(row);
  }
  box.append(title, list);
  return box;
}

function createCharacterPortraitSection(record, portraitInfo) {
  const wrapper = document.createElement("div");
  wrapper.className = "character-portrait-section";

  const preview = document.createElement("div");
  preview.className = `character-portrait-preview ${portraitInfo.assetExists ? "ok" : "missing"}`;

  if (portraitInfo.previewPath) {
    const image = document.createElement("img");
    image.src = `/api/assets/file?path=${encodeURIComponent(portraitInfo.previewPath)}&v=${Date.now()}`;
    image.alt = portraitInfo.portraitId || "角色头像";
    preview.appendChild(image);
  } else {
    const empty = document.createElement("div");
    empty.className = "character-portrait-preview-empty";
    empty.textContent = "缺图";
    preview.appendChild(empty);
  }

  const meta = document.createElement("div");
  meta.className = "character-portrait-meta";
  meta.append(
    createCharacterMetaRow("头像资源", portraitInfo.portraitId || "未填写"),
    createCharacterMetaRow("真实图片", portraitInfo.assetPath || "未找到"),
    createCharacterMetaRow("自动检测路径", portraitInfo.detectedAssetValue || "未检测到"),
    createCharacterMetaRow("资源状态", portraitInfo.resourceExists ? "已存在" : "resources.json 中不存在"),
  );

  if (!portraitInfo.assetExists) {
    const warning = document.createElement("div");
    warning.className = "character-portrait-warning";
    warning.textContent = portraitInfo.portraitId
      ? `缺图红色提示：${portraitInfo.portraitId} 还没有可用图片。`
      : "缺图红色提示：当前角色还没有填写 portrait。";
    meta.appendChild(warning);
  }

  const actions = document.createElement("div");
  actions.className = "character-portrait-actions";

  const pickerButton = document.createElement("button");
  pickerButton.type = "button";
  pickerButton.className = "primary";
  pickerButton.textContent = "从图库选择";
  pickerButton.addEventListener("click", () => {
    openPortraitPicker(portraitInfo.previewPath);
  });

  const openButton = document.createElement("button");
  openButton.type = "button";
  openButton.textContent = "打开图片";
  openButton.disabled = !portraitInfo.previewPath;
  openButton.addEventListener("click", () => {
    if (!portraitInfo.previewPath) {
      return;
    }

    setMode("assets");
    openAssetFile(portraitInfo.previewPath);
  });

  const normalizeButton = document.createElement("button");
  normalizeButton.type = "button";
  normalizeButton.textContent = "规范化512";
  normalizeButton.disabled = !portraitInfo.previewPath || !portraitInfo.previewPath.toLowerCase().endsWith(".png");
  normalizeButton.addEventListener("click", async () => {
    normalizeButton.disabled = true;
    try {
      await normalizePortraitAsset(portraitInfo.previewPath);
      await loadAssetFiles();
      renderFormView();
    } catch (error) {
      showValidation(false, formatNormalizePortraitError(error));
    } finally {
      normalizeButton.disabled = false;
    }
  });

  const createResourceButton = document.createElement("button");
  createResourceButton.type = "button";
  createResourceButton.className = "primary";
  createResourceButton.textContent = "一键创建头像资源";
  createResourceButton.disabled = portraitInfo.resourceExists || !portraitInfo.portraitId || !portraitInfo.detectedAssetValue;
  createResourceButton.addEventListener("click", async () => {
    createResourceButton.disabled = true;
    try {
      const result = await createPortraitResource(portraitInfo.portraitId, portraitInfo.detectedAssetValue);
      await loadDataFiles();
      await rebuildContentIndex();
      showValidation(result.validation.ok, result.validation.message);
      renderFormView();
    } catch (error) {
      showValidation(false, error.message);
    } finally {
      createResourceButton.disabled = portraitInfo.resourceExists || !portraitInfo.portraitId || !portraitInfo.detectedAssetValue;
    }
  });

  actions.append(pickerButton, openButton, normalizeButton, createResourceButton);
  preview.append(meta, actions);
  wrapper.appendChild(preview);
  return wrapper;
}

function createCharacterMetaRow(label, value) {
  const row = document.createElement("div");
  row.className = "character-meta-row";
  const labelNode = document.createElement("span");
  labelNode.textContent = label;
  const valueNode = document.createElement("strong");
  valueNode.textContent = value;
  row.append(labelNode, valueNode);
  return row;
}

function createCharacterStatField(record, stat) {
  ensureCharacterShape(record);
  const field = createCharacterFieldShell(stat.label, stat.key);
  const input = document.createElement("input");
  input.type = "number";
  input.value = String(getDisplayNumber(record.stats[stat.key], stat.key.startsWith("max_") ? 100 : 10));
  input.addEventListener("input", () => {
    record.stats[stat.key] = parseNumberInputValue(input.value, 0);
    syncFormToEditor();
  });
  field.appendChild(input);
  return field;
}

function createCharacterTabBar() {
  const tabBar = document.createElement("div");
  tabBar.className = "character-tab-bar";
  for (const tab of [
    { value: "talents", label: "天赋" },
    { value: "externalSkills", label: "外功" },
    { value: "internalSkills", label: "内功" },
    { value: "specialSkills", label: "绝技" },
    { value: "equipment", label: "装备" },
  ]) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "character-tab-button";
    button.classList.toggle("active", state.characterTab === tab.value);
    button.textContent = tab.label;
    button.addEventListener("click", () => {
      state.characterTab = tab.value;
      renderFormView();
    });
    tabBar.appendChild(button);
  }
  return tabBar;
}

function createCharacterTabContent(record) {
  const wrapper = document.createElement("div");
  wrapper.className = "character-tab-content";

  switch (state.characterTab) {
    case "externalSkills":
      wrapper.appendChild(createExternalSkillEditor(record));
      break;
    case "internalSkills":
      wrapper.appendChild(createInternalSkillEditor(record));
      break;
    case "specialSkills":
      wrapper.appendChild(createStringChipEditor(record, "specialSkillIds", "绝技", ensureDefinitionIdDatalist("special-skills")));
      break;
    case "equipment":
      wrapper.appendChild(createEquipmentEditor(record));
      break;
    case "talents":
    default:
      wrapper.appendChild(createStringChipEditor(record, "talentIds", "天赋", ensureDefinitionIdDatalist("talents")));
      break;
  }

  return wrapper;
}

function createStringChipEditor(record, key, labelCn, datalistId) {
  ensureCharacterShape(record);
  const wrapper = document.createElement("div");
  wrapper.className = "character-chip-editor";

  const summary = document.createElement("div");
  summary.className = "character-list-summary";
  summary.textContent = Array.isArray(record[key]) && record[key].length > 0
    ? record[key].join(" / ")
    : `未配置${labelCn}`;

  const chips = document.createElement("div");
  chips.className = "character-chip-list";
  for (const id of record[key]) {
    const chip = document.createElement("span");
    const valid = key === "equipmentIds" ? isEquipmentId(id) : hasDefinitionInTypes(id, [key === "talentIds" ? "talents" : "special-skills"]);
    chip.className = `character-chip ${valid ? "" : "invalid"}`.trim();
    chip.textContent = id;

    const remove = document.createElement("button");
    remove.type = "button";
    remove.textContent = "x";
    remove.addEventListener("click", () => {
      record[key] = record[key].filter((entry) => entry !== id);
      syncFormToEditor();
      renderFormView();
    });

    chip.appendChild(remove);
    chips.appendChild(chip);
  }

  const addRow = document.createElement("div");
  addRow.className = "character-add-row";
  const input = document.createElement("input");
  input.type = "text";
  input.className = "tool-input";
  input.placeholder = `搜索并添加${labelCn} id`;
  input.setAttribute("list", datalistId);
  const addButton = document.createElement("button");
  addButton.type = "button";
  addButton.textContent = `+ 添加${labelCn}`;
  addButton.addEventListener("click", () => {
    const value = input.value.trim();
    if (!value || record[key].includes(value)) {
      return;
    }

    record[key].push(value);
    input.value = "";
    syncFormToEditor();
    renderFormView();
  });
  addRow.append(input, addButton);

  wrapper.append(summary, chips, addRow);
  return wrapper;
}

function createExternalSkillEditor(record) {
  ensureCharacterShape(record);
  const wrapper = document.createElement("div");
  wrapper.className = "character-table-editor";
  wrapper.appendChild(createSkillSummary(record.externalSkills, "外功"));

  const table = document.createElement("div");
  table.className = "character-skill-table";
  table.appendChild(createSkillTableHeader(["武功ID", "等级", "上限", "操作"]));

  for (const entry of record.externalSkills) {
    table.appendChild(createExternalSkillRow(record, entry));
  }

  const addButton = document.createElement("button");
  addButton.type = "button";
  addButton.textContent = "+ 添加外功";
  addButton.addEventListener("click", () => {
    record.externalSkills.push({ id: "", level: 1, maxLevel: 10 });
    syncFormToEditor();
    renderFormView();
  });

  wrapper.append(table, addButton);
  return wrapper;
}

function createExternalSkillRow(record, entry) {
  const row = document.createElement("div");
  row.className = "character-skill-row";
  row.append(
    createRowTextInput(entry, "id", ensureDefinitionIdDatalist("external-skills"), "外功 id"),
    createRowNumberInput(entry, "level", 1),
    createRowNumberInput(entry, "maxLevel", 10, true),
  );

  const actions = document.createElement("div");
  actions.className = "character-row-actions";
  const remove = document.createElement("button");
  remove.type = "button";
  remove.textContent = "删除";
  remove.addEventListener("click", () => {
    record.externalSkills = record.externalSkills.filter((candidate) => candidate !== entry);
    syncFormToEditor();
    renderFormView();
  });
  actions.appendChild(remove);
  row.appendChild(actions);
  return row;
}

function createInternalSkillEditor(record) {
  ensureCharacterShape(record);
  const wrapper = document.createElement("div");
  wrapper.className = "character-table-editor";
  wrapper.appendChild(createSkillSummary(record.internalSkills, "内功"));

  const table = document.createElement("div");
  table.className = "character-skill-table";
  table.appendChild(createSkillTableHeader(["内功ID", "等级", "上限", "已装备", "操作"]));

  for (const entry of record.internalSkills) {
    table.appendChild(createInternalSkillRow(record, entry));
  }

  const addButton = document.createElement("button");
  addButton.type = "button";
  addButton.textContent = "+ 添加内功";
  addButton.addEventListener("click", () => {
    record.internalSkills.push({ id: "", level: 1, maxLevel: 10, equipped: false });
    syncFormToEditor();
    renderFormView();
  });

  wrapper.append(table, addButton);
  return wrapper;
}

function createInternalSkillRow(record, entry) {
  const row = document.createElement("div");
  row.className = "character-skill-row internal";
  row.append(
    createRowTextInput(entry, "id", ensureDefinitionIdDatalist("internal-skills"), "内功 id"),
    createRowNumberInput(entry, "level", 1),
    createRowNumberInput(entry, "maxLevel", 10, true),
  );

  const equipped = document.createElement("label");
  equipped.className = "character-row-toggle";
  const input = document.createElement("input");
  input.type = "checkbox";
  input.checked = entry.equipped === true;
  input.addEventListener("change", () => {
    entry.equipped = input.checked;
    syncFormToEditor();
    renderCharacterCheckTool();
  });
  equipped.append(input, document.createTextNode(input.checked ? "是" : "否"));
  input.addEventListener("change", () => {
    equipped.lastChild.textContent = input.checked ? "是" : "否";
  });
  row.appendChild(equipped);

  const actions = document.createElement("div");
  actions.className = "character-row-actions";
  const remove = document.createElement("button");
  remove.type = "button";
  remove.textContent = "删除";
  remove.addEventListener("click", () => {
    record.internalSkills = record.internalSkills.filter((candidate) => candidate !== entry);
    syncFormToEditor();
    renderFormView();
  });
  actions.appendChild(remove);
  row.appendChild(actions);
  return row;
}

function createEquipmentEditor(record) {
  ensureCharacterShape(record);
  const wrapper = document.createElement("div");
  wrapper.className = "character-table-editor";

  const summary = document.createElement("div");
  summary.className = "character-list-summary";
  summary.textContent = record.equipmentIds.length > 0
    ? record.equipmentIds.map((id) => `${id}${getEquipmentSlotLabel(id) ? ` (${getEquipmentSlotLabel(id)})` : ""}`).join(" / ")
    : "未配置装备";

  const list = document.createElement("div");
  list.className = "character-equipment-list";
  for (const id of record.equipmentIds) {
    const row = document.createElement("div");
    row.className = "character-equipment-row";
    const text = document.createElement("div");
    text.className = "character-equipment-row-text";
    text.textContent = id;
    const meta = document.createElement("div");
    meta.className = "character-equipment-row-meta";
    meta.textContent = getEquipmentSlotLabel(id) || "未知部位";
    const remove = document.createElement("button");
    remove.type = "button";
    remove.textContent = "删除";
    remove.addEventListener("click", () => {
      record.equipmentIds = record.equipmentIds.filter((entry) => entry !== id);
      syncFormToEditor();
      renderFormView();
    });
    row.append(text, meta, remove);
    list.appendChild(row);
  }

  const addRow = document.createElement("div");
  addRow.className = "character-add-row";
  const input = document.createElement("input");
  input.type = "text";
  input.className = "tool-input";
  input.placeholder = "搜索并添加装备 id";
  input.setAttribute("list", ensureDefinitionIdDatalist("equipment"));
  const addButton = document.createElement("button");
  addButton.type = "button";
  addButton.textContent = "+ 添加装备";
  addButton.addEventListener("click", () => {
    const value = input.value.trim();
    if (!value || record.equipmentIds.includes(value)) {
      return;
    }

    record.equipmentIds.push(value);
    input.value = "";
    syncFormToEditor();
    renderFormView();
  });
  addRow.append(input, addButton);

  wrapper.append(summary, list, addRow);
  return wrapper;
}

function createSkillSummary(entries, label) {
  const summary = document.createElement("div");
  summary.className = "character-list-summary";
  summary.textContent = Array.isArray(entries) && entries.length > 0
    ? entries.map((entry) => {
      const level = Number.isFinite(entry?.level) ? entry.level : 1;
      const maxLevel = Number.isFinite(entry?.maxLevel) ? entry.maxLevel : "?";
      const equipped = entry?.equipped === true ? " 已装备" : "";
      return `${entry?.id || "未命名"} Lv${level}/${maxLevel}${equipped}`;
    }).join(" / ")
    : `未配置${label}`;
  return summary;
}

function createSkillTableHeader(labels) {
  const header = document.createElement("div");
  header.className = "character-skill-row header";
  for (const label of labels) {
    const cell = document.createElement("div");
    cell.className = "character-skill-header";
    cell.textContent = label;
    header.appendChild(cell);
  }
  return header;
}

function createRowTextInput(target, key, datalistId, placeholder) {
  const input = document.createElement("input");
  input.type = "text";
  input.className = "tool-input";
  input.value = target[key] == null ? "" : String(target[key]);
  input.placeholder = placeholder;
  if (datalistId) {
    input.setAttribute("list", datalistId);
  }
  input.addEventListener("input", () => {
    target[key] = input.value;
    syncFormToEditor();
    renderCharacterCheckTool();
  });
  return input;
}

function createRowNumberInput(target, key, fallback, nullable = false) {
  const input = document.createElement("input");
  input.type = "number";
  input.className = "tool-input";
  const value = target[key];
  input.value = Number.isFinite(value) ? String(value) : String(fallback);
  input.addEventListener("input", () => {
    const trimmed = input.value.trim();
    target[key] = nullable && !trimmed ? null : parseNumberInputValue(trimmed, fallback);
    syncFormToEditor();
    renderCharacterCheckTool();
  });
  return input;
}

function createCharacterAdvancedJsonSection(record) {
  const section = createCharacterSection("高级 JSON", "Advanced");
  const details = document.createElement("details");
  details.className = "character-advanced-json";
  const summary = document.createElement("summary");
  summary.textContent = "展开原始角色 JSON";
  const textarea = document.createElement("textarea");
  textarea.value = JSON.stringify(record, null, 2);
  textarea.addEventListener("change", () => {
    try {
      const parsed = JSON.parse(textarea.value);
      state.formRecords[state.selectedRecordIndex] = parsed;
      syncFormToEditor();
      renderFormView();
    } catch (error) {
      textarea.setCustomValidity(error instanceof Error ? error.message : String(error));
      textarea.reportValidity();
    }
  });
  details.append(summary, textarea);
  section.appendChild(details);
  return section;
}

function getEquipmentSlotLabel(id) {
  const item = state.contentIndex.itemsById.get(id);
  if (!item || item.type !== "equipment") {
    return "";
  }

  switch (item.slotType) {
    case "weapon":
      return "武器";
    case "armor":
      return "护甲";
    case "accessory":
      return "饰品";
    default:
      return item.slotType || "";
  }
}

function ensureDefinitionIdDatalist(kind) {
  const id = `definitionOptions-${kind}`;
  const existing = document.getElementById(id);
  if (existing) {
    existing.remove();
  }

  const datalist = document.createElement("datalist");
  datalist.id = id;
  const options = getDefinitionOptions(kind);
  for (const option of options) {
    const node = document.createElement("option");
    node.value = option.id;
    node.label = option.label;
    datalist.appendChild(node);
  }

  document.body.appendChild(datalist);
  return id;
}

function getDefinitionOptions(kind) {
  if (kind === "equipment") {
    return Array.from(state.contentIndex.itemsById.values())
      .filter((item) => item.type === "equipment" && typeof item.id === "string")
      .sort((left, right) => String(left.id).localeCompare(String(right.id), "zh-Hans-CN"))
      .map((item) => ({
        id: item.id,
        label: `${item.name || item.id} ${getEquipmentSlotLabel(item.id) || ""}`.trim(),
      }));
  }

  const options = [];
  for (const definitions of state.contentIndex.definitionsById.values()) {
    for (const definition of definitions) {
      if (definition.type !== kind) {
        continue;
      }

      options.push({
        id: definition.id,
        label: definition.displayName || definition.id,
      });
    }
  }

  return options.sort((left, right) => left.id.localeCompare(right.id, "zh-Hans-CN"));
}

function createActionButton(label, action) {
  const button = document.createElement("button");
  button.type = "button";
  button.textContent = label;
  button.addEventListener("click", action);
  return button;
}

function createFieldEditor(record, key, value) {
  const field = document.createElement("div");
  const complex = value !== null && typeof value === "object";
  const longText = typeof value === "string" && value.length > 80;
  field.className = `form-field ${complex || longText ? "full" : ""}`;

  const label = document.createElement("label");
  label.textContent = key;
  field.appendChild(label);

  if (typeof value === "boolean") {
    const input = document.createElement("input");
    input.type = "checkbox";
    input.checked = value;
    input.addEventListener("change", () => updateRecordField(record, key, input.checked));
    field.appendChild(input);
    return field;
  }

  if (typeof value === "number") {
    const input = document.createElement("input");
    input.type = "number";
    input.value = String(value);
    input.addEventListener("input", () => updateRecordField(record, key, Number(input.value)));
    field.appendChild(input);
    return field;
  }

  if (state.currentPath === "resources.json" && key === "group") {
    const input = document.createElement("input");
    input.value = value === null ? "" : String(value);
    input.setAttribute("list", ensureResourceGroupDatalist());
    input.addEventListener("input", () => updateRecordField(record, key, input.value.trim() || null));
    field.appendChild(input);
    return field;
  }

  if (state.currentPath === "characters.json" && key === "portrait") {
    const input = document.createElement("input");
    input.value = value === null ? "" : String(value);
    input.setAttribute("list", ensureResourceIdDatalist("头像"));
    input.addEventListener("input", () => updateRecordField(record, key, input.value));
    field.appendChild(input);

    const assetPath = resolveAssetPath(input.value);
    if (assetPath) {
      field.appendChild(createInlineAssetPreview(assetPath, input.value));
    }

    return field;
  }

  if (complex) {
    const textarea = document.createElement("textarea");
    textarea.value = JSON.stringify(value, null, 2);
    textarea.addEventListener("change", () => {
      try {
        updateRecordField(record, key, JSON.parse(textarea.value));
        textarea.setCustomValidity("");
      } catch (error) {
        textarea.setCustomValidity(error instanceof Error ? error.message : String(error));
        textarea.reportValidity();
      }
    });
    field.appendChild(textarea);
    return field;
  }

  const input = longText ? document.createElement("textarea") : document.createElement("input");
  input.value = value === null ? "" : String(value);
  if (!longText && isResourceReferenceField(key)) {
    input.setAttribute("list", ensureResourceIdDatalist(getResourceGroupForField(key)));
  } else if (!longText && isAssetField(key, input.value)) {
    input.setAttribute("list", ensureAssetDatalist());
  }
  input.addEventListener("input", () => updateRecordField(record, key, input.value));
  field.appendChild(input);

  const assetPath = resolveAssetPath(input.value);
  if (assetPath) {
    field.appendChild(createInlineAssetPreview(assetPath, input.value));
  }

  return field;
}

function updateRecordField(record, key, value, options = {}) {
  record[key] = value;
  syncFormToEditor();
  if (options.rerender) {
    renderFormView();
  }
}

function addRecord() {
  const record = createRecordTemplate();
  state.formRecords.splice(state.selectedRecordIndex + 1, 0, record);
  state.selectedRecordIndex += 1;
  syncFormToEditor();
  renderFormView();
}

function duplicateRecord() {
  const current = state.formRecords[state.selectedRecordIndex];
  if (!current) {
    return;
  }

  const record = structuredCloneCompat(current);
  if (typeof record.id === "string") {
    record.id = createUniqueId(`${record.id}_copy`);
  }

  if (typeof record.name === "string") {
    record.name = `${record.name} 副本`;
  }

  state.formRecords.splice(state.selectedRecordIndex + 1, 0, record);
  state.selectedRecordIndex += 1;
  syncFormToEditor();
  renderFormView();
}

function deleteRecord() {
  if (state.formRecords.length === 0) {
    return;
  }

  const current = state.formRecords[state.selectedRecordIndex];
  const title = current ? getRecordTitle(current, state.selectedRecordIndex) : "当前条目";
  if (!window.confirm(`确认删除「${title}」？`)) {
    return;
  }

  state.formRecords.splice(state.selectedRecordIndex, 1);
  state.selectedRecordIndex = Math.max(0, Math.min(state.selectedRecordIndex, state.formRecords.length - 1));
  syncFormToEditor();
  renderFormView();
}

function createRecordTemplate() {
  if (state.currentPath === "resources.json") {
    return { id: createUniqueId("新资源"), group: null, value: "" };
  }

  if (state.currentPath === "characters.json") {
    const id = createUniqueId("新角色");
    return {
      id,
      name: id,
      level: 1,
      portrait: `头像.${id}`,
      gender: "neutral",
      arenaEnabled: false,
      talentIds: [],
      stats: {
        bili: 10,
        dingli: 10,
        fuyuan: 10,
        gengu: 10,
        jianfa: 10,
        daofa: 10,
        quanzhang: 10,
        qimen: 10,
        shenfa: 10,
        wuxing: 10,
        wuxue: 10,
        max_hp: 100,
        max_mp: 100,
      },
      specialSkillIds: [],
      internalSkills: [],
      equipmentIds: [],
      externalSkills: [],
    };
  }

  if (state.currentPath === "items.json") {
    const id = createUniqueId("新物品");
    return {
      category: "normal",
      id,
      name: id,
      type: "consumable",
      level: 1,
      price: 0,
      cooldown: 0,
      canDrop: false,
      description: "",
      picture: `物品.${id}`,
      requirements: [],
      useEffects: [],
    };
  }

  if (state.currentPath === "game-tips.json") {
    return { id: createUniqueId("小贴士.新"), text: "" };
  }

  return { id: createUniqueId("新条目") };
}

function createUniqueId(baseId) {
  const existing = new Set(
    state.formRecords
      .map((record) => typeof record.id === "string" ? record.id : "")
      .filter(Boolean)
  );
  if (!existing.has(baseId)) {
    return baseId;
  }

  for (let index = 2; index < 10000; index += 1) {
    const candidate = `${baseId}_${index}`;
    if (!existing.has(candidate)) {
      return candidate;
    }
  }

  return `${baseId}_${Date.now()}`;
}

function structuredCloneCompat(value) {
  if (typeof structuredClone === "function") {
    return structuredClone(value);
  }

  return JSON.parse(JSON.stringify(value));
}

function syncFormToEditor() {
  elements.editor.value = `${JSON.stringify(state.formRecords, null, 2)}\n`;
  state.dirty = true;
  elements.saveState.textContent = "表单已修改，尚未保存";
  updateSearchMatches();
  renderDirtyState();
  renderCursorState();
  renderIndexPanel();
  renderCharacterCheckTool();
}

function getRecordTitle(record, index) {
  return String(record.name || record.id || record.text || `#${index + 1}`);
}

function getRecordSubtitle(record, index) {
  const id = record.id ? String(record.id) : `#${index + 1}`;
  const type = record.type ? ` · ${record.type}` : "";
  return `${id}${type}`;
}

function createRecordThumb(record) {
  const assetPath = resolveRecordAssetPath(record);
  if (assetPath && isImage(assetPath.toLowerCase())) {
    const image = document.createElement("img");
    image.className = "record-thumb";
    image.src = `/api/assets/file?path=${encodeURIComponent(assetPath)}`;
    image.alt = getRecordTitle(record, 0);
    return image;
  }

  const placeholder = document.createElement("div");
  placeholder.className = "record-thumb record-thumb-placeholder";
  placeholder.textContent = "JSON";
  return placeholder;
}

function resolveRecordAssetPath(record) {
  for (const key of ["portrait", "picture", "icon", "model", "avatar", "image"]) {
    if (typeof record[key] === "string") {
      const path = resolveAssetPath(record[key]);
      if (path) {
        return path;
      }
    }
  }

  return "";
}

function resolveAssetPath(value) {
  if (!value) {
    return "";
  }

  const resource = state.contentIndex.resourcesById.get(value);
  if (resource) {
    return resolveResourceAssetPath(resource);
  }

  if (value.includes("/") && !value.startsWith("res://") && !value.startsWith("user://")) {
    return findAssetPath(value) || value;
  }

  return "";
}

function resolveResourceAssetPath(resource) {
  const value = typeof resource.value === "string" ? resource.value : "";
  if (!value) {
    return "";
  }

  if (resource.group === "音乐" || resource.group === "音效") {
    return findAssetPath(value, { audio: true }) || value;
  }

  return findAssetPath(value, { art: true }) || value;
}

function findAssetPath(value, options = {}) {
  const normalized = value.trim().replaceAll("\\", "/").replace(/^res:\/\/assets\//, "").replace(/^assets\//, "");
  const candidates = [];
  const hasExtension = /\.[a-z0-9]+$/i.test(normalized);
  const imageExtensions = [".png", ".jpg", ".jpeg", ".webp"];
  const audioExtensions = [".ogg", ".mp3", ".wav", ".flac"];
  const extensions = options.audio ? audioExtensions : imageExtensions;
  const roots = [];

  if (options.audio) {
    roots.push("", "audio/");
  } else if (options.art) {
    roots.push("", "art/");
  } else {
    roots.push("", "art/", "audio/");
  }

  for (const root of roots) {
    const base = normalized.startsWith(root) ? normalized : `${root}${normalized}`;
    if (hasExtension) {
      candidates.push(base);
    } else {
      for (const extension of extensions) {
        candidates.push(`${base}${extension}`);
      }
    }
  }

  for (const candidate of candidates) {
    if (state.assetFiles.some((file) => file.path === candidate)) {
      return candidate;
    }
  }

  return "";
}

function isAssetField(key, value) {
  const lowerKey = key.toLowerCase();
  return ["value", "portrait", "icon", "model", "avatar", "image", "music", "audio", "background"]
    .some((part) => lowerKey.includes(part)) || resolveAssetPath(value) !== "";
}

function isResourceReferenceField(key) {
  return ["portrait", "picture", "background", "music", "musics", "image", "icon"].includes(key);
}

function getResourceGroupForField(key) {
  if (key === "portrait") {
    return "头像";
  }

  if (key === "music" || key === "musics") {
    return "音乐";
  }

  if (key === "background" || key === "picture" || key === "image") {
    return null;
  }

  return null;
}

function createInlineAssetPreview(path, label) {
  const wrapper = document.createElement("div");
  wrapper.className = "inline-asset";
  const lower = path.toLowerCase();

  if (isImage(lower)) {
    const image = document.createElement("img");
    image.src = `/api/assets/file?path=${encodeURIComponent(path)}`;
    image.alt = label;
    wrapper.appendChild(image);
  } else if (isAudio(lower)) {
    const audio = document.createElement("audio");
    audio.controls = true;
    audio.src = `/api/assets/file?path=${encodeURIComponent(path)}`;
    wrapper.appendChild(audio);
  }

  const text = document.createElement("div");
  text.className = "inline-asset-path";
  text.textContent = path;
  wrapper.appendChild(text);
  return wrapper;
}

function ensureAssetDatalist() {
  const id = "assetPathOptions";
  if (document.getElementById(id)) {
    return id;
  }

  const datalist = document.createElement("datalist");
  datalist.id = id;
  for (const file of state.assetFiles) {
    if (file.path.endsWith(".import")) {
      continue;
    }

    const option = document.createElement("option");
    option.value = file.path;
    datalist.appendChild(option);
  }

  document.body.appendChild(datalist);
  return id;
}

function ensureResourceIdDatalist(group) {
  const id = `resourceIdOptions-${group || "all"}`;
  const existing = document.getElementById(id);
  if (existing) {
    existing.remove();
  }

  const datalist = document.createElement("datalist");
  datalist.id = id;
  const resources = group
    ? state.contentIndex.resourcesByGroup.get(group) || []
    : Array.from(state.contentIndex.resourcesById.values());

  for (const resource of resources) {
    if (typeof resource.id !== "string") {
      continue;
    }

    const option = document.createElement("option");
    option.value = resource.id;
    option.label = typeof resource.value === "string" ? resource.value : "";
    datalist.appendChild(option);
  }

  document.body.appendChild(datalist);
  return id;
}

function ensureResourceGroupDatalist() {
  const id = "resourceGroupOptions";
  const existing = document.getElementById(id);
  if (existing) {
    existing.remove();
  }

  const groups = new Set();
  for (const record of state.formRecords) {
    if (typeof record.group === "string" && record.group.length > 0) {
      groups.add(record.group);
    }
  }

  const datalist = document.createElement("datalist");
  datalist.id = id;
  for (const group of Array.from(groups).sort((a, b) => a.localeCompare(b, "zh-Hans-CN"))) {
    const option = document.createElement("option");
    option.value = group;
    datalist.appendChild(option);
  }

  document.body.appendChild(datalist);
  return id;
}

async function rebuildContentIndex() {
  const definitionsById = new Map();
  const fileSummaries = new Map();
  const parseErrors = [];
  const resourceValues = new Map();
  const resourcesById = new Map();
  const resourcesByGroup = new Map();
  const charactersByIdOrName = new Map();
  const itemsById = new Map();
  const storySpeakers = new Map();

  for (const file of state.dataFiles) {
    try {
      const response = await requestJson(`/api/data/file?path=${encodeURIComponent(file.path)}`);
      const json = JSON.parse(response.content);
      if (file.path === "resources.json" && Array.isArray(json)) {
        for (const resource of json) {
          if (typeof resource?.id === "string" && typeof resource?.value === "string") {
            resourceValues.set(resource.id, resource.value);
          }

          if (typeof resource?.id === "string") {
            resourcesById.set(resource.id, resource);
            const group = typeof resource.group === "string" ? resource.group : "";
            const groupResources = resourcesByGroup.get(group) || [];
            groupResources.push(resource);
            resourcesByGroup.set(group, groupResources);
          }
        }
      }

      if (file.path === "characters.json" && Array.isArray(json)) {
        for (const character of json) {
          if (typeof character?.id === "string") {
            charactersByIdOrName.set(character.id, character);
          }

          if (typeof character?.name === "string") {
            charactersByIdOrName.set(character.name, character);
          }
        }
      }

      if (file.path === "items.json" && Array.isArray(json)) {
        for (const item of json) {
          if (typeof item?.id === "string") {
            itemsById.set(item.id, item);
          }
        }
      }

      if (file.path.endsWith(".story.json")) {
        for (const speaker of ExtractStorySpeakers(file.path, response.content, json)) {
          storySpeakers.set(speaker.Name, (storySpeakers.get(speaker.Name) || 0) + 1);
        }
      }

      const definitions = extractDefinitions(file.path, response.content, json);
      fileSummaries.set(file.path, {
        definitions: definitions.length,
        type: getDefinitionType(file.path),
      });

      for (const definition of definitions) {
        const existing = definitionsById.get(definition.id) || [];
        existing.push(definition);
        definitionsById.set(definition.id, existing);
      }
    } catch (error) {
      parseErrors.push({
        path: file.path,
        message: error instanceof Error ? error.message : String(error),
      });
    }
  }

  state.contentIndex = {
    ready: true,
    definitionsById,
    fileSummaries,
    duplicateDefinitions: findDuplicateDefinitions(definitionsById),
    parseErrors,
    resourcesById,
    resourcesByGroup,
    charactersByIdOrName,
    itemsById,
    storySpeakers,
  };
  state.resourceValues = resourceValues;

  renderIndexPanel();
  renderSelectionLookup();
  renderSpeakerTool();
  renderPortraitCheckTool();
  renderCharacterCheckTool();
}

function extractDefinitions(path, content, json) {
  if (path.endsWith(".story.json")) {
    const segments = Array.isArray(json?.segments) ? json.segments : [];
    return segments
      .filter((segment) => typeof segment?.name === "string" && segment.name.length > 0)
      .map((segment) => ({
        id: segment.name,
        displayName: segment.name,
        type: "story",
        path,
        line: findJsonPropertyLine(content, "name", segment.name),
      }));
  }

  const records = Array.isArray(json) ? json : [json];
  return records
    .filter((record) => typeof record?.id === "string" && record.id.length > 0)
    .map((record) => ({
      id: record.id,
      displayName: typeof record.name === "string" && record.name.length > 0 ? record.name : record.id,
      type: getDefinitionType(path),
      path,
      line: findJsonPropertyLine(content, "id", record.id),
    }));
}

function getDefinitionType(path) {
  if (path.endsWith(".story.json")) {
    return "story";
  }

  return path.replace(/\.json$/i, "");
}

function findJsonPropertyLine(content, propertyName, value) {
  const escapedValue = escapeRegExp(JSON.stringify(value).slice(1, -1));
  const pattern = new RegExp(`"${escapeRegExp(propertyName)}"\\s*:\\s*"${escapedValue}"`);
  const match = pattern.exec(content);
  if (!match) {
    return 1;
  }

  return content.slice(0, match.index).split("\n").length;
}

function findDuplicateDefinitions(definitionsById) {
  const duplicates = [];
  for (const [id, definitions] of definitionsById.entries()) {
    const byType = new Map();
    for (const definition of definitions) {
      const key = definition.type;
      const group = byType.get(key) || [];
      group.push(definition);
      byType.set(key, group);
    }

    for (const [type, group] of byType.entries()) {
      if (group.length > 1) {
        duplicates.push({ id, type, count: group.length, definitions: group });
      }
    }
  }

  return duplicates;
}

function renderIndexPanel() {
  elements.indexBox.replaceChildren();

  if (!state.contentIndex.ready) {
    elements.indexBox.className = "index-box muted";
    elements.indexBox.textContent = "正在建立索引";
    return;
  }

  elements.indexBox.className = "index-box";
  const summary = state.contentIndex.fileSummaries.get(state.currentPath);
  const totalDefinitions = Array.from(state.contentIndex.definitionsById.values())
    .reduce((total, definitions) => total + definitions.length, 0);

  appendIndexRow(elements.indexBox, "全局定义", String(totalDefinitions));
  appendIndexRow(elements.indexBox, "当前文件", summary ? `${summary.definitions} 条 ${summary.type}` : "-");

  if (state.contentIndex.parseErrors.length > 0) {
    const line = document.createElement("div");
    line.className = "warning-line";
    line.textContent = `解析失败：${state.contentIndex.parseErrors.length} 个文件`;
    elements.indexBox.appendChild(line);
  }

  if (state.contentIndex.duplicateDefinitions.length > 0) {
    const line = document.createElement("div");
    line.className = "warning-line";
    line.textContent = `重复定义：${state.contentIndex.duplicateDefinitions.length} 组`;
    elements.indexBox.appendChild(line);
  }
}

function appendIndexRow(parent, label, value) {
  const row = document.createElement("div");
  row.className = "index-row";

  const labelNode = document.createElement("span");
  labelNode.className = "index-label";
  labelNode.textContent = label;

  const valueNode = document.createElement("span");
  valueNode.className = "index-value";
  valueNode.textContent = value;

  row.append(labelNode, valueNode);
  parent.appendChild(row);
}

function renderSelectionLookup() {
  elements.selectionBox.replaceChildren();

  const selected = getSelectedLookupText();
  if (!selected) {
    elements.selectionBox.className = "index-box muted";
    elements.selectionBox.textContent = "选中 JSON 字符串后显示定义";
    return;
  }

  const definitions = state.contentIndex.definitionsById.get(selected) || [];
  elements.selectionBox.className = "index-box";
  appendIndexRow(elements.selectionBox, "文本", selected);

  if (definitions.length === 0) {
    const empty = document.createElement("div");
    empty.className = "warning-line";
    empty.textContent = "未找到定义";
    elements.selectionBox.appendChild(empty);
    return;
  }

  const list = document.createElement("div");
  list.className = "definition-list";
  for (const definition of definitions) {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "definition-button";
    button.textContent = `${definition.type}: ${definition.path}:${definition.line}`;
    button.addEventListener("click", () => revealDefinition(definition));
    list.appendChild(button);
  }

  elements.selectionBox.appendChild(list);
}

function renderSpeakerTool() {
  elements.speakerToolBox.replaceChildren();

  const description = document.createElement("div");
  description.className = "static-tool-note";
  description.textContent = "创建可用于 dialogue speaker 的角色定义，并补头像资源。头像路径可填 head/qingbing 或 art/head/qingbing.png。";

  const idInput = createToolInput("speakerId", "说话人 id", "清兵");
  const nameInput = createToolInput("speakerName", "显示名", "清兵");
  const portraitInput = createToolInput("speakerPortrait", "头像资源 id", "头像.清兵");
  portraitInput.setAttribute("list", ensureResourceIdDatalist("头像"));
  const assetInput = createToolInput("speakerAsset", "头像路径 value", "head/qingbing");
  assetInput.setAttribute("list", ensureAssetDatalist());
  const assetStatus = document.createElement("div");
  assetStatus.className = "speaker-asset-preview muted";
  let speakerAutoValues = {
    name: "",
    portraitId: "",
    assetValue: "",
    gender: "neutral",
  };
  const speakerManualFields = {
    name: false,
    portraitId: false,
    assetValue: false,
    gender: false,
  };

  const genderSelect = document.createElement("select");
  genderSelect.className = "tool-input";
  for (const [value, label] of [["neutral", "neutral"], ["male", "male"], ["female", "female"]]) {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = label;
    genderSelect.appendChild(option);
  }

  idInput.addEventListener("input", () => {
    applySpeakerDefaults(false);
  });
  nameInput.addEventListener("input", () => {
    speakerManualFields.name = true;
  });
  portraitInput.addEventListener("input", () => {
    speakerManualFields.portraitId = true;
    if (!speakerManualFields.assetValue) {
      applySpeakerDefaults(false);
    }
  });
  assetInput.addEventListener("input", () => {
    speakerManualFields.assetValue = true;
    renderSpeakerAssetStatus(assetInput.value, assetStatus);
  });
  genderSelect.addEventListener("change", () => {
    speakerManualFields.gender = true;
  });

  const selectedButton = document.createElement("button");
  selectedButton.type = "button";
  selectedButton.textContent = "使用选中文本";
  selectedButton.addEventListener("click", () => {
    const selected = getSelectedLookupText();
    if (!selected) {
      return;
    }

    idInput.value = selected;
    resetSpeakerManualFields();
    applySpeakerDefaults(true);
  });

  const createButton = document.createElement("button");
  createButton.type = "button";
  createButton.className = "primary";
  createButton.textContent = "创建说话人";

  const status = document.createElement("div");
  status.className = "static-tool-status muted";

  createButton.addEventListener("click", async () => {
    if (state.dirty && (state.currentPath === "characters.json" || state.currentPath === "resources.json")) {
      status.className = "static-tool-status bad";
      status.textContent = "当前 characters/resources 有未保存改动，请先保存或切换文件后再创建。";
      return;
    }

    createButton.disabled = true;
    status.className = "static-tool-status muted";
    status.textContent = "正在创建...";

    try {
      const result = await requestJson("/api/static/speaker", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          id: idInput.value,
          name: nameInput.value,
          portraitId: portraitInput.value,
          assetValue: assetInput.value,
          gender: genderSelect.value,
        }),
      });

      await loadDataFiles();
      await rebuildContentIndex();
      renderFileList();
      showValidation(result.validation.ok, result.validation.message);
      status.className = "static-tool-status ok";
      status.textContent = `已创建：${result.id} / ${result.portraitId}`;

      if (state.currentPath === "characters.json" || state.currentPath === "resources.json") {
        await openDataFile(state.currentPath);
      }
    } catch (error) {
      status.className = "static-tool-status bad";
      status.textContent = error.message;
    } finally {
      createButton.disabled = false;
    }
  });

  elements.speakerToolBox.append(
    description,
    createToolField("说话人 id", idInput),
    createToolField("显示名", nameInput),
    createToolField("头像资源", portraitInput),
    createToolField("头像路径", assetInput),
    assetStatus,
    createToolField("性别", genderSelect),
    selectedButton,
    createButton,
    status
  );

  function resetSpeakerManualFields() {
    speakerManualFields.name = false;
    speakerManualFields.portraitId = false;
    speakerManualFields.assetValue = false;
    speakerManualFields.gender = false;
  }

  function applySpeakerDefaults(force) {
    const explicitPortraitId = speakerManualFields.portraitId ? portraitInput.value : "";
    const defaults = getSpeakerDefaults(idInput.value, explicitPortraitId);
    if (!defaults.id) {
      return;
    }

    if (force || !speakerManualFields.name || nameInput.value === speakerAutoValues.name) {
      nameInput.value = defaults.name;
    }

    if (force || !speakerManualFields.portraitId || portraitInput.value === speakerAutoValues.portraitId) {
      portraitInput.value = defaults.portraitId;
    }

    if (force || !speakerManualFields.assetValue || assetInput.value === speakerAutoValues.assetValue) {
      assetInput.value = defaults.assetValue;
    }

    if (force || !speakerManualFields.gender || genderSelect.value === speakerAutoValues.gender) {
      genderSelect.value = defaults.gender;
    }

    speakerAutoValues = defaults;
    renderSpeakerAssetStatus(assetInput.value, assetStatus);
  }
}

function getSpeakerDefaults(rawId, explicitPortraitId) {
  const id = rawId.trim();
  if (!id) {
    return {
      id: "",
      name: "",
      portraitId: "",
      assetValue: "",
      gender: "neutral",
    };
  }

  const character = state.contentIndex.charactersByIdOrName.get(id);
  const name = typeof character?.name === "string" && character.name.trim()
    ? character.name.trim()
    : id;
  const portraitId = typeof character?.portrait === "string" && character.portrait.trim()
    ? character.portrait.trim()
    : explicitPortraitId.trim() && explicitPortraitId.trim() !== "头像."
      ? explicitPortraitId.trim()
      : `头像.${id}`;
  const resource = state.contentIndex.resourcesById.get(portraitId);
  const assetValue = typeof resource?.value === "string" && resource.value.trim()
    ? normalizeToolAssetValue(resource.value)
    : detectSpeakerPortraitAssetValue(id, name) || `head/${id}`;
  const gender = ["male", "female", "neutral"].includes(character?.gender)
    ? character.gender
    : "neutral";

  return {
    id,
    name,
    portraitId,
    assetValue,
    gender,
  };
}

function detectSpeakerPortraitAssetValue(id, name) {
  const candidates = [];
  for (const value of [id, name, toPinyinSlug(id), toPinyinSlug(name)]) {
    const normalized = normalizeToolSearchValue(value);
    if (!normalized) {
      continue;
    }

    candidates.push(`head/${normalized}`);
  }

  for (const candidate of candidates) {
    const found = findAssetPath(candidate, { art: true });
    if (found) {
      return normalizeToolAssetValue(found);
    }
  }

  const wanted = new Set(candidates.map((candidate) => normalizeToolSearchValue(candidate.split("/").pop() || "")));
  for (const file of state.assetFiles) {
    if (!file.path.toLowerCase().startsWith("art/head/") || !isImage(file.path.toLowerCase())) {
      continue;
    }

    const basename = normalizeToolSearchValue((file.name || file.path.split("/").pop() || "").replace(/\.[^.]+$/i, ""));
    if (wanted.has(basename)) {
      return normalizeToolAssetValue(file.path);
    }
  }

  return "";
}

function renderSpeakerAssetStatus(value, statusNode) {
  statusNode.replaceChildren();
  const found = findAssetPath(value, { art: true });
  if (found) {
    statusNode.className = "speaker-asset-preview ok";

    const image = document.createElement("img");
    image.src = `/api/assets/file?path=${encodeURIComponent(found)}`;
    image.alt = found;

    const info = document.createElement("div");
    const label = document.createElement("div");
    label.className = "speaker-asset-preview-label";
    label.textContent = "已找到真实文件";
    const path = document.createElement("button");
    path.type = "button";
    path.className = "speaker-asset-preview-path";
    path.textContent = `assets/${found}`;
    path.title = "打开资产预览";
    path.addEventListener("click", () => {
      setMode("assets");
      openAssetFile(found);
    });

    const normalizeButton = document.createElement("button");
    normalizeButton.type = "button";
    normalizeButton.className = "speaker-asset-action";
    normalizeButton.textContent = "规范化512";
    normalizeButton.addEventListener("click", async () => {
      normalizeButton.disabled = true;
      label.textContent = "正在规范化...";
      try {
        const result = await normalizePortraitAsset(found);
        await loadAssetFiles();
        label.textContent = `已规范化为 512x512，备份：${result.backupPath || "无"}`;
        image.src = `/api/assets/file?path=${encodeURIComponent(found)}&v=${Date.now()}`;
      } catch (error) {
        label.textContent = formatNormalizePortraitError(error);
      } finally {
        normalizeButton.disabled = false;
      }
    });

    info.append(label, path, normalizeButton);

    statusNode.append(image, info);
    return;
  }

  statusNode.className = "speaker-asset-preview muted";
  statusNode.textContent = "未找到同名图片；创建数据前请确认 assets/art/head 下已有头像 PNG。";
}

async function normalizePortraitAsset(assetPath) {
  const lower = assetPath.toLowerCase();
  const supportedFolder = lower.startsWith("art/head/") || lower.startsWith("art/item/");
  if (!supportedFolder || !lower.endsWith(".png")) {
    throw new Error("目前只支持规范化 assets/art/head 或 assets/art/item 下的 PNG 图片。");
  }

  const image = await loadImage(`/api/assets/file?path=${encodeURIComponent(assetPath)}&v=${Date.now()}`);
  const canvas = document.createElement("canvas");
  canvas.width = 512;
  canvas.height = 512;
  const context = canvas.getContext("2d");
  context.clearRect(0, 0, 512, 512);

  const scale = Math.min(512 / image.naturalWidth, 512 / image.naturalHeight);
  const width = Math.max(1, Math.round(image.naturalWidth * scale));
  const height = Math.max(1, Math.round(image.naturalHeight * scale));
  const x = Math.round((512 - width) / 2);
  const y = Math.round((512 - height) / 2);
  context.imageSmoothingEnabled = true;
  context.imageSmoothingQuality = "high";
  context.drawImage(image, x, y, width, height);

  const pngBase64 = canvas.toDataURL("image/png").split(",", 2)[1];
  return requestJson("/api/assets/portrait/normalize", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      path: assetPath,
      pngBase64,
    }),
  });
}

function loadImage(src) {
  return new Promise((resolve, reject) => {
    const image = new Image();
    image.onload = () => resolve(image);
    image.onerror = () => reject(new Error("图片读取失败，无法规范化。"));
    image.src = src;
  });
}

function readFileAsDataUrl(file) {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result || ""));
    reader.onerror = () => reject(new Error("图片读取失败，无法上传。"));
    reader.readAsDataURL(file);
  });
}

function formatNormalizePortraitError(error) {
  const message = error instanceof Error ? error.message : String(error);
  if (message === "Not Found" || message.includes("404")) {
    return "工具后端未更新，请重启 JsonEditor 服务后再点规范化512。";
  }

  return message;
}

function normalizeToolAssetValue(value) {
  return value
    .trim()
    .replaceAll("\\", "/")
    .replace(/^res:\/\/assets\/art\//i, "")
    .replace(/^assets\/art\//i, "")
    .replace(/^art\//i, "")
    .replace(/\.(png|jpg|jpeg|webp)$/i, "");
}

function normalizeToolSearchValue(value) {
  return String(value || "")
    .trim()
    .toLowerCase()
    .replaceAll("\\", "/")
    .replace(/^res:\/\/assets\/art\//i, "")
    .replace(/^assets\/art\//i, "")
    .replace(/^art\//i, "")
    .replace(/\.(png|jpg|jpeg|webp)$/i, "")
    .replace(/[^a-z0-9\u4e00-\u9fff]+/g, "");
}

function toPinyinSlug(value) {
  const table = {
    阿: "a", 敖: "ao", 白: "bai", 包: "bao", 宝: "bao", 北: "bei", 本: "ben", 冰: "bing", 伯: "bo", 博: "bo",
    蔡: "cai", 常: "chang", 陈: "chen", 程: "cheng", 崇: "chong", 仇: "chou", 春: "chun",
    达: "da", 大: "da", 丁: "ding", 狄: "di", 段: "duan", 多: "duo",
    范: "fan", 方: "fang", 菲: "fei", 冯: "feng", 福: "fu",
    高: "gao", 公: "gong", 关: "guan", 桂: "gui", 郭: "guo",
    海: "hai", 韩: "han", 和: "he", 何: "he", 赫: "he", 洪: "hong", 胡: "hu", 花: "hua", 华: "hua", 霍: "huo", 黄: "huang",
    家: "jia", 蒋: "jiang", 金: "jin", 镜: "jing",
    康: "kang", 柯: "ke", 空: "kong",
    来: "lai", 蓝: "lan", 郎: "lang", 李: "li", 梁: "liang", 廖: "liao", 林: "lin", 凌: "ling", 刘: "liu", 陆: "lu", 洛: "luo", 罗: "luo",
    马: "ma", 苗: "miao", 闵: "min", 木: "mu", 慕: "mu",
    南: "nan", 宁: "ning",
    欧: "ou",
    彭: "peng", 平: "ping",
    祁: "qi", 齐: "qi", 乾: "qian", 青: "qing", 丘: "qiu",
    任: "ren", 茹: "ru", 阮: "ruan",
    石: "shi", 士: "shi", 双: "shuang", 水: "shui", 苏: "su",
    泰: "tai", 谭: "tan", 唐: "tang", 天: "tian", 田: "tian", 铁: "tie", 童: "tong",
    万: "wan", 王: "wang", 韦: "wei", 卫: "wei", 文: "wen", 闻: "wen", 无: "wu", 吴: "wu",
    夏: "xia", 香: "xiang", 萧: "xiao", 小: "xiao", 谢: "xie", 心: "xin", 星: "xing", 徐: "xu", 许: "xu", 薛: "xue",
    杨: "yang", 阳: "yang", 姚: "yao", 叶: "ye", 殷: "yin", 英: "ying", 余: "yu", 鱼: "yu", 袁: "yuan", 岳: "yue",
    张: "zhang", 章: "zhang", 赵: "zhao", 郑: "zheng", 周: "zhou", 朱: "zhu", 庄: "zhuang", 卓: "zhuo",
  };

  let result = "";
  for (const char of String(value || "").trim()) {
    if (/[a-z0-9]/i.test(char)) {
      result += char.toLowerCase();
    } else if (table[char]) {
      result += table[char];
    }
  }

  return result;
}

function renderPortraitCheckTool() {
  elements.portraitCheckBox.replaceChildren();

  const description = document.createElement("div");
  description.className = "static-tool-note";
  description.textContent = "静态检查角色 portrait、头像资源、剧情 speaker 与头像图片尺寸/透明度。只检查，不自动修改数据或图片。";

  const checkButton = document.createElement("button");
  checkButton.type = "button";
  checkButton.className = "primary";
  checkButton.textContent = "检查头像";

  const status = document.createElement("div");
  status.className = "static-tool-status muted";

  const resultBox = document.createElement("div");
  resultBox.className = "portrait-check-result";

  checkButton.addEventListener("click", async () => {
    checkButton.disabled = true;
    status.className = "static-tool-status muted";
    status.textContent = "正在检查...";
    resultBox.replaceChildren();

    try {
      state.portraitCheck = await requestJson("/api/static/portraits/check");
      status.className = `static-tool-status ${state.portraitCheck.ok ? "ok" : "bad"}`;
      status.textContent = state.portraitCheck.ok ? "检查完成，没有阻断问题。" : "检查完成，发现需要处理的问题。";
      renderPortraitCheckResult(resultBox, state.portraitCheck);
    } catch (error) {
      state.portraitCheck = null;
      status.className = "static-tool-status bad";
      status.textContent = error.message;
    } finally {
      checkButton.disabled = false;
    }
  });

  elements.portraitCheckBox.append(description, checkButton, status, resultBox);

  if (state.portraitCheck) {
    status.className = `static-tool-status ${state.portraitCheck.ok ? "ok" : "bad"}`;
    status.textContent = state.portraitCheck.ok ? "上次检查没有阻断问题。" : "上次检查发现问题。";
    renderPortraitCheckResult(resultBox, state.portraitCheck);
  }
}

function renderPortraitCheckResult(parent, result) {
  parent.replaceChildren();
  const summary = result.summary;

  const summaryGrid = document.createElement("div");
  summaryGrid.className = "portrait-summary";
  summaryGrid.append(
    createPortraitMetric("角色", summary.characterCount),
    createPortraitMetric("头像资源", summary.portraitResourceCount),
    createPortraitMetric("剧情说话", summary.storySpeakerCount),
    createPortraitMetric("已查图片", summary.checkedPortraitCount),
    createPortraitMetric("错误", summary.errors, "error"),
    createPortraitMetric("警告", summary.warnings, "warn"),
    createPortraitMetric("提示", summary.infos, "info")
  );
  parent.appendChild(summaryGrid);

  if (!Array.isArray(result.issues) || result.issues.length === 0) {
    const empty = document.createElement("div");
    empty.className = "portrait-empty ok";
    empty.textContent = "头像链路看起来都通。";
    parent.appendChild(empty);
    return;
  }

  const list = document.createElement("div");
  list.className = "portrait-issue-list";
  for (const issue of result.issues.slice(0, 120)) {
    list.appendChild(createPortraitIssueRow(issue));
  }

  parent.appendChild(list);

  if (result.issues.length > 120) {
    const more = document.createElement("div");
    more.className = "static-tool-note";
    more.textContent = `仅显示前 120 条，共 ${result.issues.length} 条。`;
    parent.appendChild(more);
  }
}

function createPortraitMetric(label, value, severity = "") {
  const item = document.createElement("div");
  item.className = `portrait-metric ${severity}`;
  const valueNode = document.createElement("strong");
  valueNode.textContent = String(value);
  const labelNode = document.createElement("span");
  labelNode.textContent = label;
  item.append(valueNode, labelNode);
  return item;
}

function createPortraitIssueRow(issue) {
  const row = document.createElement("div");
  row.className = `portrait-issue ${issue.severity || "info"}`;

  const badge = document.createElement("span");
  badge.className = "portrait-issue-badge";
  badge.textContent = issue.severity === "error" ? "错误" : issue.severity === "warn" ? "警告" : "提示";

  const message = document.createElement("div");
  message.className = "portrait-issue-message";
  message.textContent = issue.message;

  const meta = document.createElement("div");
  meta.className = "portrait-issue-meta";
  meta.textContent = [
    issue.area,
    issue.definitionId,
    issue.dataPath ? `${issue.dataPath}${issue.line ? `:${issue.line}` : ""}` : "",
    issue.assetPath || "",
  ].filter(Boolean).join(" · ");

  const actions = document.createElement("div");
  actions.className = "portrait-issue-actions";
  if (issue.dataPath) {
    const dataButton = document.createElement("button");
    dataButton.type = "button";
    dataButton.textContent = "打开数据";
    dataButton.addEventListener("click", async () => {
      setMode("data");
      await openDataFile(issue.dataPath);
      if (issue.line) {
        selectLine(issue.line);
      }
    });
    actions.appendChild(dataButton);
  }

  if (issue.assetPath && issue.assetExists) {
    const assetButton = document.createElement("button");
    assetButton.type = "button";
    assetButton.textContent = "预览图片";
    assetButton.addEventListener("click", () => {
      setMode("assets");
      openAssetFile(issue.assetPath);
    });
    actions.appendChild(assetButton);
  }

  row.append(badge, message, meta, actions);
  return row;
}

function renderCharacterCheckTool() {
  elements.characterCheckBox.replaceChildren();

  if (isItemFile()) {
    renderItemCheckTool();
    return;
  }

  if (!isCharacterFile() || state.formRecords.length === 0) {
    const empty = document.createElement("div");
    empty.className = "static-tool-note";
    empty.textContent = "选择 characters.json 中的角色后，这里会显示当前角色的引用和配置检查。";
    elements.characterCheckBox.appendChild(empty);
    return;
  }

  const record = state.formRecords[state.selectedRecordIndex];
  if (!record) {
    const empty = document.createElement("div");
    empty.className = "static-tool-note";
    empty.textContent = "未选择角色。";
    elements.characterCheckBox.appendChild(empty);
    return;
  }

  const portraitInfo = getCharacterPortraitInfo(record);
  const issues = getCharacterValidationIssues(record);
  const summary = document.createElement("div");
  summary.className = "portrait-summary";
  summary.append(
    createPortraitMetric("角色", 1),
    createPortraitMetric("问题", issues.length, issues.some((issue) => issue.severity === "error") ? "error" : issues.length > 0 ? "warn" : "info"),
    createPortraitMetric("有资源", portraitInfo.resourceExists ? 1 : 0, portraitInfo.resourceExists ? "info" : "error"),
    createPortraitMetric("有图片", portraitInfo.assetExists ? 1 : 0, portraitInfo.assetExists ? "info" : "error"),
  );
  elements.characterCheckBox.appendChild(summary);

  if (issues.length === 0) {
    const ok = document.createElement("div");
    ok.className = "portrait-empty ok";
    ok.textContent = "当前角色没有明显的引用或配置问题。";
    elements.characterCheckBox.appendChild(ok);
    return;
  }

  const list = document.createElement("div");
  list.className = "portrait-issue-list";
  for (const issue of issues) {
    const row = document.createElement("div");
    row.className = `portrait-issue ${issue.severity || "warn"}`;
    const badge = document.createElement("span");
    badge.className = "portrait-issue-badge";
    badge.textContent = issue.severity === "error" ? "错误" : "警告";
    const message = document.createElement("div");
    message.className = "portrait-issue-message";
    message.textContent = issue.message;
    const meta = document.createElement("div");
    meta.className = "portrait-issue-meta";
    meta.textContent = issue.definitionId || "当前角色";
    const actions = document.createElement("div");
    actions.className = "portrait-issue-actions";
    if (issue.definitionId && Array.isArray(issue.types) && issue.types.length > 0) {
      const openButton = document.createElement("button");
      openButton.type = "button";
      openButton.textContent = "打开定义";
      openButton.addEventListener("click", () => revealDefinitionById(issue.definitionId, issue.types));
      actions.appendChild(openButton);
    }
    row.append(badge, message, meta, actions);
    list.appendChild(row);
  }
  elements.characterCheckBox.appendChild(list);
}

function renderItemCheckTool() {
  if (state.formRecords.length === 0) {
    const empty = document.createElement("div");
    empty.className = "static-tool-note";
    empty.textContent = "选择 items.json 中的物品后，这里会显示当前物品的图片和引用检查。";
    elements.characterCheckBox.appendChild(empty);
    return;
  }

  const record = state.formRecords[state.selectedRecordIndex];
  if (!record) {
    const empty = document.createElement("div");
    empty.className = "static-tool-note";
    empty.textContent = "未选择物品。";
    elements.characterCheckBox.appendChild(empty);
    return;
  }

  const pictureInfo = getItemPictureInfo(record);
  const issues = getItemValidationIssues(record);
  const summary = document.createElement("div");
  summary.className = "portrait-summary";
  summary.append(
    createPortraitMetric("物品", 1),
    createPortraitMetric("问题", issues.length, issues.some((issue) => issue.severity === "error") ? "error" : issues.length > 0 ? "warn" : "info"),
    createPortraitMetric("有资源", pictureInfo.resourceExists ? 1 : 0, pictureInfo.resourceExists ? "info" : "error"),
    createPortraitMetric("有图片", pictureInfo.assetExists ? 1 : 0, pictureInfo.assetExists ? "info" : "error"),
  );
  elements.characterCheckBox.appendChild(summary);

  if (issues.length === 0) {
    const ok = document.createElement("div");
    ok.className = "portrait-empty ok";
    ok.textContent = "当前物品没有明显的图片或引用问题。";
    elements.characterCheckBox.appendChild(ok);
    return;
  }

  const list = document.createElement("div");
  list.className = "portrait-issue-list";
  for (const issue of issues) {
    const row = document.createElement("div");
    row.className = `portrait-issue ${issue.severity || "warn"}`;
    const badge = document.createElement("span");
    badge.className = "portrait-issue-badge";
    badge.textContent = issue.severity === "error" ? "错误" : "警告";
    const message = document.createElement("div");
    message.className = "portrait-issue-message";
    message.textContent = issue.message;
    const meta = document.createElement("div");
    meta.className = "portrait-issue-meta";
    meta.textContent = issue.definitionId || "当前物品";
    const actions = document.createElement("div");
    actions.className = "portrait-issue-actions";
    if (issue.definitionId && Array.isArray(issue.types) && issue.types.length > 0) {
      const openButton = document.createElement("button");
      openButton.type = "button";
      openButton.textContent = "打开定义";
      openButton.addEventListener("click", () => revealDefinitionById(issue.definitionId, issue.types));
      actions.appendChild(openButton);
    }
    row.append(badge, message, meta, actions);
    list.appendChild(row);
  }
  elements.characterCheckBox.appendChild(list);
}

async function revealDefinitionById(id, types) {
  const definitions = state.contentIndex.definitionsById.get(id) || [];
  const definition = definitions.find((candidate) => types.includes(candidate.type));
  if (!definition) {
    return;
  }

  await revealDefinition(definition);
}

function createPortraitResource(portraitId, assetValue) {
  return requestJson("/api/static/portrait-resource", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      portraitId,
      assetValue,
    }),
  });
}

function createItemResource(pictureId, assetValue) {
  return requestJson("/api/static/item-resource", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      pictureId,
      assetValue,
    }),
  });
}

function getBindableItemPictureId(record) {
  const current = typeof record?.picture === "string" ? record.picture.trim() : "";
  if (current) {
    return current;
  }

  const itemId = typeof record?.id === "string" && record.id.trim()
    ? record.id.trim()
    : "新物品";
  return `物品.${itemId}`;
}

async function uploadItemImageAndBind(record, file, pictureId) {
  const dataUrl = await readFileAsDataUrl(file);
  const [, mimeType = "application/octet-stream", base64 = ""] = dataUrl.match(/^data:([^;]+);base64,(.+)$/) || [];
  if (!base64) {
    throw new Error("图片读取失败，未拿到可上传的数据。");
  }

  return requestJson("/api/assets/item/upload-bind", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({
      itemId: typeof record?.id === "string" ? record.id.trim() : "",
      pictureId,
      fileName: file.name,
      mimeType,
      imageBase64: base64,
    }),
  });
}

function openPortraitPicker(initialAssetPath = "") {
  state.portraitPicker.open = true;
  state.portraitPicker.search = "";
  state.portraitPicker.selectedAssetPath = initialAssetPath || getCurrentCharacterPortraitAssetPath();
  renderPortraitPicker();
}

function closePortraitPicker() {
  state.portraitPicker.open = false;
  state.portraitPicker.search = "";
  state.portraitPicker.selectedAssetPath = "";
  renderPortraitPicker();
}

function getCurrentCharacterRecord() {
  if (!isCharacterFile()) {
    return null;
  }

  return state.formRecords[state.selectedRecordIndex] || null;
}

function getCurrentCharacterPortraitAssetPath() {
  const record = getCurrentCharacterRecord();
  if (!record) {
    return "";
  }

  return getCharacterPortraitInfo(record).previewPath || "";
}

function openItemPicturePicker(initialAssetPath = "") {
  state.itemPicturePicker.open = true;
  state.itemPicturePicker.search = "";
  state.itemPicturePicker.selectedAssetPath = initialAssetPath || getCurrentItemPictureAssetPath();
  renderItemPicturePicker();
}

function closeItemPicturePicker() {
  state.itemPicturePicker.open = false;
  state.itemPicturePicker.search = "";
  state.itemPicturePicker.selectedAssetPath = "";
  renderItemPicturePicker();
}

function getCurrentItemRecord() {
  if (!isItemFile()) {
    return null;
  }

  return state.formRecords[state.selectedRecordIndex] || null;
}

function getCurrentItemPictureAssetPath() {
  const record = getCurrentItemRecord();
  if (!record) {
    return "";
  }

  return getItemPictureInfo(record).previewPath || "";
}

function getHeadPortraitLibraryEntries() {
  const resourcesByAssetPath = new Map();
  for (const resource of state.contentIndex.resourcesById.values()) {
    if (resource?.group !== "头像" || typeof resource?.id !== "string") {
      continue;
    }

    const assetPath = resolveResourceAssetPath(resource);
    if (!assetPath) {
      continue;
    }

    const linked = resourcesByAssetPath.get(assetPath) || [];
    linked.push(resource.id);
    resourcesByAssetPath.set(assetPath, linked);
  }

  return state.assetFiles
    .filter((file) => file.path.toLowerCase().startsWith("art/head/") && isImage(file.path.toLowerCase()))
    .map((file) => {
      const basename = (file.name || file.path.split("/").pop() || "").replace(/\.[^.]+$/i, "");
      const resourceIds = (resourcesByAssetPath.get(file.path) || []).sort((left, right) => left.localeCompare(right, "zh-Hans-CN"));
      return {
        assetPath: file.path,
        assetValue: normalizeToolAssetValue(file.path),
        basename,
        resourceIds,
      };
    })
    .sort((left, right) => left.basename.localeCompare(right.basename, "zh-Hans-CN"));
}

function getItemPictureLibraryEntries() {
  const resourcesByAssetPath = new Map();
  for (const resource of state.contentIndex.resourcesById.values()) {
    if (resource?.group !== "物品" || typeof resource?.id !== "string") {
      continue;
    }

    const assetPath = resolveResourceAssetPath(resource);
    if (!assetPath) {
      continue;
    }

    const linked = resourcesByAssetPath.get(assetPath) || [];
    linked.push(resource.id);
    resourcesByAssetPath.set(assetPath, linked);
  }

  return state.assetFiles
    .filter((file) => file.path.toLowerCase().startsWith("art/item/") && isImage(file.path.toLowerCase()))
    .map((file) => {
      const basename = (file.name || file.path.split("/").pop() || "").replace(/\.[^.]+$/i, "");
      const resourceIds = (resourcesByAssetPath.get(file.path) || []).sort((left, right) => left.localeCompare(right, "zh-Hans-CN"));
      return {
        assetPath: file.path,
        assetValue: normalizeToolAssetValue(file.path),
        basename,
        resourceIds,
      };
    })
    .sort((left, right) => left.basename.localeCompare(right.basename, "zh-Hans-CN"));
}

function getSelectedPortraitLibraryEntry(entries) {
  if (entries.length === 0) {
    return null;
  }

  if (state.portraitPicker.selectedAssetPath) {
    const selected = entries.find((entry) => entry.assetPath === state.portraitPicker.selectedAssetPath);
    if (selected) {
      return selected;
    }
  }

  return entries[0];
}

function matchesPortraitPickerSearch(entry, query) {
  if (!query) {
    return true;
  }

  const haystack = [
    entry.basename,
    entry.assetPath,
    entry.assetValue,
    ...entry.resourceIds,
  ].join(" ").toLowerCase();

  return haystack.includes(query);
}

function getSuggestedPortraitId(record, entry) {
  const currentPortraitId = typeof record?.portrait === "string" ? record.portrait.trim() : "";
  if (currentPortraitId && !state.contentIndex.resourcesById.has(currentPortraitId)) {
    return currentPortraitId;
  }

  return `头像.${entry.basename || "新头像"}`;
}

async function usePortraitLibraryResource(record, portraitId) {
  closePortraitPicker();
  updateRecordField(record, "portrait", portraitId, { rerender: true });
}

function getSelectedItemPictureLibraryEntry(entries) {
  if (entries.length === 0) {
    return null;
  }

  if (state.itemPicturePicker.selectedAssetPath) {
    const selected = entries.find((entry) => entry.assetPath === state.itemPicturePicker.selectedAssetPath);
    if (selected) {
      return selected;
    }
  }

  return entries[0];
}

function matchesItemPicturePickerSearch(entry, query) {
  if (!query) {
    return true;
  }

  const haystack = [
    entry.basename,
    entry.assetPath,
    entry.assetValue,
    ...entry.resourceIds,
  ].join(" ").toLowerCase();

  return haystack.includes(query);
}

function getSuggestedItemPictureId(record, entry) {
  const currentPictureId = typeof record?.picture === "string" ? record.picture.trim() : "";
  if (currentPictureId && !state.contentIndex.resourcesById.has(currentPictureId)) {
    return currentPictureId;
  }

  return `物品.${entry.basename || record?.id || "新物品"}`;
}

async function useItemPictureLibraryResource(record, pictureId) {
  closeItemPicturePicker();
  updateRecordField(record, "picture", pictureId, { rerender: true });
}

async function createAndUsePortraitLibraryResource(record, portraitId, entry, button) {
  const value = portraitId.trim();
  if (!value) {
    showValidation(false, "请先填写头像资源 id。");
    return;
  }

  button.disabled = true;
  try {
    const result = await createPortraitResource(value, entry.assetValue);
    record.portrait = value;
    syncFormToEditor();
    await loadDataFiles();
    await rebuildContentIndex();
    closePortraitPicker();
    showValidation(result.validation.ok, result.validation.message);
    renderFormView();
  } catch (error) {
    showValidation(false, error instanceof Error ? error.message : String(error));
  } finally {
    button.disabled = false;
  }
}

async function createAndUseItemPictureLibraryResource(record, pictureId, entry, button) {
  const value = pictureId.trim();
  if (!value) {
    showValidation(false, "请先填写物品资源 id。");
    return;
  }

  button.disabled = true;
  try {
    const result = await createItemResource(value, entry.assetValue);
    record.picture = value;
    syncFormToEditor();
    await loadDataFiles();
    await rebuildContentIndex();
    closeItemPicturePicker();
    showValidation(result.validation.ok, result.validation.message);
    renderFormView();
  } catch (error) {
    showValidation(false, error instanceof Error ? error.message : String(error));
  } finally {
    button.disabled = false;
  }
}

function renderPortraitPicker() {
  const scrollState = capturePortraitPickerScrollState();
  const existing = document.getElementById("portraitPickerOverlay");
  if (existing) {
    existing.remove();
  }

  if (!state.portraitPicker.open) {
    return;
  }

  const record = getCurrentCharacterRecord();
  if (!record) {
    state.portraitPicker.open = false;
    return;
  }

  const overlay = document.createElement("div");
  overlay.id = "portraitPickerOverlay";
  overlay.className = "portrait-picker-overlay";
  overlay.addEventListener("click", (event) => {
    if (event.target === overlay) {
      closePortraitPicker();
    }
  });

  const dialog = document.createElement("div");
  dialog.className = "portrait-picker-dialog";

  const header = document.createElement("div");
  header.className = "portrait-picker-header";
  const titleGroup = document.createElement("div");
  const title = document.createElement("div");
  title.className = "portrait-picker-title";
  title.textContent = "头像选择器";
  const subtitle = document.createElement("div");
  subtitle.className = "portrait-picker-subtitle";
  subtitle.textContent = "预览全部头像，已有资源可直接使用，未绑定资源可创建后立即替换当前角色。";
  titleGroup.append(title, subtitle);
  const closeButton = document.createElement("button");
  closeButton.type = "button";
  closeButton.textContent = "关闭";
  closeButton.addEventListener("click", closePortraitPicker);
  header.append(titleGroup, closeButton);

  const search = document.createElement("input");
  search.type = "search";
  search.className = "portrait-picker-search";
  search.placeholder = "搜索资源 id、文件名、路径";
  search.value = state.portraitPicker.search;
  search.addEventListener("input", () => {
    state.portraitPicker.search = search.value;
    renderPortraitPicker();
  });

  const allEntries = getHeadPortraitLibraryEntries();
  const query = state.portraitPicker.search.trim().toLowerCase();
  const entries = allEntries.filter((entry) => matchesPortraitPickerSearch(entry, query));
  const selectedEntry = getSelectedPortraitLibraryEntry(entries);
  if (selectedEntry) {
    state.portraitPicker.selectedAssetPath = selectedEntry.assetPath;
  }

  const body = document.createElement("div");
  body.className = "portrait-picker-body";

  const gallery = document.createElement("div");
  gallery.className = "portrait-picker-gallery";
  if (entries.length === 0) {
    const empty = document.createElement("div");
    empty.className = "record-empty";
    empty.textContent = "没有匹配的头像图片";
    gallery.appendChild(empty);
  } else {
    for (const entry of entries) {
      const card = document.createElement("button");
      card.type = "button";
      card.className = "portrait-picker-card";
      card.classList.toggle("active", entry.assetPath === state.portraitPicker.selectedAssetPath);
      card.addEventListener("click", () => {
        state.portraitPicker.selectedAssetPath = entry.assetPath;
        renderPortraitPicker();
      });

      const image = document.createElement("img");
      image.className = "portrait-picker-card-image";
      image.src = `/api/assets/file?path=${encodeURIComponent(entry.assetPath)}`;
      image.alt = entry.basename;

      const content = document.createElement("div");
      content.className = "portrait-picker-card-content";

      const cardTitle = document.createElement("div");
      cardTitle.className = "portrait-picker-card-title";
      cardTitle.textContent = entry.resourceIds[0] || entry.basename;

      const cardMeta = document.createElement("div");
      cardMeta.className = "portrait-picker-card-meta";
      cardMeta.textContent = entry.resourceIds.length > 0
        ? `${entry.resourceIds.length} 个资源`
        : "未绑定资源";

      const cardPath = document.createElement("div");
      cardPath.className = "portrait-picker-card-path";
      cardPath.textContent = entry.assetPath;

      content.append(cardTitle, cardMeta, cardPath);
      card.append(image, content);
      gallery.appendChild(card);
    }
  }

  const detail = document.createElement("div");
  detail.className = "portrait-picker-detail";
  if (!selectedEntry) {
    const empty = document.createElement("div");
    empty.className = "record-empty";
    empty.textContent = "没有可用头像";
    detail.appendChild(empty);
  } else {
    const preview = document.createElement("img");
    preview.className = "portrait-picker-detail-image";
    preview.src = `/api/assets/file?path=${encodeURIComponent(selectedEntry.assetPath)}&v=${Date.now()}`;
    preview.alt = selectedEntry.basename;

    const info = document.createElement("div");
    info.className = "portrait-picker-detail-info";
    info.append(
      createCharacterMetaRow("文件", selectedEntry.assetPath),
      createCharacterMetaRow("资源 value", selectedEntry.assetValue),
      createCharacterMetaRow("已绑定资源", selectedEntry.resourceIds.length > 0 ? selectedEntry.resourceIds.join(" / ") : "暂无"),
    );

    const actionBlock = document.createElement("div");
    actionBlock.className = "portrait-picker-detail-actions";

    if (selectedEntry.resourceIds.length > 0) {
      for (const resourceId of selectedEntry.resourceIds) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "primary";
        button.textContent = `使用 ${resourceId}`;
        button.addEventListener("click", async () => {
          await usePortraitLibraryResource(record, resourceId);
        });
        actionBlock.appendChild(button);
      }
    } else {
      const helper = document.createElement("div");
      helper.className = "static-tool-note";
      helper.textContent = "这张图还没有头像资源。创建资源后会自动写回当前角色的 portrait。";
      const portraitIdInput = document.createElement("input");
      portraitIdInput.type = "text";
      portraitIdInput.className = "portrait-picker-resource-input";
      portraitIdInput.value = getSuggestedPortraitId(record, selectedEntry);
      const createButton = document.createElement("button");
      createButton.type = "button";
      createButton.className = "primary";
      createButton.textContent = "创建资源并使用";
      createButton.addEventListener("click", async () => {
        await createAndUsePortraitLibraryResource(record, portraitIdInput.value, selectedEntry, createButton);
      });
      actionBlock.append(helper, portraitIdInput, createButton);
    }

    const footerActions = document.createElement("div");
    footerActions.className = "portrait-picker-footer-actions";
    const previewButton = document.createElement("button");
    previewButton.type = "button";
    previewButton.textContent = "在资产面板中打开";
    previewButton.addEventListener("click", () => {
      closePortraitPicker();
      setMode("assets");
      openAssetFile(selectedEntry.assetPath);
    });
    footerActions.appendChild(previewButton);

    detail.append(preview, info, actionBlock, footerActions);
  }

  body.append(gallery, detail);
  dialog.append(header, search, body);
  overlay.appendChild(dialog);
  document.body.appendChild(overlay);
  restorePortraitPickerScrollState(scrollState);
}

function renderItemPicturePicker() {
  const scrollState = captureItemPicturePickerScrollState();
  const existing = document.getElementById("itemPicturePickerOverlay");
  if (existing) {
    existing.remove();
  }

  if (!state.itemPicturePicker.open) {
    return;
  }

  const record = getCurrentItemRecord();
  if (!record) {
    state.itemPicturePicker.open = false;
    return;
  }

  const overlay = document.createElement("div");
  overlay.id = "itemPicturePickerOverlay";
  overlay.className = "portrait-picker-overlay";
  overlay.addEventListener("click", (event) => {
    if (event.target === overlay) {
      closeItemPicturePicker();
    }
  });

  const dialog = document.createElement("div");
  dialog.className = "portrait-picker-dialog";

  const header = document.createElement("div");
  header.className = "portrait-picker-header";
  const titleGroup = document.createElement("div");
  const title = document.createElement("div");
  title.className = "portrait-picker-title";
  title.textContent = "物品图片选择器";
  const subtitle = document.createElement("div");
  subtitle.className = "portrait-picker-subtitle";
  subtitle.textContent = "预览全部物品缩略图，已有资源可直接使用，未绑定资源可创建后立即替换当前物品。";
  titleGroup.append(title, subtitle);
  const closeButton = document.createElement("button");
  closeButton.type = "button";
  closeButton.textContent = "关闭";
  closeButton.addEventListener("click", closeItemPicturePicker);
  header.append(titleGroup, closeButton);

  const search = document.createElement("input");
  search.type = "search";
  search.className = "portrait-picker-search";
  search.placeholder = "搜索资源 id、文件名、路径";
  search.value = state.itemPicturePicker.search;
  search.addEventListener("input", () => {
    state.itemPicturePicker.search = search.value;
    renderItemPicturePicker();
  });

  const allEntries = getItemPictureLibraryEntries();
  const query = state.itemPicturePicker.search.trim().toLowerCase();
  const entries = allEntries.filter((entry) => matchesItemPicturePickerSearch(entry, query));
  const selectedEntry = getSelectedItemPictureLibraryEntry(entries);
  if (selectedEntry) {
    state.itemPicturePicker.selectedAssetPath = selectedEntry.assetPath;
  }

  const body = document.createElement("div");
  body.className = "portrait-picker-body";

  const gallery = document.createElement("div");
  gallery.className = "portrait-picker-gallery item-picture-picker-gallery";
  if (entries.length === 0) {
    const empty = document.createElement("div");
    empty.className = "record-empty";
    empty.textContent = "没有匹配的物品图片";
    gallery.appendChild(empty);
  } else {
    for (const entry of entries) {
      const card = document.createElement("button");
      card.type = "button";
      card.className = "portrait-picker-card item-picture-picker-card";
      card.classList.toggle("active", entry.assetPath === state.itemPicturePicker.selectedAssetPath);
      card.addEventListener("click", () => {
        state.itemPicturePicker.selectedAssetPath = entry.assetPath;
        renderItemPicturePicker();
      });

      const image = document.createElement("img");
      image.className = "portrait-picker-card-image item-picture-picker-card-image";
      image.src = `/api/assets/file?path=${encodeURIComponent(entry.assetPath)}`;
      image.alt = entry.basename;

      const content = document.createElement("div");
      content.className = "portrait-picker-card-content";

      const cardTitle = document.createElement("div");
      cardTitle.className = "portrait-picker-card-title";
      cardTitle.textContent = entry.resourceIds[0] || entry.basename;

      const cardMeta = document.createElement("div");
      cardMeta.className = "portrait-picker-card-meta";
      cardMeta.textContent = entry.resourceIds.length > 0
        ? `${entry.resourceIds.length} 个资源`
        : "未绑定资源";

      const cardPath = document.createElement("div");
      cardPath.className = "portrait-picker-card-path";
      cardPath.textContent = entry.assetPath;

      content.append(cardTitle, cardMeta, cardPath);
      card.append(image, content);
      gallery.appendChild(card);
    }
  }

  const detail = document.createElement("div");
  detail.className = "portrait-picker-detail";
  if (!selectedEntry) {
    const empty = document.createElement("div");
    empty.className = "record-empty";
    empty.textContent = "没有可用物品图片";
    detail.appendChild(empty);
  } else {
    const preview = document.createElement("img");
    preview.className = "portrait-picker-detail-image item-picture-picker-detail-image";
    preview.src = `/api/assets/file?path=${encodeURIComponent(selectedEntry.assetPath)}&v=${Date.now()}`;
    preview.alt = selectedEntry.basename;

    const info = document.createElement("div");
    info.className = "portrait-picker-detail-info";
    info.append(
      createCharacterMetaRow("文件", selectedEntry.assetPath),
      createCharacterMetaRow("资源 value", selectedEntry.assetValue),
      createCharacterMetaRow("已绑定资源", selectedEntry.resourceIds.length > 0 ? selectedEntry.resourceIds.join(" / ") : "暂无"),
    );

    const actionBlock = document.createElement("div");
    actionBlock.className = "portrait-picker-detail-actions";

    if (selectedEntry.resourceIds.length > 0) {
      for (const resourceId of selectedEntry.resourceIds) {
        const button = document.createElement("button");
        button.type = "button";
        button.className = "primary";
        button.textContent = `使用 ${resourceId}`;
        button.addEventListener("click", async () => {
          await useItemPictureLibraryResource(record, resourceId);
        });
        actionBlock.appendChild(button);
      }
    } else {
      const helper = document.createElement("div");
      helper.className = "static-tool-note";
      helper.textContent = "这张图还没有物品资源。创建资源后会自动写回当前物品的 picture。";
      const pictureIdInput = document.createElement("input");
      pictureIdInput.type = "text";
      pictureIdInput.className = "portrait-picker-resource-input";
      pictureIdInput.value = getSuggestedItemPictureId(record, selectedEntry);
      const createButton = document.createElement("button");
      createButton.type = "button";
      createButton.className = "primary";
      createButton.textContent = "创建资源并使用";
      createButton.addEventListener("click", async () => {
        await createAndUseItemPictureLibraryResource(record, pictureIdInput.value, selectedEntry, createButton);
      });
      actionBlock.append(helper, pictureIdInput, createButton);
    }

    const footerActions = document.createElement("div");
    footerActions.className = "portrait-picker-footer-actions";
    const previewButton = document.createElement("button");
    previewButton.type = "button";
    previewButton.textContent = "在资产面板中打开";
    previewButton.addEventListener("click", () => {
      closeItemPicturePicker();
      setMode("assets");
      openAssetFile(selectedEntry.assetPath);
    });
    footerActions.appendChild(previewButton);

    detail.append(preview, info, actionBlock, footerActions);
  }

  body.append(gallery, detail);
  dialog.append(header, search, body);
  overlay.appendChild(dialog);
  document.body.appendChild(overlay);
  restoreItemPicturePickerScrollState(scrollState);
}

function capturePortraitPickerScrollState() {
  const overlay = document.getElementById("portraitPickerOverlay");
  return {
    galleryScrollTop: overlay?.querySelector(".portrait-picker-gallery")?.scrollTop ?? 0,
    detailScrollTop: overlay?.querySelector(".portrait-picker-detail")?.scrollTop ?? 0,
  };
}

function captureItemPicturePickerScrollState() {
  const overlay = document.getElementById("itemPicturePickerOverlay");
  return {
    galleryScrollTop: overlay?.querySelector(".portrait-picker-gallery")?.scrollTop ?? 0,
    detailScrollTop: overlay?.querySelector(".portrait-picker-detail")?.scrollTop ?? 0,
  };
}

function restorePortraitPickerScrollState(scrollState) {
  if (!scrollState || !state.portraitPicker.open) {
    return;
  }

  requestAnimationFrame(() => {
    const overlay = document.getElementById("portraitPickerOverlay");
    if (!overlay) {
      return;
    }

    const gallery = overlay.querySelector(".portrait-picker-gallery");
    const detail = overlay.querySelector(".portrait-picker-detail");
    if (gallery) {
      gallery.scrollTop = scrollState.galleryScrollTop;
    }

    if (detail) {
      detail.scrollTop = scrollState.detailScrollTop;
    }
  });
}

function restoreItemPicturePickerScrollState(scrollState) {
  if (!scrollState || !state.itemPicturePicker.open) {
    return;
  }

  requestAnimationFrame(() => {
    const overlay = document.getElementById("itemPicturePickerOverlay");
    if (!overlay) {
      return;
    }

    const gallery = overlay.querySelector(".portrait-picker-gallery");
    const detail = overlay.querySelector(".portrait-picker-detail");
    if (gallery) {
      gallery.scrollTop = scrollState.galleryScrollTop;
    }

    if (detail) {
      detail.scrollTop = scrollState.detailScrollTop;
    }
  });
}

function createToolInput(id, label, placeholder) {
  const input = document.createElement("input");
  input.id = id;
  input.className = "tool-input";
  input.placeholder = placeholder;
  input.setAttribute("aria-label", label);
  return input;
}

function createToolField(label, control) {
  const wrapper = document.createElement("label");
  wrapper.className = "tool-field";
  const text = document.createElement("span");
  text.textContent = label;
  wrapper.append(text, control);
  return wrapper;
}

function getSelectedLookupText() {
  const start = elements.editor.selectionStart ?? 0;
  const end = elements.editor.selectionEnd ?? 0;
  if (start === end) {
    return "";
  }

  return elements.editor.value.slice(start, end).trim().replace(/^"|"$/g, "");
}

async function revealDefinition(definition) {
  state.mode = "data";
  document.body.classList.remove("story-mode");
  elements.dataTab.classList.add("active");
  elements.storyTab.classList.remove("active");
  elements.assetsTab.classList.remove("active");
  elements.saveButton.disabled = false;
  elements.formatButton.disabled = false;
  elements.storyView.classList.add("hidden");
  await openDataFile(definition.path);
  setViewMode("json");
  selectLine(definition.line);
}

async function revealStoryLocation(path, line) {
  if (!(await confirmDiscardChanges())) {
    return;
  }

  setMode("data");
  await openDataFile(path);
  setViewMode("json");
  selectLine(line);
}

function selectLine(lineNumber) {
  const lines = elements.editor.value.split("\n");
  const lineIndex = Math.max(0, Math.min(lineNumber - 1, lines.length - 1));
  let start = 0;
  for (let index = 0; index < lineIndex; index += 1) {
    start += lines[index].length + 1;
  }

  const end = start + lines[lineIndex].length;
  elements.editor.focus();
  elements.editor.setSelectionRange(start, end);
  renderCursorState();
}

async function openLastDataFile() {
  const lastPath = localStorage.getItem(getLastDataPathStorageKey());
  if (!lastPath || !state.dataFiles.some((file) => file.path === lastPath)) {
    return;
  }

  await openDataFile(lastPath);
}

function getLastDataPathStorageKey() {
  return `${storageKeys.lastDataPath}:${state.activeModId || "default"}`;
}

async function confirmDiscardChanges() {
  if (!state.dirty) {
    return true;
  }

  return window.confirm("当前文件尚未保存，是否放弃修改？");
}

function updateSearchMatches() {
  const query = elements.contentSearch.value;
  state.searchMatches = [];
  state.searchIndex = -1;

  if (!query) {
    elements.searchState.textContent = "";
    return;
  }

  const lowerText = elements.editor.value.toLowerCase();
  const lowerQuery = query.toLowerCase();
  let index = lowerText.indexOf(lowerQuery);
  while (index >= 0) {
    state.searchMatches.push(index);
    index = lowerText.indexOf(lowerQuery, index + lowerQuery.length);
  }

  if (state.searchMatches.length === 0) {
    elements.searchState.textContent = "无匹配";
    return;
  }

  elements.searchState.textContent = `${state.searchMatches.length} 处匹配`;
}

function jumpSearch(direction) {
  if (state.searchMatches.length === 0) {
    updateSearchMatches();
  }

  if (state.searchMatches.length === 0) {
    return;
  }

  state.searchIndex = (state.searchIndex + direction + state.searchMatches.length) % state.searchMatches.length;
  const start = state.searchMatches[state.searchIndex];
  const end = start + elements.contentSearch.value.length;
  elements.editor.focus();
  elements.editor.setSelectionRange(start, end);
  elements.searchState.textContent = `${state.searchIndex + 1} / ${state.searchMatches.length}`;
  renderCursorState();
}

function handleGlobalKeydown(event) {
  if (event.key === "Escape" && state.portraitPicker.open) {
    event.preventDefault();
    closePortraitPicker();
    return;
  }

  const command = event.metaKey || event.ctrlKey;
  if (!command) {
    return;
  }

  if (event.key.toLowerCase() === "s") {
    event.preventDefault();
    saveCurrentFile();
  } else if (event.shiftKey && event.key.toLowerCase() === "f") {
    event.preventDefault();
    formatCurrentJson();
  }
}

function formatJsonError(error) {
  const message = error instanceof Error ? error.message : String(error);
  const positionMatch = message.match(/position\s+(\d+)/i);
  if (!positionMatch) {
    return `JSON parse failed: ${message}`;
  }

  const position = Number(positionMatch[1]);
  const lines = elements.editor.value.slice(0, position).split("\n");
  return `JSON parse failed near 行 ${lines.length}，列 ${lines[lines.length - 1].length + 1}: ${message}`;
}

async function requestJson(url, options) {
  const response = await fetch(withActiveMod(url), options);
  const body = await response.json().catch(() => null);
  if (!response.ok) {
    throw new Error(body?.message || response.statusText);
  }

  return body;
}

function withActiveMod(url) {
  if (!url.startsWith("/api/") || url.startsWith("/api/workspace") || !state.activeModId) {
    return url;
  }

  const separator = url.includes("?") ? "&" : "?";
  return `${url}${separator}modId=${encodeURIComponent(state.activeModId)}`;
}

function isImage(path) {
  return [".png", ".jpg", ".jpeg", ".webp", ".bmp", ".gif"].some((extension) => path.endsWith(extension));
}

function isAudio(path) {
  return [".ogg", ".wav", ".mp3"].some((extension) => path.endsWith(extension));
}

function formatFileSize(bytes) {
  if (bytes < 1024) {
    return `${bytes} B`;
  }

  if (bytes < 1024 * 1024) {
    return `${(bytes / 1024).toFixed(1)} KB`;
  }

  return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

function escapeRegExp(value) {
  return value.replace(/[.*+?^${}()|[\]\\]/g, "\\$&");
}

function escapeHtml(value) {
  return String(value)
    .replaceAll("&", "&amp;")
    .replaceAll("<", "&lt;")
    .replaceAll(">", "&gt;")
    .replaceAll('"', "&quot;")
    .replaceAll("'", "&#39;");
}
