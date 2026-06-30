// pages/graphkit-interactions.js
// GraphKit editor event binding, canvas navigation and direct manipulation.
function bindGraphKitEditor() {
    const search = $pageBody.querySelector('[data-graphkit-search]');
    if (search && search.dataset.bound !== '1') {
        search.dataset.bound = '1';
        search.addEventListener('input', () => {
            graphKitState.searchTerm = search.value;
            renderGraphKitWorkbench();
        });
    }

    $pageBody.querySelectorAll('[data-graphkit-template-select]').forEach(select => {
        if (select.dataset.bound === '1') return;
        select.dataset.bound = '1';
        select.addEventListener('change', () => {
            if (!select.value || typeof loadGraphKitTemplateProject !== 'function') return;
            loadGraphKitTemplateProject(select.value);
        });
    });

    $pageBody.querySelectorAll('[data-graphkit-node]').forEach(node => {
        if (node.dataset.bound === '1') return;
        node.dataset.bound = '1';
        node.addEventListener('click', event => {
            selectGraphKitNode(node.dataset.graphkitNode || '', event.shiftKey || event.ctrlKey || event.metaKey);
            event.preventDefault();
            event.stopPropagation();
        });
        node.addEventListener('dblclick', event => {
            const nodeId = node.dataset.graphkitNode || '';
            const graphNode = graphKitProject.graph.nodes.find(candidate => candidate.id === nodeId);
            selectGraphKitNode(nodeId, false);
            if ((graphNode?.type === 'graph.subgraph' || graphNode?.type === 'graph.portal') && typeof openGraphKitSelectedSubgraphReference === 'function') openGraphKitSelectedSubgraphReference();
            event.preventDefault();
            event.stopPropagation();
        });
        bindGraphKitNodeDrag(node);
    });

    $pageBody.querySelectorAll('[data-graphkit-node-collapse]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('pointerdown', event => {
            event.preventDefault();
            event.stopPropagation();
        });
        button.addEventListener('click', event => {
            toggleGraphKitSelectedNodeCollapsed(button.dataset.graphkitNodeCollapse || '');
            event.preventDefault();
            event.stopPropagation();
        });
        button.addEventListener('keydown', event => {
            if (event.key !== 'Enter' && event.key !== ' ') return;
            toggleGraphKitSelectedNodeCollapsed(button.dataset.graphkitNodeCollapse || '');
            event.preventDefault();
            event.stopPropagation();
        });
    });

    $pageBody.querySelectorAll('[data-graphkit-blackboard-var]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => selectGraphKitBlackboardVariable(button.dataset.graphkitBlackboardVar || ''));
    });

    $pageBody.querySelectorAll('[data-graphkit-edge]').forEach(edge => {
        if (edge.dataset.bound === '1') return;
        edge.dataset.bound = '1';
        edge.addEventListener('click', event => {
            selectGraphKitEdge(edge.dataset.graphkitEdge || '');
            event.preventDefault();
            event.stopPropagation();
        });
    });

    $pageBody.querySelectorAll('[data-graphkit-type]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => selectFirstGraphKitNodeOfType(button.dataset.graphkitType));
    });

    $pageBody.querySelectorAll('[data-graphkit-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        const eventName = input.tagName === 'SELECT' || input.type === 'checkbox' ? 'change' : 'input';
        input.addEventListener(eventName, () => updateGraphKitSelectedNodeField(input.dataset.graphkitField, input.type === 'checkbox' ? input.checked : input.value));
    });

    $pageBody.querySelectorAll('[data-graphkit-edge-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('input', () => updateGraphKitSelectedEdgeField(input.dataset.graphkitEdgeField, input.value));
    });

    $pageBody.querySelectorAll('[data-graphkit-blackboard-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('input', () => updateGraphKitSelectedBlackboardField(input.dataset.graphkitBlackboardField, input.value));
    });

    bindKitButtonClick('[data-graphkit-action="reset"]', () => {
        if (typeof window.confirm === 'function' && !window.confirm('重置 XML Graph 示例数据？')) return;
        resetGraphKitProject();
        renderGraphKitWorkbench();
    });

    bindKitButtonClick('[data-graphkit-action="copy-xml"]', () => void copyGraphKitXml());
    bindKitButtonClick('[data-graphkit-action="copy-runtime-contract"]', () => void copyGraphKitRuntimeContract());
    bindKitButtonClick('[data-graphkit-action="copy-scaffold"]', () => void copyGraphKitHandlerScaffold());
    bindKitButtonClick('[data-graphkit-action="copy-luban-definition"]', () => void copyGraphKitLubanDefinition());
    bindKitButtonClick('[data-graphkit-action="copy-luban-data"]', () => void copyGraphKitLubanData());
    bindKitButtonClick('[data-graphkit-action="export-luban-files"]', () => void exportGraphKitLubanFilesToTableKit());
    bindKitButtonClick('[data-graphkit-action="export-run-luban"]', () => void exportAndRunGraphKitLuban());
    bindKitButtonClick('[data-graphkit-action="import-xml"]', () => importGraphKitXml());
    bindKitButtonClick('[data-graphkit-action="download-xml"]', () => downloadGraphKitXml());
    bindKitButtonClick('[data-graphkit-action="open-xml-file"]', () => void openGraphKitXmlFile());
    bindKitButtonClick('[data-graphkit-action="save-xml-file"]', () => void saveGraphKitXmlFile());
    bindKitButtonClick('[data-graphkit-action="save-xml-file-as"]', () => void saveGraphKitXmlFileAs());
    bindKitButtonClick('[data-graphkit-action="undo"]', () => undoGraphKitMutation());
    bindKitButtonClick('[data-graphkit-action="redo"]', () => redoGraphKitMutation());
    bindKitButtonClick('[data-graphkit-action="add-node"]', () => createGraphKitNode());
    bindKitButtonClick('[data-graphkit-action="add-subgraph"]', () => createGraphKitSubgraph());
    bindKitButtonClick('[data-graphkit-action="open-subgraph"]', () => openGraphKitSelectedSubgraphReference());
    bindKitButtonClick('[data-graphkit-action="open-portal"]', () => openGraphKitSelectedSubgraphReference());
    bindKitButtonClick('[data-graphkit-action="repair-portal-pair"]', () => repairGraphKitSelectedPortalPair());
    bindKitButtonClick('[data-graphkit-action="add-node-type"]', () => createGraphKitNodeType());
    bindKitButtonClick('[data-graphkit-action="add-note"]', () => createGraphKitStickyNote());
    bindKitButtonClick('[data-graphkit-action="add-placemat"]', () => createGraphKitPlacemat());
    bindKitButtonClick('[data-graphkit-action="add-blackboard-var"]', () => createGraphKitBlackboardVariable());
    bindKitButtonClick('[data-graphkit-action="duplicate-node"]', () => duplicateGraphKitSelectedNodes());
    bindKitButtonClick('[data-graphkit-action="delete-node"]', () => {
        if (!deleteGraphKitSelectedEdge() && !deleteGraphKitSelectedBlackboardVariable() && !deleteGraphKitSelectedOrganizationItem() && !deleteGraphKitSelectedNodes()) deleteGraphKitSelectedNode();
    });
    bindKitButtonClick('[data-graphkit-action="delete-edge"]', () => deleteGraphKitSelectedEdge());
    bindKitButtonClick('[data-graphkit-action="delete-blackboard-var"]', () => deleteGraphKitSelectedBlackboardVariable());
    bindKitButtonClick('[data-graphkit-action="zoom-in"]', () => zoomGraphKitViewport(GRAPHKIT_VIEWPORT_STEP));
    bindKitButtonClick('[data-graphkit-action="zoom-out"]', () => zoomGraphKitViewport(1 / GRAPHKIT_VIEWPORT_STEP));
    bindKitButtonClick('[data-graphkit-action="fit-view"]', () => fitGraphKitViewportToNodes());
    bindKitButtonClick('[data-graphkit-action="toggle-issues"]', () => toggleGraphKitIssuesOverlay());
    bindKitButtonClick('[data-graphkit-action="toggle-search"]', () => toggleGraphKitSearchOverlay());
    bindKitButtonClick('[data-graphkit-action="reset-view"]', () => {
        updateGraphKitViewport({ scale: 1, offsetX: 0, offsetY: 0 });
    });
    bindGraphKitPanelToggles();
    bindGraphKitGraphInspector();
    bindGraphKitLayoutActions();
    bindGraphKitOrganizationItems();
    if (typeof bindGraphKitPlacematResize === 'function') bindGraphKitPlacematResize();
    bindGraphKitNodeTypeRegistry();
    bindGraphKitPorts();
    bindGraphKitWireDrag();
    bindGraphKitBlackboardDrag();
    bindGraphKitEdgeReconnect();
    bindGraphKitShortcuts();
    bindGraphKitViewport();
    bindGraphKitMarqueeSelection();
    bindGraphKitMiniMap();
    bindGraphKitIssuesOverlay();
    bindGraphKitSearchOverlay();
    bindGraphKitPreviewDocks();
    bindGraphKitContextMenu();
}

