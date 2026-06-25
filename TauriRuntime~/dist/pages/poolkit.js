// pages/poolkit.js
// 页面：PoolKit
const poolKitState = {
    stats: null,
    pools: [],
    details: [],
    detail: null,
    events: [],
    leaks: null,
    searchTerm: '',
    selectedPoolName: null,
    detailLoadSeq: 0,
    renderSignature: '',
};


function renderPoolKitPage() {
    $pageBody.classList.add('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('poolkit.title'),
        t('poolkit.subtitle'),
        t('poolkit.tab'),
        'pool',
        `<button class="btn btn-sm" onclick="togglePoolKitTracking()">${t('poolkit.toggle_tracking')}</button><button class="btn btn-sm" onclick="togglePoolKitStackLocation()">${t('poolkit.toggle_location')}</button><button class="btn btn-sm" onclick="runPoolKitLeakCheck()">${t('poolkit.leak_check')}</button><button class="btn btn-sm" onclick="clearPoolKitHistory()">${t('poolkit.clear_history')}</button><button class="btn btn-primary btn-sm" onclick="refreshPoolKit()">${t('poolkit.refresh')}</button>`
    );

    clearTabs();
    poolKitState.renderSignature = '';
    loadPoolWorkbench();
}

async function refreshPoolKit() { loadPoolWorkbench({ forceDetailRefresh: true, forceCommandRefresh: true }); }

async function refreshPoolKitReactive(event) {
    await loadPoolWorkbench();
}

function normalizePoolKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const list = source.list ?? source;
    const events = source.events ?? {};
    const details = source.details ?? {};
    return {
        stats: source.stats ?? {},
        pools: Array.isArray(source.pools) ? source.pools : (Array.isArray(list.pools) ? list.pools : []),
        details: Array.isArray(source.details) ? source.details : (Array.isArray(details.pools) ? details.pools : []),
        events: Array.isArray(source.events) ? source.events : (Array.isArray(events.events) ? events.events : []),
        leaks: source.leaks ?? null,
    };
}

async function fetchPoolKitWorkbenchState({ forceCommandRefresh = false } = {}) {
    if (forceCommandRefresh) {
        return await fetchPoolKitWorkbenchStateFromCommands();
    }

    const telemetry = await readKitTelemetryData('PoolKit');
    if (telemetry) return normalizePoolKitStatePayload(telemetry);

    const snapshot = await readKitSnapshotData('PoolKit');
    if (snapshot) return normalizePoolKitStatePayload(snapshot);

    return null;
}

function applyPoolKitWorkbenchState(state) {
    poolKitState.stats = state.stats ?? {};
    poolKitState.pools = state.pools ?? [];
    poolKitState.details = state.details ?? [];
    poolKitState.events = state.events ?? [];
    poolKitState.leaks = state.leaks ?? poolKitState.leaks;

    const selectedPool = reconcilePoolKitSelection(poolKitState.pools);
    if (!selectedPool) {
        poolKitState.detail = null;
        return null;
    }

    const snapshotDetail = findPoolKitSnapshotDetail(selectedPool.name);
    if (snapshotDetail) poolKitState.detail = snapshotDetail;
    else if (poolKitState.detail?.name !== selectedPool.name) poolKitState.detail = null;

    return selectedPool;
}

