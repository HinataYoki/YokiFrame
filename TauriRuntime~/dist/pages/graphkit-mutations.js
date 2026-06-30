// pages/graphkit-mutations.js
// GraphKit transactional mutations for nodes, edges, fields and Blackboard variables.
function selectFirstGraphKitNodeOfType(typeId) {
    const node = graphKitProject.graph.nodes.find(candidate => candidate.type === typeId);
    if (node) setGraphKitSelectedNodeIds([node.id], node.id);
    renderGraphKitWorkbench();
}

function commitGraphKitMutation(label, mutator, options = {}) {
    if (typeof mutator !== 'function') return false;
    const beforeProject = cloneGraphKitProject(graphKitProject);
    const nextProject = mutator(beforeProject);
    graphKitProject = sanitizeGraphKitProject(nextProject || graphKitProject);
    pushGraphKitHistory(beforeProject, graphKitProject, label);
    graphKitState.redoStack = [];
    if (options.selectedNodeId !== undefined) {
        graphKitState.selectedNodeId = options.selectedNodeId;
        graphKitState.selectedNodeIds = options.selectedNodeId ? [options.selectedNodeId] : [];
        if (options.selectedEdgeId === undefined) graphKitState.selectedEdgeId = '';
        if (options.selectedBlackboardName === undefined) graphKitState.selectedBlackboardName = '';
        if (options.selectedNoteId === undefined) graphKitState.selectedNoteId = '';
        if (options.selectedPlacematId === undefined) graphKitState.selectedPlacematId = '';
        if (options.selectedNodeTypeId === undefined) graphKitState.selectedNodeTypeId = '';
    }
    if (options.selectedNodeIds !== undefined) {
        setGraphKitSelectedNodeIds(options.selectedNodeIds, options.selectedNodeId || options.selectedNodeIds[0] || '');
    }
    if (options.selectedEdgeId !== undefined) {
        graphKitState.selectedEdgeId = options.selectedEdgeId;
        if (options.selectedNodeId === undefined) graphKitState.selectedNodeId = '';
        if (options.selectedNodeIds === undefined) graphKitState.selectedNodeIds = [];
        if (options.selectedBlackboardName === undefined) graphKitState.selectedBlackboardName = '';
        if (options.selectedNoteId === undefined) graphKitState.selectedNoteId = '';
        if (options.selectedPlacematId === undefined) graphKitState.selectedPlacematId = '';
        if (options.selectedNodeTypeId === undefined) graphKitState.selectedNodeTypeId = '';
    }
    if (options.selectedBlackboardName !== undefined) {
        graphKitState.selectedBlackboardName = options.selectedBlackboardName;
        if (options.selectedNodeId === undefined) graphKitState.selectedNodeId = '';
        if (options.selectedNodeIds === undefined) graphKitState.selectedNodeIds = [];
        if (options.selectedEdgeId === undefined) graphKitState.selectedEdgeId = '';
        if (options.selectedNoteId === undefined) graphKitState.selectedNoteId = '';
        if (options.selectedPlacematId === undefined) graphKitState.selectedPlacematId = '';
        if (options.selectedNodeTypeId === undefined) graphKitState.selectedNodeTypeId = '';
    }
    if (options.selectedNoteId !== undefined) {
        graphKitState.selectedNoteId = options.selectedNoteId;
        graphKitState.selectedPlacematId = '';
        graphKitState.selectedNodeId = '';
        graphKitState.selectedNodeIds = [];
        graphKitState.selectedEdgeId = '';
        graphKitState.selectedBlackboardName = '';
        graphKitState.selectedNodeTypeId = '';
    }
    if (options.selectedPlacematId !== undefined) {
        graphKitState.selectedPlacematId = options.selectedPlacematId;
        graphKitState.selectedNoteId = '';
        graphKitState.selectedNodeId = '';
        graphKitState.selectedNodeIds = [];
        graphKitState.selectedEdgeId = '';
        graphKitState.selectedBlackboardName = '';
        graphKitState.selectedNodeTypeId = '';
    }
    if (options.selectedNodeTypeId !== undefined) {
        graphKitState.selectedNodeTypeId = options.selectedNodeTypeId;
        graphKitState.selectedNodeId = '';
        graphKitState.selectedNodeIds = [];
        graphKitState.selectedEdgeId = '';
        graphKitState.selectedBlackboardName = '';
        graphKitState.selectedNoteId = '';
        graphKitState.selectedPlacematId = '';
    }
    if (options.clearPendingPort) graphKitState.pendingPortEndpoint = '';
    if (options.noticeLevel || options.noticeText) {
        setGraphKitNotice(options.noticeLevel || 'info', options.noticeText || '');
    }
    persistGraphKitProject();
    renderGraphKitWorkbench();
    return true;
}

