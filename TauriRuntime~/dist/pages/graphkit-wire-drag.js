// pages/graphkit-wire-drag.js
// GraphKit port-to-port wire drag preview and drop-to-connect workflow.
function renderGraphKitWireDragPreview() {
    const path = getGraphKitWireDragPreviewPath();
    if (!path) return '';
    const drag = normalizeGraphKitWireDragState(graphKitState.wireDrag);
    const compatible = drag.targetEndpoint ? ' is-compatible' : '';
    const reconnecting = drag.reconnectEdgeId ? ' is-reconnect' : '';
    return `<path class="graphkit-wire-preview${compatible}${reconnecting}" data-graphkit-wire-preview="${escapeHtml(drag.sourceEndpoint)}" d="${escapeHtml(path)}"></path>`;
}

function bindGraphKitWireDrag() {
    $pageBody.querySelectorAll('[data-graphkit-port]').forEach(port => {
        if (port.dataset.wireDragBound === '1') return;
        port.dataset.wireDragBound = '1';
        port.addEventListener('pointerdown', event => startGraphKitWireDrag(port, event));
    });
}

function startGraphKitWireDrag(portElement, event) {
    if (event.button !== 0 || event.shiftKey || event.ctrlKey || event.metaKey) return;
    const endpoint = portElement.dataset.graphkitPort || '';
    const source = getGraphKitPortDefinition(endpoint);
    if (!source) return;
    const point = getGraphKitWireDragPointerScenePoint(event);
    graphKitWireDrag = {
        pointerId: event.pointerId,
        sourceEndpoint: endpoint,
        sourceElement: portElement,
        moved: false,
        startClientX: event.clientX,
        startClientY: event.clientY,
    };
    graphKitState.wireDrag = {
        active: true,
        sourceEndpoint: endpoint,
        currentX: point.x,
        currentY: point.y,
        targetEndpoint: '',
    };
    portElement.classList.add('active');
    portElement.setPointerCapture?.(event.pointerId);
    document.addEventListener('pointermove', updateGraphKitWireDrag, true);
    document.addEventListener('pointerup', finishGraphKitWireDrag, true);
    document.addEventListener('pointercancel', cancelGraphKitWireDrag, true);
    applyGraphKitWireDragPreview();
    event.preventDefault();
    event.stopPropagation();
}

function startGraphKitWireReconnectDrag(handleElement, event) {
    if (event.button !== 0 || event.shiftKey || event.ctrlKey || event.metaKey) return;
    const edgeId = handleElement.dataset.graphkitEdgeId || '';
    const side = handleElement.dataset.graphkitEdgeSide || '';
    const edge = graphKitProject.graph.edges.find(candidate => candidate.id === edgeId);
    const fixedEndpoint = side === 'from' ? edge?.to : side === 'to' ? edge?.from : '';
    if (!edge || !getGraphKitPortDefinition(fixedEndpoint)) return;
    const point = getGraphKitWireDragPointerScenePoint(event);
    graphKitWireDrag = {
        pointerId: event.pointerId,
        sourceEndpoint: fixedEndpoint,
        sourceElement: handleElement,
        reconnectEdgeId: edgeId,
        reconnectSide: side,
        moved: false,
        startClientX: event.clientX,
        startClientY: event.clientY,
    };
    graphKitState.selectedEdgeId = edgeId;
    graphKitState.selectedNodeId = '';
    graphKitState.selectedNodeIds = [];
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNoteId = '';
    graphKitState.selectedPlacematId = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.wireDrag = {
        active: true,
        sourceEndpoint: fixedEndpoint,
        currentX: point.x,
        currentY: point.y,
        targetEndpoint: '',
        reconnectEdgeId: edgeId,
        reconnectSide: side,
    };
    handleElement.classList.add('active');
    handleElement.setPointerCapture?.(event.pointerId);
    document.addEventListener('pointermove', updateGraphKitWireDrag, true);
    document.addEventListener('pointerup', finishGraphKitWireDrag, true);
    document.addEventListener('pointercancel', cancelGraphKitWireDrag, true);
    applyGraphKitWireDragPreview();
    event.preventDefault();
    event.stopPropagation();
}

function updateGraphKitWireDrag(event) {
    if (!graphKitWireDrag || graphKitWireDrag.pointerId !== event.pointerId) return;
    const point = getGraphKitWireDragPointerScenePoint(event);
    const moved = Math.abs(event.clientX - graphKitWireDrag.startClientX) > 3 || Math.abs(event.clientY - graphKitWireDrag.startClientY) > 3;
    graphKitWireDrag.moved = graphKitWireDrag.moved || moved;
    graphKitState.wireDrag = {
        ...normalizeGraphKitWireDragState(graphKitState.wireDrag),
        currentX: point.x,
        currentY: point.y,
        targetEndpoint: getGraphKitWireDragTargetEndpoint(event),
    };
    applyGraphKitWireDragPreview();
    applyGraphKitWireDragTargetHighlight();
    event.preventDefault();
    event.stopPropagation();
}

