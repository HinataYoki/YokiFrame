// pages/graphkit-render.js
// GraphKit SVG/DOM rendering for the Luban XML graph editor.
function renderGraphKitPanel() {
    graphKitProject = sanitizeGraphKitProject(graphKitProject);
    const report = validateGraphKitProject(graphKitProject);
    const selectedNode = getGraphKitSelectedNode();
    const selectedNodeType = getGraphKitNodeType(selectedNode?.type);
    const selectedEdge = getGraphKitSelectedEdge();
    const selectedBlackboard = getGraphKitSelectedBlackboardVariable();
    const selectedNote = getGraphKitSelectedNote();
    const selectedPlacemat = getGraphKitSelectedPlacemat();
    const selectedNodeTypeDefinition = getGraphKitSelectedNodeType();
    const xml = serializeGraphKitXml(graphKitProject);
    const contract = getGraphKitRuntimeContract(graphKitProject);
    const runtimeContractJson = formatGraphKitRuntimeContractJson(contract);
    const scaffold = generateGraphKitHandlerScaffold(graphKitProject);
    const lubanDefinition = generateGraphKitLubanDefinitionXml(graphKitProject);
    const lubanData = generateGraphKitLubanDataXml(graphKitProject);
    const panels = getGraphKitPanels();
    const fileBusy = !!graphKitState.fileBusy;
    const panelClass = [
        panels.blackboardCollapsed ? 'is-blackboard-collapsed' : '',
        panels.inspectorCollapsed ? 'is-inspector-collapsed' : '',
    ].filter(Boolean).join(' ');

    return `<section class="graphkit-window tablekit-graph-section ${panelClass}" data-graphkit-window>
        <div class="kit-toolbar graphkit-window-toolbar">
            <div class="graphkit-window-toolbar__main">
                <div class="graphkit-tab-strip">
                    ${renderGraphKitGraphTabs()}
                </div>
                ${renderGraphKitGraphBreadcrumb()}
                <div class="graphkit-window-meta">
                    <span>${escapeHtml(graphKitProject.graph.kind)}</span>
                    <span>${escapeHtml(`${graphKitProject.graphs.length} graphs`)}</span>
                    <span>${escapeHtml(`${graphKitProject.nodeTypes.length} node types`)}</span>
                    <span>${escapeHtml(`${graphKitProject.graph.edges.length} edges`)}</span>
                    <span title="${escapeHtml(graphKitState.filePath || '')}">${escapeHtml(graphKitState.fileName || 'Untitled Graph XML')}</span>
                </div>
            </div>
            <div class="kit-toolbar__actions graphkit-window-toolbar__actions">
                ${renderGraphKitTemplateSelect()}
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="open-xml-file" ${fileBusy ? 'disabled' : ''}>打开 XML</button>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="save-xml-file" ${fileBusy ? 'disabled' : ''}>保存</button>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="save-xml-file-as" ${fileBusy ? 'disabled' : ''}>另存为</button>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="undo" ${canUndoGraphKit() ? '' : 'disabled'}>撤销</button>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="redo" ${canRedoGraphKit() ? '' : 'disabled'}>重做</button>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="add-node">添加节点</button>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="add-subgraph">添加子图</button>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="add-note">添加备注</button>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="add-placemat">添加分组</button>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="duplicate-node" ${selectedNode ? '' : 'disabled'}>复制节点</button>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="delete-node" ${selectedNode || selectedEdge || selectedBlackboard || selectedNote || selectedPlacemat ? '' : 'disabled'}>${escapeHtml(getGraphKitDeleteSelectionLabel(selectedNode, selectedEdge, selectedBlackboard, selectedNote, selectedPlacemat))}</button>
                <span class="kit-state-pill ${report.ok ? 'kit-state-pill--ok' : 'kit-state-pill--danger'}">${escapeHtml(report.ok ? 'Graph Valid' : 'Graph Invalid')}</span>
                <span class="graphkit-dirty-dot ${graphKitState.dirty ? 'is-dirty' : ''}" title="${escapeHtml(graphKitState.dirty ? '存在未保存编辑' : '无未保存编辑')}"></span>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="copy-xml">复制 XML</button>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="reset">重置示例</button>
            </div>
        </div>
        <div class="graphkit-editor-shell">
            <div class="graphkit-canvas-grid" data-graphkit-canvas-viewport style="${renderGraphKitViewportStyle()}" tabindex="0" role="application" aria-label="${escapeHtml(graphKitProject.graph.title)}">
                ${renderGraphKitCanvas(selectedNode)}
            </div>
            ${renderGraphKitBlackboard(selectedNodeType)}
            <aside class="graphkit-inspector">
                ${renderGraphKitInspector(selectedNode, selectedNodeType, report, selectedEdge, selectedBlackboard, selectedNote, selectedPlacemat, selectedNodeTypeDefinition)}
            </aside>
            ${renderGraphKitPanelRail('blackboard', 'Blackboard', panels.blackboardCollapsed)}
            ${renderGraphKitPanelRail('inspector', 'Inspector', panels.inspectorCollapsed)}
            ${renderGraphKitMiniMap()}
            ${renderGraphKitIssuesOverlay(report)}
            ${renderGraphKitSearchOverlay()}
            ${renderGraphKitContextMenu()}
            ${renderGraphKitBottomToolbar(report)}
            ${renderGraphKitXmlDock(xml, contract, report)}
            ${renderGraphKitRuntimeContractDock(runtimeContractJson, contract)}
            ${renderGraphKitScaffoldDock(scaffold, contract)}
            ${renderGraphKitLubanDock(lubanDefinition, lubanData, contract)}
        </div>
    </section>`;
}

function renderGraphKitTemplateSelect() {
    const templates = typeof getGraphKitTemplates === 'function' ? getGraphKitTemplates() : [];
    if (!templates.length) return '';
    const selectedId = typeof getGraphKitTemplateIdForProject === 'function'
        ? getGraphKitTemplateIdForProject(graphKitProject)
        : '';
    const options = templates.map(template => {
        const selected = template.id === selectedId ? ' selected' : '';
        return `<option value="${escapeHtml(template.id)}"${selected}>${escapeHtml(template.title)}</option>`;
    }).join('');
    return `<label class="graphkit-template-picker" title="加载 GraphKit 样例">
        <span>样例</span>
        <select class="cmd-input graphkit-template-picker__select" data-graphkit-template-select>
            <option value="">选择模板</option>
            ${options}
        </select>
    </label>`;
}

function renderGraphKitGraphTabs() {
    const graphs = Array.isArray(graphKitProject.graphs) && graphKitProject.graphs.length ? graphKitProject.graphs : [graphKitProject.graph];
    return graphs.map(graph => {
        const active = graph.id === graphKitProject.graph.id ? ' graphkit-tab--active' : '';
        return `<button class="graphkit-tab${active}" type="button" data-graphkit-select-graph="" data-graphkit-graph-id="${escapeHtml(graph.id)}">${escapeHtml(graph.title)}</button>`;
    }).join('');
}

