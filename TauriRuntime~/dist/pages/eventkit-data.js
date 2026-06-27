// pages/eventkit-data.js
// EventKit 数据流：snapshot/command 获取、实时 payload 合并、归一化和关系行构建。
async function loadEventRegistrations(pageLoadToken = currentPageLoadToken('eventkit')) {
    if (!isCurrentPageLoad(pageLoadToken)) return;
    if (!invoke || !connected) {
        eventKitMonitorCache = null;
        eventKitMonitorRenderSignature = '';
        eventKitMonitorRowOrder = [];
        renderEventKitWorkbench();
        clearMetricsForLoad(pageLoadToken);
        return;
    }
    const requestSeq = ++eventKitMonitorLoadSeq;
    try {
        const data = await fetchEventKitMonitorSnapshot();
        if (!isCurrentPageLoad(pageLoadToken) || requestSeq !== eventKitMonitorLoadSeq) return;
        const nextSignature = makeEventKitMonitorSignature(
            data,
            eventKitScanCache,
            eventKitScanSearchTerm,
            selectedEventKitEngineId,
            selectedEventKitMonitorKey,
            selectedEventKitScanKey
        );
        if (nextSignature === eventKitMonitorRenderSignature) {
            clearMetricsForLoad(pageLoadToken);
            return;
        }

        eventKitMonitorCache = data;
        eventKitMonitorRenderSignature = nextSignature;
        clearMetricsForLoad(pageLoadToken);
        renderEventKitWorkbench();
    } catch (e) {
        if (!isCurrentPageLoad(pageLoadToken)) return;
        eventKitMonitorCache = null;
        clearMetricsForLoad(pageLoadToken);
        if (!canSendRuntimeKitCommand('EventKit')) {
            showRuntimeKitUnavailable('EventKit', t('eventkit.event_center'));
            return;
        }
        setPageBodyForLoad(pageLoadToken, panel(t('eventkit.error_title'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!'));
    }
}

function applyEventKitRealtimePayload(payload) {
    const record = payload?.record ?? payload?.latestEvent;
    if (!record) return false;

    const normalized = normalizeEventKitRecentEvents([record])[0];
    if (!normalized) return false;

    const current = eventKitMonitorCache ?? {
        counts: {},
        registrations: {},
        recentEvents: [],
    };
    const recentEvents = normalizeEventKitRecentEvents(current.recentEvents);
    const signature = makeEventKitRecentEventSignature(normalized);
    if (recentEvents.some(event => makeEventKitRecentEventSignature(event) === signature)) {
        return false;
    }

    recentEvents.push(normalized);
    if (recentEvents.length > EVENTKIT_MAX_RECENT_EVENTS) {
        recentEvents.splice(0, recentEvents.length - EVENTKIT_MAX_RECENT_EVENTS);
    }

    eventKitMonitorCache = {
        ...current,
        counts: current.counts ?? {},
        registrations: current.registrations ?? {},
        recentEvents,
    };
    eventKitMonitorRenderSignature = '';
    eventKitMonitorPendingRowIds.add(makeEventKitRelationId(normalized.channel, normalized.eventKey ?? normalized.key, normalized.payloadType));
    return true;
}

function scheduleEventKitSnapshotReconcile(options = {}) {
    if (activePage !== 'eventkit') return;
    const now = Date.now();
    const elapsed = now - eventKitLastSnapshotReconcileAt;
    const interactionDelay = getKitInteractionRemainingMs();
    if ((options.immediate || elapsed >= EVENTKIT_SNAPSHOT_RECONCILE_MS) && interactionDelay <= 0) {
        eventKitLastSnapshotReconcileAt = now;
        loadEventRegistrations(currentPageLoadToken('eventkit'));
        return;
    }

    if (eventKitSnapshotReconcileTimer) return;
    eventKitSnapshotReconcileTimer = setTimeout(() => {
        eventKitSnapshotReconcileTimer = 0;
        if (activePage !== 'eventkit') return;
        if (getKitInteractionRemainingMs() > 0) {
            scheduleEventKitSnapshotReconcile(options);
            return;
        }
        eventKitLastSnapshotReconcileAt = Date.now();
        loadEventRegistrations(currentPageLoadToken('eventkit'));
    }, Math.max(100, EVENTKIT_SNAPSHOT_RECONCILE_MS - elapsed, interactionDelay + 100));
}

async function fetchEventKitMonitorSnapshot() {
    const engine = getSelectedEventKitEngine();
    const engineId = engine?.engineId ?? latestStatusSummary?.engineId;
    if (invoke && engineId && engineId !== '--') {
        try {
            const raw = await invoke('read_snapshot', { engineId, kit: 'EventKit', snapshot: 'state' });
            const envelope = JSON.parse(raw);
            return normalizeEventKitMonitorPayload(envelope.data ?? envelope);
        } catch (_) {
            // Snapshot 可能尚不存在，回退到命令响应。
        }
    }

    if (!canSendRuntimeKitCommand('EventKit') && canShowStaticKitWorkbench('EventKit', engine)) {
        return createEmptyEventKitRuntimeSnapshot(runtimeKitUnavailableMessage('EventKit'));
    }

    if (!canSendRuntimeKitCommand('EventKit')) throw new Error(runtimeKitUnavailableMessage('EventKit'));

    const r = await invoke('send_command', { kit: 'EventKit', action: 'get_workbench_snapshot', payload: '{}' });
    const res = JSON.parse(r);
    if (res.status === 'error') throw formatBridgeError(res);
    return normalizeEventKitMonitorPayload(res.data ?? res);
}

function createEmptyEventKitRuntimeSnapshot(reason = '') {
    const snapshot = normalizeEventKitMonitorPayload({});
    snapshot.runtimeUnavailableReason = reason;
    return snapshot;
}

function normalizeEventKitMonitorPayload(data) {
    return {
        counts: normalizeEventKitCounts(data?.counts ?? data),
        registrations: normalizeEventKitRegistrations(data?.registrations ?? data),
        recentEvents: normalizeEventKitRecentEvents(data?.recentEvents),
    };
}

function normalizeEventKitCounts(counts = {}) {
    const typeEvents = Number(counts.typeEvents?.count ?? counts.typeEvents ?? 0);
    const enumEvents = Number(counts.enumEvents?.count ?? counts.enumEvents ?? 0);
    const stringEvents = Number(counts.stringEvents?.count ?? counts.stringEvents ?? 0);
    const totalEvents = Number(counts.totalEvents ?? typeEvents + enumEvents + stringEvents);
    const totalHandlers = Number(counts.totalHandlers ?? 0);
    return { typeEvents, enumEvents, stringEvents, totalEvents, totalHandlers };
}

function normalizeEventKitRegistrations(regs = {}) {
    return {
        typeEvents: normalizeEventKitRows(regs.typeEvents),
        enumEvents: normalizeEventKitRows(regs.enumEvents),
        stringEvents: normalizeEventKitRows(regs.stringEvents),
    };
}

function normalizeEventKitRows(rows) {
    if (!Array.isArray(rows)) return [];
    return rows.map(row => ({
        channel: row.channel ?? row.eventType ?? '--',
        key: row.key ?? row.type ?? row.eventKey ?? '--',
        type: row.type ?? row.key ?? '--',
        payloadType: row.payloadType ?? row.parameterType ?? (normalizeEventKitChannel(row.channel) === 'Type' ? row.key ?? row.type ?? row.eventKey : ''),
        handlerCount: Number(row.handlerCount ?? row.count ?? 0),
        deprecated: !!row.deprecated,
    }));
}

function normalizeEventKitRecentEvents(recentEvents) {
    const events = Array.isArray(recentEvents)
        ? recentEvents
        : (Array.isArray(recentEvents?.events) ? recentEvents.events : []);
    return events.map(event => ({
        id: event.id ?? event.eventId ?? event.requestId,
        seq: event.seq ?? event.sequence,
        kind: event.kind ?? event.event ?? 'event',
        channel: event.channel ?? event.eventType ?? '--',
        eventKey: event.eventKey ?? event.key ?? '--',
        time: event.time ?? event.timestamp ?? '--',
        handler: event.handler,
        payloadType: event.payloadType,
        sourceFile: event.sourceFile ?? event.file ?? event.filePath,
        sourceLine: event.sourceLine ?? event.line ?? event.lineNumber,
    }));
}

function normalizeEventKitScanEvents(events) {
    if (!Array.isArray(events)) return [];
    return events.map(event => ({
        channel: normalizeEventKitChannel(event.channel),
        eventKey: event.eventKey ?? event.key ?? '--',
        health: event.health ?? 'balanced',
        deprecated: !!event.deprecated,
        sendCount: Number(event.sendCount ?? 0),
        registerCount: Number(event.registerCount ?? 0),
        unregisterCount: Number(event.unregisterCount ?? 0),
        payloadType: event.payloadType ?? event.parameterType ?? (normalizeEventKitChannel(event.channel) === 'Type' ? event.eventKey ?? event.key : ''),
        sendLocations: eventKitEventLocations(event, 'send'),
        registerLocations: eventKitEventLocations(event, 'register'),
        unregisterLocations: eventKitEventLocations(event, 'unregister'),
    }));
}

function buildEventKitMonitorRows(regs, recentEvents, scanEvents = []) {
    const rowsById = new Map();

    const upsert = (channel, key, payloadType) => {
        const normalizedChannel = normalizeEventKitChannel(channel);
        const normalizedKey = String(key ?? '--');
        const normalizedPayloadType = resolveEventKitPayloadType(normalizedChannel, normalizedKey, payloadType);
        const id = makeEventKitRelationId(normalizedChannel, normalizedKey, normalizedPayloadType);
        let row = rowsById.get(id);
        if (!row) {
            row = {
                id,
                channel: normalizedChannel,
                key: normalizedKey,
                sendCount: 0,
                handlerCount: 0,
                registerActivityCount: 0,
                unregisterActivityCount: 0,
                deprecated: normalizedChannel === 'String',
                payloadType: normalizedPayloadType,
                lastTime: '',
                lastActivityIndex: -1,
                latestSourceLocation: null,
                latestSourceInferred: false,
                staticSendCount: 0,
                staticRegisterCount: 0,
                staticUnregisterCount: 0,
                senderLocations: [],
                receiverLocations: [],
                unregisterLocations: [],
                scanHealth: 'balanced',
                health: 'balanced',
                recentEvents: [],
            };
            rowsById.set(id, row);
        }
        return row;
    };
    const getExisting = (channel, key, payloadType) => {
        const normalizedChannel = normalizeEventKitChannel(channel);
        const normalizedKey = String(key ?? '--');
        const normalizedPayloadType = resolveEventKitPayloadType(normalizedChannel, normalizedKey, payloadType);
        return rowsById.get(makeEventKitRelationId(normalizedChannel, normalizedKey, normalizedPayloadType));
    };

    scanEvents.forEach(event => {
        const row = upsert(event.channel, event.eventKey, event.payloadType);
        row.staticSendCount += event.sendCount;
        row.staticRegisterCount += event.registerCount;
        row.staticUnregisterCount += event.unregisterCount;
        row.senderLocations = mergeEventKitLocations(row.senderLocations, event.sendLocations);
        row.receiverLocations = mergeEventKitLocations(row.receiverLocations, event.registerLocations);
        row.unregisterLocations = mergeEventKitLocations(row.unregisterLocations, event.unregisterLocations);
        row.scanHealth = event.health ?? row.scanHealth;
        row.deprecated = row.deprecated || !!event.deprecated;
    });

    [...regs.typeEvents, ...regs.enumEvents, ...regs.stringEvents].forEach(reg => {
        const row = getExisting(reg.channel, reg.key ?? reg.type, reg.payloadType);
        if (!row) return;

        row.handlerCount += Number(reg.handlerCount ?? 0);
        row.deprecated = row.deprecated || !!reg.deprecated;
    });

    recentEvents.forEach((event, index) => {
        const row = getExisting(event.channel, event.eventKey ?? event.key, event.payloadType);
        if (!row) return;

        const kind = String(event.kind ?? event.event ?? '').toLowerCase();
        if (kind === 'send') row.sendCount += 1;
        if (kind === 'register') row.registerActivityCount += 1;
        if (kind === 'unregister') row.unregisterActivityCount += 1;
        row.lastActivityIndex = index;
        row.lastTime = event.time ?? row.lastTime;
        row.payloadType = resolveEventKitPayloadType(row.channel, row.key, event.payloadType ?? row.payloadType);
        if (kind === 'send') {
            const sourceLocation = normalizeEventKitLocation(event.sourceFile, event.sourceLine);
            if (sourceLocation) {
                row.latestSourceLocation = sourceLocation;
                row.latestSourceInferred = false;
                row.senderLocations = mergeEventKitLocations(row.senderLocations, [sourceLocation]);
            }
        }
        row.recentEvents.push(event);
    });

    rowsById.forEach(row => {
        if (!row.latestSourceLocation && row.sendCount > 0 && row.senderLocations.length === 1) {
            row.latestSourceLocation = row.senderLocations[0];
            row.latestSourceInferred = true;
        }

        const hasSender = row.sendCount > 0 || row.senderLocations.length > 0;
        const hasReceiver = row.receiverLocations.length > 0;
        if (hasSender && !hasReceiver) {
            row.health = 'no_receiver';
        } else if (!hasSender && hasReceiver) {
            row.health = 'no_sender';
        } else if (row.scanHealth === 'leak_risk') {
            row.health = 'leak_risk';
        } else {
            row.health = 'balanced';
        }

        const hasScannedTopology = row.staticSendCount > 0 ||
            row.staticRegisterCount > 0 ||
            row.staticUnregisterCount > 0 ||
            row.senderLocations.length > 0 ||
            row.receiverLocations.length > 0 ||
            row.unregisterLocations.length > 0;
        if (!hasScannedTopology) {
            rowsById.delete(row.id);
        }
    });

    return orderEventKitMonitorRows(rowsById);
}

function normalizeEventKitLocation(filePath, line) {
    const path = String(filePath ?? '').replace(/\\/g, '/').trim();
    if (!path || path === '--') return null;
    const parsedLine = Number(line ?? 0);
    return {
        filePath: path,
        line: Number.isFinite(parsedLine) && parsedLine > 0 ? parsedLine : null,
        key: makeEventKitLocationKey(path, parsedLine),
    };
}

function parseEventKitLocationRef(ref) {
    if (!ref || typeof ref !== 'string') return null;
    const normalized = ref.replace(/\\/g, '/').trim();
    const match = normalized.match(/^(.*):(\d+)$/);
    if (!match) return normalizeEventKitLocation(normalized, null);
    return normalizeEventKitLocation(match[1], Number(match[2]));
}

function makeEventKitLocationKey(filePath, line) {
    const normalizedPath = String(filePath ?? '').replace(/\\/g, '/').toLowerCase();
    const normalizedLine = Number(line ?? 0);
    return normalizedPath + ':' + (Number.isFinite(normalizedLine) && normalizedLine > 0 ? normalizedLine : '');
}

function mergeEventKitLocations(current, incoming) {
    const locations = Array.isArray(current) ? current.slice() : [];
    const seen = new Set(locations.map(location => location.key));
    (Array.isArray(incoming) ? incoming : []).forEach(location => {
        if (!location || seen.has(location.key)) return;
        seen.add(location.key);
        locations.push(location);
    });
    return locations;
}

function isEventKitSameLocation(a, b) {
    if (!a || !b) return false;
    return a.key === b.key;
}

function filterEventKitUnifiedRows(rows) {
    const term = eventKitScanSearchTerm.trim().toLowerCase();
    if (!term) return rows;
    return rows.filter(row => {
        const haystack = [
            row.channel,
            row.key,
            row.health,
            row.payloadType,
            row.lastTime,
            ...row.senderLocations.map(formatEventKitFileRef),
            ...row.receiverLocations.map(formatEventKitFileRef),
            ...row.unregisterLocations.map(formatEventKitFileRef),
        ].join(' ').toLowerCase();
        return haystack.includes(term);
    });
}

function makeEventKitRelationId(channel, key, payloadType = '') {
    const normalizedChannel = normalizeEventKitChannel(channel);
    const normalizedKey = String(key ?? '--');
    return `${normalizedChannel}::${normalizedKey}::${resolveEventKitPayloadType(normalizedChannel, normalizedKey, payloadType)}`;
}

function resolveEventKitPayloadType(channel, key, payloadType) {
    const normalizedPayloadType = normalizeEventKitPayloadType(payloadType);
    if (normalizedPayloadType) return normalizedPayloadType;
    return normalizeEventKitChannel(channel) === 'Type' ? String(key ?? '--') : '';
}

function normalizeEventKitPayloadType(value) {
    if (value === null || value === undefined) return '';
    const text = String(value).trim();
    if (!text || text === '--' || text.toLowerCase() === 'null') return '';
    return text;
}

function formatEventKitPayloadType(row) {
    const payloadType = resolveEventKitPayloadType(row?.channel, row?.key, row?.payloadType);
    return payloadType ? t('eventkit.param_label', payloadType) : t('eventkit.no_param');
}

function renderEventKitPayloadBadge(row) {
    if (normalizeEventKitChannel(row?.channel) === 'Type') return '';
    return `<span class="eventkit-payload-pill">${escapeHtml(formatEventKitPayloadType(row))}</span>`;
}

function orderEventKitMonitorRows(rowsById) {
    const rowIds = new Set(rowsById.keys());
    eventKitMonitorRowOrder = eventKitMonitorRowOrder.filter(id => rowIds.has(id));

    const newRows = [...rowsById.values()]
        .filter(row => !eventKitMonitorRowOrder.includes(row.id))
        .sort(compareEventKitMonitorRows);
    newRows.forEach(row => eventKitMonitorRowOrder.push(row.id));

    return eventKitMonitorRowOrder
        .map(id => rowsById.get(id))
        .filter(Boolean);
}

function compareEventKitMonitorRows(a, b) {
    const channelDiff = eventKitMonitorChannelRank(a.channel) - eventKitMonitorChannelRank(b.channel);
    if (channelDiff !== 0) return channelDiff;
    return a.key.localeCompare(b.key);
}

function eventKitMonitorChannelRank(channel) {
    switch (normalizeEventKitChannel(channel)) {
        case 'Enum': return 0;
        case 'Type': return 1;
        case 'String': return 2;
        default: return 3;
    }
}

function ensureEventKitSelection(rows, currentKey) {
    if (!rows.length) return null;
    if (currentKey && rows.some(row => row.id === currentKey)) return currentKey;
    return rows[0].id;
}
