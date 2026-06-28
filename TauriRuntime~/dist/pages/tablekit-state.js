// pages/tablekit-state.js
// TableKit 本地状态：配置、console、折叠状态、engine registry 派生状态和渲染签名。
const TABLEKIT_LUBAN_DEFINE = 'YOKIFRAME_LUBAN_SUPPORT';
const TABLEKIT_CONFIG_STORAGE_KEY = 'yokiframe.tablekit.generator.v1';
const TABLEKIT_CONSOLE_STORAGE_KEY = 'yokiframe.tablekit.console.v1';
const TABLEKIT_COLLAPSE_STORAGE_KEY = 'yokiframe.tablekit.collapsed.v1';
const TABLEKIT_TARGET_OPTIONS = ['client', 'server', 'all'];
const TABLEKIT_CODE_TARGET_OPTIONS = ['cs-bin', 'cs-simple-json', 'cs-newtonsoft-json'];
const TABLEKIT_DATA_TARGET_OPTIONS = ['bin', 'json', 'lua'];
const TABLEKIT_CONSOLE_MAX_ENTRIES = 120;
const TABLEKIT_PREVIEW_MAX_TREE_DEPTH = 5;
const TABLEKIT_COLLAPSIBLE_SECTION_IDS = Object.freeze(['lubanEnvironment', 'outputPaths', 'extraOutputs']);
const TABLEKIT_PATH_PICKERS = Object.freeze({
    lubanWorkDir: { kind: 'folder', buttonLabel: '选择', icon: 'folder' },
    lubanDllPath: { kind: 'file', extension: 'dll', buttonLabel: '选择', icon: 'file' },
    outputDataDir: { kind: 'folder', buttonLabel: '选择', icon: 'folder' },
    outputCodeDir: { kind: 'folder', buttonLabel: '选择', icon: 'folder' },
    editorDataPath: { kind: 'folder', buttonLabel: '选择', icon: 'folder' },
});
const TABLEKIT_EXTRA_OUTPUT_DEFAULT = Object.freeze({
    target: 'client',
    dataTarget: 'json',
    outputDataDir: 'Temp/LubanExtra',
});
const TABLEKIT_DEFAULT_CONFIG = Object.freeze({
    lubanWorkDir: 'Luban/MiniTemplate',
    lubanDllPath: 'Luban/Tools/Luban/Luban.dll',
    target: 'client',
    codeTarget: 'cs-bin',
    dataTarget: 'bin',
    outputDataDir: 'Assets/Resources/Art/Table/',
    outputCodeDir: 'Assets/Scripts/TableKit/',
    runtimePathPattern: 'Art/Table/{0}',
    customEditorDataPath: false,
    editorDataPath: 'Assets/Resources/Art/Table/',
    useAssemblyDefinition: false,
    assemblyName: 'YokiFrame.TableKit',
    generateExternalTypeUtil: false,
    useAsyncLoading: false,
    extraOutputTargets: [],
});
let tableKitConfig = loadTableKitConfig();
let tableKitConsoleEntries = loadTableKitConsoleEntries();
let tableKitCollapsedSections = loadTableKitCollapsedSections();
const tableKitPreviewState = {
    searchTerm: '',
    selectedTableName: '',
    selectedRowIndex: 0,
    previewData: null,
    previewVersion: 0,
    tables: [],
};
const TABLEKIT_BEHAVIOR_SEARCH_STORAGE_KEY = 'yokiframe.tablekit.behavior.search.v1';
const TABLEKIT_BEHAVIOR_TEMPLATE_STORAGE_KEY = 'yokiframe.tablekit.behavior.template.v1';
const TABLEKIT_BEHAVIOR_NODE_STORAGE_KEY = 'yokiframe.tablekit.behavior.node.v1';
const TABLEKIT_BEHAVIOR_DEFAULT_TEMPLATE_ID = 'behavior-selector';
const TABLEKIT_BEHAVIOR_DEFAULT_NODE_ID = 'behavior-root';
const TABLEKIT_BEHAVIOR_LIBRARY = Object.freeze([
    {
        id: 'behavior-selector',
        kind: 'Selector',
        title: 'Selector / 优先级选择',
        summary: '按优先级从左到右尝试子节点，首个成功结果即返回。',
        runtime: '适合“战斗/巡逻/待机”这类优先级分支。',
        xmlSample: '<selector id="combat-root" policy="priority" />',
        ports: 'children ordered by priority',
        tone: 'selector',
    },
    {
        id: 'behavior-sequence',
        kind: 'Sequence',
        title: 'Sequence / 顺序执行',
        summary: '所有子节点按顺序执行，遇到失败立即中止。',
        runtime: '适合线性流程，例如接近 -> 攻击 -> 冷却。',
        xmlSample: '<sequence id="engage-sequence" />',
        ports: 'children executed in order',
        tone: 'sequence',
    },
    {
        id: 'behavior-condition',
        kind: 'Condition',
        title: 'Condition / 条件节点',
        summary: '只读判断黑板、感知或上下文，不产生副作用。',
        runtime: '适合可视范围、血量阈值、目标存在性判断。',
        xmlSample: '<condition id="enemy-visible" key="Vision.EnemyVisible" />',
        ports: 'leaf, no child execution',
        tone: 'condition',
    },
    {
        id: 'behavior-action',
        kind: 'Action',
        title: 'Action / 动作节点',
        summary: '承载具体行为执行，例如移动、攻击、等待、播放动画。',
        runtime: '适合真正调用引擎能力或通用能力接口。',
        xmlSample: '<action id="move-to-target" command="MoveToTarget" />',
        ports: 'leaf executor',
        tone: 'action',
    },
    {
        id: 'behavior-decorator',
        kind: 'Decorator',
        title: 'Decorator / 装饰节点',
        summary: '对单个子节点结果做限频、反转、计时或重试。',
        runtime: '适合冷却、反转条件、超时保护等扩展。',
        xmlSample: '<decorator id="cooldown" mode="timer" seconds="2" />',
        ports: 'single child wrapper',
        tone: 'decorator',
    },
    {
        id: 'behavior-subtree',
        kind: 'Subtree',
        title: 'Subtree / 子图复用',
        summary: '把一段通用图结构注册成可复用的子树模板。',
        runtime: '适合公共巡逻、寻路、对话分支等复用块。',
        xmlSample: '<subtree id="common-patrol" ref="Patrol.Common" />',
        ports: 'imports a registered graph',
        tone: 'subtree',
    },
]);
const TABLEKIT_BEHAVIOR_GRAPH = Object.freeze({
    nodes: [
        {
            id: 'behavior-root',
            templateId: 'behavior-selector',
            type: 'Selector',
            title: '战斗入口',
            summary: '优先级选择根节点，决定当前进入战斗还是巡逻。',
            x: 520,
            y: 40,
            w: 256,
            h: 76,
            ports: 'priority output',
            params: 'Policy=priority',
            runtime: 'high-level root',
            status: 'ready',
        },
        {
            id: 'behavior-engage',
            templateId: 'behavior-sequence',
            type: 'Sequence',
            title: '攻击序列',
            summary: '执行感知、接近和施法的线性流程。',
            x: 160,
            y: 184,
            w: 224,
            h: 78,
            ports: 'children in order',
            params: 'Blackboard=Combat',
            runtime: 'engage branch',
            status: 'ready',
        },
        {
            id: 'behavior-patrol',
            templateId: 'behavior-sequence',
            type: 'Sequence',
            title: '巡逻序列',
            summary: '在安全时执行巡逻与待机动作。',
            x: 860,
            y: 184,
            w: 224,
            h: 78,
            ports: 'children in order',
            params: 'Blackboard=Patrol',
            runtime: 'patrol branch',
            status: 'ready',
        },
        {
            id: 'behavior-visible',
            templateId: 'behavior-condition',
            type: 'Condition',
            title: '敌人可见?',
            summary: '检查视野范围、角度和锁定状态。',
            x: 30,
            y: 340,
            w: 184,
            h: 68,
            ports: 'leaf',
            params: 'VisionRange=12',
            runtime: 'predicate',
            status: 'ready',
        },
        {
            id: 'behavior-move',
            templateId: 'behavior-action',
            type: 'Action',
            title: '接近目标',
            summary: '移动到有效攻击距离。',
            x: 230,
            y: 340,
            w: 184,
            h: 68,
            ports: 'leaf',
            params: 'Action=MoveToTarget',
            runtime: 'movement',
            status: 'running',
        },
        {
            id: 'behavior-cast',
            templateId: 'behavior-action',
            type: 'Action',
            title: '施放技能',
            summary: '触发攻击动作与动画。',
            x: 430,
            y: 340,
            w: 184,
            h: 68,
            ports: 'leaf',
            params: 'Action=CastSkill',
            runtime: 'combat',
            status: 'ready',
        },
        {
            id: 'behavior-safe',
            templateId: 'behavior-condition',
            type: 'Condition',
            title: '环境安全?',
            summary: '判断是否进入巡逻状态。',
            x: 730,
            y: 340,
            w: 184,
            h: 68,
            ports: 'leaf',
            params: 'ThreatLevel=Low',
            runtime: 'predicate',
            status: 'ready',
        },
        {
            id: 'behavior-route',
            templateId: 'behavior-action',
            type: 'Action',
            title: '沿路线巡逻',
            summary: '沿路标与路径点执行移动。',
            x: 930,
            y: 340,
            w: 184,
            h: 68,
            ports: 'leaf',
            params: 'Action=FollowRoute',
            runtime: 'navigation',
            status: 'ready',
        },
        {
            id: 'behavior-idle',
            templateId: 'behavior-action',
            type: 'Action',
            title: '待机观察',
            summary: '保持低频扫描与轻量刷新。',
            x: 1130,
            y: 340,
            w: 184,
            h: 68,
            ports: 'leaf',
            params: 'Action=IdleLookAround',
            runtime: 'idle',
            status: 'ready',
        },
    ],
    edges: [
        { from: 'behavior-root', to: 'behavior-engage', label: 'priority 1' },
        { from: 'behavior-root', to: 'behavior-patrol', label: 'fallback' },
        { from: 'behavior-engage', to: 'behavior-visible', label: 'check' },
        { from: 'behavior-engage', to: 'behavior-move', label: 'then' },
        { from: 'behavior-engage', to: 'behavior-cast', label: 'then' },
        { from: 'behavior-patrol', to: 'behavior-safe', label: 'check' },
        { from: 'behavior-patrol', to: 'behavior-route', label: 'then' },
        { from: 'behavior-patrol', to: 'behavior-idle', label: 'fallback' },
    ],
});
const tableKitBehaviorState = {
    searchTerm: loadTableKitBehaviorSearchTerm(),
    selectedTemplateId: loadTableKitBehaviorTemplateSelection(),
    selectedNodeId: loadTableKitBehaviorNodeSelection(),
};
const tableKitEditorState = { renderSignature: '' };

