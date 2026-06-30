// pages/graphkit-issues.js
// GraphKit validation issue overlay and click-to-locate helpers.
function getGraphKitIssueCounts(report) {
    const errors = Array.isArray(report?.errors) ? report.errors.length : 0;
    const warnings = Array.isArray(report?.warnings) ? report.warnings.length : 0;
    return { errors, warnings, total: errors + warnings };
}

function getGraphKitIssues(report) {
    const errors = Array.isArray(report?.errors) ? report.errors : [];
    const warnings = Array.isArray(report?.warnings) ? report.warnings : [];
    return [
        ...errors.map((message, index) => makeGraphKitIssue('error', message, index)),
        ...warnings.map((message, index) => makeGraphKitIssue('warning', message, index)),
    ];
}

function makeGraphKitIssue(level, message, index) {
    return {
        id: `${level}_${index}`,
        level,
        message: String(message || ''),
        target: getGraphKitIssueTarget(message),
    };
}

function renderGraphKitIssuesOverlay(report) {
    if (!graphKitState.issuesOpen) return '';
    const issues = getGraphKitIssues(report);
    const counts = getGraphKitIssueCounts(report);
    const rows = issues.length
        ? issues.map((issue, index) => renderGraphKitIssueRow(issue, index)).join('')
        : `<div class="graphkit-issue-empty">当前图没有校验问题。</div>`;
    return `<aside class="graphkit-issues-overlay" data-graphkit-issues>
        <div class="graphkit-issues-overlay__head">
            <div>
                <span>Issues</span>
                <strong>${escapeHtml(`${counts.errors} errors · ${counts.warnings} warnings`)}</strong>
            </div>
            <button class="graphkit-panel-icon" type="button" data-graphkit-action="toggle-issues" title="关闭 Issues" aria-label="关闭 Issues">×</button>
        </div>
        <div class="graphkit-issue-list">${rows}</div>
    </aside>`;
}

function renderGraphKitIssueRow(issue, index) {
    const targetLabel = issue.target.kind
        ? `${issue.target.graphId ? `${issue.target.graphId}/` : ''}${issue.target.kind}:${issue.target.id}`
        : (issue.target.graphId ? `graph:${issue.target.graphId}` : 'graph');
    return `<button class="graphkit-issue-row graphkit-issue-row--${escapeHtml(issue.level)}" type="button" data-graphkit-issue="${escapeHtml(index)}">
        <span>${escapeHtml(issue.level)}</span>
        <strong>${escapeHtml(issue.message)}</strong>
        <em>${escapeHtml(targetLabel)}</em>
    </button>`;
}

function bindGraphKitIssuesOverlay() {
    $pageBody.querySelectorAll('[data-graphkit-issue]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => focusGraphKitIssue(Number(button.dataset.graphkitIssue)));
    });
}

function toggleGraphKitIssuesOverlay() {
    graphKitState.issuesOpen = !graphKitState.issuesOpen;
    renderGraphKitWorkbench();
}

function focusGraphKitIssue(index) {
    const report = validateGraphKitProject(graphKitProject);
    const issue = getGraphKitIssues(report)[index];
    if (!issue) return false;
    const focused = focusGraphKitIssueTarget(issue.target);
    setGraphKitNotice(focused ? 'info' : 'warning', focused ? `已定位问题：${issue.message}` : `无法定位问题：${issue.message}`);
    renderGraphKitWorkbench();
    return focused;
}

function focusGraphKitIssueTarget(target) {
    if (!target) return false;
    if (target.graphId && !focusGraphKitIssueGraph(target.graphId)) return false;
    if (!target.kind) return Boolean(target.graphId);
    if (target.kind === 'edge') return focusGraphKitIssueEdge(target.id);
    if (target.kind === 'node') return focusGraphKitIssueNode(target.id);
    if (target.kind === 'nodeType') return focusGraphKitIssueNodeType(target.id);
    return false;
}

function focusGraphKitIssueGraph(graphId) {
    const targetId = normalizeGraphKitId(graphId, '');
    if (!targetId) return false;
    if (graphKitProject.graph.id === targetId) return true;
    const targetGraph = (graphKitProject.graphs || []).find(graph => graph.id === targetId);
    if (!targetGraph || typeof selectGraphKitGraphById !== 'function') return false;
    selectGraphKitGraphById(graphId);
    return graphKitProject.graph.id === targetId;
}

function focusGraphKitIssueEdge(edgeId) {
    const edge = graphKitProject.graph.edges.find(candidate => candidate.id === edgeId);
    if (!edge) return false;
    graphKitState.selectedEdgeId = edge.id;
    graphKitState.selectedNodeId = '';
    graphKitState.selectedNodeIds = [];
    graphKitState.selectedBlackboardName = '';
    graphKitState.pendingPortEndpoint = '';
    const from = getGraphKitEndpointPosition(edge.from, 'output');
    const to = getGraphKitEndpointPosition(edge.to, 'input');
    if (from && to) focusGraphKitCanvasPoint((from.x + to.x) * 0.5, (from.y + to.y) * 0.5);
    return true;
}

function focusGraphKitIssueNode(nodeId) {
    const node = graphKitProject.graph.nodes.find(candidate => candidate.id === nodeId);
    if (!node) return false;
    setGraphKitSelectedNodeIds([node.id], node.id);
    const type = getGraphKitNodeType(node.type);
    const position = getGraphKitNodePosition(node);
    focusGraphKitCanvasPoint(position.x + GRAPHKIT_NODE_WIDTH * 0.5, position.y + getGraphKitNodeHeight(type, node) * 0.5);
    return true;
}

function focusGraphKitIssueNodeType(typeId) {
    const node = graphKitProject.graph.nodes.find(candidate => candidate.type === typeId);
    return node ? focusGraphKitIssueNode(node.id) : false;
}

function focusGraphKitCanvasPoint(canvasX, canvasY) {
    const viewport = getGraphKitViewport();
    updateGraphKitViewport({
        offsetX: clampGraphKitViewportOffset(GRAPHKIT_CANVAS_WIDTH * 0.5 - canvasX * viewport.scale, GRAPHKIT_CANVAS_WIDTH, viewport.scale),
        offsetY: clampGraphKitViewportOffset(GRAPHKIT_CANVAS_HEIGHT * 0.5 - canvasY * viewport.scale, GRAPHKIT_CANVAS_HEIGHT, viewport.scale),
    });
}

function getGraphKitIssueTarget(message) {
    const text = String(message || '');
    const graphId = matchGraphKitIssueToken(text, /\bgraph\s+([A-Za-z0-9_.-]+)/);
    const edgeId = matchGraphKitIssueToken(text, /\bedge\s+([A-Za-z0-9_.-]+)/);
    if (edgeId) return { kind: 'edge', id: edgeId, graphId };
    const nodeId = matchGraphKitIssueToken(text, /\bnode\s+([A-Za-z0-9_.-]+)/);
    if (nodeId) return { kind: 'node', id: nodeId, graphId };
    const nodeTypeId = matchGraphKitIssueToken(text, /\bnodeType\s+([A-Za-z0-9_.-]+)/);
    if (nodeTypeId) return { kind: 'nodeType', id: nodeTypeId, graphId };
    return { kind: '', id: '', graphId };
}

function matchGraphKitIssueToken(text, pattern) {
    const match = String(text || '').match(pattern);
    return match ? match[1] : '';
}
