// pages/graphkit-selection.js
// GraphKit multi-selection, marquee selection and local clipboard helpers.
function getGraphKitSelectedNodeIds() {
    const nodeIds = new Set(graphKitProject.graph.nodes.map(node => node.id));
    const rawIds = Array.isArray(graphKitState.selectedNodeIds) ? graphKitState.selectedNodeIds : [];
    const ids = rawIds.filter(id => nodeIds.has(id));
    if (!ids.length && nodeIds.has(graphKitState.selectedNodeId)) ids.push(graphKitState.selectedNodeId);
    graphKitState.selectedNodeIds = [...new Set(ids)];
    if (!graphKitState.selectedNodeIds.includes(graphKitState.selectedNodeId)) {
        graphKitState.selectedNodeId = graphKitState.selectedNodeIds[0] || '';
    }
    return graphKitState.selectedNodeIds;
}

function isGraphKitNodeSelected(nodeId) {
    return getGraphKitSelectedNodeIds().includes(nodeId);
}

function selectGraphKitNode(nodeId, append = false) {
    if (append) {
        toggleGraphKitNodeSelection(nodeId);
        return;
    }
    setGraphKitSelectedNodeIds(nodeId ? [nodeId] : [], nodeId);
    renderGraphKitWorkbench();
}

function toggleGraphKitNodeSelection(nodeId) {
    const ids = getGraphKitSelectedNodeIds();
    const nextIds = ids.includes(nodeId) ? ids.filter(id => id !== nodeId) : [...ids, nodeId];
    setGraphKitSelectedNodeIds(nextIds, nodeId);
    renderGraphKitWorkbench();
}

function setGraphKitSelectedNodeIds(nodeIds, primaryId) {
    const nodeIdSet = new Set(graphKitProject.graph.nodes.map(node => node.id));
    const ids = [...new Set((nodeIds || []).filter(id => nodeIdSet.has(id)))];
    graphKitState.selectedNodeIds = ids;
    graphKitState.selectedNodeId = ids.includes(primaryId) ? primaryId : ids[0] || '';
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNoteId = '';
    graphKitState.selectedPlacematId = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.pendingPortEndpoint = '';
}

function clearGraphKitNodeSelection() {
    setGraphKitSelectedNodeIds([], '');
    renderGraphKitWorkbench();
}

function selectAllGraphKitNodes() {
    const ids = graphKitProject.graph.nodes.map(node => node.id);
    setGraphKitSelectedNodeIds(ids, ids[0] || '');
    setGraphKitNotice('info', `已选择 ${ids.length} 个节点。`);
    renderGraphKitWorkbench();
}

function copyGraphKitSelectedNodes(showNotice = true) {
    const ids = getGraphKitSelectedNodeIds();
    if (!ids.length) return false;
    const idSet = new Set(ids);
    graphKitState.clipboardNodes = graphKitProject.graph.nodes
        .filter(node => idSet.has(node.id))
        .map(node => ({ ...node, fields: { ...(node.fields || {}) } }));
    graphKitState.clipboardEdges = graphKitProject.graph.edges
        .filter(edge => isGraphKitInternalEdge(edge, idSet))
        .map(edge => ({ ...edge }));
    if (showNotice) {
        setGraphKitNotice('success', `已复制 ${graphKitState.clipboardNodes.length} 个节点。`);
        renderGraphKitWorkbench();
    }
    return true;
}

function pasteGraphKitClipboardNodes() {
    const nodes = Array.isArray(graphKitState.clipboardNodes) ? graphKitState.clipboardNodes : [];
    if (!nodes.length) return false;
    const payload = makeGraphKitNodeCopyPayload(nodes, graphKitState.clipboardEdges || [], 42, 28);
    commitGraphKitMutation('Paste nodes', before => ({
        ...before,
        graph: {
            ...before.graph,
            nodes: [...before.graph.nodes, ...payload.nodes],
            edges: [...before.graph.edges, ...payload.edges],
        },
    }), {
        selectedNodeId: payload.nodes[0]?.id || '',
        selectedNodeIds: payload.nodes.map(node => node.id),
        clearPendingPort: true,
        noticeLevel: 'success',
        noticeText: `已粘贴 ${payload.nodes.length} 个节点。`,
    });
    return true;
}