function bindGraphKitPreviewDocks() {
    $pageBody.querySelectorAll('.graphkit-xml-dock, .graphkit-scaffold-dock, .graphkit-luban-dock, .graphkit-runtime-contract-dock').forEach(dock => {
        if (dock.dataset.previewDockBound === '1') return;
        dock.dataset.previewDockBound = '1';
        const summary = dock.querySelector('summary');
        summary?.addEventListener('click', () => {
            if (!dock.open) closeGraphKitPreviewDockSiblings(dock);
        });
        dock.addEventListener('toggle', () => {
            if (dock.open) closeGraphKitPreviewDockSiblings(dock);
        });
    });
}

function closeGraphKitPreviewDockSiblings(activeDock) {
    $pageBody.querySelectorAll('.graphkit-xml-dock, .graphkit-scaffold-dock, .graphkit-luban-dock, .graphkit-runtime-contract-dock').forEach(otherDock => {
        if (otherDock !== activeDock) otherDock.open = false;
    });
}

function bindGraphKitPorts() {
    $pageBody.querySelectorAll('[data-graphkit-port]').forEach(port => {
        if (port.dataset.bound === '1') return;
        port.dataset.bound = '1';
        port.addEventListener('click', event => {
            if (consumeGraphKitWireDragClickSuppress()) {
                event.preventDefault();
                event.stopPropagation();
                return;
            }
            handleGraphKitPortClick(port.dataset.graphkitPort || '');
            event.preventDefault();
            event.stopPropagation();
        });
    });
}

