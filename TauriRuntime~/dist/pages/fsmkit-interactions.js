// pages/fsmkit-interactions.js
// FsmKit 交互：列表选择、搜索和延迟详情刷新。
function bindFsmWorkbenchList() {
    $pageBody.querySelectorAll('.fsm-list-item').forEach(btn => {
        if (btn.dataset.bound === '1') return;
        btn.dataset.bound = '1';
        btn.addEventListener('click', () => {
            selectFsmWorkbench(decodeURIComponent(btn.dataset.fsm));
        });
    });
}

function cancelFsmSelectionRender() {
    if (fsmSelectionRenderKind === 'idle' && fsmSelectionRenderHandle && typeof window.cancelIdleCallback === 'function') {
        cancelIdleCallback(fsmSelectionRenderHandle);
    } else if (fsmSelectionRenderHandle) {
        clearTimeout(fsmSelectionRenderHandle);
    }
    fsmSelectionRenderHandle = 0;
    fsmSelectionRenderKind = '';
}

function scheduleFsmSelectionRender(fsmName, requestSeq, fsms, selectedMeta) {
    const selectionSeq = ++fsmSelectionRenderSeq;
    cancelFsmSelectionRender();

    const run = () => {
        fsmSelectionRenderHandle = 0;
        fsmSelectionRenderKind = '';
        if (selectionSeq !== fsmSelectionRenderSeq) return;
        if (!isCurrentFsmWorkbenchLoad(requestSeq) || selectedFsmName !== fsmName) return;
        renderFsmWorkbenchFromCache(fsms, selectedMeta, '', true);
        if (!invoke || !connected) return;
        refreshSelectedFsmWorkbenchDetail(fsmName, requestSeq, fsms)
            .catch(e => {
                if (!isCurrentFsmWorkbenchLoad(requestSeq)) return;
                $pageBody.innerHTML = panel('FsmKit 错误', `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
            });
    };

    if (typeof window.requestIdleCallback === 'function') {
        fsmSelectionRenderKind = 'idle';
        fsmSelectionRenderHandle = requestIdleCallback(run, { timeout: FSM_SELECTION_RENDER_IDLE_TIMEOUT_MS });
        return;
    }

    fsmSelectionRenderKind = 'timeout';
    fsmSelectionRenderHandle = setTimeout(run, 48);
}

function selectFsmWorkbench(fsmName) {
    if (!fsmName || selectedFsmName === fsmName) return;

    selectedFsmName = fsmName;
    const requestSeq = ++fsmWorkbenchLoadSeq;
    markFsmListSelection(fsmName);

    const selectedMeta = fsmListCache.find(f => f.name === fsmName) ?? { name: fsmName };
    if (!updateFsmWorkbenchSummaryView(fsmListCache, selectedMeta, fsmDetailCache.get(fsmName) ?? selectedMeta, '')) {
        renderFsmWorkbenchFromCache(fsmListCache, selectedMeta, '', true);
    }
    scheduleFsmSelectionRender(fsmName, requestSeq, fsmListCache, selectedMeta);
}

function bindFsmWorkbenchSearch() {
    const input = $pageBody.querySelector('[data-fsm-search-input]');
    if (input && input.dataset.bound !== 'true') {
        input.dataset.bound = 'true';
        input.addEventListener('input', event => {
            fsmListSearchTerm = event.currentTarget.value;
            applyFsmWorkbenchSearch();
        });
        input.addEventListener('keydown', event => {
            if (event.key !== 'Escape') return;
            fsmListSearchTerm = '';
            event.currentTarget.value = '';
            applyFsmWorkbenchSearch();
        });
    }

    const clear = $pageBody.querySelector('[data-fsm-search-clear]');
    if (clear && clear.dataset.bound !== 'true') {
        clear.dataset.bound = 'true';
        clear.addEventListener('click', () => {
            fsmListSearchTerm = '';
            applyFsmWorkbenchSearch();
            $pageBody.querySelector('[data-fsm-search-input]')?.focus();
        });
    }
}

function applyFsmWorkbenchSearch() {
    const root = $pageBody.querySelector('[data-fsm-workbench="root"]');
    if (!root) return;

    syncFsmWorkbenchSearchControls();
    const countBadge = root.querySelector('[data-fsm-region="count"]');
    if (countBadge) {
        countBadge.textContent = formatFsmListCount(fsmListCache);
    }

    replaceFsmWorkbenchRegion(root, 'list', renderFsmListHtml(fsmListCache, fsmWorkbenchListEmptyMessage));
    bindFsmWorkbenchList();
}

function syncFsmWorkbenchSearchControls() {
    const input = $pageBody.querySelector('[data-fsm-search-input]');
    if (input && input.value !== fsmListSearchTerm) {
        input.value = fsmListSearchTerm;
    }

    const hasQuery = !!normalizeFsmListSearchTerm(fsmListSearchTerm);
    const clear = $pageBody.querySelector('[data-fsm-search-clear]');
    if (clear) {
        clear.classList.toggle('is-empty', !hasQuery);
        clear.disabled = !hasQuery;
    }
}

function markFsmListSelection(fsmName) {
    $pageBody.querySelectorAll('.fsm-list-item').forEach(btn => {
        const isActive = decodeURIComponent(btn.dataset.fsm ?? '') === fsmName;
        btn.classList.toggle('active', isActive);
        btn.setAttribute('aria-pressed', isActive ? 'true' : 'false');
    });
}
