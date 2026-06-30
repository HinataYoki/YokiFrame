// pages/graphkit-type-registry.js
// Visual node type registry editing for custom Luban XML graph nodes.
function renderGraphKitNodeTypeInspector(type) {
    return `<div class="graphkit-inspector__shell">
        <div class="graphkit-panel-head graphkit-inspector__head">
            <div>
                <span>Node Type Registry</span>
                <strong>${escapeHtml(type.title)}</strong>
                <em>${escapeHtml(type.id)}</em>
            </div>
            <div class="graphkit-panel-head__actions">
                <span class="kit-state-pill ${type.handlerId ? 'kit-state-pill--ok' : 'kit-state-pill--danger'}">${escapeHtml(type.handlerId || 'missing handler')}</span>
                <button class="graphkit-panel-icon" type="button" data-graphkit-panel-toggle="inspector" title="收起 Inspector" aria-label="收起 Inspector">›</button>
            </div>
        </div>
        <div class="graphkit-inspector__body">
            <section class="graphkit-property-group">
                <button class="graphkit-fold-title" type="button">Definition</button>
                ${renderGraphKitNodeTypeField('id', 'Type Id', type.id)}
                ${renderGraphKitNodeTypeField('title', 'Title', type.title)}
                ${renderGraphKitNodeTypeField('category', 'Category', type.category)}
                ${renderGraphKitNodeTypeField('handlerId', 'Handler', type.handlerId)}
                ${renderGraphKitNodeTypeField('color', 'Color', type.color)}
            </section>
            <section class="graphkit-property-group">
                <div class="graphkit-type-registry-head">
                    <button class="graphkit-fold-title" type="button">Ports</button>
                    <div>
                        <button class="btn btn-secondary btn-sm" type="button" data-graphkit-node-type-port-action="add-input">+ In</button>
                        <button class="btn btn-secondary btn-sm" type="button" data-graphkit-node-type-port-action="add-output">+ Out</button>
                    </div>
                </div>
                <div class="graphkit-type-registry-rows">${renderGraphKitNodeTypePorts(type)}</div>
            </section>
            <section class="graphkit-property-group">
                <div class="graphkit-type-registry-head">
                    <button class="graphkit-fold-title" type="button">Fields</button>
                    <button class="btn btn-secondary btn-sm" type="button" data-graphkit-node-type-field-action="add">+ Field</button>
                </div>
                <div class="graphkit-type-registry-rows">${renderGraphKitNodeTypeFields(type)}</div>
            </section>
        </div>
    </div>`;
}

function renderGraphKitNodeTypeField(field, label, value) {
    return `<label class="graphkit-property-field">
        <span>${escapeHtml(label)}<em>${escapeHtml(field)}</em></span>
        <input class="cmd-input" type="text" data-graphkit-node-type-field="${escapeHtml(field)}" value="${escapeHtml(value)}">
    </label>`;
}

function renderGraphKitNodeTypePorts(type) {
    if (!type.ports.length) return `<div class="graphkit-empty-line">该节点类型没有端口。</div>`;
    return type.ports.map(port => `<div class="graphkit-type-registry-row">
        ${renderGraphKitRegistryInput('port', port.id, 'id', 'Id', port.id)}
        ${renderGraphKitRegistryInput('port', port.id, 'title', 'Title', port.title)}
        ${renderGraphKitRegistryInput('port', port.id, 'kind', 'Kind', port.kind)}
        <label class="graphkit-property-field">
            <span>Direction<em>input/output</em></span>
            <select class="cmd-input" data-graphkit-node-type-port-id="${escapeHtml(port.id)}" data-graphkit-node-type-port-field="direction">
                <option value="input" ${port.direction === 'input' ? 'selected' : ''}>input</option>
                <option value="output" ${port.direction === 'output' ? 'selected' : ''}>output</option>
            </select>
        </label>
        <label class="graphkit-property-field graphkit-property-field--checkbox">
            <span>Multiple<em>connection capacity</em></span>
            <input type="checkbox" data-graphkit-node-type-port-id="${escapeHtml(port.id)}" data-graphkit-node-type-port-field="multiple" ${port.multiple ? 'checked' : ''}>
        </label>
        <button class="btn btn-secondary btn-sm" type="button" data-graphkit-node-type-port-action="delete" data-graphkit-node-type-port-id="${escapeHtml(port.id)}">Delete Port</button>
    </div>`).join('');
}