function canUndoGraphKit() {
    return graphKitState.undoStack.length > 0;
}

function canRedoGraphKit() {
    return graphKitState.redoStack.length > 0;
}

function undoGraphKitMutation() {
    if (!canUndoGraphKit()) return;
    const entry = graphKitState.undoStack.pop();
    graphKitState.redoStack.push(entry);
    graphKitProject = cloneGraphKitProject(entry.before);
    graphKitState.selectedNodeId = getGraphKitInitialNodeId(graphKitProject);
    graphKitState.selectedNodeIds = [graphKitState.selectedNodeId].filter(Boolean);
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNoteId = '';
    graphKitState.selectedPlacematId = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.pendingPortEndpoint = '';
    setGraphKitNotice('info', `已撤销：${entry.label}`);
    persistGraphKitProject();
    renderGraphKitWorkbench();
}

function redoGraphKitMutation() {
    if (!canRedoGraphKit()) return;
    const entry = graphKitState.redoStack.pop();
    graphKitState.undoStack.push(entry);
    graphKitProject = cloneGraphKitProject(entry.after);
    graphKitState.selectedNodeId = getGraphKitInitialNodeId(graphKitProject);
    graphKitState.selectedNodeIds = [graphKitState.selectedNodeId].filter(Boolean);
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNoteId = '';
    graphKitState.selectedPlacematId = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.pendingPortEndpoint = '';
    setGraphKitNotice('info', `已重做：${entry.label}`);
    persistGraphKitProject();
    renderGraphKitWorkbench();
}

function createGraphKitNode(typeId) {
    const type = getGraphKitNodeType(typeId) || getGraphKitDefaultCreatableNodeType();
    if (!type) return;
    const selectedNode = getGraphKitSelectedNode();
    const newNodeId = makeUniqueGraphKitNodeId(type.id.replace(/\./g, '_'));
    const position = selectedNode
        ? {
            x: normalizeFiniteGraphKitNumber(selectedNode.x, 120) + 120,
            y: normalizeFiniteGraphKitNumber(selectedNode.y, 120) + 40,
        }
        : getGraphKitViewportCenterModelPosition();
    const newNode = {
        id: newNodeId,
        type: type.id,
        x: Math.round(position.x),
        y: Math.round(position.y),
        fields: makeGraphKitDefaultNodeFields(type),
    };
    commitGraphKitMutation('Add node', before => ({
        ...before,
        graph: {
            ...before.graph,
            nodes: [...before.graph.nodes, newNode],
        },
    }), {
        selectedNodeId: newNodeId,
        noticeLevel: 'success',
        noticeText: `已添加节点 ${type.title}。`,
    });
}

function createGraphKitBlackboardReferenceNode(variableName, position) {
    const variable = graphKitProject.graph.blackboard.find(item => item.name === String(variableName || ''));
    const type = getGraphKitNodeType('graph.variable');
    if (!variable || !type) return false;
    const targetPosition = position || getGraphKitViewportCenterModelPosition();
    const fields = {
        ...makeGraphKitDefaultNodeFields(type),
        variableName: variable.name,
        variableType: variable.type,
    };
    const newNodeId = makeUniqueGraphKitNodeId(`var_${variable.name}`);
    const newNode = {
        id: newNodeId,
        type: type.id,
        x: Math.round(normalizeFiniteGraphKitNumber(targetPosition.x, 120)),
        y: Math.round(normalizeFiniteGraphKitNumber(targetPosition.y, 120)),
        fields,
    };
    commitGraphKitMutation('Add blackboard reference node', before => ({
        ...before,
        graph: {
            ...before.graph,
            nodes: [...before.graph.nodes, newNode],
        },
    }), {
        selectedNodeId: newNodeId,
        clearPendingPort: true,
        noticeLevel: 'success',
        noticeText: `已创建 Blackboard 变量引用 ${variable.name}。`,
    });
    return true;
}

