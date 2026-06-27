// pages/actionkit-data.js
// ActionKit 数据归一化、搜索和选择状态
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