function bindGraphKitBlackboardDrag() {
    $pageBody.querySelectorAll('[data-graphkit-blackboard-drag-var]').forEach(button => {
        if (button.dataset.blackboardDragBound === '1') return;
        button.dataset.blackboardDragBound = '1';
        button.addEventListener('dragstart', event => {
            const name = button.dataset.graphkitBlackboardDragVar || '';
            graphKitBlackboardDrag = { name };
            if (event.dataTransfer) {
                event.dataTransfer.effectAllowed = 'copy';
                event.dataTransfer.setData('application/x-graphkit-blackboard-var', name);
                event.dataTransfer.setData('text/plain', name);
            }
        });
        button.addEventListener('dragend', () => {
            graphKitBlackboardDrag = null;
            clearGraphKitBlackboardDropTarget();
        });
    });

    const viewport = $pageBody.querySelector('[data-graphkit-canvas-viewport]');
    if (!viewport || viewport.dataset.blackboardDropBound === '1') return;
    viewport.dataset.blackboardDropBound = '1';
    viewport.addEventListener('dragover', event => {
        if (!isGraphKitBlackboardDragEvent(event)) return;
        event.preventDefault();
        if (event.dataTransfer) event.dataTransfer.dropEffect = 'copy';
        viewport.classList.add('is-blackboard-drop-target');
    });
    viewport.addEventListener('dragleave', event => {
        if (event.relatedTarget && viewport.contains(event.relatedTarget)) return;
        viewport.classList.remove('is-blackboard-drop-target');
    });
    viewport.addEventListener('drop', event => {
        if (!isGraphKitBlackboardDragEvent(event)) return;
        event.preventDefault();
        const name = getGraphKitBlackboardDropName(event);
        const position = getGraphKitModelPositionFromPointer(event);
        graphKitBlackboardDrag = null;
        clearGraphKitBlackboardDropTarget();
        if (name) createGraphKitBlackboardReferenceNode(name, position);
    });
}

