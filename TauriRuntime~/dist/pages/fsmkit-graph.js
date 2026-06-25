// pages/fsmkit-graph.js
// FsmKit 状态图构建、SVG 渲染、缩放和平移。
// ═══════════════════════════════════════════════════════════════════
// FsmKit — SVG 状态流转图
// 从 get_history 的 {from,to,time} 序列推导有向图，分层布局后渲染 SVG。
// 纯前端、零依赖。当前态高亮，边权重映射线宽，自环/双向边特殊处理。
// ═══════════════════════════════════════════════════════════════════
async function loadFsmGraph() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('\u{1F500}', t('fsmkit.connect_to_view_graph'));
        clearMetrics();
        return;
    }
    try {
        if (!fsmListCache.length) await fetchFsmList();
        if (!fsmListCache.length) {
            $pageBody.innerHTML = emptyState('\u{1F500}', t('fsmkit.no_registered_machines_short'));
            return;
        }
        if (!selectedFsmName || !fsmListCache.some(f => f.name === selectedFsmName))
            selectedFsmName = fsmListCache[0].name;

        // 当前态：从列表缓存取（list_all 已含 currentState）
        const fsmMeta = fsmListCache.find(f => f.name === selectedFsmName) ?? {};
        const currentState = fsmMeta.currentState ?? null;

        const detail = await fetchFsmStateDetail(selectedFsmName);
        const history = await fetchFsmHistory(selectedFsmName);

        const graph = buildFsmGraph(history, currentState ?? detail?.currentState ?? null, detail?.states ?? []);
        let body;
        if (!graph.nodes.length) {
            body = emptyState('\u{1F500}', t('fsmkit.no_snapshot'));
        } else {
            body = `<div class="fsm-graph-toolbar">
                        <span class="fsm-graph-stat">${t("fsmkit.graph_stat", graph.nodes.length, graph.edges.length)}</span>
                        <div class="fsm-viewport-controls">
                            <button class="btn btn-secondary btn-sm" id="fsm-graph-zoom-out" type="button">-</button>
                            <span id="fsm-graph-zoom-label" class="fsm-graph-zoom-label">100%</span>
                            <button class="btn btn-secondary btn-sm" id="fsm-graph-zoom-in" type="button">+</button>
                            <button class="btn btn-secondary btn-sm" id="fsm-graph-fit" type="button">${t("fsmkit.fit")}</button>
                        </div>
                    </div>
                    <div class="fsm-graph-scroll" id="fsm-graph-scroll" data-fsm-name="${escapeHtml(selectedFsmName)}">${renderFsmGraphCanvas(graph)}</div>
                    ${fsmGraphLegend(graph)}`;
        }
        $pageBody.innerHTML = panel(t('fsmkit.state_flow_graph'), fsmSelector() + body, '\u{1F500}');
        bindFsmSelect(loadFsmGraph);
        bindFsmGraphInteractions();
    } catch (e) {
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '\u{26A0}');
    }
}

