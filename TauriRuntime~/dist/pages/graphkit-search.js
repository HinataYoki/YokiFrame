// pages/graphkit-search.js
// GraphKit graph-wide search overlay and click-to-locate helpers.
const GRAPHKIT_SEARCH_RESULT_LIMIT = 48;
const GRAPHKIT_SEARCH_SCOPE_CURRENT = 'current';
const GRAPHKIT_SEARCH_SCOPE_PROJECT = 'project';

function getClosedGraphKitSearchOverlay() {
    return {
        open: false,
        query: '',
        scope: GRAPHKIT_SEARCH_SCOPE_CURRENT,
        activeIndex: 0,
    };
}

function normalizeGraphKitSearchOverlay(overlay) {
    const source = overlay && typeof overlay === 'object' ? overlay : {};
    return {
        open: Boolean(source.open),
        query: String(source.query || ''),
        scope: normalizeGraphKitSearchScope(source.scope),
        activeIndex: normalizeGraphKitSearchActiveIndex(source.activeIndex),
    };
}

function toggleGraphKitSearchOverlay(forceOpen) {
    const current = normalizeGraphKitSearchOverlay(graphKitState.searchOverlay);
    const open = typeof forceOpen === 'boolean' ? forceOpen : !current.open;
    graphKitState.searchOverlay = {
        ...current,
        open,
    };
    if (open && typeof getClosedGraphKitContextMenu === 'function') {
        graphKitState.contextMenu = getClosedGraphKitContextMenu();
    }
    renderGraphKitWorkbench();
    if (open) setTimeout(focusGraphKitSearchInput, 0);
}

function focusGraphKitSearchInput() {
    const input = typeof $pageBody !== 'undefined' ? $pageBody.querySelector('[data-graphkit-search-query]') : null;
    if (!input) return;
    input.focus();
    input.select?.();
}

function renderGraphKitSearchOverlay() {
    const overlay = normalizeGraphKitSearchOverlay(graphKitState.searchOverlay);
    graphKitState.searchOverlay = overlay;
    if (!overlay.open) return '';
    const results = getGraphKitSearchResults(graphKitProject, overlay.query, { scope: overlay.scope });
    overlay.activeIndex = getGraphKitSearchActiveIndex(results.length, overlay.activeIndex);
    graphKitState.searchOverlay = overlay;
    return `<aside class="graphkit-search-overlay" data-graphkit-search-overlay>
        <div class="graphkit-search-overlay__head">
            <label class="graphkit-search-overlay__field">
                <span>Graph Search</span>
                <input class="cmd-input" type="search" data-graphkit-search-query value="${escapeGraphKitSearchHtml(overlay.query)}" placeholder="Search graph">
            </label>
            <button class="graphkit-panel-icon" type="button" data-graphkit-action="toggle-search" title="关闭 Search" aria-label="关闭 Search">×</button>
        </div>
        ${renderGraphKitSearchScopeControls(overlay.scope)}
        <div class="graphkit-search-overlay__meta">
            <span>${escapeGraphKitSearchHtml(formatGraphKitSearchScopeTitle(overlay.scope))}</span>
            <strong data-graphkit-search-count>${escapeGraphKitSearchHtml(formatGraphKitSearchCount(results, overlay.query))}</strong>
        </div>
        <div class="graphkit-search-results" data-graphkit-search-results>
            ${renderGraphKitSearchResults(results, overlay.query, overlay.activeIndex)}
        </div>
    </aside>`;
}

function renderGraphKitSearchScopeControls(scope) {
    const currentActive = scope === GRAPHKIT_SEARCH_SCOPE_CURRENT ? ' active' : '';
    const projectActive = scope === GRAPHKIT_SEARCH_SCOPE_PROJECT ? ' active' : '';
    return `<div class="graphkit-search-scope" role="group" aria-label="Search scope">
        <button class="graphkit-search-scope__button${currentActive}" type="button" data-graphkit-search-scope="current">Current Graph</button>
        <button class="graphkit-search-scope__button${projectActive}" type="button" data-graphkit-search-scope="project">All Graphs</button>
    </div>`;
}

