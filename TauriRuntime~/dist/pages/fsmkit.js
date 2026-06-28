// pages/fsmkit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：FsmKit
// ═══════════════════════════════════════════════════════════════════
let fsmListCache = [];        // 最近一次 list_all 的 fsms
let selectedFsmName = null;   // State Detail / History 选中的 FSM
let fsmWorkbenchLoadSeq = 0;
let fsmListSearchTerm = '';
let fsmWorkbenchListEmptyMessage = '';
let fsmWorkbenchRenderSignature = '';
let fsmLastDetailReconcileAt = 0;
let fsmDetailReconcileTimer = 0;
let fsmSelectionRenderSeq = 0;
let fsmSelectionRenderHandle = 0;
let fsmSelectionRenderKind = '';
const fsmDetailCache = new Map();
const fsmHistoryCache = new Map();
const fsmGraphViewportByMachine = new Map();
const FSM_WORKBENCH_SPLIT_ACTIONS = 'list_all / get_state / get_history / get_state_events';

function renderFsmKitPage() {
    $pageBody.classList.add('content-body--fsmkit');
    clearMetrics();
    setHero(
        t('fsmkit.title'),
        t('fsmkit.subtitle'),
        t('fsmkit.tab'),
        'fsm',
        `<button class="btn btn-primary btn-sm" onclick="refreshFsmKit()">${t('common.refresh')}</button>`
    );

    clearTabs();
    loadFsmWorkbench({ preferInPlace: false });
}

async function refreshFsmKit() { loadFsmWorkbench({ preferInPlace: true }); }

async function refreshFsmKitReactive(event) {
    const reason = event?.payload?.event ?? event?.type ?? '';
    const isTelemetry = reason === 'telemetry_update' || event?.type === 'telemetry_update';
    const isSnapshotUpdated = reason === 'snapshot_updated';

    if (isTelemetry || isSnapshotUpdated) {
        await refreshFsmWorkbenchCurrentState(event);
        if (isSnapshotUpdated) {
            scheduleFsmDetailReconcile(reason);
        }
        return;
    }

    await loadFsmWorkbench({ preferInPlace: true });
}

async function refreshFsmWorkbenchCurrentState(event) {
    if (!invoke || !connected || activePage !== 'fsmkit') return;
    const requestSeq = ++fsmWorkbenchLoadSeq;
    try {
        const fsms = await fetchFsmList();
        if (!isCurrentFsmWorkbenchLoad(requestSeq)) return;
        applyFsmWorkbenchListSnapshot(fsms, true);
    } catch (_) {
        // 响应式刷新尽力而为；显式刷新仍会报告命令桥错误。
    }
}

function applyFsmWorkbenchListSnapshot(fsms, preferInPlace = true) {
    if (!Array.isArray(fsms)) return;
    fsmListCache = fsms;

    if (!fsms.length) {
        selectedFsmName = null;
        const emptyMessage = t('fsmkit.no_active_machines');
        if (preferInPlace && updateFsmWorkbenchSummaryView([], null, null, emptyMessage)) {
            return;
        }
        renderOrUpdateFsmWorkbench([], null, null, [], emptyMessage, preferInPlace);
        return;
    }

    if (!selectedFsmName || !fsms.some(f => f.name === selectedFsmName)) {
        selectedFsmName = fsms[0].name;
    }

    const selectedMeta = fsms.find(f => f.name === selectedFsmName) ?? fsms[0];
    const liveDetail = mergeFsmRealtimeDetail(selectedMeta);
    const history = fsmHistoryCache.get(selectedMeta.name) ?? [];
    renderOrUpdateFsmWorkbench(fsms, selectedMeta, liveDetail, history, '', preferInPlace);
}

function mergeFsmRealtimeDetail(selectedMeta) {
    const fsmName = selectedMeta?.name ?? selectedFsmName ?? '';
    const cachedDetail = fsmName ? (fsmDetailCache.get(fsmName) ?? {}) : {};

    const liveDetail = {
        ...cachedDetail,
        ...selectedMeta,
        fsmName: cachedDetail.fsmName ?? selectedMeta?.name ?? fsmName,
        currentState: selectedMeta?.currentState ?? cachedDetail.currentState,
        currentStateId: selectedMeta?.currentStateId ?? cachedDetail.currentStateId,
        machineState: selectedMeta?.machineState ?? cachedDetail.machineState,
        stateCount: selectedMeta?.stateCount ?? cachedDetail.stateCount,
    };
    liveDetail.states = mergeFsmRealtimeStateTree(cachedDetail.states, liveDetail.currentStateId);
    if (fsmName) fsmDetailCache.set(fsmName, liveDetail);
    return liveDetail;
}

function mergeFsmRealtimeStateTree(states, currentStateId) {
    if (!Array.isArray(states)) return states;
    const targetId = Number(currentStateId);
    if (!Number.isFinite(targetId)) return states;

    return states.map(state => {
        const next = { ...state, isCurrent: Number(state?.id) === targetId };
        if (Array.isArray(state?.children)) {
            next.children = mergeFsmRealtimeStateTree(state.children, currentStateId);
        }
        return next;
    });
}

function scheduleFsmDetailReconcile(reason = '') {
    if (!selectedFsmName || activePage !== 'fsmkit') return;
    const now = Date.now();
    const elapsed = now - fsmLastDetailReconcileAt;
    const interactionDelay = getKitInteractionRemainingMs();
    if (elapsed >= FSM_DETAIL_RECONCILE_MS && interactionDelay <= 0) {
        fsmLastDetailReconcileAt = now;
        const requestSeq = fsmWorkbenchLoadSeq;
        refreshSelectedFsmWorkbenchDetail(selectedFsmName, requestSeq, fsmListCache).catch(() => {});
        return;
    }

    if (fsmDetailReconcileTimer) return;
    fsmDetailReconcileTimer = setTimeout(() => {
        fsmDetailReconcileTimer = 0;
        if (!selectedFsmName || activePage !== 'fsmkit') return;
        if (getKitInteractionRemainingMs() > 0) {
            scheduleFsmDetailReconcile(reason);
            return;
        }
        fsmLastDetailReconcileAt = Date.now();
        const requestSeq = fsmWorkbenchLoadSeq;
        refreshSelectedFsmWorkbenchDetail(selectedFsmName, requestSeq, fsmListCache).catch(() => {});
    }, Math.max(80, FSM_DETAIL_RECONCILE_MS - elapsed, interactionDelay + 80));
}