// 从状态树和转换历史构图：状态树补齐所有状态，历史只负责边和频率。
function buildFsmGraph(history, currentState, stateTree = []) {
    const nodeIndex = new Map();   // 名称 -> 节点
    const edgeIndex = new Map();   // “from→to” -> 边
    const order = [];              // 节点首次出现顺序（用作布局回退）
    const composites = [];
    let latestTransition = null;
    let treeCurrentState = null;

    function ensureNode(name, meta = null) {
        if (!name) return null;
        if (!nodeIndex.has(name)) {
            const node = { id: name, count: 0, layer: -1, isComposite: false, children: [] };
            nodeIndex.set(name, node);
            order.push(node);
        }
        const node = nodeIndex.get(name);
        if (meta) Object.assign(node, meta);
        return node;
    }

    function addStateTreeNodes(states, depth = 0, parentPath = '') {
        for (const state of states ?? []) {
            const name = state?.name ?? state?.stateType ?? null;
            if (!name) continue;
            const children = Array.isArray(state.children) ? state.children : [];
            const node = ensureNode(name, {
                enumId: state.id ?? null,
                orderIndex: state.orderIndex ?? state.id ?? null,
                stateType: state.stateType ?? '',
                isComposite: !!state.isComposite || children.length > 0,
                childMachineName: state.childMachineName ?? '',
                machineState: state.machineState ?? '',
                childCurrentState: state.currentState ?? '',
                childCurrentStateId: state.currentStateId ?? '',
                stateCount: state.stateCount ?? children.length,
                children,
                depth,
                parentPath,
            });
            if (state.isCurrent && !treeCurrentState) treeCurrentState = name;
            if (node.isComposite) composites.push(node);
        }
    }

    addStateTreeNodes(stateTree);

    const activeState = treeCurrentState ?? currentState;
    if (activeState) ensureNode(activeState);

    for (const h of history) {
        const from = h.from ?? null;
        const to = h.to ?? null;
        const fn = ensureNode(from);
        const tn = ensureNode(to);
        if (fn) fn.count++;
        if (tn) tn.count++;
        if (!from || !to) continue;
        const key = from + '→' + to;
        let edge = edgeIndex.get(key);
        if (!edge) {
            edge = { from, to, weight: 0, lastTime: '', selfLoop: from === to };
            edgeIndex.set(key, edge);
        }
        edge.weight++;
        if (h.time) edge.lastTime = h.time;
        latestTransition = { from, to, time: h.time ?? '' };
    }

    const nodes = order;
    const edges = [...edgeIndex.values()];

    // ── 分层（简化 Sugiyama）：从起点 BFS 分配 layer ──
    // 起点优先级：当前态 → 首个出现的节点。
    const startId = activeState && nodeIndex.has(activeState)
        ? activeState
        : (nodes[0]?.id ?? null);

    const adj = new Map();
    for (const n of nodes) adj.set(n.id, []);
    for (const e of edges) {
        if (e.selfLoop) continue;
        adj.get(e.from)?.push(e.to);
    }

    if (startId) {
        const queue = [startId];
        nodeIndex.get(startId).layer = 0;
        while (queue.length) {
            const cur = queue.shift();
            const curLayer = nodeIndex.get(cur).layer;
            for (const next of adj.get(cur) ?? []) {
                const nn = nodeIndex.get(next);
                if (nn.layer === -1) {
                    nn.layer = curLayer + 1;
                    queue.push(next);
                }
            }
        }
    }
    // 未被 BFS 触及的节点（孤立/反向不可达）：追加到末列
    let maxLayer = 0;
    for (const n of nodes) if (n.layer > maxLayer) maxLayer = n.layer;
    for (const n of nodes) if (n.layer === -1) n.layer = ++maxLayer;

    return { nodes, edges, nodeIndex, startId, currentState: activeState, composites, latestTransition };
}

// 布局常量
const FSM_G = {
    nodeW: 132,
    nodeH: 42,
    minRadius: 155,
    nodeArcGap: 64,
    padX: 96,
    padY: 96,
};

// 圆环布局：节点沿圆周排布，转换线在圆内连接节点边界，便于观察任意两态之间的转换关系。
function layoutFsmGraph(graph) {
    return layoutFsmGraphCircular(graph);
}

function layoutFsmGraphCircular(graph) {
    const ordered = sortFsmGraphNodesForCircularLayout(graph.nodes);
    const count = Math.max(1, ordered.length);
    const circumferenceRadius = ((FSM_G.nodeW + FSM_G.nodeArcGap) * count) / (Math.PI * 2);
    const radius = count <= 1 ? 0 : Math.max(FSM_G.minRadius, circumferenceRadius);
    const width = Math.max(520, Math.ceil((radius + FSM_G.nodeW / 2 + FSM_G.padX) * 2));
    const height = Math.max(420, Math.ceil((radius + FSM_G.nodeH / 2 + FSM_G.padY) * 2));
    const centerX = width / 2;
    const centerY = height / 2;

    ordered.forEach((node, index) => {
        const angle = count <= 1 ? -Math.PI / 2 : -Math.PI / 2 + (Math.PI * 2 * index) / count;
        const cx = centerX + Math.cos(angle) * radius;
        const cy = centerY + Math.sin(angle) * radius;
        node.angle = angle;
        node.cx = cx;
        node.cy = cy;
        node.x = cx - FSM_G.nodeW / 2;
        node.y = cy - FSM_G.nodeH / 2;
    });

    graph.ring = { centerX, centerY, radius };
    return { width, height, centerX, centerY, radius };
}

