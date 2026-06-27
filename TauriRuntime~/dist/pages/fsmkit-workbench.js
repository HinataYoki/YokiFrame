// pages/fsmkit-workbench.js
// FsmKit 工作台壳：列表、详情、图区域和历史区域的稳定 DOM 更新。
function fsmStatePill(state) {
    if (state === 'Running') return 'success';
    if (state === 'Suspend') return 'warning';
    return 'info';
}
function renderFsmWorkbenchFromCache(fsms, selectedMeta, emptyMessage = '', preferInPlace = true) {
    const fsmName = selectedMeta?.name ?? selectedFsmName ?? '';
    const detail = fsmDetailCache.get(fsmName) ?? selectedMeta;
    const history = fsmHistoryCache.get(fsmName) ?? [];
    renderOrUpdateFsmWorkbench(fsms, selectedMeta, detail, history, emptyMessage, preferInPlace);
}

function updateFsmWorkbenchSummaryView(fsms, selectedMeta, detail, emptyMessage = '') {
    const root = $pageBody.querySelector('[data-fsm-workbench="root"]');
    if (!root) return false;

    fsmWorkbenchListEmptyMessage = emptyMessage;
    const selectedTitle = root.querySelector('[data-fsm-region="detail-title"]');
    if (selectedTitle) {
        selectedTitle.textContent = selectedMeta ? (selectedMeta.name ?? t('fsmkit.state_detail')) : t('fsmkit.no_selection');
    }

    const countBadge = root.querySelector('[data-fsm-region="count"]');
    if (countBadge) {
        countBadge.textContent = formatFsmListCount(fsms);
    }

    const listChanged = replaceFsmWorkbenchRegion(root, 'list', renderFsmListHtml(fsms, emptyMessage));
    const detailChanged = replaceFsmWorkbenchRegion(root, 'detail', renderFsmDetailRegionHtml(selectedMeta, detail));

    syncFsmWorkbenchSearchControls();
    if (listChanged) {
        bindFsmWorkbenchList();
    }
    if (detailChanged) {
        scheduleFsmCurrentStateFit();
    }
    return true;
}

async function refreshSelectedFsmWorkbenchDetail(requestedFsmName, requestSeq, fsms = fsmListCache) {
    try {
        const snapshot = await fetchFsmWorkbenchSnapshot(requestedFsmName);
        if (!isCurrentFsmWorkbenchLoad(requestSeq) || selectedFsmName !== requestedFsmName) return;
        if (snapshot) {
            const snapshotFsms = snapshot.fsms?.length ? snapshot.fsms : (fsmListCache.length ? fsmListCache : fsms);
            const selectedMeta = snapshotFsms.find(f => f.name === requestedFsmName)
                ?? fsms.find(f => f.name === requestedFsmName)
                ?? { name: requestedFsmName };
            renderOrUpdateFsmWorkbench(snapshotFsms, selectedMeta, snapshot.detail ?? selectedMeta, snapshot.history ?? [], '', true);
            return;
        }

        const [detail, history] = await Promise.all([
            fetchFsmStateDetail(requestedFsmName),
            fetchFsmHistory(requestedFsmName)
        ]);
        if (!isCurrentFsmWorkbenchLoad(requestSeq) || selectedFsmName !== requestedFsmName) return;

        const latestFsms = fsmListCache.length ? fsmListCache : fsms;
        const selectedMeta = latestFsms.find(f => f.name === requestedFsmName)
            ?? fsms.find(f => f.name === requestedFsmName)
            ?? { name: requestedFsmName };
        renderOrUpdateFsmWorkbench(latestFsms, selectedMeta, detail, history, '', true);
    } catch (e) {
        if (isFsmNotFoundError(e) && await reconcileFsmWorkbenchAfterMissingSelection(requestedFsmName, requestSeq)) {
            return;
        }

        throw e;
    }
}

