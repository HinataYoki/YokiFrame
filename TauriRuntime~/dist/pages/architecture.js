// pages/architecture.js
// ═══════════════════════════════════════════════════════════════════
// 页面：Architecture
// ═══════════════════════════════════════════════════════════════════
const architectureKitState = {
    stats: {},
    architectures: [],
    searchTerm: '',
    selectedType: null,
    renderSignature: '',
};

function renderArchitecturePage() {
    $pageBody.classList.add('content-body--architecture');
    setHero(
        t('architecture.title'),
        t('architecture.subtitle'),
        t('architecture.tab'),
        'architecture',
        `<button class="btn btn-primary btn-sm" onclick="refreshArchitecture()">${t('common.refresh')}</button>`
    );
    clearTabs();
    architectureKitState.renderSignature = '';
    loadArchitectureWorkbench();
}

async function refreshArchitecture() { await loadArchitectureWorkbench(); }

async function refreshArchitectureReactive(event) {
    await loadArchitectureWorkbench();
}

function normalizeArchitectureStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const list = source.list ?? source;
    return {
        stats: source.stats ?? {},
        architectures: Array.isArray(source.architectures)
            ? source.architectures
            : (Array.isArray(list.architectures) ? list.architectures : []),
    };
}

async function fetchArchitectureWorkbenchStateFromCommands() {
    const snapshot = await sendKitCommandData('Architecture', 'get_workbench_snapshot');
    return normalizeArchitectureStatePayload(snapshot);
}

async function loadArchitectureWorkbench() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('architecture', t('architecture.connect_hint'));
        clearMetrics();
        return;
    }

    if (!canSendRuntimeKitCommand('Architecture')) {
        showRuntimeKitUnavailable('Architecture', t('architecture.instance_label'));
        return;
    }

    try {
        const state = await fetchArchitectureWorkbenchStateFromCommands();
        architectureKitState.stats = state.stats;
        architectureKitState.architectures = state.architectures;
        reconcileArchitectureSelection(architectureKitState.architectures);
        clearMetrics();

        const html = renderArchitectureWorkbench(architectureKitState.architectures);
        const signature = makeStableSignature({
            stats: architectureKitState.stats,
            architectures: architectureKitState.architectures,
            selected: architectureKitState.selectedType,
        });
        renderWorkbenchHtmlStable(architectureKitState, html, signature, bindArchitectureWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('Architecture')) {
            showRuntimeKitUnavailable('Architecture', t('architecture.instance_label'));
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function reconcileArchitectureSelection(architectures) {
    if (!Array.isArray(architectures) || !architectures.length) {
        architectureKitState.selectedType = null;
        return null;
    }
    let selected = architectures.find(item => item.fullName === architectureKitState.selectedType);
    if (!selected) {
        selected = architectures[0];
        architectureKitState.selectedType = selected.fullName;
    }
    return selected;
}

function renderArchitectureWorkbench(architectures) {
    const visibleArchitectures = filterArchitectureRows(architectures);
    const selected = architectures.find(item => item.fullName === architectureKitState.selectedType) ?? null;
    const architectureCount = architectureKitState.stats?.architectureCount ?? architectures.length;
    const aliveCount = architectureKitState.stats?.aliveCount ?? architectures.filter(item => item.isAlive).length;
    const serviceCount = architectureKitState.stats?.serviceCount ?? getArchitectureTotalServiceCount(architectures);
    return `<div class="kit-workbench kit-workbench--architecture">
        <section class="kit-toolbar">
            <div>
                <div class="kit-toolbar__title">${renderKitTitle('architecture', t('architecture.registry'))}</div>
                <div class="kit-toolbar__meta">${t('architecture.meta', escapeHtml(architectureCount), escapeHtml(aliveCount), escapeHtml(serviceCount))}</div>
            </div>
        </section>
        <div class="kit-workbench-grid kit-workbench-grid--architecture">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('architecture', t('architecture.instance_list'))}</div>
                        <div class="kit-panel__desc">${t('architecture.instance_desc')}</div>
                    </div>
                    <span class="kit-panel__count" data-architecture-visible-count>${escapeHtml(visibleArchitectures.length)} / ${escapeHtml(architectures.length)}</span>
                </div>
                <div class="kit-panel__tools">${renderKitSearchInput(architectureKitState.searchTerm, 'data-architecture-search', t('architecture.search_placeholder'))}</div>
                <div class="kit-resource-list" data-kit-scroll-key="architecture-list">${renderArchitectureRows(visibleArchitectures)}</div>
            </section>
            ${renderArchitectureDetailSection(selected)}
        </div>
    </div>`;
}

function getArchitectureTotalServiceCount(architectures) {
    if (!Array.isArray(architectures)) return 0;
    let count = 0;
    for (let i = 0; i < architectures.length; i++) {
        count += getArchitectureServiceCount(architectures[i]);
    }
    return count;
}

function getArchitectureServiceCount(item) {
    if (!item) return 0;
    if (Array.isArray(item.services)) return item.services.length;
    const count = Number(item.serviceCount);
    return Number.isFinite(count) ? count : 0;
}

function filterArchitectureRows(architectures) {
    return (Array.isArray(architectures) ? architectures : []).filter(item => {
        const values = [
            item.typeName,
            item.fullName,
            item.isAlive ? 'alive' : 'disposed',
            item.initialized ? 'initialized' : 'not initialized',
        ];
        const services = Array.isArray(item.services) ? item.services : [];
        services.forEach(service => {
            values.push(service.typeName, service.fullName, service.implementationTypeName, service.implementationFullName);
        });
        return kitSearchMatches(architectureKitState.searchTerm, values);
    });
}

function renderArchitectureRows(architectures) {
    if (!Array.isArray(architectures) || !architectures.length) {
        return emptyState('architecture', t('architecture.no_instances_hint'));
    }
    return architectures.map(item => {
        const selected = item.fullName === architectureKitState.selectedType;
        const serviceCount = getArchitectureServiceCount(item);
        return `<button class="kit-list-row kit-list-row--architecture${selected ? ' active' : ''}" type="button" data-architecture-type="${escapeHtml(item.fullName ?? '')}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(item.typeName ?? '--')}</strong>
                <em>${escapeHtml(item.fullName ?? '--')}</em>
            </span>
            <span class="kit-list-row__stats">${escapeHtml(serviceCount)} ${t('architecture.services')}</span>
            <span class="kit-state-pill ${item.isAlive ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${item.isAlive ? 'Alive' : 'Disposed'}</span>
        </button>`;
    }).join('');
}

function renderArchitectureDetail(item) {
    if (!item) {
        return emptyState('architecture', t('architecture.select_hint'));
    }

    const services = Array.isArray(item.services) ? item.services : [];
    return `<div class="kit-detail-summary kit-detail-summary--architecture">
        <div><span>${t("common.status")}</span><strong>${item.isAlive ? 'Alive' : 'Disposed'}</strong></div>
        <div><span>${t('architecture.initialized')}</span><strong>${item.initialized ? 'Ready' : 'Pending'}</strong></div>
        <div><span>${t('architecture.services')}</span><strong>${escapeHtml(getArchitectureServiceCount(item))}</strong></div>
        <div><span>Hash</span><strong>${escapeHtml(item.instanceHash ?? 0)}</strong></div>
    </div>
    <div class="kit-code-path">${escapeHtml(item.fullName ?? item.typeName ?? '--')}</div>
    <div class="kit-mini-list architecture-service-list">
        ${renderArchitectureServiceRows(services)}
    </div>`;
}

function renderArchitectureServiceRows(services) {
    if (!Array.isArray(services) || !services.length) {
        return emptyState('architecture', t('architecture.no_services'));
    }
    return services.map(service => {
        const implementation = service.implementationTypeName && service.implementationTypeName !== service.typeName
            ? service.implementationTypeName
            : service.implementationFullName;
        return `<div class="kit-mini-row kit-mini-row--architecture-service">
            <span class="kit-mini-row__name" title="${escapeHtml(service.fullName ?? service.typeName ?? '--')}">${escapeHtml(service.typeName ?? '--')}</span>
            <em title="${escapeHtml(service.implementationFullName ?? implementation ?? '--')}">${escapeHtml(implementation ?? '--')}</em>
            <span class="kit-state-pill ${service.initialized ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${service.initialized ? 'Ready' : 'Pending'}</span>
        </div>`;
    }).join('');
}

function renderArchitectureDetailSection(item) {
    return `<section class="kit-panel kit-panel--detail" data-architecture-detail-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', t('architecture.registered_services'))}</div>
                <div class="kit-panel__desc">${escapeHtml(item?.typeName ?? t('common.no_selection'))}</div>
            </div>
        </div>
        ${renderArchitectureDetail(item)}
    </section>`;
}

function bindArchitectureWorkbenchActions() {
    $pageBody.querySelectorAll('[data-architecture-type]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectArchitectureRow(button.dataset.architectureType);
        });
    });
    bindArchitectureSearch();
}