function renderGraphKitGraphBreadcrumb() {
    const path = typeof getGraphKitGraphNavigationPath === 'function'
        ? getGraphKitGraphNavigationPath(graphKitProject, graphKitProject.graph.id)
        : [graphKitProject.graph];
    const crumbs = path.map((graph, index) => {
        const active = graph.id === graphKitProject.graph.id;
        const disabled = active ? ' disabled aria-current="page"' : '';
        const separator = index === 0 ? '' : '<span class="graphkit-breadcrumb__sep">/</span>';
        return `${separator}<button class="graphkit-breadcrumb__item${active ? ' is-active' : ''}" type="button" data-graphkit-select-graph="" data-graphkit-graph-id="${escapeHtml(graph.id)}"${disabled}>${escapeHtml(graph.title)}</button>`;
    }).join('');
    return `<nav class="graphkit-breadcrumb" data-graphkit-breadcrumb aria-label="Graph path">${crumbs}</nav>`;
}

function renderGraphKitViewportStyle() {
    const viewport = getGraphKitViewport();
    return [
        `--graphkit-grid-x:${escapeHtml(viewport.offsetX)}px`,
        `--graphkit-grid-y:${escapeHtml(viewport.offsetY)}px`,
        `--graphkit-grid-scale:${escapeHtml(viewport.scale)}`,
    ].join(';');
}

function renderGraphKitBlackboard(selectedNodeType) {
    const variables = Array.isArray(graphKitProject.graph.blackboard) ? graphKitProject.graph.blackboard : [];
    const variableRows = renderGraphKitBlackboardSections(variables);

    return `<aside class="graphkit-blackboard" data-kit-scroll-key="graphkit-blackboard">
        <div class="graphkit-panel-head">
            <div>
                <span>Blackboard</span>
                <strong>${escapeHtml(graphKitProject.graph.title)}</strong>
                <em>${escapeHtml(graphKitProject.graph.id)}</em>
            </div>
            <button class="graphkit-panel-icon" type="button" data-graphkit-panel-toggle="blackboard" title="收起 Blackboard" aria-label="收起 Blackboard">‹</button>
            <button class="graphkit-panel-icon" type="button" data-graphkit-action="add-blackboard-var" title="添加变量" aria-label="添加变量">＋</button>
        </div>
        <div class="graphkit-blackboard-list">${variableRows}</div>
        <div class="graphkit-node-library">
            <div class="graphkit-node-library__head">
                <div class="graphkit-node-library__title">
                    <strong>Node Library</strong>
                    <button class="graphkit-panel-icon" type="button" data-graphkit-action="add-node-type" title="添加节点类型" aria-label="添加节点类型">＋</button>
                </div>
                <input class="cmd-input graphkit-search" type="search" data-graphkit-search value="${escapeHtml(graphKitState.searchTerm)}" placeholder="Search type / handler">
            </div>
            <div class="graphkit-node-library__list graphkit-type-registry-list">
                ${renderGraphKitNodeTypeList(selectedNodeType)}
            </div>
        </div>
    </aside>`;
}

function renderGraphKitBlackboardSections(variables) {
    if (!variables.length) return `<div class="graphkit-empty-line">暂无 Blackboard 变量。</div>`;
    const sections = new Map();
    variables.forEach(item => {
        const section = normalizeGraphKitBlackboardSection(item.section);
        if (!sections.has(section)) sections.set(section, []);
        sections.get(section).push(item);
    });

    return [...sections.entries()].map(([section, items]) => `<section class="graphkit-blackboard-section" data-graphkit-blackboard-section="${escapeHtml(section)}">
        <div class="graphkit-blackboard-section__head">
            <strong>${escapeHtml(section)}</strong>
            <span>${escapeHtml(`${items.length} vars`)}</span>
        </div>
        ${items.map(renderGraphKitBlackboardItem).join('')}
    </section>`).join('');
}

function renderGraphKitBlackboardItem(item) {
    return `<button class="graphkit-blackboard-item ${graphKitState.selectedBlackboardName === item.name ? 'active' : ''}" type="button" draggable="true" data-graphkit-blackboard-var="${escapeHtml(item.name)}" data-graphkit-blackboard-drag-var="${escapeHtml(item.name)}" title="${escapeHtml(item.name)}">
        <span class="graphkit-blackboard-item__mark" aria-hidden="true"></span>
        <span>
            <strong>${escapeHtml(item.name)}</strong>
            <em>${escapeHtml(item.type)}</em>
        </span>
        <code>${escapeHtml(formatGraphKitFieldValue(item.defaultValue))}</code>
    </button>`;
}

function renderGraphKitPanelRail(panel, label, collapsed) {
    const side = panel === 'inspector' ? 'inspector' : 'blackboard';
    const marker = side === 'inspector' ? '‹' : '›';
    return `<button class="graphkit-panel-rail graphkit-panel-rail--${escapeHtml(side)}" type="button" data-graphkit-panel-toggle="${escapeHtml(side)}" aria-expanded="${escapeHtml(!collapsed)}" title="${escapeHtml(collapsed ? `展开 ${label}` : `收起 ${label}`)}">
        <span>${escapeHtml(label)}</span>
        <strong aria-hidden="true">${escapeHtml(marker)}</strong>
    </button>`;
}

function renderGraphKitNodeTypeList(selectedNodeType) {
    const query = String(graphKitState.searchTerm || '').trim().toLowerCase();
    const types = graphKitProject.nodeTypes.filter(type => {
        if (!query) return true;
        return `${type.id} ${type.title} ${type.category} ${type.handlerId}`.toLowerCase().includes(query);
    });
    if (!types.length) return `<div class="graphkit-empty-line">没有匹配的节点类型。</div>`;

    return types.map(type => {
        const active = selectedNodeType?.id === type.id ? ' active' : '';
        return `<div class="graphkit-node-type-row">
        <button class="graphkit-node-type${active}" type="button" data-graphkit-type="${escapeHtml(type.id)}">
            <span class="graphkit-node-type__color" style="--node-color:${escapeHtml(type.color)}"></span>
            <span>
                <strong>${escapeHtml(type.title)}</strong>
                <em>${escapeHtml(type.id)}</em>
            </span>
            <code>${escapeHtml(type.handlerId || 'missing handler')}</code>
        </button>
        <button class="graphkit-node-type__edit" type="button" data-graphkit-type-edit="${escapeHtml(type.id)}" title="编辑节点类型" aria-label="编辑节点类型">Edit</button>
    </div>`;
    }).join('');
}