if (!tableKitConsoleEntries.length) {
    tableKitConsoleEntries = [
        makeTableKitConsoleEntry('info', '已从 localStorage 恢复 TableKit 配置。'),
        makeTableKitConsoleEntry('info', 'Tauri Luban 执行器已准备调用本地 dotnet Luban.dll。'),
    ];
    persistTableKitConsoleEntries();
}
function loadTableKitConfig() {
    try {
        const raw = localStorage.getItem(TABLEKIT_CONFIG_STORAGE_KEY);
        if (raw) return sanitizeTableKitConfig(JSON.parse(raw));
    } catch (_) {
        // 读取失败时回退默认配置，避免损坏的本地配置阻断页面。
    }
    return sanitizeTableKitConfig({});
}

function persistTableKitConfig() {
    tableKitConfig = sanitizeTableKitConfig(tableKitConfig);
    localStorage.setItem(TABLEKIT_CONFIG_STORAGE_KEY, JSON.stringify(tableKitConfig));
}

function sanitizeTableKitConfig(raw) {
    const config = { ...TABLEKIT_DEFAULT_CONFIG };
    if (raw && typeof raw === 'object') {
        Object.keys(TABLEKIT_DEFAULT_CONFIG).forEach(key => {
            if (Object.prototype.hasOwnProperty.call(raw, key)) config[key] = raw[key];
        });
    }

    config.target = normalizeTableKitOption(config.target, TABLEKIT_TARGET_OPTIONS, TABLEKIT_DEFAULT_CONFIG.target);
    config.codeTarget = normalizeTableKitOption(config.codeTarget, TABLEKIT_CODE_TARGET_OPTIONS, TABLEKIT_DEFAULT_CONFIG.codeTarget);
    config.dataTarget = normalizeTableKitOption(config.dataTarget, TABLEKIT_DATA_TARGET_OPTIONS, TABLEKIT_DEFAULT_CONFIG.dataTarget);
    config.lubanWorkDir = normalizeTableKitString(config.lubanWorkDir, TABLEKIT_DEFAULT_CONFIG.lubanWorkDir);
    config.lubanDllPath = normalizeTableKitString(config.lubanDllPath, TABLEKIT_DEFAULT_CONFIG.lubanDllPath);
    config.outputDataDir = normalizeTableKitString(config.outputDataDir, TABLEKIT_DEFAULT_CONFIG.outputDataDir);
    config.outputCodeDir = normalizeTableKitString(config.outputCodeDir, TABLEKIT_DEFAULT_CONFIG.outputCodeDir);
    config.runtimePathPattern = normalizeTableKitString(config.runtimePathPattern, TABLEKIT_DEFAULT_CONFIG.runtimePathPattern);
    config.customEditorDataPath = normalizeTableKitBool(config.customEditorDataPath);
    config.editorDataPath = config.customEditorDataPath
        ? normalizeTableKitString(config.editorDataPath, config.outputDataDir)
        : config.outputDataDir;
    config.useAssemblyDefinition = normalizeTableKitBool(config.useAssemblyDefinition);
    config.assemblyName = normalizeTableKitString(config.assemblyName, TABLEKIT_DEFAULT_CONFIG.assemblyName);
    config.generateExternalTypeUtil = normalizeTableKitBool(config.generateExternalTypeUtil);
    config.useAsyncLoading = normalizeTableKitBool(config.useAsyncLoading);
    config.extraOutputTargets = sanitizeTableKitExtraOutputTargets(config.extraOutputTargets);
    return config;
}