function isGraphKitBlackboardDragEvent(event) {
    if (graphKitBlackboardDrag?.name) return true;
    const types = Array.from(event.dataTransfer?.types || []);
    return types.includes('application/x-graphkit-blackboard-var');
}

function getGraphKitBlackboardDropName(event) {
    return graphKitBlackboardDrag?.name
        || event.dataTransfer?.getData('application/x-graphkit-blackboard-var')
        || event.dataTransfer?.getData('text/plain')
        || '';
}

function clearGraphKitBlackboardDropTarget() {
    $pageBody.querySelector('[data-graphkit-canvas-viewport]')?.classList.remove('is-blackboard-drop-target');
}

function bindGraphKitShortcuts() {
    const shortcutRoot = document.documentElement || document.body;
    if (shortcutRoot.dataset.graphkitShortcutsBound === '1') return;
    shortcutRoot.dataset.graphkitShortcutsBound = '1';
    document.addEventListener('keydown', event => {
        if (activePage !== 'graphkit') return;
        if (event.key === 'Escape' && typeof normalizeGraphKitSearchOverlay === 'function' && normalizeGraphKitSearchOverlay(graphKitState.searchOverlay).open) {
            toggleGraphKitSearchOverlay(false);
            event.preventDefault();
            return;
        }
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'f') {
            toggleGraphKitSearchOverlay(true);
            event.preventDefault();
            return;
        }
        const target = event.target;
        if (target && typeof target.matches === 'function' && target.matches('input, textarea, select, [contenteditable="true"]')) return;
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'z') {
            if (event.shiftKey) redoGraphKitMutation();
            else undoGraphKitMutation();
            event.preventDefault();
            return;
        }
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 's') {
            if (event.shiftKey) void saveGraphKitXmlFileAs();
            else void saveGraphKitXmlFile();
            event.preventDefault();
            return;
        }
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'a') {
            selectAllGraphKitNodes();
            event.preventDefault();
            return;
        }
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'c') {
            copyGraphKitSelectedNodes();
            event.preventDefault();
            return;
        }
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'v') {
            pasteGraphKitClipboardNodes();
            event.preventDefault();
            return;
        }
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'd') {
            duplicateGraphKitSelectedNodes();
            event.preventDefault();
            return;
        }
        if (event.key === 'Delete') {
            if (!deleteGraphKitSelectedEdge() && !deleteGraphKitSelectedBlackboardVariable() && !deleteGraphKitSelectedOrganizationItem() && !deleteGraphKitSelectedNodes()) deleteGraphKitSelectedNode();
            event.preventDefault();
        }
    });
}

function bindGraphKitPanelToggles() {
    $pageBody.querySelectorAll('[data-graphkit-panel-toggle]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => toggleGraphKitPanel(button.dataset.graphkitPanelToggle));
    });
}

