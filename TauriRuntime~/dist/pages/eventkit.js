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

