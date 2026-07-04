// pages/uikit-render.js
// UIKit 运行时面板渲染
function renderUIKitWorkbench(stats, panels, stacks) {
    const visiblePanels = filterUIKitPanels(panels);
    const visibleStacks = filterUIKitStacks(stacks);
    const selectedPanel = uikitState.selectedKind === 'panel' ? findUIKitPanel(uikitState.selectedId) : null;
    const selectedStack = uikitState.selectedKind === 'stack' ? findUIKitStack(uikitState.selectedId) : null;
    const editorTools = uikitState.editorToolsAvailable ? renderUIKitEditorToolsSection() : '';
    return `<div class="kit-workbench kit-workbench--ui">
        <section class="kit-toolbar">
            <div>
                <div class="kit-toolbar__title">${renderKitTitle('ui', 'UI 面板工作台')}</div>
                <div class="kit-toolbar__meta">Backend: ${escapeHtml(stats?.backendName || 'None')} · Panels ${escapeHtml(stats?.panelCount ?? panels.length ?? 0)} · Stacks ${escapeHtml(stats?.stackCount ?? stacks.length ?? 0)} · Top ${escapeHtml(stats?.defaultTopPanelName || '--')}</div>
            </div>
            <div class="kit-toolbar__actions">
                <span class="kit-state-pill ${stats?.isInitialized ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${escapeHtml(stats?.isInitialized ? 'Initialized' : 'No Backend')}</span>
            </div>
        </section>
        <div class="uikit-setup-grid" data-uikit-setup-grid>
            ${editorTools}
            ${renderUIKitRootSettingsSection(stats?.rootSettings ?? {})}
        </div>
        ${renderUIKitRuntimeSummarySection(stats, stacks)}
        <div class="uikit-inspector-layout">
            <div class="kit-workbench-grid kit-workbench-grid--uikit">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('ui', '面板与栈')}</div>
                        <div class="kit-panel__desc">运行时面板缓存和栈状态</div>
                    </div>
                    <span class="kit-panel__count" data-uikit-visible-count>${escapeHtml(visiblePanels.length)} / ${escapeHtml(panels.length)}</span>
                </div>
                <div class="kit-panel__tools">${renderKitSearchInput(uikitState.searchTerm, 'data-uikit-search', '搜索面板、类型、层级、标签或栈')}</div>
                <div class="kit-note">面板</div>
                <div class="kit-resource-list" data-kit-scroll-key="ui-panels" data-uikit-panel-list>${renderUIKitPanelRows(visiblePanels)}</div>
                <div class="kit-note">面板栈</div>
                <div class="kit-resource-list" data-kit-scroll-key="ui-stacks" data-uikit-stack-list>${renderUIKitStackRows(visibleStacks)}</div>
            </section>
            ${renderUIKitDetailSection(selectedPanel, selectedStack)}
            </div>
        </div>
    </div>`;
}

function filterUIKitPanels(panels) {
    return (Array.isArray(panels) ? panels : []).filter(panel => kitSearchMatches(uikitState.searchTerm, [
        panel.panelName,
        panel.panelTypeName,
        panel.state,
        panel.level,
        panel.tag,
        panel.dataTypeName,
        ...(panel.stackNames ?? []),
    ]));
}

function filterUIKitStacks(stacks) {
    return (Array.isArray(stacks) ? stacks : []).filter(stack => kitSearchMatches(uikitState.searchTerm, [
        stack.stackName,
        stack.topPanelName,
        stack.depth,
        ...(stack.panelNames ?? []),
    ]));
}

function renderUIKitPanelRows(panels) {
    if (!Array.isArray(panels) || !panels.length) {
        return emptyState('ui', '暂无面板。调用 UIKit.OpenPanel 或 PushOpenPanel 后会显示。');
    }

    return panels.map(panel => {
        const key = makeUIKitPanelKey(panel);
        const selected = uikitState.selectedKind === 'panel' && String(key) === String(uikitState.selectedId);
        return `<button class="kit-list-row${selected ? ' active' : ''}" type="button" data-uikit-panel="${escapeHtml(key)}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(panel.panelName || '--')}</strong>
                <em>${escapeHtml(panel.state || 'Unknown')} · ${escapeHtml(panel.level || '--')} · ${escapeHtml(panel.tag || '无标签')}</em>
            </span>
            <span class="kit-list-row__stats">${escapeHtml((panel.stackNames ?? []).length)} 栈</span>
        </button>`;
    }).join('');
}

function renderUIKitStackRows(stacks) {
    if (!Array.isArray(stacks) || !stacks.length) {
        return emptyState('ui', '暂无面板栈。调用 UIKit.PushPanel 后会显示。');
    }

    return stacks.map(stack => {
        const selected = uikitState.selectedKind === 'stack' && String(stack.stackName) === String(uikitState.selectedId);
        return `<button class="kit-list-row${selected ? ' active' : ''}" type="button" data-uikit-stack="${escapeHtml(stack.stackName ?? '')}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(stack.stackName || '--')}</strong>
                <em>Depth ${escapeHtml(stack.depth ?? 0)} · Top ${escapeHtml(stack.topPanelName || '--')}</em>
            </span>
            <span class="kit-list-row__stats">${escapeHtml((stack.panelNames ?? []).length)} Panels</span>
        </button>`;
    }).join('');
}

function renderUIKitDetailSection(panelData, stackData) {
    return `<section class="kit-panel kit-panel--detail" data-uikit-detail-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', 'UI 详情')}</div>
                <div class="kit-panel__desc">${escapeHtml(panelData?.panelName || stackData?.stackName || '未选择')}</div>
            </div>
        </div>
        ${renderUIKitDetail(panelData, stackData)}
    </section>`;
}