function bindGraphKitNodeDrag(nodeElement) {
    if (!nodeElement || nodeElement.dataset.dragBound === '1') return;
    nodeElement.dataset.dragBound = '1';
    nodeElement.addEventListener('pointerdown', startGraphKitNodeDrag);
    nodeElement.addEventListener('pointermove', updateGraphKitNodeDrag);
    nodeElement.addEventListener('pointerup', finishGraphKitNodeDrag);
    nodeElement.addEventListener('pointercancel', finishGraphKitNodeDrag);
}

function startGraphKitNodeDrag(event) {
    if (event.button !== 0) return;
    if (event.shiftKey || event.ctrlKey || event.metaKey) return;
    const target = event.target;
    if (target && typeof target.closest === 'function' && target.closest('[data-graphkit-node-collapse]')) return;
    const nodeElement = event.currentTarget;
    const nodeId = nodeElement?.dataset?.graphkitNode || '';
    const node = graphKitProject.graph.nodes.find(candidate => candidate.id === nodeId);
    if (!node) return;
    const current = getGraphKitViewport();
    graphKitState.selectedNodeId = nodeId;
    graphKitNodeDrag = {
        nodeId,
        pointerId: event.pointerId,
        startX: event.clientX,
        startY: event.clientY,
        beforeProject: cloneGraphKitProject(graphKitProject),
        nodeX: normalizeFiniteGraphKitNumber(node.x, 0),
        nodeY: normalizeFiniteGraphKitNumber(node.y, 0),
        scale: current.scale,
        moved: false,
    };
    nodeElement.classList.add('is-dragging', 'active');
    nodeElement.setPointerCapture?.(event.pointerId);
    bindGraphKitNodeDragDocumentEvents();
    event.preventDefault();
    event.stopPropagation();
}

function bindGraphKitNodeDragDocumentEvents() {
    document.addEventListener('pointermove', updateGraphKitNodeDrag, true);
    document.addEventListener('pointerup', finishGraphKitNodeDrag, true);
    document.addEventListener('pointercancel', finishGraphKitNodeDrag, true);
}

function unbindGraphKitNodeDragDocumentEvents() {
    document.removeEventListener('pointermove', updateGraphKitNodeDrag, true);
    document.removeEventListener('pointerup', finishGraphKitNodeDrag, true);
    document.removeEventListener('pointercancel', finishGraphKitNodeDrag, true);
}

function updateGraphKitNodeDrag(event) {
    if (!graphKitNodeDrag || graphKitNodeDrag.pointerId !== event.pointerId) return;
    const viewport = $pageBody.querySelector('[data-graphkit-canvas-viewport]');
    if (!viewport) return;
    const rect = viewport.getBoundingClientRect();
    const unitX = GRAPHKIT_CANVAS_WIDTH / Math.max(1, rect.width);
    const unitY = GRAPHKIT_CANVAS_HEIGHT / Math.max(1, rect.height);
    const deltaX = (event.clientX - graphKitNodeDrag.startX) * unitX / graphKitNodeDrag.scale / GRAPHKIT_NODE_MODEL_SCALE_X;
    const deltaY = (event.clientY - graphKitNodeDrag.startY) * unitY / graphKitNodeDrag.scale / GRAPHKIT_NODE_MODEL_SCALE_Y;
    updateGraphKitNodePosition(graphKitNodeDrag.nodeId, graphKitNodeDrag.nodeX + deltaX, graphKitNodeDrag.nodeY + deltaY, false);
    applyGraphKitNodeDrag(graphKitNodeDrag.nodeId);
    graphKitNodeDrag.moved = graphKitNodeDrag.moved || Math.abs(event.clientX - graphKitNodeDrag.startX) > 2 || Math.abs(event.clientY - graphKitNodeDrag.startY) > 2;
    event.preventDefault();
    event.stopPropagation();
}

