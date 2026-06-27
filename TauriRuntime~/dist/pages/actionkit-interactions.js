// pages/actionkit-interactions.js
// ActionKit 局部交互和命令动作
function bindActionKitWorkbenchActions() {
    bindKitToggleChange('[data-actionkit-stack]', () => toggleActionKitStackTrace());
    bindKitButtonClick('[data-actionkit-clear-stack]', () => clearActionKitStackTrace());
    bindActionKitSearch();
    bindActionKitNodeClicks();
    bindKitCodeJumpButtons();
}

function bindActionKitSearch() {
    const input = $pageBody.querySelector('[data-actionkit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            actionKitState.searchTerm = input.value || '';
            renderActionKitWorkbenchFromState();
        });
    }

    const clearButton = $pageBody.querySelector('[data-actionkit-search-clear]');
    if (clearButton && clearButton.dataset.bound !== '1') {
        clearButton.dataset.bound = '1';
        clearButton.addEventListener('click', () => {
            actionKitState.searchTerm = '';
            renderActionKitWorkbenchFromState();
        });
    }
}

function bindActionKitNodeClicks() {
    $pageBody.querySelectorAll('[data-actionkit-node]').forEach(element => {
        if (element.dataset.bound === '1') return;
        element.dataset.bound = '1';
        element.addEventListener('click', event => {
            const nodeId = element.dataset.actionkitNode;
            if (!nodeId) return;
            actionKitState.selectedNodeId = nodeId;
            syncActionKitSelectionDom();
            event.stopPropagation();
        });
    });
}

function syncActionKitSelectionDom() {
    const visibleRoots = filterActionKitRoots(actionKitState.roots, actionKitState.searchTerm);
    const selectedNode = reconcileActionKitSelection(visibleRoots);
    const selectedNodeId = selectedNode ? String(selectedNode.id) : '';
    const root = $pageBody.querySelector('[data-actionkit-workbench="root"]');

    if (!root) {
        renderActionKitWorkbenchFromState();
        return;
    }

    root.querySelectorAll('[data-actionkit-node]').forEach(element => {
        element.classList.toggle('active', selectedNodeId && String(element.dataset.actionkitNode) === selectedNodeId);
    });

    const selectedPath = getActionKitNodePath(visibleRoots, selectedNode?.id);
    const breadcrumb = root.querySelector('[data-actionkit-breadcrumb]');
    if (breadcrumb) {
        breadcrumb.innerHTML = renderActionKitBreadcrumb(selectedPath);
    }

    const detailPanel = root.querySelector('[data-actionkit-detail-panel]');
    if (detailPanel) {
        detailPanel.outerHTML = renderActionKitNodeDetail(selectedNode, selectedPath);
    }

    actionKitState.renderSignature = makeStableSignature({
        stats: actionKitState.stats,
        roots: visibleRoots,
        selectedNodeId: actionKitState.selectedNodeId,
        searchTerm: actionKitState.searchTerm,
    });
    bindActionKitWorkbenchActions();
}

function renderActionKitWorkbenchFromState() {
    const visibleRoots = filterActionKitRoots(actionKitState.roots, actionKitState.searchTerm);
    const selectedNode = reconcileActionKitSelection(visibleRoots);
    const html = renderActionKitWorkbench(actionKitState.stats, visibleRoots, selectedNode);
    const signature = makeStableSignature({
        stats: actionKitState.stats,
        roots: visibleRoots,
        selectedNodeId: actionKitState.selectedNodeId,
        searchTerm: actionKitState.searchTerm,
    });
    renderWorkbenchHtmlStable(actionKitState, html, signature, bindActionKitWorkbenchActions);
}

async function toggleActionKitStackTrace() {
    const nextEnabled = !(actionKitState.stats?.stackTraceEnabled);
    await sendKitCommandData('ActionKit', 'set_stack_trace', { enabled: nextEnabled });
    await loadActionKitWorkbench({ forceCommandRefresh: true });
}

async function clearActionKitStackTrace() {
    await sendKitCommandData('ActionKit', 'clear_stack_trace');
    await loadActionKitWorkbench({ forceCommandRefresh: true });
}
