// pages/singletonkit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：SingletonKit
// ═══════════════════════════════════════════════════════════════════
const singletonKitState = {
    stats: {},
    singletons: [],
    searchTerm: '',
    selectedType: null,
    renderSignature: '',
};

function renderSingletonKitPage() {
    $pageBody.classList.add('content-body--singletonkit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    setHero(
        t('singletonkit.title'),
        t('singletonkit.subtitle'),
        t('singletonkit.tab'),
        'singleton',
        `<button class="btn btn-primary btn-sm" onclick="refreshSingletonKit()">${t('common.refresh')}</button>`
    );
    clearTabs();
    singletonKitState.renderSignature = '';
    loadSingletonKitWorkbench();
}

async function refreshSingletonKit() { loadSingletonKitWorkbench(); }

async function refreshSingletonKitReactive(event) {
    await loadSingletonKitWorkbench();
}

function normalizeSingletonKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const list = source.list ?? source;
    return {
        stats: source.stats ?? {},
        singletons: Array.isArray(source.singletons) ? source.singletons : (Array.isArray(list.singletons) ? list.singletons : []),
    };
}

async function fetchSingletonKitWorkbenchState() {
    return await fetchKitWorkbenchState('SingletonKit', normalizeSingletonKitStatePayload);
}

async function loadSingletonKitWorkbench() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('singleton', t('singletonkit.connect_hint'));
        clearMetrics();
        return;
    }
    try {
        const state = await fetchSingletonKitWorkbenchState();
        singletonKitState.stats = state.stats;
        singletonKitState.singletons = state.singletons;
        reconcileSingletonKitSelection(singletonKitState.singletons);
        clearMetrics();

        const html = renderSingletonKitWorkbench(singletonKitState.singletons);
        const signature = makeStableSignature({ singletons: singletonKitState.singletons, selected: singletonKitState.selectedType });
        renderWorkbenchHtmlStable(singletonKitState, html, signature, bindSingletonKitWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('SingletonKit')) {
            showRuntimeKitUnavailable('SingletonKit', t('singletonkit.singleton_label'));
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function reconcileSingletonKitSelection(singletons) {
    if (!Array.isArray(singletons) || !singletons.length) {
        singletonKitState.selectedType = null;
        return null;
    }
    let selected = singletons.find(item => item.fullName === singletonKitState.selectedType);
    if (!selected) {
        selected = singletons[0];
        singletonKitState.selectedType = selected.fullName;
    }
    return selected;
}

function renderSingletonKitWorkbench(singletons) {
    const visibleSingletons = filterSingletonKitRows(singletons);
    const selected = singletons.find(item => item.fullName === singletonKitState.selectedType) ?? null;
    return `<div class="kit-workbench kit-workbench--singleton">
        <div class="kit-workbench-grid kit-workbench-grid--singleton">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('singleton', t('singletonkit.singleton_list'))}</div>
                        <div class="kit-panel__desc">${t('singletonkit.backend_desc')}</div>
                    </div>
                    <span class="kit-panel__count" data-singletonkit-visible-count>${escapeHtml(visibleSingletons.length)} / ${escapeHtml(singletons.length)}</span>
                </div>
                <div class="kit-panel__tools">${renderKitSearchInput(singletonKitState.searchTerm, 'data-singletonkit-search', t('singletonkit.search_placeholder'))}</div>
                <div class="kit-resource-list" data-kit-scroll-key="singleton-list">${renderSingletonRows(visibleSingletons)}</div>
            </section>
            ${renderSingletonDetailSection(selected)}
        </div>
    </div>`;
}

function filterSingletonKitRows(singletons) {
    return (Array.isArray(singletons) ? singletons : []).filter(item => kitSearchMatches(singletonKitState.searchTerm, [
        item.typeName,
        item.fullName,
        item.backend,
        item.source,
        item.isAlive ? 'alive' : 'disposed',
    ]));
}

function renderSingletonRows(singletons) {
    if (!Array.isArray(singletons) || !singletons.length) {
        return emptyState('singleton', t('singletonkit.no_singletons_hint'));
    }
    return singletons.map(item => {
        const selected = item.fullName === singletonKitState.selectedType;
        return `<button class="kit-list-row${selected ? ' active' : ''}" type="button" data-singleton-type="${escapeHtml(item.fullName ?? '')}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(item.typeName ?? '--')}</strong>
                <em>${escapeHtml(item.backend ?? 'Base')} · ${escapeHtml(item.source ?? '--')}</em>
            </span>
            <span class="kit-state-pill ${item.isAlive ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${item.isAlive ? 'Alive' : 'Disposed'}</span>
        </button>`;
    }).join('');
}

function renderSingletonDetail(item) {
    if (!item) {
        return emptyState('singleton', t('singletonkit.select_hint'));
    }
    return `<div class="kit-detail-summary kit-detail-summary--singleton">
        <div><span>${t('singletonkit.backend')}</span><strong>${escapeHtml(item.backend ?? 'Base')}</strong></div>
        <div><span>${t('common.source')}</span><strong>${escapeHtml(item.source ?? '--')}</strong></div>
        <div><span>${t("common.status")}</span><strong>${item.isAlive ? 'Alive' : 'Disposed'}</strong></div>
        <div><span>Hash</span><strong>${escapeHtml(item.instanceHash ?? 0)}</strong></div>
    </div>
    <div class="kit-code-path">${escapeHtml(item.fullName ?? item.typeName ?? '--')}</div>
    <div class="kit-note">${t('singletonkit.backend_note')}</div>`;
}

function renderSingletonDetailSection(item) {
    return `<section class="kit-panel kit-panel--detail" data-singletonkit-detail-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', t('singletonkit.lifecycle_detail'))}</div>
                <div class="kit-panel__desc">${escapeHtml(item?.typeName ?? t('common.no_selection'))}</div>
            </div>
        </div>
        ${renderSingletonDetail(item)}
    </section>`;
}

function bindSingletonKitWorkbenchActions() {
    $pageBody.querySelectorAll('[data-singleton-type]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectSingletonKitRow(button.dataset.singletonType);
        });
    });
    bindSingletonKitSearch();
}

