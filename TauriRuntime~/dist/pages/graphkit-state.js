// pages/graphkit-state.js
// GraphKit 本地状态：XML 节点图项目、选择状态和稳定渲染签名。
const GRAPHKIT_STORAGE_KEY = 'yokiframe.graphkit.project.v1';
const GRAPHKIT_HISTORY_LIMIT = 48;

let graphKitStorageScope = null;
let graphKitProject = sanitizeGraphKitProject({});
const graphKitState = {
    searchTerm: '',
    selectedNodeId: '',
    selectedNodeIds: [],
    selectedEdgeId: '',
    selectedBlackboardName: '',
    selectedNoteId: '',
    selectedPlacematId: '',
    selectedNodeTypeId: '',
    filePath: '',
    fileName: 'Untitled Graph XML',
    fileBusy: false,
    pendingPortEndpoint: '',
    noticeLevel: '',
    noticeText: '',
    lubanRun: null,
    viewport: {
        scale: 1,
        offsetX: 0,
        offsetY: 0,
    },
    panels: {
        blackboardCollapsed: false,
        inspectorCollapsed: false,
    },
    issuesOpen: false,
    searchOverlay: {
        open: false,
        query: '',
        scope: 'current',
        activeIndex: 0,
    },
    clipboardNodes: [],
    clipboardEdges: [],
    marqueeSelection: {
        active: false,
        pointerId: 0,
        startX: 0,
        startY: 0,
        currentX: 0,
        currentY: 0,
        append: false,
    },
    contextMenu: {
        open: false,
        kind: '',
        x: 0,
        y: 0,
        modelX: 0,
        modelY: 0,
        targetId: '',
        endpoint: '',
        searchTerm: '',
    },
    wireDrag: {
        active: false,
        sourceEndpoint: '',
        currentX: 0,
        currentY: 0,
        targetEndpoint: '',
    },
    undoStack: [],
    redoStack: [],
    dirty: false,
    renderSignature: '',
};

syncGraphKitProjectStorageScope({ force: true });

function getGraphKitStorageKey() {
    return getProjectScopedStorageKey(GRAPHKIT_STORAGE_KEY);
}

function syncGraphKitProjectStorageScope(options = {}) {
    const nextScope = getProjectStorageScopeIdentifier();
    if (!options.force && graphKitStorageScope === nextScope) return false;

    graphKitStorageScope = nextScope;
    graphKitProject = loadGraphKitProject();
    const selectedNodeId = getGraphKitInitialNodeId(graphKitProject);
    graphKitState.selectedNodeId = selectedNodeId;
    graphKitState.selectedNodeIds = selectedNodeId ? [selectedNodeId] : [];
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNoteId = '';
    graphKitState.selectedPlacematId = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.searchTerm = '';
    graphKitState.pendingPortEndpoint = '';
    graphKitState.noticeLevel = options.force ? graphKitState.noticeLevel : 'info';
    graphKitState.noticeText = options.force ? graphKitState.noticeText : '已切换到当前项目的 GraphKit 草稿。';
    graphKitState.lubanRun = null;
    graphKitState.viewport = { scale: 1, offsetX: 0, offsetY: 0 };
    graphKitState.panels = { blackboardCollapsed: false, inspectorCollapsed: false };
    graphKitState.issuesOpen = false;
    graphKitState.searchOverlay = { open: false, query: '', scope: 'current', activeIndex: 0 };
    graphKitState.clipboardNodes = [];
    graphKitState.clipboardEdges = [];
    graphKitState.undoStack = [];
    graphKitState.redoStack = [];
    graphKitState.dirty = false;
    graphKitState.renderSignature = '';
    if (!options.force && typeof resetGraphKitFileState === 'function') resetGraphKitFileState();
    return true;
}

function loadGraphKitProject() {
    try {
        const raw = readProjectScopedStorageItem(GRAPHKIT_STORAGE_KEY);
        if (raw) return sanitizeGraphKitProject(JSON.parse(raw));
    } catch (_) {
        // 损坏的本地图数据不应阻断 GraphKit 页面打开。
    }
    return sanitizeGraphKitProject({});
}

function persistGraphKitProject() {
    graphKitProject = sanitizeGraphKitProject(graphKitProject);
    localStorage.setItem(getGraphKitStorageKey(), JSON.stringify(graphKitProject));
}