function normalizeTableKitString(value, fallback) {
    const text = String(value ?? '').trim();
    return text || fallback;
}

function normalizeTableKitOption(value, options, fallback) {
    return options.includes(value) ? value : fallback;
}

function normalizeTableKitBool(value) {
    return value === true || value === 'true' || value === 1 || value === '1';
}

function sanitizeTableKitExtraOutputTargets(rawTargets) {
    if (!Array.isArray(rawTargets)) return [];
    const targets = [];
    rawTargets.forEach(rawTarget => {
        if (!rawTarget || typeof rawTarget !== 'object') return;
        const outputDataDir = normalizeTableKitString(rawTarget.outputDataDir, TABLEKIT_EXTRA_OUTPUT_DEFAULT.outputDataDir);
        targets.push({
            target: normalizeTableKitOption(rawTarget.target, TABLEKIT_TARGET_OPTIONS, TABLEKIT_EXTRA_OUTPUT_DEFAULT.target),
            dataTarget: normalizeTableKitOption(rawTarget.dataTarget, TABLEKIT_DATA_TARGET_OPTIONS, TABLEKIT_EXTRA_OUTPUT_DEFAULT.dataTarget),
            outputDataDir,
        });
    });
    return targets.slice(0, 8);
}

function loadTableKitConsoleEntries() {
    try {
        const raw = localStorage.getItem(TABLEKIT_CONSOLE_STORAGE_KEY);
        const parsed = raw ? JSON.parse(raw) : [];
        if (!Array.isArray(parsed)) return [];
        return parsed.map(entry => ({
            id: normalizeTableKitString(entry?.id, makeTableKitConsoleId()),
            time: normalizeTableKitString(entry?.time, new Date().toLocaleTimeString()),
            level: normalizeTableKitConsoleLevel(entry?.level),
            message: normalizeTableKitString(entry?.message, ''),
        })).filter(entry => entry.message).slice(-TABLEKIT_CONSOLE_MAX_ENTRIES);
    } catch (_) {
        return [];
    }
}

