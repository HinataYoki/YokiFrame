// core/app-status.js
// 连接轮询和 System 状态面板
function setConnectionStatus(isConnected) {
    connected = isConnected;
    $status.textContent = isConnected ? t('common.connected') : t('common.disconnected');
    $status.className = `status-badge ${isConnected ? 'connected' : 'disconnected'}`;
    syncFrameworkCommandControls();
    if (isConnected) {
        void refreshFrameworkCommandCatalog({ force: true });
    } else {
        frameworkCommandCatalogLoaded = false;
    }
}

function summarizeStatus(status) {
    if (BridgeDiagnostics?.summarizeStatus) {
        return BridgeDiagnostics.summarizeStatus(status) ?? {};
    }
    const engine = status?.engines?.[0] ?? {};
    return {
        connected: status?.connected ?? false,
        engineId: engine.engineId ?? '--',
        engineLabel: `${engine.engine ?? '--'} ${engine.version ?? ''}`.trim() || '--',
        heartbeatPath: engine.heartbeatPath ?? '--',
        projectPath: engine.projectPath ?? '--',
    };
}

function summarizePing(response) {
    if (BridgeDiagnostics?.summarizePing) {
        return BridgeDiagnostics.summarizePing(response) ?? {};
    }
    return {
        statusLabel: response?.status ?? '--',
        requestId: response?.requestId ?? '--',
        engineId: response?.engineId ?? '--',
        reason: response?.status === 'error' ? formatBridgeError(response) : JSON.stringify(response?.data ?? {}),
    };
}

function summarizeBridgeStatus(payload) {
    if (BridgeDiagnostics?.summarizeBridgeStatus) {
        return BridgeDiagnostics.summarizeBridgeStatus(payload) ?? {};
    }
    return {
        queueLabel: payload?.pendingCommandCount ?? '--',
        deadletterLabel: payload?.deadletterCount ?? '--',
        resultLabel: payload?.resultCount ?? '--',
        storageLabel: payload?.storageLabel ?? '--',
        oldestFileLabel: payload?.oldestCommandFile ?? '--',
        backpressureLabel: payload?.backpressureLabel ?? '--',
        bridgeBusyLabel: payload?.bridgeBusy ?? '--',
        lastErrorLabel: payload?.lastError ?? '--',
    };
}

function isWindowInteractive() {
    if (document.hidden) return false;
    if (typeof document.hasFocus === 'function' && !document.hasFocus()) return false;
    return true;
}

function handleInteractiveFocusResume() {
    if (document.hidden) return;
    clearTimeout(statusPollResumeTimer);
    statusPollResumeTimer = setTimeout(() => {
        statusPollResumeTimer = 0;
        pollStatus({ force: true });
    }, 120);
}