function finishGraphKitWireDrag(event) {
    if (!graphKitWireDrag || graphKitWireDrag.pointerId !== event.pointerId) return;
    const drag = graphKitWireDrag;
    const state = normalizeGraphKitWireDragState(graphKitState.wireDrag);
    const sourceEndpoint = state.sourceEndpoint;
    const targetEndpoint = state.targetEndpoint || getGraphKitWireDragTargetEndpoint(event);
    const shouldConnect = drag.moved && sourceEndpoint && targetEndpoint;
    resetGraphKitWireDrag(event);
    if (drag.moved) suppressNextGraphKitPortClick();
    if (shouldConnect) {
        if (drag.reconnectEdgeId) reconnectGraphKitEdgeEndpoint(drag.reconnectEdgeId, drag.reconnectSide, targetEndpoint);
        else connectGraphKitPorts(sourceEndpoint, targetEndpoint);
    } else if (drag.moved) {
        setGraphKitNotice('info', '已取消拖拽连线。');
        renderGraphKitWorkbench();
    }
    event.preventDefault();
    event.stopPropagation();
}

function cancelGraphKitWireDrag(event) {
    if (!graphKitWireDrag || graphKitWireDrag.pointerId !== event.pointerId) return;
    const moved = graphKitWireDrag.moved;
    resetGraphKitWireDrag(event);
    if (moved) suppressNextGraphKitPortClick();
    if (moved) {
        setGraphKitNotice('info', '已取消拖拽连线。');
        renderGraphKitWorkbench();
    }
    event.preventDefault();
    event.stopPropagation();
}

function resetGraphKitWireDrag(event) {
    const endpoint = graphKitWireDrag?.sourceEndpoint || graphKitState.wireDrag?.sourceEndpoint || '';
    const sourceElement = endpoint ? $pageBody.querySelector(`[data-graphkit-port="${escapeGraphKitSelectorValue(endpoint)}"]`) : null;
    const pointerSourceElement = graphKitWireDrag?.sourceElement || sourceElement;
    sourceElement?.classList.remove('active');
    pointerSourceElement?.classList.remove('active');
    pointerSourceElement?.releasePointerCapture?.(event.pointerId);
    graphKitWireDrag = null;
    document.removeEventListener('pointermove', updateGraphKitWireDrag, true);
    document.removeEventListener('pointerup', finishGraphKitWireDrag, true);
    document.removeEventListener('pointercancel', cancelGraphKitWireDrag, true);
    graphKitState.wireDrag = getClosedGraphKitWireDrag();
    applyGraphKitWireDragTargetHighlight();
    const preview = $pageBody.querySelector('[data-graphkit-wire-preview]');
    preview?.remove();
}

function getClosedGraphKitWireDrag() {
    return {
        active: false,
        sourceEndpoint: '',
        currentX: 0,
        currentY: 0,
        targetEndpoint: '',
        reconnectEdgeId: '',
        reconnectSide: '',
    };
}

function normalizeGraphKitWireDragState(value) {
    const source = value && typeof value === 'object' ? value : {};
    return {
        active: Boolean(source.active),
        sourceEndpoint: String(source.sourceEndpoint || ''),
        currentX: normalizeFiniteGraphKitNumber(source.currentX, 0),
        currentY: normalizeFiniteGraphKitNumber(source.currentY, 0),
        targetEndpoint: String(source.targetEndpoint || ''),
        reconnectEdgeId: String(source.reconnectEdgeId || ''),
        reconnectSide: source.reconnectSide === 'from' || source.reconnectSide === 'to' ? source.reconnectSide : '',
    };
}

function getGraphKitWireDragPreviewPath() {
    const drag = normalizeGraphKitWireDragState(graphKitState.wireDrag);
    if (!drag.active || !drag.sourceEndpoint) return '';
    const source = getGraphKitPortDefinition(drag.sourceEndpoint);
    if (!source) return '';
    const sourcePoint = getGraphKitEndpointPosition(drag.sourceEndpoint, source.port.direction);
    const targetPoint = drag.targetEndpoint
        ? getGraphKitEndpointPosition(drag.targetEndpoint, getGraphKitOppositePortDirection(source.port.direction))
        : { x: drag.currentX, y: drag.currentY };
    if (!sourcePoint || !targetPoint) return '';
    const from = source.port.direction === 'output' ? sourcePoint : targetPoint;
    const to = source.port.direction === 'output' ? targetPoint : sourcePoint;
    const mid = Math.max(90, Math.abs(to.x - from.x) * 0.44);
    return `M ${roundGraphKitTransformNumber(from.x)} ${roundGraphKitTransformNumber(from.y)} C ${roundGraphKitTransformNumber(from.x + mid)} ${roundGraphKitTransformNumber(from.y)}, ${roundGraphKitTransformNumber(to.x - mid)} ${roundGraphKitTransformNumber(to.y)}, ${roundGraphKitTransformNumber(to.x)} ${roundGraphKitTransformNumber(to.y)}`;
}