function renderGraphKitNodeTypeFields(type) {
    if (!type.fields.length) return `<div class="graphkit-empty-line">该节点类型没有字段。</div>`;
    return type.fields.map(field => `<div class="graphkit-type-registry-row">
        ${renderGraphKitRegistryInput('field', field.name, 'name', 'Name', field.name)}
        ${renderGraphKitRegistryInput('field', field.name, 'title', 'Title', field.title)}
        ${renderGraphKitRegistryInput('field', field.name, 'type', 'Type', field.type)}
        ${renderGraphKitRegistryInput('field', field.name, 'ref', 'Ref', field.ref)}
        ${renderGraphKitRegistryInput('field', field.name, 'defaultValue', 'Default', field.defaultValue)}
        ${renderGraphKitNodeTypeOptionsInput(field)}
        <label class="graphkit-property-field graphkit-property-field--checkbox">
            <span>Required<em>required</em></span>
            <input type="checkbox" data-graphkit-node-type-field-name="${escapeHtml(field.name)}" data-graphkit-node-type-data-field="required" ${field.required ? 'checked' : ''}>
        </label>
        <button class="btn btn-secondary btn-sm" type="button" data-graphkit-node-type-field-action="delete" data-graphkit-node-type-field-name="${escapeHtml(field.name)}">Delete Field</button>
    </div>`).join('');
}

function renderGraphKitNodeTypeOptionsInput(field) {
    return `<label class="graphkit-property-field">
        <span>Options<em>pipe-separated</em></span>
        <input class="cmd-input" type="text" data-graphkit-node-type-field-name="${escapeHtml(field.name)}" data-graphkit-node-type-data-field="options" value="${escapeHtml((field.options || []).join('|'))}">
    </label>`;
}

function renderGraphKitRegistryInput(kind, itemId, field, label, value, hint) {
    const idAttribute = kind === 'port'
        ? `data-graphkit-node-type-port-id="${escapeHtml(itemId)}" data-graphkit-node-type-port-field="${escapeHtml(field)}"`
        : `data-graphkit-node-type-field-name="${escapeHtml(itemId)}" data-graphkit-node-type-data-field="${escapeHtml(field)}"`;
    return `<label class="graphkit-property-field">
        <span>${escapeHtml(label)}<em>${escapeHtml(hint || field)}</em></span>
        <input class="cmd-input" type="text" ${idAttribute} value="${escapeHtml(value)}">
    </label>`;
}

function bindGraphKitNodeTypeRegistry() {
    $pageBody.querySelectorAll('[data-graphkit-type-edit]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', event => {
            selectGraphKitNodeType(button.dataset.graphkitTypeEdit || '');
            event.preventDefault();
            event.stopPropagation();
        });
    });
    $pageBody.querySelectorAll('[data-graphkit-node-type-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('input', () => updateGraphKitSelectedNodeTypeField(input.dataset.graphkitNodeTypeField, input.value));
    });
    $pageBody.querySelectorAll('[data-graphkit-node-type-port-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        const eventName = input.type === 'checkbox' ? 'change' : 'input';
        input.addEventListener(eventName, () => {
            const value = input.type === 'checkbox' ? input.checked : input.value;
            updateGraphKitSelectedNodeTypePort(input.dataset.graphkitNodeTypePortId, input.dataset.graphkitNodeTypePortField, value);
        });
    });
    $pageBody.querySelectorAll('[data-graphkit-node-type-data-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        const eventName = input.type === 'checkbox' ? 'change' : 'input';
        input.addEventListener(eventName, () => {
            const value = input.type === 'checkbox' ? input.checked : input.value;
            updateGraphKitSelectedNodeTypeDataField(input.dataset.graphkitNodeTypeFieldName, input.dataset.graphkitNodeTypeDataField, value);
        });
    });
    $pageBody.querySelectorAll('[data-graphkit-node-type-port-action]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => handleGraphKitNodeTypePortAction(button));
    });
    $pageBody.querySelectorAll('[data-graphkit-node-type-field-action]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => handleGraphKitNodeTypeFieldAction(button));
    });
}

