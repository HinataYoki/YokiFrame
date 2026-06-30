// pages/graphkit-context-menu.js
// GraphKit context menus and search-driven node insertion.
function getClosedGraphKitContextMenu() {
    return {
        open: false,
        kind: '',
        x: 0,
        y: 0,
        modelX: 0,
        modelY: 0,
        targetId: '',
        endpoint: '',
        searchTerm: '',
    };
}

function normalizeGraphKitContextMenu(menu) {
    const source = menu && typeof menu === 'object' ? menu : {};
    return {
        open: Boolean(source.open),
        kind: String(source.kind || ''),
        x: normalizeFiniteGraphKitNumber(source.x, 0),
        y: normalizeFiniteGraphKitNumber(source.y, 0),
        modelX: normalizeFiniteGraphKitNumber(source.modelX, 0),
        modelY: normalizeFiniteGraphKitNumber(source.modelY, 0),
        targetId: String(source.targetId || ''),
        endpoint: String(source.endpoint || ''),
        searchTerm: String(source.searchTerm || ''),
    };
}

function renderGraphKitContextMenu() {
    const menu = normalizeGraphKitContextMenu(graphKitState.contextMenu);
    graphKitState.contextMenu = menu;
    if (!menu.open) return '';
    const types = renderGraphKitContextNodeTypes(menu);
    const actions = renderGraphKitContextActions(menu);
    return `<div class="graphkit-context-menu" data-graphkit-context="${escapeHtml(menu.kind)}" style="left:${escapeHtml(menu.x)}px;top:${escapeHtml(menu.y)}px;">
        <div class="graphkit-context-menu__head">
            <span>${escapeHtml(getGraphKitContextMenuTitle(menu))}</span>
            <button class="graphkit-panel-icon" type="button" data-graphkit-context-action="close" title="关闭菜单" aria-label="关闭菜单">×</button>
        </div>
        ${actions}
        ${types}
    </div>`;
}

function getGraphKitContextMenuTitle(menu) {
    if (menu.kind === 'node') return `Node · ${menu.targetId}`;
    if (menu.kind === 'edge') return `Wire · ${menu.targetId}`;
    if (menu.kind === 'note') return `Sticky Note · ${menu.targetId}`;
    if (menu.kind === 'placemat') return `Placemat · ${menu.targetId}`;
    if (menu.kind === 'port') return `Port · ${menu.endpoint}`;
    if (menu.kind === 'blackboard-var') return `Blackboard · ${menu.targetId}`;
    if (menu.kind === 'blackboard') return 'Blackboard';
    return 'Create Node';
}

function renderGraphKitContextActions(menu) {
    const rows = [];
    if (menu.kind === 'node') {
        rows.push(renderGraphKitContextAction('duplicate-node', '复制节点', 'Ctrl+D'));
        rows.push(renderGraphKitContextAction('delete-node', '删除节点', 'Delete'));
        const selectedCount = getGraphKitSelectedNodeIds().length;
        if (selectedCount > 1) {
            rows.push(renderGraphKitContextAction('align-left', '左对齐', 'Align'));
            rows.push(renderGraphKitContextAction('align-center', '水平居中', 'Align'));
            rows.push(renderGraphKitContextAction('align-right', '右对齐', 'Align'));
            rows.push(renderGraphKitContextAction('align-top', '顶部对齐', 'Align'));
            rows.push(renderGraphKitContextAction('align-middle', '垂直居中', 'Align'));
            rows.push(renderGraphKitContextAction('align-bottom', '底部对齐', 'Align'));
        }
        if (selectedCount > 2) {
            rows.push(renderGraphKitContextAction('distribute-horizontal', '水平分布', 'Space'));
            rows.push(renderGraphKitContextAction('distribute-vertical', '垂直分布', 'Space'));
        }
    } else if (menu.kind === 'edge') {
        rows.push(renderGraphKitContextAction('delete-edge', '删除连线', 'Delete'));
    } else if (menu.kind === 'note') {
        rows.push(renderGraphKitContextAction('delete-organization', '删除备注', 'Delete'));
    } else if (menu.kind === 'placemat') {
        rows.push(renderGraphKitContextAction('delete-organization', '删除分组', 'Delete'));
    } else if (menu.kind === 'port') {
        rows.push(renderGraphKitContextAction('start-connection', '从端口开始连线', menu.endpoint));
        if (graphKitState.pendingPortEndpoint) rows.push(renderGraphKitContextAction('cancel-connection', '取消待连接端口', graphKitState.pendingPortEndpoint));
    } else if (menu.kind === 'blackboard-var') {
        rows.push(renderGraphKitContextAction('add-blackboard-var', '添加变量', 'Blackboard'));
        rows.push(renderGraphKitContextAction('delete-blackboard-var', '删除变量', menu.targetId));
    } else {
        rows.push(renderGraphKitContextAction('add-blackboard-var', '添加 Blackboard 变量', 'Blackboard'));
        rows.push(renderGraphKitContextAction('add-note', '添加 Sticky Note', 'Note'));
        rows.push(renderGraphKitContextAction('add-placemat', '添加 Placemat', 'Group'));
        rows.push(renderGraphKitContextAction('fit-view', '适配全部节点', 'Fit'));
        rows.push(renderGraphKitContextAction('reset-view', '重置视图', '100%'));
    }
    return rows.length ? `<div class="graphkit-context-menu__actions">${rows.join('')}</div>` : '';
}

