// pages/eventkit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：EventKit
// ═══════════════════════════════════════════════════════════════════
let selectedEventKitEngineId = null;
let eventKitActiveTab = 'monitor';
let eventKitMonitorCache = null;
let eventKitMonitorLoadSeq = 0;
let eventKitScanCache = null;
let eventKitScanCacheKey = '';
let eventKitAutoScanAttemptedKey = '';
let eventKitScanInFlight = false;
let eventKitScanSearchTerm = '';
let eventKitScanExcludeEditor = true;
let selectedEventKitMonitorKey = null;
let selectedEventKitScanKey = null;
let eventKitMonitorRowOrder = [];
let eventKitLastObservedEventSignature = null;
let eventKitAnimatedRowKey = null;
let eventKitMonitorAnimationPrimed = false;
let eventKitMonitorRenderSignature = '';
let eventKitLastSnapshotReconcileAt = 0;
let eventKitSnapshotReconcileTimer = 0;
let eventKitMonitorRenderFrame = 0;
let eventKitMonitorLastAnimatedDomRowKey = null;
const eventKitMonitorPendingRowIds = new Set();
const EVENTKIT_SCROLL_SELECTORS = [
    '.eventkit-v1-relations',
    '.eventkit-v1-scan-map',
    '.eventkit-v1-timeline',
    '.eventkit-v1-quick-list',
    '.eventkit-v1-detail-scroll',
];

function renderEventKitPage() {
    $pageBody.classList.add('content-body--eventkit');
    setEventKitHero();

    clearMetrics();
    clearTabs();
    eventKitActiveTab = 'monitor';
    activeTab = 'monitor';

    selectEventKitDefaultEngine();
    renderEventKitWorkbench();
    loadEventRegistrations(currentPageLoadToken('eventkit'));
    scheduleEventKitAutoScan();
}

function setEventKitHero() {
    setHero(
        t('eventkit.title'),
        t('eventkit.subtitle'),
        t('eventkit.tab'),
        'event',
        renderEventKitHeroActions()
    );
}

async function refreshEventKit() {
    loadEventRegistrations(currentPageLoadToken('eventkit'));
}

async function refreshEventKitReactive(event) {
    const payload = event?.payload ?? {};
    const reason = payload.event ?? event?.type ?? '';
    if (reason === 'snapshot_updated') {
        const changed = applyEventKitRealtimePayload(payload);
        if (changed && activePage === 'eventkit' && eventKitActiveTab === 'monitor') {
            scheduleEventKitMonitorPartialRender();
        }
        scheduleEventKitSnapshotReconcile();
        return;
    }

    if (payload.record || payload.latestEvent) {
        const changed = applyEventKitRealtimePayload(payload);
        if (changed && activePage === 'eventkit' && eventKitActiveTab === 'monitor') {
            scheduleEventKitMonitorPartialRender();
        }
    }
}

function renderEventKitTabs() {
    $tabBar.innerHTML = `${renderTabButtons([
        { id: 'monitor', label: t('eventkit.tab_monitor') },
        { id: 'scan', label: t('eventkit.tab_scan') },
    ], eventKitActiveTab)}`;
    $tabBar.querySelectorAll('.tab-button').forEach(btn => {
        btn.dataset.eventkitTab = btn.dataset.tab;
    });
    $tabBar.querySelectorAll('.tab-button').forEach(btn => {
        btn.addEventListener('click', () => {
            $tabBar.querySelectorAll('.tab-button').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            eventKitActiveTab = btn.dataset.tab || 'monitor';
            activeTab = eventKitActiveTab;
            syncTabActiveIndicator(eventKitActiveTab);
            renderEventKitWorkbench();
            if (eventKitActiveTab === 'monitor') {
                loadEventRegistrations(currentPageLoadToken('eventkit'));
            }
            scheduleEventKitAutoScan();
        });
    });
    syncTabActiveIndicator(eventKitActiveTab, { scroll: false, animate: false });
}

function selectEventKitDefaultEngine() {
    const engines = getEventKitEngines();
    if (!engines.length) {
        selectedEventKitEngineId = null;
        selectedEventKitMonitorKey = null;
        selectedEventKitScanKey = null;
        resetEventKitMonitorRuntimeState();
        return;
    }

    if (!selectedEventKitEngineId || !engines.some(engine => engine.engineId === selectedEventKitEngineId)) {
        const connectedEngine = engines.find(engine => engine.connected !== false);
        selectedEventKitEngineId = (connectedEngine || engines[0]).engineId;
        selectedEventKitMonitorKey = null;
        selectedEventKitScanKey = null;
        resetEventKitMonitorRuntimeState();
    }
}

