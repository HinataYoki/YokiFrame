// ═══════════════════════════════════════════════════════════════════
// YokiFrame Editor —— 前端应用    v2026-06-20.1
// Native Debug Console · 页面式 SPA · Tauri IPC 桥
// ═══════════════════════════════════════════════════════════════════

function registerYokiFramePages() {
    registerPage('docs', { render: renderDocsPage });
    registerPage('system', { render: renderSystemPage });
    registerPage('architecture', { render: renderArchitecturePage });
    registerPage('poolkit', { render: renderPoolKitPage });
    registerPage('fsmkit', { render: renderFsmKitPage });
    registerPage('eventkit', { render: renderEventKitPage });
    registerPage('logkit', { render: renderLogKitPage });
    registerPage('actionkit', { render: renderActionKitPage });
    registerPage('reskit', { render: renderResKitPage });
    registerPage('audiokit', { render: renderAudioKitPage });
    registerPage('savekit', { render: renderSaveKitPage });
    registerPage('localizationkit', { render: renderLocalizationKitPage });
    registerPage('scenekit', { render: renderSceneKitPage });
    registerPage('spatialkit', { render: renderSpatialKitPage });
    registerPage('uikit', { render: renderUIKitPage });
    registerPage('tablekit', { render: renderTableKitPage });
    registerPage('graphkit', { render: renderGraphKitPage });
    registerPage('singletonkit', { render: renderSingletonKitPage });
    registerPage('managedruntime', { render: renderManagedRuntimePage });

    registerKitReactiveRefresh('fsmkit', {
        pageId: 'fsmkit',
        refresh: refreshFsmKitReactive,
        throttleMs: FSMKIT_REACTIVE_REFRESH_THROTTLE_MS,
    });
    registerKitReactiveRefresh('eventkit', {
        pageId: 'eventkit',
        refresh: refreshEventKitReactive,
    });
    registerKitReactiveRefresh('logkit', {
        pageId: 'logkit',
        refresh: refreshLogKitReactive,
    });
    registerKitReactiveRefresh('actionkit', {
        pageId: 'actionkit',
        refresh: refreshActionKitReactive,
    });
    registerKitReactiveRefresh('poolkit', {
        pageId: 'poolkit',
        refresh: refreshPoolKitReactive,
        throttleMs: POOLKIT_REACTIVE_REFRESH_THROTTLE_MS,
    });
    registerKitReactiveRefresh('reskit', {
        pageId: 'reskit',
        refresh: refreshResKitReactive,
    });
    registerKitReactiveRefresh('audiokit', {
        pageId: 'audiokit',
        refresh: refreshAudioKitReactive,
    });
    registerKitReactiveRefresh('savekit', {
        pageId: 'savekit',
        refresh: refreshSaveKitReactive,
    });
    registerKitReactiveRefresh('localizationkit', {
        pageId: 'localizationkit',
        refresh: refreshLocalizationKitReactive,
    });
    registerKitReactiveRefresh('scenekit', {
        pageId: 'scenekit',
        refresh: refreshSceneKitReactive,
    });
    registerKitReactiveRefresh('spatialkit', {
        pageId: 'spatialkit',
        refresh: refreshSpatialKitReactive,
    });
    registerKitReactiveRefresh('uikit', {
        pageId: 'uikit',
        refresh: refreshUIKitReactive,
    });
    registerKitReactiveRefresh('tablekit', {
        pageId: 'tablekit',
        refresh: refreshTableKitReactive,
    });
    registerKitReactiveRefresh('architecture', {
        pageId: 'architecture',
        refresh: refreshArchitectureReactive,
    });
    registerKitReactiveRefresh('singletonkit', {
        pageId: 'singletonkit',
        refresh: refreshSingletonKitReactive,
    });
}

function bootstrapYokiFrameEditor() {
    if (yokiFrameEditorBootstrapped) return;
    yokiFrameEditorBootstrapped = true;

    applyStaticTranslations();
    applyTheme(activeTheme, { silent: true });
    applyInitialFontPreference();
    bindShellControls();
    bindPackageExternalLinks();
    bindWindowResizeHandles();
    initWindowPersistence();
    window.addEventListener('focus', handleInteractiveFocusResume);
    document.addEventListener('visibilitychange', () => {
        if (!document.hidden) handleInteractiveFocusResume();
    });
    window.addEventListener('beforeunload', teardownPushListeners);
    window.addEventListener('resize', handleWorkspaceResize);
    $pageBody.addEventListener('scroll', markKitInteractionActive, { passive: true, capture: true });
    $pageBody.addEventListener('wheel', markKitInteractionActive, { passive: true });
    $pageBody.addEventListener('pointerdown', markKitInteractionActive, { passive: true });
    restoreWindowState({ showAfter: true });
    setInterval(() => pollStatus(), STATUS_POLL_INTERVAL_MS);
    pollStatus({ force: true });
    setupPushListeners();
    navigateTo('system');
    syncSidebarActiveIndicator();
    void loadYokiFramePackageInfo();
}

registerYokiFramePages();
bootstrapYokiFrameEditor();