function bindArchitectureSearch() {
    const input = $pageBody.querySelector('[data-architecture-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            architectureKitState.searchTerm = input.value || '';
            updateArchitectureListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-architecture-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            architectureKitState.searchTerm = '';
            updateArchitectureListDom();
            $pageBody.querySelector('[data-architecture-search]')?.focus();
        });
    }
}

function updateArchitectureListDom() {
    const visible = filterArchitectureRows(architectureKitState.architectures);
    const list = $pageBody.querySelector('.kit-resource-list');
    if (list) list.innerHTML = renderArchitectureRows(visible);
    const count = $pageBody.querySelector('[data-architecture-visible-count]');
    if (count) count.textContent = `${visible.length} / ${architectureKitState.architectures.length}`;
    const input = $pageBody.querySelector('[data-architecture-search]');
    if (input && input.value !== architectureKitState.searchTerm) input.value = architectureKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-architecture-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !architectureKitState.searchTerm.trim());
    bindArchitectureWorkbenchActions();
}

function selectArchitectureRow(typeName) {
    if (!typeName) return;
    architectureKitState.selectedType = typeName;
    $pageBody.querySelectorAll('[data-architecture-type]').forEach(row => {
        row.classList.toggle('active', row.dataset.architectureType === architectureKitState.selectedType);
    });
    const selected = architectureKitState.architectures.find(item => item.fullName === architectureKitState.selectedType) ?? null;
    const detailPanel = $pageBody.querySelector('[data-architecture-detail-panel]');
    if (detailPanel) detailPanel.outerHTML = renderArchitectureDetailSection(selected);
    bindArchitectureWorkbenchActions();
}

window.refreshArchitecture = refreshArchitecture;

