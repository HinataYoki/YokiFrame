// pages/tablekit-graph-model.js
// TableKit Graph data model: node type registry, graph instance validation and XML serialization.
const TABLEKIT_GRAPH_PROJECT_VERSION = '0.1';

const TABLEKIT_GRAPH_DEFAULT_NODE_TYPES = Object.freeze([
    {
        id: 'flow.start',
        title: 'Start',
        category: 'Flow',
        handlerId: 'graph.start',
        color: '#2f7d57',
        ports: [
            { id: 'out', title: 'Out', kind: 'flow', direction: 'output' },
        ],
        fields: [],
    },
    {
        id: 'dialogue.line',
        title: 'Dialogue Line',
        category: 'Dialogue',
        handlerId: 'dialogue.line',
        color: '#2f6f9f',
        ports: [
            { id: 'in', title: 'In', kind: 'flow', direction: 'input' },
            { id: 'out', title: 'Out', kind: 'flow', direction: 'output' },
        ],
        fields: [
            { name: 'speaker', title: 'Speaker', type: 'string', ref: 'npc.TbNpc', defaultValue: 'npc_001' },
            { name: 'text', title: 'Text', type: 'string', defaultValue: '你好，{playerName}' },
        ],
    },
    {
        id: 'dialogue.choice',
        title: 'Choice',
        category: 'Dialogue',
        handlerId: 'dialogue.choice',
        color: '#8a6f26',
        ports: [
            { id: 'in', title: 'In', kind: 'flow', direction: 'input' },
            { id: 'accept', title: 'Accept', kind: 'flow', direction: 'output' },
            { id: 'leave', title: 'Leave', kind: 'flow', direction: 'output' },
        ],
        fields: [
            { name: 'prompt', title: 'Prompt', type: 'string', defaultValue: '要接受委托吗？' },
            { name: 'acceptText', title: 'Accept Text', type: 'string', defaultValue: '接受任务' },
            { name: 'leaveText', title: 'Leave Text', type: 'string', defaultValue: '暂时离开' },
        ],
    },
    {
        id: 'flow.end',
        title: 'End',
        category: 'Flow',
        handlerId: 'graph.end',
        color: '#8b3d49',
        ports: [
            { id: 'in', title: 'In', kind: 'flow', direction: 'input' },
        ],
        fields: [],
    },
]);

const TABLEKIT_GRAPH_DEFAULT_PROJECT = Object.freeze({
    version: TABLEKIT_GRAPH_PROJECT_VERSION,
    graph: {
        id: 'sample.dialogue',
        title: 'Sample Dialogue Graph',
        kind: 'dialogue',
        blackboard: [
            { name: 'playerName', type: 'string', defaultValue: '旅行者' },
        ],
        nodes: [
            { id: 'start', type: 'flow.start', x: 40, y: 170, fields: {} },
            { id: 'line_1', type: 'dialogue.line', x: 220, y: 130, fields: { speaker: 'npc_001', text: '你好，{playerName}' } },
            { id: 'choice_1', type: 'dialogue.choice', x: 400, y: 120, fields: { prompt: '要接受委托吗？', acceptText: '接受任务', leaveText: '暂时离开' } },
            { id: 'end', type: 'flow.end', x: 570, y: 170, fields: {} },
        ],
        edges: [
            { id: 'start_to_line', from: 'start.out', to: 'line_1.in' },
            { id: 'line_to_choice', from: 'line_1.out', to: 'choice_1.in' },
            { id: 'choice_accept_to_end', from: 'choice_1.accept', to: 'end.in' },
            { id: 'choice_leave_to_end', from: 'choice_1.leave', to: 'end.in' },
        ],
    },
    nodeTypes: TABLEKIT_GRAPH_DEFAULT_NODE_TYPES,
});

function sanitizeTableKitGraphProject(raw) {
    const source = raw && typeof raw === 'object' ? raw : {};
    const nodeTypes = sanitizeTableKitGraphNodeTypes(source.nodeTypes);
    const graphSource = source.graph && typeof source.graph === 'object' ? source.graph : TABLEKIT_GRAPH_DEFAULT_PROJECT.graph;
    return {
        version: normalizeTableKitGraphText(source.version, TABLEKIT_GRAPH_PROJECT_VERSION),
        nodeTypes,
        graph: sanitizeTableKitGraphInstance(graphSource, nodeTypes),
    };
}