function renderGraphKitCanvas(selectedNode) {
    const graph = graphKitProject.graph;
    const renderMode = getGraphKitCanvasRenderMode(graph);
    const edges = graph.edges.map(edge => renderGraphKitEdge(edge)).join('');
    const edgeLabels = renderGraphKitEdgeLabels(graph.edges);
    const edgeHandles = graph.edges.map(edge => renderGraphKitEdgeReconnectHandles(edge)).join('');
    const nodes = graph.nodes.map(node => renderGraphKitNode(node, selectedNode, renderMode)).join('');
    return `<svg class="graphkit-canvas tablekit-graph-canvas" data-graphkit-render-mode="${escapeHtml(renderMode.level)}" viewBox="0 0 ${GRAPHKIT_CANVAS_WIDTH} ${GRAPHKIT_CANVAS_HEIGHT}" role="img" aria-label="${escapeHtml(graph.title)}">
        <defs>
            <marker id="graphkit-arrow" markerWidth="12" markerHeight="12" refX="10" refY="4" orient="auto">
                <path d="M0,0 L0,8 L11,4 z"></path>
            </marker>
        </defs>
        <g class="graphkit-scene" data-graphkit-scene data-graphkit-render-mode="${escapeHtml(renderMode.level)}" transform="${escapeHtml(getGraphKitCanvasTransform())}">
            ${renderGraphKitPlacemats()}
            ${edges}
            ${edgeLabels}
            ${renderGraphKitWireDragPreview()}
            ${renderGraphKitSelectionRect()}
            ${nodes}
            ${edgeHandles}
            ${renderGraphKitStickyNotes()}
        </g>
    </svg>`;
}

function getGraphKitCanvasRenderMode(graph) {
    const nodeCount = Array.isArray(graph?.nodes) ? graph.nodes.length : 0;
    const viewport = typeof getGraphKitViewport === 'function'
        ? getGraphKitViewport()
        : { scale: 1 };
    const rawScale = Number(viewport?.scale);
    const scale = Number.isFinite(rawScale) ? rawScale : 1;
    const compact = nodeCount >= GRAPHKIT_LOD_NODE_COUNT_THRESHOLD && scale <= GRAPHKIT_LOD_SCALE_THRESHOLD;
    return {
        compact,
        level: compact ? 'compact' : 'full',
        nodeCount,
        scale,
    };
}

function shouldRenderGraphKitNodeCompact(node, selectedNode, renderMode) {
    if (!renderMode?.compact) return false;
    if (selectedNode?.id && selectedNode.id === node?.id) return false;
    if (typeof isGraphKitNodeSelected === 'function' && isGraphKitNodeSelected(node?.id)) return false;
    return true;
}

function renderGraphKitMiniMap() {
    const nodes = graphKitProject.graph.nodes.map(node => renderGraphKitMiniMapNode(node)).join('');
    return `<div class="graphkit-minimap" data-graphkit-minimap title="MiniMap">
        <div class="graphkit-minimap__head">
            <span>MiniMap</span>
            <div class="graphkit-minimap__actions">
                <strong>${escapeHtml(graphKitProject.graph.nodes.length)} nodes</strong>
                <button class="graphkit-minimap__fit" type="button" data-graphkit-action="fit-view" title="适配全部节点" aria-label="适配全部节点">Fit</button>
            </div>
        </div>
        <svg class="graphkit-minimap__svg" width="${GRAPHKIT_MINIMAP_WIDTH}" height="${GRAPHKIT_MINIMAP_HEIGHT}" viewBox="0 0 ${GRAPHKIT_CANVAS_WIDTH} ${GRAPHKIT_CANVAS_HEIGHT}" aria-hidden="true">
            <rect class="graphkit-minimap__canvas" x="0" y="0" width="${GRAPHKIT_CANVAS_WIDTH}" height="${GRAPHKIT_CANVAS_HEIGHT}"></rect>
            ${nodes}
            ${renderGraphKitMiniMapViewport()}
        </svg>
    </div>`;
}

function renderGraphKitMiniMapNode(node) {
    const type = getGraphKitNodeType(node.type);
    const position = getGraphKitNodePosition(node);
    return `<rect class="graphkit-minimap__node" data-graphkit-minimap-node="${escapeHtml(node.id)}" x="${escapeHtml(position.x)}" y="${escapeHtml(position.y)}" width="${GRAPHKIT_NODE_WIDTH}" height="${escapeHtml(getGraphKitNodeHeight(type, node))}" rx="10" style="--node-color:${escapeHtml(type?.color || '#6c7480')}"></rect>`;
}

function renderGraphKitMiniMapViewport() {
    const rect = getGraphKitMiniMapViewportRect();
    return `<rect class="graphkit-minimap__viewport" data-graphkit-minimap-viewport x="${escapeHtml(rect.x)}" y="${escapeHtml(rect.y)}" width="${escapeHtml(rect.width)}" height="${escapeHtml(rect.height)}" rx="18"></rect>`;
}

function renderGraphKitEdge(edge) {
    const path = getGraphKitEdgePath(edge);
    if (!path) return '';
    const active = graphKitState.selectedEdgeId === edge.id;
    const edgeClass = graphKitState.selectedEdgeId === edge.id
        ? 'graphkit-edge active tablekit-graph-edge'
        : 'graphkit-edge tablekit-graph-edge';
    const groupClass = active ? 'graphkit-edge-group active' : 'graphkit-edge-group';
    return `<g class="${groupClass}" data-graphkit-edge-group="${escapeHtml(edge.id)}">
        <path class="${edgeClass}" data-graphkit-edge="${escapeHtml(edge.id)}" d="${escapeHtml(path)}"></path>
    </g>`;
}

function renderGraphKitEdgeLabels(edges) {
    return (Array.isArray(edges) ? edges : []).map(edge => renderGraphKitEdgeLabel(edge)).join('');
}

function renderGraphKitEdgeLabel(edge) {
    const label = formatGraphKitEdgeLabel(edge);
    if (!label) return '';
    const position = getGraphKitEdgeLabelPosition(edge);
    if (!position) return '';
    const active = graphKitState.selectedEdgeId === edge.id ? ' active' : '';
    const width = getGraphKitEdgeLabelWidth(label);
    const height = label.condition ? 42 : 26;
    const title = [edge.from, edge.to, edge.label, edge.condition, edge.priority ? `priority ${edge.priority}` : ''].filter(Boolean).join(' · ');
    return `<g class="graphkit-edge-label${active}" data-graphkit-edge-label="${escapeHtml(edge.id)}" transform="translate(${escapeHtml(position.x)} ${escapeHtml(position.y)})">
        <title>${escapeHtml(title)}</title>
        <rect class="graphkit-edge-label__box" x="${escapeHtml(-Math.round(width * 0.5))}" y="${escapeHtml(-Math.round(height * 0.5))}" width="${escapeHtml(width)}" height="${escapeHtml(height)}" rx="4"></rect>
        <text class="graphkit-edge-label__title" x="0" y="${label.condition ? '-4' : '4'}">${escapeHtml(label.title)}</text>
        ${label.condition ? `<text class="graphkit-edge-label__condition" x="0" y="13">${escapeHtml(label.condition)}</text>` : ''}
    </g>`;
}

