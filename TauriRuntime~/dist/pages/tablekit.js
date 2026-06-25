// pages/tablekit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：TableKit
// ═══════════════════════════════════════════════════════════════════
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

function renderTableKitPage() {
    $pageBody.classList.add('content-body--tablekit');
    setHero(
        'TableKit 配置表编辑器',
        '在 Tauri 中配置 Luban 生成参数；运行时代码由工具生成到项目 Scripts。',
        '工具 · TABLEKIT',
        'table',
        '<button class="btn btn-primary btn-sm" onclick="refreshTableKit()">刷新</button>'
    );
    clearTabs();
    renderTableKitRegistryStatus();
    if (invoke) {
        pollStatus({ force: true });
    }
}

async function refreshTableKit() {
    if (invoke) {
        await pollStatus({ force: true });
    }
    renderTableKitRegistryStatus();
}

async function refreshTableKitReactive(event) {
    renderTableKitRegistryStatus();
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

function renderTableKitRegistryStatus() {
    clearMetrics();
    tableKitConfig = sanitizeTableKitConfig(tableKitConfig);
    const status = getTableKitLubanStatus();
    const html = renderTableKitGeneratorStatus(status, tableKitConfig);
    const signature = makeStableSignature({
        status,
        config: tableKitConfig,
        collapsed: tableKitCollapsedSections,
        console: tableKitConsoleEntries,
        preview: tableKitPreviewState,
    });
    renderWorkbenchHtmlStable(tableKitEditorState, html, signature, bindTableKitEditor);
}

function renderTableKitGeneratorStatus(status, config) {
    const stateClass = status.available ? 'kit-state-pill--ok' : 'kit-state-pill--muted';
    const stateText = status.available ? 'Luban ON' : 'Luban OFF';
    const registryText = status.hasRegistry
        ? `${status.engine} · ${status.engineId}`
        : '等待 engine registry';
    const effectiveEngine = getTableKitEffectiveEngine(status);

    return `<div class="kit-workbench kit-workbench--table tablekit-workbench">
        <section class="kit-toolbar tablekit-command-center">
            <div class="tablekit-command-center__main">
                <div class="kit-toolbar__title">${renderKitTitle('table', 'TableKit 配置表生成')}</div>
                <div class="kit-toolbar__meta">${escapeHtml(registryText)} · ${escapeHtml(status.connected ? '在线' : '未连接或离线快照')} · ${escapeHtml(effectiveEngine)}</div>
            </div>
            <div class="kit-toolbar__actions tablekit-command-center__actions">
                <div class="tablekit-command-center__states">
                    <span class="kit-state-pill ${stateClass}">${escapeHtml(stateText)}</span>
                    <span class="kit-state-pill">${escapeHtml(status.define)}</span>
                    <span class="kit-state-pill kit-state-pill--ok">${escapeHtml('Tauri 执行')}</span>
                </div>
                <div class="tablekit-command-center__buttons">
                    <button class="btn btn-secondary btn-sm" type="button" data-tablekit-action="reset">还原默认</button>
                    <button class="btn btn-secondary btn-sm" type="button" data-tablekit-action="open-config">打开配置表</button>
                    <button class="btn btn-primary btn-sm" type="button" data-tablekit-action="generate">生成配置表</button>
                </div>
            </div>
        </section>
        <div class="tablekit-sections" data-kit-scroll-key="tablekit-sections">
            <section class="kit-panel tablekit-config-log-shell">
                <div class="tablekit-config-log-grid">
                    ${renderTableKitEnvironmentPanel(status, config, effectiveEngine)}
                    ${renderTableKitConsole(status, config)}
                </div>
            </section>
            ${renderTableKitPreviewPanel(status, config)}
        </div>
    </div>`;
}

function renderTableKitEnvironmentPanel(status, config, effectiveEngine) {
    const isGodot = String(effectiveEngine).toLowerCase() === 'godot';
    const assemblyNote = isGodot
        ? 'Godot 使用 .csproj/Directory.Build.props 管理编译宏；没有 Unity asmdef 语义。'
        : 'Unity 启用后会在代码输出目录生成 asmdef，并引用 Luban.Runtime。';
    return `<section class="kit-panel tablekit-section tablekit-section--environment">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', '环境与路径配置')}</div>
                <div class="kit-panel__desc">配置 Luban 环境位置、Luban 数据目录、生成代码和生成数据落点。</div>
            </div>
            <span class="kit-state-pill ${status.available ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${escapeHtml(formatTableKitBool(status.available))}</span>
        </div>
        <div class="tablekit-form" data-kit-scroll-key="tablekit-form">
            ${renderTableKitLubanNotice(status)}
            ${renderTableKitLubanInstallGuide(status, config)}
            ${renderTableKitCollapsibleGroup('lubanEnvironment', 'Luban 环境', '工作目录与 Luban.dll 路径', `
                ${renderTableKitTextField('lubanWorkDir', '工作目录', config.lubanWorkDir, '包含 Datas、Defines、luban.conf 的目录')}
                ${renderTableKitTextField('lubanDllPath', 'Luban.dll', config.lubanDllPath, 'Luban 代码生成工具 DLL 路径')}
            `)}
            ${renderTableKitCollapsibleGroup('outputPaths', '数据路径', '生成数据与 C# 代码落点', `
                <div class="tablekit-form-grid tablekit-form-grid--targets">
                    ${renderTableKitSelectField('target', 'Target', config.target, TABLEKIT_TARGET_OPTIONS, 'Luban target')}
                    ${renderTableKitSelectField('codeTarget', 'Code', config.codeTarget, TABLEKIT_CODE_TARGET_OPTIONS, 'Luban code target')}
                    ${renderTableKitSelectField('dataTarget', '数据格式', config.dataTarget, TABLEKIT_DATA_TARGET_OPTIONS, 'Luban data target')}
                </div>
                ${renderTableKitTextField('outputDataDir', '数据输出', config.outputDataDir, '生成的配置表数据文件存放路径')}
                ${renderTableKitTextField('outputCodeDir', '代码输出', config.outputCodeDir, '生成的 C# 配置表代码存放路径')}
            `)}
            ${renderTableKitCollapsibleGroup('extraOutputs', '额外输出目标', '多份数据格式独立导出', renderTableKitExtraOutputs(config), `
                <button class="btn btn-secondary btn-sm" type="button" data-tablekit-action="add-extra-output">添加输出目标</button>
            `)}
            <div class="tablekit-subsection tablekit-subsection--plain">
                <div class="tablekit-subsection__title">TableKit 路径</div>
                <div class="tablekit-toggle-row">
                    ${renderKitToggle('自定义编辑器数据路径', !!config.customEditorDataPath, 'data-tablekit-toggle="customEditorDataPath"', '关闭时编辑器数据路径自动跟随数据输出路径')}
                </div>
                ${renderTableKitTextField('editorDataPath', '编辑器数据', config.editorDataPath, 'TableKit.TablesEditor 编辑器访问用的数据路径', !config.customEditorDataPath)}
                ${renderTableKitTextField('runtimePathPattern', '运行时模式', config.runtimePathPattern, '{0} 为文件名占位符；例如 Art/Table/{0}')}
            </div>
            <div class="tablekit-subsection tablekit-subsection--plain">
                <div class="tablekit-subsection__title">生成选项</div>
                <div class="tablekit-toggle-row tablekit-toggle-row--wrap">
                    ${renderKitToggle('使用独立程序集', !!config.useAssemblyDefinition, 'data-tablekit-toggle="useAssemblyDefinition"', assemblyNote)}
                    ${renderKitToggle('生成 ExternalTypeUtil', !!config.generateExternalTypeUtil, 'data-tablekit-toggle="generateExternalTypeUtil"', 'Luban 在 vector 转 Unity/Godot 类型时可追加转换工具')}
                    ${renderKitToggle('异步加载模式', !!config.useAsyncLoading, 'data-tablekit-toggle="useAsyncLoading"', '生成 InitAsync 入口；需要异步加载时启用')}
                </div>
                ${renderTableKitTextField('assemblyName', '程序集名称', config.assemblyName, 'Unity asmdef 名称；Godot 仅作为项目侧组织信息', !config.useAssemblyDefinition)}
            </div>
            <div class="kit-detail-summary kit-detail-summary--tablekit-status">
                <div><span>Package</span><strong>${escapeHtml(status.packageName)}</strong></div>
                <div><span>Asmdef</span><strong>${escapeHtml(status.asmdefName)}</strong></div>
                <div><span>Type</span><strong>${escapeHtml(status.typeName)}</strong></div>
            </div>
        </div>
    </section>`;
}

function renderTableKitCollapsibleGroup(id, title, summary, bodyHtml, actionsHtml = '') {
    const collapsed = isTableKitSectionCollapsed(id);
    return `<section class="tablekit-fold-card" data-tablekit-collapsible="${escapeHtml(id)}" data-collapsed="${escapeHtml(String(collapsed))}">
        <div class="tablekit-fold-card__head">
            <button class="tablekit-fold-card__toggle" type="button" data-tablekit-collapse="${escapeHtml(id)}" aria-expanded="${escapeHtml(String(!collapsed))}">
                <span class="tablekit-fold-card__chevron" aria-hidden="true"></span>
                <span class="tablekit-fold-card__text">
                    <strong>${escapeHtml(title)}</strong>
                    <em>${escapeHtml(summary)}</em>
                </span>
            </button>
            ${actionsHtml ? `<div class="tablekit-fold-card__actions">${actionsHtml}</div>` : ''}
        </div>
        <div class="tablekit-fold-card__body">${bodyHtml}</div>
    </section>`;
}

function renderTableKitLubanNotice(status) {
    const level = status.available ? 'success' : 'warning';
    const text = status.available
        ? '宿主已报告 Luban 环境，TableKit 编辑器代码会在对应宏定义下参与编译。'
        : 'Luban 工具不应放置在 Assets 内，推荐放在与 Assets 同级目录；安装后宿主会自动维护 YOKIFRAME_LUBAN_SUPPORT。';
    return `<div class="tablekit-callout tablekit-callout--${level}">${escapeHtml(text)}</div>`;
}

function renderTableKitLubanInstallGuide(status, config) {
    if (status.available) return '';

    const recommendedDllPath = 'Luban/Tools/Luban/Luban.dll';
    const projectRoot = getTableKitProjectRoot(status) || '<GodotProjectRoot>';
    const workDir = normalizeTableKitPath(config.lubanWorkDir);
    const dllPath = normalizeTableKitPath(config.lubanDllPath);
    const resolvedWorkDir = resolveTableKitProjectPath(projectRoot, workDir);
    const resolvedDllPath = resolveTableKitProjectPath(projectRoot, dllPath);

    return `<div class="tablekit-install-guide">
        <div class="tablekit-install-guide__title">Luban 运行时环境未安装</div>
        <ol class="tablekit-install-guide__steps">
            <li>下载 Luban release，并把工具目录放到项目根目录附近，推荐相对路径为 <code>${escapeHtml(recommendedDllPath)}</code>；当前配置会解析到 <code>${escapeHtml(resolvedDllPath)}</code>，不要放进 Assets。</li>
            <li>创建或复制 Luban/MiniTemplate 到 <code>${escapeHtml(resolvedWorkDir)}</code>，其中需要包含 Datas、Defines 和 luban.conf。</li>
            <li>在上方字段确认 Luban 工作目录为 <code>${escapeHtml(workDir)}</code>，Luban.dll 为 <code>${escapeHtml(dllPath)}</code>。</li>
            <li>确认 Godot 工程能通过 Directory.Build.props 识别 Luban.Runtime，让 ${escapeHtml(status.define)} 宏保持启用。</li>
            <li>安装完成后重启 Godot 或重新启用 YokiFrame 插件，再回到 TableKit 刷新状态。</li>
        </ol>
    </div>`;
}

function renderTableKitExtraOutputs(config) {
    const rows = config.extraOutputTargets.length
        ? config.extraOutputTargets.map((target, index) => renderTableKitExtraOutputRow(target, index)).join('')
        : '<div class="tablekit-empty-line">未配置额外输出目标；需要多份数据格式时可在这里追加。</div>';
    return `<div class="tablekit-extra-output">
        <div class="tablekit-extra-output__rows">${rows}</div>
        <div class="tablekit-warning-text">提示：不同导出目标会分别执行 Luban，确保字段记得导出。</div>
    </div>`;
}

function renderTableKitExtraOutputRow(target, index) {
    return `<div class="tablekit-extra-output-row">
        ${renderTableKitExtraSelect(index, 'target', target.target, TABLEKIT_TARGET_OPTIONS, 'Target')}
        ${renderTableKitExtraSelect(index, 'dataTarget', target.dataTarget, TABLEKIT_DATA_TARGET_OPTIONS, 'Data')}
        ${renderTableKitPathControl(target.outputDataDir, {
            picker: { kind: 'folder', buttonLabel: '选择', icon: 'folder' },
            ariaLabel: '额外输出目录',
            inputAttrs: `data-tablekit-extra-index="${index}" data-tablekit-extra-field="outputDataDir"`,
            buttonAttrs: `data-tablekit-extra-pick-folder="${index}"`,
        })}
        <button class="btn btn-secondary btn-sm" type="button" data-tablekit-remove-extra="${index}">移除</button>
    </div>`;
}

function renderTableKitExtraSelect(index, field, value, options, label) {
    const items = options.map(option => {
        const selected = option === value ? ' selected' : '';
        return `<option value="${escapeHtml(option)}"${selected}>${escapeHtml(formatTableKitOption(option))}</option>`;
    }).join('');
    return `<label class="tablekit-extra-select">
        <span>${escapeHtml(label)}</span>
        <select class="cmd-select" data-tablekit-extra-index="${index}" data-tablekit-extra-field="${escapeHtml(field)}">${items}</select>
    </label>`;
}

function renderTableKitConsole(status, config) {
    const rows = tableKitConsoleEntries.length
        ? tableKitConsoleEntries.map(entry => `<div class="tablekit-console-line tablekit-console-line--${escapeHtml(entry.level)}"><span>[${escapeHtml(entry.time)}]</span><strong>${escapeHtml(entry.level.toUpperCase())}</strong><em>${escapeHtml(entry.message)}</em></div>`).join('')
        : '<div class="tablekit-console-empty">暂无 Luban 输出。生成或验证配置表后会显示日志。</div>';
    return `<section class="kit-panel tablekit-section tablekit-section--console">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('log', '控制台')}</div>
                <div class="kit-panel__desc">显示 Luban 生成配置表后的输出日志，可复制粘贴给协作者或 AI。</div>
            </div>
            <div class="tablekit-panel-actions">
                <button class="btn btn-secondary btn-sm" type="button" data-tablekit-action="copy-console">复制</button>
                <button class="btn btn-secondary btn-sm" type="button" data-tablekit-action="clear-console">清空</button>
            </div>
        </div>
        <div class="tablekit-console-status">
            <span>${escapeHtml('Luban 命令')}</span>
            <code>${escapeHtml(renderTableKitCommandPreview(config, status).split('\n')[1] || '')}</code>
        </div>
        <div class="tablekit-console" data-kit-scroll-key="tablekit-console">${rows}</div>
    </section>`;
}

function renderTableKitSelectField(field, label, value, options, hint) {
    const items = options.map(option => {
        const selected = option === value ? ' selected' : '';
        return `<option value="${escapeHtml(option)}"${selected}>${escapeHtml(formatTableKitOption(option))}</option>`;
    }).join('');
    return `<label class="tablekit-field">
        <span>${escapeHtml(label)}${hint ? `<em>${escapeHtml(hint)}</em>` : ''}</span>
        <select class="cmd-select" data-tablekit-field="${escapeHtml(field)}">${items}</select>
    </label>`;
}

function renderTableKitTextField(field, label, value, hint, disabled = false) {
    const picker = TABLEKIT_PATH_PICKERS[field];
    if (picker) {
        return `<div class="tablekit-field tablekit-field--path${disabled ? ' tablekit-field--disabled' : ''}">
            <span>${escapeHtml(label)}${hint ? `<em>${escapeHtml(hint)}</em>` : ''}</span>
            ${renderTableKitPathControl(value, {
                picker,
                disabled,
                ariaLabel: label,
                inputAttrs: `data-tablekit-field="${escapeHtml(field)}"`,
                buttonAttrs: `data-tablekit-pick-path="${escapeHtml(field)}"`,
            })}
        </div>`;
    }

    return `<label class="tablekit-field${disabled ? ' tablekit-field--disabled' : ''}">
        <span>${escapeHtml(label)}${hint ? `<em>${escapeHtml(hint)}</em>` : ''}</span>
        <input class="cmd-input" type="text" data-tablekit-field="${escapeHtml(field)}" value="${escapeHtml(value)}"${disabled ? ' disabled' : ''}>
    </label>`;
}

function renderTableKitPathControl(value, options) {
    const picker = options?.picker || TABLEKIT_PATH_PICKERS.outputDataDir;
    const disabled = !!options?.disabled;
    const inputAttrs = options?.inputAttrs ? ` ${options.inputAttrs}` : '';
    const buttonAttrs = options?.buttonAttrs ? ` ${options.buttonAttrs}` : '';
    const ariaLabel = options?.ariaLabel ? String(options.ariaLabel) : '路径';
    const buttonTitle = picker.kind === 'file' ? `选择${ariaLabel}` : `选择${ariaLabel}`;
    const buttonIcon = svgIcon(picker.icon || 'folder', 'shell-icon');
    return `<div class="cmd-path-control tablekit-path-control">
        <input class="cmd-input cmd-path-display" type="text" value="${escapeHtml(value)}"${inputAttrs} aria-label="${escapeHtml(ariaLabel)}" readonly aria-readonly="true"${disabled ? ' disabled' : ''}>
        <button class="btn btn-secondary btn-sm cmd-path-button" type="button"${buttonAttrs}${disabled ? ' disabled' : ''} title="${escapeHtml(buttonTitle)}">${buttonIcon}<span>${escapeHtml(picker.buttonLabel || '选择')}</span></button>
    </div>`;
}

function getTableKitPathFieldLabel(field) {
    switch (field) {
        case 'lubanWorkDir':
            return 'Luban 工作目录';
        case 'lubanDllPath':
            return 'Luban.dll';
        case 'outputDataDir':
            return '数据输出';
        case 'outputCodeDir':
            return '代码输出';
        case 'editorDataPath':
            return '编辑器数据';
        default:
            return field;
    }
}

function getTableKitFilePickerInitialPath(path) {
    const normalized = normalizeTableKitPath(path).replace(/\/+$/, '');
    if (!normalized) return normalized;
    if (!/\.[^/]+$/.test(normalized)) return normalized;
    const lastSlash = normalized.lastIndexOf('/');
    if (lastSlash <= 2) return normalized;
    return normalized.slice(0, lastSlash);
}

function normalizeTableKitPickedPath(value, projectRoot) {
    const path = normalizeTableKitPath(value).trim();
    if (!path) return '';

    const root = normalizeTableKitPath(projectRoot || '').replace(/\/+$/, '');
    if (!root || !isTableKitAbsolutePath(path) || !isTableKitAbsolutePath(root)) {
        return path;
    }

    const pathKey = path.toLowerCase();
    const rootKey = root.toLowerCase();
    if (pathKey === rootKey) return '.';
    const prefix = root.endsWith('/') ? root : `${root}/`;
    if (pathKey.startsWith(prefix.toLowerCase())) {
        return path.slice(prefix.length);
    }
    return path;
}

async function handleTableKitOpenConfig() {
    if (!invoke) {
        appendTableKitConsoleEntry('warning', '当前不在 Tauri 环境中，无法打开本地配置表目录。');
        renderTableKitRegistryStatus();
        return;
    }

    const status = getTableKitLubanStatus();
    try {
        const result = parseTableKitOperationResult(await invoke('tablekit_open_config', {
            projectRoot: getTableKitProjectRoot(status),
            lubanWorkDir: tableKitConfig.lubanWorkDir,
        }));
        if (result.log) {
            appendTableKitConsoleLines(result.success ? 'success' : 'warning', result.log);
        } else {
            appendTableKitConsoleEntry(result.success ? 'success' : 'warning', '已打开配置表目录。');
        }
    } catch (error) {
        appendTableKitConsoleEntry('error', `打开配置表失败：${formatTableKitError(error)}`);
    }
    renderTableKitRegistryStatus();
}

async function handleTableKitGenerate() {
    await runTableKitLuban('generate');
}

async function handleTableKitValidatePreview() {
    await runTableKitLuban('validate');
}

async function runTableKitLuban(requestedMode) {
    if (!invoke) {
        appendTableKitConsoleEntry('warning', '当前不在 Tauri 环境中，无法执行 Luban。');
        renderTableKitRegistryStatus();
        return;
    }

    const status = getTableKitLubanStatus();
    const mode = requestedMode === 'validate' ? 'validate' : 'generate';
    const isValidate = mode === 'validate';
    appendTableKitConsoleEntry('info', isValidate ? '开始验证配置并生成临时 JSON 预览。' : '开始生成配置表。');

    try {
        const result = parseTableKitOperationResult(await invoke('tablekit_run_luban', {
            projectRoot: getTableKitProjectRoot(status),
            engine: getTableKitEffectiveEngine(status),
            mode,
            config: tableKitConfig,
        }));
        if (result.log) {
            appendTableKitConsoleLines(result.success ? 'info' : 'error', result.log);
        } else {
            appendTableKitConsoleEntry(result.success ? 'success' : 'error', result.success ? 'Luban 执行完成。' : 'Luban 执行失败。');
        }

        if (isValidate) {
            tableKitPreviewState.previewData = result.previewTables || [];
            tableKitPreviewState.tables = Array.isArray(result.previewTables) ? result.previewTables : [];
            if (!tableKitPreviewState.tables.some(table => String(table?.name || table?.fullName || '') === tableKitPreviewState.selectedTableName)) {
                tableKitPreviewState.selectedTableName = tableKitPreviewState.tables[0]?.name || tableKitPreviewState.tables[0]?.fullName || '';
                tableKitPreviewState.selectedRowIndex = 0;
            }
        }
    } catch (error) {
        appendTableKitConsoleEntry('error', `${isValidate ? '验证配置' : '生成配置表'}失败：${formatTableKitError(error)}`);
    }
    renderTableKitRegistryStatus();
}

function parseTableKitOperationResult(raw) {
    if (!raw) return { success: false, log: 'TableKit 后端没有返回结果。' };
    if (typeof raw === 'string') {
        try {
            return JSON.parse(raw);
        } catch (_) {
            return { success: true, log: raw };
        }
    }
    return raw;
}

function formatTableKitError(error) {
    return String(error?.message ?? error ?? '未知错误');
}

function getTableKitProjectRoot(status) {
    const statusProjectRoot = status?.projectPath && status.projectPath !== '--' ? normalizeTableKitPath(status.projectPath) : '';
    if (statusProjectRoot) return statusProjectRoot;

    const summaryProjectRoot = latestStatusSummary?.projectPath && latestStatusSummary.projectPath !== '--'
        ? normalizeTableKitPath(latestStatusSummary.projectPath)
        : '';
    if (summaryProjectRoot) return summaryProjectRoot;

    const engines = Array.isArray(latestStatusRaw?.engines) ? latestStatusRaw.engines : [];
    const engine = engines.find(candidate => candidate?.projectPath && candidate.connected !== false)
        || engines.find(candidate => candidate?.projectPath);
    const projectRoot = engine?.projectPath && engine.projectPath !== '--' ? normalizeTableKitPath(engine.projectPath) : '';
    return projectRoot || null;
}

async function copyTableKitConsoleText() {
    const text = tableKitConsoleEntries.length
        ? tableKitConsoleEntries.map(entry => `[${entry.time}] ${entry.level.toUpperCase()} ${entry.message}`).join('\n')
        : '暂无 TableKit 控制台日志';
    try {
        await navigator.clipboard?.writeText?.(text);
        appendTableKitConsoleEntry('success', '控制台日志已复制到剪贴板。');
    } catch (_) {
        appendTableKitConsoleEntry('warning', '剪贴板不可用，请直接在控制台区域手动选择复制。');
    }
    renderTableKitRegistryStatus();
}

function addTableKitExtraOutput() {
    tableKitConfig.extraOutputTargets = sanitizeTableKitExtraOutputTargets([
        ...(tableKitConfig.extraOutputTargets || []),
        TABLEKIT_EXTRA_OUTPUT_DEFAULT,
    ]);
    tableKitCollapsedSections = { ...tableKitCollapsedSections, extraOutputs: false };
    persistTableKitCollapsedSections();
    appendTableKitConsoleEntry('info', '已添加额外输出目标。');
    persistTableKitConfig();
    renderTableKitRegistryStatus();
}

function removeTableKitExtraOutput(index) {
    const targetIndex = Number(index);
    if (!Number.isInteger(targetIndex) || targetIndex < 0) return;
    tableKitConfig.extraOutputTargets = (tableKitConfig.extraOutputTargets || []).filter((_, itemIndex) => itemIndex !== targetIndex);
    appendTableKitConsoleEntry('info', '已移除额外输出目标。');
    persistTableKitConfig();
    renderTableKitRegistryStatus();
}

function updateTableKitExtraOutputField(index, field, value) {
    const targetIndex = Number(index);
    if (!Number.isInteger(targetIndex) || targetIndex < 0) return;
    const targets = (tableKitConfig.extraOutputTargets || []).map(item => ({ ...item }));
    if (!targets[targetIndex] || !Object.prototype.hasOwnProperty.call(TABLEKIT_EXTRA_OUTPUT_DEFAULT, field)) return;

    targets[targetIndex][field] = value;
    tableKitConfig.extraOutputTargets = sanitizeTableKitExtraOutputTargets(targets);
    persistTableKitConfig();
    renderTableKitRegistryStatus();
}

function updateTableKitConfigField(field, value) {
    if (!Object.prototype.hasOwnProperty.call(TABLEKIT_DEFAULT_CONFIG, field)) return;

    tableKitConfig = { ...tableKitConfig, [field]: value };

    if (field === 'dataTarget') {
        tableKitConfig.codeTarget = getTableKitMatchingCodeTarget(tableKitConfig.codeTarget, tableKitConfig.dataTarget);
    } else if (field === 'codeTarget') {
        tableKitConfig.dataTarget = getTableKitMatchingDataTarget(tableKitConfig.codeTarget);
    } else if (field === 'outputDataDir' && !tableKitConfig.customEditorDataPath) {
        tableKitConfig.editorDataPath = tableKitConfig.outputDataDir;
    } else if (field === 'customEditorDataPath' && !normalizeTableKitBool(value)) {
        tableKitConfig.editorDataPath = tableKitConfig.outputDataDir;
    }

    persistTableKitConfig();
    renderTableKitRegistryStatus();
}

function getTableKitMatchingCodeTarget(currentCodeTarget, dataTarget) {
    if (dataTarget === 'bin') return 'cs-bin';
    if (currentCodeTarget === 'cs-bin') return 'cs-simple-json';
    return TABLEKIT_CODE_TARGET_OPTIONS.includes(currentCodeTarget) ? currentCodeTarget : 'cs-simple-json';
}

function getTableKitMatchingDataTarget(codeTarget) {
    return codeTarget === 'cs-bin' ? 'bin' : 'json';
}

function renderTableKitValidateCommandPreview(config, status) {
    const projectRoot = status.projectPath && status.projectPath !== '--' ? normalizeTableKitPath(status.projectPath) : '<projectRoot>';
    const workDir = resolveTableKitProjectPath(projectRoot, config.lubanWorkDir);
    const dllPath = resolveTableKitProjectPath(projectRoot, config.lubanDllPath);
    const validateDir = resolveTableKitProjectPath(projectRoot, 'Temp/LubanValidate');
    const args = [
        '-t ' + config.target,
        '--conf luban.conf',
        '-d json',
        '-x outputDataDir=' + quoteTableKitShellValue(validateDir),
    ];

    return [
        'cd ' + quoteTableKitShellValue(workDir),
        'dotnet ' + quoteTableKitShellValue(dllPath) + ' ' + args.join(' '),
        '',
        '# validate JSON: ' + validateDir,
    ].join('\n');
}

function renderTableKitCommandPreview(config, status) {
    const projectRoot = status.projectPath && status.projectPath !== '--' ? normalizeTableKitPath(status.projectPath) : '<projectRoot>';
    const workDir = resolveTableKitProjectPath(projectRoot, config.lubanWorkDir);
    const dllPath = resolveTableKitProjectPath(projectRoot, config.lubanDllPath);
    const outputDataDir = resolveTableKitProjectPath(projectRoot, config.outputDataDir);
    const outputCodeDir = resolveTableKitProjectPath(projectRoot, config.outputCodeDir);
    const lubanCodeDir = joinTableKitPath(outputCodeDir, 'Luban');
    const args = [
        '-t ' + config.target,
        '--conf luban.conf',
        '-d ' + config.dataTarget,
        '-x ' + config.dataTarget + '.outputDataDir=' + quoteTableKitShellValue(outputDataDir),
        '-c ' + config.codeTarget,
        '-x ' + config.codeTarget + '.outputCodeDir=' + quoteTableKitShellValue(lubanCodeDir),
    ];
    const extraArgs = (config.extraOutputTargets || []).map(target => {
        const extraDir = resolveTableKitProjectPath(projectRoot, target.outputDataDir);
        return [
            '',
            '# extra output: ' + target.target + ' / ' + target.dataTarget,
            'dotnet ' + quoteTableKitShellValue(dllPath) + ' ' + [
                '-t ' + target.target,
                '--conf luban.conf',
                '-d ' + target.dataTarget,
                '-x ' + target.dataTarget + '.outputDataDir=' + quoteTableKitShellValue(extraDir),
            ].join(' '),
        ].join('\n');
    }).join('\n');

    return [
        'cd ' + quoteTableKitShellValue(workDir),
        'dotnet ' + quoteTableKitShellValue(dllPath) + ' ' + args.join(' '),
        extraArgs,
        '',
        '# 生成成功后',
        '# TableKit.cs: ' + outputCodeDir,
        '# Luban C#: ' + lubanCodeDir,
        '# Data: ' + outputDataDir,
    ].filter(line => line !== '').join('\n');
}

function resolveTableKitProjectPath(projectRoot, value) {
    const path = normalizeTableKitPath(value);
    if (isTableKitAbsolutePath(path) || projectRoot === '<projectRoot>') return path;
    return joinTableKitPath(projectRoot, path);
}

function isTableKitAbsolutePath(path) {
    return /^[A-Za-z]:\//.test(path) || path.startsWith('/') || path.startsWith('//');
}

function joinTableKitPath(left, right) {
    const a = normalizeTableKitPath(left).replace(/\/+$/, '');
    const b = normalizeTableKitPath(right).replace(/^\/+/, '');
    if (!a) return b;
    if (!b) return a;
    return `${a}/${b}`;
}

function normalizeTableKitPath(path) {
    return String(path ?? '').replace(/\\/g, '/');
}

function quoteTableKitShellValue(value) {
    return `"${String(value ?? '').replace(/"/g, '\\"')}"`;
}

function getTableKitEffectiveEngine(status) {
    return status.engine && status.engine !== '--' ? status.engine : 'auto';
}

function getTableKitLoaderText(engine) {
    return 'ResKit.LoadRaw / LoadRawText';
}

function formatTableKitOption(option) {
    if (option === 'auto') return 'auto（跟随宿主）';
    return option;
}

function formatTableKitBool(value) {
    return value ? '是' : '否';
}
