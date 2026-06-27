// pages/reskit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：ResKit
// ═══════════════════════════════════════════════════════════════════
const resKitState = {
    stats: null,
    resources: [],
    history: [],
    searchTerm: '',
    selectedKey: null,
    renderSignature: '',
};

function renderResKitPage() {
    $pageBody.classList.add('content-body--reskit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('reskit.title'),
        t('reskit.subtitle'),
        t('reskit.tab'),
        'res',
        `<button class="btn btn-sm" onclick="toggleResKitLoadLocationTracking()">${t('poolkit.toggle_location')}</button><button class="btn btn-sm" onclick="clearResKitHistory()">${t('poolkit.clear_history')}</button><button class="btn btn-primary btn-sm" onclick="refreshResKit()">${t('common.refresh')}</button>`
    );

    clearTabs();
    resKitState.renderSignature = '';
    loadResKitWorkbench();
}

async function refreshResKit() { loadResKitWorkbench(); }

async function refreshResKitReactive(event) {
    await loadResKitWorkbench();
}

function normalizeResKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const list = source.list ?? source;
    const history = source.history ?? {};
    return {
        stats: source.stats ?? {},
        resources: Array.isArray(source.resources) ? source.resources : (Array.isArray(list.resources) ? list.resources : []),
        history: Array.isArray(source.history) ? source.history : (Array.isArray(history.history) ? history.history : []),
    };
}

async function fetchResKitWorkbenchState() {
    return await fetchKitWorkbenchState('ResKit', normalizeResKitStatePayload);
}

async function loadResKitWorkbench() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('■', t('reskit.connect_hint'));
        clearMetrics();
        return;
    }
    try {
        const state = await fetchResKitWorkbenchState();
        resKitState.stats = state.stats;
        resKitState.resources = state.resources;
        resKitState.history = state.history;
        reconcileResKitSelection(resKitState.resources);

        clearMetrics();

        const html = renderResKitWorkbench(resKitState.stats, resKitState.resources, resKitState.history);
        const signature = makeStableSignature({
            stats: resKitState.stats,
            resources: resKitState.resources,
            history: resKitState.history,
            selected: resKitState.selectedKey
        });
        renderWorkbenchHtmlStable(resKitState, html, signature, bindResKitWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('ResKit')) {
            showRuntimeKitUnavailable('ResKit', t('reskit.resources'));
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function makeResKitKey(resource) {
    return `${resource?.path ?? ''}|${resource?.typeName ?? ''}`;
}

function reconcileResKitSelection(resources) {
    if (!Array.isArray(resources) || !resources.length) {
        resKitState.selectedKey = null;
        return null;
    }
    let selected = resources.find(item => makeResKitKey(item) === resKitState.selectedKey);
    if (!selected) {
        selected = resources[0];
        resKitState.selectedKey = makeResKitKey(selected);
    }
    return selected;
}

function renderResKitWorkbench(stats, resources, history) {
    const visibleResources = filterResKitResources(resources);
    const selected = resources.find(item => makeResKitKey(item) === resKitState.selectedKey) ?? null;
    return `<div class="kit-workbench kit-workbench--res">
        <div class="kit-workbench-grid kit-workbench-grid--res">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('res', t('reskit.loaded_resources'))}</div>
                        <div class="kit-panel__desc">${t('reskit.loaded_resources_desc')}</div>
                    </div>
                    <span class="kit-panel__count" data-reskit-visible-count>${escapeHtml(visibleResources.length)} / ${escapeHtml(resources.length)}</span>
                </div>
                <div class="kit-panel__tools">${renderKitSearchInput(resKitState.searchTerm, 'data-reskit-search', t('reskit.search_placeholder'))}</div>
                <div class="kit-resource-list" data-kit-scroll-key="res-list">${renderResKitRows(visibleResources)}</div>
            </section>
            ${renderResKitDetailSection(selected)}
            <section class="kit-panel kit-panel--events">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('log', t('reskit.unload_history'))}</div>
                        <div class="kit-panel__desc">${t('reskit.unload_history_desc')}</div>
                    </div>
                </div>
                ${renderResKitHistory(history)}
            </section>
        </div>
    </div>`;
}

function filterResKitResources(resources) {
    return (Array.isArray(resources) ? resources : []).filter(resource => kitSearchMatches(resKitState.searchTerm, [
        resource.path,
        resource.typeName,
        resource.providerName,
        resource.source,
        resource.sourceFile,
        resource.refCount,
    ]));
}

function renderResKitRows(resources) {
    if (!Array.isArray(resources) || !resources.length) {
        return emptyState('res', t('reskit.no_cache_hint'));
    }
    return resources.map(item => {
        const key = makeResKitKey(item);
        const selected = key === resKitState.selectedKey;
        return `<button class="kit-list-row${selected ? ' active' : ''}" type="button" data-reskit-resource="${escapeHtml(key)}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(item.path ?? '--')}</strong>
                <em>${escapeHtml(item.typeName ?? '--')} · ${escapeHtml(item.providerName ?? '--')}</em>
            </span>
            <span class="kit-list-row__stats">ref ${escapeHtml(item.refCount ?? 0)}</span>
        </button>`;
    }).join('');
}

function renderResKitDetail(resource) {
    if (!resource) {
        return emptyState('res', t('reskit.select_resource_hint'));
    }
    return `<div class="kit-detail-summary">
        <div><span>${t('reskit.label_type')}</span><strong>${escapeHtml(resource.typeName ?? '--')}</strong></div>
        <div><span>${t('reskit.label_ref')}</span><strong>${escapeHtml(resource.refCount ?? 0)}</strong></div>
        <div><span>${t('reskit.label_status')}</span><strong>${resource.isDone ? t('reskit.status_done') : t('reskit.status_loading')}</strong></div>
        <div><span>${t('reskit.label_source')}</span><strong>${escapeHtml(resource.source ?? '--')}</strong></div>
    </div>
    <div class="kit-code-path">${escapeHtml(resource.path ?? '--')}</div>
    ${resource.sourceFile ? `<div class="kit-code-action">${renderKitCodeJumpButton(resource.sourceFile, resource.sourceLine, t('reskit.load_location'), 'kit-code-action__jump')}</div>` : ''}
    <div class="kit-note">${resource.sourceFile ? t('reskit.load_location_note') : t('reskit.load_tracking_note')}</div>`;
}

function renderResKitDetailSection(resource) {
    return `<section class="kit-panel kit-panel--detail" data-reskit-detail-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('jump', t('reskit.detail_title'))}</div>
                <div class="kit-panel__desc">${escapeHtml(resource?.typeName ?? t('reskit.unselected'))}</div>
            </div>
        </div>
        ${renderResKitDetail(resource)}
    </section>`;
}

function renderResKitHistory(history) {
    if (!Array.isArray(history) || !history.length) {
        return emptyState('res', t('reskit.no_unload_history'));
    }
    return `<div class="kit-timeline" data-kit-scroll-key="res-history">${history.slice(0, 80).map(item => `<div class="kit-timeline-row">
        <span>${escapeHtml(item.typeName ?? '--')}</span>
        <strong>${escapeHtml(item.path ?? '--')}</strong>
        <em>${escapeHtml(item.unloadTimeUtc ?? '--')}</em>
    </div>`).join('')}</div>`;
}

function bindResKitWorkbenchActions() {
    $pageBody.querySelectorAll('[data-reskit-resource]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectResKitResource(button.dataset.reskitResource);
        });
    });
    bindResKitSearch();
    bindKitCodeJumpButtons();
}