function getGraphKitEdgeLabelPosition(edge) {
    const from = getGraphKitEndpointPosition(edge.from, 'output');
    const to = getGraphKitEndpointPosition(edge.to, 'input');
    if (!from || !to) return null;
    const mid = Math.max(120, Math.abs(to.x - from.x) * 0.44);
    const controlA = { x: from.x + mid, y: from.y };
    const controlB = { x: to.x - mid, y: to.y };
    return {
        x: Math.round((from.x + 3 * controlA.x + 3 * controlB.x + to.x) / 8),
        y: Math.round((from.y + 3 * controlA.y + 3 * controlB.y + to.y) / 8) - 18,
    };
}

function formatGraphKitEdgeLabel(edge) {
    const rawLabel = String(edge?.label || '').trim();
    const rawCondition = String(edge?.condition || '').trim();
    const priority = Number(edge?.priority || 0);
    const hasPriority = Number.isFinite(priority) && priority !== 0;
    if (!rawLabel && !rawCondition && !hasPriority) return null;
    const title = rawLabel || rawCondition || `priority ${priority}`;
    const conditionParts = [];
    if (rawCondition && rawCondition !== title) conditionParts.push(rawCondition);
    if (hasPriority) conditionParts.push(`#${priority}`);
    return {
        title: formatGraphKitNodeLabel(title, 28),
        condition: formatGraphKitNodeLabel(conditionParts.join(' · '), 34),
    };
}

function getGraphKitEdgeLabelWidth(label) {
    const titleLength = String(label?.title || '').length;
    const conditionLength = String(label?.condition || '').length;
    return Math.max(96, Math.min(236, 26 + Math.max(titleLength * 7, conditionLength * 6)));
}

function renderGraphKitEdgeReconnectHandles(edge) {
    const from = getGraphKitEndpointPosition(edge.from, 'output');
    const to = getGraphKitEndpointPosition(edge.to, 'input');
    if (!from || !to) return '';
    const active = graphKitState.selectedEdgeId === edge.id ? ' active' : '';
    return `<circle class="graphkit-edge-handle graphkit-edge-handle--from${active}" data-graphkit-edge-handle="${escapeHtml(edge.id)}:from" data-graphkit-edge-id="${escapeHtml(edge.id)}" data-graphkit-edge-side="from" cx="${escapeHtml(from.x)}" cy="${escapeHtml(from.y)}" r="8"><title>${escapeHtml(edge.from)}</title></circle>
        <circle class="graphkit-edge-handle graphkit-edge-handle--to${active}" data-graphkit-edge-handle="${escapeHtml(edge.id)}:to" data-graphkit-edge-id="${escapeHtml(edge.id)}" data-graphkit-edge-side="to" cx="${escapeHtml(to.x)}" cy="${escapeHtml(to.y)}" r="8"><title>${escapeHtml(edge.to)}</title></circle>`;
}

function getGraphKitEdgePath(edge) {
    const from = getGraphKitEndpointPosition(edge.from, 'output');
    const to = getGraphKitEndpointPosition(edge.to, 'input');
    if (!from || !to) return null;
    const mid = Math.max(120, Math.abs(to.x - from.x) * 0.44);
    return `M ${from.x} ${from.y} C ${from.x + mid} ${from.y}, ${to.x - mid} ${to.y}, ${to.x} ${to.y}`;
}

function renderGraphKitNode(node, selectedNode, renderMode) {
    const type = getGraphKitNodeType(node.type);
    const position = getGraphKitNodePosition(node);
    const collapsed = Boolean(node.collapsed);
    const height = getGraphKitNodeHeight(type, node);
    const active = selectedNode?.id === node.id ? ' active' : '';
    const multiSelected = isGraphKitNodeSelected(node.id) ? ' is-multi-selected' : '';
    const collapsedClass = collapsed ? ' is-collapsed' : '';
    const compact = shouldRenderGraphKitNodeCompact(node, selectedNode, renderMode);
    const lodClass = compact ? ' is-lod-compact' : '';
    const title = type?.title || node.type;
    const handler = type?.handlerId || 'missing handler';
    const fill = type?.color || '#4f5f73';
    const fields = collapsed || compact ? [] : getGraphKitNodeFieldRows(node, type);
    const collapseLabel = collapsed ? '展开节点' : '折叠节点';
    const collapseIcon = collapsed ? '+' : '-';
    const collapseControl = compact ? '' : `<g class="graphkit-node-collapse" data-graphkit-node-collapse="${escapeHtml(node.id)}" role="button" aria-label="${escapeHtml(collapseLabel)}" tabindex="0">
            <rect class="graphkit-node-collapse__hit" x="218" y="11" width="20" height="20" rx="3"></rect>
            <text class="graphkit-node-collapse__icon" x="228" y="26">${escapeHtml(collapseIcon)}</text>
            <title>${escapeHtml(collapseLabel)}</title>
        </g>`;
    const handlerLabel = compact ? '' : `<text class="graphkit-node__handler" x="14" y="${escapeHtml(height - 13)}">${escapeHtml(formatGraphKitNodeLabel(handler, 32))}</text>`;
    const portDots = compact ? '' : renderGraphKitPortDots(node, type, height);

    return `<g class="graphkit-node tablekit-graph-node${active}${multiSelected}${collapsedClass}${lodClass}" data-graphkit-node="${escapeHtml(node.id)}" transform="${escapeHtml(getGraphKitNodeTransform(node))}">
        <rect class="graphkit-node__body tablekit-graph-node__body" width="${GRAPHKIT_NODE_WIDTH}" height="${escapeHtml(height)}" rx="5" style="--node-color:${escapeHtml(fill)}"></rect>
        <rect class="graphkit-node__header" width="${GRAPHKIT_NODE_WIDTH}" height="${GRAPHKIT_NODE_HEADER_HEIGHT}" rx="5" style="--node-color:${escapeHtml(fill)}"></rect>
        <rect class="graphkit-node__header-mask" y="38" width="${GRAPHKIT_NODE_WIDTH}" height="12"></rect>
        <text class="graphkit-node__title tablekit-graph-node__title" x="34" y="21">${escapeHtml(formatGraphKitNodeLabel(title, 24))}</text>
        <text class="graphkit-node__type tablekit-graph-node__type" x="34" y="37">${escapeHtml(formatGraphKitNodeLabel(type?.category || node.type, 26))}</text>
        <text class="graphkit-node__icon" x="12" y="29">◆</text>
        ${collapseControl}
        ${renderGraphKitNodeFields(fields)}
        ${handlerLabel}
        ${portDots}
    </g>`;
}

function renderGraphKitNodeFields(fields) {
    if (!fields.length) return '';
    return fields.map((field, index) => {
        const y = GRAPHKIT_NODE_HEADER_HEIGHT + 18 + index * GRAPHKIT_NODE_FIELD_HEIGHT;
        return `<g class="graphkit-node-field" transform="translate(0, ${escapeHtml(y)})">
            <circle class="graphkit-node-field__dot" cx="18" cy="-3" r="3"></circle>
            <text class="graphkit-node-field__name" x="30" y="0">${escapeHtml(formatGraphKitNodeLabel(field.label, 18))}</text>
            <text class="graphkit-node-field__value" x="134" y="0">${escapeHtml(formatGraphKitNodeLabel(field.value, 16))}</text>
        </g>`;
    }).join('');
}

