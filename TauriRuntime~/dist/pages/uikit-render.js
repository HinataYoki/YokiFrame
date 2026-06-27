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
        ${editorTools}
        <div class="kit-workbench-grid kit-workbench-grid--uikit">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('ui', '面板与栈')}</div>
                        <div class="kit-panel__desc">当前缓存面板、面板栈和层级状态</div>
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
            ${renderUIKitStatsSection(stats, stacks)}
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

function renderUIKitStatsSection(stats, stacks) {
    const rootSettings = stats?.rootSettings ?? {};
    const pixelPerfect = typeof rootSettings.pixelPerfect === 'boolean' ? (rootSettings.pixelPerfect ? '是' : '否') : '--';
    return `<section class="kit-panel kit-panel--events">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('ui', '运行统计')}</div>
                <div class="kit-panel__desc">后端、缓存、栈深度和面板可见状态</div>
            </div>
        </div>
        <div class="kit-detail-summary kit-detail-summary--save-auto">
            <div><span>后端</span><strong>${escapeHtml(stats?.backendName || '--')}</strong></div>
            <div><span>已初始化</span><strong>${escapeHtml(stats?.isInitialized ? '是' : '否')}</strong></div>
            <div><span>面板总数</span><strong>${escapeHtml(stats?.panelCount ?? 0)}</strong></div>
            <div><span>缓存面板</span><strong>${escapeHtml(stats?.cachedPanelCount ?? 0)}</strong></div>
            <div><span>打开</span><strong>${escapeHtml(stats?.openPanelCount ?? 0)}</strong></div>
            <div><span>隐藏</span><strong>${escapeHtml(stats?.hiddenPanelCount ?? 0)}</strong></div>
            <div><span>关闭</span><strong>${escapeHtml(stats?.closedPanelCount ?? 0)}</strong></div>
            <div><span>栈数量</span><strong>${escapeHtml(stats?.stackCount ?? stacks.length ?? 0)}</strong></div>
            <div><span>栈深度</span><strong>${escapeHtml(stats?.totalStackDepth ?? 0)}</strong></div>
            <div><span>Default 顶部</span><strong>${escapeHtml(stats?.defaultTopPanelName || '--')}</strong></div>
        </div>
        <div class="kit-note" data-uikit-root-settings>UIRoot 设置</div>
        <div class="kit-detail-summary kit-detail-summary--save-auto">
            <div><span>Render Mode</span><strong>${escapeHtml(rootSettings.renderMode || '--')}</strong></div>
            <div><span>Sort Order</span><strong>${escapeHtml(rootSettings.sortOrder ?? '--')}</strong></div>
            <div><span>Target Display</span><strong>${escapeHtml(rootSettings.targetDisplay ?? '--')}</strong></div>
            <div><span>Pixel Perfect</span><strong>${escapeHtml(pixelPerfect)}</strong></div>
            <div><span>Scale Mode</span><strong>${escapeHtml(rootSettings.scaleMode || '--')}</strong></div>
            <div><span>Reference</span><strong>${escapeHtml(rootSettings.referenceResolution || '--')}</strong></div>
            <div><span>Match</span><strong>${escapeHtml(rootSettings.matchWidthOrHeight ?? '--')}</strong></div>
            <div><span>Blocking</span><strong>${escapeHtml(rootSettings.blockingObjects || '--')}</strong></div>
        </div>
        <div class="kit-note">快照由 Adapter 节流发布到 UIKit/state；页面优先读 telemetry，再读 snapshot，缺失时才走命令桥。</div>
    </section>`;
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
