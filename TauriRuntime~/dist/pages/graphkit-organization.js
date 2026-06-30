// pages/graphkit-organization.js
// Sticky Note and Placemat organization helpers for GraphKit.
function renderGraphKitPlacemats() {
    return getGraphKitSortedPlacemats(graphKitProject.graph.placemats).map(item => {
        const rect = getGraphKitOrganizationSceneRect(item);
        const active = graphKitState.selectedPlacematId === item.id ? ' active' : '';
        const locked = item.locked ? ' is-locked' : '';
        const collapsed = item.collapsed ? ' is-collapsed' : '';
        const renderHeight = item.collapsed ? Math.min(rect.height, 58) : rect.height;
        const resizeHandles = typeof renderGraphKitPlacematResizeHandles === 'function' ? renderGraphKitPlacematResizeHandles(item, { ...rect, height: renderHeight }) : '';
        const status = item.locked ? 'locked' : item.collapsed ? 'collapsed' : `${item.nodeIds.length} nodes`;
        return `<g class="graphkit-placemat${active}${locked}${collapsed}" data-graphkit-placemat="${escapeHtml(item.id)}" transform="translate(${escapeHtml(rect.x)}, ${escapeHtml(rect.y)})">
            <rect class="graphkit-placemat__body" width="${escapeHtml(rect.width)}" height="${escapeHtml(renderHeight)}" rx="8" style="--organization-color:${escapeHtml(item.color)}"></rect>
            <text class="graphkit-placemat__title" x="14" y="24">${escapeHtml(formatGraphKitNodeLabel(item.title, 42))}</text>
            <text class="graphkit-placemat__meta" x="14" y="43">${escapeHtml(status)}</text>
            ${resizeHandles}
        </g>`;
    }).join('');
}

function renderGraphKitStickyNotes() {
    return graphKitProject.graph.notes.map(item => {
        const rect = getGraphKitOrganizationSceneRect(item);
        const active = graphKitState.selectedNoteId === item.id ? ' active' : '';
        const lines = formatGraphKitNoteLines(item.text);
        return `<g class="graphkit-sticky-note${active}" data-graphkit-note="${escapeHtml(item.id)}" transform="translate(${escapeHtml(rect.x)}, ${escapeHtml(rect.y)})">
            <rect class="graphkit-sticky-note__body" width="${escapeHtml(rect.width)}" height="${escapeHtml(rect.height)}" rx="5" style="--organization-color:${escapeHtml(item.color)}"></rect>
            <rect class="graphkit-sticky-note__head" width="${escapeHtml(rect.width)}" height="28" rx="5" style="--organization-color:${escapeHtml(item.color)}"></rect>
            <text class="graphkit-sticky-note__title" x="12" y="19">${escapeHtml(formatGraphKitNodeLabel(item.title, 30))}</text>
            ${lines.map((line, index) => `<text class="graphkit-sticky-note__text" x="12" y="${escapeHtml(48 + index * 17)}">${escapeHtml(line)}</text>`).join('')}
        </g>`;
    }).join('');
}