function sortFsmGraphNodesForCircularLayout(nodes) {
    return [...(nodes ?? [])].sort((a, b) => {
        const ai = Number.isFinite(Number(a.orderIndex)) ? Number(a.orderIndex) : Number.POSITIVE_INFINITY;
        const bi = Number.isFinite(Number(b.orderIndex)) ? Number(b.orderIndex) : Number.POSITIVE_INFINITY;
        if (ai !== bi) return ai - bi;

        const ae = Number.isFinite(Number(a.enumId)) ? Number(a.enumId) : Number.POSITIVE_INFINITY;
        const be = Number.isFinite(Number(b.enumId)) ? Number(b.enumId) : Number.POSITIVE_INFINITY;
        if (ae !== be) return ae - be;

        return nodes.indexOf(a) - nodes.indexOf(b);
    });
}

// 渲染 SVG 字符串。
function renderFsmGraphSvg(graph) {
    const { width, height, centerX, centerY, radius } = layoutFsmGraph(graph);
    const maxWeight = graph.edges.reduce((m, e) => Math.max(m, e.weight), 1);
    const latestTransition = graph.latestTransition ?? null;

    // 边：普通边走贝塞尔；自环走节点上方回环；双向边加曲率偏移区分。
    const edgePairs = new Set(graph.edges.map(e => e.from + '→' + e.to));
    const edgeSvg = graph.edges.map(e => {
        const a = graph.nodeIndex.get(e.from);
        const b = graph.nodeIndex.get(e.to);
        if (!a || !b) return '';
        const w = 1 + (e.weight / maxWeight) * 3;     // 线宽 1~4
        const op = 0.35 + (e.weight / maxWeight) * 0.5; // 透明度
        const isLatest = latestTransition && e.from === latestTransition.from && e.to === latestTransition.to;
        if (e.selfLoop) return fsmSelfLoopSvg(a, e, w, op, isLatest);
        const hasReverse = edgePairs.has(e.to + '→' + e.from);
        return fsmEdgeSvg(a, b, e, w, op, hasReverse, isLatest);
    }).join('');

    const nodeSvg = graph.nodes.map(n => fsmNodeSvg(n, graph, latestTransition?.to === n.id)).join('');
    const compositeSvg = renderFsmCompositeGroups(graph);

    return `<svg class="fsm-graph-svg" viewBox="0 0 ${width} ${height}" width="${width}" height="${height}"
                 data-base-width="${width}" data-base-height="${height}" data-zoom="1"
                 xmlns="http://www.w3.org/2000/svg" role="img" aria-label="State flow graph">
        <defs>
            <marker id="fsm-arrow" viewBox="0 0 10 10" refX="9" refY="5"
                    markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                <path d="M0,0 L10,5 L0,10 z" fill="var(--ink-subtle)"/>
            </marker>
            <marker id="fsm-arrow-active" viewBox="0 0 10 10" refX="9" refY="5"
                    markerWidth="7" markerHeight="7" orient="auto-start-reverse">
                <path d="M0,0 L10,5 L0,10 z" fill="var(--primary-hover)"/>
            </marker>
            <linearGradient id="fsm-node-current" x1="0" y1="0" x2="0" y2="1">
                <stop offset="0%" stop-color="#5e6ad2"/>
                <stop offset="100%" stop-color="#828fff"/>
            </linearGradient>
            <filter id="fsm-glow" x="-50%" y="-50%" width="200%" height="200%">
                <feGaussianBlur stdDeviation="4" result="b"/>
                <feMerge><feMergeNode in="b"/><feMergeNode in="SourceGraphic"/></feMerge>
            </filter>
        </defs>
        <g class="fsm-graph-guides">
            ${radius > 0 ? `<circle class="fsm-graph-ring" cx="${centerX}" cy="${centerY}" r="${radius}"/>` : ''}
        </g>
        <g class="fsm-graph-composites">${compositeSvg}</g>
        <g class="fsm-graph-edges">${edgeSvg}</g>
        <g class="fsm-graph-nodes">${nodeSvg}</g>
    </svg>`;
}

function renderFsmGraphCanvas(graph) {
    return `<div class="fsm-graph-pan" data-pan-x="0" data-pan-y="0">${renderFsmGraphSvg(graph)}</div>`;
}

