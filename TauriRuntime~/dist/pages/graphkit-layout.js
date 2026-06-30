// pages/graphkit-layout.js
// Pure GraphKit layout helpers for alignment and distribution commands.
const GRAPHKIT_LAYOUT_DEFAULTS = Object.freeze({
    nodeWidth: 250,
    nodeMinHeight: 104,
    nodeHeaderHeight: 48,
    nodeFieldHeight: 23,
    nodeOriginX: 320,
    nodeOriginY: 260,
    nodeModelScaleX: 1.9,
    nodeModelScaleY: 1.18,
});

function getGraphKitLayoutConstants(overrides = {}) {
    const globalConstants = {
        nodeWidth: typeof GRAPHKIT_NODE_WIDTH !== 'undefined' ? GRAPHKIT_NODE_WIDTH : undefined,
        nodeMinHeight: typeof GRAPHKIT_NODE_MIN_HEIGHT !== 'undefined' ? GRAPHKIT_NODE_MIN_HEIGHT : undefined,
        nodeHeaderHeight: typeof GRAPHKIT_NODE_HEADER_HEIGHT !== 'undefined' ? GRAPHKIT_NODE_HEADER_HEIGHT : undefined,
        nodeFieldHeight: typeof GRAPHKIT_NODE_FIELD_HEIGHT !== 'undefined' ? GRAPHKIT_NODE_FIELD_HEIGHT : undefined,
        nodeOriginX: typeof GRAPHKIT_NODE_ORIGIN_X !== 'undefined' ? GRAPHKIT_NODE_ORIGIN_X : undefined,
        nodeOriginY: typeof GRAPHKIT_NODE_ORIGIN_Y !== 'undefined' ? GRAPHKIT_NODE_ORIGIN_Y : undefined,
        nodeModelScaleX: typeof GRAPHKIT_NODE_MODEL_SCALE_X !== 'undefined' ? GRAPHKIT_NODE_MODEL_SCALE_X : undefined,
        nodeModelScaleY: typeof GRAPHKIT_NODE_MODEL_SCALE_Y !== 'undefined' ? GRAPHKIT_NODE_MODEL_SCALE_Y : undefined,
    };
    return Object.entries({ ...GRAPHKIT_LAYOUT_DEFAULTS, ...globalConstants, ...overrides })
        .reduce((constants, [key, value]) => {
            constants[key] = Number.isFinite(Number(value)) ? Number(value) : GRAPHKIT_LAYOUT_DEFAULTS[key];
            return constants;
        }, {});
}

function alignGraphKitProjectNodes(project, selectedNodeIds, mode, constants) {
    const context = getGraphKitLayoutContext(project, selectedNodeIds, constants);
    if (context.items.length < 2) return cloneGraphKitLayoutProject(project);
    const bounds = getGraphKitLayoutSelectionBounds(context.items);
    const nextPositions = new Map();

    context.items.forEach(item => {
        let x = item.bounds.x;
        let y = item.bounds.y;
        if (mode === 'left') x = bounds.minX;
        else if (mode === 'center') x = bounds.centerX - item.bounds.width * 0.5;
        else if (mode === 'right') x = bounds.maxX - item.bounds.width;
        else if (mode === 'top') y = bounds.minY;
        else if (mode === 'middle') y = bounds.centerY - item.bounds.height * 0.5;
        else if (mode === 'bottom') y = bounds.maxY - item.bounds.height;
        nextPositions.set(item.node.id, graphKitScenePositionToModel(x, y, context.constants));
    });

    return applyGraphKitLayoutPositions(project, nextPositions);
}

function distributeGraphKitProjectNodes(project, selectedNodeIds, axis, constants) {
    const context = getGraphKitLayoutContext(project, selectedNodeIds, constants);
    if (context.items.length < 3) return cloneGraphKitLayoutProject(project);
    const horizontal = axis === 'horizontal';
    const sorted = [...context.items].sort((a, b) => {
        const primary = horizontal ? a.bounds.x - b.bounds.x : a.bounds.y - b.bounds.y;
        return primary || a.node.id.localeCompare(b.node.id);
    });
    const start = horizontal ? sorted[0].bounds.x : sorted[0].bounds.y;
    const endItem = sorted[sorted.length - 1];
    const end = horizontal ? endItem.bounds.x + endItem.bounds.width : endItem.bounds.y + endItem.bounds.height;
    const totalSize = sorted.reduce((sum, item) => sum + (horizontal ? item.bounds.width : item.bounds.height), 0);
    const gap = (end - start - totalSize) / Math.max(1, sorted.length - 1);
    const nextPositions = new Map();
    let cursor = start;

    sorted.forEach(item => {
        const x = horizontal ? cursor : item.bounds.x;
        const y = horizontal ? item.bounds.y : cursor;
        nextPositions.set(item.node.id, graphKitScenePositionToModel(x, y, context.constants));
        cursor += (horizontal ? item.bounds.width : item.bounds.height) + gap;
    });

    return applyGraphKitLayoutPositions(project, nextPositions);
}

