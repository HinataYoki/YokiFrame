// core/window-state.js
// 窗口尺寸、位置和自定义拉伸句柄。
function saveWindowState() {
    const w = window.outerWidth;
    const h = window.outerHeight;
    if (!Number.isFinite(w) || !Number.isFinite(h) || w <= 0 || h <= 0) return;
    const size = clampWindowSize(w, h);
    const x = window.screenX ?? window.screenLeft ?? 0;
    const y = window.screenY ?? window.screenTop ?? 0;
    localStorage.setItem(WIN_KEY, JSON.stringify({ w: size.w, h: size.h, x, y }));
}

function clampWindowSize(w, h) {
    return {
        w: Math.max(MIN_WINDOW_WIDTH, Math.round(w)),
        h: Math.max(MIN_WINDOW_HEIGHT, Math.round(h)),
    };
}

async function enforceMinimumWindowSize() {
    if (minimumWindowResizeInFlight || !window.__TAURI__?.window) return;
    const w = window.outerWidth || window.innerWidth || 0;
    const h = window.outerHeight || window.innerHeight || 0;
    if (w >= MIN_WINDOW_WIDTH && h >= MIN_WINDOW_HEIGHT) return;

    minimumWindowResizeInFlight = true;
    try {
        const win = window.__TAURI__.window.getCurrentWindow();
        const SizeCls = window.__TAURI__.window.LogicalSize ?? window.__TAURI__.window.PhysicalSize;
        const size = clampWindowSize(w || MIN_WINDOW_WIDTH, h || MIN_WINDOW_HEIGHT);
        await win.setSize(new SizeCls(size.w, size.h));
    } catch (_) {
        // 原生最小尺寸配置仍是主保护；旧运行时失败时保持静默。
    } finally {
        minimumWindowResizeInFlight = false;
    }
}

function scheduleWorkspaceResizeWork() {
    if (workspaceResizeFrame) return;
    workspaceResizeFrame = requestAnimationFrame(() => {
        workspaceResizeFrame = 0;
        enforceMinimumWindowSize();
        fitFsmCurrentStateValueText();
        syncSidebarActiveIndicator();
    });
}

function getCurrentTauriWindow() {
    return window.__TAURI__?.window?.getCurrentWindow?.();
}

async function disableNativeWindowResizeHitTest() {
    const win = getCurrentTauriWindow();
    if (!win || typeof win.setResizable !== 'function') return;
    try {
        await win.setResizable(false);
    } catch (_) {
        // 运行时权限或旧 API 缺失时不阻塞工作台。
    }
}

function scheduleNativeWindowResizeHitTestSuppression(delayMs = WINDOW_RESIZE_RESET_IDLE_MS) {
    clearTimeout(nativeResizeResetTimer);
    nativeResizeResetTimer = setTimeout(() => {
        nativeResizeDragActive = false;
        disableNativeWindowResizeHitTest();
    }, delayMs);
}

function finishWindowResizeDrag() {
    if (!nativeResizeDragActive) return;
    scheduleNativeWindowResizeHitTestSuppression(160);
}

function normalizeWindowResizeDirection(direction) {
    return WINDOW_RESIZE_DIRECTIONS[direction] || null;
}

async function startWindowResizeDrag(direction) {
    const win = getCurrentTauriWindow();
    if (!win || typeof win.startResizeDragging !== 'function') return;
    const nativeDirection = normalizeWindowResizeDirection(direction);
    if (!nativeDirection) return;

    nativeResizeDragActive = true;
    clearTimeout(nativeResizeResetTimer);
    try {
        if (typeof win.setResizable === 'function') {
            await win.setResizable(true);
        }
        await win.startResizeDragging(nativeDirection);
        scheduleNativeWindowResizeHitTestSuppression(WINDOW_RESIZE_RESET_FAILSAFE_MS);
    } catch (e) {
        nativeResizeDragActive = false;
        scheduleNativeWindowResizeHitTestSuppression(0);
        addLog(t('window.resize_failed', e.message || e), 'error');
    }
}