function renderGraphKitSearchResults(results, query, activeIndex = 0) {
    if (!normalizeGraphKitSearchQuery(query)) {
        return `<div class="graphkit-search-empty">No query</div>`;
    }
    if (!results.length) return `<div class="graphkit-search-empty">No results</div>`;
    const currentIndex = getGraphKitSearchActiveIndex(results.length, activeIndex);
    return results.map((result, index) => renderGraphKitSearchResult(result, index, currentIndex)).join('');
}

function renderGraphKitSearchResult(result, index, activeIndex = 0) {
    const subtitle = result.graphTitle && result.graphId
        ? `${result.graphTitle} · ${result.subtitle}`
        : result.subtitle;
    const active = index === activeIndex;
    const activeClass = active ? ' is-active' : '';
    const activeAttrs = active ? ' data-graphkit-search-active="true" aria-current="true"' : '';
    return `<button class="graphkit-search-result graphkit-search-result--${escapeGraphKitSearchHtml(result.kind)}${activeClass}" type="button" data-graphkit-search-result="${escapeGraphKitSearchHtml(index)}"${activeAttrs}>
        <span>${escapeGraphKitSearchHtml(result.kind)}</span>
        <strong>${escapeGraphKitSearchHtml(result.title)}</strong>
        <em>${escapeGraphKitSearchHtml(subtitle)}</em>
        <code>${escapeGraphKitSearchHtml(result.text)}</code>
    </button>`;
}

function formatGraphKitSearchCount(results, query) {
    if (!normalizeGraphKitSearchQuery(query)) return 'ready';
    return `${results.length} results`;
}

function formatGraphKitSearchScopeTitle(scope) {
    if (scope === GRAPHKIT_SEARCH_SCOPE_PROJECT) return `${graphKitProject.graphs.length} graphs`;
    return graphKitProject.graph.title;
}

function bindGraphKitSearchOverlay() {
    const input = $pageBody.querySelector('[data-graphkit-search-query]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => applyGraphKitSearchQuery(input.value));
        input.addEventListener('keydown', event => {
            if (event.key === 'Enter') {
                const overlay = normalizeGraphKitSearchOverlay(graphKitState.searchOverlay);
                focusGraphKitSearchResult(overlay.activeIndex);
                event.preventDefault();
            } else if (event.key === 'ArrowDown') {
                navigateGraphKitSearchResults(1);
                event.preventDefault();
            } else if (event.key === 'ArrowUp') {
                navigateGraphKitSearchResults(-1);
                event.preventDefault();
            }
        });
    }
    $pageBody.querySelectorAll('[data-graphkit-search-scope]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => applyGraphKitSearchScope(button.dataset.graphkitSearchScope));
    });
    bindGraphKitSearchResultButtons();
}

function bindGraphKitSearchResultButtons() {
    $pageBody.querySelectorAll('[data-graphkit-search-result]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => focusGraphKitSearchResult(Number(button.dataset.graphkitSearchResult)));
    });
}

function applyGraphKitSearchQuery(value) {
    graphKitState.searchOverlay = {
        ...normalizeGraphKitSearchOverlay(graphKitState.searchOverlay),
        query: String(value || ''),
        activeIndex: 0,
    };
    applyGraphKitSearchResults();
}

function applyGraphKitSearchScope(scope) {
    graphKitState.searchOverlay = {
        ...normalizeGraphKitSearchOverlay(graphKitState.searchOverlay),
        scope: normalizeGraphKitSearchScope(scope),
        activeIndex: 0,
    };
    renderGraphKitWorkbench();
    setTimeout(focusGraphKitSearchInput, 0);
}

function navigateGraphKitSearchResults(delta) {
    const overlay = normalizeGraphKitSearchOverlay(graphKitState.searchOverlay);
    const results = getGraphKitSearchResults(graphKitProject, overlay.query, { scope: overlay.scope });
    if (!results.length) return false;
    graphKitState.searchOverlay = {
        ...overlay,
        activeIndex: getGraphKitSearchNextIndex(results.length, overlay.activeIndex, delta),
    };
    applyGraphKitSearchResults();
    return true;
}

