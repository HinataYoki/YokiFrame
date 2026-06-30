// pages/graphkit-placemat-resize.js
// Placemat resize helpers for XML-backed GraphKit organization data.
const GRAPHKIT_PLACEMAT_MIN_WIDTH = 180;
const GRAPHKIT_PLACEMAT_MIN_HEIGHT = 110;

let graphKitPlacematResizeDrag = null;

function resizeGraphKitProjectPlacemat(project, placematId, width, height, options = {}) {
    const source = project && typeof project === 'object' ? project : {};
    const graph = source.graph && typeof source.graph === 'object' ? source.graph : {};
    const placemats = Array.isArray(graph.placemats) ? graph.placemats : [];
    const target = placemats.find(item => item.id === placematId);
    if (!target) return source;
    if (target.locked || target.collapsed) return source;

    const minWidth = normalizeGraphKitPlacematResizeNumber(options.minWidth, GRAPHKIT_PLACEMAT_MIN_WIDTH);
    const minHeight = normalizeGraphKitPlacematResizeNumber(options.minHeight, GRAPHKIT_PLACEMAT_MIN_HEIGHT);
    const nextWidth = Math.max(minWidth, roundGraphKitPlacematResizeNumber(width, target.width || minWidth));
    const nextHeight = Math.max(minHeight, roundGraphKitPlacematResizeNumber(height, target.height || minHeight));

    return {
        ...source,
        graph: {
            ...graph,
            nodes: (Array.isArray(graph.nodes) ? graph.nodes : []).map(node => ({
                ...node,
                fields: { ...(node.fields || {}) },
            })),
            placemats: placemats.map(item => item.id === placematId ? {
                ...item,
                width: nextWidth,
                height: nextHeight,
                nodeIds: Array.isArray(item.nodeIds) ? [...item.nodeIds] : [],
            } : item),
        },
    };
}

function renderGraphKitPlacematResizeHandles(item, rect) {
    if (item?.locked || item?.collapsed) return '';
    const x = Math.max(0, normalizeGraphKitPlacematResizeNumber(rect?.width, 0) - 20);
    const y = Math.max(0, normalizeGraphKitPlacematResizeNumber(rect?.height, 0) - 20);
    return `<rect class="graphkit-placemat__resize" data-graphkit-placemat-resize="${escapeHtml(item.id)}" data-graphkit-placemat-resize-side="se" x="${escapeHtml(x)}" y="${escapeHtml(y)}" width="18" height="18" rx="4"><title>Resize placemat</title></rect>`;
}

function bindGraphKitPlacematResize() {
    $pageBody.querySelectorAll('[data-graphkit-placemat-resize]').forEach(handle => {
        if (handle.dataset.bound === '1') return;
        handle.dataset.bound = '1';
        handle.addEventListener('pointerdown', startGraphKitPlacematResize);
        handle.addEventListener('pointermove', updateGraphKitPlacematResize);
        handle.addEventListener('pointerup', finishGraphKitPlacematResize);
        handle.addEventListener('pointercancel', finishGraphKitPlacematResize);
    });
}

function startGraphKitPlacematResize(event) {
    if (event.button !== 0 || event.shiftKey || event.ctrlKey || event.metaKey) return;
    const handle = event.currentTarget;
    const id = handle?.dataset?.graphkitPlacematResize || '';
    const item = getGraphKitPlacematResizeItem(id);
    if (!item) return;
    if (item.locked || item.collapsed) return;

    graphKitState.selectedPlacematId = id;
    graphKitState.selectedNoteId = '';
    graphKitState.selectedNodeId = '';
    graphKitState.selectedNodeIds = [];
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.pendingPortEndpoint = '';
    graphKitPlacematResizeDrag = {
        id,
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        beforeProject: cloneGraphKitProject(graphKitProject),
        width: normalizeGraphKitPlacematResizeNumber(item.width, GRAPHKIT_PLACEMAT_MIN_WIDTH),
        height: normalizeGraphKitPlacematResizeNumber(item.height, GRAPHKIT_PLACEMAT_MIN_HEIGHT),
        scale: getGraphKitViewport().scale,
        moved: false,
    };
    handle.closest('[data-graphkit-placemat]')?.classList.add('is-resizing', 'active');
    handle.setPointerCapture?.(event.pointerId);
    bindGraphKitPlacematResizeDocumentEvents();
    event.preventDefault();
    event.stopPropagation();
}

function bindGraphKitPlacematResizeDocumentEvents() {
    document.addEventListener('pointermove', updateGraphKitPlacematResize, true);
    document.addEventListener('pointerup', finishGraphKitPlacematResize, true);
    document.addEventListener('pointercancel', finishGraphKitPlacematResize, true);
}