function renderFsmCompositeGroups(graph) {
    return (graph.composites ?? []).map(node => {
        if (typeof node.x !== 'number' || typeof node.y !== 'number') return '';
        const childCount = Array.isArray(node.children) ? node.children.length : 0;
        const childLabel = childCount > 0 ? t('fsmkit.child_states', childCount) : t('fsmkit.child_machine');
        const title = t('fsmkit.composite_title', node.id, node.childMachineName || t('fsmkit.hierarchical_machine'), childLabel);
        const x = Math.max(FSM_G.padX / 2, node.x - 16);
        const y = Math.max(FSM_G.padY / 2, node.y - 30);
        const w = FSM_G.nodeW + 32;
        const h = FSM_G.nodeH + 58;
        return `<g class="fsm-composite-state" transform="translate(${x},${y})">
            <title>${escapeHtml(title)}</title>
            <rect width="${w}" height="${h}" rx="12" ry="12"/>
            <text x="14" y="20">${escapeHtml(node.childMachineName || t('fsmkit.hierarchical_machine'))}</text>
            <text x="14" y="${h - 12}" class="fsm-composite-state__meta">${escapeHtml(childLabel)}</text>
        </g>`;
    }).join('');
}

// 普通有向边：从 a 右侧到 b 左侧的三次贝塞尔；双向时按方向加竖直偏移。
function fsmEdgeSvg(a, b, e, w, op, hasReverse, isLatest = false) {
    const start = fsmNodeAnchorPoint(a, b);
    const end = fsmNodeAnchorPoint(b, a);
    const mid = { x: (start.x + end.x) / 2, y: (start.y + end.y) / 2 };
    const curve = fsmCircularEdgePath(start, end, a, b, hasReverse);
    const badge = e.weight > 1
        ? `<g class="fsm-edge-badge" transform="translate(${mid.x},${mid.y})">
               <circle r="9" />
               <text x="0" y="3" text-anchor="middle">${e.weight}</text>
           </g>`
        : '';
    const title = `${e.from} → ${e.to} · ${e.weight}×${e.lastTime ? ' · ' + e.lastTime : ''}`;
    const latestClass = isLatest ? ' fsm-edge--latest' : '';
    const flow = isLatest ? renderFsmEdgeFlowDot(curve) : '';
    return `<g class="fsm-edge${latestClass}" data-from="${encodeURIComponent(e.from)}" data-to="${encodeURIComponent(e.to)}">
        <title>${escapeHtml(title)}</title>
        <path d="${curve}"
              fill="none" stroke="var(--ink-subtle)" stroke-width="${w.toFixed(2)}"
              stroke-opacity="${op.toFixed(2)}" marker-end="${isLatest ? 'url(#fsm-arrow-active)' : 'url(#fsm-arrow)'}"/>
        ${badge}
        ${flow}
    </g>`;
}

// 自环：节点上方画一段回环弧 + 箭头。
function fsmSelfLoopSvg(a, e, w, op, isLatest = false) {
    const ux = Math.cos(a.angle ?? -Math.PI / 2);
    const uy = Math.sin(a.angle ?? -Math.PI / 2);
    const tx = -uy;
    const ty = ux;
    const p1 = fsmNodeBoundaryPoint(a, ux * 0.5 + tx, uy * 0.5 + ty, 2);
    const p2 = fsmNodeBoundaryPoint(a, ux * 0.5 - tx, uy * 0.5 - ty, 2);
    const c1 = { x: p1.x + ux * 44 + tx * 16, y: p1.y + uy * 44 + ty * 16 };
    const c2 = { x: p2.x + ux * 44 - tx * 16, y: p2.y + uy * 44 - ty * 16 };
    const badgeX = a.cx + ux * 54;
    const badgeY = a.cy + uy * 54;
    const title = `${e.from} → ${e.to} (self) · ${e.weight}×${e.lastTime ? ' · ' + e.lastTime : ''}`;
    const path = `M${p1.x},${p1.y} C${c1.x},${c1.y} ${c2.x},${c2.y} ${p2.x},${p2.y}`;
    const latestClass = isLatest ? ' fsm-edge--latest' : '';
    return `<g class="fsm-edge fsm-edge--self${latestClass}" data-from="${encodeURIComponent(e.from)}" data-to="${encodeURIComponent(e.to)}">
        <title>${escapeHtml(title)}</title>
        <path d="${path}"
              fill="none" stroke="var(--ink-subtle)" stroke-width="${w.toFixed(2)}"
              stroke-opacity="${op.toFixed(2)}" marker-end="${isLatest ? 'url(#fsm-arrow-active)' : 'url(#fsm-arrow)'}"/>
        ${e.weight > 1 ? `<g class="fsm-edge-badge" transform="translate(${badgeX},${badgeY})">
            <circle r="9"/><text x="0" y="3" text-anchor="middle">${e.weight}</text></g>` : ''}
        ${isLatest ? renderFsmEdgeFlowDot(path) : ''}
    </g>`;
}

