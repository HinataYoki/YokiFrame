// shared/kit-bridge.js
// ═══════════════════════════════════════════════════════════════════
// 命令桥与 Kit 状态读取
// ═══════════════════════════════════════════════════════════════════
async function sendKitCommandData(kit, action, payload = {}) {
    if (!canSendRuntimeKitCommand(kit)) {
        throw new Error(runtimeKitUnavailableMessage(kit));
    }

    const r = await invoke('send_command', { kit, action, payload: JSON.stringify(payload ?? {}) });
    const parsed = JSON.parse(r);
    if (parsed.status === 'error') {
        throw new Error(parsed.error?.message || parsed.errorMessage || `${kit}/${action} failed`);
    }
    return parsed.data ?? parsed;
}

function getActiveEngineId() {
    return getPreferredEngineId();
}

function getWorkbenchStatus() {
    return latestStatusRaw && typeof latestStatusRaw === 'object' ? latestStatusRaw : null;
}

function getWorkbenchEngines() {
    const status = getWorkbenchStatus();
    return Array.isArray(status?.engines) ? status.engines : [];
}

function getConnectedEngines() {
    const engines = getWorkbenchEngines();
    return engines.filter(engine => engine && engine.engineId && engine.connected !== false);
}

function canSendRuntimeKitCommand(kit) {
    if (kit === 'System') return true;
    return getConnectedEngines().some(engine => {
        return engineSupportsCapability(engine, 'commands') && engineSupportsCapability(engine, 'snapshots') && engineSupportsKit(engine, kit);
    });
}

function runtimeKitUnavailableMessage(kit) {
    return t('runtime.need_bridge', kit);
}

function getPreferredEngineId(options = {}) {
    const capability = String(options?.capability ?? options?.preferredCapability ?? '').trim().toLowerCase();
    const connectedEngines = getConnectedEngines();
    const findWithCapability = cap => connectedEngines.find(engine => engineSupportsCapability(engine, cap)) ?? null;
    const summaryEngineId = latestStatusSummary?.engineId;
    const summaryEngine = summaryEngineId && summaryEngineId !== '--'
        ? connectedEngines.find(engine => engine.engineId === summaryEngineId) ?? null
        : null;

    if (capability) {
        const capableEngine = findWithCapability(capability);
        if (capableEngine) return capableEngine.engineId;

        if (capability === 'telemetry') {
            const snapshotEngine = findWithCapability('snapshots');
            if (snapshotEngine) return snapshotEngine.engineId;
        }

        const commandEngine = findWithCapability('commands');
        if (commandEngine) return commandEngine.engineId;

        if (summaryEngine && engineSupportsCapability(summaryEngine, capability)) return summaryEngine.engineId;
    } else if (summaryEngine) {
        return summaryEngine.engineId;
    }

    return connectedEngines[0]?.engineId ?? (summaryEngineId && summaryEngineId !== '--' ? summaryEngineId : null);
}

function getSelectedEngineForNavigation() {
    const connectedEngines = getConnectedEngines();
    if (!connectedEngines.length) return null;

    const preferredEngineId = getPreferredEngineId();
    if (preferredEngineId) {
        const selectedEngine = connectedEngines.find(engine => engine.engineId === preferredEngineId);
        if (selectedEngine) return selectedEngine;
    }

    return connectedEngines[0];
}

function engineSupportsCapability(engine, capability) {
    if (!engine || !capability) return false;
    const capabilities = Array.isArray(engine.capabilities) ? engine.capabilities : [];
    return capabilities.some(item => String(item ?? '').toLowerCase() === capability);
}

function engineSupportsKit(engine, kit) {
    if (!kit || kit === 'System') return true;
    if (!engine) return true;

    if (!Array.isArray(engine.implementedKits)) return true;
    return engine.implementedKits.some(item => String(item ?? '').toLowerCase() === String(kit).toLowerCase());
}

function getKitFeatureList(engine, kit) {
    if (!engine || !kit || !engine.kitFeatures || typeof engine.kitFeatures !== 'object') return [];
    const direct = engine.kitFeatures[kit];
    if (Array.isArray(direct)) return direct;

    const target = String(kit).toLowerCase();
    for (const [key, value] of Object.entries(engine.kitFeatures)) {
        if (String(key).toLowerCase() === target && Array.isArray(value)) return value;
    }
    return [];
}

function engineSupportsKitFeature(engine, kit, feature) {
    if (!feature || !engineSupportsKit(engine, kit)) return false;
    return getKitFeatureList(engine, kit)
        .some(item => String(item ?? '').toLowerCase() === String(feature).toLowerCase());
}

function parseBridgePayload(raw) {
    if (raw == null) return null;
    if (typeof raw === 'object') return raw;
    if (typeof raw !== 'string' || !raw.trim()) return null;
    try {
        return JSON.parse(raw);
    } catch (_) {
        return null;
    }
}

async function readKitTelemetryData(kit, name = 'state') {
    const engineId = getPreferredEngineId({ capability: 'telemetry' });
    if (!invoke || !engineId) return null;
    try {
        const raw = await invoke('read_telemetry', { engineId, kit, name });
        const envelope = parseBridgePayload(raw);
        if (!envelope || envelope.available !== true) return null;
        return envelope.data ?? envelope;
    } catch (_) {
        return null;
    }
}

async function readKitSnapshotData(kit, snapshot = 'state') {
    const engineId = getPreferredEngineId({ capability: 'snapshots' });
    if (!invoke || !engineId) return null;
    try {
        const raw = await invoke('read_snapshot', { engineId, kit, snapshot });
        const envelope = parseBridgePayload(raw);
        if (!envelope) return null;
        return envelope.data ?? envelope;
    } catch (_) {
        return null;
    }
}

async function fetchKitWorkbenchState(kit, normalize, options = {}) {
    const forceCommandRefresh = !!options.forceCommandRefresh;
    const telemetryName = options.telemetryName || 'state';
    const snapshotName = options.snapshotName || telemetryName;
    const commandAction = options.commandAction || 'get_workbench_snapshot';
    const commandPayload = options.commandPayload || {};

    let data = null;
    if (!forceCommandRefresh) {
        data = await readKitTelemetryData(kit, telemetryName);
        if (!data) {
            data = await readKitSnapshotData(kit, snapshotName);
        }
    }

    if (!data) {
        if (!canSendRuntimeKitCommand(kit)) return null;
        data = await sendKitCommandData(kit, commandAction, commandPayload);
    }

    return typeof normalize === 'function' ? normalize(data) : data;
}