function resetEventKitMonitorRuntimeState() {
    eventKitMonitorRowOrder = [];
    eventKitLastObservedEventSignature = null;
    eventKitAnimatedRowKey = null;
    eventKitMonitorAnimationPrimed = false;
    eventKitMonitorLastAnimatedDomRowKey = null;
    eventKitMonitorPendingRowIds.clear();
    if (eventKitMonitorRenderFrame) {
        cancelAnimationFrame(eventKitMonitorRenderFrame);
        eventKitMonitorRenderFrame = 0;
    }
}

function getEventKitEngines() {
    return (latestStatusRaw?.engines ?? [])
        .filter(engine => engine && engine.engineId)
        .map(engine => ({
            ...engine,
            engineId: String(engine.engineId),
        }));
}

function getSelectedEventKitEngine() {
    const engines = getEventKitEngines();
    return engines.find(engine => engine.engineId === selectedEventKitEngineId) || engines[0] || null;
}

function renderEventKitWorkbench() {
    const scrollState = captureEventKitScrollState();
    setEventKitHero();
    let root = $pageBody.querySelector('[data-eventkit-workbench="root"]');
    if (!root) {
        $pageBody.innerHTML = `
            <div class="eventkit-workbench fade-in" data-eventkit-workbench="root">
                <section class="eventkit-monitor-workbench" data-eventkit-region="monitor"></section>
            </div>`;
        root = $pageBody.querySelector('[data-eventkit-workbench="root"]');
    }

    const monitor = root?.querySelector('[data-eventkit-region="monitor"]');
    const html = renderEventKitMonitorContent(eventKitMonitorCache);
    if (monitor && monitor.innerHTML !== html) {
        monitor.innerHTML = html;
    }
    bindEventKitWorkbenchActions();
    restoreEventKitScrollState(scrollState);
    eventKitMonitorLastAnimatedDomRowKey = eventKitAnimatedRowKey;
}

function captureEventKitScrollState() {
    const state = {};
    EVENTKIT_SCROLL_SELECTORS.forEach(selector => {
        const el = $pageBody.querySelector(selector);
        if (el) state[selector] = el.scrollTop;
    });
    return state;
}

function restoreEventKitScrollState(state) {
    if (!state) return;
    requestAnimationFrame(() => {
        EVENTKIT_SCROLL_SELECTORS.forEach(selector => {
            const el = $pageBody.querySelector(selector);
            if (el && Number.isFinite(state[selector])) {
                el.scrollTop = state[selector];
            }
        });
    });
}

function renderEventKitEngineInline(selectedEngine) {
    const engines = getEventKitEngines();
    if (!engines.length) {
        return `<div class="eventkit-engine-inline eventkit-engine-inline--stale"><span>${t('eventkit.no_engine')}</span></div>`;
    }

    const stateClass = selectedEngine?.connected === false ? ' eventkit-engine-inline--stale' : '';
    const stateText = selectedEngine?.connected === false ? t('eventkit.offline_snapshot') : t('eventkit.realtime_online');
    if (engines.length === 1) {
        return `<div class="eventkit-engine-inline${stateClass}">
            <span class="eventkit-engine-inline__state">${escapeHtml(stateText)}</span>
            <strong>${escapeHtml(selectedEngine?.engine ?? 'Engine')}</strong>
            <code>${escapeHtml(selectedEngine?.engineId ?? '--')}</code>
        </div>`;
    }

    const options = engines.map(engine => `<option value="${escapeHtml(engine.engineId)}"${engine.engineId === selectedEventKitEngineId ? ' selected' : ''}>${escapeHtml(engine.engine ?? 'Engine')} · ${escapeHtml(engine.engineId)}</option>`).join('');
    return `<label class="eventkit-engine-inline${stateClass}">
        <span class="eventkit-engine-inline__state">${escapeHtml(stateText)}</span>
        <select data-eventkit-engine-select>${options}</select>
    </label>`;
}

function renderEventKitMonitorContent(snapshot) {
    const view = buildEventKitMonitorView(snapshot);
    return renderEventKitMonitorHtml(view);
}