function renderGraphKitPortDots(node, type, nodeHeight) {
    const ports = Array.isArray(type?.ports) ? type.ports : [];
    const inputs = ports.filter(port => port.direction === 'input');
    const outputs = ports.filter(port => port.direction !== 'input');
    const inputDots = inputs.map((port, index) => {
        const y = getGraphKitPortY(inputs.length, index, nodeHeight);
        const endpoint = `${node.id}.${port.id}`;
        const active = graphKitState.pendingPortEndpoint === endpoint ? ' active' : '';
        return `<g class="graphkit-port-target graphkit-port-target--input${active}" data-graphkit-port="${escapeHtml(endpoint)}" data-graphkit-port-direction="${escapeHtml(port.direction)}" data-graphkit-port-kind="${escapeHtml(port.kind)}" data-graphkit-port-multiple="${port.multiple ? 'true' : 'false'}">
            <circle class="graphkit-port-hit" cx="0" cy="${escapeHtml(y)}" r="13"></circle>
            <circle class="graphkit-port graphkit-port--input tablekit-graph-port tablekit-graph-port--input" cx="0" cy="${escapeHtml(y)}" r="5"><title>${escapeHtml(`${port.id} · ${port.multiple ? 'multi' : 'single'}`)}</title></circle>
        </g>`;
    }).join('');
    const outputDots = outputs.map((port, index) => {
        const y = getGraphKitPortY(outputs.length, index, nodeHeight);
        const endpoint = `${node.id}.${port.id}`;
        const active = graphKitState.pendingPortEndpoint === endpoint ? ' active' : '';
        return `<g class="graphkit-port-target graphkit-port-target--output${active}" data-graphkit-port="${escapeHtml(endpoint)}" data-graphkit-port-direction="${escapeHtml(port.direction)}" data-graphkit-port-kind="${escapeHtml(port.kind)}" data-graphkit-port-multiple="${port.multiple ? 'true' : 'false'}">
            <circle class="graphkit-port-hit" cx="${GRAPHKIT_NODE_WIDTH}" cy="${escapeHtml(y)}" r="13"></circle>
            <circle class="graphkit-port graphkit-port--output tablekit-graph-port tablekit-graph-port--output" cx="${GRAPHKIT_NODE_WIDTH}" cy="${escapeHtml(y)}" r="5"><title>${escapeHtml(`${port.id} · ${port.multiple ? 'multi' : 'single'}`)}</title></circle>
        </g>`;
    }).join('');
    return `${inputDots}${outputDots}`;
}

function renderGraphKitInspector(node, type, report, selectedEdge, selectedBlackboard, selectedNote, selectedPlacemat, selectedNodeTypeDefinition) {
    if (selectedNote || selectedPlacemat) return renderGraphKitOrganizationInspector(selectedNote, selectedPlacemat);
    if (selectedNodeTypeDefinition) return renderGraphKitNodeTypeInspector(selectedNodeTypeDefinition);

    if (selectedEdge) {
        return `<div class="graphkit-inspector__shell">
        <div class="graphkit-panel-head graphkit-inspector__head">
            <div>
                <span>Graph Inspector</span>
                <strong>${escapeHtml(selectedEdge.id)}</strong>
                <em>${escapeHtml(selectedEdge.from)} -> ${escapeHtml(selectedEdge.to)}</em>
            </div>
            <div class="graphkit-panel-head__actions">
                <span class="kit-state-pill kit-state-pill--ok">edge</span>
                <button class="graphkit-panel-icon" type="button" data-graphkit-panel-toggle="inspector" title="收起 Inspector" aria-label="收起 Inspector">›</button>
            </div>
        </div>
        <div class="graphkit-inspector__body">
            <section class="graphkit-property-group">
                <button class="graphkit-fold-title" type="button">Edge</button>
                <div class="graphkit-port-list">
                    <span>from · <strong>${escapeHtml(selectedEdge.from)}</strong></span>
                    <span>to · <strong>${escapeHtml(selectedEdge.to)}</strong></span>
                </div>
                <label class="graphkit-property-field">
                    <span>Label<em>branch display</em></span>
                    <input class="cmd-input" type="text" data-graphkit-edge-field="label" value="${escapeHtml(selectedEdge.label)}">
                </label>
                <label class="graphkit-property-field">
                    <span>Condition<em>runtime expression id</em></span>
                    <input class="cmd-input" type="text" data-graphkit-edge-field="condition" value="${escapeHtml(selectedEdge.condition)}">
                </label>
                <label class="graphkit-property-field">
                    <span>Priority<em>branch order</em></span>
                    <input class="cmd-input" type="number" step="1" data-graphkit-edge-field="priority" value="${escapeHtml(selectedEdge.priority)}">
                </label>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="delete-edge">删除连线</button>
            </section>
        </div>
    </div>`;
    }

    if (selectedBlackboard) {
        return `<div class="graphkit-inspector__shell">
        <div class="graphkit-panel-head graphkit-inspector__head">
            <div>
                <span>Graph Inspector</span>
                <strong>${escapeHtml(selectedBlackboard.name)}</strong>
                <em>blackboard · ${escapeHtml(selectedBlackboard.type)}</em>
            </div>
            <div class="graphkit-panel-head__actions">
                <span class="kit-state-pill kit-state-pill--ok">variable</span>
                <button class="graphkit-panel-icon" type="button" data-graphkit-panel-toggle="inspector" title="收起 Inspector" aria-label="收起 Inspector">›</button>
            </div>
        </div>
        <div class="graphkit-inspector__body">
            <section class="graphkit-property-group">
                <button class="graphkit-fold-title" type="button">Blackboard Variable</button>
                <label class="graphkit-property-field">
                    <span>Name<em>id</em></span>
                    <input class="cmd-input" type="text" data-graphkit-blackboard-field="name" value="${escapeHtml(selectedBlackboard.name)}">
                </label>
                <label class="graphkit-property-field">
                    <span>Type<em>value type</em></span>
                    <input class="cmd-input" type="text" data-graphkit-blackboard-field="type" value="${escapeHtml(selectedBlackboard.type)}">
                </label>
                <label class="graphkit-property-field">
                    <span>Section<em>blackboard group</em></span>
                    <input class="cmd-input" type="text" data-graphkit-blackboard-field="section" value="${escapeHtml(selectedBlackboard.section)}">
                </label>
                <label class="graphkit-property-field">
                    <span>Default<em>serialized value</em></span>
                    <input class="cmd-input" type="text" data-graphkit-blackboard-field="defaultValue" value="${escapeHtml(formatGraphKitFieldValue(selectedBlackboard.defaultValue))}">
                </label>
                <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="delete-blackboard-var">删除变量</button>
            </section>
        </div>
    </div>`;
    }

    if (!node) {
        return renderGraphKitGraphInspector(report);
    }

    const fields = Array.isArray(type?.fields) ? type.fields : [];
    const ports = Array.isArray(type?.ports) ? type.ports : [];
    const fieldControls = fields.length ? fields.map(field => {
        const value = node.fields?.[field.name] ?? field.defaultValue ?? '';
        const fieldType = String(field.type || 'string').toLowerCase();
        const checkboxClass = fieldType === 'bool' || fieldType === 'boolean' ? ' graphkit-property-field--checkbox' : '';
        return `<label class="graphkit-property-field${checkboxClass}">
            <span>${escapeHtml(field.title || field.name)}<em>${escapeHtml(field.type + (field.ref ? ` · ref ${field.ref}` : ''))}</em></span>
            ${renderGraphKitNodeFieldControl(field, value)}
        </label>`;
    }).join('') : `<div class="graphkit-empty-line">该节点没有可编辑字段。</div>`;
    const relatedMessages = [...report.errors, ...report.warnings].filter(message => message.includes(node.id) || message.includes(node.type));
    const subgraphControls = renderGraphKitSubgraphReferenceInspector(node);
    const portalControls = renderGraphKitPortalReferenceInspector(node);

    return `<div class="graphkit-inspector__shell">
        <div class="graphkit-panel-head graphkit-inspector__head">
            <div>
                <span>Graph Inspector</span>
                <strong>${escapeHtml(type?.title || node.id)}</strong>
                <em>${escapeHtml(node.id)} · ${escapeHtml(node.type)}</em>
            </div>
            <div class="graphkit-panel-head__actions">
                <span class="kit-state-pill ${type?.handlerId ? 'kit-state-pill--ok' : 'kit-state-pill--danger'}">${escapeHtml(type?.handlerId || 'missing handler')}</span>
                <button class="graphkit-panel-icon" type="button" data-graphkit-panel-toggle="inspector" title="收起 Inspector" aria-label="收起 Inspector">›</button>
            </div>
        </div>
        <div class="graphkit-inspector__body">
            <section class="graphkit-property-group">
                <button class="graphkit-fold-title" type="button">Node Properties</button>
                ${fieldControls}
            </section>
            ${subgraphControls}
            ${portalControls}
            <section class="graphkit-property-group">
                <button class="graphkit-fold-title" type="button">Ports</button>
                <div class="graphkit-port-list">
                    ${ports.map(port => `<span>${escapeHtml(port.direction)} · ${escapeHtml(port.kind)} · ${port.multiple ? 'multi' : 'single'} · <strong>${escapeHtml(port.id)}</strong></span>`).join('')}
                </div>
            </section>
            ${relatedMessages.length ? `<div class="tablekit-callout tablekit-callout--warning">${escapeHtml(relatedMessages.join(' / '))}</div>` : ''}
        </div>
    </div>`;
}