function renderFsmEdgeFlowDot(path) {
    return `<circle class="fsm-edge-flow-dot" r="4">
        <animateMotion dur="0.9s" repeatCount="1" path="${escapeHtml(path)}"/>
    </circle>`;
}

function fsmCircularEdgePath(start, end, a, b, hasReverse) {
    if (!hasReverse) {
        return `M${start.x},${start.y} L${end.x},${end.y}`;
    }
    const dx = end.x - start.x;
    const dy = end.y - start.y;
    const len = Math.max(1, Math.hypot(dx, dy));
    const direction = (a.id ?? '').localeCompare(b.id ?? '') <= 0 ? 1 : -1;
    const offset = 18 * direction;
    const cx = (start.x + end.x) / 2 + (-dy / len) * offset;
    const cy = (start.y + end.y) / 2 + (dx / len) * offset;
    return `M${start.x},${start.y} Q${cx},${cy} ${end.x},${end.y}`;
}

function fsmNodeAnchorPoint(fromNode, toNode) {
    const dx = (toNode.cx ?? toNode.x) - (fromNode.cx ?? fromNode.x);
    const dy = (toNode.cy ?? toNode.y) - (fromNode.cy ?? fromNode.y);
    return fsmNodeBoundaryPoint(fromNode, dx, dy, 3);
}

function fsmNodeBoundaryPoint(node, dx, dy, padding = 0) {
    const cx = node.cx ?? node.x + FSM_G.nodeW / 2;
    const cy = node.cy ?? node.y + FSM_G.nodeH / 2;
    const absX = Math.abs(dx);
    const absY = Math.abs(dy);
    if (absX < 0.001 && absY < 0.001) {
        return { x: cx, y: cy - FSM_G.nodeH / 2 - padding };
    }
    const scaleX = absX > 0.001 ? (FSM_G.nodeW / 2 + padding) / absX : Number.POSITIVE_INFINITY;
    const scaleY = absY > 0.001 ? (FSM_G.nodeH / 2 + padding) / absY : Number.POSITIVE_INFINITY;
    const scale = Math.min(scaleX, scaleY);
    return { x: cx + dx * scale, y: cy + dy * scale };
}

// 状态节点：节点本身仍是可读标签，整体按圆环排布。
function fsmNodeSvg(n, graph, isLatestTarget = false) {
    const isCurrent = graph.currentState && n.id === graph.currentState;
    const cls = 'fsm-node' + (isCurrent ? ' fsm-node--current' : '') + (isLatestTarget ? ' fsm-node--latest-target' : '');
    const label = n.id.length > 12 ? n.id.slice(0, 11) + '…' : n.id;
    const nodeRole = isCurrent ? t('fsmkit.graph_node_role_current') : t('fsmkit.graph_node_role_visited');
    return `<g class="${cls}" data-node="${encodeURIComponent(n.id)}" transform="translate(${n.x},${n.y})">
        <title>${t("fsmkit.graph_node_title", escapeHtml(n.id), nodeRole, escapeHtml(n.count))}</title>
        <rect width="${FSM_G.nodeW}" height="${FSM_G.nodeH}" rx="10" ry="10"
              ${isCurrent ? 'filter="url(#fsm-glow)"' : ''}/>
        <text class="fsm-node-label" x="12" y="${FSM_G.nodeH / 2 + 4}" text-anchor="start">${escapeHtml(label)}</text>
        <text class="fsm-node-visit-count" x="${FSM_G.nodeW - 12}" y="${FSM_G.nodeH / 2 + 4}" text-anchor="end">${t("fsmkit.visits", escapeHtml(n.count))}</text>
    </g>`;
}

function fsmGraphLegend(graph) {
    const cur = graph.currentState
        ? `<span class="fsm-legend-item"><span class="fsm-legend-dot fsm-legend-dot--current"></span>${t("fsmkit.legend_current_state")}</span>`
        : '';
    return `<div class="fsm-graph-legend">
        ${cur}
        <span class="fsm-legend-item"><span class="fsm-legend-dot"></span>${t("fsmkit.legend_state")}</span>
        <span class="fsm-legend-item"><span class="fsm-legend-edge"></span>${t("fsmkit.legend_transition")}</span>
    </div>`;
}