async function pollStatus({ force = false } = {}) {
    if (!invoke) return;
    if (!force && !isWindowInteractive()) return;
    if (statusPollInFlight) {
        statusPollQueued = true;
        return;
    }

    statusPollInFlight = true;
    try {
        const result = await invoke('get_status');
        const status = JSON.parse(result);
        latestStatusRaw = status;
        latestStatusSummary = summarizeStatus(status);
        const scopedStorageChanged = syncProjectScopedEditorStorage();
        syncSidebarKitAvailability();
        const isConnected = status.connected;
        if (isConnected !== lastConnectedState) {
            lastConnectedState = isConnected;
            setConnectionStatus(isConnected);
            if (isConnected) {
                const list = (status.engines ?? []).map(e => `${e.engineId ?? e.engine}:${e.engine} v${e.version ?? ''} ${e.connected === false ? '(stale)' : ''}`.trim()).join(', ');
                addLog(t('connection.engine_connected', list), 'system');
            } else {
                addLog(t('connection.engine_disconnected'), 'error');
            }
        }
        if (activePage === 'system') {
            scheduleSystemPanelUpdate();
            if (isConnected && status.engines) {
                refreshBridgeDiagnosticsThrottled();
            }
        }
        if (activePage === 'tablekit') {
            renderTableKitRegistryStatus();
        } else if (scopedStorageChanged && activePage === 'audiokit') {
            renderAudioKitCachedPage();
        } else if (scopedStorageChanged && activePage === 'graphkit') {
            renderGraphKitWorkbench();
        }
    } catch (_) { /* IPC unavailable */ }
    finally {
        statusPollInFlight = false;
        if (statusPollQueued) {
            statusPollQueued = false;
            if (isWindowInteractive()) {
                setTimeout(() => pollStatus({ force: true }), 0);
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════

// 页面：System 概览
// ═══════════════════════════════════════════════════════════════════
function renderSystemMetrics(engines) {
    const count = engines?.length ?? 0;
    renderMetrics([
        { title: t('system.metrics.engine'), value: count, hint: t('system.metrics.engine_hint') },
        { title: t('system.metrics.bridge'), value: 'File I/O', hint: t('system.metrics.bridge_hint') },
        { title: t('system.metrics.frontend'), value: 'Tauri', hint: t('system.metrics.frontend_hint') },
    ]);
}

async function refreshBridgeDiagnosticsThrottled(force = false) {
    if (!invoke || !connected || bridgeDiagnosticsInFlight) return;
    const now = Date.now();
    if (!force && now - lastBridgeDiagnosticsAt < BRIDGE_DIAGNOSTICS_INTERVAL_MS) return;

    bridgeDiagnosticsInFlight = true;
    lastBridgeDiagnosticsAt = now;
    try {
        const response = await sendCommand({
            kit: 'System',
            action: 'bridge_status',
            syncControls: false,
            silentLog: true,
        });
        if (response) {
            latestBridgeStatusResponse = response;
            latestBridgeSummary = summarizeBridgeStatus(response.data ?? response);
        }
    } catch (e) {
        latestBridgeSummary = {
            queueLabel: '--',
            deadletterLabel: '--',
            resultLabel: '--',
            storageLabel: '--',
            oldestFileLabel: '--',
            backpressureLabel: '--',
            bridgeBusyLabel: '--',
            lastErrorLabel: String(e)
        };
    } finally {
        bridgeDiagnosticsInFlight = false;
        scheduleSystemPanelUpdate();
    }
}

function renderFrameworkStatusPanel() {
    return panel(t('system.engine_status'),
        renderFrameworkStatusContent(),
        'framework');
}

function renderFrameworkStatusContent() {
    const status = latestStatusSummary ?? {};
    const hasStatus = latestStatusSummary !== null;
    const isConnected = hasStatus ? !!status.connected : connected;
    const engineId = status.engineId && status.engineId !== '--' ? status.engineId : '--';
    const engineLabel = status.engineLabel ?? '--';
    const engineCount = Number.isFinite(Number(status.engineCount)) ? Number(status.engineCount) : (isConnected ? 1 : 0);
    const commandRoot = engineId !== '--'
        ? `.yokiframe/engines/${engineId}/commands`
        : '.yokiframe/engines/<engineId>/commands';
    return `<div class="framework-grid framework-grid--status">
        ${diagnosticTile(t('system.connection'), hasStatus ? (isConnected ? t('common.connected') : t('common.disconnected')) : t('system.checking'), hasStatus ? (isConnected ? engineLabel : t('system.waiting_heartbeat')) : t('system.reading'), hasStatus ? (isConnected ? 'success' : 'error') : 'warning')}
        ${diagnosticTile(t('system.engine'), engineId, engineCount ? t('system.registry_count', engineCount, engineLabel) : t('system.no_host_found'))}
        ${diagnosticTile(t('system.heartbeat_file'), status.heartbeatPath ?? '--', t('system.heartbeat_hint'))}
        ${diagnosticTile(t('system.command_path'), commandRoot, t('system.command_path_hint'))}
        ${diagnosticTile(t('system.event_stream'), 'JSONL', t('system.event_stream_hint'))}
    </div>`;
}

function updateFrameworkStatusPanel(force = false) {
    const container = document.getElementById('framework-status-panel');
    if (!container) return;
    const html = renderFrameworkStatusContent();
    if (!force && html === lastFrameworkStatusPanelHtml) return;
    container.innerHTML = html;
    lastFrameworkStatusPanelHtml = html;
}

function renderDeveloperContextStrip() {
    const status = latestStatusSummary ?? {};
    const hostLabel = status.engineLabel && status.engineLabel !== '--' ? status.engineLabel : 'Unity / Godot';
    const engineId = status.engineId && status.engineId !== '--' ? status.engineId : t('system.waiting_host');
    const bridgeLabel = latestBridgeSummary?.queueLabel && latestBridgeSummary.queueLabel !== '--'
        ? `${t('system.queue')} ${latestBridgeSummary.queueLabel}`
        : 'FileBridge';
    return `
        <section class="developer-context-strip" aria-label="${t('system.workbench_context')}">
            <div class="developer-context-strip__main">
                <span class="developer-context-strip__label">Native Debug Console</span>
                <strong>${t('system.workbench_title')}</strong>
                <span>${t('system.workbench_desc')}</span>
            </div>
            <div class="developer-context-strip__meta">
                <span><b>${t('system.host_connection')}</b><code>${escapeHtml(hostLabel)}</code></span>
                <span><b>${t('command_panel.refresh_bridge')}</b><code>${escapeHtml(engineId)}</code></span>
                <span><b>${t('system.kit_diagnostic')}</b><code>${escapeHtml(bridgeLabel)}</code></span>
            </div>
        </section>`;
}

function renderBridgeHealthPanel() {
    return panel(t('system.bridge_status'),
        renderBridgeHealthContent(),
        'status');
}

function renderBridgeHealthContent() {
    const bridge = latestBridgeSummary ?? {};
    const raw = latestBridgeStatusResponse?.data ?? latestBridgeStatusResponse ?? {};
    return `<div class="diagnostic-grid diagnostic-grid--compact">
        ${diagnosticTile(t('system.queue'), bridge.queueLabel ?? '--', 'pending / processing')}
        ${diagnosticTile(t('common.result'), bridge.resultLabel ?? '--', `deadletter ${bridge.deadletterLabel ?? '--'}`)}
        ${diagnosticTile(t('system.backpressure'), bridge.backpressureLabel ?? '--', `BridgeBusy ${bridge.bridgeBusyLabel ?? '--'}`, raw.backpressureActive ? 'warning' : 'success')}
        ${diagnosticTile(t('system.recent_error'), bridge.lastErrorLabel ?? '--', raw.lastPollLimitReason ? `limit ${raw.lastPollLimitReason}` : t('system.no_activity_limit'), bridge.lastErrorLabel && bridge.lastErrorLabel !== '--' ? 'error' : 'success')}
    </div>`;
}

function diagnosticTile(label, value, hint = '', tone = 'info') {
    return `<div class="diagnostic-tile diagnostic-tile--${tone}">
        <div class="diagnostic-tile__label">${escapeHtml(label)}</div>
        <div class="diagnostic-tile__value">${escapeHtml(value)}</div>
        ${hint ? `<div class="diagnostic-tile__hint">${escapeHtml(hint)}</div>` : ''}
    </div>`;
}

function updateBridgeHealthPanel(force = false) {
    const container = document.getElementById('bridge-health-panel');
    if (!container) return;
    const html = renderBridgeHealthContent();
    if (!force && html === lastBridgeHealthPanelHtml) return;
    container.innerHTML = html;
    lastBridgeHealthPanelHtml = html;
}

function scheduleSystemPanelUpdate({ force = false } = {}) {
    if (activePage !== 'system') return;
    if (pendingSystemPanelFrame) return;
    pendingSystemPanelFrame = requestAnimationFrame(() => {
        pendingSystemPanelFrame = 0;
        if (activePage !== 'system') return;
        updateFrameworkStatusPanel(force);
        updateFrameworkThemeSummary();
        updateFrameworkFontSummary();
        updateBridgeHealthPanel(force);
    });
}