function renderGraphKitOrganizationInspector(note, placemat) {
    if (note) {
        return `<div class="graphkit-inspector__shell">
        <div class="graphkit-panel-head graphkit-inspector__head">
            <div>
                <span>Graph Inspector</span>
                <strong>${escapeHtml(note.title)}</strong>
                <em>sticky note · ${escapeHtml(note.id)}</em>
            </div>
            <div class="graphkit-panel-head__actions">
                <span class="kit-state-pill kit-state-pill--ok">note</span>
                <button class="graphkit-panel-icon" type="button" data-graphkit-panel-toggle="inspector" title="收起 Inspector" aria-label="收起 Inspector">›</button>
            </div>
        </div>
        <div class="graphkit-inspector__body">
            <section class="graphkit-property-group">
                <button class="graphkit-fold-title" type="button">Sticky Note</button>
                ${renderGraphKitOrganizationField('note', 'title', 'Title', note.title)}
                <label class="graphkit-property-field">
                    <span>Text<em>designer note</em></span>
                    <textarea class="cmd-input graphkit-property-textarea" data-graphkit-note-field="text">${escapeHtml(note.text)}</textarea>
                </label>
                ${renderGraphKitOrganizationField('note', 'color', 'Color', note.color)}
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="delete-node">删除备注</button>
            </section>
        </div>
    </div>`;
    }
    return `<div class="graphkit-inspector__shell">
        <div class="graphkit-panel-head graphkit-inspector__head">
            <div>
                <span>Graph Inspector</span>
                <strong>${escapeHtml(placemat.title)}</strong>
                <em>placemat · ${escapeHtml(placemat.id)}</em>
            </div>
            <div class="graphkit-panel-head__actions">
                <span class="kit-state-pill kit-state-pill--ok">placemat</span>
                <button class="graphkit-panel-icon" type="button" data-graphkit-panel-toggle="inspector" title="收起 Inspector" aria-label="收起 Inspector">›</button>
            </div>
        </div>
        <div class="graphkit-inspector__body">
            <section class="graphkit-property-group">
                <button class="graphkit-fold-title" type="button">Placemat</button>
                ${renderGraphKitOrganizationField('placemat', 'title', 'Title', placemat.title)}
                ${renderGraphKitOrganizationField('placemat', 'color', 'Color', placemat.color)}
                <label class="graphkit-property-field">
                    <span>Locked<em>prevent move / resize</em></span>
                    <input class="graphkit-property-check" type="checkbox" data-graphkit-placemat-field="locked" ${placemat.locked ? 'checked' : ''}>
                </label>
                <label class="graphkit-property-field">
                    <span>Collapsed<em>compact placemat</em></span>
                    <input class="graphkit-property-check" type="checkbox" data-graphkit-placemat-field="collapsed" ${placemat.collapsed ? 'checked' : ''}>
                </label>
                <div class="graphkit-z-actions">
                    <button class="btn btn-secondary btn-sm" type="button" data-graphkit-placemat-z="backward">后移层级</button>
                    <button class="btn btn-secondary btn-sm" type="button" data-graphkit-placemat-z="forward">前移层级</button>
                </div>
                ${renderGraphKitOrganizationField('placemat', 'nodeIds', 'Members', placemat.nodeIds.join(', '))}
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="delete-node">删除分组</button>
            </section>
        </div>
    </div>`;
}

function renderGraphKitOrganizationField(kind, field, label, value) {
    if (kind === 'note') {
        return `<label class="graphkit-property-field">
            <span>${escapeHtml(label)}<em>${escapeHtml(field)}</em></span>
            <input class="cmd-input" type="text" data-graphkit-note-field="${escapeHtml(field)}" value="${escapeHtml(value)}">
        </label>`;
    }
    return `<label class="graphkit-property-field">
        <span>${escapeHtml(label)}<em>${escapeHtml(field)}</em></span>
        <input class="cmd-input" type="text" data-graphkit-placemat-field="${escapeHtml(field)}" value="${escapeHtml(value)}">
    </label>`;
}

function bindGraphKitOrganizationItems() {
    bindGraphKitOrganizationItemSet('[data-graphkit-note]', 'note');
    bindGraphKitOrganizationItemSet('[data-graphkit-placemat]', 'placemat');
    $pageBody.querySelectorAll('[data-graphkit-note-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('input', () => updateGraphKitSelectedNoteField(input.dataset.graphkitNoteField, input.value));
    });
    $pageBody.querySelectorAll('[data-graphkit-placemat-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('input', () => updateGraphKitSelectedPlacematField(input.dataset.graphkitPlacematField, input.type === 'checkbox' ? input.checked : input.value));
    });
    $pageBody.querySelectorAll('[data-graphkit-placemat-z]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => updateGraphKitSelectedPlacematZOrder(button.dataset.graphkitPlacematZ));
    });
}

function bindGraphKitOrganizationItemSet(selector, kind) {
    $pageBody.querySelectorAll(selector).forEach(element => {
        if (element.dataset.bound === '1') return;
        element.dataset.bound = '1';
        element.addEventListener('click', event => {
            if (kind === 'note') selectGraphKitStickyNote(element.dataset.graphkitNote || '');
            else selectGraphKitPlacemat(element.dataset.graphkitPlacemat || '');
            event.preventDefault();
            event.stopPropagation();
        });
        bindGraphKitOrganizationDrag(element, kind);
    });
}

