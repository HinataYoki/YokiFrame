// pages/graphkit-edge-reconnect.js
// Edge endpoint reconnect helpers and UI binding for GraphKit wire editing.
(function initGraphKitEdgeReconnectModule(root) {
    const model = typeof module !== 'undefined' && module.exports
        ? require('./graphkit-model.js')
        : root;

    function buildGraphKitReconnectedEdge(project, edge, side, replacementEndpoint) {
        const sanitized = model.sanitizeGraphKitProject(project);
        const sourceEdge = edge && typeof edge === 'object' ? edge : null;
        const reconnectSide = side === 'to' ? 'to' : side === 'from' ? 'from' : '';
        if (!sourceEdge || !reconnectSide) {
            return { ok: false, edge: sourceEdge, message: 'edge side is required.' };
        }

        const fixedEndpoint = reconnectSide === 'from' ? sourceEdge.to : sourceEdge.from;
        const connection = getGraphKitReconnectConnection(sanitized, fixedEndpoint, replacementEndpoint, sourceEdge.id);
        if (!connection.ok) {
            return { ok: false, edge: sourceEdge, message: connection.message };
        }

        return {
            ok: true,
            edge: {
                ...sourceEdge,
                from: connection.from,
                to: connection.to,
            },
            message: '',
        };
    }

    function getGraphKitReconnectConnection(project, firstEndpoint, secondEndpoint, editingEdgeId) {
        const first = getGraphKitPortDefinitionInProject(project, firstEndpoint);
        const second = getGraphKitPortDefinitionInProject(project, secondEndpoint);
        if (!first || !second) return { ok: false, message: 'port endpoint is missing.' };

        const from = first.port.direction === 'output' ? first : second.port.direction === 'output' ? second : null;
        const to = first.port.direction === 'input' ? first : second.port.direction === 'input' ? second : null;
        if (!from || !to) return { ok: false, message: 'port direction must connect output to input.' };
        if (from.port.kind !== to.port.kind) return { ok: false, message: `port kind mismatch: ${from.port.kind} -> ${to.port.kind}.` };

        const duplicate = project.graph.edges.some(edge => {
            if (edge.id === editingEdgeId) return false;
            return edge.from === from.endpoint && edge.to === to.endpoint;
        });
        if (duplicate) return { ok: false, message: 'edge endpoint pair already exists.' };
        const capacityIssue = model.getGraphKitPortConnectionCapacityIssue(project, from.endpoint, to.endpoint, editingEdgeId);
        if (capacityIssue) return { ok: false, message: capacityIssue };
        return { ok: true, from: from.endpoint, to: to.endpoint };
    }

    function getGraphKitPortDefinitionInProject(project, endpoint) {
        const parsed = model.parseGraphKitEndpoint(endpoint);
        if (!parsed.nodeId || !parsed.portId) return null;
        const node = project.graph.nodes.find(candidate => candidate.id === parsed.nodeId);
        if (!node) return null;
        const type = project.nodeTypes.find(candidate => candidate.id === node.type);
        const port = (type?.ports || []).find(candidate => candidate.id === parsed.portId);
        if (!port) return null;
        return { endpoint, node, type, port };
    }

    function reconnectGraphKitEdgeEndpoint(edgeId, side, replacementEndpoint) {
        const currentEdge = graphKitProject.graph.edges.find(edge => edge.id === edgeId);
        const preview = buildGraphKitReconnectedEdge(graphKitProject, currentEdge, side, replacementEndpoint);
        if (!preview.ok) {
            setGraphKitNotice('warning', preview.message || '端口不兼容，无法重接连线。');
            renderGraphKitWorkbench();
            return false;
        }

        commitGraphKitMutation('Reconnect edge', before => {
            const edge = before.graph.edges.find(candidate => candidate.id === edgeId);
            const result = buildGraphKitReconnectedEdge(before, edge, side, replacementEndpoint);
            if (!result.ok) return before;
            return {
                ...before,
                graph: {
                    ...before.graph,
                    edges: before.graph.edges.map(candidate => candidate.id === edgeId ? result.edge : candidate),
                },
            };
        }, {
            selectedEdgeId: edgeId,
            clearPendingPort: true,
            noticeLevel: 'success',
            noticeText: `已重接连线 ${edgeId}。`,
        });
        return true;
    }

    function bindGraphKitEdgeReconnect() {
        $pageBody.querySelectorAll('[data-graphkit-edge-handle]').forEach(handle => {
            if (handle.dataset.edgeReconnectBound === '1') return;
            handle.dataset.edgeReconnectBound = '1';
            handle.addEventListener('pointerdown', event => {
                startGraphKitWireReconnectDrag(handle, event);
            });
        });
    }

    const api = {
        buildGraphKitReconnectedEdge,
        bindGraphKitEdgeReconnect,
        reconnectGraphKitEdgeEndpoint,
    };

    if (typeof module !== 'undefined' && module.exports) {
        module.exports = api;
    }

    if (root) {
        Object.assign(root, api);
    }
})(typeof window !== 'undefined' ? window : globalThis);