function duplicateGraphKitSelectedNodes() {
    const ids = getGraphKitSelectedNodeIds();
    if (ids.length <= 1) {
        duplicateGraphKitSelectedNode();
        return;
    }
    const idSet = new Set(ids);
    const nodes = graphKitProject.graph.nodes.filter(node => idSet.has(node.id));
    const edges = graphKitProject.graph.edges.filter(edge => isGraphKitInternalEdge(edge, idSet));
    const payload = makeGraphKitNodeCopyPayload(nodes, edges, 72, 36);
    commitGraphKitMutation('Duplicate nodes', before => ({
        ...before,
        graph: {
            ...before.graph,
            nodes: [...before.graph.nodes, ...payload.nodes],
            edges: [...before.graph.edges, ...payload.edges],
        },
    }), {
        selectedNodeId: payload.nodes[0]?.id || '',
        selectedNodeIds: payload.nodes.map(node => node.id),
        clearPendingPort: true,
        noticeLevel: 'success',
        noticeText: `已复制 ${payload.nodes.length} 个节点。`,
    });
}

function deleteGraphKitSelectedNodes() {
    const ids = getGraphKitSelectedNodeIds();
    if (ids.length <= 1) return false;
    const idSet = new Set(ids);
    const nextSelection = graphKitProject.graph.nodes.find(node => !idSet.has(node.id))?.id || '';
    commitGraphKitMutation('Delete nodes', before => ({
        ...before,
        graph: {
            ...before.graph,
            nodes: before.graph.nodes.filter(node => !idSet.has(node.id)),
            edges: before.graph.edges.filter(edge => {
                const from = parseGraphKitEndpoint(edge.from);
                const to = parseGraphKitEndpoint(edge.to);
                return !idSet.has(from.nodeId) && !idSet.has(to.nodeId);
            }),
            placemats: before.graph.placemats.map(item => ({
                ...item,
                nodeIds: item.nodeIds.filter(nodeId => !idSet.has(nodeId)),
            })),
        },
    }), {
        selectedNodeId: nextSelection,
        selectedNodeIds: nextSelection ? [nextSelection] : [],
        noticeLevel: 'warning',
        noticeText: `已删除 ${ids.length} 个节点，并移除相关连线。`,
    });
    return true;
}

function alignGraphKitSelectedNodes(mode) {
    const ids = getGraphKitSelectedNodeIds();
    if (ids.length < 2) {
        setGraphKitNotice('warning', '至少选择 2 个节点才能对齐。');
        renderGraphKitWorkbench();
        return false;
    }
    commitGraphKitMutation(`Align ${mode}`, before => alignGraphKitProjectNodes(before, ids, mode), {
        selectedNodeId: graphKitState.selectedNodeId || ids[0],
        selectedNodeIds: ids,
        clearPendingPort: true,
        noticeLevel: 'success',
        noticeText: `已对齐 ${ids.length} 个节点。`,
    });
    return true;
}

function distributeGraphKitSelectedNodes(axis) {
    const ids = getGraphKitSelectedNodeIds();
    if (ids.length < 3) {
        setGraphKitNotice('warning', '至少选择 3 个节点才能分布。');
        renderGraphKitWorkbench();
        return false;
    }
    commitGraphKitMutation(`Distribute ${axis}`, before => distributeGraphKitProjectNodes(before, ids, axis), {
        selectedNodeId: graphKitState.selectedNodeId || ids[0],
        selectedNodeIds: ids,
        clearPendingPort: true,
        noticeLevel: 'success',
        noticeText: `已分布 ${ids.length} 个节点。`,
    });
    return true;
}

function handleGraphKitLayoutAction(action) {
    const text = String(action || '');
    if (text.startsWith('align-')) return alignGraphKitSelectedNodes(text.slice('align-'.length));
    if (text === 'distribute-horizontal') return distributeGraphKitSelectedNodes('horizontal');
    if (text === 'distribute-vertical') return distributeGraphKitSelectedNodes('vertical');
    return false;
}

function bindGraphKitLayoutActions() {
    $pageBody.querySelectorAll('[data-graphkit-layout-action]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => handleGraphKitLayoutAction(button.dataset.graphkitLayoutAction));
    });
}