function persistTableKitConsoleEntries() {
    localStorage.setItem(TABLEKIT_CONSOLE_STORAGE_KEY, JSON.stringify(tableKitConsoleEntries.slice(-TABLEKIT_CONSOLE_MAX_ENTRIES)));
}

function loadTableKitBehaviorSearchTerm() {
    try {
        return String(localStorage.getItem(TABLEKIT_BEHAVIOR_SEARCH_STORAGE_KEY) || '').trim();
    } catch (_) {
        return '';
    }
}

function persistTableKitBehaviorSearchTerm() {
    localStorage.setItem(TABLEKIT_BEHAVIOR_SEARCH_STORAGE_KEY, String(tableKitBehaviorState.searchTerm || '').trim());
}

function loadTableKitBehaviorTemplateSelection() {
    try {
        const value = String(localStorage.getItem(TABLEKIT_BEHAVIOR_TEMPLATE_STORAGE_KEY) || '').trim();
        return value || TABLEKIT_BEHAVIOR_DEFAULT_TEMPLATE_ID;
    } catch (_) {
        return TABLEKIT_BEHAVIOR_DEFAULT_TEMPLATE_ID;
    }
}

function persistTableKitBehaviorTemplateSelection() {
    localStorage.setItem(TABLEKIT_BEHAVIOR_TEMPLATE_STORAGE_KEY, String(tableKitBehaviorState.selectedTemplateId || TABLEKIT_BEHAVIOR_DEFAULT_TEMPLATE_ID));
}