function renderGraphKitContextAction(action, label, meta) {
    return `<button class="graphkit-context-action" type="button" data-graphkit-context-action="${escapeHtml(action)}">
        <span>${escapeHtml(label)}</span>
        <em>${escapeHtml(meta || '')}</em>
    </button>`;
}

function renderGraphKitContextNodeTypes(menu) {
    if (menu.kind === 'note' || menu.kind === 'placemat' || menu.kind === 'blackboard-var' || menu.kind === 'blackboard') return '';
    const types = getGraphKitContextNodeTypes();
    const title = menu.kind === 'edge' ? 'Insert Node on Wire' : menu.kind === 'port' ? 'Create Connected Node' : 'Node Library';
    const rows = types.length
        ? types.map(type => renderGraphKitContextNodeType(type)).join('')
        : `<div class="graphkit-empty-line">没有匹配的节点类型。</div>`;
    return `<div class="graphkit-context-menu__library">
        <label class="graphkit-context-menu__search">
            <span>${escapeHtml(title)}</span>
            <input class="cmd-input" type="search" data-graphkit-context-search value="${escapeHtml(menu.searchTerm)}" placeholder="Search type / handler">
        </label>
        <div class="graphkit-context-menu__types">${rows}</div>
    </div>`;
}

function renderGraphKitContextNodeType(type) {
    return `<button class="graphkit-context-type" type="button" data-graphkit-context-type="${escapeHtml(type.id)}">
        <span class="graphkit-node-type__color" style="--node-color:${escapeHtml(type.color)}"></span>
        <span>
            <strong>${escapeHtml(type.title)}</strong>
            <em>${escapeHtml(type.category)} · ${escapeHtml(type.id)}</em>
        </span>
        <code>${escapeHtml(formatGraphKitNodeLabel(type.handlerId || 'missing handler', 22))}</code>
    </button>`;
}

function getGraphKitContextNodeTypes() {
    const menu = normalizeGraphKitContextMenu(graphKitState.contextMenu);
    const query = String(menu.searchTerm || '').trim().toLowerCase();
    return graphKitProject.nodeTypes.filter(type => {
        const matchesQuery = !query || `${type.id} ${type.title} ${type.category} ${type.handlerId}`.toLowerCase().includes(query);
        return matchesQuery && isGraphKitContextNodeTypeCompatible(type, menu);
    });
}

function isGraphKitContextNodeTypeCompatible(type, menu) {
    if (menu.kind === 'edge') {
        const edge = graphKitProject.graph.edges.find(candidate => candidate.id === menu.targetId);
        return isGraphKitNodeTypeInsertableOnEdge(graphKitProject, edge, type.id);
    }
    if (menu.kind !== 'port') return true;
    const source = getGraphKitPortDefinition(menu.endpoint);
    if (!source) return false;
    if (getGraphKitPortCapacityIssue(graphKitProject, source.endpoint)) return false;
    return (type.ports || []).some(port => port.kind === source.port.kind && port.direction !== source.port.direction);
}