function applyGraphKitSearchResults() {
    const overlay = normalizeGraphKitSearchOverlay(graphKitState.searchOverlay);
    const results = getGraphKitSearchResults(graphKitProject, overlay.query, { scope: overlay.scope });
    const activeIndex = getGraphKitSearchActiveIndex(results.length, overlay.activeIndex);
    graphKitState.searchOverlay = { ...overlay, activeIndex };
    const list = $pageBody.querySelector('[data-graphkit-search-results]');
    if (list) list.innerHTML = renderGraphKitSearchResults(results, overlay.query, activeIndex);
    const count = $pageBody.querySelector('[data-graphkit-search-count]');
    if (count) count.textContent = formatGraphKitSearchCount(results, overlay.query);
    bindGraphKitSearchResultButtons();
    scrollGraphKitSearchActiveResultIntoView();
}

function scrollGraphKitSearchActiveResultIntoView() {
    const active = $pageBody.querySelector('[data-graphkit-search-active="true"]');
    active?.scrollIntoView?.({ block: 'nearest' });
}

function getGraphKitSearchResults(project, query, options = {}) {
    const normalizedQuery = normalizeGraphKitSearchQuery(query);
    if (!normalizedQuery) return [];
    const queryTokens = normalizedQuery.split(' ').filter(Boolean);
    const source = project && typeof project === 'object' ? project : {};
    const nodeTypes = Array.isArray(source.nodeTypes) ? source.nodeTypes : [];
    const nodeTypeById = new Map(nodeTypes.map(type => [type.id, type]));
    const graphs = getGraphKitSearchGraphs(source, normalizeGraphKitSearchScope(options.scope));
    const results = graphs.flatMap(graph => [
        ...getGraphKitNodeSearchResults(graph, nodeTypeById, queryTokens),
        ...getGraphKitEdgeSearchResults(graph, queryTokens),
        ...getGraphKitBlackboardSearchResults(graph, queryTokens),
        ...getGraphKitNoteSearchResults(graph, queryTokens),
        ...getGraphKitPlacematSearchResults(graph, queryTokens),
    ]);
    return results.slice(0, GRAPHKIT_SEARCH_RESULT_LIMIT);
}

function getGraphKitSearchGraphs(project, scope) {
    const source = project && typeof project === 'object' ? project : {};
    const activeGraph = source.graph && typeof source.graph === 'object' ? source.graph : {};
    if (scope !== GRAPHKIT_SEARCH_SCOPE_PROJECT) return [activeGraph].filter(graph => graph && typeof graph === 'object');
    const graphs = Array.isArray(source.graphs) && source.graphs.length ? source.graphs : [activeGraph];
    return graphs.filter(graph => graph && typeof graph === 'object');
}

function getGraphKitNodeSearchResults(graph, nodeTypeById, queryTokens) {
    return (Array.isArray(graph.nodes) ? graph.nodes : []).map(node => {
        const nodeType = nodeTypeById.get(node.type) || {};
        const fieldText = Object.entries(node.fields || {}).map(([name, value]) => `${name} ${formatGraphKitSearchValue(value)}`).join(' ');
        return makeGraphKitSearchResult({
            kind: 'node',
            id: node.id,
            title: `${nodeType.title || node.type} · ${node.id}`,
            subtitle: `${node.type} · ${nodeType.handlerId || 'missing handler'}`,
            text: fieldText,
            graphId: graph.id,
            graphTitle: graph.title,
            terms: [graph.id, graph.title, graph.kind, node.id, node.type, nodeType.title, nodeType.category, nodeType.handlerId, fieldText],
        });
    }).filter(result => matchesGraphKitSearch(result.terms, queryTokens));
}

function getGraphKitEdgeSearchResults(graph, queryTokens) {
    return (Array.isArray(graph.edges) ? graph.edges : []).map(edge => makeGraphKitSearchResult({
        kind: 'edge',
        id: edge.id,
        title: edge.label || edge.id,
        subtitle: `${edge.from} -> ${edge.to}`,
        text: [edge.condition, edge.priority ? `priority ${edge.priority}` : ''].filter(Boolean).join(' · '),
        graphId: graph.id,
        graphTitle: graph.title,
        terms: [graph.id, graph.title, graph.kind, edge.id, edge.from, edge.to, edge.label, edge.condition, edge.priority],
    })).filter(result => matchesGraphKitSearch(result.terms, queryTokens));
}