async function loadPoolWorkbench({ forceDetailRefresh = false, forceCommandRefresh = false } = {}) {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('●', t('poolkit.need_unity'));
        clearMetrics();
        return;
    }
    try {
        const snapshotState = await fetchPoolKitWorkbenchState({ forceCommandRefresh });
        const state = snapshotState ?? await fetchPoolKitWorkbenchStateFromCommands();
        const selectedPool = applyPoolKitWorkbenchState(state);

        clearMetrics();

        const html = renderPoolKitWorkbench(poolKitState.stats, poolKitState.pools, poolKitState.detail, poolKitState.events, poolKitState.leaks);
        const signature = makeStableSignature({
            stats: poolKitState.stats,
            pools: poolKitState.pools,
            details: poolKitState.details,
            detail: poolKitState.detail,
            events: poolKitState.events,
            leaks: poolKitState.leaks,
            selected: poolKitState.selectedPoolName
        });
        renderWorkbenchHtmlStable(poolKitState, html, signature, bindPoolKitWorkbenchActions);

        if (selectedPool && (forceDetailRefresh || !poolKitState.detail)) {
            refreshPoolKitSelectionDetail(selectedPool.name, { force: true }).catch(() => {});
        }
    } catch (e) {
        if (!canSendRuntimeKitCommand('PoolKit')) {
            showRuntimeKitUnavailable('PoolKit', t('poolkit.title'));
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

async function fetchPoolKitWorkbenchStateFromCommands() {
    const snapshot = await sendKitCommandData('PoolKit', 'get_workbench_snapshot');
    return normalizePoolKitStatePayload(snapshot);
}

function findPoolKitSnapshotDetail(poolName) {
    if (!poolName || !Array.isArray(poolKitState.details)) return null;
    return poolKitState.details.find(detail => detail?.name === poolName) ?? null;
}

function reconcilePoolKitSelection(pools) {
    if (!Array.isArray(pools) || !pools.length) {
        poolKitState.selectedPoolName = null;
        return null;
    }
    let selected = pools.find(p => p.name === poolKitState.selectedPoolName);
    if (!selected) {
        selected = pools[0];
        poolKitState.selectedPoolName = selected.name;
    }
    return selected;
}

function renderPoolKitWorkbench(stats, pools, detail, events, leaks) {
    const visiblePools = filterPoolKitPools(pools);
    return `<div class="kit-workbench kit-workbench--pool">
        <div class="kit-workbench-grid kit-workbench-grid--pool">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('pool', t('poolkit.object_pool'))}</div>
                        <div class="kit-panel__desc">${t('poolkit.pool_desc')}</div>
                    </div>
                    <span class="kit-panel__count" data-poolkit-visible-count>${escapeHtml(visiblePools.length)} / ${escapeHtml(pools.length)}</span>
                </div>
                <div class="kit-panel__tools">${renderKitSearchInput(poolKitState.searchTerm, 'data-poolkit-search', t('poolkit.search_placeholder'))}</div>
                <div class="kit-pool-list" data-kit-scroll-key="pool-list">${renderPoolRows(visiblePools, events)}</div>
            </section>
            ${renderPoolDetailSection(detail)}
            ${renderPoolEventsSection(events, leaks)}
        </div>
    </div>`;
}

function filterPoolKitPools(pools) {
    return (Array.isArray(pools) ? pools : []).filter(pool => kitSearchMatches(poolKitState.searchTerm, [
        pool.name,
        pool.typeName,
        pool.healthStatus,
        pool.activeCount,
        pool.totalCount,
    ]));
}

function renderPoolRows(pools, events = []) {
    if (!Array.isArray(pools) || !pools.length) {
        return emptyState('pool', t('poolkit.no_pools'));
    }
    const recentEventsByPool = getPoolKitRecentEventByPool(events);
    return pools.map(pool => {
        const selected = pool.name === poolKitState.selectedPoolName;
        const activeCount = poolNumber(pool.activeCount);
        const totalCount = poolNumber(pool.totalCount);
        const inactiveCount = poolNumber(pool.inactiveCount, Math.max(0, totalCount - activeCount));
        const peakCount = poolNumber(pool.peakCount);
        const recentEvent = recentEventsByPool.get(pool.name);
        const eventClass = poolKitRecentEventClass(recentEvent);
        const eventText = formatPoolKitRecentEvent(recentEvent);
        return `<button class="kit-list-row kit-list-row--pool${selected ? ' active' : ''}${eventClass}" type="button" data-poolkit-pool="${escapeHtml(pool.name)}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(pool.name ?? '--')}</strong>
                <em>${escapeHtml(pool.typeName ?? '--')}</em>
            </span>
            <span class="kit-list-row__stats kit-pool-row-stats">
                <span>${t('poolkit.in_use')} <strong>${escapeHtml(activeCount)}</strong></span>
                <span>${t('poolkit.pooled')} <strong>${escapeHtml(inactiveCount)}</strong></span>
                <span>${t('poolkit.total')} <strong>${escapeHtml(totalCount)}</strong></span>
            </span>
            <span class="kit-list-row__meta">${t('poolkit.peak')} ${escapeHtml(peakCount)} · ${t('poolkit.upper_limit')} ${escapeHtml(formatPoolLimit(pool.maxCacheCount))}${eventText ? ` · <span class="kit-pool-event-chip">${escapeHtml(eventText)}</span>` : ''}</span>
            ${renderPoolUsageBar(activeCount, inactiveCount, totalCount)}
        </button>`;
    }).join('');
}

function getPoolKitRecentEventByPool(events) {
    const map = new Map();
    if (!Array.isArray(events)) return map;
    events.forEach(evt => {
        const poolName = evt?.poolName;
        if (!poolName || map.has(poolName)) return;
        map.set(poolName, evt);
    });
    return map;
}

function poolKitRecentEventClass(evt) {
    const type = String(evt?.eventType ?? '').toLowerCase();
    if (type === 'spawn') return ' kit-list-row--pool-spawn';
    if (type === 'return' || type === 'forced') return ' kit-list-row--pool-return';
    return '';
}

function formatPoolKitRecentEvent(evt) {
    const type = String(evt?.eventType ?? '').toLowerCase();
    if (type === 'spawn') return t('poolkit.just_spawned');
    if (type === 'return') return t('poolkit.just_returned');
    if (type === 'forced') return t('poolkit.forced_return');
    return '';
}

function renderPoolUsageBar(activeCount, inactiveCount, totalCount, extraClass = '') {
    const activeWidth = percentText(totalCount > 0 ? activeCount / totalCount : 0);
    const inactiveWidth = percentText(totalCount > 0 ? inactiveCount / totalCount : 0);
    return `<span class="kit-usage kit-usage--split ${escapeHtml(extraClass)}" title="${t('poolkit.borrowed')} ${escapeHtml(activeCount)} / ${t('poolkit.pooled_objects')} ${escapeHtml(inactiveCount)} / ${t('poolkit.capacity')} ${escapeHtml(totalCount)}">
        <span class="kit-usage__active" style="width:${activeWidth}"></span>
        <span class="kit-usage__idle" style="width:${inactiveWidth}"></span>
    </span>`;
}

function poolNumber(value, fallback = 0) {
    const number = Number(value);
    return Number.isFinite(number) ? number : fallback;
}

function formatPoolLimit(value) {
    const number = Number(value);
    return Number.isFinite(number) && number >= 0 ? String(number) : t('poolkit.unlimited');
}

function renderPoolObjectRows(objects, emptyText, mode) {
    if (!Array.isArray(objects) || !objects.length) {
        return emptyState('pool', emptyText);
    }

    return objects.map(obj => {
        const timeText = mode === 'active'
            ? `${escapeHtml(obj.spawnTime ?? '--')}s`
            : t('poolkit.available');
        return `<div class="kit-mini-row kit-mini-row--pool-object">
            <span class="kit-mini-row__name">${escapeHtml(obj.objectName ?? '--')}</span>
            <em>${timeText}</em>
            ${mode === 'active' ? renderKitCodeJumpButton(obj.sourceFile, obj.sourceLine, t('poolkit.location'), 'kit-mini-row__jump') : ''}
        </div>`;
    }).join('');
}

function renderPoolObjectSection(title, count, truncated, objects, emptyText, mode, scrollKey) {
    return `<section class="kit-pool-object-section">
        <div class="kit-pool-object-section__head">
            <strong>${escapeHtml(title)}</strong>
            <span>${escapeHtml(count ?? 0)}${truncated ? '+' : ''}</span>
        </div>
        ${truncated ? `<div class="kit-note kit-note--compact">${t('poolkit.too_many_objects')}</div>` : ''}
        <div class="kit-mini-list kit-mini-list--pool-objects" data-kit-scroll-key="${escapeHtml(scrollKey)}">
            ${renderPoolObjectRows(objects, emptyText, mode)}
        </div>
    </section>`;
}

function renderPoolDetail(detail) {
    if (!detail) {
        return emptyState('pool', t('poolkit.select_pool_hint'));
    }
    const activeObjects = detail.activeObjects ?? [];
    const inactiveObjects = detail.inactiveObjects ?? [];
    const activeCount = poolNumber(detail.activeCount);
    const totalCount = poolNumber(detail.totalCount);
    const inactiveCount = poolNumber(detail.inactiveCount, Math.max(0, totalCount - activeCount));
    const peakCount = poolNumber(detail.peakCount);
    const usage = totalCount > 0 ? activeCount / totalCount : 0;
    return `<div class="kit-pressure-card">
        <div class="kit-pressure-card__head">
            <span>${svgIcon('pressure', 'shell-icon')}${t('poolkit.pool_pressure')}</span>
            <strong>${escapeHtml(percentText(usage))}</strong>
        </div>
        ${renderPoolUsageBar(activeCount, inactiveCount, totalCount, 'kit-usage--large')}
        <div class="kit-pressure-card__meta">
            <span>${t('poolkit.in_use')} ${escapeHtml(activeCount)}</span>
            <span>${t('poolkit.pooled_objects')} ${escapeHtml(inactiveCount)}</span>
            <span>${t('poolkit.capacity')} ${escapeHtml(totalCount)}</span>
            <span>${t('poolkit.peak')} ${escapeHtml(peakCount)}</span>
            <span>${t('poolkit.upper_limit')} ${escapeHtml(formatPoolLimit(detail.maxCacheCount))}</span>
        </div>
    </div>
    <div class="kit-detail-summary kit-detail-summary--pool">
        <div><span>${t('poolkit.in_use')}</span><strong>${escapeHtml(activeCount)}</strong></div>
        <div><span>${t('poolkit.pooled_objects')}</span><strong>${escapeHtml(inactiveCount)}</strong></div>
        <div><span>${t('poolkit.capacity')}</span><strong>${escapeHtml(totalCount)}</strong></div>
        <div><span>${t('poolkit.peak')}</span><strong>${escapeHtml(peakCount)}</strong></div>
        <div><span>${t('poolkit.upper_limit')}</span><strong>${escapeHtml(formatPoolLimit(detail.maxCacheCount))}</strong></div>
    </div>
    <div class="kit-pool-object-sections">
        ${renderPoolObjectSection(t('poolkit.borrowed_objects'), detail.activeObjectTotal ?? activeObjects.length, detail.activeObjectTruncated, activeObjects, t('poolkit.no_borrowed'), 'active', 'pool-active-objects')}
        ${renderPoolObjectSection(t('poolkit.pooled_objects_list'), detail.inactiveObjectTotal ?? inactiveObjects.length, detail.inactiveObjectTruncated, inactiveObjects, t('poolkit.no_pooled'), 'inactive', 'pool-inactive-objects')}
    </div>`;
}

function renderPoolDetailSection(detail) {
    const desc = detail
        ? `${t('poolkit.in_use')} ${poolNumber(detail.activeCount)} · ${t('poolkit.pooled_objects')} ${poolNumber(detail.inactiveCount, Math.max(0, poolNumber(detail.totalCount) - poolNumber(detail.activeCount)))} · ${t('poolkit.upper_limit')} ${formatPoolLimit(detail.maxCacheCount)}`
        : (poolKitState.selectedPoolName ?? t('common.no_selection'));
    return `<section class="kit-panel kit-panel--detail" data-poolkit-detail-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('pressure', t('poolkit.current_pool'))}</div>
                <div class="kit-panel__desc">${escapeHtml(detail?.name ?? poolKitState.selectedPoolName ?? t('common.no_selection'))} · ${escapeHtml(desc)}</div>
            </div>
        </div>
        ${renderPoolDetail(detail)}
    </section>`;
}

function renderPoolEventsSection(events, leaks) {
    return `<section class="kit-panel kit-panel--events" data-poolkit-events-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('log', t('poolkit.event_stream'))}</div>
                <div class="kit-panel__desc">${t('poolkit.event_stream_desc')}</div>
            </div>
        </div>
        ${renderPoolEvents(events, leaks)}
    </section>`;
}

function renderPoolEvents(events, leaks) {
    const leakRows = leaks?.suspectedLeaks ?? [];
    const leakHtml = leakRows.length ? `<div class="kit-alert-row">${t('poolkit.leak_warning', leakRows.length)}</div>` : '';
    const eventHtml = Array.isArray(events) && events.length
        ? events.slice(0, 80).map(evt => `<div class="kit-timeline-row">
            <span>${escapeHtml(evt.eventType ?? '--')}</span>
            <strong>${escapeHtml(evt.poolName ?? '--')}</strong>
            <em>${escapeHtml(evt.objectName ?? '--')}</em>
            ${evt.sourceFile ? `<button class="kit-icon-button kit-icon-button--inline" data-kit-open-code="${escapeHtml(evt.sourceFile)}" data-kit-open-line="${escapeHtml(evt.sourceLine ?? 1)}" title="${t('poolkit.open_location')}">${svgIcon('jump', 'shell-icon')}</button>` : ''}
        </div>`).join('')
        : emptyState('pool', t('poolkit.no_event_history'));
    return leakHtml + `<div class="kit-timeline" data-kit-scroll-key="pool-events">${eventHtml}</div>`;
}

function bindPoolKitWorkbenchActions() {
    $pageBody.querySelectorAll('[data-poolkit-pool]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectPoolKitPool(button.dataset.poolkitPool);
        });
    });
    bindPoolKitSearch();
    bindKitCodeJumpButtons();
}