function unbindGraphKitPlacematResizeDocumentEvents() {
    document.removeEventListener('pointermove', updateGraphKitPlacematResize, true);
    document.removeEventListener('pointerup', finishGraphKitPlacematResize, true);
    document.removeEventListener('pointercancel', finishGraphKitPlacematResize, true);
}

function updateGraphKitPlacematResize(event) {
    if (!graphKitPlacematResizeDrag || graphKitPlacematResizeDrag.pointerId !== event.pointerId) return;
    const viewport = $pageBody.querySelector('[data-graphkit-canvas-viewport]');
    if (!viewport) return;
    const rect = viewport.getBoundingClientRect();
    const unitX = GRAPHKIT_CANVAS_WIDTH / Math.max(1, rect.width);
    const unitY = GRAPHKIT_CANVAS_HEIGHT / Math.max(1, rect.height);
    const deltaX = (event.clientX - graphKitPlacematResizeDrag.startX) * unitX / graphKitPlacematResizeDrag.scale / GRAPHKIT_NODE_MODEL_SCALE_X;
    const deltaY = (event.clientY - graphKitPlacematResizeDrag.startY) * unitY / graphKitPlacematResizeDrag.scale / GRAPHKIT_NODE_MODEL_SCALE_Y;
    graphKitProject = resizeGraphKitProjectPlacemat(
        graphKitProject,
        graphKitPlacematResizeDrag.id,
        graphKitPlacematResizeDrag.width + deltaX,
        graphKitPlacematResizeDrag.height + deltaY
    );
    applyGraphKitPlacematResize(graphKitPlacematResizeDrag.id);
    graphKitPlacematResizeDrag.moved = graphKitPlacematResizeDrag.moved || Math.abs(event.clientX - graphKitPlacematResizeDrag.startX) > 2 || Math.abs(event.clientY - graphKitPlacematResizeDrag.startY) > 2;
    event.preventDefault();
    event.stopPropagation();
}

function finishGraphKitPlacematResize(event) {
    if (!graphKitPlacematResizeDrag || graphKitPlacematResizeDrag.pointerId !== event.pointerId) return;
    const drag = graphKitPlacematResizeDrag;
    graphKitPlacematResizeDrag = null;
    unbindGraphKitPlacematResizeDocumentEvents();
    const handle = $pageBody.querySelector(`[data-graphkit-placemat-resize="${escapeGraphKitSelectorValue(drag.id)}"]`);
    handle?.closest('[data-graphkit-placemat]')?.classList.remove('is-resizing');
    handle?.releasePointerCapture?.(event.pointerId);
    if (drag.moved) {
        pushGraphKitHistory(drag.beforeProject, graphKitProject, 'Resize placemat');
        graphKitState.redoStack = [];
        persistGraphKitProject();
        renderGraphKitWorkbench();
    }
    event.preventDefault();
    event.stopPropagation();
}

function applyGraphKitPlacematResize(id) {
    const item = getGraphKitPlacematResizeItem(id);
    if (!item) return;
    const element = $pageBody.querySelector(`[data-graphkit-placemat="${escapeGraphKitSelectorValue(id)}"]`);
    if (!element) return;
    const rect = getGraphKitOrganizationSceneRect(item);
    const body = element.querySelector('.graphkit-placemat__body');
    if (body) {
        body.setAttribute('width', String(roundGraphKitTransformNumber(rect.width)));
        body.setAttribute('height', String(roundGraphKitTransformNumber(rect.height)));
    }
    const handle = element.querySelector('[data-graphkit-placemat-resize]');
    if (handle) {
        handle.setAttribute('x', String(Math.max(0, roundGraphKitTransformNumber(rect.width - 20))));
        handle.setAttribute('y', String(Math.max(0, roundGraphKitTransformNumber(rect.height - 20))));
    }
}

function getGraphKitPlacematResizeItem(id) {
    return graphKitProject.graph.placemats.find(item => item.id === id) || null;
}

function normalizeGraphKitPlacematResizeNumber(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) ? number : fallback;
}

function roundGraphKitPlacematResizeNumber(value, fallback) {
    return Math.round(normalizeGraphKitPlacematResizeNumber(value, fallback) * 1000) / 1000;
}

const graphKitPlacematResizeApi = {
    GRAPHKIT_PLACEMAT_MIN_HEIGHT,
    GRAPHKIT_PLACEMAT_MIN_WIDTH,
    resizeGraphKitProjectPlacemat,
};

if (typeof module !== 'undefined' && module.exports) {
    module.exports = graphKitPlacematResizeApi;
}

if (typeof window !== 'undefined') {
    Object.assign(window, graphKitPlacematResizeApi);
}