function renderGraphKitNodeFieldControl(field, value) {
    const fieldName = String(field?.name || '');
    const fieldType = String(field?.type || 'string').toLowerCase();
    const fieldAttribute = `data-graphkit-field="${escapeHtml(fieldName)}" data-graphkit-field-type="${escapeHtml(fieldType)}"`;
    if (Array.isArray(field?.options) && field.options.length) return renderGraphKitNodeFieldOptionsSelect(field, value, fieldType);
    if (fieldType === 'bool' || fieldType === 'boolean') {
        const checked = normalizeGraphKitNodeFieldValue(field, value) ? ' checked' : '';
        return `<input type="checkbox" data-graphkit-field="${escapeHtml(fieldName)}" data-graphkit-field-type="${escapeHtml(fieldType)}"${checked}>`;
    }
    if (fieldType === 'int' || fieldType === 'integer' || fieldType === 'long') {
        return `<input class="cmd-input" type="number" step="1" ${fieldAttribute} value="${escapeHtml(formatGraphKitFieldValue(value))}">`;
    }
    if (fieldType === 'float' || fieldType === 'double' || fieldType === 'number') {
        return `<input class="cmd-input" type="number" step="any" ${fieldAttribute} value="${escapeHtml(formatGraphKitFieldValue(value))}">`;
    }
    if (fieldType === 'graphref') return renderGraphKitGraphFieldSelect(fieldName, value, fieldType);
    if (fieldType === 'blackboardref') return renderGraphKitBlackboardFieldSelect(fieldName, value, fieldType);
    const multiline = fieldType === 'text' || fieldType === 'multiline' || /text|dialogue|prompt|description/i.test(fieldName);
    if (multiline) {
        return `<textarea class="cmd-input graphkit-property-textarea" ${fieldAttribute}>${escapeHtml(formatGraphKitFieldValue(value))}</textarea>`;
    }
    return `<input class="cmd-input" type="text" ${fieldAttribute} value="${escapeHtml(formatGraphKitFieldValue(value))}">`;
}

function renderGraphKitNodeFieldOptionsSelect(field, value, fieldType) {
    const fieldName = String(field?.name || '');
    const selected = formatGraphKitFieldValue(value).trim();
    const options = Array.isArray(field?.options) ? field.options : [];
    const hasSelectedOption = options.includes(selected);
    const missingOption = selected && !hasSelectedOption
        ? `<option value="${escapeHtml(selected)}" selected>${escapeHtml(selected)} (invalid)</option>`
        : '';
    const optionRows = options.map(option => {
        const selectedAttr = option === selected ? ' selected' : '';
        return `<option value="${escapeHtml(option)}"${selectedAttr}>${escapeHtml(option)}</option>`;
    }).join('');
    return `<select class="cmd-input" data-graphkit-field="${escapeHtml(fieldName)}" data-graphkit-field-type="${escapeHtml(fieldType)}">
        <option value="">选择选项</option>
        ${missingOption}
        ${optionRows}
    </select>`;
}

function renderGraphKitGraphFieldSelect(fieldName, value, fieldType) {
    const selected = String(value ?? '').trim();
    const hasSelectedGraph = graphKitProject.graphs.some(graph => graph.id === selected);
    const missingOption = selected && !hasSelectedGraph
        ? `<option value="${escapeHtml(selected)}" selected>${escapeHtml(selected)} (missing)</option>`
        : '';
    const options = graphKitProject.graphs.map(graph => {
        const selectedAttr = graph.id === selected ? ' selected' : '';
        return `<option value="${escapeHtml(graph.id)}"${selectedAttr}>${escapeHtml(graph.title)} · ${escapeHtml(graph.id)}</option>`;
    }).join('');
    return `<select class="cmd-input" data-graphkit-field="${escapeHtml(fieldName)}" data-graphkit-field-type="${escapeHtml(fieldType)}">
        <option value="">选择目标图</option>
        ${missingOption}
        ${options}
    </select>`;
}

