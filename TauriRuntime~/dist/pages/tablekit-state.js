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