function resetGraphKitProject() {
    const previousProject = cloneGraphKitProject(graphKitProject);
    graphKitProject = sanitizeGraphKitProject({});
    graphKitState.selectedNodeId = getGraphKitInitialNodeId(graphKitProject);
    graphKitState.selectedNodeIds = [graphKitState.selectedNodeId].filter(Boolean);
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNoteId = '';
    graphKitState.selectedPlacematId = '';
    graphKitState.selectedNodeTypeId = '';
    if (typeof resetGraphKitFileState === 'function') resetGraphKitFileState();
    else {
        graphKitState.filePath = '';
        graphKitState.fileName = 'Untitled Graph XML';
        graphKitState.fileBusy = false;
    }
    graphKitState.pendingPortEndpoint = '';
    graphKitState.searchTerm = '';
    graphKitState.searchOverlay = { open: false, query: '', scope: 'current', activeIndex: 0 };
    graphKitState.lubanRun = null;
    graphKitState.viewport = { scale: 1, offsetX: 0, offsetY: 0 };
    pushGraphKitHistory(previousProject, graphKitProject, 'Reset graph');
    graphKitState.redoStack = [];
    setGraphKitNotice('info', '已重置 XML Graph 示例数据。');
    persistGraphKitProject();
}

function renderGraphKitPage() {
    syncGraphKitProjectStorageScope();
    $pageBody.classList.add('content-body--graphkit');
    graphKitState.renderSignature = '';
    setHero(
        'GraphKit 节点图编辑器',
        '用 XML 保存跨引擎节点图数据；节点类型、端口、字段和 handler id 由 Luban 生成强类型描述。',
        '工具 · GRAPHKIT',
        'fsm',
        '<button class="btn btn-primary btn-sm" onclick="refreshGraphKit()">刷新</button>'
    );
    clearMetrics();
    clearTabs();
    renderGraphKitWorkbench();
}

function renderGraphKitWorkbench() {
    graphKitProject = sanitizeGraphKitProject(graphKitProject);
    const html = renderGraphKitPanel();
    const signature = getGraphKitSignature();
    renderWorkbenchHtmlStable(graphKitState, html, signature, bindGraphKitEditor);
}

async function refreshGraphKit() {
    renderGraphKitWorkbench();
}

async function refreshGraphKitReactive() {
    renderGraphKitWorkbench();
}

function setGraphKitNotice(level, message) {
    graphKitState.noticeLevel = ['success', 'warning', 'error', 'info'].includes(level) ? level : 'info';
    graphKitState.noticeText = String(message ?? '').trim();
}

function cloneGraphKitProject(project) {
    return sanitizeGraphKitProject(JSON.parse(JSON.stringify(project || {})));
}

function pushGraphKitHistory(beforeProject, afterProject, label) {
    const before = cloneGraphKitProject(beforeProject);
    const after = cloneGraphKitProject(afterProject);
    if (makeStableSignature(before) === makeStableSignature(after)) return;
    graphKitState.undoStack.push({
        label: String(label || 'Edit graph'),
        before,
        after,
    });
    if (graphKitState.undoStack.length > GRAPHKIT_HISTORY_LIMIT) graphKitState.undoStack.shift();
    graphKitState.dirty = true;
}

function getGraphKitInitialNodeId(project) {
    const nodes = Array.isArray(project?.graph?.nodes) ? project.graph.nodes : [];
    return nodes[1]?.id || nodes[0]?.id || '';
}

function getGraphKitSignature() {
    return makeStableSignature({
        selectedNodeId: graphKitState.selectedNodeId,
        selectedNodeIds: graphKitState.selectedNodeIds,
        selectedEdgeId: graphKitState.selectedEdgeId,
        selectedBlackboardName: graphKitState.selectedBlackboardName,
        selectedNoteId: graphKitState.selectedNoteId,
        selectedPlacematId: graphKitState.selectedPlacematId,
        selectedNodeTypeId: graphKitState.selectedNodeTypeId,
        filePath: graphKitState.filePath,
        fileName: graphKitState.fileName,
        fileBusy: graphKitState.fileBusy,
        pendingPortEndpoint: graphKitState.pendingPortEndpoint,
        searchTerm: graphKitState.searchTerm,
        noticeLevel: graphKitState.noticeLevel,
        noticeText: graphKitState.noticeText,
        lubanRun: graphKitState.lubanRun,
        viewport: graphKitState.viewport,
        panels: graphKitState.panels,
        issuesOpen: graphKitState.issuesOpen,
        searchOverlay: graphKitState.searchOverlay,
        clipboardNodeCount: graphKitState.clipboardNodes.length,
        marqueeSelection: graphKitState.marqueeSelection,
        contextMenu: graphKitState.contextMenu,
        wireDrag: graphKitState.wireDrag,
        undoCount: graphKitState.undoStack.length,
        redoCount: graphKitState.redoStack.length,
        dirty: graphKitState.dirty,
        activeGraphId: graphKitProject.activeGraphId,
        project: graphKitProject,
    });
}
