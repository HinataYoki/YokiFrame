// pages/graphkit-graph-inspector.js
// Graph-level metadata editing for XML-backed graph assets.
(function initGraphKitGraphInspectorModule(root) {
    const model = typeof module !== 'undefined' && module.exports
        ? require('./graphkit-model.js')
        : root;

    function updateGraphKitProjectGraphMetadata(project, field, value) {
        const sanitized = model.sanitizeGraphKitProject(project);
        const key = String(field || '');
        const text = String(value ?? '').trim();
        const graph = { ...sanitized.graph };
        if (key === 'id') {
            graph.id = model.normalizeGraphKitId ? model.normalizeGraphKitId(text, sanitized.graph.id) : normalizeGraphKitMetadataId(text, sanitized.graph.id);
        } else if (key === 'title') {
            graph.title = text || sanitized.graph.title;
        } else if (key === 'kind') {
            graph.kind = text || sanitized.graph.kind;
        } else {
            return sanitized;
        }
        return { ...sanitized, graph };
    }

    function normalizeGraphKitMetadataId(value, fallback) {
        const text = String(value ?? '').trim();
        const sanitized = text.replace(/[^A-Za-z0-9_.-]/g, '_').replace(/^_+|_+$/g, '');
        return sanitized || fallback;
    }

    function selectGraphKitGraph(graphId) {
        if (graphId && typeof selectGraphKitGraphById === 'function') {
            selectGraphKitGraphById(graphId);
            return;
        }
        graphKitState.selectedNodeId = '';
        graphKitState.selectedNodeIds = [];
        graphKitState.selectedEdgeId = '';
        graphKitState.selectedBlackboardName = '';
        graphKitState.selectedNoteId = '';
        graphKitState.selectedPlacematId = '';
        graphKitState.selectedNodeTypeId = '';
        graphKitState.pendingPortEndpoint = '';
        renderGraphKitWorkbench();
    }

    function updateGraphKitSelectedGraphField(field, value) {
        commitGraphKitMutation('Edit graph metadata', before => updateGraphKitProjectGraphMetadata(before, field, value), {
            selectedNodeId: '',
            clearPendingPort: true,
        });
    }

    function bindGraphKitGraphInspector() {
        $pageBody.querySelectorAll('[data-graphkit-select-graph]').forEach(button => {
            if (button.dataset.bound === '1') return;
            button.dataset.bound = '1';
            button.addEventListener('click', () => selectGraphKitGraph(button.dataset.graphkitGraphId || ''));
        });
        $pageBody.querySelectorAll('[data-graphkit-graph-field]').forEach(input => {
            if (input.dataset.bound === '1') return;
            input.dataset.bound = '1';
            input.addEventListener('input', () => updateGraphKitSelectedGraphField(input.dataset.graphkitGraphField, input.value));
        });
    }

    const api = {
        bindGraphKitGraphInspector,
        selectGraphKitGraph,
        updateGraphKitProjectGraphMetadata,
        updateGraphKitSelectedGraphField,
    };

    if (typeof module !== 'undefined' && module.exports) module.exports = api;
    if (root) Object.assign(root, api);
})(typeof window !== 'undefined' ? window : globalThis);
