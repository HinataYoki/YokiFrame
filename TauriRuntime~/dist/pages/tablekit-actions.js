// pages/tablekit-actions.js
// TableKit Tauri 操作：路径选择、打开配置、生成/校验、console 复制和配置更新。
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
            tableKitPreviewState.previewVersion += 1;
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

function updateTableKitBehaviorSearchTerm(value) {
    tableKitBehaviorState.searchTerm = String(value || '');
    persistTableKitBehaviorSearchTerm();
    renderTableKitRegistryStatus();
}

function updateTableKitBehaviorTemplateSelection(templateId) {
    const selected = getTableKitBehaviorTemplateById(templateId);
    tableKitBehaviorState.selectedTemplateId = selected ? selected.id : TABLEKIT_BEHAVIOR_DEFAULT_TEMPLATE_ID;
    persistTableKitBehaviorTemplateSelection();
    renderTableKitRegistryStatus();
}

function updateTableKitBehaviorNodeSelection(nodeId) {
    const selected = getTableKitBehaviorNodeById(nodeId);
    tableKitBehaviorState.selectedNodeId = selected ? selected.id : TABLEKIT_BEHAVIOR_DEFAULT_NODE_ID;
    persistTableKitBehaviorNodeSelection();
    renderTableKitRegistryStatus();
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

function renderTableKitBehaviorCommandPreview() {
    const template = getTableKitBehaviorSelectedTemplate();
    const node = getTableKitBehaviorSelectedNode();
    return [
        '# TableKit Behavior 原型',
        '# template: ' + (template ? template.id : TABLEKIT_BEHAVIOR_DEFAULT_TEMPLATE_ID),
        '# node: ' + (node ? node.id : TABLEKIT_BEHAVIOR_DEFAULT_NODE_ID),
        '# xml: ' + getTableKitBehaviorXmlPreview(template, node),
    ].join('\n');
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
