// pages/graphkit-helpers.js
// GraphKit selection, id, geometry and formatting helpers.
function getGraphKitSelectedNode() {
    const nodes = graphKitProject.graph.nodes;
    if (!graphKitState.selectedNodeId) return null;
    return nodes.find(node => node.id === graphKitState.selectedNodeId) || null;
}

function getGraphKitSelectedEdge() {
    if (!graphKitState.selectedEdgeId) return null;
    return graphKitProject.graph.edges.find(edge => edge.id === graphKitState.selectedEdgeId) || null;
}

function getGraphKitSelectedBlackboardVariable() {
    if (!graphKitState.selectedBlackboardName) return null;
    return graphKitProject.graph.blackboard.find(item => item.name === graphKitState.selectedBlackboardName) || null;
}

function getGraphKitSelectedNote() {
    if (!graphKitState.selectedNoteId) return null;
    return graphKitProject.graph.notes.find(item => item.id === graphKitState.selectedNoteId) || null;
}

function getGraphKitSelectedPlacemat() {
    if (!graphKitState.selectedPlacematId) return null;
    return graphKitProject.graph.placemats.find(item => item.id === graphKitState.selectedPlacematId) || null;
}

function getGraphKitSelectedNodeType() {
    if (!graphKitState.selectedNodeTypeId) return null;
    return graphKitProject.nodeTypes.find(type => type.id === graphKitState.selectedNodeTypeId) || null;
}

function getGraphKitDeleteSelectionLabel(selectedNode, selectedEdge, selectedBlackboard, selectedNote, selectedPlacemat) {
    if (selectedEdge) return '删除连线';
    if (selectedBlackboard) return '删除变量';
    if (selectedNote) return '删除备注';
    if (selectedPlacemat) return '删除分组';
    if (selectedNode) return '删除节点';
    return '删除';
}

function getGraphKitNodeType(typeId) {
    return graphKitProject.nodeTypes.find(type => type.id === typeId) || null;
}

function getGraphKitPortDefinition(endpoint) {
    const parsed = parseGraphKitEndpoint(endpoint);
    if (!parsed.nodeId || !parsed.portId) return null;
    const node = graphKitProject.graph.nodes.find(candidate => candidate.id === parsed.nodeId);
    if (!node) return null;
    const type = getGraphKitNodeType(node.type);
    const port = (type?.ports || []).find(candidate => candidate.id === parsed.portId);
    if (!port) return null;
    return { endpoint, node, type, port };
}

function getGraphKitPortConnection(firstEndpoint, secondEndpoint) {
    const first = getGraphKitPortDefinition(firstEndpoint);
    const second = getGraphKitPortDefinition(secondEndpoint);
    if (!first || !second) return { ok: false, message: '端口不存在，无法创建连线。' };

    const from = first.port.direction === 'output' ? first : second.port.direction === 'output' ? second : null;
    const to = first.port.direction === 'input' ? first : second.port.direction === 'input' ? second : null;
    if (!from || !to) return { ok: false, message: '连线必须从 output 端口指向 input 端口。' };
    if (from.port.kind !== to.port.kind) return { ok: false, message: `端口类型不一致：${from.port.kind} -> ${to.port.kind}。` };
    const duplicate = graphKitProject.graph.edges.some(edge => edge.from === from.endpoint && edge.to === to.endpoint);
    if (duplicate) return { ok: false, message: '该连线已经存在。' };
    const capacityIssue = getGraphKitPortConnectionCapacityIssue(graphKitProject, from.endpoint, to.endpoint);
    if (capacityIssue) return { ok: false, message: capacityIssue };
    return { ok: true, from: from.endpoint, to: to.endpoint };
}

function getGraphKitDefaultCreatableNodeType() {
    return graphKitProject.nodeTypes.find(type => type.id === 'dialogue.line')
        || graphKitProject.nodeTypes.find(type => type.id !== 'flow.start')
        || graphKitProject.nodeTypes[0]
        || null;
}

function makeGraphKitDefaultNodeFields(type) {
    const fields = {};
    (type?.fields || []).forEach(field => {
        fields[field.name] = field.defaultValue !== undefined ? field.defaultValue : '';
    });
    return fields;
}

function makeUniqueGraphKitNodeId(baseId) {
    const usedIds = new Set(graphKitProject.graph.nodes.map(node => node.id));
    let id = normalizeGraphKitId(baseId || 'node', 'node');
    let index = 2;
    while (usedIds.has(id)) {
        id = `${normalizeGraphKitId(baseId || 'node', 'node')}_${index}`;
        index += 1;
    }
    return id;
}