function resolveFsmGraphViewportKey(fsmName = '') {
    const scroll = document.getElementById('fsm-graph-scroll');
    return fsmName || scroll?.dataset.fsmName || selectedFsmName || '';
}

function captureFsmGraphViewport(fsmName = '') {
    const scroll = document.getElementById('fsm-graph-scroll');
    const key = resolveFsmGraphViewportKey(fsmName);
    if (!scroll || !key) return;
    const svg = scroll.querySelector('.fsm-graph-svg');
    const pan = scroll.querySelector('.fsm-graph-pan');
    const zoom = Number(scroll.dataset.zoom || svg?.dataset.zoom || '1') || 1;
    fsmGraphViewportByMachine.set(key, {
        scrollLeft: scroll.scrollLeft,
        scrollTop: scroll.scrollTop,
        panX: Number(pan?.dataset.panX || '0') || 0,
        panY: Number(pan?.dataset.panY || '0') || 0,
        zoom,
    });
}

function restoreFsmGraphViewport(fsmName = '') {
    const scroll = document.getElementById('fsm-graph-scroll');
    const key = resolveFsmGraphViewportKey(fsmName);
    if (!scroll || !key) return;
    const viewport = fsmGraphViewportByMachine.get(key);
    if (!viewport) {
        requestAnimationFrame(() => fitFsmGraphToViewport(false));
        return;
    }
    applyFsmGraphZoom(viewport?.zoom ?? 1, false);
    applyFsmGraphPan(viewport?.panX ?? 0, viewport?.panY ?? 0, false);
    requestAnimationFrame(() => {
        scroll.scrollLeft = viewport.scrollLeft;
        scroll.scrollTop = viewport.scrollTop;
    });
}

function applyFsmGraphZoom(zoom, persist = true) {
    const scroll = document.getElementById('fsm-graph-scroll');
    const svg = scroll?.querySelector('.fsm-graph-svg');
    const pan = scroll?.querySelector('.fsm-graph-pan');
    if (!scroll || !svg || !pan) return;
    const nextZoom = Math.max(0.35, Math.min(1.8, zoom));
    const baseWidth = Number(svg.getAttribute('data-base-width') || svg.getAttribute('width')) || 360;
    const baseHeight = Number(svg.getAttribute('data-base-height') || svg.getAttribute('height')) || 200;
    svg.setAttribute('data-base-width', String(baseWidth));
    svg.setAttribute('data-base-height', String(baseHeight));
    svg.setAttribute('data-zoom', String(nextZoom));
    pan.style.width = `${Math.round(baseWidth * nextZoom)}px`;
    pan.style.height = `${Math.round(baseHeight * nextZoom)}px`;
    svg.style.width = `${Math.round(baseWidth * nextZoom)}px`;
    svg.style.height = `${Math.round(baseHeight * nextZoom)}px`;
    scroll.dataset.zoom = String(nextZoom);
    const label = document.getElementById('fsm-graph-zoom-label');
    if (label) label.textContent = `${Math.round(nextZoom * 100)}%`;
    applyFsmGraphPan(Number(pan.dataset.panX || '0') || 0, Number(pan.dataset.panY || '0') || 0, false);
    if (persist) captureFsmGraphViewport();
}

function applyFsmGraphPan(panX, panY, persist = true) {
    const scroll = document.getElementById('fsm-graph-scroll');
    const pan = scroll?.querySelector('.fsm-graph-pan');
    if (!scroll || !pan) return;
    pan.dataset.panX = String(Math.round(panX));
    pan.dataset.panY = String(Math.round(panY));
    pan.style.transform = `translate(${Math.round(panX)}px, ${Math.round(panY)}px)`;
    if (persist) captureFsmGraphViewport();
}

function zoomFsmGraphAtPoint(nextZoom, clientX, clientY) {
    const scroll = document.getElementById('fsm-graph-scroll');
    if (!scroll) return;
    const beforeZoom = Number(scroll.dataset.zoom || '1') || 1;
    const rect = scroll.getBoundingClientRect();
    const beforeLeft = scroll.scrollLeft + clientX - rect.left;
    const beforeTop = scroll.scrollTop + clientY - rect.top;
    applyFsmGraphZoom(nextZoom, false);
    const afterZoom = Number(scroll.dataset.zoom || '1') || 1;
    const ratio = afterZoom / beforeZoom;
    scroll.scrollLeft = beforeLeft * ratio - (clientX - rect.left);
    scroll.scrollTop = beforeTop * ratio - (clientY - rect.top);
    captureFsmGraphViewport();
}