function bindWindowResizeHandles() {
    document.querySelectorAll('[data-window-resize]').forEach(handle => {
        handle.addEventListener('pointerdown', (event) => {
            if (event.pointerType === 'mouse' && event.button !== 0) return;
            event.preventDefault();
            event.stopPropagation();
            startWindowResizeDrag(handle.dataset.windowResize);
        });
    });

    document.addEventListener('pointerup', finishWindowResizeDrag);
    document.addEventListener('mouseup', finishWindowResizeDrag);
    window.addEventListener('blur', () => scheduleNativeWindowResizeHitTestSuppression(240));
    document.addEventListener('visibilitychange', () => {
        if (document.hidden) scheduleNativeWindowResizeHitTestSuppression(240);
    });
}

async function refinePositionFromTauri() {
    if (!window.__TAURI__?.window) return;
    try {
        const win = window.__TAURI__.window.getCurrentWindow();
        const pos = await Promise.race([
            win.outerPosition(),
            new Promise((_, rej) => setTimeout(() => rej(new Error('timeout')), 1500)),
        ]);
        const raw = localStorage.getItem(WIN_KEY);
        const data = raw ? JSON.parse(raw) : {};
        data.x = pos.x;
        data.y = pos.y;
        if (!data.w) data.w = window.outerWidth;
        if (!data.h) data.h = window.outerHeight;
        localStorage.setItem(WIN_KEY, JSON.stringify(data));
    } catch (_) {
        // 静默降级为浏览器 screenX/Y。
    }
}

function scheduleSaveWindow() {
    clearTimeout(_winSaveTimer);
    _winSaveTimer = setTimeout(() => {
        saveWindowState();
        refinePositionFromTauri();
    }, 600);
}

async function showWindowOnce() {
    if (_windowShown || !window.__TAURI__?.window) return;
    _windowShown = true;
    try {
        await window.__TAURI__.window.getCurrentWindow().show();
    } catch (e) {
        addLog(t('window.show_failed', e.message || e), 'error');
    }
}

async function signalPanelWindowReady() {
    if (typeof invoke !== 'function') {
        await showWindowOnce();
        return;
    }

    try {
        await invoke('mark_panel_window_ready');
    } catch (e) {
        addLog(t('window.show_failed', e.message || e), 'error');
        await showWindowOnce();
    }
}

async function restoreWindowState({ showAfter = false } = {}) {
    if (!window.__TAURI__?.window) {
        return;
    }
    const win = window.__TAURI__.window.getCurrentWindow();
    try {
        const raw = localStorage.getItem(WIN_KEY);
        if (raw) {
            const { w, h, x, y } = JSON.parse(raw);
            const SizeCls = window.__TAURI__.window.LogicalSize ?? window.__TAURI__.window.PhysicalSize;
            const PosCls = window.__TAURI__.window.LogicalPosition ?? window.__TAURI__.window.PhysicalPosition;
            if (Number.isFinite(w) && Number.isFinite(h)) {
                const size = clampWindowSize(w, h);
                await win.setSize(new SizeCls(size.w, size.h));
                if (typeof x === 'number' && typeof y === 'number' && x > -200 && y > -200 && x < 8000 && y < 6000) {
                    await win.setPosition(new PosCls(Math.round(x), Math.round(y)));
                }
            }
        }
    } catch (e) {
        addLog(t('window.restore_failed', e.message || e), 'error');
    } finally {
        if (showAfter) {
            await signalPanelWindowReady();
        }
    }
}

function initWindowPersistence() {
    window.addEventListener('resize', scheduleSaveWindow);
    window.addEventListener('beforeunload', () => saveWindowState());

    if (window.__TAURI__?.window) {
        try {
            const tauriWindow = window.__TAURI__.window.getCurrentWindow();
            disableNativeWindowResizeHitTest();
            if (typeof tauriWindow.onMoved === 'function') {
                tauriWindow.onMoved(() => { saveWindowState(); refinePositionFromTauri(); });
            }
            if (typeof tauriWindow.onResized === 'function') {
                tauriWindow.onResized(() => {
                    scheduleSaveWindow();
                    scheduleWorkspaceResizeWork();
                    if (nativeResizeDragActive) {
                        scheduleNativeWindowResizeHitTestSuppression(WINDOW_RESIZE_RESET_IDLE_MS);
                    }
                });
            }
        } catch (_) {
            // 保持浏览器事件兜底。
        }
    }

    setInterval(() => {
        if (!isWindowInteractive()) return;
        saveWindowState();
        refinePositionFromTauri();
    }, 5000);
}

// ═══════════════════════════════════════════════════════════════════
// 日志系统（内存缓冲，只在 System 页面渲染）
// ═══════════════════════════════════════════════════════════════════