function finishGraphKitNodeDrag(event) {
    if (!graphKitNodeDrag || graphKitNodeDrag.pointerId !== event.pointerId) return;
    const drag = graphKitNodeDrag;
    const moved = drag.moved;
    const beforeProject = drag.beforeProject;
    graphKitNodeDrag = null;
    unbindGraphKitNodeDragDocumentEvents();
    const nodeElement = $pageBody.querySelector(`[data-graphkit-node="${escapeGraphKitSelectorValue(drag.nodeId)}"]`);
    nodeElement?.classList.remove('is-dragging');
    nodeElement?.releasePointerCapture?.(event.pointerId);
    if (moved) {
        if (typeof syncGraphKitPlacematMembershipForNode === 'function') syncGraphKitPlacematMembershipForNode(drag.nodeId);
        pushGraphKitHistory(beforeProject, graphKitProject, 'Move node');
        graphKitState.redoStack = [];
        persistGraphKitProject();
        renderGraphKitWorkbench();
    }
    event.preventDefault();
    event.stopPropagation();
}

function bindGraphKitViewport() {
    const viewport = $pageBody.querySelector('[data-graphkit-canvas-viewport]');
    if (!viewport || viewport.dataset.bound === '1') return;
    viewport.dataset.bound = '1';
    viewport.addEventListener('wheel', event => {
        event.preventDefault();
        zoomGraphKitViewport(event.deltaY > 0 ? 1 / GRAPHKIT_VIEWPORT_STEP : GRAPHKIT_VIEWPORT_STEP);
    }, { passive: false });
    viewport.addEventListener('pointerdown', event => {
        if (event.button !== 0 && event.button !== 1) return;
        if (event.shiftKey) return;
        const target = event.target;
        const interactiveTarget = target && typeof target.closest === 'function'
            ? target.closest('[data-graphkit-node], [data-graphkit-note], [data-graphkit-placemat], button, input, textarea, select, .graphkit-blackboard, .graphkit-inspector, .graphkit-bottom-toolbar, .graphkit-xml-dock')
            : null;
        if (interactiveTarget) return;
        const current = getGraphKitViewport();
        graphKitViewportDrag = {
            pointerId: event.pointerId,
            startX: event.clientX,
            startY: event.clientY,
            offsetX: current.offsetX,
            offsetY: current.offsetY,
            scale: current.scale,
        };
        viewport.classList.add('is-panning');
        viewport.setPointerCapture?.(event.pointerId);
        event.preventDefault();
    });
    viewport.addEventListener('pointermove', event => {
        if (!graphKitViewportDrag || graphKitViewportDrag.pointerId !== event.pointerId) return;
        const rect = viewport.getBoundingClientRect();
        const unitX = GRAPHKIT_CANVAS_WIDTH / Math.max(1, rect.width);
        const unitY = GRAPHKIT_CANVAS_HEIGHT / Math.max(1, rect.height);
        updateGraphKitViewport({
            offsetX: graphKitViewportDrag.offsetX + (event.clientX - graphKitViewportDrag.startX) * unitX / graphKitViewportDrag.scale,
            offsetY: graphKitViewportDrag.offsetY + (event.clientY - graphKitViewportDrag.startY) * unitY / graphKitViewportDrag.scale,
        });
    });
    const endPan = event => {
        if (!graphKitViewportDrag || graphKitViewportDrag.pointerId !== event.pointerId) return;
        graphKitViewportDrag = null;
        viewport.classList.remove('is-panning');
        viewport.releasePointerCapture?.(event.pointerId);
    };
    viewport.addEventListener('pointerup', endPan);
    viewport.addEventListener('pointercancel', endPan);
}