function buildEventKitMonitorView(snapshot, options = {}) {
    if (!connected || !getSelectedEventKitEngine()) {
        return {
            state: 'disconnected',
            html: emptyState('event', t('eventkit.need_runtime_bridge')),
        };
    }

    if (!snapshot) {
        return {
            state: 'loading',
            html: `<div class="eventkit-loading">${t('eventkit.loading_snapshot')}</div>`,
        };
    }

    const counts = normalizeEventKitCounts(snapshot.counts);
    const regs = normalizeEventKitRegistrations(snapshot.registrations);
    const recentEvents = normalizeEventKitRecentEvents(snapshot.recentEvents);
    if (options.prepareAnimation !== false) {
        prepareEventKitMonitorAnimation(recentEvents);
    }
    const scanEvents = normalizeEventKitScanEvents(eventKitScanCache?.events);
    const hasScanData = !!eventKitScanCache && !eventKitScanCache.error && Array.isArray(eventKitScanCache.events);
    const rows = hasScanData && scanEvents.length ? buildEventKitMonitorRows(regs, recentEvents, scanEvents) : [];
    const filteredRows = filterEventKitUnifiedRows(rows);
    selectedEventKitMonitorKey = ensureEventKitSelection(filteredRows, selectedEventKitMonitorKey);
    const selectedRow = filteredRows.find(row => row.id === selectedEventKitMonitorKey) || filteredRows[0] || null;
    const engine = getSelectedEventKitEngine();
    const emptyMessage = rows.length
        ? t('eventkit.no_match_filter')
        : formatEventKitTopologyEmptyMessage(hasScanData, scanEvents);
    return {
        state: 'ready',
        counts: counts,
        regs: regs,
        recentEvents: recentEvents,
        scanEvents: scanEvents,
        hasScanData: hasScanData,
        rows: rows,
        filteredRows: filteredRows,
        selectedRow: selectedRow,
        selectedKey: selectedEventKitMonitorKey,
        engine: engine,
        emptyMessage: emptyMessage,
        html: renderEventKitMonitorHtml({
            counts: counts,
            recentEvents: recentEvents,
            scanEvents: scanEvents,
            hasScanData: hasScanData,
            rows: rows,
            filteredRows: filteredRows,
            selectedRow: selectedRow,
            selectedKey: selectedEventKitMonitorKey,
            engine: engine,
            emptyMessage: emptyMessage,
        }),
    };
}

function renderEventKitMonitorHtml(view) {
    if (view.state === 'disconnected' || view.state === 'loading') {
        return view.html;
    }

    return `
        <div class="eventkit-v1-layout eventkit-v1-layout--monitor">
            <div class="eventkit-v1-main">
                <section class="eventkit-v1-map">
                    ${eventKitRelationHeader(t('eventkit.sender_code'), t('eventkit.event_unregister'), t('eventkit.receiver_code'))}
                    <div class="eventkit-v1-relations" data-eventkit-region="flow">
                        ${view.filteredRows.length ? view.filteredRows.map(row => renderEventKitMonitorRow(row, row.id === view.selectedKey)).join('') : emptyState('event', view.emptyMessage)}
                    </div>
                </section>
                <aside class="eventkit-v1-side">
                    ${renderEventKitMonitorDetail(view.selectedRow, view.counts, view.recentEvents, view.emptyMessage)}
                </aside>
            </div>
        </div>`;
}

function scheduleEventKitMonitorPartialRender() {
    if (eventKitMonitorRenderFrame) return;
    eventKitMonitorRenderFrame = requestAnimationFrame(() => {
        eventKitMonitorRenderFrame = 0;
        renderEventKitMonitorPartialUpdate();
    });
}