function bindPoolKitSearch() {
    const input = $pageBody.querySelector('[data-poolkit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            poolKitState.searchTerm = input.value || '';
            updatePoolKitListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-poolkit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            poolKitState.searchTerm = '';
            updatePoolKitListDom();
            $pageBody.querySelector('[data-poolkit-search]')?.focus();
        });
    }
}

function updatePoolKitListDom() {
    const visible = filterPoolKitPools(poolKitState.pools);
    const list = $pageBody.querySelector('.kit-pool-list');
    if (list) list.innerHTML = renderPoolRows(visible, poolKitState.events);
    const count = $pageBody.querySelector('[data-poolkit-visible-count]');
    if (count) count.textContent = `${visible.length} / ${poolKitState.pools.length}`;
    const input = $pageBody.querySelector('[data-poolkit-search]');
    if (input && input.value !== poolKitState.searchTerm) input.value = poolKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-poolkit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !poolKitState.searchTerm.trim());
    bindPoolKitWorkbenchActions();
}

function updatePoolKitSelectionDom() {
    $pageBody.querySelectorAll('[data-poolkit-pool]').forEach(row => {
        row.classList.toggle('active', row.dataset.poolkitPool === poolKitState.selectedPoolName);
    });
    const detailPanel = $pageBody.querySelector('[data-poolkit-detail-panel]');
    if (detailPanel) detailPanel.outerHTML = renderPoolDetailSection(poolKitState.detail);
    const eventsPanel = $pageBody.querySelector('[data-poolkit-events-panel]');
    if (eventsPanel) eventsPanel.outerHTML = renderPoolEventsSection(poolKitState.events, poolKitState.leaks);
    bindPoolKitWorkbenchActions();
}

