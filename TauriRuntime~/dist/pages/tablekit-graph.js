// pages/tablekit-graph.js
// TableKit XML Graph prototype: Graph Toolkit style editing surface backed by Luban-friendly data.
const TABLEKIT_GRAPH_NODE_WIDTH = 150;
const TABLEKIT_GRAPH_NODE_HEIGHT = 86;

function renderTableKitGraphPanel(status, config) {
    tableKitGraphProject = sanitizeTableKitGraphProject(tableKitGraphProject);
    const report = validateTableKitGraphProject(tableKitGraphProject);
    const selectedNode = getTableKitGraphSelectedNode();
    const selectedNodeType = getTableKitGraphNodeType(selectedNode?.type);
    const xml = serializeTableKitGraphXml(tableKitGraphProject);
    const contract = getTableKitGraphRuntimeContract(tableKitGraphProject);

    return `<section class="kit-panel tablekit-section tablekit-section--wide tablekit-graph-section">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('fsm', 'XML Graph 原型')}</div>
                <div class="kit-panel__desc">以 Graph Toolkit 的节点库、画布和 Inspector 体验为参考；实际资产保存为 XML，再交给 Luban 生成强类型数据。</div>
            </div>
            <div class="tablekit-panel-actions">
                <button class="btn btn-secondary btn-sm" type="button" data-tablekit-graph-action="copy-xml">复制 XML</button>
                <button class="btn btn-secondary btn-sm" type="button" data-tablekit-graph-action="reset">重置示例</button>
            </div>
        </div>
        <div class="tablekit-graph-summary">
            <span class="kit-state-pill ${report.ok ? 'kit-state-pill--ok' : 'kit-state-pill--danger'}">${escapeHtml(report.ok ? 'Graph Valid' : 'Graph Invalid')}</span>
            <span class="kit-state-pill">${escapeHtml(`${tableKitGraphProject.nodeTypes.length} Node Types`)}</span>
            <span class="kit-state-pill">${escapeHtml(`${tableKitGraphProject.graph.nodes.length} Nodes`)}</span>
            <span class="kit-state-pill">${escapeHtml(`${tableKitGraphProject.graph.edges.length} Edges`)}</span>
        </div>
        <div class="tablekit-graph-layout">
            <aside class="tablekit-graph-library">
                <div class="tablekit-graph-library__head">
                    <strong>节点类型库</strong>
                    <input class="cmd-input tablekit-graph-search" type="search" data-tablekit-graph-search value="${escapeHtml(tableKitGraphState.searchTerm)}" placeholder="搜索 type / handler">
                </div>
                <div class="tablekit-graph-library__list">
                    ${renderTableKitGraphNodeTypeList(selectedNodeType)}
                </div>
            </aside>
            <div class="tablekit-graph-canvas-shell">
                ${renderTableKitGraphCanvas(selectedNode)}
            </div>
            <aside class="tablekit-graph-inspector">
                ${renderTableKitGraphInspector(selectedNode, selectedNodeType, report)}
            </aside>
        </div>
        <div class="tablekit-graph-output-grid">
            ${renderTableKitGraphContract(contract)}
            ${renderTableKitGraphValidation(report)}
            <details class="tablekit-json-panel tablekit-graph-xml-panel" open>
                <summary>XML 数据预览</summary>
                <pre class="tablekit-json-block"><code>${escapeHtml(xml)}</code></pre>
            </details>
        </div>
    </section>`;
}

function renderTableKitGraphNodeTypeList(selectedNodeType) {
    const query = String(tableKitGraphState.searchTerm || '').trim().toLowerCase();
    const types = tableKitGraphProject.nodeTypes.filter(type => {
        if (!query) return true;
        return `${type.id} ${type.title} ${type.category} ${type.handlerId}`.toLowerCase().includes(query);
    });
    if (!types.length) return `<div class="tablekit-empty-line">没有匹配的节点类型。</div>`;

    return types.map(type => {
        const active = selectedNodeType?.id === type.id ? ' active' : '';
        return `<button class="tablekit-graph-type${active}" type="button" data-tablekit-graph-type="${escapeHtml(type.id)}">
            <span class="tablekit-graph-type__color" style="--node-color:${escapeHtml(type.color)}"></span>
            <span>
                <strong>${escapeHtml(type.title)}</strong>
                <em>${escapeHtml(type.id)}</em>
            </span>
            <code>${escapeHtml(type.handlerId || 'missing handler')}</code>
        </button>`;
    }).join('');
}

