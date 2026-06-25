// pages/uikit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：UIKit
// ═══════════════════════════════════════════════════════════════════
const uikitState = {
    stats: {},
    panels: [],
    stacks: [],
    searchTerm: '',
    selectedKind: 'panel',
    selectedId: null,
    editorToolsAvailable: false,
    editorToolState: null,
    editorStatusMessage: '',
    editorStatusKind: 'info',
    editorForm: {
        panelName: '',
        scriptNamespace: 'GameUI',
        prefabFolder: 'Assets/Resources/Art/UIPrefab',
        scriptFolder: 'Assets/Scripts/UI',
        prefabPath: '',
        assemblyName: 'Assembly-CSharp',
        codeTemplate: 'Default',
        overwrite: false,
    },
    renderSignature: '',
};

function renderUIKitPage() {
    const targetEngine = getSelectedEngineForNavigation();
    uikitState.editorToolsAvailable = engineSupportsKitFeature(targetEngine, 'UIKit', 'ui_editor_tools');
    $pageBody.classList.add('content-body--uikit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--audiokit');
    $pageBody.classList.remove('content-body--savekit');
    $pageBody.classList.remove('content-body--localizationkit');
    $pageBody.classList.remove('content-body--scenekit');
    $pageBody.classList.remove('content-body--spatialkit');
    $pageBody.classList.remove('content-body--inputkit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('uikit.title'),
        t('uikit.subtitle'),
        t('uikit.tab'),
        'ui',
        `<button class="btn btn-primary btn-sm" onclick="refreshUIKit()">${t('common.refresh')}</button>`
    );
    clearTabs();
    uikitState.renderSignature = '';
    loadUIKitWorkbench();
}

async function refreshUIKit() { loadUIKitWorkbench(); }

async function refreshUIKitReactive(event) {
    await loadUIKitWorkbench();
}

function normalizeUIKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const panelsSource = source.panels ?? {};
    const stacksSource = source.stacks ?? {};
    return {
        stats: source.stats ?? {},
        panels: Array.isArray(source.panels)
            ? source.panels
            : (Array.isArray(panelsSource.panels) ? panelsSource.panels : []),
        stacks: Array.isArray(source.stacks)
            ? source.stacks
            : (Array.isArray(stacksSource.stacks) ? stacksSource.stacks : []),
    };
}

async function fetchUIKitWorkbenchState() {
    const telemetry = await readKitTelemetryData('UIKit');
    if (telemetry) return normalizeUIKitStatePayload(telemetry);

    const snapshot = await readKitSnapshotData('UIKit');
    if (snapshot) return normalizeUIKitStatePayload(snapshot);

    return null;
}

async function fetchUIKitWorkbenchStateFromCommands() {
    const snapshot = await sendKitCommandData('UIKit', 'get_workbench_snapshot');
    return normalizeUIKitStatePayload(snapshot);
}

async function refreshUIKitEditorToolState() {
    try {
        const state = await sendKitCommandData('UIKit', 'get_editor_tool_state');
        uikitState.editorToolState = normalizeUIKitEditorToolState(state);
    } catch (e) {
        uikitState.editorToolState = null;
    }
}

function normalizeUIKitEditorToolState(data) {
    const source = data?.data ?? data ?? {};
    const defaults = source.defaults ?? {};
    const assemblyName = defaults.assemblyName ?? 'Assembly-CSharp';
    const codeTemplate = defaults.codeTemplate ?? 'Default';
    return {
        available: source.available !== false,
        selectedObjectCount: Number(source.selectedObjectCount ?? 0),
        activeAssetPath: source.activeAssetPath ?? '',
        canGenerateCode: !!source.canGenerateCode,
        defaults: {
            prefabFolder: defaults.prefabFolder ?? 'Assets/Resources/Art/UIPrefab',
            scriptFolder: defaults.scriptFolder ?? 'Assets/Scripts/UI',
            namespace: defaults.namespace ?? 'GameUI',
            assemblyName,
            codeTemplate,
        },
        assemblies: normalizeUIKitEditorOptions(source.assemblies, [assemblyName]),
        codeTemplates: normalizeUIKitEditorOptions(source.codeTemplates, [codeTemplate]),
    };
}

