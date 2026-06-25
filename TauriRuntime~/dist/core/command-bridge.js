// core/command-bridge.js
// 命令桥请求、诊断和命令面板。
function formatBridgeError(response) {
    const err = response?.error;
    if (err && typeof err === 'object') {
        const code = err.code ? `${err.code}: ` : '';
        return `${code}${err.message ?? response.errorMessage ?? 'error'}`;
    }
    return err ?? response?.errorMessage ?? 'error';
}

function getFrameworkCommandElements() {
    return {
        kit: document.getElementById('framework-kit-select'),
        action: document.getElementById('framework-action-select'),
        send: document.getElementById('framework-send-btn'),
        clear: document.getElementById('log-clear-btn'),
    };
}

function syncFrameworkCommandControls() {
    const { kit, action, send } = getFrameworkCommandElements();
    if (kit) kit.disabled = !connected;
    if (action) action.disabled = !connected;
    if (send) send.disabled = !connected;
}

function setFrameworkCommandState(kit, action) {
    const catalog = getFrameworkCommandCatalog();
    const normalizedKit = catalog[kit] ? kit : 'System';
    const actions = getFrameworkCommandActions(normalizedKit);
    const normalizedAction = actions.some(item => item.action === action)
        ? action
        : actions[0]?.action || 'ping';
    commandComposerState = {
        kit: normalizedKit,
        action: normalizedAction,
    };
    const elements = getFrameworkCommandElements();
    if (elements.kit) elements.kit.value = commandComposerState.kit;
    updateFrameworkActionOptions();
}

function bindFrameworkCommandComposer() {
    const elements = getFrameworkCommandElements();
    if (elements.send && elements.send.dataset.bound !== '1') {
        elements.send.dataset.bound = '1';
        elements.send.addEventListener('click', () => sendCommand());
    }
    if (elements.kit && elements.kit.dataset.bound !== '1') {
        elements.kit.dataset.bound = '1';
        elements.kit.addEventListener('change', () => {
            commandComposerState.kit = elements.kit.value;
            updateFrameworkActionOptions();
        });
    }
    if (elements.action && elements.action.dataset.bound !== '1') {
        elements.action.dataset.bound = '1';
        elements.action.addEventListener('change', () => {
            commandComposerState.action = elements.action.value;
        });
    }
    document.querySelectorAll('[data-command-preset]').forEach((button) => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', async () => {
            const kit = button.dataset.commandPresetKit || 'System';
            const action = button.dataset.commandPresetAction || button.dataset.commandPreset || 'ping';
            await runFrameworkCommand(kit, action);
        });
    });
    syncFrameworkCommandControls();
}

async function runFrameworkCommand(kit, action) {
    setFrameworkCommandState(kit, action);
    return sendCommand({ kit, action, syncControls: false });
}

async function sendCommand(options = {}) {
    if (!invoke || !connected) return null;

    const elements = getFrameworkCommandElements();
    let kit = options.kit ?? elements.kit?.value ?? commandComposerState.kit;
    let action = options.action ?? elements.action?.value ?? commandComposerState.action;
    if (!action) return null;
    if (action === 'ping' || action === 'status' || action === 'bridge_status' || action === 'list_commands') {
        kit = 'System';
    }
    if (options.syncControls !== false) {
        setFrameworkCommandState(kit, action);
    }
    const payload = typeof options.payload === 'string'
        ? options.payload
        : JSON.stringify(options.payload ?? {});

    if (!options.silentLog) {
        addLog(`> ${kit}/${action}`, 'cmd-out');
    }
    try {
        const result = await invoke('send_command', { kit, action, payload });
        const response = JSON.parse(result);
        const errorText = formatBridgeError(response);
        const data = response.data ?? response.payload ?? errorText;
        const st = response.status ?? 'echo';
        const level = st === 'error' ? 'error' : 'cmd-in';
        const detail = st === 'error' ? ` [${errorText}]` : '';
        if (!options.silentLog) {
            addLog(`< ${response.kit}/${response.action} [${st}]: ${JSON.stringify(data)}${detail}`, level);
        }
        recordCommandDiagnostics(kit, action, response);
        return response;
    } catch (e) {
        latestPingSummary = action === 'ping'
            ? { statusLabel: 'error', requestId: '--', engineId: '--', reason: String(e) }
            : latestPingSummary;
        if (!options.silentLog) {
            addLog(`Error: ${e}`, 'error');
        }
        scheduleSystemPanelUpdate();
        return null;
    }
}