function getGraphKitBlackboardSearchResults(graph, queryTokens) {
    return (Array.isArray(graph.blackboard) ? graph.blackboard : []).map(item => makeGraphKitSearchResult({
        kind: 'blackboard',
        id: item.name,
        title: item.name,
        subtitle: `${item.type} · ${item.section || 'Default'}`,
        text: formatGraphKitSearchValue(item.defaultValue),
        graphId: graph.id,
        graphTitle: graph.title,
        terms: [graph.id, graph.title, graph.kind, item.name, item.type, item.section, item.defaultValue],
    })).filter(result => matchesGraphKitSearch(result.terms, queryTokens));
}

function getGraphKitNoteSearchResults(graph, queryTokens) {
    return (Array.isArray(graph.notes) ? graph.notes : []).map(item => makeGraphKitSearchResult({
        kind: 'note',
        id: item.id,
        title: item.title || item.id,
        subtitle: 'Sticky Note',
        text: item.text || '',
        graphId: graph.id,
        graphTitle: graph.title,
        terms: [graph.id, graph.title, graph.kind, item.id, item.title, item.text],
    })).filter(result => matchesGraphKitSearch(result.terms, queryTokens));
}

function getGraphKitPlacematSearchResults(graph, queryTokens) {
    return (Array.isArray(graph.placemats) ? graph.placemats : []).map(item => makeGraphKitSearchResult({
        kind: 'placemat',
        id: item.id,
        title: item.title || item.id,
        subtitle: `${Array.isArray(item.nodeIds) ? item.nodeIds.length : 0} nodes`,
        text: Array.isArray(item.nodeIds) ? item.nodeIds.join(', ') : '',
        graphId: graph.id,
        graphTitle: graph.title,
        terms: [graph.id, graph.title, graph.kind, item.id, item.title, item.nodeIds],
    })).filter(result => matchesGraphKitSearch(result.terms, queryTokens));
}

function makeGraphKitSearchResult(result) {
    return {
        kind: String(result.kind || ''),
        id: String(result.id || ''),
        title: String(result.title || result.id || ''),
        subtitle: String(result.subtitle || ''),
        text: String(result.text || ''),
        graphId: String(result.graphId || ''),
        graphTitle: String(result.graphTitle || ''),
        terms: result.terms || [],
    };
}

function matchesGraphKitSearch(terms, queryTokens) {
    const haystack = normalizeGraphKitSearchQuery(flattenGraphKitSearchTerms(terms).join(' '));
    return queryTokens.every(token => haystack.includes(token));
}

function flattenGraphKitSearchTerms(terms) {
    const values = [];
    (Array.isArray(terms) ? terms : [terms]).forEach(term => {
        if (Array.isArray(term)) values.push(...flattenGraphKitSearchTerms(term));
        else values.push(formatGraphKitSearchValue(term));
    });
    return values;
}

function normalizeGraphKitSearchQuery(value) {
    return String(value ?? '').trim().toLowerCase().replace(/\s+/g, ' ');
}

function normalizeGraphKitSearchScope(scope) {
    return scope === GRAPHKIT_SEARCH_SCOPE_PROJECT ? GRAPHKIT_SEARCH_SCOPE_PROJECT : GRAPHKIT_SEARCH_SCOPE_CURRENT;
}

function normalizeGraphKitSearchActiveIndex(value) {
    const index = Number(value);
    if (!Number.isFinite(index) || index < 0) return 0;
    return Math.floor(index);
}

function getGraphKitSearchActiveIndex(resultCount, activeIndex) {
    const count = Math.max(0, Math.floor(Number(resultCount) || 0));
    if (!count) return 0;
    return Math.min(normalizeGraphKitSearchActiveIndex(activeIndex), count - 1);
}