function bindGraphKitOrganizationDrag(element, kind) {
    if (!element || element.dataset.dragBound === '1') return;
    element.dataset.dragBound = '1';
    element.addEventListener('pointerdown', event => {
        if (event.button !== 0 || event.shiftKey || event.ctrlKey || event.metaKey) return;
        const id = kind === 'note' ? element.dataset.graphkitNote || '' : element.dataset.graphkitPlacemat || '';
        const item = getGraphKitOrganizationItem(kind, id);
        if (!item) return;
        if (kind === 'placemat' && item.locked) return;
        graphKitOrganizationDrag = {
            kind,
            id,
            pointerId: event.pointerId,
            startX: event.clientX,
            startY: event.clientY,
            beforeProject: cloneGraphKitProject(graphKitProject),
            itemX: item.x,
            itemY: item.y,
            scale: getGraphKitViewport().scale,
            moved: false,
        };
        element.classList.add('is-dragging', 'active');
        element.setPointerCapture?.(event.pointerId);
        event.preventDefault();
        event.stopPropagation();
    });
    element.addEventListener('pointermove', updateGraphKitOrganizationDrag);
    element.addEventListener('pointerup', finishGraphKitOrganizationDrag);
    element.addEventListener('pointercancel', finishGraphKitOrganizationDrag);
}

function updateGraphKitOrganizationDrag(event) {
    if (!graphKitOrganizationDrag || graphKitOrganizationDrag.pointerId !== event.pointerId) return;
    const viewport = $pageBody.querySelector('[data-graphkit-canvas-viewport]');
    if (!viewport) return;
    const rect = viewport.getBoundingClientRect();
    const unitX = GRAPHKIT_CANVAS_WIDTH / Math.max(1, rect.width);
    const unitY = GRAPHKIT_CANVAS_HEIGHT / Math.max(1, rect.height);
    const deltaX = (event.clientX - graphKitOrganizationDrag.startX) * unitX / graphKitOrganizationDrag.scale / GRAPHKIT_NODE_MODEL_SCALE_X;
    const deltaY = (event.clientY - graphKitOrganizationDrag.startY) * unitY / graphKitOrganizationDrag.scale / GRAPHKIT_NODE_MODEL_SCALE_Y;
    updateGraphKitOrganizationPosition(graphKitOrganizationDrag.kind, graphKitOrganizationDrag.id, graphKitOrganizationDrag.itemX + deltaX, graphKitOrganizationDrag.itemY + deltaY, false);
    applyGraphKitOrganizationTransform(graphKitOrganizationDrag.kind, graphKitOrganizationDrag.id);
    if (graphKitOrganizationDrag.kind === 'placemat') applyGraphKitPlacematMemberTransforms(graphKitOrganizationDrag.id);
    graphKitOrganizationDrag.moved = graphKitOrganizationDrag.moved || Math.abs(event.clientX - graphKitOrganizationDrag.startX) > 2 || Math.abs(event.clientY - graphKitOrganizationDrag.startY) > 2;
    event.preventDefault();
    event.stopPropagation();
}

function finishGraphKitOrganizationDrag(event) {
    if (!graphKitOrganizationDrag || graphKitOrganizationDrag.pointerId !== event.pointerId) return;
    const drag = graphKitOrganizationDrag;
    graphKitOrganizationDrag = null;
    const selector = drag.kind === 'note' ? `[data-graphkit-note="${escapeGraphKitSelectorValue(drag.id)}"]` : `[data-graphkit-placemat="${escapeGraphKitSelectorValue(drag.id)}"]`;
    const element = $pageBody.querySelector(selector);
    element?.classList.remove('is-dragging');
    element?.releasePointerCapture?.(event.pointerId);
    if (drag.moved) {
        pushGraphKitHistory(drag.beforeProject, graphKitProject, `Move ${drag.kind}`);
        graphKitState.redoStack = [];
        persistGraphKitProject();
        renderGraphKitWorkbench();
    }
}

function createGraphKitStickyNote(position) {
    const center = position || getGraphKitViewportCenterModelPosition();
    const note = {
        id: makeUniqueGraphKitNoteId('note'),
        title: 'Sticky Note',
        text: '记录策划意图、条件或运行时 handler 约定。',
        x: Math.round(center.x),
        y: Math.round(center.y),
        width: 220,
        height: 118,
        color: '#6f5a2e',
    };
    commitGraphKitMutation('Add sticky note', before => ({
        ...before,
        graph: {
            ...before.graph,
            notes: [...before.graph.notes, note],
        },
    }), { selectedNoteId: note.id, noticeLevel: 'success', noticeText: `已添加备注 ${note.id}。` });
}