function renderGraphKitLayoutTools(selectedCount) {
    const alignDisabled = selectedCount < 2;
    const distributeDisabled = selectedCount < 3;
    const alignDisabledAttr = alignDisabled ? 'disabled' : '';
    const distributeDisabledAttr = distributeDisabled ? 'disabled' : '';
    return `<div class="graphkit-layout-tools" aria-label="节点布局工具">
        <button class="graphkit-tool-button" type="button" data-graphkit-layout-action="align-left" title="左对齐" aria-label="左对齐" ${alignDisabledAttr}>L</button>
        <button class="graphkit-tool-button" type="button" data-graphkit-layout-action="align-center" title="水平居中对齐" aria-label="水平居中对齐" ${alignDisabledAttr}>C</button>
        <button class="graphkit-tool-button" type="button" data-graphkit-layout-action="align-right" title="右对齐" aria-label="右对齐" ${alignDisabledAttr}>R</button>
        <button class="graphkit-tool-button" type="button" data-graphkit-layout-action="align-top" title="顶部对齐" aria-label="顶部对齐" ${alignDisabledAttr}>T</button>
        <button class="graphkit-tool-button" type="button" data-graphkit-layout-action="align-middle" title="垂直居中对齐" aria-label="垂直居中对齐" ${alignDisabledAttr}>M</button>
        <button class="graphkit-tool-button" type="button" data-graphkit-layout-action="align-bottom" title="底部对齐" aria-label="底部对齐" ${alignDisabledAttr}>B</button>
        <button class="graphkit-tool-button" type="button" data-graphkit-layout-action="distribute-horizontal" title="水平分布" aria-label="水平分布" ${distributeDisabledAttr}>H</button>
        <button class="graphkit-tool-button" type="button" data-graphkit-layout-action="distribute-vertical" title="垂直分布" aria-label="垂直分布" ${distributeDisabledAttr}>V</button>
    </div>`;
}

function makeGraphKitNodeCopyPayload(sourceNodes, sourceEdges, offsetX, offsetY) {
    const usedNodeIds = new Set(graphKitProject.graph.nodes.map(node => node.id));
    const usedEdgeIds = new Set(graphKitProject.graph.edges.map(edge => edge.id));
    const nodeIdMap = new Map();
    const nodes = sourceNodes.map(node => {
        const id = makeUniqueGraphKitIdInSet(`${node.id}_copy`, usedNodeIds, 'node');
        nodeIdMap.set(node.id, id);
        return {
            ...node,
            id,
            x: normalizeFiniteGraphKitNumber(node.x, 0) + offsetX,
            y: normalizeFiniteGraphKitNumber(node.y, 0) + offsetY,
            fields: { ...(node.fields || {}) },
        };
    });
    const edges = sourceEdges.map(edge => {
        const from = remapGraphKitEndpoint(edge.from, nodeIdMap);
        const to = remapGraphKitEndpoint(edge.to, nodeIdMap);
        if (!from || !to) return null;
        const id = makeUniqueGraphKitIdInSet(`${from}_to_${to}`.replace(/\./g, '_'), usedEdgeIds, 'edge');
        return { ...edge, id, from, to };
    }).filter(Boolean);
    return { nodes, edges };
}

function makeUniqueGraphKitIdInSet(baseId, usedIds, fallback) {
    const base = normalizeGraphKitId(baseId, fallback);
    let id = base;
    let index = 2;
    while (usedIds.has(id)) {
        id = `${base}_${index}`;
        index += 1;
    }
    usedIds.add(id);
    return id;
}

function remapGraphKitEndpoint(endpoint, nodeIdMap) {
    const parsed = parseGraphKitEndpoint(endpoint);
    const nextNodeId = nodeIdMap.get(parsed.nodeId);
    return nextNodeId && parsed.portId ? `${nextNodeId}.${parsed.portId}` : '';
}

function isGraphKitInternalEdge(edge, nodeIds) {
    const from = parseGraphKitEndpoint(edge.from);
    const to = parseGraphKitEndpoint(edge.to);
    return nodeIds.has(from.nodeId) && nodeIds.has(to.nodeId);
}

function renderGraphKitSelectionRect() {
    const rect = getGraphKitMarqueeRect();
    if (!rect) return '';
    return `<rect class="graphkit-selection-rect" x="${escapeHtml(rect.x)}" y="${escapeHtml(rect.y)}" width="${escapeHtml(rect.width)}" height="${escapeHtml(rect.height)}"></rect>`;
}

function getGraphKitMarqueeRect() {
    const selection = graphKitState.marqueeSelection;
    if (!selection?.active) return null;
    const x = Math.min(selection.startX, selection.currentX);
    const y = Math.min(selection.startY, selection.currentY);
    const width = Math.abs(selection.currentX - selection.startX);
    const height = Math.abs(selection.currentY - selection.startY);
    return { x, y, width, height };
}