function normalizeUIKitEditorOptions(values, fallbackValues) {
    const normalized = [];
    const addValue = value => {
        const text = String(value ?? '').trim();
        if (!text || normalized.includes(text)) return;
        normalized.push(text);
    };
    if (Array.isArray(fallbackValues)) {
        for (const value of fallbackValues) addValue(value);
    } else {
        addValue(fallbackValues);
    }
    if (Array.isArray(values)) {
        for (const value of values) addValue(value);
    }
    return normalized;
}

function syncUIKitEditorFormDefaults(toolState) {
    const defaults = toolState?.defaults ?? {};
    const assemblies = normalizeUIKitEditorOptions(toolState?.assemblies, [defaults.assemblyName ?? 'Assembly-CSharp']);
    const codeTemplates = normalizeUIKitEditorOptions(toolState?.codeTemplates, [defaults.codeTemplate ?? 'Default']);

    if (!isUIKitEditorOptionValid(uikitState.editorForm.assemblyName, assemblies)) {
        uikitState.editorForm.assemblyName = assemblies[0] ?? defaults.assemblyName ?? 'Assembly-CSharp';
    }

    if (!isUIKitEditorOptionValid(uikitState.editorForm.codeTemplate, codeTemplates)) {
        uikitState.editorForm.codeTemplate = codeTemplates[0] ?? defaults.codeTemplate ?? 'Default';
    }

    if (!String(uikitState.editorForm.scriptNamespace ?? '').trim()) {
        uikitState.editorForm.scriptNamespace = defaults.namespace ?? 'GameUI';
    }

    if (!String(uikitState.editorForm.prefabFolder ?? '').trim()) {
        uikitState.editorForm.prefabFolder = defaults.prefabFolder ?? 'Assets/Resources/Art/UIPrefab';
    }

    if (!String(uikitState.editorForm.scriptFolder ?? '').trim()) {
        uikitState.editorForm.scriptFolder = defaults.scriptFolder ?? 'Assets/Scripts/UI';
    }
}

function isUIKitEditorOptionValid(value, options) {
    if (!Array.isArray(options) || !options.length) return true;
    return options.includes(String(value ?? ''));
}