function bindGraphKitMiniMap() {
    const minimap = $pageBody.querySelector('[data-graphkit-minimap]');
    if (!minimap || minimap.dataset.bound === '1') return;
    minimap.dataset.bound = '1';
    minimap.addEventListener('pointerdown', event => {
        if (event.button !== 0) return;
        const target = event.target;
        if (target && typeof target.closest === 'function' && target.closest('button')) return;
        startGraphKitMiniMapDrag(minimap, event);
        updateGraphKitMiniMapDrag(event);
        event.preventDefault();
        event.stopPropagation();
    });
    minimap.addEventListener('pointermove', event => {
        if (!graphKitMiniMapDrag || graphKitMiniMapDrag.pointerId !== event.pointerId) return;
        updateGraphKitMiniMapDrag(event);
        event.preventDefault();
        event.stopPropagation();
    });
    const endMiniMapDrag = event => {
        if (!graphKitMiniMapDrag || graphKitMiniMapDrag.pointerId !== event.pointerId) return;
        finishGraphKitMiniMapDrag(minimap, event.pointerId);
        event.preventDefault();
        event.stopPropagation();
    };
    minimap.addEventListener('pointerup', endMiniMapDrag);
    minimap.addEventListener('pointercancel', endMiniMapDrag);
}

function startGraphKitMiniMapDrag(minimap, event) {
    const svg = minimap.querySelector('.graphkit-minimap__svg');
    const rect = svg?.getBoundingClientRect();
    if (!rect || rect.width <= 0 || rect.height <= 0) return;
    graphKitMiniMapDrag = {
        pointerId: event.pointerId,
        left: rect.left,
        top: rect.top,
        width: rect.width,
        height: rect.height,
    };
    minimap.classList.add('is-dragging');
    minimap.setPointerCapture?.(event.pointerId);
}

function updateGraphKitMiniMapDrag(event) {
    if (!graphKitMiniMapDrag || graphKitMiniMapDrag.pointerId !== event.pointerId) return;
    const x = clampGraphKitNumber(
        (event.clientX - graphKitMiniMapDrag.left) / graphKitMiniMapDrag.width * GRAPHKIT_CANVAS_WIDTH,
        0,
        0,
        GRAPHKIT_CANVAS_WIDTH
    );
    const y = clampGraphKitNumber(
        (event.clientY - graphKitMiniMapDrag.top) / graphKitMiniMapDrag.height * GRAPHKIT_CANVAS_HEIGHT,
        0,
        0,
        GRAPHKIT_CANVAS_HEIGHT
    );
    focusGraphKitMiniMapPoint(x, y);
}

function finishGraphKitMiniMapDrag(minimap, pointerId) {
    graphKitMiniMapDrag = null;
    minimap.classList.remove('is-dragging');
    minimap.releasePointerCapture?.(pointerId);
}

function focusGraphKitMiniMapPoint(canvasX, canvasY) {
    const viewport = getGraphKitViewport();
    const targetOffsetX = GRAPHKIT_CANVAS_WIDTH * 0.5 - normalizeFiniteGraphKitNumber(canvasX, 0) * viewport.scale;
    const targetOffsetY = GRAPHKIT_CANVAS_HEIGHT * 0.5 - normalizeFiniteGraphKitNumber(canvasY, 0) * viewport.scale;
    updateGraphKitViewport({
        offsetX: clampGraphKitViewportOffset(targetOffsetX, GRAPHKIT_CANVAS_WIDTH, viewport.scale),
        offsetY: clampGraphKitViewportOffset(targetOffsetY, GRAPHKIT_CANVAS_HEIGHT, viewport.scale),
    });
}

function clampGraphKitViewportOffset(offset, canvasSize, scale) {
    const minOffset = Math.min(0, canvasSize - canvasSize * scale);
    return Math.max(minOffset, Math.min(0, normalizeFiniteGraphKitNumber(offset, 0)));
}

function zoomGraphKitViewport(factor) {
    const current = getGraphKitViewport();
    updateGraphKitViewport({ scale: current.scale * factor });
}