function renderGraphKitBlackboardFieldSelect(fieldName, value, fieldType) {
    const selected = String(value ?? '').trim();
    const variables = Array.isArray(graphKitProject.graph.blackboard) ? graphKitProject.graph.blackboard : [];
    const hasSelectedVariable = variables.some(item => item.name === selected);
    const missingOption = selected && !hasSelectedVariable
        ? `<option value="${escapeHtml(selected)}" selected>${escapeHtml(selected)} (missing)</option>`
        : '';
    const options = variables.map(item => {
        const selectedAttr = item.name === selected ? ' selected' : '';
        return `<option value="${escapeHtml(item.name)}"${selectedAttr}>${escapeHtml(item.name)} · ${escapeHtml(item.type)}</option>`;
    }).join('');
    return `<select class="cmd-input" data-graphkit-field="${escapeHtml(fieldName)}" data-graphkit-field-type="${escapeHtml(fieldType)}">
        <option value="">选择 Blackboard 变量</option>
        ${missingOption}
        ${options}
    </select>`;
}

function renderGraphKitSubgraphReferenceInspector(node) {
    if (!node || node.type !== 'graph.subgraph') return '';
    const targetGraphId = String(node.fields?.targetGraph || '').trim();
    const targetGraph = graphKitProject.graphs.find(graph => graph.id === targetGraphId);
    const disabled = targetGraph ? '' : ' disabled';
    const mappingControls = renderGraphKitSubgraphMappingInspector(node);
    const hint = targetGraph
        ? `<div class="graphkit-empty-line">打开后只切换编辑器视图，不改变 Luban XML。</div>`
        : `<div class="tablekit-callout tablekit-callout--warning">目标图 ${escapeHtml(targetGraphId || '(empty)')} 不存在。</div>`;
    return `<section class="graphkit-property-group">
        <button class="graphkit-fold-title" type="button">Subgraph Navigation</button>
        <div class="graphkit-port-list">
            <span>target · <strong>${escapeHtml(targetGraphId || '(empty)')}</strong></span>
            <span>entry · <strong>${escapeHtml(node.fields?.entry || 'start')}</strong></span>
            <span>exit · <strong>${escapeHtml(node.fields?.exit || 'end')}</strong></span>
        </div>
        ${mappingControls}
        <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="open-subgraph"${disabled}>打开子图</button>
        ${hint}
    </section>`;
}

function renderGraphKitSubgraphMappingInspector(node) {
    if (!node || node.type !== 'graph.subgraph') return '';
    const targetGraphId = String(node.fields?.targetGraph || '').trim();
    const portalOptions = typeof getGraphKitPortalOptionsForGraph === 'function'
        ? getGraphKitPortalOptionsForGraph(graphKitProject, targetGraphId)
        : [];
    return `<div class="graphkit-mapping-grid">
        <label class="graphkit-property-field">
            <span>Entry Portal<em>target graph portalId</em></span>
            ${renderGraphKitPortalMappingSelect('entry', node.fields?.entry || '', portalOptions, '选择入口 Portal')}
        </label>
        <label class="graphkit-property-field">
            <span>Exit Portal<em>target graph portalId</em></span>
            ${renderGraphKitPortalMappingSelect('exit', node.fields?.exit || '', portalOptions, '选择出口 Portal')}
        </label>
    </div>`;
}

function renderGraphKitPortalReferenceInspector(node) {
    if (!node || node.type !== 'graph.portal') return '';
    const portalId = String(node.fields?.portalId || '').trim();
    const targetGraphId = String(node.fields?.targetGraph || '').trim();
    const targetPortalId = String(node.fields?.targetPortal || '').trim();
    const targetGraph = graphKitProject.graphs.find(graph => graph.id === targetGraphId);
    const targetPortal = targetGraph && targetPortalId
        ? (targetGraph.nodes || []).find(candidate => candidate.type === 'graph.portal' && String(candidate.fields?.portalId || '').trim() === targetPortalId)
        : null;
    const portalOptions = typeof getGraphKitPortalOptionsForGraph === 'function'
        ? getGraphKitPortalOptionsForGraph(graphKitProject, targetGraphId)
        : [];
    const disabled = targetGraph && targetPortal ? '' : ' disabled';
    const repairDisabled = targetGraph && targetPortalId && !targetPortal ? '' : ' disabled';
    const hint = renderGraphKitPortalReferenceHint(targetGraphId, targetPortalId, targetGraph, targetPortal);
    return `<section class="graphkit-property-group">
        <button class="graphkit-fold-title" type="button">Portal Navigation</button>
        <div class="graphkit-port-list">
            <span>portal · <strong>${escapeHtml(portalId || '(empty)')}</strong></span>
            <span>target graph · <strong>${escapeHtml(targetGraphId || '(empty)')}</strong></span>
            <span>target portal · <strong>${escapeHtml(targetPortalId || '(empty)')}</strong></span>
        </div>
        <div class="graphkit-mapping-grid">
            <label class="graphkit-property-field">
                <span>Target Graph<em>graph reference</em></span>
                ${renderGraphKitGraphMappingSelect('targetGraph', targetGraphId)}
            </label>
            <label class="graphkit-property-field">
                <span>Target Portal<em>portalId in target graph</em></span>
                ${renderGraphKitPortalMappingSelect('targetPortal', targetPortalId, portalOptions, '选择目标 Portal')}
            </label>
        </div>
        <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="open-portal"${disabled}>打开 Portal</button>
        <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="repair-portal-pair"${repairDisabled}>修复 Portal Pair</button>
        ${hint}
    </section>`;
}

function renderGraphKitGraphMappingSelect(fieldName, selectedGraphId) {
    const selected = String(selectedGraphId || '').trim();
    const fieldAttribute = getGraphKitFieldAttribute(fieldName);
    const hasSelectedGraph = graphKitProject.graphs.some(graph => graph.id === selected);
    const missingOption = selected && !hasSelectedGraph
        ? `<option value="${escapeHtml(selected)}" selected>${escapeHtml(selected)} (missing)</option>`
        : '';
    const graphOptions = graphKitProject.graphs.map(graph => {
        const selectedAttr = graph.id === selected ? ' selected' : '';
        return `<option value="${escapeHtml(graph.id)}"${selectedAttr}>${escapeHtml(graph.title)} · ${escapeHtml(graph.id)}</option>`;
    }).join('');
    return `<select class="cmd-input" ${fieldAttribute}>
        <option value="">选择目标图</option>
        ${missingOption}
        ${graphOptions}
    </select>`;
}

function renderGraphKitPortalMappingSelect(fieldName, selectedPortalId, portalOptions, emptyLabel) {
    const selected = String(selectedPortalId || '').trim();
    const options = Array.isArray(portalOptions) ? portalOptions : [];
    const fieldAttribute = getGraphKitFieldAttribute(fieldName);
    const hasSelectedPortal = options.some(option => option.id === selected);
    const missingOption = selected && !hasSelectedPortal
        ? `<option value="${escapeHtml(selected)}" selected>${escapeHtml(selected)} (missing)</option>`
        : '';
    const portalRows = options.map(option => {
        const selectedAttr = option.id === selected ? ' selected' : '';
        return `<option value="${escapeHtml(option.id)}"${selectedAttr}>${escapeHtml(option.id)} · ${escapeHtml(option.nodeId)}</option>`;
    }).join('');
    const disabled = options.length || selected ? '' : ' disabled';
    return `<select class="cmd-input" ${fieldAttribute}${disabled}>
        <option value="">${escapeHtml(emptyLabel || '选择 Portal')}</option>
        ${missingOption}
        ${portalRows}
    </select>`;
}