function selectGraphKitNodeType(typeId) {
    const type = graphKitProject.nodeTypes.find(candidate => candidate.id === typeId);
    if (!type) return false;
    graphKitState.selectedNodeTypeId = type.id;
    graphKitState.selectedNodeId = '';
    graphKitState.selectedNodeIds = [];
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNoteId = '';
    graphKitState.selectedPlacematId = '';
    graphKitState.pendingPortEndpoint = '';
    renderGraphKitWorkbench();
    return true;
}

function createGraphKitNodeType() {
    const id = makeUniqueGraphKitNodeTypeId('custom.node');
    const type = {
        id,
        title: 'Custom Node',
        category: 'Custom',
        handlerId: id,
        color: '#4f5f73',
        ports: [
            { id: 'in', title: 'In', kind: 'flow', direction: 'input' },
            { id: 'out', title: 'Out', kind: 'flow', direction: 'output' },
        ],
        fields: [],
    };
    commitGraphKitMutation('Add node type', before => ({ ...before, nodeTypes: [...before.nodeTypes, type] }), {
        selectedNodeTypeId: id,
        noticeLevel: 'success',
        noticeText: `已添加节点类型 ${id}。`,
    });
}

function updateGraphKitSelectedNodeTypeField(field, value) {
    const selected = getGraphKitSelectedNodeType();
    if (!selected) return;
    const usedIds = new Set(graphKitProject.nodeTypes.filter(type => type.id !== selected.id).map(type => type.id));
    const nextSelectedId = field === 'id' ? makeUniqueGraphKitNodeTypeId(value || selected.id, usedIds) : selected.id;
    commitGraphKitMutation('Edit node type', before => {
        return {
            ...before,
            nodeTypes: before.nodeTypes.map(type => type.id === selected.id ? updateGraphKitNodeTypeField(type, field, value, nextSelectedId) : type),
            graph: {
                ...before.graph,
                nodes: before.graph.nodes.map(node => node.type === selected.id ? { ...node, type: nextSelectedId } : node),
            },
        };
    }, { selectedNodeTypeId: nextSelectedId });
}

function updateGraphKitNodeTypeField(type, field, value, nextId) {
    if (field === 'id') return { ...type, id: nextId, handlerId: type.handlerId || nextId };
    if (field === 'title') return { ...type, title: String(value || '').trim() || type.id };
    if (field === 'category') return { ...type, category: String(value || '').trim() || 'Custom' };
    if (field === 'handlerId') return { ...type, handlerId: String(value || '').trim() };
    if (field === 'color') return { ...type, color: String(value || '').trim() };
    return type;
}

function handleGraphKitNodeTypePortAction(button) {
    const action = button.dataset.graphkitNodeTypePortAction || '';
    if (action === 'add-input') createGraphKitSelectedNodeTypePort('input');
    else if (action === 'add-output') createGraphKitSelectedNodeTypePort('output');
    else if (action === 'delete') deleteGraphKitSelectedNodeTypePort(button.dataset.graphkitNodeTypePortId || '');
}

function createGraphKitSelectedNodeTypePort(direction) {
    const selected = getGraphKitSelectedNodeType();
    if (!selected) return;
    const base = direction === 'input' ? 'in' : 'out';
    const id = makeUniqueGraphKitPortId(base, selected.ports);
    const port = { id, title: direction === 'input' ? 'In' : 'Out', kind: 'flow', direction, multiple: false };
    commitGraphKitMutation('Add node type port', before => replaceGraphKitNodeType(before, selected.id, type => ({ ...type, ports: [...type.ports, port] })), {
        selectedNodeTypeId: selected.id,
    });
}

