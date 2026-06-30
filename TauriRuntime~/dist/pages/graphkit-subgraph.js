// pages/graphkit-subgraph.js
// XML-backed subgraph creation and active graph switching helpers.
(function initGraphKitSubgraphModule(root) {
    const model = typeof module !== 'undefined' && module.exports
        ? require('./graphkit-model.js')
        : root;

    function createGraphKitProjectSubgraph(project, options = {}) {
        const sanitized = model.sanitizeGraphKitProject(project);
        const parentGraph = sanitized.graph;
        const title = normalizeGraphKitSubgraphText(options.title, 'Subgraph');
        const kind = normalizeGraphKitSubgraphText(options.kind, parentGraph.kind || 'graph');
        const graphId = makeUniqueGraphKitGraphId(sanitized, makeGraphKitGraphIdFromTitle(title));
        const nodeId = makeUniqueGraphKitNodeIdForGraph(parentGraph, graphId.replace(/\./g, '_'));
        const subgraph = makeGraphKitSubgraph(graphId, title, kind);
        const referenceNode = makeGraphKitSubgraphReferenceNode(nodeId, graphId, options);
        const nextParentGraph = {
            ...parentGraph,
            nodes: [...parentGraph.nodes, referenceNode],
        };
        const graphs = sanitized.graphs.map(graph => graph.id === parentGraph.id ? nextParentGraph : graph);
        graphs.push(subgraph);

        return {
            graphId,
            nodeId,
            project: model.sanitizeGraphKitProject({
                ...sanitized,
                activeGraphId: parentGraph.id,
                graph: nextParentGraph,
                graphs,
            }),
        };
    }

    function switchGraphKitProjectActiveGraph(project, graphId) {
        const sanitized = model.sanitizeGraphKitProject(project);
        const targetId = normalizeGraphKitSubgraphId(graphId, sanitized.activeGraphId);
        const targetGraph = sanitized.graphs.find(graph => graph.id === targetId) || sanitized.graph;
        return model.sanitizeGraphKitProject({
            ...sanitized,
            activeGraphId: targetGraph.id,
            graph: targetGraph,
            graphs: sanitized.graphs,
        });
    }

    function getGraphKitGraphNavigationPath(project, graphId) {
        const sanitized = model.sanitizeGraphKitProject(project);
        const targetId = normalizeGraphKitSubgraphId(graphId || sanitized.activeGraphId, sanitized.graph.id);
        const graphById = new Map(sanitized.graphs.map(graph => [graph.id, graph]));
        const targetGraph = graphById.get(targetId) || sanitized.graph;
        const rootGraph = sanitized.graphs[0] || sanitized.graph;
        const foundPath = findGraphKitNavigationPath(rootGraph.id, targetGraph.id, graphById, new Set(), []);
        return foundPath.length ? foundPath : [targetGraph];
    }

    function openGraphKitProjectSubgraphReference(project, nodeId) {
        const sanitized = model.sanitizeGraphKitProject(project);
        const node = (sanitized.graph.nodes || []).find(candidate => candidate.id === nodeId);
        if (!node) return { opened: false, reason: 'missing-node', graphId: '', project: sanitized };
        const graphId = getGraphKitSubgraphReferenceTargetId(node);
        if (!graphId) return { opened: false, reason: 'missing-target', graphId: '', project: sanitized };
        const targetGraph = sanitized.graphs.find(graph => graph.id === graphId);
        if (!targetGraph) return { opened: false, reason: 'missing-target', graphId, project: sanitized };
        const targetPortalId = node.type === 'graph.portal'
            ? normalizeGraphKitSubgraphId(node.fields?.targetPortal, '')
            : '';
        const targetNode = targetPortalId ? findGraphKitPortalNode(targetGraph, targetPortalId) : null;
        if (node.type === 'graph.portal' && !targetNode) {
            return { opened: false, reason: 'missing-portal', graphId, portalId: targetPortalId, project: sanitized };
        }
        return {
            opened: true,
            reason: '',
            graphId,
            targetNodeId: targetNode?.id || '',
            project: switchGraphKitProjectActiveGraph(sanitized, graphId),
        };
    }

    function repairGraphKitProjectPortalPair(project, nodeId) {
        const sanitized = model.sanitizeGraphKitProject(project);
        const sourceGraph = sanitized.graph;
        const node = (sourceGraph.nodes || []).find(candidate => candidate.id === nodeId);
        if (!node || node.type !== 'graph.portal') return { repaired: false, reason: 'missing-node', graphId: '', nodeId: '', project: sanitized };
        const portalId = normalizeGraphKitSubgraphId(node.fields?.portalId, node.id || 'portal');
        const targetGraphId = normalizeGraphKitSubgraphId(node.fields?.targetGraph, '');
        const targetPortalId = normalizeGraphKitSubgraphId(node.fields?.targetPortal, '');
        if (!targetGraphId) return { repaired: false, reason: 'missing-target', graphId: '', nodeId: '', project: sanitized };
        if (!targetPortalId) return { repaired: false, reason: 'missing-portal', graphId: targetGraphId, nodeId: '', project: sanitized };
        const targetGraph = sanitized.graphs.find(graph => graph.id === targetGraphId);
        if (!targetGraph) return { repaired: false, reason: 'missing-target', graphId: targetGraphId, nodeId: '', project: sanitized };
        const existingPortal = findGraphKitPortalNode(targetGraph, targetPortalId);
        if (existingPortal) {
            return {
                repaired: false,
                reason: 'already-linked',
                graphId: targetGraphId,
                nodeId: existingPortal.id,
                project: sanitized,
            };
        }

        const repairedNode = makeGraphKitReciprocalPortalNode(targetGraph, targetPortalId, sourceGraph.id, portalId);
        const repairedTargetGraph = {
            ...targetGraph,
            nodes: [...targetGraph.nodes, repairedNode],
        };
        const graphs = sanitized.graphs.map(graph => graph.id === targetGraph.id ? repairedTargetGraph : graph);
        const activeGraph = sourceGraph.id === repairedTargetGraph.id ? repairedTargetGraph : sourceGraph;
        return {
            repaired: true,
            reason: 'created',
            graphId: targetGraphId,
            nodeId: repairedNode.id,
            project: model.sanitizeGraphKitProject({
                ...sanitized,
                activeGraphId: sanitized.activeGraphId,
                graph: activeGraph,
                graphs,
            }),
        };
    }

    function createGraphKitSubgraph() {
        const position = getGraphKitViewportCenterModelPosition();
        const beforeProject = cloneGraphKitProject(graphKitProject);
        const result = createGraphKitProjectSubgraph(beforeProject, {
            title: 'Subgraph',
            kind: graphKitProject.graph.kind,
            x: position.x,
            y: position.y,
        });
        graphKitProject = result.project;
        pushGraphKitHistory(beforeProject, graphKitProject, 'Add subgraph');
        graphKitState.redoStack = [];
        selectGraphKitSubgraphReferenceNode(result.nodeId);
        setGraphKitNotice('success', `已添加子图 ${result.graphId}。`);
        persistGraphKitProject();
        renderGraphKitWorkbench();
    }

    function selectGraphKitGraphById(graphId) {
        const beforeId = graphKitProject.activeGraphId || graphKitProject.graph.id;
        graphKitProject = switchGraphKitProjectActiveGraph(graphKitProject, graphId || beforeId);
        clearGraphKitSelectionForGraphSwitch();
        graphKitState.selectedNodeId = getGraphKitInitialNodeId(graphKitProject);
        graphKitState.selectedNodeIds = [graphKitState.selectedNodeId].filter(Boolean);
        graphKitState.viewport = { scale: 1, offsetX: 0, offsetY: 0 };
        setGraphKitNotice('info', `已切换到图 ${graphKitProject.graph.id}。`);
        persistGraphKitProject();
        renderGraphKitWorkbench();
    }

    function openGraphKitSelectedSubgraphReference() {
        const result = openGraphKitProjectSubgraphReference(graphKitProject, graphKitState.selectedNodeId);
        if (!result.opened) {
            const suffix = result.graphId ? ` ${result.graphId}` : '';
            setGraphKitNotice('warning', `无法打开子图${suffix}。`);
            renderGraphKitWorkbench();
            return;
        }
        graphKitProject = result.project;
        clearGraphKitSelectionForGraphSwitch();
        graphKitState.selectedNodeId = result.targetNodeId || getGraphKitInitialNodeId(graphKitProject);
        graphKitState.selectedNodeIds = [graphKitState.selectedNodeId].filter(Boolean);
        graphKitState.viewport = { scale: 1, offsetX: 0, offsetY: 0 };
        setGraphKitNotice('info', `已打开子图 ${result.graphId}。`);
        persistGraphKitProject();
        renderGraphKitWorkbench();
    }

    function findGraphKitNavigationPath(currentGraphId, targetGraphId, graphById, visited, path) {
        if (visited.has(currentGraphId)) return [];
        const graph = graphById.get(currentGraphId);
        if (!graph) return [];
        const nextPath = [...path, graph];
        if (currentGraphId === targetGraphId) return nextPath;
        visited.add(currentGraphId);
        const references = (graph.nodes || [])
            .map(node => getGraphKitSubgraphReferenceTargetId(node))
            .filter(graphId => graphId && graphById.has(graphId));
        for (const childGraphId of references) {
            const childPath = findGraphKitNavigationPath(childGraphId, targetGraphId, graphById, visited, nextPath);
            if (childPath.length) return childPath;
        }
        return [];
    }

    function getGraphKitSubgraphReferenceTargetId(node) {
        if (!node || typeof node !== 'object') return '';
        if (node.type !== 'graph.subgraph' && node.type !== 'graph.portal') return '';
        return normalizeGraphKitSubgraphId(node.fields?.targetGraph, '');
    }

    function getGraphKitPortalOptionsForGraph(project, graphId) {
        const sanitized = model.sanitizeGraphKitProject(project);
        const targetId = normalizeGraphKitSubgraphId(graphId, '');
        const graph = sanitized.graphs.find(candidate => candidate.id === targetId);
        if (!graph) return [];
        return (graph.nodes || [])
            .filter(candidate => candidate.type === 'graph.portal')
            .map(candidate => ({
                id: normalizeGraphKitSubgraphId(candidate.fields?.portalId, ''),
                nodeId: candidate.id,
            }))
            .filter(option => option.id && option.nodeId);
    }

    function findGraphKitPortalNode(graph, portalId) {
        const normalizedPortalId = normalizeGraphKitSubgraphId(portalId, '');
        if (!graph || !normalizedPortalId) return null;
        return (graph.nodes || []).find(candidate => (
            candidate.type === 'graph.portal'
            && normalizeGraphKitSubgraphId(candidate.fields?.portalId, '') === normalizedPortalId
        )) || null;
    }

    function makeGraphKitReciprocalPortalNode(graph, portalId, targetGraphId, targetPortalId) {
        const nodeId = makeUniqueGraphKitNodeIdForGraph(graph, `${portalId}_portal`);
        const nodeCount = Array.isArray(graph.nodes) ? graph.nodes.length : 0;
        return {
            id: nodeId,
            type: 'graph.portal',
            x: 140 + nodeCount * 220,
            y: 120,
            fields: {
                portalId,
                targetGraph: targetGraphId,
                targetPortal: targetPortalId,
            },
        };
    }

    function selectGraphKitSubgraphReferenceNode(nodeId) {
        graphKitState.selectedNodeId = nodeId;
        graphKitState.selectedNodeIds = nodeId ? [nodeId] : [];
        graphKitState.selectedEdgeId = '';
        graphKitState.selectedBlackboardName = '';
        graphKitState.selectedNoteId = '';
        graphKitState.selectedPlacematId = '';
        graphKitState.selectedNodeTypeId = '';
        graphKitState.pendingPortEndpoint = '';
    }

    function clearGraphKitSelectionForGraphSwitch() {
        graphKitState.selectedNodeId = '';
        graphKitState.selectedNodeIds = [];
        graphKitState.selectedEdgeId = '';
        graphKitState.selectedBlackboardName = '';
        graphKitState.selectedNoteId = '';
        graphKitState.selectedPlacematId = '';
        graphKitState.selectedNodeTypeId = '';
        graphKitState.pendingPortEndpoint = '';
        graphKitState.contextMenu = typeof getClosedGraphKitContextMenu === 'function'
            ? getClosedGraphKitContextMenu()
            : graphKitState.contextMenu;
    }

    function makeGraphKitSubgraph(graphId, title, kind) {
        return {
            id: graphId,
            title,
            kind,
            blackboard: [],
            placemats: [],
            notes: [],
            nodes: [
                { id: 'start', type: 'flow.start', x: 80, y: 120, fields: {} },
                { id: 'end', type: 'flow.end', x: 360, y: 120, fields: {} },
            ],
            edges: [
                { id: 'start_to_end', from: 'start.out', to: 'end.in' },
            ],
        };
    }

    function makeGraphKitSubgraphReferenceNode(nodeId, graphId, options) {
        return {
            id: nodeId,
            type: 'graph.subgraph',
            x: Math.round(Number.isFinite(Number(options.x)) ? Number(options.x) : 280),
            y: Math.round(Number.isFinite(Number(options.y)) ? Number(options.y) : 160),
            fields: {
                targetGraph: graphId,
                entry: 'start',
                exit: 'end',
            },
        };
    }

    function makeUniqueGraphKitGraphId(project, baseId) {
        const usedIds = new Set((project.graphs || []).map(graph => graph.id));
        const base = normalizeGraphKitSubgraphId(baseId, 'subgraph');
        let id = base;
        let index = 2;
        while (usedIds.has(id)) {
            id = `${base}_${index}`;
            index += 1;
        }
        return id;
    }

    function makeUniqueGraphKitNodeIdForGraph(graph, baseId) {
        const usedIds = new Set((graph.nodes || []).map(node => node.id));
        const base = normalizeGraphKitSubgraphId(baseId, 'subgraph');
        let id = base;
        let index = 2;
        while (usedIds.has(id)) {
            id = `${base}_${index}`;
            index += 1;
        }
        return id;
    }

    function makeGraphKitGraphIdFromTitle(title) {
        return normalizeGraphKitSubgraphId(String(title || '').toLowerCase(), 'subgraph');
    }

    function normalizeGraphKitSubgraphId(value, fallback) {
        if (model.normalizeGraphKitId) return model.normalizeGraphKitId(value, fallback);
        const text = String(value ?? '').trim();
        const sanitized = text.replace(/[^A-Za-z0-9_.-]/g, '_').replace(/^_+|_+$/g, '');
        return sanitized || fallback;
    }

    function normalizeGraphKitSubgraphText(value, fallback) {
        const text = String(value ?? '').trim();
        return text || fallback;
    }

    const api = {
        createGraphKitProjectSubgraph,
        createGraphKitSubgraph,
        getGraphKitGraphNavigationPath,
        getGraphKitPortalOptionsForGraph,
        openGraphKitProjectSubgraphReference,
        openGraphKitSelectedSubgraphReference,
        repairGraphKitProjectPortalPair,
        switchGraphKitProjectActiveGraph,
        selectGraphKitGraphById,
    };

    if (typeof module !== 'undefined' && module.exports) module.exports = api;
    if (root) Object.assign(root, api);
})(typeof window !== 'undefined' ? window : globalThis);
