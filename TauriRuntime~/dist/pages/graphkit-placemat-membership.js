// pages/graphkit-placemat-membership.js
// Keeps XML placemat membership aligned with node placement.
const GRAPHKIT_MEMBERSHIP_NODE_WIDTH = 240;
const GRAPHKIT_MEMBERSHIP_NODE_HEIGHT = 90;

function updateGraphKitProjectPlacematMembership(project, nodeId, options = {}) {
    const source = project && typeof project === 'object' ? project : {};
    const graph = source.graph && typeof source.graph === 'object' ? source.graph : {};
    const nodes = Array.isArray(graph.nodes) ? graph.nodes : [];
    const placemats = Array.isArray(graph.placemats) ? graph.placemats : [];
    const targetNode = nodes.find(node => node.id === nodeId);
    if (!targetNode) return source;

    const nodeWidth = normalizeGraphKitMembershipNumber(options.nodeWidth, GRAPHKIT_MEMBERSHIP_NODE_WIDTH);
    const nodeHeight = normalizeGraphKitMembershipNumber(options.nodeHeight, GRAPHKIT_MEMBERSHIP_NODE_HEIGHT);
    const centerX = normalizeGraphKitMembershipNumber(targetNode.x, 0) + nodeWidth * 0.5;
    const centerY = normalizeGraphKitMembershipNumber(targetNode.y, 0) + nodeHeight * 0.5;

    return {
        ...source,
        graph: {
            ...graph,
            nodes: nodes.map(node => ({
                ...node,
                fields: { ...(node.fields || {}) },
            })),
            placemats: placemats.map(placemat => {
                const contained = isGraphKitPointInsidePlacemat(centerX, centerY, placemat);
                const nodeIds = uniqueGraphKitMembershipIds(placemat.nodeIds).filter(id => id !== nodeId);
                if (contained) nodeIds.push(nodeId);
                return {
                    ...placemat,
                    nodeIds,
                };
            }),
        },
    };
}

function syncGraphKitPlacematMembershipForNode(nodeId) {
    const node = graphKitProject.graph.nodes.find(candidate => candidate.id === nodeId);
    if (!node) return;
    const type = getGraphKitNodeType(node.type);
    graphKitProject = updateGraphKitProjectPlacematMembership(graphKitProject, nodeId, {
        nodeWidth: GRAPHKIT_NODE_WIDTH / GRAPHKIT_NODE_MODEL_SCALE_X,
        nodeHeight: getGraphKitNodeHeight(type, node) / GRAPHKIT_NODE_MODEL_SCALE_Y,
    });
}

function isGraphKitPointInsidePlacemat(x, y, placemat) {
    const left = normalizeGraphKitMembershipNumber(placemat?.x, 0);
    const top = normalizeGraphKitMembershipNumber(placemat?.y, 0);
    const right = left + normalizeGraphKitMembershipNumber(placemat?.width, 0);
    const bottom = top + normalizeGraphKitMembershipNumber(placemat?.height, 0);
    return x >= left && x <= right && y >= top && y <= bottom;
}

function uniqueGraphKitMembershipIds(values) {
    const result = [];
    const seen = new Set();
    (Array.isArray(values) ? values : []).forEach(value => {
        const id = String(value || '').trim();
        if (!id || seen.has(id)) return;
        seen.add(id);
        result.push(id);
    });
    return result;
}

function normalizeGraphKitMembershipNumber(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) ? number : fallback;
}

const graphKitPlacematMembershipApi = {
    updateGraphKitProjectPlacematMembership,
};

if (typeof module !== 'undefined' && module.exports) {
    module.exports = graphKitPlacematMembershipApi;
}

if (typeof window !== 'undefined') {
    Object.assign(window, graphKitPlacematMembershipApi);
}