function renderEventKitMonitorPartialUpdate() {
    if (activePage !== 'eventkit' || eventKitActiveTab !== 'monitor') return;
    const root = $pageBody.querySelector('[data-eventkit-workbench="root"]');
    const flow = root?.querySelector('[data-eventkit-region="flow"]');
    const side = root?.querySelector('.eventkit-v1-side');
    if (!root || !flow || !side) {
        renderEventKitWorkbench();
        return;
    }

    const view = buildEventKitMonitorView(eventKitMonitorCache, { prepareAnimation: true });
    if (view.state !== 'ready') {
        renderEventKitWorkbench();
        return;
    }
    if (!view.filteredRows.length) {
        eventKitMonitorPendingRowIds.clear();
        eventKitMonitorLastAnimatedDomRowKey = eventKitAnimatedRowKey;
        return;
    }

    const dirtyRowIds = new Set(eventKitMonitorPendingRowIds);
    eventKitMonitorPendingRowIds.clear();
    if (eventKitMonitorLastAnimatedDomRowKey && eventKitMonitorLastAnimatedDomRowKey !== eventKitAnimatedRowKey) {
        dirtyRowIds.add(eventKitMonitorLastAnimatedDomRowKey);
    }
    if (eventKitAnimatedRowKey) {
        dirtyRowIds.add(eventKitAnimatedRowKey);
    }

    let updated = false;
    const rowElements = Array.from(flow.querySelectorAll('[data-eventkit-monitor-row]'));
    for (const row of view.filteredRows) {
        if (!dirtyRowIds.has(row.id)) continue;
        const rowElement = rowElements.find(element => element.dataset.eventkitMonitorRow === row.id);
        if (!rowElement) continue;
        const nextRowHtml = renderEventKitMonitorRow(row, row.id === view.selectedKey);
        if (rowElement.outerHTML === nextRowHtml) continue;
        rowElement.outerHTML = nextRowHtml;
        updated = true;
    }

    if (!updated) {
        eventKitMonitorLastAnimatedDomRowKey = eventKitAnimatedRowKey;
        return;
    }

    const nextDetailHtml = renderEventKitMonitorDetail(view.selectedRow, view.counts, view.recentEvents, view.emptyMessage);
    if (side.innerHTML !== nextDetailHtml) {
        side.innerHTML = nextDetailHtml;
    }

    bindEventKitWorkbenchActions();
    eventKitMonitorLastAnimatedDomRowKey = eventKitAnimatedRowKey;
}

function updateEventKitMonitorSelection(value) {
    selectedEventKitMonitorKey = value;
    const root = $pageBody.querySelector('[data-eventkit-workbench="root"]');
    const side = root?.querySelector('.eventkit-v1-side');
    if (!root || !side) {
        renderEventKitWorkbench();
        return;
    }

    const view = buildEventKitMonitorView(eventKitMonitorCache, { prepareAnimation: false });
    if (view.state !== 'ready') {
        renderEventKitWorkbench();
        return;
    }

    root.querySelectorAll('[data-eventkit-monitor-row]').forEach(row => {
        row.classList.toggle('active', row.dataset.eventkitMonitorRow === view.selectedKey);
    });

    const nextDetailHtml = renderEventKitMonitorDetail(view.selectedRow, view.counts, view.recentEvents, view.emptyMessage);
    if (side.innerHTML !== nextDetailHtml) {
        side.innerHTML = nextDetailHtml;
    }
    bindEventKitWorkbenchActions();
}

function formatEventKitTopologyEmptyMessage(hasScanData, scanEvents) {
    if (eventKitScanInFlight) return t('eventkit.scan_in_progress');
    if (eventKitScanCache?.error) return t('eventkit.scan_error_hint');
    if (!hasScanData) return t('eventkit.auto_scan_preparing');
    if (!scanEvents.length) return t('eventkit.no_usage_found');
    return t('eventkit.no_relations');
}

function eventKitPanelHeader(title, description, meta = '') {
    return `<div class="eventkit-v1-panel-head">
        <div>
            <div class="eventkit-section-title">${escapeHtml(title)}</div>
            <div class="eventkit-section-desc">${escapeHtml(description)}</div>
        </div>
        ${meta ? `<span class="eventkit-v1-panel-meta">${escapeHtml(meta)}</span>` : ''}
    </div>`;
}

function eventKitRelationHeader(left, center, right) {
    return `<div class="eventkit-v1-relation-head">
        <span class="eventkit-v1-relation-head__left">${escapeHtml(left)}</span>
        <span class="eventkit-v1-relation-head__center">${escapeHtml(center)}</span>
        <span class="eventkit-v1-relation-head__right">${escapeHtml(right)}</span>
    </div>`;
}

function eventKitStatChip(label, value, tone = '') {
    return `<div class="eventkit-v1-stat eventkit-v1-stat--${escapeHtml(tone)}">
        <span>${escapeHtml(label)}</span>
        <strong>${escapeHtml(value)}</strong>
    </div>`;
}