function fitGraphKitViewportToNodes() {
    const bounds = getGraphKitNodeBounds();
    if (!bounds) {
        updateGraphKitViewport({ scale: 1, offsetX: 0, offsetY: 0 });
        return;
    }
    const paddedWidth = Math.max(1, bounds.width + GRAPHKIT_FIT_VIEW_PADDING * 2);
    const paddedHeight = Math.max(1, bounds.height + GRAPHKIT_FIT_VIEW_PADDING * 2);
    const scale = clampGraphKitNumber(
        Math.min(GRAPHKIT_CANVAS_WIDTH / paddedWidth, GRAPHKIT_CANVAS_HEIGHT / paddedHeight),
        1,
        GRAPHKIT_VIEWPORT_MIN_SCALE,
        GRAPHKIT_VIEWPORT_MAX_SCALE
    );
    updateGraphKitViewport({
        scale,
        offsetX: roundGraphKitTransformNumber(GRAPHKIT_CANVAS_WIDTH * 0.5 - bounds.centerX * scale),
        offsetY: roundGraphKitTransformNumber(GRAPHKIT_CANVAS_HEIGHT * 0.5 - bounds.centerY * scale),
    });
}

function updateGraphKitViewport(nextValues) {
    const current = getGraphKitViewport();
    graphKitState.viewport = normalizeGraphKitViewport({
        ...current,
        ...nextValues,
    });
    applyGraphKitViewport();
}

function applyGraphKitViewport() {
    const viewport = getGraphKitViewport();
    const scene = $pageBody.querySelector('[data-graphkit-scene]');
    if (scene) scene.setAttribute('transform', getGraphKitCanvasTransform());
    const shell = $pageBody.querySelector('[data-graphkit-canvas-viewport]');
    if (shell) {
        shell.style.setProperty('--graphkit-grid-x', `${viewport.offsetX}px`);
        shell.style.setProperty('--graphkit-grid-y', `${viewport.offsetY}px`);
        shell.style.setProperty('--graphkit-grid-scale', String(viewport.scale));
    }
    const label = $pageBody.querySelector('[data-graphkit-zoom-label]');
    if (label) label.textContent = formatGraphKitZoom(viewport.scale);
    applyGraphKitMiniMapViewport();
}

function applyGraphKitMiniMapViewport() {
    const viewportRect = $pageBody.querySelector('[data-graphkit-minimap-viewport]');
    if (!viewportRect) return;
    const rect = getGraphKitMiniMapViewportRect();
    viewportRect.setAttribute('x', String(rect.x));
    viewportRect.setAttribute('y', String(rect.y));
    viewportRect.setAttribute('width', String(rect.width));
    viewportRect.setAttribute('height', String(rect.height));
}

function getGraphKitViewport() {
    graphKitState.viewport = normalizeGraphKitViewport(graphKitState.viewport);
    return graphKitState.viewport;
}

function normalizeGraphKitViewport(viewport) {
    const scale = clampGraphKitNumber(Number(viewport?.scale), 1, GRAPHKIT_VIEWPORT_MIN_SCALE, GRAPHKIT_VIEWPORT_MAX_SCALE);
    return {
        scale,
        offsetX: normalizeFiniteGraphKitNumber(viewport?.offsetX, 0),
        offsetY: normalizeFiniteGraphKitNumber(viewport?.offsetY, 0),
    };
}

function clampGraphKitNumber(value, fallback, min, max) {
    const number = normalizeFiniteGraphKitNumber(value, fallback);
    return Math.max(min, Math.min(max, number));
}

function normalizeFiniteGraphKitNumber(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) ? number : fallback;
}

function formatGraphKitZoom(scale) {
    return `${Math.round(scale * 100)}%`;
}

function getGraphKitCanvasTransform() {
    const viewport = getGraphKitViewport();
    return `translate(${roundGraphKitTransformNumber(viewport.offsetX)} ${roundGraphKitTransformNumber(viewport.offsetY)}) scale(${roundGraphKitTransformNumber(viewport.scale)})`;
}

function roundGraphKitTransformNumber(value) {
    return Math.round(value * 1000) / 1000;
}