function sanitizeTableKitGraphNodeTypes(rawTypes) {
    const sourceTypes = Array.isArray(rawTypes) && rawTypes.length ? rawTypes : TABLEKIT_GRAPH_DEFAULT_NODE_TYPES;
    const usedIds = new Set();
    const sanitized = [];

    sourceTypes.forEach((rawType, index) => {
        if (!rawType || typeof rawType !== 'object') return;
        const fallbackId = `custom.node_${index + 1}`;
        const id = makeUniqueTableKitGraphId(normalizeTableKitGraphId(rawType.id, fallbackId), usedIds);
        usedIds.add(id);
        sanitized.push({
            id,
            title: normalizeTableKitGraphText(rawType.title, id),
            category: normalizeTableKitGraphText(rawType.category, 'Custom'),
            handlerId: normalizeTableKitGraphText(rawType.handlerId || rawType.handler, ''),
            color: normalizeTableKitGraphColor(rawType.color),
            ports: sanitizeTableKitGraphPorts(rawType.ports),
            fields: sanitizeTableKitGraphFields(rawType.fields),
        });
    });

    return sanitized.length ? sanitized : sanitizeTableKitGraphNodeTypes(TABLEKIT_GRAPH_DEFAULT_NODE_TYPES);
}

function sanitizeTableKitGraphPorts(rawPorts) {
    const sourcePorts = Array.isArray(rawPorts) ? rawPorts : [];
    const usedIds = new Set();
    return sourcePorts.map((rawPort, index) => {
        const port = rawPort && typeof rawPort === 'object' ? rawPort : {};
        const fallbackId = `port_${index + 1}`;
        const id = makeUniqueTableKitGraphId(normalizeTableKitGraphId(port.id || port.name, fallbackId), usedIds);
        usedIds.add(id);
        return {
            id,
            title: normalizeTableKitGraphText(port.title, id),
            kind: normalizeTableKitGraphText(port.kind, 'flow'),
            direction: port.direction === 'input' ? 'input' : 'output',
        };
    });
}

function sanitizeTableKitGraphFields(rawFields) {
    const sourceFields = Array.isArray(rawFields) ? rawFields : [];
    const usedNames = new Set();
    return sourceFields.map((rawField, index) => {
        const field = rawField && typeof rawField === 'object' ? rawField : {};
        const fallbackName = `field_${index + 1}`;
        const name = makeUniqueTableKitGraphId(normalizeTableKitGraphId(field.name || field.id, fallbackName), usedNames);
        usedNames.add(name);
        return {
            name,
            title: normalizeTableKitGraphText(field.title, name),
            type: normalizeTableKitGraphText(field.type, 'string'),
            ref: normalizeTableKitGraphText(field.ref, ''),
            defaultValue: field.defaultValue !== undefined ? field.defaultValue : '',
        };
    });
}

function sanitizeTableKitGraphInstance(rawGraph, nodeTypes) {
    const graph = rawGraph && typeof rawGraph === 'object' ? rawGraph : TABLEKIT_GRAPH_DEFAULT_PROJECT.graph;
    const nodes = sanitizeTableKitGraphNodes(graph.nodes, nodeTypes);
    return {
        id: normalizeTableKitGraphId(graph.id, TABLEKIT_GRAPH_DEFAULT_PROJECT.graph.id),
        title: normalizeTableKitGraphText(graph.title, TABLEKIT_GRAPH_DEFAULT_PROJECT.graph.title),
        kind: normalizeTableKitGraphText(graph.kind || graph.type, TABLEKIT_GRAPH_DEFAULT_PROJECT.graph.kind),
        blackboard: sanitizeTableKitGraphBlackboard(graph.blackboard),
        nodes,
        edges: sanitizeTableKitGraphEdges(graph.edges),
    };
}