function renderEventKitHeroActions() {
    const engine = getSelectedEventKitEngine();
    const scan = eventKitScanCache;
    const projectPath = scan?.projectPath ?? engine?.projectPath ?? latestStatusSummary?.projectPath ?? '--';
    const scanState = eventKitScanInFlight
        ? t('eventkit.scanning')
        : (scan?.error ? t('eventkit.scan_failed_short') : (scan ? `${escapeHtml(scan.matchedFileCount ?? 0)} matched / ${escapeHtml(scan.scannedFileCount ?? 0)} files` : t('eventkit.not_scanned')));
    return `<div class="eventkit-hero-actions">
        <div class="eventkit-hero-status">
            <span>${escapeHtml(projectPath)}</span>
            <strong>${scanState}</strong>
            ${scan?.error ? `<div class="eventkit-v1-note eventkit-v1-note--warning">${escapeHtml(scan.error)}</div>` : ''}
        </div>
        <div class="eventkit-hero-controls">
            ${renderEventKitEngineInline(engine)}
            ${renderKitToggle(t('eventkit.exclude_editor'), eventKitScanExcludeEditor, 'data-eventkit-exclude-editor')}
            <label class="eventkit-scan-search">
                ${svgIcon('search', 'shell-icon')}
                <input data-eventkit-scan-search value="${escapeHtml(eventKitScanSearchTerm)}" placeholder="${t('eventkit.search_sender_receiver')}">
            </label>
            <button class="btn btn-sm" onclick="refreshEventKit()">${t('eventkit.refresh_runtime')}</button>
            <button class="btn btn-primary btn-sm" onclick="runEventKitCodeScan()"${eventKitScanInFlight ? ' disabled' : ''}>${eventKitScanInFlight ? t('eventkit.scanning') : t('eventkit.scan_code')}</button>
        </div>
    </div>`;
}

function prepareEventKitMonitorAnimation(recentEvents) {
    const latestSend = recentEvents
        .slice()
        .reverse()
        .find(event => String(event.kind ?? event.event ?? '').toLowerCase() === 'send');
    const signature = latestSend ? makeEventKitRecentEventSignature(latestSend) : null;

    eventKitAnimatedRowKey = null;
    if (signature && eventKitMonitorAnimationPrimed && signature !== eventKitLastObservedEventSignature) {
        eventKitAnimatedRowKey = makeEventKitRelationId(latestSend.channel, latestSend.eventKey ?? latestSend.key, latestSend.payloadType);
    }

    eventKitLastObservedEventSignature = signature;
    eventKitMonitorAnimationPrimed = true;
}

function makeEventKitRecentEventSignature(event) {
    return [
        event.id ?? '',
        event.seq ?? '',
        String(event.kind ?? event.event ?? '').toLowerCase(),
        normalizeEventKitChannel(event.channel),
        event.eventKey ?? event.key ?? '--',
        event.time ?? '',
        event.handler ?? '',
        event.payloadType ?? '',
        event.sourceFile ?? '',
        event.sourceLine ?? '',
    ].join('|');
}

function renderEventKitFlightOverlay() {
    return `<div class="eventkit-v1-flight" aria-hidden="true">
        <span class="eventkit-v1-flight__beam eventkit-v1-flight__beam--in"></span>
        <span class="eventkit-v1-flight__beam eventkit-v1-flight__beam--out"></span>
        <span class="eventkit-v1-flight__pulse eventkit-v1-flight__pulse--sender"></span>
        <span class="eventkit-v1-flight__pulse eventkit-v1-flight__pulse--receiver"></span>
    </div>`;
}

