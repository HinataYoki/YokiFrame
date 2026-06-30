// pages/graphkit-model.js
// GraphKit data model: node type registry, graph instance validation and XML serialization.
const GRAPHKIT_PROJECT_VERSION = '0.1';
const GRAPHKIT_DEFAULT_BLACKBOARD_SECTION = 'Default';
const GRAPHKIT_PROJECT_VERSION_MIGRATIONS = Object.freeze({
    '1': 'graphProject.version.1-to-0.1',
});

const GRAPHKIT_DEFAULT_NODE_TYPES = Object.freeze([
    {
        id: 'flow.start',
        title: 'Start',
        category: 'Flow',
        handlerId: 'graph.start',
        color: '#2f7d57',
        ports: [
            { id: 'out', title: 'Out', kind: 'flow', direction: 'output', multiple: true },
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
            { id: 'in', title: 'In', kind: 'flow', direction: 'input', multiple: false },
            { id: 'out', title: 'Out', kind: 'flow', direction: 'output', multiple: false },
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
            { id: 'in', title: 'In', kind: 'flow', direction: 'input', multiple: false },
            { id: 'accept', title: 'Accept', kind: 'flow', direction: 'output', multiple: false },
            { id: 'leave', title: 'Leave', kind: 'flow', direction: 'output', multiple: false },
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
            { id: 'in', title: 'In', kind: 'flow', direction: 'input', multiple: true },
        ],
        fields: [],
    },
    {
        id: 'graph.subgraph',
        title: 'Subgraph',
        category: 'Graph Reference',
        handlerId: 'graph.subgraph',
        color: '#5f679f',
        ports: [
            { id: 'in', title: 'In', kind: 'flow', direction: 'input', multiple: false },
            { id: 'out', title: 'Out', kind: 'flow', direction: 'output', multiple: false },
        ],
        fields: [
            { name: 'targetGraph', title: 'Target Graph', type: 'graphRef', defaultValue: '' },
            { name: 'entry', title: 'Entry', type: 'string', defaultValue: 'start' },
            { name: 'exit', title: 'Exit', type: 'string', defaultValue: 'end' },
        ],
    },
    {
        id: 'graph.portal',
        title: 'Portal',
        category: 'Graph Reference',
        handlerId: 'graph.portal',
        color: '#74609a',
        ports: [
            { id: 'in', title: 'In', kind: 'flow', direction: 'input', multiple: false },
            { id: 'out', title: 'Out', kind: 'flow', direction: 'output', multiple: false },
        ],
        fields: [
            { name: 'portalId', title: 'Portal Id', type: 'string', defaultValue: 'portal' },
            { name: 'targetGraph', title: 'Target Graph', type: 'graphRef', defaultValue: '' },
            { name: 'targetPortal', title: 'Target Portal', type: 'string', defaultValue: '' },
        ],
    },
    {
        id: 'graph.variable',
        title: 'Blackboard Variable',
        category: 'Blackboard',
        handlerId: 'graph.variable',
        color: '#4f6f8f',
        ports: [
            { id: 'value', title: 'Value', kind: 'value', direction: 'output', multiple: true },
        ],
        fields: [
            { name: 'variableName', title: 'Variable Name', type: 'blackboardRef', defaultValue: '' },
            { name: 'variableType', title: 'Variable Type', type: 'string', defaultValue: 'string' },
        ],
    },
]);