function duplicateGraphKitSelectedNode() {
    const selectedNode = getGraphKitSelectedNode();
    if (!selectedNode) return;
    const newNodeId = makeUniqueGraphKitNodeId(`${selectedNode.id}_copy`);
    const clone = {
        ...selectedNode,
        id: newNodeId,
        x: normalizeFiniteGraphKitNumber(selectedNode.x, 120) + 72,
        y: normalizeFiniteGraphKitNumber(selectedNode.y, 120) + 36,
        fields: { ...(selectedNode.fields || {}) },
    };
    commitGraphKitMutation('Duplicate node', before => ({
        ...before,
        graph: {
            ...before.graph,
            nodes: [...before.graph.nodes, clone],
        },
    }), {
        selectedNodeId: newNodeId,
        noticeLevel: 'success',
        noticeText: `已复制节点 ${selectedNode.id}。`,
    });
}

function deleteGraphKitSelectedNode() {
    if (deleteGraphKitSelectedNodes()) return;
    const selectedNode = getGraphKitSelectedNode();
    if (!selectedNode) return;
    const nextSelection = graphKitProject.graph.nodes.find(node => node.id !== selectedNode.id)?.id || '';
    commitGraphKitMutation('Delete node', before => ({
        ...before,
        graph: {
            ...before.graph,
            nodes: before.graph.nodes.filter(node => node.id !== selectedNode.id),
            edges: before.graph.edges.filter(edge => {
                const from = parseGraphKitEndpoint(edge.from);
                const to = parseGraphKitEndpoint(edge.to);
                return from.nodeId !== selectedNode.id && to.nodeId !== selectedNode.id;
            }),
            placemats: before.graph.placemats.map(item => ({
                ...item,
                nodeIds: item.nodeIds.filter(nodeId => nodeId !== selectedNode.id),
            })),
        },
    }), {
        selectedNodeId: nextSelection,
        noticeLevel: 'warning',
        noticeText: `已删除节点 ${selectedNode.id}，并移除相关连线。`,
    });
}

function handleGraphKitPortClick(endpoint) {
    const port = getGraphKitPortDefinition(endpoint);
    if (!port) {
        setGraphKitNotice('warning', '端口不存在，无法创建连线。');
        renderGraphKitWorkbench();
        return;
    }

    if (!graphKitState.pendingPortEndpoint) {
        graphKitState.pendingPortEndpoint = endpoint;
        graphKitState.selectedNodeId = '';
        graphKitState.selectedNodeIds = [];
        graphKitState.selectedEdgeId = '';
        graphKitState.selectedBlackboardName = '';
        graphKitState.selectedNoteId = '';
        graphKitState.selectedPlacematId = '';
        graphKitState.selectedNodeTypeId = '';
        setGraphKitNotice('info', `选择目标端口以连接 ${endpoint}。`);
        renderGraphKitWorkbench();
        return;
    }

    if (graphKitState.pendingPortEndpoint === endpoint) {
        graphKitState.pendingPortEndpoint = '';
        setGraphKitNotice('info', '已取消端口连接。');
        renderGraphKitWorkbench();
        return;
    }

    connectGraphKitPorts(graphKitState.pendingPortEndpoint, endpoint);
}

function connectGraphKitPorts(firstEndpoint, secondEndpoint) {
    const connection = getGraphKitPortConnection(firstEndpoint, secondEndpoint);
    if (!connection.ok) {
        graphKitState.pendingPortEndpoint = '';
        setGraphKitNotice('warning', connection.message || '端口不兼容，无法创建连线。');
        renderGraphKitWorkbench();
        return false;
    }

    const edgeId = makeUniqueGraphKitEdgeId(connection.from, connection.to);
    commitGraphKitMutation('Connect ports', before => ({
        ...before,
        graph: {
            ...before.graph,
            edges: [
                ...before.graph.edges,
                { id: edgeId, from: connection.from, to: connection.to },
            ],
        },
    }), {
        selectedEdgeId: edgeId,
        clearPendingPort: true,
        noticeLevel: 'success',
        noticeText: `已连接 ${connection.from} -> ${connection.to}。`,
    });
    return true;
}