function loadTableKitBehaviorNodeSelection() {
    try {
        const value = String(localStorage.getItem(TABLEKIT_BEHAVIOR_NODE_STORAGE_KEY) || '').trim();
        return value || TABLEKIT_BEHAVIOR_DEFAULT_NODE_ID;
    } catch (_) {
        return TABLEKIT_BEHAVIOR_DEFAULT_NODE_ID;
    }
}

function persistTableKitBehaviorNodeSelection() {
    localStorage.setItem(TABLEKIT_BEHAVIOR_NODE_STORAGE_KEY, String(tableKitBehaviorState.selectedNodeId || TABLEKIT_BEHAVIOR_DEFAULT_NODE_ID));
}

function getTableKitBehaviorTemplates() {
    return TABLEKIT_BEHAVIOR_LIBRARY.slice();
}

function getTableKitBehaviorTemplateById(templateId) {
    const id = String(templateId || '');
    if (!id) return TABLEKIT_BEHAVIOR_LIBRARY[0] || null;
    for (let i = 0; i < TABLEKIT_BEHAVIOR_LIBRARY.length; i++) {
        const item = TABLEKIT_BEHAVIOR_LIBRARY[i];
        if (String(item.id) === id) return item;
    }
    return TABLEKIT_BEHAVIOR_LIBRARY[0] || null;
}

function getTableKitBehaviorNodes() {
    return TABLEKIT_BEHAVIOR_GRAPH.nodes.slice();
}

function getTableKitBehaviorNodeById(nodeId) {
    const id = String(nodeId || '');
    if (!id) return TABLEKIT_BEHAVIOR_GRAPH.nodes[0] || null;
    for (let i = 0; i < TABLEKIT_BEHAVIOR_GRAPH.nodes.length; i++) {
        const node = TABLEKIT_BEHAVIOR_GRAPH.nodes[i];
        if (String(node.id) === id) return node;
    }
    return TABLEKIT_BEHAVIOR_GRAPH.nodes[0] || null;
}

function getTableKitBehaviorEdges() {
    return TABLEKIT_BEHAVIOR_GRAPH.edges.slice();
}

function getTableKitFilteredBehaviorTemplates() {
    const query = String(tableKitBehaviorState.searchTerm || '').trim().toLowerCase();
    if (!query) return getTableKitBehaviorTemplates();
    return TABLEKIT_BEHAVIOR_LIBRARY.filter(item => {
        return [item.id, item.kind, item.title, item.summary, item.runtime, item.xmlSample, item.ports]
            .some(value => String(value || '').toLowerCase().includes(query));
    });
}

function getTableKitFilteredBehaviorNodes() {
    const query = String(tableKitBehaviorState.searchTerm || '').trim().toLowerCase();
    if (!query) return getTableKitBehaviorNodes();
    return TABLEKIT_BEHAVIOR_GRAPH.nodes.filter(item => {
        const template = getTableKitBehaviorTemplateById(item.templateId);
        return [item.id, item.type, item.title, item.summary, item.params, item.runtime, item.status, template?.title, template?.summary]
            .some(value => String(value || '').toLowerCase().includes(query));
    });
}

function getTableKitBehaviorSelectedTemplate() {
    return getTableKitBehaviorTemplateById(tableKitBehaviorState.selectedTemplateId);
}

function getTableKitBehaviorSelectedNode() {
    return getTableKitBehaviorNodeById(tableKitBehaviorState.selectedNodeId);
}

function getTableKitBehaviorSignature() {
    return [
        signaturePart(tableKitBehaviorState.searchTerm),
        signaturePart(tableKitBehaviorState.selectedTemplateId),
        signaturePart(tableKitBehaviorState.selectedNodeId),
        TABLEKIT_BEHAVIOR_LIBRARY.length,
        TABLEKIT_BEHAVIOR_GRAPH.nodes.length,
        TABLEKIT_BEHAVIOR_GRAPH.edges.length,
    ].join('|');
}