function bindResKitSearch() {
    const input = $pageBody.querySelector('[data-reskit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            resKitState.searchTerm = input.value || '';
            updateResKitListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-reskit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            resKitState.searchTerm = '';
            updateResKitListDom();
            $pageBody.querySelector('[data-reskit-search]')?.focus();
        });
    }
}

function updateResKitListDom() {
    const visible = filterResKitResources(resKitState.resources);
    const list = $pageBody.querySelector('.kit-resource-list');
    if (list) list.innerHTML = renderResKitRows(visible);
    const count = $pageBody.querySelector('[data-reskit-visible-count]');
    if (count) count.textContent = `${visible.length} / ${resKitState.resources.length}`;
    const input = $pageBody.querySelector('[data-reskit-search]');
    if (input && input.value !== resKitState.searchTerm) input.value = resKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-reskit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !resKitState.searchTerm.trim());
    bindResKitWorkbenchActions();
}

function selectResKitResource(key) {
    if (!key) return;
    resKitState.selectedKey = key;
    $pageBody.querySelectorAll('[data-reskit-resource]').forEach(row => {
        row.classList.toggle('active', row.dataset.reskitResource === resKitState.selectedKey);
    });
    const selected = resKitState.resources.find(item => makeResKitKey(item) === resKitState.selectedKey) ?? null;
    const detailPanel = $pageBody.querySelector('[data-reskit-detail-panel]');
    if (detailPanel) detailPanel.outerHTML = renderResKitDetailSection(selected);
    bindResKitWorkbenchActions();
}

async function clearResKitHistory() {
    if (!invoke || !connected) return;
    await sendKitCommandData('ResKit', 'clear_history');
    await loadResKitWorkbench();
}

async function toggleResKitLoadLocationTracking() {
    if (!invoke || !connected) return;
    await sendKitCommandData('ResKit', 'set_tracking', {
        loadLocationTrackingEnabled: !resKitState.stats?.loadLocationTrackingEnabled,
    });
    await loadResKitWorkbench();
}