function bindGraphKitContextMenu() {
    const windowElement = $pageBody.querySelector('[data-graphkit-window]');
    if (windowElement && windowElement.dataset.contextMenuBound !== '1') {
        windowElement.dataset.contextMenuBound = '1';
        windowElement.addEventListener('contextmenu', handleGraphKitContextMenuEvent);
    }

    const search = $pageBody.querySelector('[data-graphkit-context-search]');
    if (search && search.dataset.bound !== '1') {
        search.dataset.bound = '1';
        search.addEventListener('input', () => applyGraphKitContextSearch(search.value));
    }

    bindGraphKitContextTypeButtons();
    bindGraphKitContextActionButtons();
    bindGraphKitContextMenuGlobalListeners();
}

function bindGraphKitContextTypeButtons() {
    $pageBody.querySelectorAll('[data-graphkit-context-type]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => createGraphKitNodeFromContext(button.dataset.graphkitContextType));
    });
}

function bindGraphKitContextActionButtons() {
    $pageBody.querySelectorAll('[data-graphkit-context-action]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => handleGraphKitContextAction(button.dataset.graphkitContextAction));
    });
}

function applyGraphKitContextSearch(value) {
    graphKitState.contextMenu = {
        ...normalizeGraphKitContextMenu(graphKitState.contextMenu),
        searchTerm: value,
    };
    const list = $pageBody.querySelector('.graphkit-context-menu__types');
    if (!list) return;
    const types = getGraphKitContextNodeTypes();
    list.innerHTML = types.length
        ? types.map(type => renderGraphKitContextNodeType(type)).join('')
        : `<div class="graphkit-empty-line">没有匹配的节点类型。</div>`;
    bindGraphKitContextTypeButtons();
}

function bindGraphKitContextMenuGlobalListeners() {
    const root = document.documentElement || document.body;
    if (root.dataset.graphkitContextMenuGlobalBound === '1') return;
    root.dataset.graphkitContextMenuGlobalBound = '1';
    document.addEventListener('click', event => {
        if (activePage !== 'graphkit') return;
        const target = event.target;
        if (target && typeof target.closest === 'function' && target.closest('[data-graphkit-context]')) return;
        if (normalizeGraphKitContextMenu(graphKitState.contextMenu).open) closeGraphKitContextMenu();
    });
    document.addEventListener('keydown', event => {
        if (activePage !== 'graphkit' || event.key !== 'Escape') return;
        if (normalizeGraphKitContextMenu(graphKitState.contextMenu).open) {
            closeGraphKitContextMenu();
            event.preventDefault();
        }
    });
}

function handleGraphKitContextMenuEvent(event) {
    const target = event.target;
    if (!target || typeof target.closest !== 'function') return;
    if (target.closest('[data-graphkit-context]')) return;
    const port = target.closest('[data-graphkit-port]');
    const edge = target.closest('[data-graphkit-edge]');
    const note = target.closest('[data-graphkit-note]');
    const placemat = target.closest('[data-graphkit-placemat]');
    const node = target.closest('[data-graphkit-node]');
    const blackboardVar = target.closest('[data-graphkit-blackboard-var]');
    const blackboard = target.closest('.graphkit-blackboard');
    const viewport = target.closest('[data-graphkit-canvas-viewport]');
    const blocked = target.closest('.graphkit-inspector, .graphkit-minimap, .graphkit-issues-overlay, .graphkit-xml-dock, .kit-toolbar');
    if (blocked && !blackboard) return;

    if (port) openGraphKitContextMenu('port', event, { endpoint: port.dataset.graphkitPort || '' });
    else if (edge) openGraphKitContextMenu('edge', event, { targetId: edge.dataset.graphkitEdge || '' });
    else if (note) openGraphKitContextMenu('note', event, { targetId: note.dataset.graphkitNote || '' });
    else if (placemat) openGraphKitContextMenu('placemat', event, { targetId: placemat.dataset.graphkitPlacemat || '' });
    else if (node) openGraphKitContextMenu('node', event, { targetId: node.dataset.graphkitNode || '' });
    else if (blackboardVar) openGraphKitContextMenu('blackboard-var', event, { targetId: blackboardVar.dataset.graphkitBlackboardVar || '' });
    else if (blackboard) openGraphKitContextMenu('blackboard', event, {});
    else if (viewport) openGraphKitContextMenu('canvas', event, {});
    else return;

    event.preventDefault();
    event.stopPropagation();
}