function renderOrUpdateFsmWorkbench(fsms, selectedMeta, detail, history, emptyMessage = '', preferInPlace = true) {
    fsmWorkbenchListEmptyMessage = emptyMessage;
    const nextSignature = makeFsmWorkbenchSignature(fsms, selectedMeta, detail, history, emptyMessage);
    if (preferInPlace && nextSignature === fsmWorkbenchRenderSignature) {
        return;
    }

    fsmWorkbenchRenderSignature = nextSignature;
    const updateInfo = preferInPlace ? updateFsmWorkbenchView(fsms, selectedMeta, detail, history, emptyMessage) : null;
    if (!updateInfo) {
        renderFsmWorkbenchShell(fsms, selectedMeta, detail, history, emptyMessage);
        syncFsmWorkbenchSearchControls();
        bindFsmWorkbenchSearch();
        bindFsmWorkbenchList();
        bindFsmGraphInteractions();
        scheduleFsmCurrentStateFit();
        return;
    }

    syncFsmWorkbenchSearchControls();
    if (updateInfo.listChanged) {
        bindFsmWorkbenchList();
    }
    if (updateInfo.matrixChanged) {
        bindFsmGraphInteractions();
    }
    if (updateInfo.detailChanged) {
        scheduleFsmCurrentStateFit();
    }
}

function updateFsmWorkbenchView(fsms, selectedMeta, detail, history, emptyMessage = '') {
    const root = $pageBody.querySelector('[data-fsm-workbench="root"]');
    if (!root) return false;
    const listChanged = replaceFsmWorkbenchRegion(root, 'list', renderFsmListHtml(fsms, emptyMessage));
    const detailChanged = replaceFsmWorkbenchRegion(root, 'detail', renderFsmDetailRegionHtml(selectedMeta, detail));

    const selectedTitle = root.querySelector('[data-fsm-region="detail-title"]');
    if (selectedTitle) {
        selectedTitle.textContent = selectedMeta ? (selectedMeta.name ?? t('fsmkit.state_detail')) : t('fsmkit.no_selection');
    }

    const countBadge = root.querySelector('[data-fsm-region="count"]');
    if (countBadge) {
        countBadge.textContent = formatFsmListCount(fsms);
    }

    captureFsmGraphViewport();
    const matrixChanged = replaceFsmWorkbenchRegion(root, 'matrix', renderFsmMatrixHtml(selectedMeta, detail, history));
    const insightsChanged = replaceFsmWorkbenchRegion(root, 'insights', renderFsmInsightsHtml(selectedMeta, detail, history));
    const historyChanged = replaceFsmWorkbenchRegion(root, 'history', renderFsmHistoryHtml(selectedMeta, history));
    return {
        listChanged: listChanged,
        detailChanged: detailChanged,
        matrixChanged: matrixChanged,
        insightsChanged: insightsChanged,
        historyChanged: historyChanged,
    };
}

function replaceFsmWorkbenchRegion(root, region, html) {
    const target = root.querySelector(`[data-fsm-region="${region}"]`);
    if (!target) return false;
    if (target.innerHTML === html) return false;
    target.innerHTML = html;
    return true;
}

function renderFsmWorkbenchShell(fsms, selectedMeta, detail, history, emptyMessage = '') {
    $pageBody.innerHTML = `
        <div class="fsm-workbench" data-fsm-workbench="root">
            <aside class="tool-pane fsm-index-pane">
                <div class="tool-pane-header">
                    <div>
                        <div class="tool-pane-title">${t('fsmkit.active_machines')}</div>
                    </div>
                    <span class="status-pill status-pill--info" data-fsm-region="count">${formatFsmListCount(fsms)}</span>
                </div>
                <div class="tool-pane-body">
                    ${renderFsmListSearchHtml()}
                    <div class="fsm-list" data-fsm-region="list">${renderFsmListHtml(fsms, emptyMessage)}</div>
                </div>
            </aside>
            <section class="fsm-observe-stage">
                <div class="tool-pane fsm-detail-pane">
                    <div class="tool-pane-header">
                        <div>
                            <div class="tool-pane-title" data-fsm-region="detail-title">${selectedMeta ? escapeHtml(selectedMeta.name ?? t('fsmkit.state_detail')) : t('fsmkit.no_selection')}</div>
                        </div>
                    </div>
                    <div class="tool-pane-body" data-fsm-region="detail">${renderFsmDetailRegionHtml(selectedMeta, detail)}</div>
                </div>
                <div class="tool-pane fsm-flow-pane">
                    <div class="tool-pane-header">
                        <div>
                            <div class="tool-pane-title">${t('fsmkit.state_flow_graph')}</div>
                        </div>
                    </div>
                    <div class="tool-pane-body" data-fsm-region="matrix">${renderFsmMatrixHtml(selectedMeta, detail, history)}</div>
                </div>
            </section>
            <aside class="fsm-observe-side">
                <div class="tool-pane">
                    <div class="tool-pane-header">
                        <div>
                            <div class="tool-pane-title">${t('fsmkit.event_insights')}</div>
                        </div>
                    </div>
                    <div class="tool-pane-body" data-fsm-region="insights">${renderFsmInsightsHtml(selectedMeta, detail, history)}</div>
                </div>
                <div class="tool-pane fsm-history-pane">
                    <div class="tool-pane-header">
                        <div>
                            <div class="tool-pane-title">${t('fsmkit.transition_history')}</div>
                        </div>
                    </div>
                    <div class="tool-pane-body" data-fsm-region="history">${renderFsmHistoryHtml(selectedMeta, history)}</div>
                </div>
            </aside>
        </div>`;
    scheduleHeroActionPromotion();
}