function sanitizeTableKitGraphNodes(rawNodes, nodeTypes) {
    const sourceNodes = Array.isArray(rawNodes) && rawNodes.length ? rawNodes : TABLEKIT_GRAPH_DEFAULT_PROJECT.graph.nodes;
    const usedIds = new Set();
    return sourceNodes.map((rawNode, index) => {
        const node = rawNode && typeof rawNode === 'object' ? rawNode : {};
        const fallbackId = `node_${index + 1}`;
        const id = makeUniqueTableKitGraphId(normalizeTableKitGraphId(node.id, fallbackId), usedIds);
        usedIds.add(id);
        const type = normalizeTableKitGraphText(node.type, nodeTypes[0]?.id || 'custom.node');
        const nodeType = nodeTypes.find(candidate => candidate.id === type);
        return {
            id,
            type,
            x: normalizeTableKitGraphNumber(node.x, 120 + index * 240),
            y: normalizeTableKitGraphNumber(node.y, 120),
            fields: sanitizeTableKitGraphNodeFieldValues(node.fields, nodeType),
        };
    });
}

function sanitizeTableKitGraphNodeFieldValues(rawFields, nodeType) {
    const sourceFields = rawFields && typeof rawFields === 'object' ? rawFields : {};
    const values = {};
    const definitions = Array.isArray(nodeType?.fields) ? nodeType.fields : [];
    definitions.forEach(field => {
        values[field.name] = sourceFields[field.name] !== undefined ? sourceFields[field.name] : field.defaultValue;
    });
    Object.keys(sourceFields).forEach(key => {
        if (!Object.prototype.hasOwnProperty.call(values, key)) values[key] = sourceFields[key];
    });
    return values;
}

function sanitizeTableKitGraphEdges(rawEdges) {
    const sourceEdges = Array.isArray(rawEdges) ? rawEdges : [];
    const usedIds = new Set();
    return sourceEdges.map((rawEdge, index) => {
        const edge = rawEdge && typeof rawEdge === 'object' ? rawEdge : {};
        const from = normalizeTableKitGraphEndpoint(edge.from, '');
        const to = normalizeTableKitGraphEndpoint(edge.to, '');
        const fallbackId = from && to ? `${from}_to_${to}`.replace(/\./g, '_') : `edge_${index + 1}`;
        const id = makeUniqueTableKitGraphId(normalizeTableKitGraphId(edge.id, fallbackId), usedIds);
        usedIds.add(id);
        return { id, from, to };
    });
}

function sanitizeTableKitGraphBlackboard(rawBlackboard) {
    const sourceBlackboard = Array.isArray(rawBlackboard) ? rawBlackboard : [];
    const usedNames = new Set();
    return sourceBlackboard.map((rawVar, index) => {
        const item = rawVar && typeof rawVar === 'object' ? rawVar : {};
        const name = makeUniqueTableKitGraphId(normalizeTableKitGraphId(item.name || item.id, `var_${index + 1}`), usedNames);
        usedNames.add(name);
        return {
            name,
            type: normalizeTableKitGraphText(item.type, 'string'),
            defaultValue: item.defaultValue !== undefined ? item.defaultValue : '',
        };
    });
}

function validateTableKitGraphProject(project) {
    const sanitized = sanitizeTableKitGraphProject(project);
    const errors = [];
    const warnings = [];
    const typeById = new Map();
    const nodeById = new Map();

    sanitized.nodeTypes.forEach(type => {
        if (!type.id) errors.push('nodeType id is required.');
        if (!type.handlerId) errors.push(`nodeType ${type.id} missing handler.`);
        if (!type.ports.length) warnings.push(`nodeType ${type.id} has no ports.`);
        typeById.set(type.id, type);
    });

    sanitized.graph.nodes.forEach(node => {
        if (nodeById.has(node.id)) errors.push(`node ${node.id} is duplicated.`);
        nodeById.set(node.id, node);
        if (!typeById.has(node.type)) errors.push(`node ${node.id} uses missing type ${node.type}.`);
    });

    sanitized.graph.edges.forEach(edge => {
        const from = parseTableKitGraphEndpoint(edge.from);
        const to = parseTableKitGraphEndpoint(edge.to);
        const fromPort = resolveTableKitGraphPort(sanitized, from);
        const toPort = resolveTableKitGraphPort(sanitized, to);

        if (!from.nodeId || !from.portId) errors.push(`edge ${edge.id} has invalid from endpoint.`);
        if (!to.nodeId || !to.portId) errors.push(`edge ${edge.id} has invalid to endpoint.`);
        if (!nodeById.has(from.nodeId)) errors.push(`edge ${edge.id} from node ${from.nodeId} is missing.`);
        if (!nodeById.has(to.nodeId)) errors.push(`edge ${edge.id} to node ${to.nodeId} is missing.`);
        if (fromPort && fromPort.direction !== 'output') errors.push(`edge ${edge.id} from port ${edge.from} is not output.`);
        if (toPort && toPort.direction !== 'input') errors.push(`edge ${edge.id} to port ${edge.to} is not input.`);
        if (fromPort && toPort && fromPort.kind !== toPort.kind) {
            errors.push(`edge ${edge.id} kind mismatch: ${fromPort.kind} -> ${toPort.kind}.`);
        }
    });

    if (!sanitized.graph.nodes.length) warnings.push('graph has no nodes.');
    return { ok: errors.length === 0, errors, warnings, project: sanitized };
}

