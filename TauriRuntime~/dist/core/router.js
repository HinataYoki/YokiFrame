// core/router.js
// ═══════════════════════════════════════════════════════════════════
// 页面注册与路由
// ═══════════════════════════════════════════════════════════════════
const pages = {};
const NAVIGATION_MOTION_SETTLE_MS = 240;
const PAGE_LOADING_STATE_PAGE_IDS = new Set(['architecture', 'tablekit']);
let navigationMotionTimer = 0;

function registerPage(pageId, page) {
    if (!pageId || !page || typeof page.render !== 'function') return;
    pages[pageId] = page;
}

function isCurrentNavigation(requestSeq, pageId) {
    return requestSeq === navigationSeq && activePage === pageId;
}

function scheduleNavigationRender(pageId, requestSeq) {
    if (pendingNavigationRenderTask) {
        clearTimeout(pendingNavigationRenderTask);
        pendingNavigationRenderTask = 0;
    }

    pendingNavigationRenderTask = setTimeout(() => {
        pendingNavigationRenderTask = 0;
        if (!isCurrentNavigation(requestSeq, pageId)) return;
        const page = pages[pageId];
        if (page) {
            page.render();
            if (isCurrentNavigation(requestSeq, pageId)) {
                markNavigationMotion('settle');
            }
        }
    }, 0);
}

function markNavigationMotion(phase) {
    if (!$pageBody) return;
    if (navigationMotionTimer) {
        clearTimeout(navigationMotionTimer);
        navigationMotionTimer = 0;
    }

    if (phase === 'start') {
        $pageBody.classList.remove('is-navigation-settling');
        $pageBody.classList.add('is-navigating');
        return;
    }

    $pageBody.classList.remove('is-navigating');
    $pageBody.classList.add('is-navigation-settling');
    navigationMotionTimer = setTimeout(() => {
        $pageBody.classList.remove('is-navigation-settling');
        navigationMotionTimer = 0;
    }, NAVIGATION_MOTION_SETTLE_MS);
}

function shouldShowPageLoadingState(pageId) {
    return PAGE_LOADING_STATE_PAGE_IDS.has(pageId);
}

function navigateTo(pageId) {
    const targetKit = KIT_PAGE_ID_TO_KIT[pageId];
    if (targetKit && !engineSupportsKit(getSelectedEngineForNavigation(), targetKit)) {
        pageId = 'system';
    }
    if (!pages[pageId]) {
        pageId = 'system';
    }

    const requestSeq = ++navigationSeq;
    activePage = pageId;
    $pageBody.classList.remove('content-body--system');
    $pageBody.classList.remove('content-body--fsmkit');
    $pageBody.classList.remove('content-body--eventkit');
    $pageBody.classList.remove('content-body--architecture');
    $pageBody.classList.remove('content-body--logkit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--actionkit');
    $pageBody.classList.remove('content-body--audiokit');
    $pageBody.classList.remove('content-body--savekit');
    $pageBody.classList.remove('content-body--localizationkit');
    $pageBody.classList.remove('content-body--scenekit');
    $pageBody.classList.remove('content-body--spatialkit');
    $pageBody.classList.remove('content-body--inputkit');
    $pageBody.classList.remove('content-body--uikit');
    $pageBody.classList.remove('content-body--tablekit');
    $pageBody.classList.remove('content-body--singletonkit');
    $pageBody.classList.add(`content-body--${pageId}`);
    // 更新侧边栏。
    document.querySelectorAll('.sidebar-item').forEach(item => {
        item.classList.toggle('active', item.dataset.page === pageId);
    });
    syncSidebarKitAvailability();
    syncSidebarActiveIndicator();
    markNavigationMotion('start');
    // 清空内容。
    clearMetrics();
    clearTabs();
    const pageLoadToken = currentPageLoadToken(pageId);
    if (shouldShowPageLoadingState(pageId)) {
        setPageBodyForLoad(pageLoadToken, renderPageLoadingState());
    }
    // 渲染页面。
    scheduleNavigationRender(pageId, requestSeq);
}

function refreshCurrentPage() {
    const page = pages[activePage];
    if (page) page.render();
}

// 侧边栏点击处理
document.querySelectorAll('.sidebar-item').forEach(item => {
    item.addEventListener('click', () => navigateTo(item.dataset.page));
});

function handleWorkspaceResize() {
    scheduleWorkspaceResizeWork();
    if (nativeResizeDragActive) {
        scheduleNativeWindowResizeHitTestSuppression(WINDOW_RESIZE_RESET_IDLE_MS);
    }
}