function renderEventKitMonitorRow(row, selected) {
    const channel = normalizeEventKitChannel(row.channel);
    const channelClass = eventKitChannelClass(channel);
    const healthClass = eventKitHealthClass(row.health);
    const animated = eventKitAnimatedRowKey === row.id && row.sendCount > 0;
    return `<article class="eventkit-v1-row eventkit-v1-row--${escapeHtml(channelClass)} ${escapeHtml(healthClass)}${selected ? ' active' : ''}${animated ? ' eventkit-v1-row--animated' : ''}" data-eventkit-monitor-row="${escapeHtml(row.id)}" role="button" tabindex="0">
        ${animated ? renderEventKitFlightOverlay() : ''}
        <div class="eventkit-v1-sidecell eventkit-v1-sidecell--sender">
            ${renderEventKitMonitorSender(row)}
        </div>
        <div class="eventkit-v1-event-column">
            <div class="eventkit-v1-event-node">
                <div class="eventkit-v1-node-badges">
                    <span class="eventkit-channel eventkit-channel--${escapeHtml(channelClass)}">${escapeHtml(channel)}</span>
                    <span class="eventkit-health ${escapeHtml(healthClass)}">${escapeHtml(formatEventKitHealth(row.health))}</span>
                    ${renderEventKitPayloadBadge(row)}
                </div>
                <strong>${escapeHtml(row.key)}</strong>
                <div class="eventkit-v1-node-meta">
                    <span>Runtime ${escapeHtml(row.sendCount)} send</span>
                    <span>Code S${escapeHtml(row.staticSendCount)} / R${escapeHtml(row.staticRegisterCount)} / U${escapeHtml(row.staticUnregisterCount)}</span>
                </div>
            </div>
            ${renderEventKitMonitorUnregisters(row)}
        </div>
        <div class="eventkit-v1-sidecell eventkit-v1-sidecell--receiver">
            ${renderEventKitMonitorReceivers(row)}
        </div>
    </article>`;
}

function renderEventKitMonitorSender(row) {
    if (row.senderLocations.length) {
        return `<div class="eventkit-v1-code-stack">
            ${renderEventKitLocationButtons(row.senderLocations, 'send', row.latestSourceLocation, row.latestSourceInferred, { maxGroups: 3 })}
            ${row.sendCount > 0 ? `<div class="eventkit-v1-code-stack__runtime">${t('eventkit.trigger_suffix', row.sendCount)}${row.lastTime ? ' · ' + escapeHtml(row.lastTime) : ''}</div>` : ''}
        </div>`;
    }

    return `<div class="eventkit-v1-endpoint eventkit-v1-endpoint--muted">
        <span>${t('eventkit.no_sender_scanned')}</span>
        <strong>${row.sendCount > 0 ? t('eventkit.runtime_trigger_count', row.sendCount) : t('eventkit.waiting_scan')}</strong>
        ${row.lastTime ? `<em>${escapeHtml(row.lastTime)}</em>` : ''}
    </div>`;
}

function renderEventKitMonitorReceivers(row) {
    if (row.receiverLocations.length) {
        return `<div class="eventkit-v1-code-stack">
            ${renderEventKitLocationButtons(row.receiverLocations, 'receive', null, false, { maxGroups: 3 })}
        </div>`;
    }

    return `<div class="eventkit-v1-endpoint eventkit-v1-endpoint--warning">
        <span>${t('eventkit.no_receiver_scanned')}</span>
        <strong>${t('eventkit.unregistered')}</strong>
    </div>`;
}

function renderEventKitMonitorUnregisters(row) {
    if (row.unregisterLocations.length) {
        return `<div class="eventkit-v1-unregister-stack">
            <div class="eventkit-v1-unregister-stack__head">
                <span>${t('eventkit.unregister_party')}</span>
                <strong>${escapeHtml(row.unregisterLocations.length)}</strong>
            </div>
            ${renderEventKitLocationButtons(row.unregisterLocations, 'unregister', null, false, { maxGroups: 3 })}
            ${row.unregisterActivityCount > 0 ? `<div class="eventkit-v1-code-stack__runtime">${t('eventkit.runtime_unregister_count', row.unregisterActivityCount)}</div>` : ''}
        </div>`;
    }

    return `<div class="eventkit-v1-unregister-empty">
        <span>UnRegister</span>
        <strong>${row.unregisterActivityCount > 0 ? t('eventkit.runtime_unregister_count', row.unregisterActivityCount) : t('eventkit.no_unregister_scanned')}</strong>
    </div>`;
}

function renderEventKitScanUnregisters(files) {
    if (files.length) {
        return `<div class="eventkit-v1-unregister-stack">
            <div class="eventkit-v1-unregister-stack__head">
                <span>${t('eventkit.unregister_party')}</span>
                <strong>${escapeHtml(files.length)}</strong>
            </div>
            ${renderEventKitLocationList(files, '', 'unregister', { maxGroups: 3 })}
        </div>`;
    }

    return `<div class="eventkit-v1-unregister-empty">
        <span>UnRegister</span>
        <strong>${t('eventkit.no_unregister_scanned')}</strong>
    </div>`;
}