function createGraphKitPlacemat(position) {
    const selectedIds = getGraphKitSelectedNodeIds();
    const rect = selectedIds.length ? getGraphKitSelectedNodeModelBounds(selectedIds) : null;
    const center = position || getGraphKitViewportCenterModelPosition();
    const placemat = {
        id: makeUniqueGraphKitPlacematId('placemat'),
        title: 'Placemat',
        x: rect ? Math.floor(rect.x - 24) : Math.round(center.x),
        y: rect ? Math.floor(rect.y - 24) : Math.round(center.y),
        width: rect ? Math.ceil(rect.width + 48) : 360,
        height: rect ? Math.ceil(rect.height + 48) : 210,
        color: '#34566f',
        order: getNextGraphKitPlacematOrder(graphKitProject.graph.placemats),
        locked: false,
        collapsed: false,
        nodeIds: selectedIds,
    };
    commitGraphKitMutation('Add placemat', before => ({
        ...before,
        graph: {
            ...before.graph,
            placemats: [...before.graph.placemats, placemat],
        },
    }), { selectedPlacematId: placemat.id, noticeLevel: 'success', noticeText: `已添加分组 ${placemat.id}。` });
}

function selectGraphKitStickyNote(id) {
    if (!graphKitProject.graph.notes.some(item => item.id === id)) return;
    graphKitState.selectedNoteId = id;
    graphKitState.selectedPlacematId = '';
    graphKitState.selectedNodeId = '';
    graphKitState.selectedNodeIds = [];
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.pendingPortEndpoint = '';
    renderGraphKitWorkbench();
}

function selectGraphKitPlacemat(id) {
    if (!graphKitProject.graph.placemats.some(item => item.id === id)) return;
    graphKitState.selectedPlacematId = id;
    graphKitState.selectedNoteId = '';
    graphKitState.selectedNodeId = '';
    graphKitState.selectedNodeIds = [];
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.pendingPortEndpoint = '';
    renderGraphKitWorkbench();
}

function updateGraphKitSelectedNoteField(field, value) {
    const id = graphKitState.selectedNoteId;
    if (!id) return;
    commitGraphKitMutation('Edit sticky note', before => ({
        ...before,
        graph: {
            ...before.graph,
            notes: before.graph.notes.map(item => item.id === id ? { ...item, [field]: value } : item),
        },
    }), { selectedNoteId: id });
}

function updateGraphKitSelectedPlacematField(field, value) {
    const id = graphKitState.selectedPlacematId;
    if (!id) return;
    const nextValue = field === 'nodeIds'
        ? String(value || '').split(',').map(item => item.trim()).filter(Boolean)
        : field === 'locked' || field === 'collapsed'
            ? Boolean(value)
        : value;
    commitGraphKitMutation('Edit placemat', before => ({
        ...before,
        graph: {
            ...before.graph,
            placemats: before.graph.placemats.map(item => item.id === id ? { ...item, [field]: nextValue } : item),
        },
    }), { selectedPlacematId: id });
}

function updateGraphKitSelectedPlacematZOrder(direction) {
    const id = graphKitState.selectedPlacematId;
    if (!id) return;
    commitGraphKitMutation('Change placemat order', before => moveGraphKitProjectPlacematZOrder(before, id, direction), { selectedPlacematId: id });
}

function deleteGraphKitSelectedOrganizationItem() {
    const noteId = graphKitState.selectedNoteId;
    const placematId = graphKitState.selectedPlacematId;
    if (!noteId && !placematId) return false;
    commitGraphKitMutation(noteId ? 'Delete sticky note' : 'Delete placemat', before => ({
        ...before,
        graph: {
            ...before.graph,
            notes: noteId ? before.graph.notes.filter(item => item.id !== noteId) : before.graph.notes,
            placemats: placematId ? before.graph.placemats.filter(item => item.id !== placematId) : before.graph.placemats,
        },
    }), { selectedNoteId: '', selectedPlacematId: '', noticeLevel: 'warning', noticeText: noteId ? `已删除备注 ${noteId}。` : `已删除分组 ${placematId}。` });
    return true;
}