function getGraphKitLayoutContext(project, selectedNodeIds, constants) {
    const safeProject = project && typeof project === 'object' ? project : {};
    const nodes = Array.isArray(safeProject.graph?.nodes) ? safeProject.graph.nodes : [];
    const selected = new Set(Array.isArray(selectedNodeIds) ? selectedNodeIds : []);
    const layoutConstants = getGraphKitLayoutConstants(constants);
    const typeById = new Map((Array.isArray(safeProject.nodeTypes) ? safeProject.nodeTypes : []).map(type => [type.id, type]));
    const items = nodes
        .filter(node => selected.has(node.id))
        .map(node => ({
            node,
            bounds: getGraphKitLayoutNodeBounds(node, typeById.get(node.type), layoutConstants),
        }));
    return { constants: layoutConstants, items };
}

function getGraphKitLayoutNodeBounds(node, type, constants) {
    const x = constants.nodeOriginX + normalizeGraphKitLayoutNumber(node?.x, 0) * constants.nodeModelScaleX;
    const y = constants.nodeOriginY + normalizeGraphKitLayoutNumber(node?.y, 0) * constants.nodeModelScaleY;
    return {
        x,
        y,
        width: constants.nodeWidth,
        height: getGraphKitLayoutNodeHeight(type, constants),
    };
}

function getGraphKitLayoutNodeHeight(type, constants) {
    const fields = Array.isArray(type?.fields) ? type.fields : [];
    return Math.max(constants.nodeMinHeight, constants.nodeHeaderHeight + 38 + fields.length * constants.nodeFieldHeight);
}

function getGraphKitLayoutSelectionBounds(items) {
    const initial = {
        minX: Number.POSITIVE_INFINITY,
        minY: Number.POSITIVE_INFINITY,
        maxX: Number.NEGATIVE_INFINITY,
        maxY: Number.NEGATIVE_INFINITY,
    };
    const bounds = items.reduce((next, item) => ({
        minX: Math.min(next.minX, item.bounds.x),
        minY: Math.min(next.minY, item.bounds.y),
        maxX: Math.max(next.maxX, item.bounds.x + item.bounds.width),
        maxY: Math.max(next.maxY, item.bounds.y + item.bounds.height),
    }), initial);
    bounds.centerX = bounds.minX + (bounds.maxX - bounds.minX) * 0.5;
    bounds.centerY = bounds.minY + (bounds.maxY - bounds.minY) * 0.5;
    return bounds;
}

function graphKitScenePositionToModel(x, y, constants) {
    return {
        x: roundGraphKitLayoutNumber((x - constants.nodeOriginX) / constants.nodeModelScaleX),
        y: roundGraphKitLayoutNumber((y - constants.nodeOriginY) / constants.nodeModelScaleY),
    };
}

function applyGraphKitLayoutPositions(project, positions) {
    const nextProject = cloneGraphKitLayoutProject(project);
    nextProject.graph = {
        ...(nextProject.graph || {}),
        nodes: (Array.isArray(nextProject.graph?.nodes) ? nextProject.graph.nodes : []).map(node => {
            const position = positions.get(node.id);
            return position ? { ...node, x: position.x, y: position.y } : node;
        }),
    };
    return nextProject;
}

function cloneGraphKitLayoutProject(project) {
    const source = project && typeof project === 'object' ? project : {};
    return {
        ...source,
        graph: {
            ...(source.graph || {}),
            nodes: (Array.isArray(source.graph?.nodes) ? source.graph.nodes : []).map(node => ({
                ...node,
                fields: { ...(node.fields || {}) },
            })),
            edges: (Array.isArray(source.graph?.edges) ? source.graph.edges : []).map(edge => ({ ...edge })),
            blackboard: (Array.isArray(source.graph?.blackboard) ? source.graph.blackboard : []).map(item => ({ ...item })),
        },
        nodeTypes: (Array.isArray(source.nodeTypes) ? source.nodeTypes : []).map(type => ({
            ...type,
            ports: (Array.isArray(type.ports) ? type.ports : []).map(port => ({ ...port })),
            fields: (Array.isArray(type.fields) ? type.fields : []).map(field => ({ ...field })),
        })),
    };
}

function normalizeGraphKitLayoutNumber(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) ? number : fallback;
}

function roundGraphKitLayoutNumber(value) {
    return Math.round(value * 1000) / 1000;
}

const graphKitLayoutApi = {
    alignGraphKitProjectNodes,
    distributeGraphKitProjectNodes,
    getGraphKitLayoutConstants,
};

if (typeof module !== 'undefined' && module.exports) {
    module.exports = graphKitLayoutApi;
}

if (typeof window !== 'undefined') {
    Object.assign(window, graphKitLayoutApi);
}