function getGraphKitOppositePortDirection(direction) {
    return direction === 'input' ? 'output' : 'input';
}

function applyGraphKitWireDragPreview() {
    const path = getGraphKitWireDragPreviewPath();
    let preview = $pageBody.querySelector('[data-graphkit-wire-preview]');
    if (!path) {
        preview?.remove();
        return;
    }
    if (!preview) preview = createGraphKitWireDragPreviewElement();
    if (!preview) return;
    preview.setAttribute('d', path);
    preview.setAttribute('data-graphkit-wire-preview', graphKitState.wireDrag.sourceEndpoint);
    preview.classList.toggle('is-compatible', Boolean(graphKitState.wireDrag.targetEndpoint));
}

function createGraphKitWireDragPreviewElement() {
    const scene = $pageBody.querySelector('[data-graphkit-scene]');
    if (!scene || typeof document.createElementNS !== 'function') return null;
    const preview = document.createElementNS('http://www.w3.org/2000/svg', 'path');
    preview.setAttribute('class', 'graphkit-wire-preview');
    preview.setAttribute('data-graphkit-wire-preview', '');
    const firstNode = scene.querySelector('[data-graphkit-node]');
    if (firstNode) scene.insertBefore(preview, firstNode);
    else scene.appendChild(preview);
    return preview;
}

function getGraphKitWireDragTargetEndpoint(event) {
    const port = findGraphKitPortFromHitElements(getGraphKitWireDragHitElements(event));
    const endpoint = port?.dataset?.graphkitPort || '';
    const state = normalizeGraphKitWireDragState(graphKitState.wireDrag);
    if (!endpoint || endpoint === state.sourceEndpoint) return '';
    const connection = getGraphKitPortConnection(state.sourceEndpoint, endpoint);
    if (graphKitWireDrag?.reconnectEdgeId) {
        const edge = graphKitProject.graph.edges.find(candidate => candidate.id === graphKitWireDrag.reconnectEdgeId);
        const result = buildGraphKitReconnectedEdge(graphKitProject, edge, graphKitWireDrag.reconnectSide, endpoint);
        return result.ok ? endpoint : '';
    }
    return connection.ok ? endpoint : '';
}

function getGraphKitWireDragHitElements(event) {
    if (typeof document.elementsFromPoint === 'function') {
        return document.elementsFromPoint(event.clientX, event.clientY);
    }
    const target = document.elementFromPoint(event.clientX, event.clientY);
    return target ? [target] : [];
}

function findGraphKitPortFromHitElements(elements) {
    const hits = Array.isArray(elements) ? elements : [];
    for (const target of hits) {
        const port = target && typeof target.closest === 'function' ? target.closest('[data-graphkit-port]') : null;
        if (port) return port;
    }
    return null;
}

function applyGraphKitWireDragTargetHighlight() {
    const state = normalizeGraphKitWireDragState(graphKitState.wireDrag);
    $pageBody.querySelectorAll('[data-graphkit-port]').forEach(port => {
        const endpoint = port.dataset.graphkitPort || '';
        const compatible = state.active && endpoint === state.targetEndpoint;
        port.classList.toggle('is-compatible-target', compatible);
        if (compatible) port.setAttribute('data-graphkit-wire-compatible', 'true');
        else port.removeAttribute('data-graphkit-wire-compatible');
    });
}

function getGraphKitWireDragPointerScenePoint(event) {
    const viewport = $pageBody.querySelector('[data-graphkit-canvas-viewport]');
    if (!viewport) return { x: 0, y: 0 };
    return getGraphKitScenePointFromPointer(event, viewport);
}

function suppressNextGraphKitPortClick() {
    graphKitWireDragSuppressClick = true;
    window.setTimeout(() => {
        graphKitWireDragSuppressClick = false;
    }, 80);
}

function consumeGraphKitWireDragClickSuppress() {
    if (!graphKitWireDragSuppressClick) return false;
    graphKitWireDragSuppressClick = false;
    return true;
}