function recordCommandDiagnostics(kit, action, response) {
    if (kit === 'System' && action === 'ping') {
        latestPingSummary = summarizePing(response);
    }
    if (kit === 'System' && action === 'bridge_status') {
        latestBridgeStatusResponse = response;
        latestBridgeSummary = summarizeBridgeStatus(response?.data ?? response);
    }
    if (activePage === 'system') {
        scheduleSystemPanelUpdate();
    }
}

// ═══════════════════════════════════════════════════════════════════
// AI Skill 安装器
// ═══════════════════════════════════════════════════════════════════

function getFrameworkCommandActions(kit) {
    const catalog = getFrameworkCommandCatalog();
    return catalog[kit] || catalog.System || FRAMEWORK_COMMAND_CATALOG.System;
}

function getFrameworkCommandCatalog() {
    return frameworkCommandCatalog || FRAMEWORK_COMMAND_CATALOG;
}

function renderFrameworkCommandKitOptions() {
    return Object.keys(getFrameworkCommandCatalog()).map(kit => {
        const selected = kit === commandComposerState.kit ? ' selected' : '';
        return `<option value="${escapeHtml(kit)}"${selected}>${escapeHtml(kit)}</option>`;
    }).join('');
}

function renderFrameworkCommandActionOptions(kit) {
    return getFrameworkCommandActions(kit).map(item => {
        const selected = item.action === commandComposerState.action ? ' selected' : '';
        const desc = item.descriptionKey ? t(item.descriptionKey) : item.description;
        const label = desc ? `${item.label} - ${desc}` : item.label;
        return `<option value="${escapeHtml(item.action)}"${selected}>${escapeHtml(label)}</option>`;
    }).join('');
}

function updateFrameworkActionOptions() {
    const elements = getFrameworkCommandElements();
    if (!elements.action) return;
    const catalog = getFrameworkCommandCatalog();
    const requestedKit = elements.kit?.value || commandComposerState.kit || 'System';
    const kit = catalog[requestedKit] ? requestedKit : 'System';
    const actions = getFrameworkCommandActions(kit);
    const currentAction = actions.some(item => item.action === commandComposerState.action)
        ? commandComposerState.action
        : actions[0]?.action || 'ping';
    commandComposerState = { kit, action: currentAction };
    if (elements.kit) elements.kit.value = kit;
    elements.action.innerHTML = renderFrameworkCommandActionOptions(kit);
    elements.action.value = currentAction;
}

function getFrameworkCommandHint() {
    const action = getFrameworkCommandActions(commandComposerState.kit)
        .find(item => item.action === commandComposerState.action);
    if (action?.descriptionKey) return t(action.descriptionKey);
    return action?.description || t('command.select_from_catalog');
}

function cloneFrameworkCommandCatalog(source) {
    const clone = {};
    Object.entries(source || {}).forEach(([kit, actions]) => {
        clone[kit] = Array.isArray(actions)
            ? actions.map(item => ({ ...item }))
            : [];
    });
    return clone;
}

function normalizeCommandIdentifier(value) {
    if (typeof value !== 'string') return '';
    const trimmed = value.trim();
    if (!/^[A-Za-z0-9._-]{1,128}$/.test(trimmed) || trimmed === '.' || trimmed === '..') {
        return '';
    }
    return trimmed;
}

function findFallbackCommandAction(kit, action) {
    return (FRAMEWORK_COMMAND_CATALOG[kit] || []).find(item => item.action === action);
}

