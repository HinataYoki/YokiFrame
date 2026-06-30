// pages/graphkit-edge-insert.js
// Insert a compatible node into an existing wire while keeping the graph XML data-only.
(function initGraphKitEdgeInsertModule(root) {
    const model = typeof module !== 'undefined' && module.exports
        ? require('./graphkit-model.js')
        : root;

    function buildGraphKitEdgeInsertion(project, edge, typeId, position) {
        const sanitized = model.sanitizeGraphKitProject(project);
        const sourceEdge = edge && typeof edge === 'object' ? edge : null;
        const type = sanitized.nodeTypes.find(candidate => candidate.id === typeId);
        if (!sourceEdge || !type) return { ok: false, message: 'edge and node type are required.' };

        const ports = getGraphKitInsertionPorts(sanitized, sourceEdge, type);
        if (!ports.ok) return { ok: false, message: ports.message };

        const usedNodeIds = new Set(sanitized.graph.nodes.map(node => node.id));
        const usedEdgeIds = new Set(sanitized.graph.edges.map(item => item.id));
        const nodeId = makeGraphKitInsertionUniqueId(type.id.replace(/\./g, '_'), usedNodeIds, 'node');
        const node = {
            id: nodeId,
            type: type.id,
            x: Math.round(Number.isFinite(Number(position?.x)) ? Number(position.x) : 120),
            y: Math.round(Number.isFinite(Number(position?.y)) ? Number(position.y) : 120),
            fields: makeGraphKitInsertionDefaultFields(type),
        };
        const upstreamTo = `${nodeId}.${ports.input.id}`;
        const downstreamFrom = `${nodeId}.${ports.output.id}`;
        const upstream = {
            id: makeGraphKitInsertionUniqueId(`${sourceEdge.from}_to_${upstreamTo}`.replace(/\./g, '_'), usedEdgeIds, 'edge'),
            from: sourceEdge.from,
            to: upstreamTo,
        };
        const downstream = {
            id: makeGraphKitInsertionUniqueId(`${downstreamFrom}_to_${sourceEdge.to}`.replace(/\./g, '_'), usedEdgeIds, 'edge'),
            from: downstreamFrom,
            to: sourceEdge.to,
        };
        return { ok: true, node, edges: [upstream, downstream], removedEdgeId: sourceEdge.id, message: '' };
    }

    function getGraphKitInsertionPorts(project, edge, type) {
        const fromPort = getGraphKitInsertionPort(project, edge.from);
        const toPort = getGraphKitInsertionPort(project, edge.to);
        if (!fromPort || !toPort) return { ok: false, message: 'edge endpoints are missing.' };
        const input = (type.ports || []).find(port => port.direction === 'input' && port.kind === fromPort.kind);
        const output = (type.ports || []).find(port => port.direction !== 'input' && port.kind === toPort.kind);
        if (!input || !output) return { ok: false, message: 'node type must provide matching input and output ports.' };
        return { ok: true, input, output };
    }

    function getGraphKitInsertionPort(project, endpoint) {
        const parsed = model.parseGraphKitEndpoint(endpoint);
        const node = project.graph.nodes.find(candidate => candidate.id === parsed.nodeId);
        const type = project.nodeTypes.find(candidate => candidate.id === node?.type);
        return (type?.ports || []).find(port => port.id === parsed.portId) || null;
    }

    function makeGraphKitInsertionDefaultFields(type) {
        const fields = {};
        (type.fields || []).forEach(field => {
            fields[field.name] = field.defaultValue !== undefined ? field.defaultValue : '';
        });
        return fields;
    }

    function makeGraphKitInsertionUniqueId(baseId, usedIds, fallback) {
        const base = String(baseId || fallback).replace(/[^A-Za-z0-9_.-]/g, '_').replace(/^_+|_+$/g, '') || fallback;
        let id = base;
        let index = 2;
        while (usedIds.has(id)) {
            id = `${base}_${index}`;
            index += 1;
        }
        usedIds.add(id);
        return id;
    }

    function isGraphKitNodeTypeInsertableOnEdge(project, edge, typeId) {
        return buildGraphKitEdgeInsertion(project, edge, typeId, { x: 0, y: 0 }).ok;
    }

    function insertGraphKitNodeOnEdge(edgeId, typeId, position) {
        const currentEdge = graphKitProject.graph.edges.find(edge => edge.id === edgeId);
        const preview = buildGraphKitEdgeInsertion(graphKitProject, currentEdge, typeId, position);
        if (!preview.ok) {
            setGraphKitNotice('warning', preview.message || '该节点类型无法插入到当前连线。');
            renderGraphKitWorkbench();
            return false;
        }
        commitGraphKitMutation('Insert node on wire', before => {
            const edge = before.graph.edges.find(candidate => candidate.id === edgeId);
            const insertion = buildGraphKitEdgeInsertion(before, edge, typeId, position);
            if (!insertion.ok) return before;
            return {
                ...before,
                graph: {
                    ...before.graph,
                    nodes: [...before.graph.nodes, insertion.node],
                    edges: [
                        ...before.graph.edges.filter(candidate => candidate.id !== edgeId),
                        ...insertion.edges,
                    ],
                },
            };
        }, {
            selectedNodeId: preview.node.id,
            clearPendingPort: true,
            noticeLevel: 'success',
            noticeText: `已在连线上插入节点 ${preview.node.id}。`,
        });
        return true;
    }

    const api = {
        buildGraphKitEdgeInsertion,
        insertGraphKitNodeOnEdge,
        isGraphKitNodeTypeInsertableOnEdge,
    };

    if (typeof module !== 'undefined' && module.exports) module.exports = api;
    if (root) Object.assign(root, api);
})(typeof window !== 'undefined' ? window : globalThis);