function renderTableKitGraphCanvas(selectedNode) {
    const graph = tableKitGraphProject.graph;
    const edges = graph.edges.map(edge => renderTableKitGraphEdge(edge)).join('');
    const nodes = graph.nodes.map(node => renderTableKitGraphNode(node, selectedNode)).join('');
    return `<svg class="tablekit-graph-canvas" viewBox="0 0 740 420" role="img" aria-label="${escapeHtml(graph.title)}">
        <defs>
            <marker id="tablekit-graph-arrow" markerWidth="10" markerHeight="10" refX="8" refY="3" orient="auto">
                <path d="M0,0 L0,6 L9,3 z"></path>
            </marker>
        </defs>
        <rect class="tablekit-graph-canvas__bg" x="0" y="0" width="740" height="420"></rect>
        ${edges}
        ${nodes}
    </svg>`;
}

function renderTableKitGraphEdge(edge) {
    const from = getTableKitGraphEndpointPosition(edge.from, 'output');
    const to = getTableKitGraphEndpointPosition(edge.to, 'input');
    if (!from || !to) return '';
    const mid = Math.max(60, Math.abs(to.x - from.x) * 0.45);
    const path = `M ${from.x} ${from.y} C ${from.x + mid} ${from.y}, ${to.x - mid} ${to.y}, ${to.x} ${to.y}`;
    return `<path class="tablekit-graph-edge" d="${escapeHtml(path)}"></path>`;
}

function renderTableKitGraphNode(node, selectedNode) {
    const type = getTableKitGraphNodeType(node.type);
    const active = selectedNode?.id === node.id ? ' active' : '';
    const title = type?.title || node.type;
    const handler = type?.handlerId || 'missing handler';
    const fill = type?.color || '#4f5f73';
    const fieldCount = Object.keys(node.fields || {}).length;
    return `<g class="tablekit-graph-node${active}" data-tablekit-graph-node="${escapeHtml(node.id)}" transform="translate(${escapeHtml(node.x)}, ${escapeHtml(node.y)})">
        <rect class="tablekit-graph-node__body" width="${TABLEKIT_GRAPH_NODE_WIDTH}" height="${TABLEKIT_GRAPH_NODE_HEIGHT}" rx="8" style="--node-color:${escapeHtml(fill)}"></rect>
        <rect class="tablekit-graph-node__bar" width="${TABLEKIT_GRAPH_NODE_WIDTH}" height="8" rx="4" style="--node-color:${escapeHtml(fill)}"></rect>
        <text class="tablekit-graph-node__title" x="14" y="32">${escapeHtml(formatTableKitGraphNodeLabel(title, 16))}</text>
        <text class="tablekit-graph-node__type" x="14" y="54">${escapeHtml(formatTableKitGraphNodeLabel(node.type, 19))}</text>
        <text class="tablekit-graph-node__meta" x="14" y="73">${escapeHtml(formatTableKitGraphNodeLabel(`${handler} · ${fieldCount} fields`, 20))}</text>
        ${renderTableKitGraphPortDots(type)}
    </g>`;
}

function renderTableKitGraphPortDots(type) {
    const ports = Array.isArray(type?.ports) ? type.ports : [];
    const inputs = ports.filter(port => port.direction === 'input');
    const outputs = ports.filter(port => port.direction !== 'input');
    const inputDots = inputs.map((port, index) => {
        const y = getTableKitGraphPortY(inputs.length, index);
        return `<circle class="tablekit-graph-port tablekit-graph-port--input" cx="0" cy="${escapeHtml(y)}" r="5"><title>${escapeHtml(port.id)}</title></circle>`;
    }).join('');
    const outputDots = outputs.map((port, index) => {
        const y = getTableKitGraphPortY(outputs.length, index);
        return `<circle class="tablekit-graph-port tablekit-graph-port--output" cx="${TABLEKIT_GRAPH_NODE_WIDTH}" cy="${escapeHtml(y)}" r="5"><title>${escapeHtml(port.id)}</title></circle>`;
    }).join('');
    return `${inputDots}${outputDots}`;
}