function openGraphKitContextMenu(kind, event, details = {}) {
    const screen = getGraphKitContextMenuScreenPosition(event);
    const model = getGraphKitContextMenuModelPosition(event);
    graphKitState.contextMenu = normalizeGraphKitContextMenu({
        open: true,
        kind,
        x: screen.x,
        y: screen.y,
        modelX: model.x,
        modelY: model.y,
        targetId: details.targetId || '',
        endpoint: details.endpoint || '',
        searchTerm: '',
    });
    selectGraphKitContextTarget(kind, details);
    renderGraphKitWorkbench();
}

function selectGraphKitContextTarget(kind, details) {
    if (kind === 'node') {
        const targetId = details.targetId || '';
        const selectedIds = getGraphKitSelectedNodeIds();
        setGraphKitSelectedNodeIds(selectedIds.includes(targetId) ? selectedIds : [targetId], targetId);
    } else if (kind === 'edge') {
        graphKitState.selectedEdgeId = details.targetId || '';
        graphKitState.selectedNodeId = '';
        graphKitState.selectedNodeIds = [];
        graphKitState.selectedBlackboardName = '';
        graphKitState.selectedNoteId = '';
        graphKitState.selectedPlacematId = '';
        graphKitState.selectedNodeTypeId = '';
    } else if (kind === 'note') {
        graphKitState.selectedNoteId = details.targetId || '';
        graphKitState.selectedPlacematId = '';
        graphKitState.selectedNodeId = '';
        graphKitState.selectedNodeIds = [];
        graphKitState.selectedEdgeId = '';
        graphKitState.selectedBlackboardName = '';
        graphKitState.selectedNodeTypeId = '';
    } else if (kind === 'placemat') {
        graphKitState.selectedPlacematId = details.targetId || '';
        graphKitState.selectedNoteId = '';
        graphKitState.selectedNodeId = '';
        graphKitState.selectedNodeIds = [];
        graphKitState.selectedEdgeId = '';
        graphKitState.selectedBlackboardName = '';
        graphKitState.selectedNodeTypeId = '';
    } else if (kind === 'blackboard-var') {
        graphKitState.selectedBlackboardName = details.targetId || '';
        graphKitState.selectedNodeId = '';
        graphKitState.selectedNodeIds = [];
        graphKitState.selectedEdgeId = '';
        graphKitState.selectedNoteId = '';
        graphKitState.selectedPlacematId = '';
        graphKitState.selectedNodeTypeId = '';
    }
}

function getGraphKitContextMenuScreenPosition(event) {
    const shell = $pageBody.querySelector('.graphkit-editor-shell') || $pageBody;
    const rect = shell.getBoundingClientRect();
    const maxX = Math.max(12, rect.width - 328);
    const maxY = Math.max(12, rect.height - 420);
    return {
        x: clampGraphKitNumber(event.clientX - rect.left, 12, 12, maxX),
        y: clampGraphKitNumber(event.clientY - rect.top, 12, 12, maxY),
    };
}

function getGraphKitContextMenuModelPosition(event) {
    return getGraphKitModelPositionFromPointer(event);
}