function getGraphKitFieldAttribute(fieldName) {
    if (fieldName === 'entry') return 'data-graphkit-field="entry"';
    if (fieldName === 'exit') return 'data-graphkit-field="exit"';
    if (fieldName === 'targetGraph') return 'data-graphkit-field="targetGraph"';
    if (fieldName === 'targetPortal') return 'data-graphkit-field="targetPortal"';
    return `data-graphkit-field="${escapeHtml(fieldName)}"`;
}

function renderGraphKitPortalReferenceHint(targetGraphId, targetPortalId, targetGraph, targetPortal) {
    if (!targetGraph) {
        return `<div class="tablekit-callout tablekit-callout--warning">目标图 ${escapeHtml(targetGraphId || '(empty)')} 不存在。</div>`;
    }
    if (!targetPortalId) {
        return `<div class="tablekit-callout tablekit-callout--warning">未设置目标 Portal。</div>`;
    }
    if (!targetPortal) {
        return `<div class="tablekit-callout tablekit-callout--warning">目标图中不存在 Portal ${escapeHtml(targetPortalId)}。</div>`;
    }
    return `<div class="graphkit-empty-line">打开后切换到目标图并选中对应 Portal，Luban XML 不会被改写。</div>`;
}

function renderGraphKitGraphInspector(report) {
    const graph = graphKitProject.graph;
    const messages = [...report.errors, ...report.warnings];
    return `<div class="graphkit-inspector__shell">
        <div class="graphkit-panel-head graphkit-inspector__head">
            <div>
                <span>Graph Inspector</span>
                <strong>${escapeHtml(graph.title)}</strong>
                <em>${escapeHtml(graph.id)} · ${escapeHtml(graph.kind)}</em>
            </div>
            <div class="graphkit-panel-head__actions">
                <span class="kit-state-pill ${report.ok ? 'kit-state-pill--ok' : 'kit-state-pill--danger'}">${escapeHtml(report.ok ? 'graph' : 'invalid')}</span>
                <button class="graphkit-panel-icon" type="button" data-graphkit-panel-toggle="inspector" title="收起 Inspector" aria-label="收起 Inspector">›</button>
            </div>
        </div>
        <div class="graphkit-inspector__body">
            <section class="graphkit-property-group">
                <button class="graphkit-fold-title" type="button">Graph Properties</button>
                <label class="graphkit-property-field">
                    <span>Id<em>Luban graph id</em></span>
                    <input class="cmd-input" type="text" data-graphkit-graph-field="id" value="${escapeHtml(graph.id)}">
                </label>
                <label class="graphkit-property-field">
                    <span>Title<em>display name</em></span>
                    <input class="cmd-input" type="text" data-graphkit-graph-field="title" value="${escapeHtml(graph.title)}">
                </label>
                <label class="graphkit-property-field">
                    <span>Kind<em>graph type</em></span>
                    <input class="cmd-input" type="text" data-graphkit-graph-field="kind" value="${escapeHtml(graph.kind)}">
                </label>
            </section>
            <section class="graphkit-property-group">
                <button class="graphkit-fold-title" type="button">Graph Summary</button>
                <div class="graphkit-port-list">
                    <span>nodes · <strong>${escapeHtml(graph.nodes.length)}</strong></span>
                    <span>edges · <strong>${escapeHtml(graph.edges.length)}</strong></span>
                    <span>blackboard · <strong>${escapeHtml(graph.blackboard.length)}</strong></span>
                    <span>graphs · <strong>${escapeHtml(graphKitProject.graphs.length)}</strong></span>
                </div>
            </section>
            ${messages.length ? `<div class="tablekit-callout tablekit-callout--warning">${escapeHtml(messages.join(' / '))}</div>` : ''}
        </div>
    </div>`;
}

function renderGraphKitBottomToolbar(report) {
    const viewport = getGraphKitViewport();
    const issues = getGraphKitIssueCounts(report);
    const selectedCount = getGraphKitSelectedNodeIds().length;
    return `<div class="graphkit-bottom-toolbar">
        <button class="graphkit-tool-button" type="button" data-graphkit-action="reset-view" title="重置视图">⌂</button>
        <button class="graphkit-tool-button" type="button" data-graphkit-action="zoom-out" title="缩小">−</button>
        <span class="graphkit-zoom-label" data-graphkit-zoom-label>${escapeHtml(formatGraphKitZoom(viewport.scale))}</span>
        <button class="graphkit-tool-button" type="button" data-graphkit-action="zoom-in" title="放大">＋</button>
        <span class="graphkit-toolbar-separator"></span>
        <button class="graphkit-tool-button" type="button" data-graphkit-action="toggle-search" title="查找图元素">⌕</button>
        <span class="graphkit-toolbar-separator"></span>
        <span class="graphkit-selection-count" data-graphkit-selection-count>${escapeHtml(`${selectedCount} selected`)}</span>
        ${renderGraphKitLayoutTools(selectedCount)}
        <span class="graphkit-toolbar-separator"></span>
        <button class="graphkit-toolbar-status ${issues.total ? 'is-error' : 'is-ok'}" type="button" data-graphkit-action="toggle-issues" title="显示图校验问题">${escapeHtml(issues.total ? `${issues.errors} errors · ${issues.warnings} warnings` : '0 alerts')}</button>
    </div>`;
}

function renderGraphKitXmlDock(xml, contract, report) {
    return `<details class="graphkit-xml-dock tablekit-json-panel tablekit-graph-xml-panel">
        <summary>XML / Contract</summary>
        <div class="graphkit-xml-dock__meta">
            <span>${escapeHtml(contract.graphClass)}</span>
            <span>${escapeHtml(contract.nodeClass)}</span>
            <span>${escapeHtml(report.ok ? 'valid' : 'invalid')}</span>
        </div>
        <div class="graphkit-xml-dock__actions">
            <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="import-xml">导入 XML</button>
            <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="download-xml">下载 XML</button>
            <button class="btn btn-secondary btn-sm" type="button" data-graphkit-action="copy-xml">复制 XML</button>
        </div>
        ${renderGraphKitNotice()}
        <textarea class="cmd-input graphkit-xml-input" data-graphkit-xml-input spellcheck="false">${escapeHtml(xml)}</textarea>
    </details>`;
}

function renderGraphKitNotice() {
    if (!graphKitState.noticeText) return '';
    const level = graphKitState.noticeLevel === 'success'
        ? 'success'
        : graphKitState.noticeLevel === 'warning' || graphKitState.noticeLevel === 'error'
            ? 'warning'
            : 'info';
    return `<div class="tablekit-callout tablekit-callout--${escapeHtml(level)}">${escapeHtml(graphKitState.noticeText)}</div>`;
}