function renderTableKitGraphInspector(node, type, report) {
    if (!node) {
        return `<div class="tablekit-preview-empty">${emptyState('fsm', '选择画布节点后查看字段、端口和 handler 绑定。')}</div>`;
    }

    const fields = Array.isArray(type?.fields) ? type.fields : [];
    const ports = Array.isArray(type?.ports) ? type.ports : [];
    const fieldControls = fields.length ? fields.map(field => {
        const value = node.fields?.[field.name] ?? field.defaultValue ?? '';
        return `<label class="tablekit-field tablekit-graph-field">
            <span>${escapeHtml(field.title || field.name)}<em>${escapeHtml(field.type + (field.ref ? ` · ref ${field.ref}` : ''))}</em></span>
            <input class="cmd-input" type="text" data-tablekit-graph-field="${escapeHtml(field.name)}" value="${escapeHtml(value)}">
        </label>`;
    }).join('') : `<div class="tablekit-empty-line">该节点没有可编辑字段。</div>`;
    const relatedMessages = [...report.errors, ...report.warnings].filter(message => message.includes(node.id) || message.includes(node.type));

    return `<div class="tablekit-graph-inspector__shell">
        <div class="tablekit-preview-inspector__head">
            <div>
                <strong>${escapeHtml(type?.title || node.id)}</strong>
                <span>${escapeHtml(node.id)} · ${escapeHtml(node.type)}</span>
            </div>
            <span class="kit-state-pill ${type?.handlerId ? 'kit-state-pill--ok' : 'kit-state-pill--danger'}">${escapeHtml(type?.handlerId || 'missing handler')}</span>
        </div>
        <div class="tablekit-graph-inspector__body">
            <div class="tablekit-graph-port-list">
                ${ports.map(port => `<span>${escapeHtml(port.direction)} · ${escapeHtml(port.kind)} · <strong>${escapeHtml(port.id)}</strong></span>`).join('')}
            </div>
            ${fieldControls}
            ${relatedMessages.length ? `<div class="tablekit-callout tablekit-callout--warning">${escapeHtml(relatedMessages.join(' / '))}</div>` : ''}
        </div>
    </div>`;
}

function renderTableKitGraphContract(contract) {
    return `<div class="tablekit-graph-contract">
        <div class="tablekit-subsection__title">Luban 生成合约</div>
        <div class="kit-detail-summary kit-detail-summary--tablekit-output">
            <div><span>Graph</span><strong>${escapeHtml(contract.graphClass)}</strong></div>
            <div><span>Node</span><strong>${escapeHtml(contract.nodeClass)}</strong></div>
            <div><span>Registry</span><strong>${escapeHtml(contract.registryClass)}</strong></div>
            <div><span>Handler</span><strong>${escapeHtml(contract.handlerInterface)}</strong></div>
        </div>
        <div class="tablekit-graph-handler-list">
            ${contract.handlerIds.map(id => `<code>${escapeHtml(id)}</code>`).join('')}
        </div>
    </div>`;
}

function renderTableKitGraphValidation(report) {
    const rows = report.errors.length || report.warnings.length
        ? [
            ...report.errors.map(message => ({ level: 'error', message })),
            ...report.warnings.map(message => ({ level: 'warning', message })),
        ].map(item => `<div class="tablekit-console-line tablekit-console-line--${escapeHtml(item.level)}"><strong>${escapeHtml(item.level.toUpperCase())}</strong><em>${escapeHtml(item.message)}</em></div>`).join('')
        : `<div class="tablekit-callout tablekit-callout--success">图结构、端口连线和 handler 绑定均通过本地校验。</div>`;
    return `<div class="tablekit-graph-validation">
        <div class="tablekit-subsection__title">图结构校验</div>
        ${rows}
    </div>`;
}