function makeUniqueGraphKitBlackboardName(baseName, usedNames) {
    const used = usedNames || new Set(graphKitProject.graph.blackboard.map(item => item.name));
    const base = normalizeGraphKitId(baseName || 'variable', 'variable');
    let name = base;
    let index = 2;
    while (used.has(name)) {
        name = `${base}_${index}`;
        index += 1;
    }
    return name;
}

function makeUniqueGraphKitEdgeId(fromEndpoint, toEndpoint) {
    const usedIds = new Set(graphKitProject.graph.edges.map(edge => edge.id));
    const baseId = normalizeGraphKitId(`${fromEndpoint}_to_${toEndpoint}`.replace(/\./g, '_'), 'edge');
    let id = baseId;
    let index = 2;
    while (usedIds.has(id)) {
        id = `${baseId}_${index}`;
        index += 1;
    }
    return id;
}

function makeUniqueGraphKitNoteId(baseId) {
    const usedIds = new Set(graphKitProject.graph.notes.map(item => item.id));
    return makeUniqueGraphKitIdInGraphKitSet(baseId || 'note', usedIds, 'note');
}

function makeUniqueGraphKitPlacematId(baseId) {
    const usedIds = new Set(graphKitProject.graph.placemats.map(item => item.id));
    return makeUniqueGraphKitIdInGraphKitSet(baseId || 'placemat', usedIds, 'placemat');
}

function makeUniqueGraphKitIdInGraphKitSet(baseId, usedIds, fallback) {
    const base = normalizeGraphKitId(baseId, fallback);
    let id = base;
    let index = 2;
    while (usedIds.has(id)) {
        id = `${base}_${index}`;
        index += 1;
    }
    return id;
}

function getGraphKitViewportCenterModelPosition() {
    const viewport = getGraphKitViewport();
    return {
        x: Math.round((GRAPHKIT_CANVAS_WIDTH * 0.5 - GRAPHKIT_NODE_ORIGIN_X - viewport.offsetX) / GRAPHKIT_NODE_MODEL_SCALE_X / viewport.scale),
        y: Math.round((GRAPHKIT_CANVAS_HEIGHT * 0.5 - GRAPHKIT_NODE_ORIGIN_Y - viewport.offsetY) / GRAPHKIT_NODE_MODEL_SCALE_Y / viewport.scale),
    };
}

function getGraphKitModelPositionFromPointer(event) {
    const viewportElement = $pageBody.querySelector('[data-graphkit-canvas-viewport]');
    const rect = viewportElement?.getBoundingClientRect();
    if (!rect || rect.width <= 0 || rect.height <= 0) return getGraphKitViewportCenterModelPosition();
    const viewport = getGraphKitViewport();
    const svgX = (event.clientX - rect.left) / rect.width * GRAPHKIT_CANVAS_WIDTH;
    const svgY = (event.clientY - rect.top) / rect.height * GRAPHKIT_CANVAS_HEIGHT;
    return {
        x: Math.round(((svgX - viewport.offsetX) / viewport.scale - GRAPHKIT_NODE_ORIGIN_X) / GRAPHKIT_NODE_MODEL_SCALE_X),
        y: Math.round(((svgY - viewport.offsetY) / viewport.scale - GRAPHKIT_NODE_ORIGIN_Y) / GRAPHKIT_NODE_MODEL_SCALE_Y),
    };
}

function getGraphKitMiniMapViewportRect() {
    const viewport = getGraphKitViewport();
    const visibleWidth = Math.min(GRAPHKIT_CANVAS_WIDTH, GRAPHKIT_CANVAS_WIDTH / viewport.scale);
    const visibleHeight = Math.min(GRAPHKIT_CANVAS_HEIGHT, GRAPHKIT_CANVAS_HEIGHT / viewport.scale);
    const maxX = Math.max(0, GRAPHKIT_CANVAS_WIDTH - visibleWidth);
    const maxY = Math.max(0, GRAPHKIT_CANVAS_HEIGHT - visibleHeight);
    return {
        x: roundGraphKitTransformNumber(Math.max(0, Math.min(maxX, -viewport.offsetX / viewport.scale))),
        y: roundGraphKitTransformNumber(Math.max(0, Math.min(maxY, -viewport.offsetY / viewport.scale))),
        width: roundGraphKitTransformNumber(visibleWidth),
        height: roundGraphKitTransformNumber(visibleHeight),
    };
}