function updateGraphKitSelectedNodeTypePort(portId, field, value) {
    const selected = getGraphKitSelectedNodeType();
    if (!selected || !portId) return;
    let nextPortId = portId;
    commitGraphKitMutation('Edit node type port', before => {
        const type = before.nodeTypes.find(candidate => candidate.id === selected.id);
        const usedPorts = (type?.ports || []).filter(port => port.id !== portId);
        nextPortId = field === 'id' ? makeUniqueGraphKitPortId(value || portId, usedPorts) : portId;
        const nextProject = replaceGraphKitNodeType(before, selected.id, item => ({
            ...item,
            ports: item.ports.map(port => port.id === portId ? updateGraphKitPortField(port, field, value, nextPortId) : port),
        }));
        return field === 'id' ? remapGraphKitPortEdges(nextProject, selected.id, portId, nextPortId) : nextProject;
    }, { selectedNodeTypeId: selected.id });
}

function updateGraphKitPortField(port, field, value, nextPortId) {
    if (field === 'id') return { ...port, id: nextPortId };
    if (field === 'title') return { ...port, title: String(value || '').trim() || port.id };
    if (field === 'kind') return { ...port, kind: String(value || '').trim() || 'flow' };
    if (field === 'direction') return { ...port, direction: value === 'input' ? 'input' : 'output' };
    if (field === 'multiple') return { ...port, multiple: Boolean(value) };
    return port;
}

function deleteGraphKitSelectedNodeTypePort(portId) {
    const selected = getGraphKitSelectedNodeType();
    if (!selected || !portId) return;
    commitGraphKitMutation('Delete node type port', before => removeGraphKitPortAndEdges(before, selected.id, portId), {
        selectedNodeTypeId: selected.id,
        noticeLevel: 'warning',
        noticeText: `已删除端口 ${portId}，相关连线也已移除。`,
    });
}

function handleGraphKitNodeTypeFieldAction(button) {
    const action = button.dataset.graphkitNodeTypeFieldAction || '';
    if (action === 'add') createGraphKitSelectedNodeTypeField();
    else if (action === 'delete') deleteGraphKitSelectedNodeTypeField(button.dataset.graphkitNodeTypeFieldName || '');
}

function createGraphKitSelectedNodeTypeField() {
    const selected = getGraphKitSelectedNodeType();
    if (!selected) return;
    const name = makeUniqueGraphKitFieldName('field', selected.fields);
    const field = { name, title: 'Field', type: 'string', ref: '', defaultValue: '', required: false, options: [] };
    commitGraphKitMutation('Add node type field', before => {
        const nextProject = replaceGraphKitNodeType(before, selected.id, type => ({ ...type, fields: [...type.fields, field] }));
        return mapGraphKitNodesOfType(nextProject, selected.id, node => ({ ...node, fields: { ...(node.fields || {}), [name]: '' } }));
    }, { selectedNodeTypeId: selected.id });
}

function updateGraphKitSelectedNodeTypeDataField(fieldName, property, value) {
    const selected = getGraphKitSelectedNodeType();
    if (!selected || !fieldName) return;
    let nextFieldName = fieldName;
    commitGraphKitMutation('Edit node type field', before => {
        const type = before.nodeTypes.find(candidate => candidate.id === selected.id);
        const usedFields = (type?.fields || []).filter(field => field.name !== fieldName);
        nextFieldName = property === 'name' ? makeUniqueGraphKitFieldName(value || fieldName, usedFields) : fieldName;
        const nextProject = replaceGraphKitNodeType(before, selected.id, item => ({
            ...item,
            fields: item.fields.map(field => field.name === fieldName ? updateGraphKitDataField(field, property, value, nextFieldName) : field),
        }));
        return property === 'name' ? remapGraphKitNodeFieldValues(nextProject, selected.id, fieldName, nextFieldName) : nextProject;
    }, { selectedNodeTypeId: selected.id });
}

function updateGraphKitDataField(field, property, value, nextFieldName) {
    if (property === 'name') return { ...field, name: nextFieldName };
    if (property === 'title') return { ...field, title: String(value || '').trim() || field.name };
    if (property === 'type') return { ...field, type: String(value || '').trim() || 'string' };
    if (property === 'ref') return { ...field, ref: String(value || '').trim() };
    if (property === 'defaultValue') return { ...field, defaultValue: value };
    if (property === 'required') return { ...field, required: Boolean(value) };
    if (property === 'options') return { ...field, options: sanitizeGraphKitFieldOptions(value) };
    return field;
}