async function selectPoolKitPool(poolName) {
    if (!poolName || poolKitState.selectedPoolName === poolName) return;
    poolKitState.selectedPoolName = poolName;
    poolKitState.detail = findPoolKitSnapshotDetail(poolName);
    updatePoolKitSelectionDom();
    if (!poolKitState.detail) {
        await refreshPoolKitSelectionDetail(poolName, { force: true });
    }
}

async function refreshPoolKitSelectionDetail(poolName, { force = false } = {}) {
    if (!invoke || !connected || !poolName) return;
    if (!force && poolKitState.detail?.name === poolName) return;

    try {
        const requestSeq = ++poolKitState.detailLoadSeq;
        const [detail, events] = await Promise.all([
            sendKitCommandData('PoolKit', 'get_pool_detail', { poolName }),
            sendKitCommandData('PoolKit', 'get_event_history', { poolName }),
        ]);
        if (requestSeq !== poolKitState.detailLoadSeq || poolKitState.selectedPoolName !== poolName) return;
        poolKitState.detail = detail;
        poolKitState.events = events.events ?? [];
        updatePoolKitSelectionDom();
    } catch (e) {
        addLog(t('poolkit.detail_read_failed', e), 'error');
    }
}

async function togglePoolKitTracking() {
    if (!invoke || !connected) return;
    const current = !!poolKitState.stats?.trackingEnabled;
    const nextTracking = !current;
    await sendKitCommandData('PoolKit', 'set_tracking', {
        trackingEnabled: nextTracking,
        eventHistoryEnabled: nextTracking,
        stackTraceEnabled: nextTracking ? !!poolKitState.stats?.stackTraceEnabled : false,
    });
    await loadPoolWorkbench({ forceCommandRefresh: true, forceDetailRefresh: true });
}