function loadTableKitCollapsedSections() {
    try {
        const raw = localStorage.getItem(TABLEKIT_COLLAPSE_STORAGE_KEY);
        const parsed = raw ? JSON.parse(raw) : {};
        if (!parsed || typeof parsed !== 'object') return {};

        const collapsed = {};
        TABLEKIT_COLLAPSIBLE_SECTION_IDS.forEach(id => {
            collapsed[id] = parsed[id] === true;
        });
        return collapsed;
    } catch (_) {
        return {};
    }
}

function persistTableKitCollapsedSections() {
    localStorage.setItem(TABLEKIT_COLLAPSE_STORAGE_KEY, JSON.stringify(tableKitCollapsedSections));
}

function isTableKitSectionCollapsed(id) {
    return tableKitCollapsedSections[id] === true;
}

function toggleTableKitCollapsedSection(id) {
    if (!TABLEKIT_COLLAPSIBLE_SECTION_IDS.includes(id)) return;
    tableKitCollapsedSections = {
        ...tableKitCollapsedSections,
        [id]: !isTableKitSectionCollapsed(id),
    };
    persistTableKitCollapsedSections();
    renderTableKitRegistryStatus();
}

function makeTableKitConsoleId() {
    return `${Date.now().toString(36)}-${Math.random().toString(36).slice(2, 8)}`;
}

function makeTableKitConsoleEntry(level, message) {
    return {
        id: makeTableKitConsoleId(),
        time: new Date().toLocaleTimeString(),
        level: normalizeTableKitConsoleLevel(level),
        message: String(message ?? '').trim(),
    };
}

function normalizeTableKitConsoleLevel(level) {
    return ['info', 'success', 'warning', 'error'].includes(level) ? level : 'info';
}

function appendTableKitConsoleEntry(level, message) {
    const entry = makeTableKitConsoleEntry(level, message);
    if (!entry.message) return;
    tableKitConsoleEntries.push(entry);
    if (tableKitConsoleEntries.length > TABLEKIT_CONSOLE_MAX_ENTRIES) {
        tableKitConsoleEntries = tableKitConsoleEntries.slice(-TABLEKIT_CONSOLE_MAX_ENTRIES);
    }
    persistTableKitConsoleEntries();
}

function appendTableKitConsoleLines(level, text) {
    const lines = String(text ?? '').split(/\r?\n/).map(line => line.trim()).filter(Boolean);
    if (!lines.length) return;

    for (const line of lines) {
        tableKitConsoleEntries.push(makeTableKitConsoleEntry(level, line));
    }
    if (tableKitConsoleEntries.length > TABLEKIT_CONSOLE_MAX_ENTRIES) {
        tableKitConsoleEntries = tableKitConsoleEntries.slice(-TABLEKIT_CONSOLE_MAX_ENTRIES);
    }
    persistTableKitConsoleEntries();
}

function getTableKitLubanStatus(status = latestStatusRaw) {
    const engines = Array.isArray(status?.engines) ? status.engines : [];
    let selectedEngine = null;
    let dependency = null;

    for (const engine of engines) {
        const candidate = engine?.optionalDependencies?.luban;
        if (!candidate) continue;

        if (!selectedEngine || (engine.connected !== false && selectedEngine.connected === false)) {
            selectedEngine = engine;
            dependency = candidate;
        }

        if (engine.connected !== false) break;
    }

    const available = dependency?.available === true || dependency?.available === 'true';
    return {
        hasRegistry: !!selectedEngine,
        available,
        define: dependency?.define || TABLEKIT_LUBAN_DEFINE,
        packageName: dependency?.packageName || dependency?.package || 'com.code-philosophy.luban',
        asmdefName: dependency?.asmdefName || dependency?.asmdef || 'Luban.Runtime',
        typeName: dependency?.typeName || dependency?.type || 'Luban.ByteBuf',
        engine: selectedEngine?.engine || '--',
        engineId: selectedEngine?.engineId || '--',
        version: selectedEngine?.version || '--',
        projectPath: selectedEngine?.projectPath || '--',
        connected: selectedEngine ? selectedEngine.connected !== false : false,
    };
}

function getTableKitConsoleSignature() {
    return makeRowEdgeSignature(tableKitConsoleEntries, entry => entry.id);
}

function getTableKitPreviewSignature() {
    return [
        signaturePart(tableKitPreviewState.previewVersion),
        signaturePart(tableKitPreviewState.searchTerm),
        signaturePart(tableKitPreviewState.selectedTableName),
        signaturePart(tableKitPreviewState.selectedRowIndex),
        Array.isArray(tableKitPreviewState.tables) ? tableKitPreviewState.tables.length : 0,
    ].join('|');
}
