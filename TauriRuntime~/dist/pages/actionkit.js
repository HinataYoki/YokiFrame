// pages/actionkit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：ActionKit
// ═══════════════════════════════════════════════════════════════════
const actionKitState = {
    stats: null,
    roots: [],
    searchTerm: '',
    selectedNodeId: null,
    renderSignature: '',
};

function normalizeActionKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const roots = Array.isArray(source.roots)
        ? source.roots
        : Array.isArray(source.actions)
            ? source.actions
            : [];

    return {
        stats: source.stats ?? {},
        roots: roots.map(normalizeActionKitNode),
    };
}

function normalizeActionKitNode(node) {
    const source = node ?? {};
    const children = Array.isArray(source.children) ? source.children.map(normalizeActionKitNode) : [];
    const actionId = Number(source.actionId ?? source.id ?? 0);
    const currentChildIndex = Number(source.currentChildIndex ?? -1);

    return {
        id: String(source.id ?? source.actionId ?? actionId ?? ''),
        actionId: Number.isFinite(actionId) && actionId > 0 ? actionId : 0,
        type: String(source.type ?? 'Unknown'),
        debugInfo: String(source.debugInfo ?? source.type ?? 'Unknown'),
        status: String(source.status ?? 'NotStart'),
        paused: !!source.paused,
        deinited: !!source.deinited,
        executorName: String(source.executorName ?? ''),
        updateMode: String(source.updateMode ?? ''),
        childCount: Number(source.childCount ?? children.length ?? 0) || 0,
        currentChildIndex: Number.isFinite(currentChildIndex) ? currentChildIndex : -1,
        stackTrace: normalizeActionKitStackTrace(source.stackTrace),
        children,
    };
}

function normalizeActionKitStackTrace(stackTrace) {
    if (!stackTrace || typeof stackTrace !== 'object') return null;
    const frames = Array.isArray(stackTrace.frames)
        ? stackTrace.frames.map(frame => ({
            method: String(frame?.method ?? ''),
            filePath: String(frame?.filePath ?? ''),
            line: Number(frame?.line ?? 0) || 0,
        }))
        : [];

    return {
        text: String(stackTrace.text ?? ''),
        frameCount: Number(stackTrace.frameCount ?? frames.length) || frames.length,
        frames,
    };
}

function flattenActionKitNodes(roots, result = [], parent = null, depth = 0) {
    if (!Array.isArray(roots)) return result;

    for (let i = 0; i < roots.length; i++) {
        const node = roots[i];
        if (!node) continue;
        result.push({ node, depth, parent, isRoot: !parent });
        if (Array.isArray(node.children) && node.children.length) {
            flattenActionKitNodes(node.children, result, node, depth + 1);
        }
    }

    return result;
}

function findActionKitNodeById(roots, nodeId) {
    const id = String(nodeId ?? '');
    if (!id) return null;

    for (let i = 0; i < roots.length; i++) {
        const node = roots[i];
        if (!node) continue;
        if (String(node.id) === id) return node;
        const found = findActionKitNodeById(node.children ?? [], id);
        if (found) return found;
    }

    return null;
}

function actionKitNodeMatchesSearchSelf(node, term) {
    const query = String(term || '').trim().toLowerCase();
    if (!query) return true;

    return [
        node.id,
        node.type,
        node.debugInfo,
        node.status,
        node.executorName,
        node.updateMode,
        node.stackTrace?.text,
    ].some(value => String(value ?? '').toLowerCase().includes(query));
}

function actionKitNodeMatchesSearch(node, term) {
    if (!node) return false;
    if (actionKitNodeMatchesSearchSelf(node, term)) return true;
    if (!Array.isArray(node.children)) return false;
    for (let i = 0; i < node.children.length; i++) {
        if (actionKitNodeMatchesSearch(node.children[i], term)) return true;
    }
    return false;
}

function filterActionKitRoots(roots, term) {
    if (!Array.isArray(roots) || !roots.length) return [];
    const query = String(term || '').trim();
    if (!query) return roots;
    return roots.filter(root => actionKitNodeMatchesSearch(root, query));
}

function reconcileActionKitSelection(roots) {
    const selected = findActionKitNodeById(roots, actionKitState.selectedNodeId);
    if (selected) return selected;

    const flat = flattenActionKitNodes(roots);
    if (!flat.length) {
        actionKitState.selectedNodeId = null;
        return null;
    }

    actionKitState.selectedNodeId = flat[0].node.id;
    return flat[0].node;
}