async function loadUIKitWorkbench() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('ui', '请连接引擎后查看 UI 面板状态。');
        clearMetrics();
        return;
    }

    try {
        const targetEngine = getSelectedEngineForNavigation();
        uikitState.editorToolsAvailable = engineSupportsKitFeature(targetEngine, 'UIKit', 'ui_editor_tools');
        if (uikitState.editorToolsAvailable) {
            await refreshUIKitEditorToolState();
            syncUIKitEditorFormDefaults(uikitState.editorToolState);
        } else {
            uikitState.editorToolState = null;
        }

        const snapshotState = await fetchUIKitWorkbenchState();
        const state = snapshotState ?? await fetchUIKitWorkbenchStateFromCommands();
        uikitState.stats = state.stats;
        uikitState.panels = state.panels;
        uikitState.stacks = state.stacks;
        reconcileUIKitSelection();
        clearMetrics();

        const html = renderUIKitWorkbench(uikitState.stats, uikitState.panels, uikitState.stacks);
        const signature = makeStableSignature({
            stats: uikitState.stats,
            panels: uikitState.panels,
            stacks: uikitState.stacks,
            selectedKind: uikitState.selectedKind,
            selectedId: uikitState.selectedId,
            editorToolsAvailable: uikitState.editorToolsAvailable,
            editorToolState: uikitState.editorToolState,
            editorStatusMessage: uikitState.editorStatusMessage,
            editorStatusKind: uikitState.editorStatusKind,
            editorForm: uikitState.editorForm,
        });
        renderWorkbenchHtmlStable(uikitState, html, signature, bindUIKitWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('UIKit')) {
            showRuntimeKitUnavailable('UIKit', 'UIKit 面板');
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function reconcileUIKitSelection() {
    const selectedPanel = uikitState.selectedKind === 'panel' ? findUIKitPanel(uikitState.selectedId) : null;
    if (selectedPanel) return selectedPanel;

    const selectedStack = uikitState.selectedKind === 'stack' ? findUIKitStack(uikitState.selectedId) : null;
    if (selectedStack) return selectedStack;

    const openPanel = uikitState.panels.find(panel => panel.state === 'Open') ?? uikitState.panels[0] ?? null;
    if (openPanel) {
        uikitState.selectedKind = 'panel';
        uikitState.selectedId = makeUIKitPanelKey(openPanel);
        return openPanel;
    }

    const defaultStack = uikitState.stacks.find(stack => stack.stackName === 'Default') ?? uikitState.stacks[0] ?? null;
    if (defaultStack) {
        uikitState.selectedKind = 'stack';
        uikitState.selectedId = defaultStack.stackName;
        return defaultStack;
    }

    uikitState.selectedKind = 'panel';
    uikitState.selectedId = null;
    return null;
}

function renderUIKitEditorToolsSection() {
    const form = uikitState.editorForm;
    const toolState = uikitState.editorToolState;
    const selectedCount = toolState ? toolState.selectedObjectCount : 0;
    const activeAssetPath = toolState?.activeAssetPath || '未选择 Prefab 资源';
    const canGenerate = toolState?.canGenerateCode ? '可生成代码' : '请选择 UIPrefab';
    const assemblyOptions = normalizeUIKitEditorOptions(toolState?.assemblies, [toolState?.defaults?.assemblyName ?? form.assemblyName ?? 'Assembly-CSharp']);
    const templateOptions = normalizeUIKitEditorOptions(toolState?.codeTemplates, [toolState?.defaults?.codeTemplate ?? form.codeTemplate ?? 'Default']);
    const statusClass = uikitState.editorStatusKind === 'error'
        ? ' uikit-editor-tools__status--error'
        : (uikitState.editorStatusKind === 'success' ? ' uikit-editor-tools__status--success' : '');

    return `<section class="kit-panel uikit-editor-tools">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('ui', 'Unity 编辑器工具')}</div>
                <div class="kit-panel__desc">创建 UIPrefab、生成绑定代码，并对 Unity Selection 添加或移除 Bind。</div>
            </div>
            <span class="kit-panel__count">${escapeHtml(selectedCount)} 个选中对象</span>
        </div>
        <div class="uikit-editor-tools__body">
            <div class="uikit-editor-tools__summary">
                <span>${escapeHtml(canGenerate)}</span>
                <code>${escapeHtml(activeAssetPath)}</code>
            </div>
            <div class="uikit-editor-tools__grid">
                ${renderUIKitEditorField('panelName', 'Panel 名称', form.panelName, '例如 MainMenuPanel')}
                ${renderUIKitEditorField('scriptNamespace', '命名空间', form.scriptNamespace, '生成 Panel / Data 的 namespace')}
                ${renderUIKitEditorSelectField('assemblyName', '程序集', form.assemblyName, assemblyOptions, '用于编译后反射绑定 Prefab 组件')}
                ${renderUIKitEditorSelectField('codeTemplate', '代码模板', form.codeTemplate, templateOptions, '选择生成代码的结构')}
                ${renderUIKitEditorField('prefabFolder', 'Prefab 目录', form.prefabFolder, 'Assets/Resources/Art/UIPrefab')}
                ${renderUIKitEditorField('scriptFolder', '脚本目录', form.scriptFolder, 'Assets/Scripts/UI')}
                ${renderUIKitEditorField('prefabPath', '目标 Prefab', form.prefabPath, '可留空，生成代码时使用 Unity 当前选择')}
                <label class="uikit-editor-field uikit-editor-field--toggle">
                    <span>覆盖 Prefab<em>同名 Prefab 存在时允许替换</em></span>
                    <input type="checkbox" data-uikit-editor-field="overwrite"${form.overwrite ? ' checked' : ''}>
                </label>
            </div>
            <div class="uikit-editor-tools__actions">
                <button class="btn btn-primary btn-sm" type="button" data-uikit-create-panel>创建 UIPrefab</button>
                <button class="btn btn-secondary btn-sm" type="button" data-uikit-generate-code>为选中 Prefab 生成代码</button>
                <button class="btn btn-secondary btn-sm" type="button" data-uikit-add-bind>给选中对象添加 Bind</button>
                <button class="btn btn-secondary btn-sm" type="button" data-uikit-remove-bind>移除选中对象 Bind</button>
            </div>
            <div class="uikit-editor-tools__status${statusClass}">${escapeHtml(uikitState.editorStatusMessage || '等待操作。生成代码后 Unity 会在下一次编译完成时回填 Prefab 引用。')}</div>
        </div>
    </section>`;
}

function renderUIKitEditorField(field, label, value, hint) {
    return `<label class="uikit-editor-field">
        <span>${escapeHtml(label)}${hint ? `<em>${escapeHtml(hint)}</em>` : ''}</span>
        <input class="cmd-input" type="text" data-uikit-editor-field="${escapeHtml(field)}" value="${escapeHtml(value ?? '')}">
    </label>`;
}

function renderUIKitEditorSelectField(field, label, value, options, hint) {
    const normalizedOptions = normalizeUIKitEditorOptions(options, [value]);
    const selectedValue = String(value ?? '');
    const items = normalizedOptions.map(option => {
        const optionValue = String(option ?? '');
        return `<option value="${escapeHtml(optionValue)}"${optionValue === selectedValue ? ' selected' : ''}>${escapeHtml(optionValue)}</option>`;
    }).join('');
    return `<label class="uikit-editor-field">
        <span>${escapeHtml(label)}${hint ? `<em>${escapeHtml(hint)}</em>` : ''}</span>
        <select class="cmd-select" data-uikit-editor-field="${escapeHtml(field)}">${items}</select>
    </label>`;
}

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

function buildUIKitEditorPayload() {
    const form = uikitState.editorForm;
    return {
        PanelName: String(form.panelName ?? '').trim(),
        ScriptNamespace: String(form.scriptNamespace ?? '').trim(),
        PrefabFolder: String(form.prefabFolder ?? '').trim(),
        ScriptFolder: String(form.scriptFolder ?? '').trim(),
        PrefabPath: String(form.prefabPath ?? '').trim(),
        AssemblyName: String(form.assemblyName ?? '').trim(),
        CodeTemplate: String(form.codeTemplate ?? '').trim(),
        Overwrite: !!form.overwrite,
    };
}

async function runUIKitEditorCommand(action) {
    uikitState.editorStatusKind = 'info';
    uikitState.editorStatusMessage = '正在执行 UIKit 编辑器命令...';
    renderUIKitWorkbenchFromState();

    try {
        let result;
        if (action === 'create_panel_prefab') {
            result = await sendKitCommandData('UIKit', 'create_panel_prefab', buildUIKitEditorPayload());
            if (result?.prefabPath) uikitState.editorForm.prefabPath = result.prefabPath;
        } else if (action === 'generate_code_for_selection') {
            result = await sendKitCommandData('UIKit', 'generate_code_for_selection', buildUIKitEditorPayload());
            if (result?.prefabPath) uikitState.editorForm.prefabPath = result.prefabPath;
        } else if (action === 'add_bind_to_selection') {
            result = await sendKitCommandData('UIKit', 'add_bind_to_selection');
        } else if (action === 'remove_bind_from_selection') {
            result = await sendKitCommandData('UIKit', 'remove_bind_from_selection');
        } else {
            throw new Error(`Unsupported UIKit editor action: ${action}`);
        }

        uikitState.editorStatusKind = 'success';
        uikitState.editorStatusMessage = formatUIKitEditorResult(result);
        await loadUIKitWorkbench();
    } catch (e) {
        uikitState.editorStatusKind = 'error';
        uikitState.editorStatusMessage = String(e?.message ?? e);
        renderUIKitWorkbenchFromState();
    }
}

function formatUIKitEditorResult(result) {
    if (!result) return '命令已完成。';
    const parts = [result.message].filter(Boolean);
    if (result.prefabPath) parts.push(result.prefabPath);
    if (Number.isFinite(Number(result.changedCount))) parts.push(`Changed ${result.changedCount}`);
    if (Number.isFinite(Number(result.skippedCount)) && Number(result.skippedCount) > 0) parts.push(`Skipped ${result.skippedCount}`);
    if (result.requiresCompile) parts.push('等待 Unity 编译后回填序列化引用');
    return parts.join(' · ') || '命令已完成。';
}

function renderUIKitWorkbenchFromState() {
    const html = renderUIKitWorkbench(uikitState.stats, uikitState.panels, uikitState.stacks);
    const signature = makeStableSignature({
        stats: uikitState.stats,
        panels: uikitState.panels,
        stacks: uikitState.stacks,
        selectedKind: uikitState.selectedKind,
        selectedId: uikitState.selectedId,
        editorToolsAvailable: uikitState.editorToolsAvailable,
        editorToolState: uikitState.editorToolState,
        editorStatusMessage: uikitState.editorStatusMessage,
        editorStatusKind: uikitState.editorStatusKind,
        editorForm: uikitState.editorForm,
    });
    renderWorkbenchHtmlStable(uikitState, html, signature, bindUIKitWorkbenchActions);
}

function bindUIKitWorkbenchActions() {
    bindUIKitEditorTools();
    $pageBody.querySelectorAll('[data-uikit-panel]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectUIKitPanel(button.dataset.uikitPanel);
        });
    });
    $pageBody.querySelectorAll('[data-uikit-stack]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectUIKitStack(button.dataset.uikitStack);
        });
    });
    bindUIKitSearch();
}

