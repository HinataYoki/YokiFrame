// pages/actionkit.js
// 页面：ActionKit
function renderActionKitPage() {
    $pageBody.classList.add('content-body--actionkit');
    setHero(
        t('actionkit.title'),
        t('actionkit.subtitle'),
        t('actionkit.tab'),
        'action',
        `<button class="btn btn-sm" onclick="refreshActionKit()">${t('actionkit.refresh')}</button>`
    );

    clearTabs();
    actionKitState.renderSignature = '';
    loadActionKitWorkbench();
}

async function refreshActionKit() {
    await loadActionKitWorkbench({ forceCommandRefresh: true });
}

async function refreshActionKitReactive() {
    await loadActionKitWorkbench();
}

async function fetchActionKitWorkbenchState({ forceCommandRefresh = false } = {}) {
    return await fetchKitWorkbenchState('ActionKit', normalizeActionKitStatePayload, {
        forceCommandRefresh: forceCommandRefresh,
    });
}

function applyActionKitWorkbenchState(state) {
    actionKitState.stats = state.stats ?? {};
    actionKitState.roots = Array.isArray(state.roots) ? state.roots : [];
    return reconcileActionKitSelection(filterActionKitRoots(actionKitState.roots, actionKitState.searchTerm));
}

async function loadActionKitWorkbench({ forceCommandRefresh = false } = {}) {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('action', t('actionkit.need_runtime_bridge'));
        clearMetrics();
        return;
    }

    if (!canSendRuntimeKitCommand('ActionKit')) {
        showRuntimeKitUnavailable('ActionKit', t('actionkit.title'));
        return;
    }

    try {
        const state = await fetchActionKitWorkbenchState({ forceCommandRefresh });
        const selectedNode = applyActionKitWorkbenchState(state);
        clearMetrics();

        const visibleRoots = filterActionKitRoots(actionKitState.roots, actionKitState.searchTerm);
        const html = renderActionKitWorkbench(actionKitState.stats, visibleRoots, selectedNode);
        const signature = makeStableSignature({
            stats: actionKitState.stats,
            roots: visibleRoots,
            selectedNodeId: actionKitState.selectedNodeId,
            searchTerm: actionKitState.searchTerm,
        });
        renderWorkbenchHtmlStable(actionKitState, html, signature, bindActionKitWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('ActionKit')) {
            showRuntimeKitUnavailable('ActionKit', t('actionkit.title'));
            return;
        }
        $pageBody.innerHTML = panel(t('actionkit.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}
