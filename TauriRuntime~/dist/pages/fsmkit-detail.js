// pages/fsmkit-detail.js
// FsmKit 详情辅助：当前状态自适应、复合状态卡片和洞察摘要。
let fsmCurrentStateFitFrame = 0;

function scheduleFsmCurrentStateFit() {
    if (fsmCurrentStateFitFrame) {
        cancelAnimationFrame(fsmCurrentStateFitFrame);
    }
    fsmCurrentStateFitFrame = requestAnimationFrame(() => {
        fsmCurrentStateFitFrame = 0;
        fitFsmCurrentStateValueText();
    });
}

function fitFsmCurrentStateValueText() {
    const value = $pageBody.querySelector('[data-fsm-current-state-value]');
    if (!value) return;

    const container = value.parentElement;
    if (!container) return;

    const maxWidth = container.clientWidth;
    if (maxWidth <= 0) return;

    value.style.fontSize = '';

    const computed = window.getComputedStyle(value);
    const baseFontSize = Number.parseFloat(computed.fontSize) || 18;
    const minFontSize = Math.max(12, baseFontSize * 0.72);
    let low = minFontSize;
    let high = baseFontSize;
    let best = minFontSize;

    value.style.whiteSpace = 'nowrap';
    value.style.overflow = 'hidden';
    value.style.textOverflow = 'clip';

    for (let i = 0; i < 8; i++) {
        const size = (low + high) / 2;
        value.style.fontSize = size.toFixed(2) + 'px';
        if (value.scrollWidth <= maxWidth) {
            best = size;
            low = size;
        } else {
            high = size;
        }
    }

    value.style.fontSize = best.toFixed(2) + 'px';
}

function renderFsmStateCards(graph) {
    const sortedNodes = [...graph.nodes].sort((a, b) => {
        if (a.id === graph.currentState) return -1;
        if (b.id === graph.currentState) return 1;
        return b.count - a.count || a.id.localeCompare(b.id);
    });

    const topLevel = `<div class="fsm-state-grid">` + sortedNodes.map(node => {
        const currentClass = node.id === graph.currentState ? ' fsm-state-tile--current' : '';
        const compositeClass = node.isComposite ? ' fsm-state-tile--composite' : '';
        return `<div class="fsm-state-tile${currentClass}${compositeClass}">
            <span class="fsm-state-tile__name">${escapeHtml(node.id)}</span>
            <span class="fsm-state-tile__meta">${node.id === graph.currentState ? t('fsmkit.graph_node_role_current') : t('fsmkit.graph_node_role_visited')} · ${t('fsmkit.visits', escapeHtml(node.count))}${node.isComposite ? ' · ' + t('fsmkit.hierarchical_machine') : ''}</span>
        </div>`;
    }).join('') + `</div>`;
    return topLevel + renderFsmCompositeStateCards(graph.composites ?? []);
}

function renderFsmCompositeStateCards(composites) {
    const rows = flattenFsmCompositeStates(composites);
    if (!rows.length) return '';

    return `<div class="fsm-composite-board">
        <div class="fsm-section-label">${t('fsmkit.hierarchical_machine')}</div>
        ${rows.map(row => {
            const node = row.node;
            const children = Array.isArray(node.children) ? node.children : [];
            return `<section class="fsm-composite-card">
                <div class="fsm-composite-card__header">
                    <strong>${escapeHtml(row.path)}</strong>
                    <span>${escapeHtml(node.childMachineName || t('fsmkit.child_machine'))} · ${escapeHtml(node.machineState || '--')}</span>
                </div>
                <div class="fsm-composite-card__children">
                    ${children.length ? children.map(child => {
                        const name = child?.name ?? child?.stateType ?? '--';
                        const current = child?.isCurrent ? ' fsm-composite-child--current' : '';
                        const nested = child?.isComposite ? ' fsm-composite-child--nested' : '';
                        const suffix = child?.isComposite ? ' / ' + t('fsmkit.child_machine') : '';
                        return `<span class="fsm-composite-child${current}${nested}">${escapeHtml(name + suffix)}</span>`;
                    }).join('') : '<span class="fsm-composite-child">' + t('fsmkit.no_child_snapshot') + '</span>'}
                </div>
            </section>`;
        }).join('')}
    </div>`;
}

function flattenFsmCompositeStates(composites) {
    const rows = [];

    function visit(node, prefix = '') {
        if (!node) return;
        const nodeName = node.id ?? node.name ?? node.stateType ?? 'State';
        const path = prefix ? `${prefix} / ${nodeName}` : nodeName;
        rows.push({ node, path });

        for (const child of node.children ?? []) {
            if (!child?.isComposite && !(Array.isArray(child?.children) && child.children.length)) continue;
            visit({
                id: child.name ?? child.stateType ?? 'State',
                childMachineName: child.childMachineName ?? '',
                machineState: child.machineState ?? '',
                children: Array.isArray(child.children) ? child.children : [],
            }, path);
        }
    }

    for (const node of composites ?? []) visit(node);
    return rows;
}

function renderFsmInsightsHtml(selectedMeta, detail, history) {
    if (!selectedMeta) {
        return emptyState('○', t('fsmkit.select_machine_hint'));
    }

    const summary = summarizeFsmHistory(history ?? []);
    const currentState = detail?.currentState ?? selectedMeta.currentState ?? '--';
    const machineState = detail?.machineState ?? selectedMeta.machineState ?? '--';
    const latest = summary.latest
        ? `${summary.latest.from ?? '?'} → ${summary.latest.to ?? '?'}`
        : t('fsmkit.no_transition');
    const hottest = summary.hottest
        ? `${summary.hottest.from} → ${summary.hottest.to} · ${t('fsmkit.visits', summary.hottest.count)}`
        : t('fsmkit.no_hot_path');
    const loopText = summary.selfLoopCount > 0
        ? t('fsmkit.self_loop_count', summary.selfLoopCount)
        : t('fsmkit.no_self_loop');

    return `<div class="fsm-insight-list">
        ${fsmInsightRow(t('fsmkit.current_state_label'), currentState, machineState)}
        ${fsmInsightRow(t('fsmkit.latest_transition'), latest, summary.latest?.time ?? '--')}
        ${fsmInsightRow(t('fsmkit.hot_path'), hottest, t('fsmkit.hot_path_hint'))}
        ${fsmInsightRow(t('fsmkit.loop_risk'), loopText, t('fsmkit.history_states', summary.uniqueStateCount))}
    </div>`;
}

function fsmInsightRow(label, value, hint) {
    return `<div class="fsm-insight-item">
        <span class="fsm-insight-item__label">${escapeHtml(label)}</span>
        <strong>${escapeHtml(value)}</strong>
        <span class="fsm-insight-item__hint">${escapeHtml(hint ?? '')}</span>
    </div>`;
}

function summarizeFsmHistory(history) {
    const edgeCounts = new Map();
    const states = new Set();
    let selfLoopCount = 0;

    for (const h of history) {
        if (h.from) states.add(h.from);
        if (h.to) states.add(h.to);
        if (!h.from || !h.to) continue;
        if (h.from === h.to) selfLoopCount++;
        const key = h.from + '→' + h.to;
        const count = edgeCounts.get(key) ?? { from: h.from, to: h.to, count: 0 };
        count.count++;
        edgeCounts.set(key, count);
    }

    let hottest = null;
    for (const edge of edgeCounts.values()) {
        if (!hottest || edge.count > hottest.count) hottest = edge;
    }

    return {
        latest: history.length ? history[history.length - 1] : null,
        hottest,
        selfLoopCount,
        uniqueStateCount: states.size,
    };
}