function updateGraphKitOrganizationPosition(kind, id, x, y, persist = true) {
    if (kind === 'placemat') {
        graphKitProject = moveGraphKitProjectPlacemat(graphKitProject, id, x, y);
        if (persist) persistGraphKitProject();
        return;
    }
    const key = kind === 'note' ? 'notes' : 'placemats';
    graphKitProject = {
        ...graphKitProject,
        graph: {
            ...graphKitProject.graph,
            [key]: graphKitProject.graph[key].map(item => item.id === id ? {
                ...item,
                x: roundGraphKitTransformNumber(x),
                y: roundGraphKitTransformNumber(y),
            } : item),
        },
    };
    if (persist) persistGraphKitProject();
}

function moveGraphKitProjectPlacemat(project, placematId, x, y) {
    const source = project && typeof project === 'object' ? project : {};
    const graph = source.graph && typeof source.graph === 'object' ? source.graph : {};
    const placemats = Array.isArray(graph.placemats) ? graph.placemats : [];
    const target = placemats.find(item => item.id === placematId);
    if (!target) return source;
    if (target.locked) return source;

    const nextX = roundGraphKitOrganizationNumber(x, normalizeGraphKitOrganizationNumber(target.x, 0));
    const nextY = roundGraphKitOrganizationNumber(y, normalizeGraphKitOrganizationNumber(target.y, 0));
    const deltaX = nextX - normalizeGraphKitOrganizationNumber(target.x, 0);
    const deltaY = nextY - normalizeGraphKitOrganizationNumber(target.y, 0);
    const memberIds = new Set(Array.isArray(target.nodeIds) ? target.nodeIds : []);

    return {
        ...source,
        graph: {
            ...graph,
            nodes: (Array.isArray(graph.nodes) ? graph.nodes : []).map(node => {
                if (!memberIds.has(node.id)) return node;
                return {
                    ...node,
                    x: roundGraphKitOrganizationNumber(normalizeGraphKitOrganizationNumber(node.x, 0) + deltaX, 0),
                    y: roundGraphKitOrganizationNumber(normalizeGraphKitOrganizationNumber(node.y, 0) + deltaY, 0),
                    fields: { ...(node.fields || {}) },
                };
            }),
            placemats: placemats.map(item => item.id === placematId ? {
                ...item,
                x: nextX,
                y: nextY,
                nodeIds: Array.isArray(item.nodeIds) ? [...item.nodeIds] : [],
            } : item),
        },
    };
}

function getGraphKitSortedPlacemats(placemats) {
    return (Array.isArray(placemats) ? placemats : [])
        .map((item, index) => ({ item, index, order: normalizeGraphKitOrganizationNumber(item?.order, index) }))
        .sort((left, right) => left.order - right.order || left.index - right.index)
        .map(entry => entry.item);
}

function moveGraphKitProjectPlacematZOrder(project, placematId, direction) {
    const source = project && typeof project === 'object' ? project : {};
    const graph = source.graph && typeof source.graph === 'object' ? source.graph : {};
    const placemats = Array.isArray(graph.placemats) ? graph.placemats : [];
    const sorted = getGraphKitSortedPlacemats(placemats);
    const index = sorted.findIndex(item => item.id === placematId);
    if (index < 0) return source;
    const offset = direction === 'backward' ? -1 : 1;
    const nextIndex = Math.max(0, Math.min(sorted.length - 1, index + offset));
    if (nextIndex === index) return source;

    const nextSorted = [...sorted];
    [nextSorted[index], nextSorted[nextIndex]] = [nextSorted[nextIndex], nextSorted[index]];
    const orderById = new Map(nextSorted.map((item, order) => [item.id, order]));
    return {
        ...source,
        graph: {
            ...graph,
            placemats: placemats.map((item, fallbackOrder) => ({
                ...item,
                order: orderById.has(item.id) ? orderById.get(item.id) : normalizeGraphKitOrganizationNumber(item.order, fallbackOrder),
                nodeIds: Array.isArray(item.nodeIds) ? [...item.nodeIds] : [],
            })),
        },
    };
}