function actionKitNodeTypeClass(type) {
    const normalized = String(type || '').toLowerCase();
    if (normalized === 'sequence') return 'sequence';
    if (normalized === 'parallel') return 'parallel';
    if (normalized === 'repeat') return 'repeat';
    return 'leaf';
}

function actionKitStatusClass(status) {
    const normalized = String(status || '').toLowerCase();
    if (normalized === 'started') return 'running';
    if (normalized === 'finished') return 'finished';
    return 'idle';
}

function formatActionKitProgress(node) {
    const childCount = Array.isArray(node?.children) ? node.children.length : Number(node?.childCount ?? 0) || 0;
    if (childCount <= 0) return 'leaf';
    const index = Number(node?.currentChildIndex ?? -1);
    return index >= 0 ? `${index + 1}/${childCount}` : `${childCount} 子节点`;
}

function getActionKitNodePath(roots, nodeId, path = []) {
    const id = String(nodeId ?? '');
    if (!id || !Array.isArray(roots)) return [];

    for (let i = 0; i < roots.length; i++) {
        const node = roots[i];
        if (!node) continue;
        const nextPath = [...path, node];
        if (String(node.id) === id) return nextPath;

        const childPath = getActionKitNodePath(node.children ?? [], id, nextPath);
        if (childPath.length) return childPath;
    }

    return [];
}

function renderActionKitBreadcrumb(path) {
    if (!Array.isArray(path) || !path.length) return '<span class="actionkit-breadcrumb__empty">未选择节点</span>';
    return path.map((node, index) => {
        const label = index === path.length - 1 ? node.type : `${node.type}#${node.id}`;
        return `<span class="actionkit-breadcrumb__item">${escapeHtml(label)}</span>`;
    }).join('<span class="actionkit-breadcrumb__sep">/</span>');
}

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
    if (forceCommandRefresh) {
        return await fetchActionKitWorkbenchStateFromCommands();
    }

    const telemetry = await readKitTelemetryData('ActionKit');
    if (telemetry) return normalizeActionKitStatePayload(telemetry);

    const snapshot = await readKitSnapshotData('ActionKit');
    if (snapshot) return normalizeActionKitStatePayload(snapshot);

    return null;
}