function renderEventKitMonitorDetail(row, counts, recentEvents, emptyMessage = t('eventkit.select_event_hint')) {
    if (!row) {
        return `<section class="eventkit-v1-detail eventkit-v1-detail-card">${emptyState('event', emptyMessage)}</section>`;
    }

    const matchingEvents = row.recentEvents.length ? row.recentEvents.slice().reverse() : [];
    const channelClass = eventKitChannelClass(row.channel);
    const healthClass = eventKitHealthClass(row.health);
    return `
        <section class="eventkit-v1-detail eventkit-v1-detail-card">
            <div class="eventkit-v1-detail-top eventkit-v1-detail-top--${escapeHtml(channelClass)}">
                <div class="eventkit-v1-detail-badges">
                    <span class="eventkit-channel eventkit-channel--${escapeHtml(channelClass)}">${escapeHtml(row.channel)}</span>
                    <span class="eventkit-health ${escapeHtml(healthClass)}">${escapeHtml(formatEventKitHealth(row.health))}</span>
                    ${renderEventKitPayloadBadge(row)}
                </div>
                <h3>${escapeHtml(row.key)}</h3>
            </div>
            <div class="eventkit-v1-detail-stats">
                ${eventKitStatChip(t('eventkit.stat_cumulative'), row.sendCount, 'send')}
                ${eventKitStatChip(t('eventkit.stat_recent'), matchingEvents.length, 'history')}
                ${eventKitStatChip(t('eventkit.stat_static'), row.senderLocations.length + row.receiverLocations.length + row.unregisterLocations.length, 'handler')}
            </div>
            <div class="eventkit-v1-detail-scroll">
                ${eventKitPanelHeader(t('eventkit.timeline_title'), t('eventkit.timeline_desc'), matchingEvents.length + ' ' + t('common.items'))}
                <div class="eventkit-v1-timeline" data-eventkit-region="timeline">
                    ${matchingEvents.length ? matchingEvents.map(renderEventKitTimelineItem).join('') : emptyState('status', t('eventkit.no_event_history'))}
                </div>
            </div>
            ${row.deprecated ? `<div class="eventkit-v1-note eventkit-v1-note--warning">${t('eventkit.string_deprecated')}</div>` : ''}
        </section>`;
}

function renderEventKitTimelineItem(event) {
    const channel = normalizeEventKitChannel(event.channel);
    const kind = event.kind ?? event.event ?? 'event';
    const sourceLocation = normalizeEventKitLocation(event.sourceFile, event.sourceLine);
    return `<div class="eventkit-v1-timeline-item eventkit-v1-timeline-item--${escapeHtml(String(kind).toLowerCase())}">
        <span class="eventkit-v1-timeline-item__time">${escapeHtml(event.time ?? '--')}</span>
        <span class="eventkit-channel eventkit-channel--${escapeHtml(channel.toLowerCase())}">${escapeHtml(channel)}</span>
        <span class="eventkit-v1-timeline-item__kind">${escapeHtml(formatEventKitKind(kind))}</span>
        <strong>${escapeHtml(event.eventKey ?? event.key ?? '--')}</strong>
        ${event.handler ? `<span class="eventkit-v1-timeline-item__payload">${escapeHtml(event.handler)}</span>` : ''}
        ${event.payloadType ? `<span class="eventkit-v1-timeline-item__payload">${escapeHtml(event.payloadType)}</span>` : ''}
        ${sourceLocation ? `<button class="eventkit-v1-timeline-source" data-eventkit-open-file="${escapeHtml(sourceLocation.filePath)}" data-eventkit-open-line="${escapeHtml(sourceLocation.line ?? '')}">${escapeHtml(formatEventKitFileRef(sourceLocation))}</button>` : ''}
    </div>`;
}

function normalizeEventKitChannel(channel) {
    const raw = String(channel ?? '--');
    if (raw.toLowerCase() === 'type') return 'Type';
    if (raw.toLowerCase() === 'enum') return 'Enum';
    if (raw.toLowerCase() === 'string') return 'String';
    return raw || '--';
}

function eventKitChannelClass(channel) {
    const normalized = normalizeEventKitChannel(channel).toLowerCase();
    if (normalized === 'type' || normalized === 'enum' || normalized === 'string') return normalized;
    return 'unknown';
}

function formatEventKitKind(kind) {
    switch (String(kind).toLowerCase()) {
        case 'send': return t('eventkit.kind_send');
        case 'register': return t('eventkit.kind_register');
        case 'unregister': return t('eventkit.kind_unregister');
        default: return kind || 'event';
    }
}

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