function renderFsmListHtml(fsms, emptyMessage = '') {
    if (!fsms.length) return emptyState('○', emptyMessage || t('fsmkit.no_displayable_machines'));
    const filteredFsms = filterFsmListItems(fsms);
    if (!filteredFsms.length) {
        return emptyState('⌕', t('fsmkit.search_machines', escapeHtml(fsmListSearchTerm.trim())));
    }

    return filteredFsms.map(f => {
        const name = f.name ?? '--';
        const active = name === selectedFsmName ? ' active' : '';
        const currentState = f.currentState ?? '--';
        const stateCount = f.stateCount ?? '--';
        return `<button class="fsm-list-item${active}" data-fsm="${encodeURIComponent(name)}" type="button" aria-pressed="${active ? 'true' : 'false'}">
            <div class="fsm-list-content">
                <div class="fsm-list-name" title="${escapeHtml(name)}">${escapeHtml(name)}</div>
                <div class="fsm-list-meta" title="${escapeHtml(currentState)} · ${t('fsmkit.state_count_label', escapeHtml(stateCount))}">
                    <span class="fsm-list-state">${escapeHtml(currentState)}</span>
                    <span class="fsm-list-separator">·</span>
                    <span class="fsm-list-count">${t('fsmkit.state_count_label', escapeHtml(stateCount))}</span>
                </div>
            </div>
            <span class="status-pill status-pill--${fsmStatePill(f.machineState)}">${escapeHtml(f.machineState ?? '--')}</span>
        </button>`;
    }).join('');
}

function renderFsmListSearchHtml() {
    const hasQuery = !!fsmListSearchTerm.trim();
    return `<div class="fsm-list-search" role="search">
        ${svgIcon('search', 'fsm-list-search__icon')}
        <input class="fsm-list-search__input" data-fsm-search-input type="search"
               value="${escapeHtml(fsmListSearchTerm)}"
               placeholder="${t('fsmkit.search_placeholder')}"
               autocomplete="off" spellcheck="false" aria-label="${t('common.search')}">
        <button class="fsm-list-search__clear${hasQuery ? '' : ' is-empty'}" data-fsm-search-clear
                type="button" aria-label="${t('common.clear_search')}" ${hasQuery ? '' : 'disabled'}>×</button>
    </div>`;
}

function normalizeFsmListSearchTerm(value) {
    return String(value ?? '').trim().toLowerCase();
}

function filterFsmListItems(fsms) {
    const query = normalizeFsmListSearchTerm(fsmListSearchTerm);
    if (!query) return fsms;

    const terms = query.split(/\s+/).filter(Boolean);
    return fsms.filter(f => {
        const haystack = [
            f.name,
            f.currentState,
            f.machineState,
            f.stateCount,
        ].map(v => String(v ?? '').toLowerCase()).join('\n');
        return terms.every(term => haystack.includes(term));
    });
}

function formatFsmListCount(fsms) {
    const total = fsms.length;
    if (!normalizeFsmListSearchTerm(fsmListSearchTerm)) return String(total);
    return `${filterFsmListItems(fsms).length}/${total}`;
}

function renderFsmDetailRegionHtml(selectedMeta, detail) {
    return selectedMeta
        ? renderFsmDetail(detail ?? selectedMeta, selectedMeta)
        : emptyState('○', t('fsmkit.no_selection_hint'));
}