function bindSingletonKitSearch() {
    const input = $pageBody.querySelector('[data-singletonkit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            singletonKitState.searchTerm = input.value || '';
            updateSingletonKitListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-singletonkit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            singletonKitState.searchTerm = '';
            updateSingletonKitListDom();
            $pageBody.querySelector('[data-singletonkit-search]')?.focus();
        });
    }
}

function updateSingletonKitListDom() {
    const visible = filterSingletonKitRows(singletonKitState.singletons);
    const list = $pageBody.querySelector('.kit-resource-list');
    if (list) list.innerHTML = renderSingletonRows(visible);
    const count = $pageBody.querySelector('[data-singletonkit-visible-count]');
    if (count) count.textContent = `${visible.length} / ${singletonKitState.singletons.length}`;
    const input = $pageBody.querySelector('[data-singletonkit-search]');
    if (input && input.value !== singletonKitState.searchTerm) input.value = singletonKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-singletonkit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !singletonKitState.searchTerm.trim());
    bindSingletonKitWorkbenchActions();
}

function selectSingletonKitRow(typeName) {
    if (!typeName) return;
    singletonKitState.selectedType = typeName;
    $pageBody.querySelectorAll('[data-singleton-type]').forEach(row => {
        row.classList.toggle('active', row.dataset.singletonType === singletonKitState.selectedType);
    });
    const selected = singletonKitState.singletons.find(item => item.fullName === singletonKitState.selectedType) ?? null;
    const detailPanel = $pageBody.querySelector('[data-singletonkit-detail-panel]');
    if (detailPanel) detailPanel.outerHTML = renderSingletonDetailSection(selected);
    bindSingletonKitWorkbenchActions();
}