function selectGraphKitEdge(edgeId) {
    const edge = graphKitProject.graph.edges.find(candidate => candidate.id === edgeId);
    if (!edge) return;
    graphKitState.selectedEdgeId = edge.id;
    graphKitState.selectedNodeId = '';
    graphKitState.selectedNodeIds = [];
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNoteId = '';
    graphKitState.selectedPlacematId = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.pendingPortEndpoint = '';
    renderGraphKitWorkbench();
}

function deleteGraphKitSelectedEdge() {
    const selectedEdge = getGraphKitSelectedEdge();
    if (!selectedEdge) return false;
    const edgeId = selectedEdge.id;
    commitGraphKitMutation('Delete edge', before => ({
        ...before,
        graph: {
            ...before.graph,
            edges: before.graph.edges.filter(edge => edge.id !== edgeId),
        },
    }), {
        selectedEdgeId: '',
        selectedNodeId: getGraphKitInitialNodeId(graphKitProject),
        clearPendingPort: true,
        noticeLevel: 'warning',
        noticeText: `已删除连线 ${edgeId}。`,
    });
    return true;
}

function updateGraphKitSelectedEdgeField(field, value) {
    const selectedEdge = getGraphKitSelectedEdge();
    if (!selectedEdge) return false;
    const edgeId = selectedEdge.id;
    const nextValue = field === 'priority'
        ? normalizeGraphKitEdgePriority(value)
        : String(value ?? '').trim();
    commitGraphKitMutation('Edit edge metadata', before => ({
        ...before,
        graph: {
            ...before.graph,
            edges: before.graph.edges.map(edge => (
                edge.id === edgeId ? { ...edge, [field]: nextValue } : edge
            )),
        },
    }), {
        selectedEdgeId: edgeId,
    });
    return true;
}

function normalizeGraphKitEdgePriority(value) {
    const number = Number(value);
    return Number.isFinite(number) ? Math.round(number) : 0;
}

function createGraphKitBlackboardVariable() {
    const name = makeUniqueGraphKitBlackboardName('variable');
    const selectedVariable = getGraphKitSelectedBlackboardVariable();
    const section = normalizeGraphKitBlackboardSection(selectedVariable?.section);
    const variable = { name, type: 'string', section, defaultValue: '' };
    commitGraphKitMutation('Add blackboard variable', before => ({
        ...before,
        graph: {
            ...before.graph,
            blackboard: [...before.graph.blackboard, variable],
        },
    }), {
        selectedBlackboardName: name,
        clearPendingPort: true,
        noticeLevel: 'success',
        noticeText: `已添加 Blackboard 变量 ${name}。`,
    });
}

function selectGraphKitBlackboardVariable(name) {
    const variable = graphKitProject.graph.blackboard.find(item => item.name === name);
    if (!variable) return;
    graphKitState.selectedBlackboardName = variable.name;
    graphKitState.selectedNodeId = '';
    graphKitState.selectedNodeIds = [];
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedNoteId = '';
    graphKitState.selectedPlacematId = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.pendingPortEndpoint = '';
    renderGraphKitWorkbench();
}

function updateGraphKitSelectedBlackboardField(field, value) {
    const selectedName = graphKitState.selectedBlackboardName;
    const selectedVariable = getGraphKitSelectedBlackboardVariable();
    if (!selectedVariable) return;
    const usedNames = new Set(graphKitProject.graph.blackboard.filter(item => item.name !== selectedName).map(item => item.name));
    const nextSelectedName = field === 'name'
        ? makeUniqueGraphKitBlackboardName(value || selectedName, usedNames)
        : selectedName;
    const nextSelectedType = field === 'type'
        ? String(value ?? '').trim() || 'string'
        : selectedVariable.type;
    const nextSelectedSection = field === 'section'
        ? normalizeGraphKitBlackboardSection(value)
        : selectedVariable.section;
    commitGraphKitMutation('Edit blackboard variable', before => {
        const nextProject = {
            ...before,
            graph: {
                ...before.graph,
                blackboard: before.graph.blackboard.map(item => {
                    if (item.name !== selectedName) return item;
                    if (field === 'name') {
                        return { ...item, name: nextSelectedName };
                    }
                    if (field === 'type') return { ...item, type: nextSelectedType };
                    if (field === 'section') return { ...item, section: nextSelectedSection };
                    if (field === 'defaultValue') return { ...item, defaultValue: value };
                    return item;
                }),
            },
        };
        if (field === 'name' || field === 'type') {
            return syncGraphKitBlackboardReferenceNodes(nextProject, selectedName, nextSelectedName, nextSelectedType);
        }
        return nextProject;
    }, {
        selectedBlackboardName: nextSelectedName,
        clearPendingPort: true,
    });
}