const GRAPHKIT_DEFAULT_PROJECT = Object.freeze({
    version: GRAPHKIT_PROJECT_VERSION,
    graph: {
        id: 'sample.dialogue',
        title: 'Sample Dialogue Graph',
        kind: 'dialogue',
        blackboard: [
            { name: 'playerName', type: 'string', defaultValue: '旅行者', section: GRAPHKIT_DEFAULT_BLACKBOARD_SECTION },
        ],
        placemats: [
            { id: 'main_flow', title: 'Main Flow', x: 185, y: 86, width: 520, height: 210, color: '#34566f', order: 0, locked: false, collapsed: false, nodeIds: ['line_1', 'choice_1', 'end'] },
        ],
        notes: [
            { id: 'intro_note', title: 'Designer Note', text: '这里可以记录策划意图、条件和后续接入的 handler 语义。', x: 210, y: 340, width: 230, height: 118, color: '#6f5a2e' },
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
    graphs: [],
    nodeTypes: GRAPHKIT_DEFAULT_NODE_TYPES,
});

function sanitizeGraphKitProject(raw) {
    const source = raw && typeof raw === 'object' ? raw : {};
    const versionMigration = migrateGraphKitProjectVersion(source.version, source.migration);
    const nodeTypes = sanitizeGraphKitNodeTypes(source.nodeTypes);
    const graphSources = getGraphKitProjectGraphSources(source);
    let graphs = sanitizeGraphKitGraphs(graphSources, nodeTypes);
    const explicitGraph = source.graph && typeof source.graph === 'object' ? sanitizeGraphKitInstance(source.graph, nodeTypes) : null;
    let activeGraphId = normalizeGraphKitId(source.activeGraphId || explicitGraph?.id || graphs[0]?.id, graphs[0]?.id || GRAPHKIT_DEFAULT_PROJECT.graph.id);

    if (explicitGraph) {
        const replaceIndex = graphs.findIndex(graph => graph.id === activeGraphId || graph.id === explicitGraph.id);
        if (replaceIndex >= 0) graphs[replaceIndex] = explicitGraph;
        else graphs = [explicitGraph, ...graphs];
        activeGraphId = explicitGraph.id;
    }

    graphs = sanitizeGraphKitGraphs(graphs, nodeTypes);
    const activeGraph = graphs.find(graph => graph.id === activeGraphId) || graphs[0] || sanitizeGraphKitInstance(GRAPHKIT_DEFAULT_PROJECT.graph, nodeTypes);
    const sanitized = {
        version: versionMigration.version,
        nodeTypes,
        activeGraphId: activeGraph.id,
        graph: activeGraph,
        graphs,
    };
    if (versionMigration.migration) sanitized.migration = versionMigration.migration;
    return sanitized;
}

function migrateGraphKitProjectVersion(rawVersion, existingMigration) {
    const version = normalizeGraphKitText(rawVersion, GRAPHKIT_PROJECT_VERSION);
    const migration = sanitizeGraphKitMigration(existingMigration);
    const migrationId = GRAPHKIT_PROJECT_VERSION_MIGRATIONS[version];
    if (!migrationId) return { version, migration };
    return {
        version: GRAPHKIT_PROJECT_VERSION,
        migration: appendGraphKitMigration(migration, version, GRAPHKIT_PROJECT_VERSION, migrationId),
    };
}

function sanitizeGraphKitMigration(rawMigration) {
    if (!rawMigration || typeof rawMigration !== 'object') return null;
    const fromVersion = normalizeGraphKitText(rawMigration.fromVersion, '');
    const toVersion = normalizeGraphKitText(rawMigration.toVersion, GRAPHKIT_PROJECT_VERSION);
    const applied = Array.isArray(rawMigration.applied)
        ? [...new Set(rawMigration.applied.map(item => normalizeGraphKitText(item, '')).filter(Boolean))]
        : [];
    if (!fromVersion || !applied.length) return null;
    return { fromVersion, toVersion, applied };
}

function appendGraphKitMigration(existingMigration, fromVersion, toVersion, migrationId) {
    const applied = existingMigration?.applied ? [...existingMigration.applied] : [];
    if (!applied.includes(migrationId)) applied.push(migrationId);
    return {
        fromVersion: existingMigration?.fromVersion || fromVersion,
        toVersion,
        applied,
    };
}

function getGraphKitProjectGraphSources(source) {
    if (Array.isArray(source.graphs) && source.graphs.length) return source.graphs;
    if (source.graph && typeof source.graph === 'object') return [source.graph];
    return [GRAPHKIT_DEFAULT_PROJECT.graph];
}

function sanitizeGraphKitGraphs(rawGraphs, nodeTypes) {
    const sourceGraphs = Array.isArray(rawGraphs) && rawGraphs.length ? rawGraphs : [GRAPHKIT_DEFAULT_PROJECT.graph];
    const usedIds = new Set();
    const graphs = sourceGraphs.map((rawGraph, index) => {
        const graph = sanitizeGraphKitInstance(rawGraph, nodeTypes);
        const id = makeUniqueGraphKitId(normalizeGraphKitId(graph.id, `graph_${index + 1}`), usedIds);
        usedIds.add(id);
        return id === graph.id ? graph : { ...graph, id };
    });
    return graphs.length ? graphs : [sanitizeGraphKitInstance(GRAPHKIT_DEFAULT_PROJECT.graph, nodeTypes)];
}

function sanitizeGraphKitNodeTypes(rawTypes) {
    const sourceTypes = Array.isArray(rawTypes) ? rawTypes : [];
    const usedIds = new Set();
    const sanitized = [];

    const appendSanitizedType = (rawType, index) => {
        if (!rawType || typeof rawType !== 'object') return;
        const fallbackId = `custom.node_${index + 1}`;
        const id = makeUniqueGraphKitId(normalizeGraphKitId(rawType.id, fallbackId), usedIds);
        usedIds.add(id);
        sanitized.push({
            id,
            title: normalizeGraphKitText(rawType.title, id),
            category: normalizeGraphKitText(rawType.category, 'Custom'),
            handlerId: normalizeGraphKitText(rawType.handlerId || rawType.handler, ''),
            color: normalizeGraphKitColor(rawType.color),
            ports: sanitizeGraphKitPorts(rawType.ports),
            fields: sanitizeGraphKitFields(rawType.fields),
        });
    };

    sourceTypes.forEach(appendSanitizedType);
    GRAPHKIT_DEFAULT_NODE_TYPES.forEach((rawType, index) => {
        if (usedIds.has(rawType.id)) return;
        appendSanitizedType(rawType, sourceTypes.length + index);
    });

    return sanitized;
}

function sanitizeGraphKitPorts(rawPorts) {
    const sourcePorts = Array.isArray(rawPorts) ? rawPorts : [];
    const usedIds = new Set();
    return sourcePorts.map((rawPort, index) => {
        const port = rawPort && typeof rawPort === 'object' ? rawPort : {};
        const fallbackId = `port_${index + 1}`;
        const id = makeUniqueGraphKitId(normalizeGraphKitId(port.id || port.name, fallbackId), usedIds);
        usedIds.add(id);
        return {
            id,
            title: normalizeGraphKitText(port.title, id),
            kind: normalizeGraphKitText(port.kind, 'flow'),
            direction: port.direction === 'input' ? 'input' : 'output',
            multiple: normalizeGraphKitBoolean(port.multiple, false),
        };
    });
}

function sanitizeGraphKitFields(rawFields) {
    const sourceFields = Array.isArray(rawFields) ? rawFields : [];
    const usedNames = new Set();
    return sourceFields.map((rawField, index) => {
        const field = rawField && typeof rawField === 'object' ? rawField : {};
        const fallbackName = `field_${index + 1}`;
        const name = makeUniqueGraphKitId(normalizeGraphKitId(field.name || field.id, fallbackName), usedNames);
        usedNames.add(name);
        return {
            name,
            title: normalizeGraphKitText(field.title, name),
            type: normalizeGraphKitText(field.type, 'string'),
            ref: normalizeGraphKitText(field.ref, ''),
            defaultValue: field.defaultValue !== undefined ? field.defaultValue : '',
            required: normalizeGraphKitBoolean(field.required, false),
            options: sanitizeGraphKitFieldOptions(field.options),
        };
    });
}

function sanitizeGraphKitFieldOptions(rawOptions) {
    const source = Array.isArray(rawOptions)
        ? rawOptions
        : String(rawOptions ?? '').split(/[|,\n]/g);
    const values = source
        .map(item => normalizeGraphKitText(item, ''))
        .filter(Boolean);
    return [...new Set(values)];
}

function sanitizeGraphKitInstance(rawGraph, nodeTypes) {
    const graph = rawGraph && typeof rawGraph === 'object' ? rawGraph : GRAPHKIT_DEFAULT_PROJECT.graph;
    const fallbackNodes = Array.isArray(graph.nodes) ? [] : GRAPHKIT_DEFAULT_PROJECT.graph.nodes;
    const nodes = sanitizeGraphKitNodes(graph.nodes, nodeTypes, fallbackNodes);
    const nodeIds = new Set(nodes.map(node => node.id));
    return {
        id: normalizeGraphKitId(graph.id, GRAPHKIT_DEFAULT_PROJECT.graph.id),
        title: normalizeGraphKitText(graph.title, GRAPHKIT_DEFAULT_PROJECT.graph.title),
        kind: normalizeGraphKitText(graph.kind || graph.type, GRAPHKIT_DEFAULT_PROJECT.graph.kind),
        blackboard: sanitizeGraphKitBlackboard(graph.blackboard),
        placemats: sanitizeGraphKitPlacemats(graph.placemats, nodeIds),
        notes: sanitizeGraphKitNotes(graph.notes),
        nodes,
        edges: sanitizeGraphKitEdges(graph.edges),
    };
}

function sanitizeGraphKitNodes(rawNodes, nodeTypes, fallbackNodes) {
    const fallback = Array.isArray(fallbackNodes) ? fallbackNodes : GRAPHKIT_DEFAULT_PROJECT.graph.nodes;
    const sourceNodes = Array.isArray(rawNodes) ? rawNodes : fallback;
    const usedIds = new Set();
    return sourceNodes.map((rawNode, index) => {
        const node = rawNode && typeof rawNode === 'object' ? rawNode : {};
        const fallbackId = `node_${index + 1}`;
        const id = makeUniqueGraphKitId(normalizeGraphKitId(node.id, fallbackId), usedIds);
        usedIds.add(id);
        const type = normalizeGraphKitText(node.type, nodeTypes[0]?.id || 'custom.node');
        const nodeType = nodeTypes.find(candidate => candidate.id === type);
        return {
            id,
            type,
            x: normalizeGraphKitNumber(node.x, 120 + index * 240),
            y: normalizeGraphKitNumber(node.y, 120),
            collapsed: normalizeGraphKitBoolean(node.collapsed, false),
            fields: sanitizeGraphKitNodeFieldValues(node.fields, nodeType),
        };
    });
}

function sanitizeGraphKitNodeFieldValues(rawFields, nodeType) {
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

function sanitizeGraphKitEdges(rawEdges) {
    const sourceEdges = Array.isArray(rawEdges) ? rawEdges : [];
    const usedIds = new Set();
    return sourceEdges.map((rawEdge, index) => {
        const edge = rawEdge && typeof rawEdge === 'object' ? rawEdge : {};
        const from = normalizeGraphKitEndpoint(edge.from, '');
        const to = normalizeGraphKitEndpoint(edge.to, '');
        const fallbackId = from && to ? `${from}_to_${to}`.replace(/\./g, '_') : `edge_${index + 1}`;
        const id = makeUniqueGraphKitId(normalizeGraphKitId(edge.id, fallbackId), usedIds);
        usedIds.add(id);
        return {
            id,
            from,
            to,
            label: normalizeGraphKitText(edge.label, ''),
            condition: normalizeGraphKitText(edge.condition, ''),
            priority: normalizeGraphKitNumber(edge.priority, 0),
        };
    });
}

function sanitizeGraphKitBlackboard(rawBlackboard) {
    const sourceBlackboard = Array.isArray(rawBlackboard) ? rawBlackboard : [];
    const usedNames = new Set();
    return sourceBlackboard.map((rawVar, index) => {
        const item = rawVar && typeof rawVar === 'object' ? rawVar : {};
        const name = makeUniqueGraphKitId(normalizeGraphKitId(item.name || item.id, `var_${index + 1}`), usedNames);
        usedNames.add(name);
        return {
            name,
            type: normalizeGraphKitText(item.type, 'string'),
            section: normalizeGraphKitBlackboardSection(item.section),
            defaultValue: item.defaultValue !== undefined ? item.defaultValue : '',
        };
    });
}

function sanitizeGraphKitPlacemats(rawPlacemats, nodeIds) {
    const sourcePlacemats = Array.isArray(rawPlacemats) ? rawPlacemats : [];
    const usedIds = new Set();
    return sourcePlacemats.map((rawPlacemat, index) => {
        const item = rawPlacemat && typeof rawPlacemat === 'object' ? rawPlacemat : {};
        const id = makeUniqueGraphKitId(normalizeGraphKitId(item.id, `placemat_${index + 1}`), usedIds);
        usedIds.add(id);
        const title = normalizeGraphKitText(item.title, id);
        const width = Math.max(80, normalizeGraphKitNumber(item.width, 320));
        const height = Math.max(60, normalizeGraphKitNumber(item.height, 180));
        return {
            id,
            title,
            x: normalizeGraphKitNumber(item.x, 120),
            y: normalizeGraphKitNumber(item.y, 120),
            width,
            height,
            color: normalizeGraphKitColor(item.color),
            order: normalizeGraphKitNumber(item.order, index),
            locked: normalizeGraphKitBoolean(item.locked, false),
            collapsed: normalizeGraphKitBoolean(item.collapsed, false),
            nodeIds: sanitizeGraphKitNodeIdRefs(item.nodeIds || item.nodes, nodeIds),
        };
    });
}

function sanitizeGraphKitNotes(rawNotes) {
    const sourceNotes = Array.isArray(rawNotes) ? rawNotes : [];
    const usedIds = new Set();
    return sourceNotes.map((rawNote, index) => {
        const item = rawNote && typeof rawNote === 'object' ? rawNote : {};
        const id = makeUniqueGraphKitId(normalizeGraphKitId(item.id, `note_${index + 1}`), usedIds);
        usedIds.add(id);
        const title = normalizeGraphKitText(item.title, id);
        const width = Math.max(80, normalizeGraphKitNumber(item.width, 180));
        const height = Math.max(60, normalizeGraphKitNumber(item.height, 110));
        return { id, title, text: normalizeGraphKitText(item.text, ''), x: normalizeGraphKitNumber(item.x, 160), y: normalizeGraphKitNumber(item.y, 160), width, height, color: normalizeGraphKitColor(item.color || '#6f5a2e') };
    });
}

function sanitizeGraphKitNodeIdRefs(rawNodeIds, nodeIds) {
    const source = Array.isArray(rawNodeIds) ? rawNodeIds : [];
    const refs = source.map(value => normalizeGraphKitId(value, '')).filter(Boolean);
    return [...new Set(refs)].filter(id => !nodeIds || nodeIds.has(id));
}

function validateGraphKitProject(project) {
    const sanitized = sanitizeGraphKitProject(project);
    const errors = [];
    const warnings = [];
    const typeById = new Map();
    const graphIds = new Set(sanitized.graphs.map(graph => graph.id));
    const graphById = new Map(sanitized.graphs.map(graph => [graph.id, graph]));

    if (sanitized.migration?.applied?.length) {
        warnings.push(`graphProject version ${sanitized.migration.fromVersion} migrated to ${sanitized.migration.toVersion}.`);
    }

    sanitized.nodeTypes.forEach(type => {
        if (!type.id) errors.push('nodeType id is required.');
        if (!type.handlerId) errors.push(`nodeType ${type.id} missing handler.`);
        if (!type.ports.length) warnings.push(`nodeType ${type.id} has no ports.`);
        typeById.set(type.id, type);
    });

    sanitized.graphs.forEach(graph => {
        validateGraphKitGraphInstance(graph, sanitized, typeById, graphIds, graphById, errors, warnings);
    });

    return { ok: errors.length === 0, errors, warnings, project: sanitized };
}

function validateGraphKitGraphInstance(graph, project, typeById, graphIds, graphById, errors, warnings) {
    const nodeById = new Map();
    graph.nodes.forEach(node => {
        if (nodeById.has(node.id)) pushGraphKitGraphIssue(errors, graph, `node ${node.id} is duplicated.`);
        nodeById.set(node.id, node);
        const nodeType = typeById.get(node.type);
        if (!nodeType) pushGraphKitGraphIssue(errors, graph, `node ${node.id} uses missing type ${node.type}.`);
        else validateGraphKitNodeFieldReferences(node, nodeType, graph, graphIds, graphById, errors);
    });

    const endpointCounts = new Map();
    graph.edges.forEach(edge => {
        const from = parseGraphKitEndpoint(edge.from);
        const to = parseGraphKitEndpoint(edge.to);
        const fromPort = resolveGraphKitPortInGraph(project, graph, from);
        const toPort = resolveGraphKitPortInGraph(project, graph, to);

        incrementGraphKitEndpointCount(endpointCounts, edge.from);
        incrementGraphKitEndpointCount(endpointCounts, edge.to);
        if (!from.nodeId || !from.portId) pushGraphKitGraphIssue(errors, graph, `edge ${edge.id} has invalid from endpoint.`);
        if (!to.nodeId || !to.portId) pushGraphKitGraphIssue(errors, graph, `edge ${edge.id} has invalid to endpoint.`);
        if (!nodeById.has(from.nodeId)) pushGraphKitGraphIssue(errors, graph, `edge ${edge.id} from node ${from.nodeId} is missing.`);
        if (!nodeById.has(to.nodeId)) pushGraphKitGraphIssue(errors, graph, `edge ${edge.id} to node ${to.nodeId} is missing.`);
        if (fromPort && fromPort.direction !== 'output') pushGraphKitGraphIssue(errors, graph, `edge ${edge.id} from port ${edge.from} is not output.`);
        if (toPort && toPort.direction !== 'input') pushGraphKitGraphIssue(errors, graph, `edge ${edge.id} to port ${edge.to} is not input.`);
        if (fromPort && toPort && fromPort.kind !== toPort.kind) {
            pushGraphKitGraphIssue(errors, graph, `edge ${edge.id} kind mismatch: ${fromPort.kind} -> ${toPort.kind}.`);
        }
    });

    endpointCounts.forEach((count, endpoint) => {
        const parsed = parseGraphKitEndpoint(endpoint);
        const port = resolveGraphKitPortInGraph(project, graph, parsed);
        if (port && !port.multiple && count > 1) {
            pushGraphKitGraphIssue(errors, graph, `port ${endpoint} allows only one connection.`);
        }
    });

    graph.placemats.forEach(item => {
        item.nodeIds.forEach(nodeId => {
            if (!nodeById.has(nodeId)) pushGraphKitGraphIssue(warnings, graph, `placemat ${item.id} references missing node ${nodeId}.`);
        });
    });

    if (!graph.nodes.length) pushGraphKitGraphIssue(warnings, graph, 'graph has no nodes.');
}

function pushGraphKitGraphIssue(issues, graph, message) {
    issues.push(`graph ${graph.id} ${message}`);
}

function incrementGraphKitEndpointCount(endpointCounts, endpoint) {
    const normalizedEndpoint = normalizeGraphKitEndpoint(endpoint, '');
    if (!normalizedEndpoint) return;
    endpointCounts.set(normalizedEndpoint, (endpointCounts.get(normalizedEndpoint) || 0) + 1);
}

function validateGraphKitNodeFieldReferences(node, nodeType, graph, graphIds, graphById, errors) {
    (nodeType.fields || []).forEach(field => {
        const fieldValue = node.fields?.[field.name];
        if (field.required && !formatGraphKitFieldValue(fieldValue).trim()) {
            pushGraphKitGraphIssue(errors, graph, `node ${node.id} missing required field ${field.name}.`);
        }
        if (field.options?.length) {
            const normalizedValue = formatGraphKitFieldValue(fieldValue).trim();
            if (normalizedValue && !field.options.includes(normalizedValue)) {
                pushGraphKitGraphIssue(errors, graph, `node ${node.id} field ${field.name} has invalid option ${normalizedValue}.`);
            }
        }
        if (field.type === 'graphRef') {
            const targetGraph = normalizeGraphKitId(fieldValue, '');
            if (targetGraph && !graphIds.has(targetGraph)) {
                pushGraphKitGraphIssue(errors, graph, `node ${node.id} missing graph reference ${targetGraph}.`);
            }
        }
    });
    validateGraphKitBlackboardVariableReference(node, graph, errors);
    validateGraphKitPortalTargetReference(node, graph, graphById, errors);
}

function validateGraphKitBlackboardVariableReference(node, graph, errors) {
    if (node.type !== 'graph.variable') return;
    const variableName = normalizeGraphKitId(node.fields?.variableName, '');
    if (!variableName) {
        pushGraphKitGraphIssue(errors, graph, `node ${node.id} missing blackboard variable reference.`);
        return;
    }
    const variable = (graph.blackboard || []).find(item => item.name === variableName);
    if (!variable) {
        pushGraphKitGraphIssue(errors, graph, `node ${node.id} missing blackboard variable ${variableName}.`);
        return;
    }
    const declaredType = normalizeGraphKitText(node.fields?.variableType, '');
    if (declaredType && declaredType !== variable.type) {
        pushGraphKitGraphIssue(errors, graph, `node ${node.id} blackboard variable ${variableName} type mismatch: ${declaredType} -> ${variable.type}.`);
    }
}

function validateGraphKitPortalTargetReference(node, graph, graphById, errors) {
    if (node.type !== 'graph.portal') return;
    const targetGraphId = normalizeGraphKitId(node.fields?.targetGraph, '');
    const targetPortalId = normalizeGraphKitId(node.fields?.targetPortal, '');
    if (!targetGraphId || !targetPortalId) return;
    const targetGraph = graphById.get(targetGraphId);
    if (!targetGraph) return;
    const targetPortal = (targetGraph.nodes || []).find(candidate => (
        candidate.type === 'graph.portal'
        && normalizeGraphKitId(candidate.fields?.portalId, '') === targetPortalId
    ));
    if (!targetPortal) pushGraphKitGraphIssue(errors, graph, `node ${node.id} missing target portal ${targetPortalId} in graph ${targetGraphId}.`);
}

function resolveGraphKitPort(project, endpoint) {
    return resolveGraphKitPortInGraph(project, project.graph, endpoint);
}

function resolveGraphKitPortInGraph(project, graph, endpoint) {
    const node = graph.nodes.find(candidate => candidate.id === endpoint.nodeId);
    if (!node) return null;
    const nodeType = project.nodeTypes.find(type => type.id === node.type);
    if (!nodeType) return null;
    return nodeType.ports.find(port => port.id === endpoint.portId) || null;
}

function getGraphKitPortCapacityIssue(project, endpoint, ignoredEdgeId) {
    const sanitized = sanitizeGraphKitProject(project);
    const normalizedEndpoint = normalizeGraphKitEndpoint(endpoint, '');
    const parsed = parseGraphKitEndpoint(normalizedEndpoint);
    const port = resolveGraphKitPort(sanitized, parsed);
    if (!normalizedEndpoint || !port || port.multiple) return '';
    const existingConnection = sanitized.graph.edges.find(edge => {
        if (ignoredEdgeId && edge.id === ignoredEdgeId) return false;
        return edge.from === normalizedEndpoint || edge.to === normalizedEndpoint;
    });
    return existingConnection ? `port ${normalizedEndpoint} allows only one connection.` : '';
}

function getGraphKitPortConnectionCapacityIssue(project, fromEndpoint, toEndpoint, ignoredEdgeId) {
    return getGraphKitPortCapacityIssue(project, fromEndpoint, ignoredEdgeId)
        || getGraphKitPortCapacityIssue(project, toEndpoint, ignoredEdgeId);
}

function getGraphKitCanonicalXmlProject(project) {
    const sanitized = sanitizeGraphKitProject(project);
    return {
        ...sanitized,
        nodeTypes: sortGraphKitXmlItems(sanitized.nodeTypes, item => item.id).map(type => ({ ...type })),
        graphs: sortGraphKitXmlItems(sanitized.graphs, graph => graph.id).map(getGraphKitCanonicalXmlGraph),
    };
}

function getGraphKitCanonicalXmlGraph(graph) {
    return {
        ...graph,
        blackboard: sortGraphKitXmlItems(graph.blackboard, item => item.section, item => item.name).map(item => ({ ...item })),
        nodes: sortGraphKitXmlItems(graph.nodes, node => node.id).map(node => ({
            ...node,
            fields: getGraphKitCanonicalXmlFields(node.fields),
        })),
        edges: sortGraphKitXmlItems(graph.edges, edge => edge.id, edge => edge.from, edge => edge.to).map(edge => ({ ...edge })),
        placemats: sortGraphKitXmlItems(graph.placemats, item => normalizeGraphKitXmlSortNumber(item.order), item => item.id).map(item => ({
            ...item,
            nodeIds: sortGraphKitXmlTextValues(item.nodeIds),
        })),
        notes: sortGraphKitXmlItems(graph.notes, item => item.id).map(item => ({ ...item })),
    };
}

function getGraphKitCanonicalXmlFields(fields) {
    const entries = Object.entries(fields || {}).sort((left, right) => compareGraphKitXmlSortValue(left[0], right[0]));
    return Object.fromEntries(entries);
}

function sortGraphKitXmlItems(items, ...selectors) {
    return [...(Array.isArray(items) ? items : [])].sort((left, right) => {
        for (const selector of selectors) {
            const result = compareGraphKitXmlSortValue(selector(left), selector(right));
            if (result !== 0) return result;
        }
        return 0;
    });
}

function sortGraphKitXmlTextValues(values) {
    return [...(Array.isArray(values) ? values : [])].sort(compareGraphKitXmlSortValue);
}

function compareGraphKitXmlSortValue(left, right) {
    const leftType = typeof left;
    const rightType = typeof right;
    if (leftType === 'number' || rightType === 'number') {
        const leftNumber = normalizeGraphKitXmlSortNumber(left);
        const rightNumber = normalizeGraphKitXmlSortNumber(right);
        if (leftNumber !== rightNumber) return leftNumber - rightNumber;
    }
    const leftText = String(left ?? '');
    const rightText = String(right ?? '');
    if (leftText < rightText) return -1;
    if (leftText > rightText) return 1;
    return 0;
}

function normalizeGraphKitXmlSortNumber(value) {
    const number = Number(value);
    return Number.isFinite(number) ? number : 0;
}

function serializeGraphKitXml(project) {
    const sanitized = getGraphKitCanonicalXmlProject(project);
    const lines = [
        `<?xml version="1.0" encoding="utf-8"?>`,
        `<graphProject version="${escapeGraphKitXmlAttribute(sanitized.version)}">`,
        `  <nodeTypes>`,
    ];

    sanitized.nodeTypes.forEach(type => {
        lines.push(`    <nodeType id="${escapeGraphKitXmlAttribute(type.id)}" title="${escapeGraphKitXmlAttribute(type.title)}" category="${escapeGraphKitXmlAttribute(type.category)}" handler="${escapeGraphKitXmlAttribute(type.handlerId)}" color="${escapeGraphKitXmlAttribute(type.color)}">`);
        lines.push(`      <ports>`);
        type.ports.forEach(port => {
            lines.push(`        <port id="${escapeGraphKitXmlAttribute(port.id)}" title="${escapeGraphKitXmlAttribute(port.title)}" kind="${escapeGraphKitXmlAttribute(port.kind)}" direction="${escapeGraphKitXmlAttribute(port.direction)}" multiple="${escapeGraphKitXmlAttribute(port.multiple ? 'true' : 'false')}" />`);
        });
        lines.push(`      </ports>`);
        lines.push(`      <fields>`);
        type.fields.forEach(field => {
            const refAttr = field.ref ? ` ref="${escapeGraphKitXmlAttribute(field.ref)}"` : '';
            const defaultAttr = field.defaultValue !== undefined && field.defaultValue !== ''
                ? ` defaultValue="${escapeGraphKitXmlAttribute(formatGraphKitFieldValue(field.defaultValue))}"`
                : '';
            const requiredAttr = field.required ? ' required="true"' : '';
            const optionsAttr = field.options?.length ? ` options="${escapeGraphKitXmlAttribute(field.options.join('|'))}"` : '';
            lines.push(`        <field name="${escapeGraphKitXmlAttribute(field.name)}" title="${escapeGraphKitXmlAttribute(field.title)}" type="${escapeGraphKitXmlAttribute(field.type)}"${refAttr}${defaultAttr}${requiredAttr}${optionsAttr} />`);
        });
        lines.push(`      </fields>`);
        lines.push(`    </nodeType>`);
    });

    lines.push(`  </nodeTypes>`);
    lines.push(`  <graphs>`);
    sanitized.graphs.forEach(graph => appendGraphKitXmlGraph(lines, graph));
    lines.push(`  </graphs>`);
    lines.push(`</graphProject>`);
    return lines.join('\n');
}

function appendGraphKitXmlGraph(lines, graph) {
    lines.push(`    <graph id="${escapeGraphKitXmlAttribute(graph.id)}" title="${escapeGraphKitXmlAttribute(graph.title)}" type="${escapeGraphKitXmlAttribute(graph.kind)}" kind="${escapeGraphKitXmlAttribute(graph.kind)}">`);
    lines.push(`      <blackboard>`);
    graph.blackboard.forEach(item => {
        lines.push(`        <var name="${escapeGraphKitXmlAttribute(item.name)}" type="${escapeGraphKitXmlAttribute(item.type)}" section="${escapeGraphKitXmlAttribute(item.section)}">${escapeGraphKitXmlText(item.defaultValue)}</var>`);
    });
    lines.push(`      </blackboard>`);
    lines.push(`      <nodes>`);
    graph.nodes.forEach(node => {
        const collapsedAttr = node.collapsed ? ` collapsed="true"` : '';
        lines.push(`        <node id="${escapeGraphKitXmlAttribute(node.id)}" type="${escapeGraphKitXmlAttribute(node.type)}" x="${escapeGraphKitXmlAttribute(node.x)}" y="${escapeGraphKitXmlAttribute(node.y)}"${collapsedAttr}>`);
        Object.entries(node.fields || {}).forEach(([name, value]) => {
            lines.push(`          <field name="${escapeGraphKitXmlAttribute(name)}">${escapeGraphKitXmlText(formatGraphKitFieldValue(value))}</field>`);
        });
        lines.push(`        </node>`);
    });
    lines.push(`      </nodes>`);
    lines.push(`      <edges>`);
    graph.edges.forEach(edge => {
        const labelAttr = edge.label ? ` label="${escapeGraphKitXmlAttribute(edge.label)}"` : '';
        const conditionAttr = edge.condition ? ` condition="${escapeGraphKitXmlAttribute(edge.condition)}"` : '';
        const priorityAttr = edge.priority ? ` priority="${escapeGraphKitXmlAttribute(edge.priority)}"` : '';
        lines.push(`        <edge id="${escapeGraphKitXmlAttribute(edge.id)}" from="${escapeGraphKitXmlAttribute(edge.from)}" to="${escapeGraphKitXmlAttribute(edge.to)}"${labelAttr}${conditionAttr}${priorityAttr} />`);
    });
    lines.push(`      </edges>`);
    lines.push(`      <placemats>`);
    graph.placemats.forEach(item => {
        const orderAttr = ` order="${escapeGraphKitXmlAttribute(item.order)}"`;
        const lockedAttr = item.locked ? ` locked="true"` : '';
        const collapsedAttr = item.collapsed ? ` collapsed="true"` : '';
        lines.push(`        <placemat id="${escapeGraphKitXmlAttribute(item.id)}" title="${escapeGraphKitXmlAttribute(item.title)}" x="${escapeGraphKitXmlAttribute(item.x)}" y="${escapeGraphKitXmlAttribute(item.y)}" width="${escapeGraphKitXmlAttribute(item.width)}" height="${escapeGraphKitXmlAttribute(item.height)}" color="${escapeGraphKitXmlAttribute(item.color)}"${orderAttr}${lockedAttr}${collapsedAttr}>`);
        item.nodeIds.forEach(nodeId => {
            lines.push(`          <member node="${escapeGraphKitXmlAttribute(nodeId)}" />`);
        });
        lines.push(`        </placemat>`);
    });
    lines.push(`      </placemats>`);
    lines.push(`      <notes>`);
    graph.notes.forEach(item => {
        lines.push(`        <note id="${escapeGraphKitXmlAttribute(item.id)}" title="${escapeGraphKitXmlAttribute(item.title)}" x="${escapeGraphKitXmlAttribute(item.x)}" y="${escapeGraphKitXmlAttribute(item.y)}" width="${escapeGraphKitXmlAttribute(item.width)}" height="${escapeGraphKitXmlAttribute(item.height)}" color="${escapeGraphKitXmlAttribute(item.color)}">${escapeGraphKitXmlText(item.text)}</note>`);
    });
    lines.push(`      </notes>`);
    lines.push(`    </graph>`);
}

function getGraphKitRuntimeContract(project) {
    const sanitized = sanitizeGraphKitProject(project);
    const graphById = new Map(sanitized.graphs.map(graph => [graph.id, graph]));
    const nodeTypes = sanitized.nodeTypes.map(type => makeGraphKitRuntimeNodeTypeContract(type));
    const handlers = nodeTypes
        .filter(type => type.handlerId)
        .map(type => ({
            handlerId: type.handlerId,
            nodeTypeId: type.id,
            fieldNames: type.fields.map(field => field.name),
        }));
    const graphs = sanitized.graphs.map(graph => makeGraphKitRuntimeGraphContract(graph, sanitized.nodeTypes, graphById));
    const portalRoutes = graphs.flatMap(graph => graph.portalRoutes);
    return {
        version: sanitized.version,
        graphClass: 'GraphDefinition',
        nodeClass: 'GraphNodeDefinition',
        edgeClass: 'GraphEdgeDefinition',
        placematClass: 'GraphPlacematDefinition',
        noteClass: 'GraphNoteDefinition',
        registryClass: 'GraphNodeTypeRegistry',
        handlerInterface: 'IGraphNodeHandler<TNodeData>',
        nodeTypes,
        handlers,
        handlerIds: sanitized.nodeTypes.map(type => type.handlerId).filter(Boolean),
        graphCount: sanitized.graphs.length,
        nodeTypeCount: sanitized.nodeTypes.length,
        handlerCount: handlers.length,
        graphs,
        portalRoutes,
    };
}

function makeGraphKitRuntimeNodeTypeContract(type) {
    return {
        id: type.id,
        title: type.title,
        category: type.category,
        handlerId: type.handlerId,
        inputPorts: type.ports.filter(port => port.direction === 'input').map(port => port.id),
        outputPorts: type.ports.filter(port => port.direction === 'output').map(port => port.id),
        ports: type.ports.map(port => makeGraphKitRuntimePortContract(port)),
        fields: type.fields.map(field => ({
            name: field.name,
            type: field.type,
            ref: field.ref,
            defaultValue: formatGraphKitFieldValue(field.defaultValue),
            required: field.required,
            options: [...(field.options || [])],
        })),
    };
}

function makeGraphKitRuntimePortContract(port) {
    return {
        id: port.id,
        title: port.title,
        kind: port.kind,
        direction: port.direction,
        multiple: port.multiple,
    };
}

function makeGraphKitRuntimeGraphContract(graph, nodeTypes, graphById) {
    const typeById = new Map(nodeTypes.map(type => [type.id, type]));
    const nodes = graph.nodes.map(node => makeGraphKitRuntimeNodeContract(node, typeById.get(node.type)));
    const edges = graph.edges.map(edge => {
        const from = parseGraphKitEndpoint(edge.from);
        const to = parseGraphKitEndpoint(edge.to);
        return {
            id: edge.id,
            from: edge.from,
            to: edge.to,
            fromNodeId: from.nodeId,
            fromPortId: from.portId,
            toNodeId: to.nodeId,
            toPortId: to.portId,
            label: edge.label,
            condition: edge.condition,
            priority: edge.priority,
        };
    });
    const portalRoutes = makeGraphKitRuntimePortalRoutes(graph, graphById);
    return {
        id: graph.id,
        title: graph.title,
        kind: graph.kind,
        nodeCount: nodes.length,
        edgeCount: edges.length,
        blackboard: graph.blackboard.map(item => ({
            name: item.name,
            type: item.type,
            section: item.section,
            defaultValue: formatGraphKitFieldValue(item.defaultValue),
        })),
        nodes,
        edges,
        portalRoutes,
    };
}

function makeGraphKitRuntimeNodeContract(node, nodeType) {
    return {
        id: node.id,
        type: node.type,
        handlerId: nodeType?.handlerId || '',
        fieldNames: Object.keys(node.fields || {}),
        inputPorts: (nodeType?.ports || []).filter(port => port.direction === 'input').map(port => port.id),
        outputPorts: (nodeType?.ports || []).filter(port => port.direction === 'output').map(port => port.id),
        ports: (nodeType?.ports || []).map(port => makeGraphKitRuntimePortContract(port)),
    };
}

function makeGraphKitRuntimePortalRoutes(graph, graphById) {
    return graph.nodes
        .filter(node => node.type === 'graph.portal')
        .map(node => {
            const portalId = normalizeGraphKitId(node.fields?.portalId, '');
            const targetGraph = normalizeGraphKitId(node.fields?.targetGraph, '');
            const targetPortal = normalizeGraphKitId(node.fields?.targetPortal, '');
            const targetGraphInstance = graphById.get(targetGraph);
            const targetNode = targetGraphInstance
                ? targetGraphInstance.nodes.find(candidate => (
                    candidate.type === 'graph.portal'
                    && normalizeGraphKitId(candidate.fields?.portalId, '') === targetPortal
                ))
                : null;
            return {
                graphId: graph.id,
                nodeId: node.id,
                portalId,
                targetGraph,
                targetPortal,
                targetNodeId: targetNode?.id || '',
                valid: Boolean(targetNode),
            };
        });
}

function parseGraphKitEndpoint(endpoint) {
    const text = normalizeGraphKitEndpoint(endpoint, '');
    const dot = text.lastIndexOf('.');
    if (dot <= 0 || dot >= text.length - 1) return { nodeId: '', portId: '' };
    return {
        nodeId: text.slice(0, dot),
        portId: text.slice(dot + 1),
    };
}

function normalizeGraphKitEndpoint(value, fallback) {
    const text = normalizeGraphKitText(value, fallback);
    return /^[A-Za-z0-9_.-]+\.[A-Za-z0-9_.-]+$/.test(text) ? text : fallback;
}

function normalizeGraphKitId(value, fallback) {
    const text = normalizeGraphKitText(value, fallback);
    const sanitized = text.replace(/[^A-Za-z0-9_.-]/g, '_').replace(/^_+|_+$/g, '');
    return sanitized || fallback;
}

function normalizeGraphKitText(value, fallback) {
    const text = String(value ?? '').trim();
    return text || fallback;
}

function normalizeGraphKitNumber(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) ? Math.round(number) : fallback;
}

function normalizeGraphKitBoolean(value, fallback) {
    if (typeof value === 'boolean') return value;
    const text = String(value ?? '').trim().toLowerCase();
    if (text === 'true' || text === '1' || text === 'yes' || text === 'on') return true;
    if (text === 'false' || text === '0' || text === 'no' || text === 'off') return false;
    return fallback;
}

function normalizeGraphKitBlackboardSection(value) {
    return normalizeGraphKitText(value, GRAPHKIT_DEFAULT_BLACKBOARD_SECTION);
}

function normalizeGraphKitColor(value) {
    const text = String(value ?? '').trim();
    return /^#[0-9a-fA-F]{6}$/.test(text) ? text : '#4f5f73';
}

function makeUniqueGraphKitId(baseId, usedIds) {
    let id = baseId;
    let index = 2;
    while (usedIds.has(id)) {
        id = `${baseId}_${index}`;
        index += 1;
    }
    return id;
}

function formatGraphKitFieldValue(value) {
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

function normalizeGraphKitNodeFieldValue(field, value) {
    const type = getGraphKitNormalizedFieldType(field);
    if (type === 'bool' || type === 'boolean') return normalizeGraphKitBoolean(value, false);
    if (type === 'int' || type === 'integer' || type === 'long') {
        const text = String(value ?? '').trim();
        if (!text) return '';
        const number = Number(text);
        return Number.isFinite(number) ? Math.round(number) : '';
    }
    if (type === 'float' || type === 'double' || type === 'number') {
        const text = String(value ?? '').trim();
        if (!text) return '';
        const number = Number(text);
        return Number.isFinite(number) ? number : '';
    }
    return formatGraphKitFieldValue(value);
}

function getGraphKitNormalizedFieldType(field) {
    return normalizeGraphKitText(field?.type, 'string').toLowerCase();
}

function escapeGraphKitXmlAttribute(value) {
    return escapeGraphKitXmlText(value).replace(/"/g, '&quot;').replace(/'/g, '&apos;');
}

function escapeGraphKitXmlText(value) {
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;');
}

const graphKitModelApi = {
    GRAPHKIT_DEFAULT_PROJECT,
    GRAPHKIT_DEFAULT_NODE_TYPES,
    GRAPHKIT_DEFAULT_BLACKBOARD_SECTION,
    GRAPHKIT_PROJECT_VERSION,
    getGraphKitPortCapacityIssue,
    getGraphKitPortConnectionCapacityIssue,
    migrateGraphKitProjectVersion,
    getGraphKitRuntimeContract,
    parseGraphKitEndpoint,
    normalizeGraphKitNodeFieldValue,
    normalizeGraphKitBlackboardSection,
    normalizeGraphKitId,
    sanitizeGraphKitProject,
    serializeGraphKitXml,
    validateGraphKitProject,
};

if (typeof module !== 'undefined' && module.exports) {
    module.exports = graphKitModelApi;
}

if (typeof window !== 'undefined') {
    Object.assign(window, graphKitModelApi);
}