function getGraphKitNodeBounds() {
    const nodes = Array.isArray(graphKitProject?.graph?.nodes) ? graphKitProject.graph.nodes : [];
    if (!nodes.length) return null;
    return nodes.reduce((bounds, node) => {
        const type = getGraphKitNodeType(node.type);
        const position = getGraphKitNodePosition(node);
        const nextBounds = {
            minX: Math.min(bounds.minX, position.x),
            minY: Math.min(bounds.minY, position.y),
            maxX: Math.max(bounds.maxX, position.x + GRAPHKIT_NODE_WIDTH),
            maxY: Math.max(bounds.maxY, position.y + getGraphKitNodeHeight(type, node)),
        };
        nextBounds.width = nextBounds.maxX - nextBounds.minX;
        nextBounds.height = nextBounds.maxY - nextBounds.minY;
        nextBounds.centerX = nextBounds.minX + nextBounds.width * 0.5;
        nextBounds.centerY = nextBounds.minY + nextBounds.height * 0.5;
        return nextBounds;
    }, {
        minX: Number.POSITIVE_INFINITY,
        minY: Number.POSITIVE_INFINITY,
        maxX: Number.NEGATIVE_INFINITY,
        maxY: Number.NEGATIVE_INFINITY,
        width: 0,
        height: 0,
        centerX: GRAPHKIT_CANVAS_WIDTH * 0.5,
        centerY: GRAPHKIT_CANVAS_HEIGHT * 0.5,
    });
}

function getGraphKitEndpointPosition(endpoint, direction) {
    const parsed = parseGraphKitEndpoint(endpoint);
    const node = graphKitProject.graph.nodes.find(candidate => candidate.id === parsed.nodeId);
    if (!node) return null;
    const type = getGraphKitNodeType(node.type);
    const nodePosition = getGraphKitNodePosition(node);
    const nodeHeight = getGraphKitNodeHeight(type, node);
    const ports = (type?.ports || []).filter(port => direction === 'input' ? port.direction === 'input' : port.direction !== 'input');
    const index = Math.max(0, ports.findIndex(port => port.id === parsed.portId));
    const y = getGraphKitPortY(ports.length || 1, index, nodeHeight);
    return {
        x: nodePosition.x + (direction === 'input' ? 0 : GRAPHKIT_NODE_WIDTH),
        y: nodePosition.y + y,
    };
}

function getGraphKitNodePosition(node) {
    return {
        x: GRAPHKIT_NODE_ORIGIN_X + normalizeFiniteGraphKitNumber(node?.x, 0) * GRAPHKIT_NODE_MODEL_SCALE_X,
        y: GRAPHKIT_NODE_ORIGIN_Y + normalizeFiniteGraphKitNumber(node?.y, 0) * GRAPHKIT_NODE_MODEL_SCALE_Y,
    };
}

function getGraphKitNodeTransform(node) {
    const position = getGraphKitNodePosition(node);
    return `translate(${roundGraphKitTransformNumber(position.x)}, ${roundGraphKitTransformNumber(position.y)})`;
}

function getGraphKitPanels() {
    graphKitState.panels = normalizeGraphKitPanels(graphKitState.panels);
    return graphKitState.panels;
}

function normalizeGraphKitPanels(panels) {
    return {
        blackboardCollapsed: Boolean(panels?.blackboardCollapsed),
        inspectorCollapsed: Boolean(panels?.inspectorCollapsed),
    };
}

function escapeGraphKitSelectorValue(value) {
    return String(value ?? '').replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}

function getGraphKitNodeHeight(type, node) {
    if (node?.collapsed) return GRAPHKIT_NODE_COLLAPSED_HEIGHT;
    const fields = Array.isArray(type?.fields) ? type.fields : [];
    return Math.max(GRAPHKIT_NODE_MIN_HEIGHT, GRAPHKIT_NODE_HEADER_HEIGHT + 38 + fields.length * GRAPHKIT_NODE_FIELD_HEIGHT);
}

function getGraphKitNodeFieldRows(node, type) {
    const fields = Array.isArray(type?.fields) ? type.fields : [];
    return fields.slice(0, 4).map(field => ({
        label: field.title || field.name,
        value: node.fields?.[field.name] ?? field.defaultValue ?? '',
    }));
}

function getGraphKitPortY(count, index, nodeHeight) {
    if (count <= 1) return Math.round(nodeHeight * 0.5);
    const top = GRAPHKIT_NODE_HEADER_HEIGHT + 22;
    const bottom = Math.max(top, nodeHeight - 22);
    return Math.round(top + ((bottom - top) * index / (count - 1)));
}

function formatGraphKitNodeLabel(value, maxLength) {
    const text = String(value ?? '');
    if (text.length <= maxLength) return text;
    return `${text.slice(0, Math.max(0, maxLength - 3))}...`;
}