function renderFsmMatrixHtml(selectedMeta, detail, history) {
    const currentState = selectedMeta?.currentState ?? detail?.currentState ?? null;
    const graph = selectedMeta ? buildFsmGraph(history ?? [], currentState, detail?.states ?? []) : null;
    if (!graph || !graph.nodes.length) {
        return `<div class="fsm-flow-placeholder">${t('fsmkit.graph_placeholder')}</div>`;
    }

    return `<div class="fsm-flow-shell">
        <div class="fsm-graph-toolbar">
            <div>
                <div class="fsm-graph-label">${t('fsmkit.state_flow_graph')}</div>
                <span class="fsm-graph-stat">${t('fsmkit.visible_states', graph.nodes.length, graph.edges.length)}</span>
            </div>
            <div class="fsm-viewport-controls">
                <button class="btn btn-secondary btn-sm" id="fsm-graph-zoom-out" type="button">-</button>
                <span id="fsm-graph-zoom-label" class="fsm-graph-zoom-label">100%</span>
                <button class="btn btn-secondary btn-sm" id="fsm-graph-zoom-in" type="button">+</button>
                <button class="btn btn-secondary btn-sm" id="fsm-graph-fit" type="button">${t("fsmkit.fit")}</button>
            </div>
        </div>
        <div class="fsm-graph-scroll" id="fsm-graph-scroll" data-fsm-name="${escapeHtml(selectedMeta.name ?? selectedFsmName)}">${renderFsmGraphCanvas(graph)}</div>
        ${fsmGraphLegend(graph)}
    </div>`;
}

function renderFsmHistoryHtml(selectedMeta, history) {
    if (!history || !history.length) {
        return emptyState('↔', selectedMeta ? t('fsmkit.no_history_short') : t('fsmkit.no_selection'));
    }

    return `<div class="fsm-history-scroll"><div class="fsm-timeline">` + history.slice(-120).reverse().map((h, index) => `
        <div class="fsm-timeline-item">
            <span class="fsm-timeline-dot${index === 0 ? ' fsm-timeline-dot--latest' : ''}"></span>
            <div class="fsm-timeline-card">
                <span class="fsm-timeline-card__time">${escapeHtml(h.time ?? '')}</span>
                <span class="fsm-timeline-card__route">
                    <span class="fsm-timeline-from">${escapeHtml(h.from ?? '?')}</span>
                    <span class="fsm-timeline-arrow">→</span>
                    <span class="fsm-timeline-to">${escapeHtml(h.to ?? '?')}</span>
                </span>
            </div>
        </div>`).join('') + `</div></div>`;
}

function renderFsmDetail(detail, selectedMeta) {
    if (detail?.error) {
        return `<span style="color:var(--error)">${escapeHtml(detail.error)}</span>`;
    }
    const fsmName = detail.fsmName ?? selectedMeta?.name ?? '--';
    const currentState = detail.currentState ?? selectedMeta?.currentState ?? '--';
    const machineState = detail.machineState ?? selectedMeta?.machineState ?? '--';
    const stateId = detail.currentStateId ?? '--';
    const stateCount = detail.stateCount ?? selectedMeta?.stateCount ?? '--';
    return `<div class="fsm-detail-grid">
        <div class="fsm-state-card">
            <div class="fsm-state-card__signal" aria-hidden="true"></div>
            <div class="fsm-state-card__content">
                <span class="fsm-state-card__label">${t("fsmkit.current_state")}</span>
                <strong class="fsm-state-card__value" data-fsm-current-state-value title="${escapeHtml(currentState)}">${escapeHtml(currentState)}</strong>
                <span class="fsm-state-card__meta" title="${escapeHtml(fsmName)} · State Id ${escapeHtml(stateId)}">${escapeHtml(fsmName)} · State Id ${escapeHtml(stateId)}</span>
            </div>
            <span class="status-pill status-pill--${fsmStatePill(machineState)}">${escapeHtml(machineState)}</span>
        </div>
        ${fsmDetailChip(t('fsmkit.state_count'), stateCount)}
        ${fsmDetailChip('Machine State', machineState)}
        ${fsmDetailChip('FSM', fsmName)}
    </div>`;
}

function fsmDetailChip(label, value) {
    return `<div class="fsm-detail-chip"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`;
}