function getNextGraphKitPlacematOrder(placemats) {
    return getGraphKitSortedPlacemats(placemats).reduce((maxOrder, item, index) => {
        return Math.max(maxOrder, normalizeGraphKitOrganizationNumber(item.order, index));
    }, -1) + 1;
}

function applyGraphKitOrganizationTransform(kind, id) {
    const item = getGraphKitOrganizationItem(kind, id);
    if (!item) return;
    const selector = kind === 'note' ? `[data-graphkit-note="${escapeGraphKitSelectorValue(id)}"]` : `[data-graphkit-placemat="${escapeGraphKitSelectorValue(id)}"]`;
    const element = $pageBody.querySelector(selector);
    if (!element) return;
    const rect = getGraphKitOrganizationSceneRect(item);
    element.setAttribute('transform', `translate(${roundGraphKitTransformNumber(rect.x)}, ${roundGraphKitTransformNumber(rect.y)})`);
}

function applyGraphKitPlacematMemberTransforms(id) {
    const item = getGraphKitOrganizationItem('placemat', id);
    if (!item || !Array.isArray(item.nodeIds)) return;
    item.nodeIds.forEach(nodeId => applyGraphKitNodeDrag(nodeId));
}

function getGraphKitOrganizationItem(kind, id) {
    const key = kind === 'note' ? 'notes' : 'placemats';
    return graphKitProject.graph[key].find(item => item.id === id) || null;
}

function getGraphKitOrganizationSceneRect(item) {
    return {
        x: GRAPHKIT_NODE_ORIGIN_X + normalizeFiniteGraphKitNumber(item.x, 0) * GRAPHKIT_NODE_MODEL_SCALE_X,
        y: GRAPHKIT_NODE_ORIGIN_Y + normalizeFiniteGraphKitNumber(item.y, 0) * GRAPHKIT_NODE_MODEL_SCALE_Y,
        width: normalizeFiniteGraphKitNumber(item.width, 180) * GRAPHKIT_NODE_MODEL_SCALE_X,
        height: normalizeFiniteGraphKitNumber(item.height, 110) * GRAPHKIT_NODE_MODEL_SCALE_Y,
    };
}

function getGraphKitSelectedNodeModelBounds(nodeIds) {
    const idSet = new Set(nodeIds);
    const nodes = graphKitProject.graph.nodes.filter(node => idSet.has(node.id));
    return nodes.reduce((bounds, node) => {
        const type = getGraphKitNodeType(node.type);
        const width = GRAPHKIT_NODE_WIDTH / GRAPHKIT_NODE_MODEL_SCALE_X;
        const height = getGraphKitNodeHeight(type, node) / GRAPHKIT_NODE_MODEL_SCALE_Y;
        const next = {
            minX: Math.min(bounds.minX, node.x),
            minY: Math.min(bounds.minY, node.y),
            maxX: Math.max(bounds.maxX, node.x + width),
            maxY: Math.max(bounds.maxY, node.y + height),
        };
        next.x = next.minX;
        next.y = next.minY;
        next.width = next.maxX - next.minX;
        next.height = next.maxY - next.minY;
        return next;
    }, { minX: Infinity, minY: Infinity, maxX: -Infinity, maxY: -Infinity, x: 0, y: 0, width: 360, height: 210 });
}

function formatGraphKitNoteLines(text) {
    const source = String(text || '').replace(/\s+/g, ' ').trim();
    if (!source) return ['No note text'];
    const lines = [];
    for (let index = 0; index < source.length && lines.length < 4; index += 24) {
        lines.push(formatGraphKitNodeLabel(source.slice(index, index + 24), 24));
    }
    return lines;
}

function normalizeGraphKitOrganizationNumber(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) ? number : fallback;
}

function roundGraphKitOrganizationNumber(value, fallback) {
    return Math.round(normalizeGraphKitOrganizationNumber(value, fallback) * 1000) / 1000;
}

const graphKitOrganizationApi = {
    getGraphKitSortedPlacemats,
    moveGraphKitProjectPlacemat,
    moveGraphKitProjectPlacematZOrder,
};

if (typeof module !== 'undefined' && module.exports) {
    module.exports = graphKitOrganizationApi;
}

if (typeof window !== 'undefined') {
    Object.assign(window, graphKitOrganizationApi);
}
