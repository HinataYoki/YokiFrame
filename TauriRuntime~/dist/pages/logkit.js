// pages/logkit.js
// 页面：LogKit
const LOGKIT_LEVEL_OPTIONS = ['Debug', 'Info', 'Warning', 'Error'];
const logKitState = {
    stats: {},
    settings: {},
    history: [],
    searchTerm: '',
    selectedLogKind: 'editor',
    viewerTitle: t('logkit.editor_log'),
    viewerContent: '',
    viewerMeta: null,
    decryptSourcePath: '',
    actionMessage: '',
    actionTone: 'success',
    renderSignature: '',
};

function renderLogKitPage() {
    $pageBody.classList.add('content-body--logkit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('logkit.title'),
        t('logkit.subtitle'),
        t('logkit.tab'),
        'log',
        `<button class="btn btn-sm" onclick="clearLogKitHistory()">${t('logkit.clear_history')}</button><button class="btn btn-sm" onclick="resetLogKitSettings()">${t('common.reset')}</button><button class="btn btn-primary btn-sm" onclick="refreshLogKit()">${t('common.refresh')}</button>`
    );
    clearTabs();
    logKitState.renderSignature = '';
    loadLogKitWorkbench();
}

async function refreshLogKit() { loadLogKitWorkbench({ forceCommandRefresh: true }); }

async function refreshLogKitReactive(event) {
    await loadLogKitWorkbench();
}

function normalizeLogKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const historySource = source.history ?? {};
    return {
        stats: source.stats ?? {},
        settings: normalizeLogKitSettings(source.settings ?? {}),
        history: Array.isArray(source.history)
            ? source.history
            : (Array.isArray(historySource.entries) ? historySource.entries : []),
    };
}

function normalizeLogKitSettings(settings) {
    const source = settings ?? {};
    return {
        enabled: source.enabled ?? true,
        minimumLevel: source.minimumLevel ?? 'Debug',
        saveLogInEditor: source.saveLogInEditor ?? false,
        saveLogInPlayer: source.saveLogInPlayer ?? true,
        enableIMGUIInPlayer: source.enableIMGUIInPlayer ?? false,
        enableEncryption: source.enableEncryption ?? true,
        maxQueueSize: source.maxQueueSize ?? 20000,
        maxSameLogCount: source.maxSameLogCount ?? 50,
        maxRetentionDays: source.maxRetentionDays ?? 15,
        maxFileSizeMB: source.maxFileSizeMB ?? 100,
        imguiMaxLogCount: source.imguiMaxLogCount ?? 200,
        logDirectory: source.logDirectory ?? '',
        editorFileName: source.editorFileName ?? 'yoki_editor.log',
        playerFileName: source.playerFileName ?? 'yoki_player.log',
        assetResourcePath: source.assetResourcePath ?? 'YokiFrameRuntimeSettings',
        assetPath: source.assetPath ?? 'Assets/Settings/Resources/YokiFrameRuntimeSettings.asset',
    };
}

async function fetchLogKitWorkbenchState({ forceCommandRefresh = false } = {}) {
    return await fetchKitWorkbenchState('LogKit', normalizeLogKitStatePayload, {
        forceCommandRefresh: forceCommandRefresh,
    });
}

async function loadLogKitWorkbench({ forceCommandRefresh = false } = {}) {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('log', t('logkit.need_runtime'));
        clearMetrics();
        return;
    }

    try {
        const state = await fetchLogKitWorkbenchState({ forceCommandRefresh });
        logKitState.stats = state.stats;
        logKitState.settings = state.settings;
        logKitState.history = state.history;
        await refreshLogKitViewerState();
        clearMetrics();

        const html = renderLogKitWorkbench(logKitState.stats, logKitState.settings, logKitState.history);
        const signature = makeStableSignature({
            stats: logKitState.stats,
            settings: logKitState.settings,
            history: logKitState.history,
            search: logKitState.searchTerm,
            selectedLogKind: logKitState.selectedLogKind,
            viewerTitle: logKitState.viewerTitle,
            viewerContent: logKitState.viewerContent,
            viewerMeta: logKitState.viewerMeta,
            decryptSourcePath: logKitState.decryptSourcePath,
            actionMessage: logKitState.actionMessage,
            actionTone: logKitState.actionTone,
        });
        renderWorkbenchHtmlStable(logKitState, html, signature, bindLogKitWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('LogKit')) {
            showRuntimeKitUnavailable('LogKit', t('logkit.title'));
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}