function applyFrameworkCommandCatalogResponse(payload) {
    const kits = Array.isArray(payload?.kits) ? payload.kits : [];
    const nextCatalog = {};
    kits.forEach(entry => {
        const kit = normalizeCommandIdentifier(entry?.kit);
        if (!kit) return;

        const actions = Array.isArray(entry?.actions) ? entry.actions : [];
        const normalizedActions = [];
        actions.forEach(actionEntry => {
            const action = normalizeCommandIdentifier(actionEntry?.action ?? actionEntry);
            if (!action) return;

            const fallback = findFallbackCommandAction(kit, action);
            if (!fallback) return;
            normalizedActions.push({
                action,
                label: fallback.label || action,
                description: fallback.description || t('command.select_from_catalog'),
            });
        });

        if (normalizedActions.length > 0) {
            nextCatalog[kit] = normalizedActions;
        }
    });

    if (!nextCatalog.System) {
        nextCatalog.System = cloneFrameworkCommandCatalog(FRAMEWORK_COMMAND_CATALOG).System;
    }

    frameworkCommandCatalog = nextCatalog;
    frameworkCommandCatalogLoaded = true;
    refreshFrameworkCommandControlsFromCatalog();
    return Object.keys(nextCatalog).length > 0;
}

function refreshFrameworkCommandControlsFromCatalog() {
    const elements = getFrameworkCommandElements();
    if (!elements.kit) return;

    const catalog = getFrameworkCommandCatalog();
    const kit = catalog[commandComposerState.kit] ? commandComposerState.kit : 'System';
    elements.kit.innerHTML = renderFrameworkCommandKitOptions();
    commandComposerState.kit = kit;
    elements.kit.value = kit;
    updateFrameworkActionOptions();
}

async function refreshFrameworkCommandCatalog({ force = false } = {}) {
    if (!invoke || !connected) return false;
    if (frameworkCommandCatalogInFlight || (!force && frameworkCommandCatalogLoaded)) return false;

    frameworkCommandCatalogInFlight = true;
    try {
        const response = await sendCommand({
            kit: 'System',
            action: 'list_commands',
            syncControls: false,
            silentLog: true,
        });
        if (response?.status === 'success') {
            return applyFrameworkCommandCatalogResponse(response.data ?? response);
        }
    } finally {
        frameworkCommandCatalogInFlight = false;
    }

    return false;
}

function renderFrameworkCommandContent() {
    return `<div class="framework-command">
        <div class="framework-command__form">
            <select id="framework-kit-select" class="cmd-select" aria-label="${t('command_panel.kit_label')}">
                ${renderFrameworkCommandKitOptions()}
            </select>
            <select id="framework-action-select" class="cmd-select" aria-label="${t('command_panel.action_label')}">
                ${renderFrameworkCommandActionOptions(commandComposerState.kit)}
            </select>
            <button id="framework-send-btn" class="btn btn-primary" type="button">${t('command_panel.send')}</button>
        </div>
        <div class="framework-command__hint">${escapeHtml(getFrameworkCommandHint())}</div>
        <div class="framework-command__presets">
            <button class="btn btn-secondary btn-sm" type="button" data-command-preset-kit="System" data-command-preset-action="ping">${t('command_panel.ping_engine')}</button>
            <button class="btn btn-secondary btn-sm" type="button" data-command-preset-kit="System" data-command-preset-action="status">${t('command_panel.read_status')}</button>
            <button class="btn btn-secondary btn-sm" type="button" data-command-preset-kit="System" data-command-preset-action="bridge_status">${t('command_panel.refresh_bridge')}</button>
            <button class="btn btn-secondary btn-sm" type="button" data-command-preset-kit="PoolKit" data-command-preset-action="get_workbench_snapshot">${t('command_panel.pool_snapshot')}</button>
        </div>
    </div>`;
}
function renderFrameworkCommandPanel() {
    return panel(t('command_panel.title'),
        renderFrameworkCommandContent(),
        'command');
}
