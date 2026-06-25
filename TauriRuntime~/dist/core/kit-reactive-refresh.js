// core/kit-reactive-refresh.js
// Kit 推送事件与响应式刷新调度。
// ═══════════════════════════════════════════════════════════════════
// IPC push 事件监听（Unity 事件 JSONL → Rust → IPC → 前端）
// ═══════════════════════════════════════════════════════════════════
async function setupPushListeners() {
    if (pushListenersReady) return;
    if (!listen) return;
    if (pushListenerSetupPromise) return pushListenerSetupPromise;

    pushListenerSetupPromise = (async () => {
        const nextUnlisteners = [];
        try {
            // Kit 状态变更统一进入响应式刷新总线，避免高频事件重建整页造成抖动。
            nextUnlisteners.push(await listen('yoki-event', (event) => {
                const data = event.payload;
                scheduleKitReactiveRefresh(data);
            }));
            nextUnlisteners.push(await listen('yoki-telemetry', (event) => {
                const data = event.payload;
                scheduleKitReactiveRefresh(data);
            }));

            pushListenerUnlisteners.length = 0;
            nextUnlisteners.forEach(unlisten => {
                if (typeof unlisten === 'function') pushListenerUnlisteners.push(unlisten);
            });
            pushListenersReady = true;
            addLog('IPC 推送监听已挂载', 'system');
        } catch (e) {
            nextUnlisteners.forEach(unlisten => {
                try {
                    if (typeof unlisten === 'function') unlisten();
                } catch (_) { /* 忽略清理失败，下一次初始化会重试。 */ }
            });
            addLog(`IPC 推送监听挂载失败: ${e}`, 'error');
        }
    })();

    return pushListenerSetupPromise.finally(() => {
        pushListenerSetupPromise = null;
    });
}

function teardownPushListeners() {
    pushListenerUnlisteners.forEach(unlisten => {
        try {
            unlisten();
        } catch (_) { /* 页面卸载阶段忽略宿主清理失败。 */ }
    });
    pushListenerUnlisteners.length = 0;
    pushListenersReady = false;
}

function registerKitReactiveRefresh(kitId, options) {
    const normalizedKitId = normalizeKitId(kitId);
    if (!normalizedKitId || !options || typeof options.refresh !== 'function') return;
    kitReactiveRefreshHandlers.set(normalizedKitId, {
        pageId: options.pageId || normalizedKitId,
        refresh: options.refresh,
        throttleMs: options.throttleMs ?? KIT_REACTIVE_REFRESH_THROTTLE_MS,
    });
}

function scheduleKitReactiveRefresh(data) {
    const kitId = resolveKitIdFromReactiveEvent(data);
    if (!kitId) return false;

    const handler = kitReactiveRefreshHandlers.get(kitId);
    if (!handler) return false;

    const state = getKitReactiveRefreshState(kitId);
    state.pendingReason = data?.payload?.event ?? data?.type ?? 'kit_update';
    state.latestEvent = data;
    if (activePage !== handler.pageId) return false;

    if (state.inFlight) {
        state.queued = true;
        return true;
    }

    if (state.timer !== null) return true;

    const elapsed = Date.now() - state.lastRefreshAt;
    const delay = Math.max(0, handler.throttleMs - elapsed);
    state.timer = setTimeout(() => flushKitReactiveRefresh(kitId), delay);
    return true;
}

async function flushKitReactiveRefresh(kitId) {
    const handler = kitReactiveRefreshHandlers.get(kitId);
    const state = getKitReactiveRefreshState(kitId);
    if (state.timer !== null) {
        clearTimeout(state.timer);
        state.timer = null;
    }

    if (!handler) {
        state.pendingReason = null;
        return;
    }

    if (state.inFlight) {
        state.queued = true;
        return;
    }

    if (activePage !== handler.pageId) {
        state.pendingReason = null;
        return;
    }

    const refreshNavigationSeq = navigationSeq;
    if (!isCurrentNavigation(refreshNavigationSeq, handler.pageId)) {
        state.pendingReason = null;
        return;
    }

    const deferMs = getKitInteractionRefreshDelay(state);
    if (deferMs > 0) {
        state.queued = true;
        state.timer = setTimeout(() => flushKitReactiveRefresh(kitId), deferMs);
        return;
    }

    state.inFlight = true;
    state.pendingReason = null;
    try {
        await handler.refresh(state.latestEvent);
    } finally {
        state.lastRefreshAt = Date.now();
        state.interactionDeferred = false;
        state.inFlight = false;
        if ((state.queued || state.pendingReason) && isCurrentNavigation(refreshNavigationSeq, handler.pageId)) {
            state.queued = false;
            state.pendingReason = null;
            scheduleKitReactiveRefresh(state.latestEvent || { type: kitId + '_update', payload: { event: 'coalesced' } });
        } else {
            state.queued = false;
            state.pendingReason = null;
        }
    }
}

function getKitReactiveRefreshState(kitId) {
    let state = kitReactiveRefreshState.get(kitId);
    if (state) return state;

    state = {
        pendingReason: null,
        latestEvent: null,
        timer: null,
        inFlight: false,
        queued: false,
        lastRefreshAt: 0,
        interactionDeferred: false,
    };
    kitReactiveRefreshState.set(kitId, state);
    return state;
}

function markKitInteractionActive() {
    kitInteractionActiveUntil = Date.now() + KIT_INTERACTION_IDLE_MS;
    if (kitInteractionIdleTimer) clearTimeout(kitInteractionIdleTimer);
    kitInteractionIdleTimer = setTimeout(() => {
        kitInteractionActiveUntil = 0;
        kitReactiveRefreshState.forEach((state, kitId) => {
            if (state.queued || state.pendingReason) {
                scheduleKitReactiveRefresh(state.latestEvent || { type: kitId + '_update', payload: { event: 'interaction_idle' } });
            }
        });
    }, KIT_INTERACTION_IDLE_MS);
}

function getKitInteractionRefreshDelay(state) {
    const remaining = kitInteractionActiveUntil - Date.now();
    if (remaining <= 0) return 0;
    if (state.interactionDeferred) return 0;
    state.interactionDeferred = true;
    return Math.min(KIT_INTERACTION_REFRESH_DEFER_MS, remaining);
}

function resolveKitIdFromReactiveEvent(data) {
    const payloadKit = normalizeKitId(data?.payload?.kit);
    if (payloadKit) return payloadKit;

    const directKit = normalizeKitId(data?.kit);
    if (directKit) return directKit;

    const type = typeof data?.type === 'string' ? data.type : '';
    if (!type.endsWith('_update')) return null;

    const rawKit = type.slice(0, -'_update'.length);
    const normalized = normalizeKitId(rawKit);
    return KIT_EVENT_ALIASES[normalized] || normalized;
}

function normalizeKitId(value) {
    if (typeof value !== 'string') return null;
    const normalized = value.toLowerCase().replace(/[^a-z0-9]/g, '');
    return normalized || null;
}