function resolveTableKitGraphPort(project, endpoint) {
    const node = project.graph.nodes.find(candidate => candidate.id === endpoint.nodeId);
    if (!node) return null;
    const nodeType = project.nodeTypes.find(type => type.id === node.type);
    if (!nodeType) return null;
    return nodeType.ports.find(port => port.id === endpoint.portId) || null;
}

function serializeTableKitGraphXml(project) {
    const sanitized = sanitizeTableKitGraphProject(project);
    const lines = [
        `<?xml version="1.0" encoding="utf-8"?>`,
        `<graphProject version="${escapeTableKitGraphXmlAttribute(sanitized.version)}">`,
        `  <nodeTypes>`,
    ];

    sanitized.nodeTypes.forEach(type => {
        lines.push(`    <nodeType id="${escapeTableKitGraphXmlAttribute(type.id)}" title="${escapeTableKitGraphXmlAttribute(type.title)}" category="${escapeTableKitGraphXmlAttribute(type.category)}" handler="${escapeTableKitGraphXmlAttribute(type.handlerId)}" color="${escapeTableKitGraphXmlAttribute(type.color)}">`);
        lines.push(`      <ports>`);
        type.ports.forEach(port => {
            lines.push(`        <port id="${escapeTableKitGraphXmlAttribute(port.id)}" title="${escapeTableKitGraphXmlAttribute(port.title)}" kind="${escapeTableKitGraphXmlAttribute(port.kind)}" direction="${escapeTableKitGraphXmlAttribute(port.direction)}" />`);
        });
        lines.push(`      </ports>`);
        lines.push(`      <fields>`);
        type.fields.forEach(field => {
            const refAttr = field.ref ? ` ref="${escapeTableKitGraphXmlAttribute(field.ref)}"` : '';
            lines.push(`        <field name="${escapeTableKitGraphXmlAttribute(field.name)}" title="${escapeTableKitGraphXmlAttribute(field.title)}" type="${escapeTableKitGraphXmlAttribute(field.type)}"${refAttr} />`);
        });
        lines.push(`      </fields>`);
        lines.push(`    </nodeType>`);
    });

    lines.push(`  </nodeTypes>`);
    lines.push(`  <graphs>`);
    lines.push(`    <graph id="${escapeTableKitGraphXmlAttribute(sanitized.graph.id)}" title="${escapeTableKitGraphXmlAttribute(sanitized.graph.title)}" type="${escapeTableKitGraphXmlAttribute(sanitized.graph.kind)}">`);
    lines.push(`      <blackboard>`);
    sanitized.graph.blackboard.forEach(item => {
        lines.push(`        <var name="${escapeTableKitGraphXmlAttribute(item.name)}" type="${escapeTableKitGraphXmlAttribute(item.type)}">${escapeTableKitGraphXmlText(item.defaultValue)}</var>`);
    });
    lines.push(`      </blackboard>`);
    lines.push(`      <nodes>`);
    sanitized.graph.nodes.forEach(node => {
        lines.push(`        <node id="${escapeTableKitGraphXmlAttribute(node.id)}" type="${escapeTableKitGraphXmlAttribute(node.type)}" x="${escapeTableKitGraphXmlAttribute(node.x)}" y="${escapeTableKitGraphXmlAttribute(node.y)}">`);
        Object.entries(node.fields || {}).forEach(([name, value]) => {
            lines.push(`          <field name="${escapeTableKitGraphXmlAttribute(name)}">${escapeTableKitGraphXmlText(formatTableKitGraphFieldValue(value))}</field>`);
        });
        lines.push(`        </node>`);
    });
    lines.push(`      </nodes>`);
    lines.push(`      <edges>`);
    sanitized.graph.edges.forEach(edge => {
        lines.push(`        <edge id="${escapeTableKitGraphXmlAttribute(edge.id)}" from="${escapeTableKitGraphXmlAttribute(edge.from)}" to="${escapeTableKitGraphXmlAttribute(edge.to)}" />`);
    });
    lines.push(`      </edges>`);
    lines.push(`    </graph>`);
    lines.push(`  </graphs>`);
    lines.push(`</graphProject>`);
    return lines.join('\n');
}