function bindUIKitEditorTools() {
    $pageBody.querySelectorAll('[data-uikit-editor-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        const field = input.dataset.uikitEditorField;
        const eventName = input.type === 'checkbox' || input.tagName === 'SELECT' ? 'change' : 'input';
        input.addEventListener(eventName, () => {
            uikitState.editorForm[field] = input.type === 'checkbox' ? input.checked : input.value;
        });
    });

    bindKitButtonClick('[data-uikit-create-panel]', () => void runUIKitEditorCommand('create_panel_prefab'));
    bindKitButtonClick('[data-uikit-generate-code]', () => void runUIKitEditorCommand('generate_code_for_selection'));
    bindKitButtonClick('[data-uikit-add-bind]', () => void runUIKitEditorCommand('add_bind_to_selection'));
    bindKitButtonClick('[data-uikit-remove-bind]', () => void runUIKitEditorCommand('remove_bind_from_selection'));
}

function bindUIKitSearch() {
    const input = $pageBody.querySelector('[data-uikit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            uikitState.searchTerm = input.value || '';
            updateUIKitListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-uikit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            uikitState.searchTerm = '';
            updateUIKitListDom();
            $pageBody.querySelector('[data-uikit-search]')?.focus();
        });
    }
}

function updateUIKitListDom() {
    const visiblePanels = filterUIKitPanels(uikitState.panels);
    const visibleStacks = filterUIKitStacks(uikitState.stacks);
    const panelList = $pageBody.querySelector('[data-uikit-panel-list]');
    if (panelList) panelList.innerHTML = renderUIKitPanelRows(visiblePanels);
    const stackList = $pageBody.querySelector('[data-uikit-stack-list]');
    if (stackList) stackList.innerHTML = renderUIKitStackRows(visibleStacks);
    const count = $pageBody.querySelector('[data-uikit-visible-count]');
    if (count) count.textContent = `${visiblePanels.length} / ${uikitState.panels.length}`;
    const input = $pageBody.querySelector('[data-uikit-search]');
    if (input && input.value !== uikitState.searchTerm) input.value = uikitState.searchTerm;
    const clear = $pageBody.querySelector('[data-uikit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !uikitState.searchTerm.trim());
    bindUIKitWorkbenchActions();
}

function selectUIKitPanel(panelKey) {
    if (!panelKey) return;
    uikitState.selectedKind = 'panel';
    uikitState.selectedId = panelKey;
    syncUIKitSelectionDom();
}

function selectUIKitStack(stackName) {
    if (!stackName) return;
    uikitState.selectedKind = 'stack';
    uikitState.selectedId = stackName;
    syncUIKitSelectionDom();
}

function syncUIKitSelectionDom() {
    $pageBody.querySelectorAll('[data-uikit-panel]').forEach(row => {
        row.classList.toggle('active',
            uikitState.selectedKind === 'panel' && String(row.dataset.uikitPanel) === String(uikitState.selectedId));
    });
    $pageBody.querySelectorAll('[data-uikit-stack]').forEach(row => {
        row.classList.toggle('active',
            uikitState.selectedKind === 'stack' && String(row.dataset.uikitStack) === String(uikitState.selectedId));
    });
    const panelData = uikitState.selectedKind === 'panel' ? findUIKitPanel(uikitState.selectedId) : null;
    const stackData = uikitState.selectedKind === 'stack' ? findUIKitStack(uikitState.selectedId) : null;
    const detailPanel = $pageBody.querySelector('[data-uikit-detail-panel]');
    if (detailPanel) detailPanel.outerHTML = renderUIKitDetailSection(panelData, stackData);
    bindUIKitWorkbenchActions();
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