function handleGraphKitContextAction(action) {
    const menu = normalizeGraphKitContextMenu(graphKitState.contextMenu);
    if (action === 'close') {
        closeGraphKitContextMenu();
    } else if (action === 'duplicate-node') {
        closeGraphKitContextMenu(false);
        duplicateGraphKitSelectedNodes();
    } else if (action === 'delete-node') {
        closeGraphKitContextMenu(false);
        if (!deleteGraphKitSelectedNodes()) deleteGraphKitSelectedNode();
    } else if (action === 'delete-edge') {
        graphKitState.selectedEdgeId = menu.targetId;
        closeGraphKitContextMenu(false);
        deleteGraphKitSelectedEdge();
    } else if (action === 'add-blackboard-var') {
        closeGraphKitContextMenu(false);
        createGraphKitBlackboardVariable();
    } else if (action === 'add-note') {
        closeGraphKitContextMenu(false);
        createGraphKitStickyNote({ x: menu.modelX, y: menu.modelY });
    } else if (action === 'add-placemat') {
        closeGraphKitContextMenu(false);
        createGraphKitPlacemat({ x: menu.modelX, y: menu.modelY });
    } else if (action === 'delete-blackboard-var') {
        graphKitState.selectedBlackboardName = menu.targetId;
        closeGraphKitContextMenu(false);
        deleteGraphKitSelectedBlackboardVariable();
    } else if (action === 'fit-view') {
        closeGraphKitContextMenu(false);
        fitGraphKitViewportToNodes();
        renderGraphKitWorkbench();
    } else if (action === 'reset-view') {
        closeGraphKitContextMenu(false);
        updateGraphKitViewport({ scale: 1, offsetX: 0, offsetY: 0 });
        renderGraphKitWorkbench();
    } else if (action === 'start-connection') {
        graphKitState.pendingPortEndpoint = menu.endpoint;
        graphKitState.selectedNodeIds = [];
        closeGraphKitContextMenu(false);
        setGraphKitNotice('info', `选择目标端口以连接 ${menu.endpoint}。`);
        renderGraphKitWorkbench();
    } else if (action === 'cancel-connection') {
        graphKitState.pendingPortEndpoint = '';
        closeGraphKitContextMenu(false);
        setGraphKitNotice('info', '已取消端口连接。');
        renderGraphKitWorkbench();
    } else if (action.startsWith('align-') || action.startsWith('distribute-')) {
        closeGraphKitContextMenu(false);
        handleGraphKitLayoutAction(action);
    } else if (action === 'delete-organization') {
        closeGraphKitContextMenu(false);
        deleteGraphKitSelectedOrganizationItem();
    }
}

function createGraphKitNodeFromContext(typeId) {
    const menu = normalizeGraphKitContextMenu(graphKitState.contextMenu);
    const type = getGraphKitNodeType(typeId) || getGraphKitDefaultCreatableNodeType();
    if (!type) return false;
    if (menu.kind === 'edge') {
        closeGraphKitContextMenu(false);
        return insertGraphKitNodeOnEdge(menu.targetId, type.id, { x: menu.modelX, y: menu.modelY });
    }
    const source = menu.kind === 'port' ? getGraphKitPortDefinition(menu.endpoint) : null;
    const position = getGraphKitContextNodeModelPosition(menu, source);
    const newNodeId = makeUniqueGraphKitNodeId(type.id.replace(/\./g, '_'));
    const newNode = {
        id: newNodeId,
        type: type.id,
        x: position.x,
        y: position.y,
        fields: makeGraphKitDefaultNodeFields(type),
    };
    const edge = makeGraphKitContextEdge(source, type, newNodeId);
    closeGraphKitContextMenu(false);
    commitGraphKitMutation(edge ? 'Add connected node' : 'Add node', before => ({
        ...before,
        graph: {
            ...before.graph,
            nodes: [...before.graph.nodes, newNode],
            edges: edge ? [...before.graph.edges, edge] : before.graph.edges,
        },
    }), {
        selectedNodeId: newNodeId,
        clearPendingPort: true,
        noticeLevel: 'success',
        noticeText: edge ? `已添加并连接节点 ${type.title}。` : `已添加节点 ${type.title}。`,
    });
    return true;
}

function getGraphKitContextNodeModelPosition(menu, source) {
    let x = normalizeFiniteGraphKitNumber(menu.modelX, 120);
    const y = normalizeFiniteGraphKitNumber(menu.modelY, 120) - 32;
    if (source?.port?.direction === 'output') x += 72;
    if (source?.port?.direction === 'input') x -= 180;
    return {
        x: Math.round(x),
        y: Math.round(y),
    };
}

function makeGraphKitContextEdge(source, type, newNodeId) {
    if (!source) return null;
    const targetPort = (type.ports || []).find(port => port.kind === source.port.kind && port.direction !== source.port.direction);
    if (!targetPort) return null;
    const newEndpoint = `${newNodeId}.${targetPort.id}`;
    const from = source.port.direction === 'output' ? source.endpoint : newEndpoint;
    const to = source.port.direction === 'output' ? newEndpoint : source.endpoint;
    if (getGraphKitPortCapacityIssue(graphKitProject, source.endpoint)) return null;
    return {
        id: makeUniqueGraphKitEdgeId(from, to),
        from,
        to,
    };
}

function closeGraphKitContextMenu(shouldRender = true) {
    graphKitState.contextMenu = getClosedGraphKitContextMenu();
    if (shouldRender) renderGraphKitWorkbench();
}