function getTableKitGraphRuntimeContract(project) {
    const sanitized = sanitizeTableKitGraphProject(project);
    return {
        graphClass: 'GraphDefinition',
        nodeClass: 'GraphNodeDefinition',
        edgeClass: 'GraphEdgeDefinition',
        registryClass: 'GraphNodeTypeRegistry',
        handlerInterface: 'IGraphNodeHandler<TNodeData>',
        handlerIds: sanitized.nodeTypes.map(type => type.handlerId).filter(Boolean),
    };
}

function parseTableKitGraphEndpoint(endpoint) {
    const text = normalizeTableKitGraphEndpoint(endpoint, '');
    const dot = text.lastIndexOf('.');
    if (dot <= 0 || dot >= text.length - 1) return { nodeId: '', portId: '' };
    return {
        nodeId: text.slice(0, dot),
        portId: text.slice(dot + 1),
    };
}

function normalizeTableKitGraphEndpoint(value, fallback) {
    const text = normalizeTableKitGraphText(value, fallback);
    return /^[A-Za-z0-9_.-]+\.[A-Za-z0-9_.-]+$/.test(text) ? text : fallback;
}

function normalizeTableKitGraphId(value, fallback) {
    const text = normalizeTableKitGraphText(value, fallback);
    const sanitized = text.replace(/[^A-Za-z0-9_.-]/g, '_').replace(/^_+|_+$/g, '');
    return sanitized || fallback;
}

function normalizeTableKitGraphText(value, fallback) {
    const text = String(value ?? '').trim();
    return text || fallback;
}

function normalizeTableKitGraphNumber(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) ? Math.round(number) : fallback;
}

function normalizeTableKitGraphColor(value) {
    const text = String(value ?? '').trim();
    return /^#[0-9a-fA-F]{6}$/.test(text) ? text : '#4f5f73';
}

function makeUniqueTableKitGraphId(baseId, usedIds) {
    let id = baseId;
    let index = 2;
    while (usedIds.has(id)) {
        id = `${baseId}_${index}`;
        index += 1;
    }
    return id;
}

function formatTableKitGraphFieldValue(value) {
    if (value === null || value === undefined) return '';
    if (typeof value === 'object') {
        try {
            return JSON.stringify(value);
        } catch (_) {
            return String(value);
        }
    }
    return String(value);
}

function escapeTableKitGraphXmlAttribute(value) {
    return escapeTableKitGraphXmlText(value).replace(/"/g, '&quot;').replace(/'/g, '&apos;');
}

function escapeTableKitGraphXmlText(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}

const tableKitGraphModelApi = {
    TABLEKIT_GRAPH_DEFAULT_PROJECT,
    TABLEKIT_GRAPH_DEFAULT_NODE_TYPES,
    TABLEKIT_GRAPH_PROJECT_VERSION,
    getTableKitGraphRuntimeContract,
    parseTableKitGraphEndpoint,
    sanitizeTableKitGraphProject,
    serializeTableKitGraphXml,
    validateTableKitGraphProject,
};

if (typeof module !== 'undefined' && module.exports) {
    module.exports = tableKitGraphModelApi;
}

if (typeof window !== 'undefined') {
    Object.assign(window, tableKitGraphModelApi);
}