function syncGraphKitBlackboardReferenceNodes(project, previousName, nextSelectedName, nextSelectedType) {
    return {
        ...project,
        graph: {
            ...project.graph,
            nodes: project.graph.nodes.map(node => {
                if (node.type !== 'graph.variable') return node;
                if (node.fields?.variableName !== previousName) return node;
                return {
                    ...node,
                    fields: {
                        ...(node.fields || {}),
                        variableName: nextSelectedName,
                        variableType: nextSelectedType,
                    },
                };
            }),
        },
    };
}

function deleteGraphKitSelectedBlackboardVariable() {
    const selectedVariable = getGraphKitSelectedBlackboardVariable();
    if (!selectedVariable) return false;
    const name = selectedVariable.name;
    const nextSelection = graphKitProject.graph.blackboard.find(item => item.name !== name)?.name || '';
    commitGraphKitMutation('Delete blackboard variable', before => ({
        ...before,
        graph: {
            ...before.graph,
            blackboard: before.graph.blackboard.filter(item => item.name !== name),
        },
    }), {
        selectedBlackboardName: nextSelection,
        clearPendingPort: true,
        noticeLevel: 'warning',
        noticeText: `已删除 Blackboard 变量 ${name}。`,
    });
    return true;
}

function updateGraphKitSelectedNodeField(field, value) {
    const selectedId = graphKitState.selectedNodeId;
    const selectedNode = getGraphKitSelectedNode();
    const selectedType = getGraphKitNodeType(selectedNode?.type);
    const fieldDefinition = (selectedType?.fields || []).find(item => item.name === field);
    const nextValue = normalizeGraphKitNodeFieldValue(fieldDefinition, value);
    commitGraphKitMutation('Edit node field', before => ({
        ...before,
        graph: {
            ...before.graph,
            nodes: before.graph.nodes.map(node => {
                if (node.id !== selectedId) return node;
                return {
                    ...node,
                    fields: {
                        ...(node.fields || {}),
                        [field]: nextValue,
                    },
                };
            }),
        },
    }), { selectedNodeId: selectedId });
}

function repairGraphKitSelectedPortalPair() {
    const selectedId = graphKitState.selectedNodeId;
    if (!selectedId || typeof repairGraphKitProjectPortalPair !== 'function') return false;
    const result = repairGraphKitProjectPortalPair(graphKitProject, selectedId);
    if (!result.repaired) {
        const message = result.reason === 'already-linked'
            ? `Portal Pair 已存在：${result.graphId}.${result.nodeId}。`
            : '无法修复 Portal Pair，请先设置目标图和目标 Portal。';
        setGraphKitNotice(result.reason === 'already-linked' ? 'info' : 'warning', message);
        renderGraphKitWorkbench();
        return false;
    }
    commitGraphKitMutation('Repair portal pair', () => result.project, {
        selectedNodeId: selectedId,
        noticeLevel: 'success',
        noticeText: `已在 ${result.graphId} 创建 Portal ${result.nodeId}。`,
    });
    return true;
}

function updateGraphKitProjectNodeCollapsed(project, nodeId, collapsed) {
    const sanitized = sanitizeGraphKitProject(project);
    return sanitizeGraphKitProject({
        ...sanitized,
        graph: {
            ...sanitized.graph,
            nodes: sanitized.graph.nodes.map(node => (
                node.id === nodeId ? { ...node, collapsed: Boolean(collapsed) } : node
            )),
        },
    });
}

function toggleGraphKitSelectedNodeCollapsed(nodeId) {
    const targetId = nodeId || graphKitState.selectedNodeId;
    const node = graphKitProject.graph.nodes.find(candidate => candidate.id === targetId);
    if (!node) return false;
    const collapsed = !node.collapsed;
    commitGraphKitMutation(collapsed ? 'Collapse node' : 'Expand node', before => (
        updateGraphKitProjectNodeCollapsed(before, targetId, collapsed)
    ), {
        selectedNodeId: targetId,
        noticeLevel: 'info',
        noticeText: `${collapsed ? '已折叠' : '已展开'}节点 ${targetId}。`,
    });
    return true;
}