async function togglePoolKitStackLocation() {
    if (!invoke || !connected) return;
    const nextStack = !poolKitState.stats?.stackTraceEnabled;
    await sendKitCommandData('PoolKit', 'set_tracking', {
        trackingEnabled: nextStack ? true : !!poolKitState.stats?.trackingEnabled,
        eventHistoryEnabled: nextStack ? true : !!poolKitState.stats?.eventHistoryEnabled,
        stackTraceEnabled: nextStack,
    });
    await loadPoolWorkbench({ forceCommandRefresh: true, forceDetailRefresh: true });
}

async function clearPoolKitHistory() {
    if (!invoke || !connected) return;
    await sendKitCommandData('PoolKit', 'clear_history');
    poolKitState.events = [];
    await loadPoolWorkbench({ forceCommandRefresh: true });
}

async function runPoolKitLeakCheck() {
    if (!invoke || !connected) return;
    poolKitState.leaks = await sendKitCommandData('PoolKit', 'check_leak');
    const stats = poolKitState.stats ?? {};
    const detail = poolKitState.selectedPoolName
        ? await sendKitCommandData('PoolKit', 'get_pool_detail', { poolName: poolKitState.selectedPoolName }).catch(() => null)
        : null;
    poolKitState.detail = detail;
    updatePoolKitSelectionDom();
}

async function loadPoolOverview() { await loadPoolWorkbench(); }
async function loadPoolList() { await loadPoolWorkbench(); }
async function loadLeakCheck() {
    await runPoolKitLeakCheck();
}