function fitFsmGraphToViewport(persist = true) {
    const scroll = document.getElementById('fsm-graph-scroll');
    const svg = scroll?.querySelector('.fsm-graph-svg');
    if (!scroll || !svg) return;
    const baseWidth = Number(svg.getAttribute('data-base-width') || svg.getAttribute('width')) || 360;
    const baseHeight = Number(svg.getAttribute('data-base-height') || svg.getAttribute('height')) || 200;
    const widthZoom = (scroll.clientWidth - 32) / baseWidth;
    const heightZoom = (scroll.clientHeight - 32) / baseHeight;
    const fitZoom = Math.min(1, widthZoom, heightZoom);
    applyFsmGraphZoom(Math.max(0.35, fitZoom), false);
    applyFsmGraphPan(0, 0, false);
    scroll.scrollLeft = 0;
    scroll.scrollTop = 0;
    if (persist) captureFsmGraphViewport();
}

// 图交互：滚轮缩放、按住拖拽平移，并保留工具按钮。
function bindFsmGraphInteractions() {
    const fit = document.getElementById('fsm-graph-fit');
    const zoomIn = document.getElementById('fsm-graph-zoom-in');
    const zoomOut = document.getElementById('fsm-graph-zoom-out');
    const scroll = document.getElementById('fsm-graph-scroll');
    if (fit && fit.dataset.bound !== '1') {
        fit.dataset.bound = '1';
        fit.addEventListener('click', () => {
            fitFsmGraphToViewport(true);
        });
    }
    if (zoomIn && zoomIn.dataset.bound !== '1') {
        zoomIn.dataset.bound = '1';
        zoomIn.addEventListener('click', () => zoomFsmGraphAtPoint(Number(scroll?.dataset.zoom || '1') + 0.15, scroll?.clientWidth / 2 ?? 0, scroll?.clientHeight / 2 ?? 0));
    }
    if (zoomOut && zoomOut.dataset.bound !== '1') {
        zoomOut.dataset.bound = '1';
        zoomOut.addEventListener('click', () => zoomFsmGraphAtPoint(Number(scroll?.dataset.zoom || '1') - 0.15, scroll?.clientWidth / 2 ?? 0, scroll?.clientHeight / 2 ?? 0));
    }
    if (scroll && scroll.dataset.bound !== '1') {
        scroll.dataset.bound = '1';
        scroll.addEventListener('scroll', () => captureFsmGraphViewport(), { passive: true });
        scroll.addEventListener('wheel', event => {
            event.preventDefault();
            const delta = event.deltaY < 0 ? 0.12 : -0.12;
            zoomFsmGraphAtPoint(Number(scroll.dataset.zoom || '1') + delta, event.clientX, event.clientY);
        }, { passive: false });
        let dragState = null;
        scroll.addEventListener('pointerdown', event => {
            if (event.button !== 0) return;
            const pan = scroll.querySelector('.fsm-graph-pan');
            dragState = {
                pointerId: event.pointerId,
                startX: event.clientX,
                startY: event.clientY,
                panX: Number(pan?.dataset.panX || '0') || 0,
                panY: Number(pan?.dataset.panY || '0') || 0,
            };
            scroll.classList.add('fsm-graph-scroll--panning');
            scroll.setPointerCapture?.(event.pointerId);
        });
        scroll.addEventListener('pointermove', event => {
            if (!dragState || dragState.pointerId !== event.pointerId) return;
            applyFsmGraphPan(
                dragState.panX + event.clientX - dragState.startX,
                dragState.panY + event.clientY - dragState.startY
            );
        });
        const endDrag = event => {
            if (!dragState || dragState.pointerId !== event.pointerId) return;
            dragState = null;
            scroll.classList.remove('fsm-graph-scroll--panning');
            scroll.releasePointerCapture?.(event.pointerId);
            captureFsmGraphViewport();
        };
        scroll.addEventListener('pointerup', endDrag);
        scroll.addEventListener('pointercancel', endDrag);
    }
    restoreFsmGraphViewport();
}