function bindGraphKitMarqueeSelection() {
    const viewport = $pageBody.querySelector('[data-graphkit-canvas-viewport]');
    if (!viewport || viewport.dataset.marqueeBound === '1') return;
    viewport.dataset.marqueeBound = '1';
    viewport.addEventListener('pointerdown', event => {
        if (event.button !== 0 || !event.shiftKey) return;
        const target = event.target;
        const interactiveTarget = target && typeof target.closest === 'function'
            ? target.closest('[data-graphkit-node], [data-graphkit-note], [data-graphkit-placemat], [data-graphkit-port], button, input, textarea, select, .graphkit-blackboard, .graphkit-inspector, .graphkit-bottom-toolbar, .graphkit-xml-dock')
            : null;
        if (interactiveTarget) return;
        const start = getGraphKitScenePointFromPointer(event, viewport);
        graphKitState.marqueeSelection = {
            active: true,
            pointerId: event.pointerId,
            startX: start.x,
            startY: start.y,
            currentX: start.x,
            currentY: start.y,
            append: event.ctrlKey || event.metaKey,
        };
        viewport.classList.add('is-selecting');
        viewport.setPointerCapture?.(event.pointerId);
        applyGraphKitMarqueeRect();
        event.preventDefault();
    });
    viewport.addEventListener('pointermove', event => {
        const selection = graphKitState.marqueeSelection;
        if (!selection?.active || selection.pointerId !== event.pointerId) return;
        const current = getGraphKitScenePointFromPointer(event, viewport);
        graphKitState.marqueeSelection = { ...selection, currentX: current.x, currentY: current.y };
        applyGraphKitMarqueeRect();
        event.preventDefault();
    });
    const finish = event => finishGraphKitMarqueeSelection(viewport, event);
    viewport.addEventListener('pointerup', finish);
    viewport.addEventListener('pointercancel', finish);
}

function finishGraphKitMarqueeSelection(viewport, event) {
    const selection = graphKitState.marqueeSelection;
    if (!selection?.active || selection.pointerId !== event.pointerId) return;
    const rect = getGraphKitMarqueeRect();
    const selectedIds = rect && (rect.width > 3 || rect.height > 3) ? getGraphKitNodesInRect(rect) : [];
    const nextIds = selection.append ? [...getGraphKitSelectedNodeIds(), ...selectedIds] : selectedIds;
    graphKitState.marqueeSelection = { ...selection, active: false };
    viewport.classList.remove('is-selecting');
    viewport.releasePointerCapture?.(event.pointerId);
    setGraphKitSelectedNodeIds(nextIds, selectedIds[0] || nextIds[0] || '');
    renderGraphKitWorkbench();
    event.preventDefault();
}

function applyGraphKitMarqueeRect() {
    const rect = getGraphKitMarqueeRect();
    if (!rect) return;
    const rectElement = getOrCreateGraphKitSelectionRectElement();
    if (!rectElement) return;
    rectElement.setAttribute('x', String(rect.x));
    rectElement.setAttribute('y', String(rect.y));
    rectElement.setAttribute('width', String(rect.width));
    rectElement.setAttribute('height', String(rect.height));
}

function getOrCreateGraphKitSelectionRectElement() {
    let rectElement = $pageBody.querySelector('.graphkit-selection-rect');
    if (rectElement) return rectElement;
    const scene = $pageBody.querySelector('[data-graphkit-scene]');
    if (!scene || typeof document.createElementNS !== 'function') return null;
    rectElement = document.createElementNS('http://www.w3.org/2000/svg', 'rect');
    rectElement.setAttribute('class', 'graphkit-selection-rect');
    scene.appendChild(rectElement);
    return rectElement;
}

function getGraphKitNodesInRect(rect) {
    return graphKitProject.graph.nodes
        .filter(node => graphKitRectIntersects(getGraphKitNodeSceneBounds(node), rect))
        .map(node => node.id);
}

function getGraphKitNodeSceneBounds(node) {
    const type = getGraphKitNodeType(node.type);
    const position = getGraphKitNodePosition(node);
    return {
        x: position.x,
        y: position.y,
        width: GRAPHKIT_NODE_WIDTH,
        height: getGraphKitNodeHeight(type, node),
    };
}

function graphKitRectIntersects(a, b) {
    return a.x <= b.x + b.width
        && a.x + a.width >= b.x
        && a.y <= b.y + b.height
        && a.y + a.height >= b.y;
}

function getGraphKitScenePointFromPointer(event, viewportElement) {
    const rect = viewportElement.getBoundingClientRect();
    const viewport = getGraphKitViewport();
    const svgX = (event.clientX - rect.left) / Math.max(1, rect.width) * GRAPHKIT_CANVAS_WIDTH;
    const svgY = (event.clientY - rect.top) / Math.max(1, rect.height) * GRAPHKIT_CANVAS_HEIGHT;
    return {
        x: roundGraphKitTransformNumber((svgX - viewport.offsetX) / viewport.scale),
        y: roundGraphKitTransformNumber((svgY - viewport.offsetY) / viewport.scale),
    };
}