function bindTableKitGraphEditor() {
    const search = $pageBody.querySelector('[data-tablekit-graph-search]');
    if (search && search.dataset.bound !== '1') {
        search.dataset.bound = '1';
        search.addEventListener('input', () => {
            tableKitGraphState.searchTerm = search.value;
            renderTableKitRegistryStatus();
        });
    }

    $pageBody.querySelectorAll('[data-tablekit-graph-node]').forEach(node => {
        if (node.dataset.bound === '1') return;
        node.dataset.bound = '1';
        node.addEventListener('click', () => {
            tableKitGraphState.selectedNodeId = node.dataset.tablekitGraphNode || '';
            renderTableKitRegistryStatus();
        });
    });

    $pageBody.querySelectorAll('[data-tablekit-graph-type]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => selectFirstTableKitGraphNodeOfType(button.dataset.tablekitGraphType));
    });

    $pageBody.querySelectorAll('[data-tablekit-graph-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('input', () => updateTableKitGraphSelectedNodeField(input.dataset.tablekitGraphField, input.value));
    });

    bindKitButtonClick('[data-tablekit-graph-action="reset"]', () => {
        if (typeof window.confirm === 'function' && !window.confirm('重置 XML Graph 示例数据？')) return;
        resetTableKitGraphProject();
        appendTableKitConsoleEntry('info', '已重置 XML Graph 示例数据。');
        renderTableKitRegistryStatus();
    });

    bindKitButtonClick('[data-tablekit-graph-action="copy-xml"]', () => void copyTableKitGraphXml());
}

function selectFirstTableKitGraphNodeOfType(typeId) {
    const node = tableKitGraphProject.graph.nodes.find(candidate => candidate.type === typeId);
    if (node) tableKitGraphState.selectedNodeId = node.id;
    renderTableKitRegistryStatus();
}

function updateTableKitGraphSelectedNodeField(field, value) {
    const selectedId = tableKitGraphState.selectedNodeId;
    tableKitGraphProject = {
        ...tableKitGraphProject,
        graph: {
            ...tableKitGraphProject.graph,
            nodes: tableKitGraphProject.graph.nodes.map(node => {
                if (node.id !== selectedId) return node;
                return {
                    ...node,
                    fields: {
                        ...(node.fields || {}),
                        [field]: value,
                    },
                };
            }),
        },
    };
    persistTableKitGraphProject();
    renderTableKitRegistryStatus();
}

async function copyTableKitGraphXml() {
    const xml = serializeTableKitGraphXml(tableKitGraphProject);
    try {
        await navigator.clipboard?.writeText?.(xml);
        appendTableKitConsoleEntry('success', 'XML Graph 数据已复制到剪贴板。');
    } catch (_) {
        appendTableKitConsoleEntry('warning', '剪贴板不可用，请直接复制 XML 预览内容。');
    }
    renderTableKitRegistryStatus();
}

function getTableKitGraphSelectedNode() {
    const nodes = tableKitGraphProject.graph.nodes;
    return nodes.find(node => node.id === tableKitGraphState.selectedNodeId) || nodes[0] || null;
}

function getTableKitGraphNodeType(typeId) {
    return tableKitGraphProject.nodeTypes.find(type => type.id === typeId) || null;
}

function getTableKitGraphEndpointPosition(endpoint, direction) {
    const parsed = parseTableKitGraphEndpoint(endpoint);
    const node = tableKitGraphProject.graph.nodes.find(candidate => candidate.id === parsed.nodeId);
    if (!node) return null;
    const type = getTableKitGraphNodeType(node.type);
    const ports = (type?.ports || []).filter(port => direction === 'input' ? port.direction === 'input' : port.direction !== 'input');
    const index = Math.max(0, ports.findIndex(port => port.id === parsed.portId));
    const y = getTableKitGraphPortY(ports.length || 1, index);
    return {
        x: node.x + (direction === 'input' ? 0 : TABLEKIT_GRAPH_NODE_WIDTH),
        y: node.y + y,
    };
}

function getTableKitGraphPortY(count, index) {
    if (count <= 1) return 43;
    const top = 26;
    const bottom = 68;
    return Math.round(top + ((bottom - top) * index / (count - 1)));
}

function formatTableKitGraphNodeLabel(value, maxLength) {
    const text = String(value ?? '');
    if (text.length <= maxLength) return text;
    return `${text.slice(0, Math.max(0, maxLength - 3))}...`;
}