function updateGraphKitNodePosition(nodeId, x, y, persist = true) {
    graphKitProject = {
        ...graphKitProject,
        graph: {
            ...graphKitProject.graph,
            nodes: graphKitProject.graph.nodes.map(node => {
                if (node.id !== nodeId) return node;
                return {
                    ...node,
                    x: roundGraphKitTransformNumber(x),
                    y: roundGraphKitTransformNumber(y),
                };
            }),
        },
    };
    if (persist) persistGraphKitProject();
}

function applyGraphKitNodeDrag(nodeId) {
    const node = graphKitProject.graph.nodes.find(candidate => candidate.id === nodeId);
    if (!node) return;
    const nodeElement = $pageBody.querySelector(`[data-graphkit-node="${escapeGraphKitSelectorValue(nodeId)}"]`);
    if (nodeElement) nodeElement.setAttribute('transform', getGraphKitNodeTransform(node));
    const miniMapNode = $pageBody.querySelector(`[data-graphkit-minimap-node="${escapeGraphKitSelectorValue(nodeId)}"]`);
    if (miniMapNode) {
        const position = getGraphKitNodePosition(node);
        miniMapNode.setAttribute('x', String(position.x));
        miniMapNode.setAttribute('y', String(position.y));
    }
    graphKitProject.graph.edges.forEach(edge => {
        const edgeElement = $pageBody.querySelector(`[data-graphkit-edge="${escapeGraphKitSelectorValue(edge.id)}"]`);
        const path = getGraphKitEdgePath(edge);
        if (edgeElement && path) edgeElement.setAttribute('d', path);
    });
}

function toggleGraphKitPanel(panel) {
    const panels = getGraphKitPanels();
    if (panel === 'blackboard') {
        graphKitState.panels = {
            ...panels,
            blackboardCollapsed: !panels.blackboardCollapsed,
        };
    } else if (panel === 'inspector') {
        graphKitState.panels = {
            ...panels,
            inspectorCollapsed: !panels.inspectorCollapsed,
        };
    } else {
        return;
    }
    renderGraphKitWorkbench();
}

async function copyGraphKitXml() {
    const xml = serializeGraphKitXml(graphKitProject);
    try {
        await navigator.clipboard?.writeText?.(xml);
        setGraphKitNotice('success', 'XML Graph 数据已复制到剪贴板。');
    } catch (_) {
        setGraphKitNotice('warning', '剪贴板不可用，请直接复制 XML 预览内容。');
    }
    renderGraphKitWorkbench();
}

function importGraphKitXml() {
    const input = $pageBody.querySelector('[data-graphkit-xml-input]');
    const xmlText = input ? input.value : '';
    let importedProject;
    try {
        importedProject = parseGraphKitXml(xmlText);
    } catch (error) {
        setGraphKitNotice('error', `XML 导入失败：${error.message || error}`);
        renderGraphKitWorkbench();
        return false;
    }

    const report = validateGraphKitProject(importedProject);
    if (report.errors.length) {
        setGraphKitNotice('error', `XML 导入失败：${report.errors.join(' / ')}`);
        renderGraphKitWorkbench();
        return false;
    }

    commitGraphKitMutation('Import XML', () => importedProject, {
        selectedNodeId: getGraphKitInitialNodeId(importedProject),
        clearPendingPort: true,
        noticeLevel: 'success',
        noticeText: `已导入 XML Graph：${importedProject.graph.id}。`,
    });
    return true;
}

function downloadGraphKitXml() {
    const xml = serializeGraphKitXml(graphKitProject);
    const fileName = `${normalizeGraphKitId(graphKitProject.graph.id, 'graph')}.xml`;
    if (typeof Blob === 'undefined' || !window.URL?.createObjectURL) {
        setGraphKitNotice('warning', '当前环境不支持直接下载，请复制 XML。');
        renderGraphKitWorkbench();
        return false;
    }

    const blob = new Blob([xml], { type: 'application/xml;charset=utf-8' });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = fileName;
    document.body.appendChild(link);
    link.click();
    link.remove();
    window.URL.revokeObjectURL(url);
    setGraphKitNotice('success', `已生成下载文件 ${fileName}。`);
    renderGraphKitWorkbench();
    return true;
}