function deleteGraphKitSelectedNodeTypeField(fieldName) {
    const selected = getGraphKitSelectedNodeType();
    if (!selected || !fieldName) return;
    commitGraphKitMutation('Delete node type field', before => {
        const nextProject = replaceGraphKitNodeType(before, selected.id, type => ({ ...type, fields: type.fields.filter(field => field.name !== fieldName) }));
        return mapGraphKitNodesOfType(nextProject, selected.id, node => {
            const fields = { ...(node.fields || {}) };
            delete fields[fieldName];
            return { ...node, fields };
        });
    }, { selectedNodeTypeId: selected.id });
}

function replaceGraphKitNodeType(project, typeId, updater) {
    return { ...project, nodeTypes: project.nodeTypes.map(type => type.id === typeId ? updater(type) : type) };
}

function mapGraphKitNodesOfType(project, typeId, mapper) {
    return {
        ...project,
        graph: {
            ...project.graph,
            nodes: project.graph.nodes.map(node => node.type === typeId ? mapper(node) : node),
        },
    };
}

function remapGraphKitNodeFieldValues(project, typeId, oldName, nextName) {
    return mapGraphKitNodesOfType(project, typeId, node => {
        const fields = { ...(node.fields || {}) };
        if (Object.prototype.hasOwnProperty.call(fields, oldName)) {
            fields[nextName] = fields[oldName];
            delete fields[oldName];
        }
        return { ...node, fields };
    });
}

function remapGraphKitPortEdges(project, typeId, oldPortId, nextPortId) {
    const nodeIds = new Set(project.graph.nodes.filter(node => node.type === typeId).map(node => node.id));
    return {
        ...project,
        graph: {
            ...project.graph,
            edges: project.graph.edges.map(edge => ({
                ...edge,
                from: remapGraphKitEndpointPort(edge.from, nodeIds, oldPortId, nextPortId),
                to: remapGraphKitEndpointPort(edge.to, nodeIds, oldPortId, nextPortId),
            })),
        },
    };
}

function removeGraphKitPortAndEdges(project, typeId, portId) {
    const nodeIds = new Set(project.graph.nodes.filter(node => node.type === typeId).map(node => node.id));
    const nextProject = replaceGraphKitNodeType(project, typeId, type => ({ ...type, ports: type.ports.filter(port => port.id !== portId) }));
    return {
        ...nextProject,
        graph: {
            ...nextProject.graph,
            edges: nextProject.graph.edges.filter(edge => !doesGraphKitEdgeUsePort(edge, nodeIds, portId)),
        },
    };
}

function remapGraphKitEndpointPort(endpoint, nodeIds, oldPortId, nextPortId) {
    const parsed = parseGraphKitEndpoint(endpoint);
    if (!nodeIds.has(parsed.nodeId) || parsed.portId !== oldPortId) return endpoint;
    return `${parsed.nodeId}.${nextPortId}`;
}

function doesGraphKitEdgeUsePort(edge, nodeIds, portId) {
    const from = parseGraphKitEndpoint(edge.from);
    const to = parseGraphKitEndpoint(edge.to);
    return (nodeIds.has(from.nodeId) && from.portId === portId) || (nodeIds.has(to.nodeId) && to.portId === portId);
}

function makeUniqueGraphKitNodeTypeId(baseId, usedIds) {
    const used = usedIds || new Set(graphKitProject.nodeTypes.map(type => type.id));
    return makeUniqueGraphKitIdForRegistry(baseId, used, 'custom.node');
}

function makeUniqueGraphKitPortId(baseId, ports) {
    return makeUniqueGraphKitIdForRegistry(baseId, new Set((ports || []).map(port => port.id)), 'port');
}

function makeUniqueGraphKitFieldName(baseName, fields) {
    return makeUniqueGraphKitIdForRegistry(baseName, new Set((fields || []).map(field => field.name)), 'field');
}

function makeUniqueGraphKitIdForRegistry(baseId, usedIds, fallback) {
    const base = normalizeGraphKitId(baseId, fallback);
    let id = base;
    let index = 2;
    while (usedIds.has(id)) {
        id = `${base}_${index}`;
        index += 1;
    }
    return id;
}
