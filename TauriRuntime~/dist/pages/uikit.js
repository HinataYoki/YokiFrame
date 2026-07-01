// pages/uikit.js
// 页面：UIKit
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
    return await fetchKitWorkbenchState('UIKit', normalizeUIKitStatePayload);
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

        const state = await fetchUIKitWorkbenchState();
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