function renderUIKitDetail(panelData, stackData) {
    if (panelData) {
        return `<div class="kit-detail-summary kit-detail-summary--save">
            <div><span>面板</span><strong>${escapeHtml(panelData.panelName || '--')}</strong></div>
            <div><span>类型</span><strong>${escapeHtml(panelData.panelTypeName || '--')}</strong></div>
            <div><span>${t("common.status")}</span><strong>${escapeHtml(panelData.state || 'Unknown')}</strong></div>
            <div><span>层级</span><strong>${escapeHtml(panelData.level || '--')} (${escapeHtml(panelData.levelOrder ?? 0)})</strong></div>
            <div><span>标签</span><strong>${escapeHtml(panelData.tag || '--')}</strong></div>
            <div><span>数据</span><strong>${escapeHtml(panelData.dataTypeName || '--')}</strong></div>
            <div><span>缓存</span><strong>${escapeHtml(panelData.isCached ? '是' : '否')}</strong></div>
            <div><span>所在栈</span><strong>${escapeHtml(formatUIKitStringList(panelData.stackNames))}</strong></div>
        </div>
        <div class="kit-note">UIKit 命令桥只读展示面板缓存和面板栈，不通过文件桥打开、关闭或切换 UI。</div>`;
    }

    if (stackData) {
        return `<div class="kit-detail-summary kit-detail-summary--save">
            <div><span>栈名</span><strong>${escapeHtml(stackData.stackName || '--')}</strong></div>
            <div><span>深度</span><strong>${escapeHtml(stackData.depth ?? 0)}</strong></div>
            <div><span>顶部面板</span><strong>${escapeHtml(stackData.topPanelName || '--')}</strong></div>
        </div>
        <div class="kit-mini-list" data-kit-scroll-key="ui-stack-panels">${renderUIKitStackPanelNames(stackData)}</div>
        <div class="kit-note">面板栈仍由运行时代码通过 UIKit.PushPanel/PopPanel 管理；工作台只观察当前状态。</div>`;
    }

    return emptyState('ui', '选择一个面板或面板栈后查看详细状态。');
}

function renderUIKitStackPanelNames(stackData) {
    const panelNames = Array.isArray(stackData?.panelNames) ? stackData.panelNames : [];
    if (!panelNames.length) {
        return emptyState('ui', '这个面板栈当前为空。');
    }

    return panelNames.map((panelName, index) => `<div class="kit-mini-row">
        <strong>${escapeHtml(panelName || '--')}</strong>
        <em>#${escapeHtml(index)}</em>
    </div>`).join('');
}

function renderUIKitRuntimeSummarySection(stats, stacks) {
    const panelCount = Number(stats?.panelCount ?? 0);
    const stackCount = Number(stats?.stackCount ?? stacks.length ?? 0);
    const openCount = Number(stats?.openPanelCount ?? 0);
    const cachedCount = Number(stats?.cachedPanelCount ?? 0);
    const hiddenCount = Number(stats?.hiddenPanelCount ?? 0);
    const closedCount = Number(stats?.closedPanelCount ?? 0);
    const totalStackDepth = Number(stats?.totalStackDepth ?? 0);
    return `<section class="uikit-runtime-strip" data-uikit-runtime-summary>
        <div class="uikit-runtime-strip__identity">
            <span class="kit-state-pill ${stats?.isInitialized ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${escapeHtml(stats?.isInitialized ? '已初始化' : '未初始化')}</span>
            <strong>${escapeHtml(stats?.backendName || '--')}</strong>
            <em>Default 顶部：${escapeHtml(stats?.defaultTopPanelName || '--')}</em>
        </div>
        <div class="uikit-runtime-strip__metrics">
            ${renderUIKitRuntimeMetric('面板', panelCount, `${openCount} 打开 / ${cachedCount} 缓存`)}
            ${renderUIKitRuntimeMetric('栈', stackCount, `${totalStackDepth} 总深度`)}
            ${renderUIKitRuntimeMetric('隐藏', hiddenCount, `${closedCount} 关闭`)}
        </div>
        <details class="uikit-runtime-strip__details">
            <summary>诊断细节</summary>
            <div class="uikit-runtime-strip__detail-grid">
                <span>数据源</span><strong>telemetry / snapshot / command</strong>
                <span>面板总数</span><strong>${escapeHtml(panelCount)}</strong>
                <span>缓存面板</span><strong>${escapeHtml(cachedCount)}</strong>
                <span>栈数量</span><strong>${escapeHtml(stackCount)}</strong>
            </div>
        </details>
    </section>`;
}

function renderUIKitRuntimeMetric(label, value, hint) {
    return `<div class="uikit-runtime-strip__metric">
        <span>${escapeHtml(label)}</span>
        <strong>${escapeHtml(value)}</strong>
        <em>${escapeHtml(hint)}</em>
    </div>`;
}

function makeUIKitPanelKey(panel) {
    return String(panel?.panelTypeName || panel?.panelName || '');
}

function findUIKitPanel(panelKey) {
    return uikitState.panels.find(panel => String(makeUIKitPanelKey(panel)) === String(panelKey)) ?? null;
}

function findUIKitStack(stackName) {
    return uikitState.stacks.find(stack => String(stack.stackName) === String(stackName)) ?? null;
}

function formatUIKitStringList(values) {
    if (!Array.isArray(values) || !values.length) return '--';
    return values.filter(Boolean).join(', ') || '--';
}