async function fetchActionKitWorkbenchStateFromCommands() {
    const snapshot = await sendKitCommandData('ActionKit', 'get_workbench_snapshot');
    return normalizeActionKitStatePayload(snapshot);
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
        const snapshotState = await fetchActionKitWorkbenchState({ forceCommandRefresh });
        const state = snapshotState ?? await fetchActionKitWorkbenchStateFromCommands();
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

function renderActionKitWorkbench(stats, roots, selectedNode) {
    const stackText = stats?.stackTraceEnabled ? t('actionkit.stack_trace_enabled') : t('actionkit.stack_trace_disabled');
    const visibleNodeCount = flattenActionKitNodes(roots).length;
    const selectedPath = getActionKitNodePath(roots, selectedNode?.id);
    return `<div class="kit-workbench kit-workbench--actionkit">
        <section class="kit-toolbar">
            <div>
                <div class="kit-toolbar__title">${renderKitTitle('action', t('actionkit.action_hierarchy'))}</div>
                <div class="kit-toolbar__meta">${escapeHtml(stackText)} · ${t('actionkit.active')} ${escapeHtml(stats?.activeCount ?? 0)} · ${t('actionkit.finished')} ${escapeHtml(stats?.finishedCount ?? 0)} · ${t('actionkit.cancelled')} ${escapeHtml(stats?.cancelledCount ?? 0)} · ${t('common.frames')} ${escapeHtml(stats?.frameCount ?? 0)} · ${t('common.records')} ${escapeHtml(stats?.stackTraceCount ?? 0)} · ${t('common.nodes')} ${escapeHtml(visibleNodeCount)}</div>
            </div>
            <div class="kit-toolbar__actions">
                ${renderKitToggle(t('actionkit.stack_trace'), !!stats?.stackTraceEnabled, 'data-actionkit-stack', t('actionkit.stack_trace_hint'))}
                <button class="btn btn-sm" data-actionkit-clear-stack>${t('actionkit.clear_records')}</button>
                <button class="btn btn-primary btn-sm" onclick="refreshActionKit()">${t('actionkit.refresh')}</button>
            </div>
        </section>
        <section class="actionkit-structure-shell">
            <aside class="actionkit-root-rail">
                <div class="actionkit-rail-head">
                    <div>
                        <div class="actionkit-rail-title">${renderKitTitle('action', t('actionkit.root_actions'))}</div>
                        <div class="actionkit-rail-desc">${t('actionkit.root_actions_desc')}</div>
                    </div>
                    <span class="actionkit-count">${escapeHtml(roots.length)}</span>
                </div>
                <div class="actionkit-rail-tools">${renderKitSearchInput(actionKitState.searchTerm, 'data-actionkit-search', t('actionkit.search_placeholder'))}</div>
                <div class="actionkit-root-list" data-kit-scroll-key="actionkit-root-list">${renderActionKitRootList(roots, selectedNode)}</div>
            </aside>
            <main class="actionkit-tree-stage">
                <div class="actionkit-stage-head">
                    <div>
                        <div class="actionkit-stage-title">${renderKitTitle('action', t('actionkit.execution_flow'))}</div>
                        <div class="actionkit-stage-desc">${t('actionkit.execution_flow_desc')}</div>
                    </div>
                    <div class="actionkit-breadcrumb" title="${t('actionkit.select_node_hint')}">${renderActionKitBreadcrumb(selectedPath)}</div>
                </div>
                <div class="actionkit-flow-map actionkit-tree-scroll" data-kit-scroll-key="actionkit-flow-map">${renderActionKitFlowMap(roots, selectedNode)}</div>
                ${renderActionKitNodeDetail(selectedNode, selectedPath)}
            </main>
        </section>
    </div>`;
}

function renderActionKitRootList(roots, selectedNode) {
    if (!Array.isArray(roots) || !roots.length) {
        return emptyState('action', t('actionkit.no_visible_action'));
    }

    return roots.map(root => {
        const selected = selectedNode && String(selectedNode.id) === String(root.id);
        const statusClass = actionKitStatusClass(root.status);
        const typeClass = actionKitNodeTypeClass(root.type);
        const progress = formatActionKitProgress(root);
        const stackText = root.stackTrace?.frameCount > 0 ? ` · ${t('actionkit.stack')} ${root.stackTrace.frameCount}` : '';
        return `<button class="actionkit-root-item actionkit-root-item--${typeClass}${selected ? ' active' : ''} actionkit-root-item--${statusClass}" type="button" data-actionkit-node="${escapeHtml(root.id)}">
            <span class="actionkit-root-item__main">
                <strong>${escapeHtml(root.type ?? '--')}</strong>
                <em>${escapeHtml(root.debugInfo ?? '--')}</em>
            </span>
            <span class="actionkit-root-item__meta">
                <span>${escapeHtml(root.executorName || 'PlayerLoop')}</span>
                <span>${escapeHtml(progress)}</span>
            </span>
            <span class="actionkit-root-item__sub">ID ${escapeHtml(root.id)} · ${escapeHtml(root.status)}${stackText}</span>
        </button>`;
    }).join('');
}

function renderActionKitFlowMap(roots, selectedNode) {
    if (!Array.isArray(roots) || !roots.length) {
        return emptyState('action', t('actionkit.no_visual_action'));
    }

    return `<div class="actionkit-tree" role="tree">${roots.map(root => renderActionKitTreeRows(root, {
        selectedNodeId: selectedNode ? selectedNode.id : null,
        searchTerm: actionKitState.searchTerm,
        depth: 0,
        parentType: 'root',
        siblingIndex: 0,
    })).join('')}</div>`;
}

function renderActionKitTreeRows(node, options = {}) {
    if (!node) return '';

    const searchTerm = options.searchTerm || '';
    if (searchTerm && !actionKitNodeMatchesSearch(node, searchTerm)) return '';

    const depth = Number(options.depth ?? 0) || 0;
    const selectedNodeId = options.selectedNodeId != null ? String(options.selectedNodeId) : null;
    const selected = selectedNodeId != null && String(node.id) === selectedNodeId;
    const typeClass = actionKitNodeTypeClass(node.type);
    const statusClass = actionKitStatusClass(node.status);
    const current = node.currentChildIndex >= 0 && node.currentChildIndex < node.children.length ? node.currentChildIndex : -1;
    const childCount = Array.isArray(node.children) ? node.children.length : 0;
    const isCurrent = options.isCurrent === true;
    const metaParts = [`ID ${escapeHtml(node.id)}`, escapeHtml(node.status)];
    if (node.executorName) metaParts.push(escapeHtml(node.executorName));
    if (node.updateMode) metaParts.push(escapeHtml(node.updateMode));
    if (node.paused) metaParts.push(t('actionkit.paused'));
    if (node.stackTrace?.frameCount > 0) metaParts.push(`${t('actionkit.stack')} ${escapeHtml(node.stackTrace.frameCount)}`);

    const row = `<button class="actionkit-tree-row actionkit-tree-row--${typeClass} actionkit-tree-row--status-${statusClass}${selected ? ' active' : ''}${isCurrent ? ' is-current' : ''}" type="button" role="treeitem" data-actionkit-node="${escapeHtml(node.id)}" data-actionkit-depth="${escapeHtml(depth)}" style="--action-depth:${escapeHtml(depth * 28)}px">
            <span class="actionkit-tree-row__rail" aria-hidden="true"></span>
            <span class="actionkit-tree-row__kind">${escapeHtml(typeClass === 'leaf' ? 'Action' : node.type)}</span>
            <span class="actionkit-tree-row__title">
                <span class="actionkit-tree-row__status actionkit-tree-row__status--${statusClass}"></span>
                <strong>${escapeHtml(node.type ?? '--')}</strong>
                ${childCount ? `<em>${escapeHtml(current >= 0 ? (current + 1) + '/' + childCount : childCount + ' 个')}</em>` : ''}
            </span>
            <span class="actionkit-tree-row__debug">${escapeHtml(node.debugInfo || node.type || '--')}</span>
            <span class="actionkit-tree-row__meta">${metaParts.join(' · ')}</span>
        </button>
    `;

    if (!childCount) return row;

    const children = node.children.map((child, index) => renderActionKitTreeRows(child, {
        selectedNodeId,
        searchTerm,
        depth: depth + 1,
        parentType: typeClass,
        siblingIndex: index,
        isCurrent: index === node.currentChildIndex,
    })).join('');

    return `${row}<div class="actionkit-tree-children actionkit-tree-children--${typeClass}" role="group">${children}</div>`;
}

function renderActionKitNodeDetail(node, selectedPath = []) {
    if (!node) {
        return emptyState('action', t('actionkit.select_node_hint'));
    }

    return `<section class="actionkit-inspector-drawer" data-kit-scroll-key="actionkit-detail-stack">
        <div class="actionkit-inspector-head">
            <div>
                <div class="actionkit-inspector-title">${escapeHtml(node.type ?? '--')}</div>
                <div class="actionkit-inspector-path">${renderActionKitBreadcrumb(selectedPath)}</div>
            </div>
            <span class="actionkit-inspector-status">${escapeHtml(node.paused ? t('actionkit.paused_state') : node.deinited ? t('actionkit.released') : node.status)}</span>
        </div>
        <div class="actionkit-inspector-grid">
            <div><span>ID</span><strong>${escapeHtml(node.id)}</strong></div>
            <div><span>${t('actionkit.executor')}</span><strong>${escapeHtml(node.executorName || 'PlayerLoop')}</strong></div>
            <div><span>${t('actionkit.update_mode')}</span><strong>${escapeHtml(node.updateMode || '--')}</strong></div>
            <div><span>${t('actionkit.child_nodes')}</span><strong>${escapeHtml(node.childCount)}</strong></div>
            <div><span>${t('actionkit.current_index')}</span><strong>${escapeHtml(node.currentChildIndex >= 0 ? node.currentChildIndex : '--')}</strong></div>
            <div><span>${t('actionkit.stack')}</span><strong>${escapeHtml(node.stackTrace?.frameCount > 0 ? t('actionkit.stack_frames', node.stackTrace.frameCount) : t('actionkit.stack_not_recorded'))}</strong></div>
        </div>
        <div class="actionkit-inspector-debug">${escapeHtml(node.debugInfo ?? '--')}</div>
        <div class="actionkit-inspector-stack">
            ${renderActionKitStackTrace(node.stackTrace)}
        </div>
    </section>`;
}

function renderActionKitStackTrace(stackTrace) {
    if (!stackTrace || !Array.isArray(stackTrace.frames) || !stackTrace.frames.length) {
        return emptyState('action', t('actionkit.no_stack_recorded'));
    }

    return `<div class="actionkit-stack-list">${stackTrace.frames.map((frame, index) => {
        const location = frame.filePath ? renderKitCodeJumpButton(frame.filePath, frame.line || 1, frame.filePath.split('/').pop() || frame.filePath, 'actionkit-stack-list__jump') : `<span class="kit-code-jump kit-code-jump--missing">${t('actionkit.source_not_recorded')}</span>`;
        return `<div class="actionkit-stack-row">
            <span class="actionkit-stack-row__index">#${escapeHtml(index + 1)}</span>
            <span class="actionkit-stack-row__method">${escapeHtml(frame.method || '--')}</span>
            <span class="actionkit-stack-row__location">${location}</span>
        </div>`;
    }).join('')}</div>`;
}

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
            renderActionKitWorkbenchFromState();
            event.stopPropagation();
        });
    });
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