function getGraphKitSearchNextIndex(resultCount, currentIndex, delta) {
    const count = Math.max(0, Math.floor(Number(resultCount) || 0));
    if (!count) return 0;
    const offset = Math.trunc(Number(delta) || 0);
    const index = getGraphKitSearchActiveIndex(count, currentIndex);
    return (index + offset + count) % count;
}

function formatGraphKitSearchValue(value) {
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

function focusGraphKitSearchResult(index) {
    const overlay = normalizeGraphKitSearchOverlay(graphKitState.searchOverlay);
    const result = getGraphKitSearchResults(graphKitProject, overlay.query, { scope: overlay.scope })[index];
    if (!result) return false;
    const focused = focusGraphKitSearchTarget(result);
    setGraphKitNotice(focused ? 'info' : 'warning', focused ? `已定位 ${result.kind}：${result.id}` : `无法定位 ${result.kind}：${result.id}`);
    renderGraphKitWorkbench();
    return focused;
}

function focusGraphKitSearchTarget(result) {
    if (!result) return false;
    if (result.graphId && graphKitProject.graph.id !== result.graphId) {
        if (typeof selectGraphKitGraphById !== 'function') return false;
        selectGraphKitGraphById(result.graphId);
    }
    if (result.kind === 'node' && typeof focusGraphKitIssueNode === 'function') return focusGraphKitIssueNode(result.id);
    if (result.kind === 'edge' && typeof focusGraphKitIssueEdge === 'function') return focusGraphKitIssueEdge(result.id);
    if (result.kind === 'blackboard') return focusGraphKitSearchBlackboard(result.id);
    if (result.kind === 'note') return focusGraphKitSearchOrganizationItem('note', result.id);
    if (result.kind === 'placemat') return focusGraphKitSearchOrganizationItem('placemat', result.id);
    return false;
}

function focusGraphKitSearchBlackboard(name) {
    const variable = graphKitProject.graph.blackboard.find(item => item.name === name);
    if (!variable) return false;
    graphKitState.selectedBlackboardName = variable.name;
    graphKitState.selectedNodeId = '';
    graphKitState.selectedNodeIds = [];
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedNoteId = '';
    graphKitState.selectedPlacematId = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.pendingPortEndpoint = '';
    return true;
}

function focusGraphKitSearchOrganizationItem(kind, id) {
    const key = kind === 'note' ? 'notes' : 'placemats';
    const item = graphKitProject.graph[key].find(candidate => candidate.id === id);
    if (!item) return false;
    graphKitState.selectedNoteId = kind === 'note' ? item.id : '';
    graphKitState.selectedPlacematId = kind === 'placemat' ? item.id : '';
    graphKitState.selectedNodeId = '';
    graphKitState.selectedNodeIds = [];
    graphKitState.selectedEdgeId = '';
    graphKitState.selectedBlackboardName = '';
    graphKitState.selectedNodeTypeId = '';
    graphKitState.pendingPortEndpoint = '';
    if (typeof getGraphKitOrganizationSceneRect === 'function' && typeof focusGraphKitCanvasPoint === 'function') {
        const rect = getGraphKitOrganizationSceneRect(item);
        focusGraphKitCanvasPoint(rect.x + rect.width * 0.5, rect.y + rect.height * 0.5);
    }
    return true;
}

function escapeGraphKitSearchHtml(value) {
    if (typeof escapeHtml === 'function') return escapeHtml(value);
    return String(value ?? '')
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;')
        .replace(/'/g, '&#39;');
}

const graphKitSearchApi = {
    getClosedGraphKitSearchOverlay,
    normalizeGraphKitSearchOverlay,
    getGraphKitSearchResults,
    getGraphKitSearchGraphs,
    normalizeGraphKitSearchQuery,
    normalizeGraphKitSearchScope,
    normalizeGraphKitSearchActiveIndex,
    getGraphKitSearchActiveIndex,
    getGraphKitSearchNextIndex,
};

if (typeof module !== 'undefined' && module.exports) {
    module.exports = graphKitSearchApi;
}

if (typeof window !== 'undefined') {
    Object.assign(window, graphKitSearchApi);
}
