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
    const cachedDetail = fsmDetailCache.get(selectedMeta.name) ?? selectedMeta;
    if (preferInPlace && updateFsmWorkbenchSummaryView(fsms, selectedMeta, cachedDetail, '')) {
        return;
    }
    const history = fsmHistoryCache.get(selectedMeta.name) ?? [];
    renderOrUpdateFsmWorkbench(fsms, selectedMeta, cachedDetail, history, '', preferInPlace);
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

// 拉取 FSM 列表，刷新缓存与指标。返回 fsms 数组（失败返回 null）。
async function fetchFsmList() {
    const telemetryFsms = await fetchFsmTelemetryList();
    if (telemetryFsms) {
        fsmListCache = telemetryFsms;
        return telemetryFsms;
    }

    const snapshotFsms = await fetchFsmSnapshotList();
    if (snapshotFsms) {
        fsmListCache = snapshotFsms;
        return snapshotFsms;
    }

    if (!canSendRuntimeKitCommand('FsmKit')) return [];

    return await fetchFsmCommandList() ?? [];
}

async function fetchFsmCommandList() {
    if (!canSendRuntimeKitCommand('FsmKit')) return null;
    const res = await sendKitCommandData('FsmKit', 'list_all');
    const fsms = Array.isArray(res?.fsms) ? res.fsms : [];
    fsmListCache = fsms;
    return fsms;
}

function isFsmNotFoundError(error) {
    const message = String(error?.message ?? error ?? '');
    return /FSM\s+'[^']+'\s+not found/.test(message);
}

async function reconcileFsmWorkbenchAfterMissingSelection(missingFsmName, requestSeq) {
    if (!isCurrentFsmWorkbenchLoad(requestSeq) || selectedFsmName !== missingFsmName) return true;

    let latestFsms = null;
    try {
        latestFsms = await fetchFsmCommandList();
    } catch (_) {
        return false;
    }

    if (!isCurrentFsmWorkbenchLoad(requestSeq) || selectedFsmName !== missingFsmName) return true;
    if (!Array.isArray(latestFsms)) return false;
    if (latestFsms.some(f => f.name === missingFsmName)) return false;

    fsmDetailCache.delete(missingFsmName);
    fsmHistoryCache.delete(missingFsmName);

    if (!latestFsms.length) {
        selectedFsmName = null;
        fsmDetailCache.clear();
        fsmHistoryCache.clear();
        renderOrUpdateFsmWorkbench([], null, null, [], t('fsmkit.no_active_machines'), true);
        return true;
    }

    const selectedMeta = latestFsms.find(f => f.name === selectedFsmName) ?? latestFsms[0];
    selectedFsmName = selectedMeta?.name ?? null;
    renderFsmWorkbenchFromCache(latestFsms, selectedMeta, '', true);
    return true;
}

async function fetchFsmTelemetryList() {
    const engineId = getPreferredEngineId({ capability: 'telemetry' });
    if (!invoke || !engineId || engineId === '--') return null;
    try {
        const raw = await invoke('read_telemetry', { engineId, kit: 'FsmKit', name: 'state' });
        const envelope = JSON.parse(raw);
        if (envelope?.available !== true) return null;
        return BridgeDiagnostics?.extractFsmSnapshotList(envelope) ?? null;
    } catch (_) {
        return null;
    }
}

async function fetchFsmSnapshotList() {
    const engineId = getPreferredEngineId({ capability: 'snapshots' });
    if (!invoke || !engineId || engineId === '--') return null;
    try {
        const raw = await invoke('read_snapshot', { engineId, kit: 'FsmKit', snapshot: 'state' });
        const envelope = JSON.parse(raw);
        return BridgeDiagnostics?.extractFsmSnapshotList(envelope) ?? null;
    } catch (_) {
        return null;
    }
}

async function fetchFsmWorkbenchSnapshot(fsmName) {
    if (!invoke || !connected || !fsmName) return null;
    if (!canSendRuntimeKitCommand('FsmKit')) return null;
    try {
        const data = await sendKitCommandData('FsmKit', 'get_workbench_snapshot', { fsmName });
        const fsms = Array.isArray(data.fsms) ? data.fsms : [];
        const detail = data.selected ?? data.detail ?? null;
        const historyPayload = data.history ?? {};
        const history = Array.isArray(historyPayload)
            ? historyPayload
            : (Array.isArray(historyPayload.history) ? historyPayload.history : []);

        if (fsms.length) fsmListCache = fsms;
        if (fsmName && detail) fsmDetailCache.set(fsmName, detail);
        if (fsmName) fsmHistoryCache.set(fsmName, history);

        return { fsms, detail, history };
    } catch (_) {
        return null;
    }
}

async function loadFsmList() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('\u{1F504}', t('fsmkit.connect_to_view_list'));
        clearMetrics();
        return;
    }
    try {
        const fsms = await fetchFsmList();
        renderMetrics([
            { title: t('fsmkit.state_machine'), value: fsms.length },
            { title: t('fsmkit.running_label'), value: fsms.filter(f => f.machineState === 'Running').length },
        ]);
        if (!fsms.length) {
            if (!canSendRuntimeKitCommand('FsmKit')) {
                showRuntimeKitUnavailable('FsmKit', t('fsmkit.fsm_state_machine'));
                return;
            }
            $pageBody.innerHTML = emptyState('\u{1F504}', t('fsmkit.no_registered_machines'));
            return;
        }
        const rows = fsms.map(f => `
            <tr class="fsm-row" data-fsm="${encodeURIComponent(f.name ?? '')}">
                <td>${f.name ?? '--'}</td>
                <td>${f.currentState ?? '--'}</td>
                <td>${f.stateCount ?? '--'}</td>
                <td><span class="status-pill status-pill--${fsmStatePill(f.machineState)}">${f.machineState ?? '--'}</span></td>
            </tr>`).join('');
        $pageBody.innerHTML = panel('Registered FSMs',
            `<table class="data-table"><thead><tr><th>Name</th><th>Current State</th><th>States</th><th>Status</th></tr></thead><tbody>${rows}</tbody></table>`,
            '\u{1F4CB}');
        // 行点击 → 选中并跳到状态详情。
        $pageBody.querySelectorAll('.fsm-row').forEach(tr => {
            tr.style.cursor = 'pointer';
            tr.addEventListener('click', () => {
                selectedFsmName = decodeURIComponent(tr.dataset.fsm);
                activateFsmTab('fsm-state');
            });
        });
    } catch (e) {
        if (!canSendRuntimeKitCommand('FsmKit')) {
            showRuntimeKitUnavailable('FsmKit', t('fsmkit.fsm_state_machine'));
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '\u{26A0}');
    }
}

function fsmStatePill(state) {
    if (state === 'Running') return 'success';
    if (state === 'Suspend') return 'warning';
    return 'info';
}

// 程序化激活某个 FsmKit tab（同步 tab-bar 高亮 + 触发加载）
function activateFsmTab(tabId) {
    $tabBar.querySelectorAll('.tab-button').forEach(b =>
        b.classList.toggle('active', b.dataset.tab === tabId));
    activeTab = tabId;
    syncTabActiveIndicator(tabId);
    if (tabId === 'fsm-graph') loadFsmGraph();
    else if (tabId === 'fsm-state') loadFsmState();
    else if (tabId === 'fsm-history') loadFsmHistory();
    else loadFsmList();
}

// FSM 选择下拉（状态详情 / 历史共用）
function fsmSelector(onChange) {
    if (!fsmListCache.length) return '';
    const opts = fsmListCache.map(f => {
        const n = f.name ?? '';
        return `<option value="${n}"${n === selectedFsmName ? ' selected' : ''}>${n}</option>`;
    }).join('');
    return `<div class="fsm-selector-row">
        <span class="info-label">FSM</span>
        <select id="fsm-select" class="cmd-select">${opts}</select>
    </div>`;
}

async function loadFsmState() {
    if (!invoke || !connected) { $pageBody.innerHTML = emptyState('\u{1F504}', t('fsmkit.connect_first')); clearMetrics(); return; }
    try {
        if (!fsmListCache.length) await fetchFsmList();
        if (!fsmListCache.length) {
            if (!canSendRuntimeKitCommand('FsmKit')) {
                showRuntimeKitUnavailable('FsmKit', t('fsmkit.fsm_state_machine'));
                return;
            }
            $pageBody.innerHTML = emptyState('\u{1F504}', t('fsmkit.no_registered_machines_short'));
            return;
        }
        if (!selectedFsmName || !fsmListCache.some(f => f.name === selectedFsmName))
            selectedFsmName = fsmListCache[0].name;

        const d = await sendKitCommandData('FsmKit', 'get_state', { fsmName: selectedFsmName });
        const detail = `<div class="info-row"><span class="info-label">FSM</span><span class="info-value info-value--highlight">${d.fsmName ?? '--'}</span></div>
               <div class="info-row"><span class="info-label">Current State</span><span class="info-value">${d.currentState ?? '--'}</span></div>
               <div class="info-row"><span class="info-label">State Id</span><span class="info-value">${d.currentStateId ?? '--'}</span></div>
               <div class="info-row"><span class="info-label">Machine State</span><span class="info-value">${d.machineState ?? '--'}</span></div>
               <div class="info-row"><span class="info-label">State Count</span><span class="info-value">${d.stateCount ?? '--'}</span></div>`;
        $pageBody.innerHTML = panel(t('fsmkit.state_detail'), fsmSelector() + detail, '\u{1F4CC}');
        bindFsmSelect(loadFsmState);
    } catch (e) {
        if (!canSendRuntimeKitCommand('FsmKit')) {
            showRuntimeKitUnavailable('FsmKit', t('fsmkit.fsm_state_machine'));
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '\u{26A0}');
    }
}

async function loadFsmHistory() {
    if (!invoke || !connected) { $pageBody.innerHTML = emptyState('\u{1F4DC}', t('fsmkit.connect_first')); clearMetrics(); return; }
    try {
        if (!fsmListCache.length) await fetchFsmList();
        if (!fsmListCache.length) {
            if (!canSendRuntimeKitCommand('FsmKit')) {
                showRuntimeKitUnavailable('FsmKit', t('fsmkit.fsm_state_machine'));
                return;
            }
            $pageBody.innerHTML = emptyState('\u{1F4DC}', t('fsmkit.no_registered_machines_short'));
            return;
        }
        if (!selectedFsmName || !fsmListCache.some(f => f.name === selectedFsmName))
            selectedFsmName = fsmListCache[0].name;

        const res = await sendKitCommandData('FsmKit', 'get_history', { fsmName: selectedFsmName });
        const history = res?.history ?? [];
        let body;
        if (!history.length) {
            body = emptyState('\u{1F4DC}', t('fsmkit.no_history'));
        } else {
            body = `<div class="fsm-timeline">` + history.map(h => `
                <div class="fsm-timeline-item">
                    <span class="fsm-timeline-time">${h.time ?? ''}</span>
                    <span class="fsm-timeline-from">${h.from ?? '?'}</span>
                    <span class="fsm-timeline-arrow">→</span>
                    <span class="fsm-timeline-to">${h.to ?? '?'}</span>
                </div>`).join('') + `</div>`;
        }
        $pageBody.innerHTML = panel(t('fsmkit.transition_history'), fsmSelector() + body, '\u{1F4DC}');
        bindFsmSelect(loadFsmHistory);
    } catch (e) {
        if (!canSendRuntimeKitCommand('FsmKit')) {
            showRuntimeKitUnavailable('FsmKit', t('fsmkit.fsm_state_machine'));
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '\u{26A0}');
    }
}

// 绑定 FSM 下拉的 change 事件，切换后重载当前视图
function bindFsmSelect(reload) {
    const sel = document.getElementById('fsm-select');
    if (!sel) return;
    sel.addEventListener('change', () => {
        selectedFsmName = sel.value;
        reload();
    });
}

async function loadFsmWorkbench({ preferInPlace = true } = {}) {
    const requestSeq = ++fsmWorkbenchLoadSeq;
    if (!invoke || !connected) {
        if (!isCurrentFsmWorkbenchLoad(requestSeq)) return;
        clearMetrics();
        fsmListCache = [];
        selectedFsmName = null;
        fsmDetailCache.clear();
        fsmHistoryCache.clear();
        renderOrUpdateFsmWorkbench([], null, null, [], '请连接 Unity 引擎后查看运行中的状态机。', preferInPlace);
        return;
    }

    try {
        const fsms = await fetchFsmList();
        if (!isCurrentFsmWorkbenchLoad(requestSeq)) return;

        if (!fsms.length) {
            selectedFsmName = null;
            if (!canSendRuntimeKitCommand('FsmKit')) {
                showRuntimeKitUnavailable('FsmKit', t('fsmkit.fsm_state_machine'));
                return;
            }
            renderOrUpdateFsmWorkbench([], null, null, [], t('fsmkit.no_active_machines'), preferInPlace);
            return;
        }

        if (!selectedFsmName || !fsms.some(f => f.name === selectedFsmName)) {
            selectedFsmName = fsms[0].name;
        }

        const requestedFsmName = selectedFsmName;
        const selectedMeta = fsms.find(f => f.name === requestedFsmName) ?? fsms[0];
        renderFsmWorkbenchFromCache(fsms, selectedMeta, '', preferInPlace);
        await refreshSelectedFsmWorkbenchDetail(requestedFsmName, requestSeq, fsms);
    } catch (e) {
        if (!isCurrentFsmWorkbenchLoad(requestSeq)) return;
        clearMetrics();
        if (!canSendRuntimeKitCommand('FsmKit')) {
            showRuntimeKitUnavailable('FsmKit', t('fsmkit.fsm_state_machine'));
            return;
        }
        $pageBody.innerHTML = panel('FsmKit 错误', `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function isCurrentFsmWorkbenchLoad(requestSeq) {
    return activePage === 'fsmkit' && requestSeq === fsmWorkbenchLoadSeq;
}

async function fetchFsmStateDetail(fsmName) {
    const detail = await sendKitCommandData('FsmKit', 'get_state', { fsmName });
    if (fsmName) fsmDetailCache.set(fsmName, detail);
    return detail;
}

async function fetchFsmHistory(fsmName) {
    const res = await sendKitCommandData('FsmKit', 'get_history', { fsmName });
    const history = res?.history ?? [];
    if (fsmName) fsmHistoryCache.set(fsmName, history);
    return history;
}

function renderFsmWorkbenchFromCache(fsms, selectedMeta, emptyMessage = '', preferInPlace = true) {
    const fsmName = selectedMeta?.name ?? selectedFsmName ?? '';
    const detail = fsmDetailCache.get(fsmName) ?? selectedMeta;
    const history = fsmHistoryCache.get(fsmName) ?? [];
    renderOrUpdateFsmWorkbench(fsms, selectedMeta, detail, history, emptyMessage, preferInPlace);
}

function updateFsmWorkbenchSummaryView(fsms, selectedMeta, detail, emptyMessage = '') {
    const root = $pageBody.querySelector('[data-fsm-workbench="root"]');
    if (!root) return false;

    fsmWorkbenchListEmptyMessage = emptyMessage;
    const selectedTitle = root.querySelector('[data-fsm-region="detail-title"]');
    if (selectedTitle) {
        selectedTitle.textContent = selectedMeta ? (selectedMeta.name ?? t('fsmkit.state_detail')) : t('fsmkit.no_selection');
    }

    const countBadge = root.querySelector('[data-fsm-region="count"]');
    if (countBadge) {
        countBadge.textContent = formatFsmListCount(fsms);
    }

    const listChanged = replaceFsmWorkbenchRegion(root, 'list', renderFsmListHtml(fsms, emptyMessage));
    const detailChanged = replaceFsmWorkbenchRegion(root, 'detail', renderFsmDetailRegionHtml(selectedMeta, detail));

    syncFsmWorkbenchSearchControls();
    if (listChanged) {
        bindFsmWorkbenchList();
    }
    if (detailChanged) {
        scheduleFsmCurrentStateFit();
    }
    return true;
}

async function refreshSelectedFsmWorkbenchDetail(requestedFsmName, requestSeq, fsms = fsmListCache) {
    try {
        const snapshot = await fetchFsmWorkbenchSnapshot(requestedFsmName);
        if (!isCurrentFsmWorkbenchLoad(requestSeq) || selectedFsmName !== requestedFsmName) return;
        if (snapshot) {
            const snapshotFsms = snapshot.fsms?.length ? snapshot.fsms : (fsmListCache.length ? fsmListCache : fsms);
            const selectedMeta = snapshotFsms.find(f => f.name === requestedFsmName)
                ?? fsms.find(f => f.name === requestedFsmName)
                ?? { name: requestedFsmName };
            renderOrUpdateFsmWorkbench(snapshotFsms, selectedMeta, snapshot.detail ?? selectedMeta, snapshot.history ?? [], '', true);
            return;
        }

        const [detail, history] = await Promise.all([
            fetchFsmStateDetail(requestedFsmName),
            fetchFsmHistory(requestedFsmName)
        ]);
        if (!isCurrentFsmWorkbenchLoad(requestSeq) || selectedFsmName !== requestedFsmName) return;

        const latestFsms = fsmListCache.length ? fsmListCache : fsms;
        const selectedMeta = latestFsms.find(f => f.name === requestedFsmName)
            ?? fsms.find(f => f.name === requestedFsmName)
            ?? { name: requestedFsmName };
        renderOrUpdateFsmWorkbench(latestFsms, selectedMeta, detail, history, '', true);
    } catch (e) {
        if (isFsmNotFoundError(e) && await reconcileFsmWorkbenchAfterMissingSelection(requestedFsmName, requestSeq)) {
            return;
        }

        throw e;
    }
}

function renderOrUpdateFsmWorkbench(fsms, selectedMeta, detail, history, emptyMessage = '', preferInPlace = true) {
    fsmWorkbenchListEmptyMessage = emptyMessage;
    const nextSignature = makeFsmWorkbenchSignature(fsms, selectedMeta, detail, history, emptyMessage);
    if (preferInPlace && nextSignature === fsmWorkbenchRenderSignature) {
        return;
    }

    fsmWorkbenchRenderSignature = nextSignature;
    const updateInfo = preferInPlace ? updateFsmWorkbenchView(fsms, selectedMeta, detail, history, emptyMessage) : null;
    if (!updateInfo) {
        renderFsmWorkbenchShell(fsms, selectedMeta, detail, history, emptyMessage);
        syncFsmWorkbenchSearchControls();
        bindFsmWorkbenchSearch();
        bindFsmWorkbenchList();
        bindFsmGraphInteractions();
        scheduleFsmCurrentStateFit();
        return;
    }

    syncFsmWorkbenchSearchControls();
    if (updateInfo.listChanged) {
        bindFsmWorkbenchList();
    }
    if (updateInfo.matrixChanged) {
        bindFsmGraphInteractions();
    }
    if (updateInfo.detailChanged) {
        scheduleFsmCurrentStateFit();
    }
}

function updateFsmWorkbenchView(fsms, selectedMeta, detail, history, emptyMessage = '') {
    const root = $pageBody.querySelector('[data-fsm-workbench="root"]');
    if (!root) return false;
    const listChanged = replaceFsmWorkbenchRegion(root, 'list', renderFsmListHtml(fsms, emptyMessage));
    const detailChanged = replaceFsmWorkbenchRegion(root, 'detail', renderFsmDetailRegionHtml(selectedMeta, detail));

    const selectedTitle = root.querySelector('[data-fsm-region="detail-title"]');
    if (selectedTitle) {
        selectedTitle.textContent = selectedMeta ? (selectedMeta.name ?? t('fsmkit.state_detail')) : t('fsmkit.no_selection');
    }

    const countBadge = root.querySelector('[data-fsm-region="count"]');
    if (countBadge) {
        countBadge.textContent = formatFsmListCount(fsms);
    }

    captureFsmGraphViewport();
    const matrixChanged = replaceFsmWorkbenchRegion(root, 'matrix', renderFsmMatrixHtml(selectedMeta, detail, history));
    const insightsChanged = replaceFsmWorkbenchRegion(root, 'insights', renderFsmInsightsHtml(selectedMeta, detail, history));
    const historyChanged = replaceFsmWorkbenchRegion(root, 'history', renderFsmHistoryHtml(selectedMeta, history));
    return {
        listChanged: listChanged,
        detailChanged: detailChanged,
        matrixChanged: matrixChanged,
        insightsChanged: insightsChanged,
        historyChanged: historyChanged,
    };
}

function replaceFsmWorkbenchRegion(root, region, html) {
    const target = root.querySelector(`[data-fsm-region="${region}"]`);
    if (!target) return false;
    if (target.innerHTML === html) return false;
    target.innerHTML = html;
    return true;
}

function renderFsmWorkbenchShell(fsms, selectedMeta, detail, history, emptyMessage = '') {
    $pageBody.innerHTML = `
        <div class="fsm-workbench" data-fsm-workbench="root">
            <aside class="tool-pane fsm-index-pane">
                <div class="tool-pane-header">
                    <div>
                        <div class="tool-pane-title">${t('fsmkit.active_machines')}</div>
                    </div>
                    <span class="status-pill status-pill--info" data-fsm-region="count">${formatFsmListCount(fsms)}</span>
                </div>
                <div class="tool-pane-body">
                    ${renderFsmListSearchHtml()}
                    <div class="fsm-list" data-fsm-region="list">${renderFsmListHtml(fsms, emptyMessage)}</div>
                </div>
            </aside>
            <section class="fsm-observe-stage">
                <div class="tool-pane fsm-detail-pane">
                    <div class="tool-pane-header">
                        <div>
                            <div class="tool-pane-title" data-fsm-region="detail-title">${selectedMeta ? escapeHtml(selectedMeta.name ?? t('fsmkit.state_detail')) : t('fsmkit.no_selection')}</div>
                        </div>
                    </div>
                    <div class="tool-pane-body" data-fsm-region="detail">${renderFsmDetailRegionHtml(selectedMeta, detail)}</div>
                </div>
                <div class="tool-pane fsm-flow-pane">
                    <div class="tool-pane-header">
                        <div>
                            <div class="tool-pane-title">${t('fsmkit.state_flow_graph')}</div>
                        </div>
                    </div>
                    <div class="tool-pane-body" data-fsm-region="matrix">${renderFsmMatrixHtml(selectedMeta, detail, history)}</div>
                </div>
            </section>
            <aside class="fsm-observe-side">
                <div class="tool-pane">
                    <div class="tool-pane-header">
                        <div>
                            <div class="tool-pane-title">${t('fsmkit.event_insights')}</div>
                        </div>
                    </div>
                    <div class="tool-pane-body" data-fsm-region="insights">${renderFsmInsightsHtml(selectedMeta, detail, history)}</div>
                </div>
                <div class="tool-pane fsm-history-pane">
                    <div class="tool-pane-header">
                        <div>
                            <div class="tool-pane-title">${t('fsmkit.transition_history')}</div>
                        </div>
                    </div>
                    <div class="tool-pane-body" data-fsm-region="history">${renderFsmHistoryHtml(selectedMeta, history)}</div>
                </div>
            </aside>
        </div>`;
    scheduleHeroActionPromotion();
}

function renderFsmListHtml(fsms, emptyMessage = '') {
    if (!fsms.length) return emptyState('○', emptyMessage || t('fsmkit.no_displayable_machines'));
    const filteredFsms = filterFsmListItems(fsms);
    if (!filteredFsms.length) {
        return emptyState('⌕', t('fsmkit.search_machines', escapeHtml(fsmListSearchTerm.trim())));
    }

    return filteredFsms.map(f => {
        const name = f.name ?? '--';
        const active = name === selectedFsmName ? ' active' : '';
        const currentState = f.currentState ?? '--';
        const stateCount = f.stateCount ?? '--';
        return `<button class="fsm-list-item${active}" data-fsm="${encodeURIComponent(name)}" type="button" aria-pressed="${active ? 'true' : 'false'}">
            <div class="fsm-list-content">
                <div class="fsm-list-name" title="${escapeHtml(name)}">${escapeHtml(name)}</div>
                <div class="fsm-list-meta" title="${escapeHtml(currentState)} · ${t('fsmkit.state_count_label', escapeHtml(stateCount))}">
                    <span class="fsm-list-state">${escapeHtml(currentState)}</span>
                    <span class="fsm-list-separator">·</span>
                    <span class="fsm-list-count">${t('fsmkit.state_count_label', escapeHtml(stateCount))}</span>
                </div>
            </div>
            <span class="status-pill status-pill--${fsmStatePill(f.machineState)}">${escapeHtml(f.machineState ?? '--')}</span>
        </button>`;
    }).join('');
}

function renderFsmListSearchHtml() {
    const hasQuery = !!fsmListSearchTerm.trim();
    return `<div class="fsm-list-search" role="search">
        ${svgIcon('search', 'fsm-list-search__icon')}
        <input class="fsm-list-search__input" data-fsm-search-input type="search"
               value="${escapeHtml(fsmListSearchTerm)}"
               placeholder="${t('fsmkit.search_placeholder')}"
               autocomplete="off" spellcheck="false" aria-label="${t('common.search')}">
        <button class="fsm-list-search__clear${hasQuery ? '' : ' is-empty'}" data-fsm-search-clear
                type="button" aria-label="${t('common.clear_search')}" ${hasQuery ? '' : 'disabled'}>×</button>
    </div>`;
}

function normalizeFsmListSearchTerm(value) {
    return String(value ?? '').trim().toLowerCase();
}

function filterFsmListItems(fsms) {
    const query = normalizeFsmListSearchTerm(fsmListSearchTerm);
    if (!query) return fsms;

    const terms = query.split(/\s+/).filter(Boolean);
    return fsms.filter(f => {
        const haystack = [
            f.name,
            f.currentState,
            f.machineState,
            f.stateCount,
        ].map(v => String(v ?? '').toLowerCase()).join('\n');
        return terms.every(term => haystack.includes(term));
    });
}

function formatFsmListCount(fsms) {
    const total = fsms.length;
    if (!normalizeFsmListSearchTerm(fsmListSearchTerm)) return String(total);
    return `${filterFsmListItems(fsms).length}/${total}`;
}

function renderFsmDetailRegionHtml(selectedMeta, detail) {
    return selectedMeta
        ? renderFsmDetail(detail ?? selectedMeta, selectedMeta)
        : emptyState('○', t('fsmkit.no_selection_hint'));
}

function renderFsmMatrixHtml(selectedMeta, detail, history) {
    const currentState = selectedMeta?.currentState ?? detail?.currentState ?? null;
    const graph = selectedMeta ? buildFsmGraph(history ?? [], currentState, detail?.states ?? []) : null;
    if (!graph || !graph.nodes.length) {
        return `<div class="fsm-flow-placeholder">${t('fsmkit.graph_placeholder')}</div>`;
    }

    return `<div class="fsm-flow-shell">
        <div class="fsm-graph-toolbar">
            <div>
                <div class="fsm-graph-label">${t('fsmkit.state_flow_graph')}</div>
                <span class="fsm-graph-stat">${t('fsmkit.visible_states', graph.nodes.length, graph.edges.length)}</span>
            </div>
            <div class="fsm-viewport-controls">
                <button class="btn btn-secondary btn-sm" id="fsm-graph-zoom-out" type="button">-</button>
                <span id="fsm-graph-zoom-label" class="fsm-graph-zoom-label">100%</span>
                <button class="btn btn-secondary btn-sm" id="fsm-graph-zoom-in" type="button">+</button>
                <button class="btn btn-secondary btn-sm" id="fsm-graph-fit" type="button">${t("fsmkit.fit")}</button>
            </div>
        </div>
        <div class="fsm-graph-scroll" id="fsm-graph-scroll" data-fsm-name="${escapeHtml(selectedMeta.name ?? selectedFsmName)}">${renderFsmGraphCanvas(graph)}</div>
        ${fsmGraphLegend(graph)}
    </div>`;
}

function renderFsmHistoryHtml(selectedMeta, history) {
    if (!history || !history.length) {
        return emptyState('↔', selectedMeta ? t('fsmkit.no_history_short') : t('fsmkit.no_selection'));
    }

    return `<div class="fsm-history-scroll"><div class="fsm-timeline">` + history.slice(-120).reverse().map((h, index) => `
        <div class="fsm-timeline-item">
            <span class="fsm-timeline-dot${index === 0 ? ' fsm-timeline-dot--latest' : ''}"></span>
            <div class="fsm-timeline-card">
                <span class="fsm-timeline-card__time">${escapeHtml(h.time ?? '')}</span>
                <span class="fsm-timeline-card__route">
                    <span class="fsm-timeline-from">${escapeHtml(h.from ?? '?')}</span>
                    <span class="fsm-timeline-arrow">→</span>
                    <span class="fsm-timeline-to">${escapeHtml(h.to ?? '?')}</span>
                </span>
            </div>
        </div>`).join('') + `</div></div>`;
}

function renderFsmDetail(detail, selectedMeta) {
    if (detail?.error) {
        return `<span style="color:var(--error)">${escapeHtml(detail.error)}</span>`;
    }
    const fsmName = detail.fsmName ?? selectedMeta?.name ?? '--';
    const currentState = detail.currentState ?? selectedMeta?.currentState ?? '--';
    const machineState = detail.machineState ?? selectedMeta?.machineState ?? '--';
    const stateId = detail.currentStateId ?? '--';
    const stateCount = detail.stateCount ?? selectedMeta?.stateCount ?? '--';
    return `<div class="fsm-detail-grid">
        <div class="fsm-state-card">
            <div class="fsm-state-card__signal" aria-hidden="true"></div>
            <div class="fsm-state-card__content">
                <span class="fsm-state-card__label">${t("fsmkit.current_state")}</span>
                <strong class="fsm-state-card__value" data-fsm-current-state-value title="${escapeHtml(currentState)}">${escapeHtml(currentState)}</strong>
                <span class="fsm-state-card__meta" title="${escapeHtml(fsmName)} · State Id ${escapeHtml(stateId)}">${escapeHtml(fsmName)} · State Id ${escapeHtml(stateId)}</span>
            </div>
            <span class="status-pill status-pill--${fsmStatePill(machineState)}">${escapeHtml(machineState)}</span>
        </div>
        ${fsmDetailChip(t('fsmkit.state_count'), stateCount)}
        ${fsmDetailChip('Machine State', machineState)}
        ${fsmDetailChip('FSM', fsmName)}
    </div>`;
}

function fsmDetailChip(label, value) {
    return `<div class="fsm-detail-chip"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></div>`;
}

let fsmCurrentStateFitFrame = 0;

function scheduleFsmCurrentStateFit() {
    if (fsmCurrentStateFitFrame) {
        cancelAnimationFrame(fsmCurrentStateFitFrame);
    }
    fsmCurrentStateFitFrame = requestAnimationFrame(() => {
        fsmCurrentStateFitFrame = 0;
        fitFsmCurrentStateValueText();
    });
}

function fitFsmCurrentStateValueText() {
    const value = $pageBody.querySelector('[data-fsm-current-state-value]');
    if (!value) return;

    const container = value.parentElement;
    if (!container) return;

    const maxWidth = container.clientWidth;
    if (maxWidth <= 0) return;

    value.style.fontSize = '';

    const computed = window.getComputedStyle(value);
    const baseFontSize = Number.parseFloat(computed.fontSize) || 18;
    const minFontSize = Math.max(12, baseFontSize * 0.72);
    let low = minFontSize;
    let high = baseFontSize;
    let best = minFontSize;

    value.style.whiteSpace = 'nowrap';
    value.style.overflow = 'hidden';
    value.style.textOverflow = 'clip';

    for (let i = 0; i < 8; i++) {
        const size = (low + high) / 2;
        value.style.fontSize = size.toFixed(2) + 'px';
        if (value.scrollWidth <= maxWidth) {
            best = size;
            low = size;
        } else {
            high = size;
        }
    }

    value.style.fontSize = best.toFixed(2) + 'px';
}

function renderFsmStateCards(graph) {
    const sortedNodes = [...graph.nodes].sort((a, b) => {
        if (a.id === graph.currentState) return -1;
        if (b.id === graph.currentState) return 1;
        return b.count - a.count || a.id.localeCompare(b.id);
    });

    const topLevel = `<div class="fsm-state-grid">` + sortedNodes.map(node => {
        const currentClass = node.id === graph.currentState ? ' fsm-state-tile--current' : '';
        const compositeClass = node.isComposite ? ' fsm-state-tile--composite' : '';
        return `<div class="fsm-state-tile${currentClass}${compositeClass}">
            <span class="fsm-state-tile__name">${escapeHtml(node.id)}</span>
            <span class="fsm-state-tile__meta">${node.id === graph.currentState ? t('fsmkit.graph_node_role_current') : t('fsmkit.graph_node_role_visited')} · ${t('fsmkit.visits', escapeHtml(node.count))}${node.isComposite ? ' · ' + t('fsmkit.hierarchical_machine') : ''}</span>
        </div>`;
    }).join('') + `</div>`;
    return topLevel + renderFsmCompositeStateCards(graph.composites ?? []);
}

function renderFsmCompositeStateCards(composites) {
    const rows = flattenFsmCompositeStates(composites);
    if (!rows.length) return '';

    return `<div class="fsm-composite-board">
        <div class="fsm-section-label">${t('fsmkit.hierarchical_machine')}</div>
        ${rows.map(row => {
            const node = row.node;
            const children = Array.isArray(node.children) ? node.children : [];
            return `<section class="fsm-composite-card">
                <div class="fsm-composite-card__header">
                    <strong>${escapeHtml(row.path)}</strong>
                    <span>${escapeHtml(node.childMachineName || t('fsmkit.child_machine'))} · ${escapeHtml(node.machineState || '--')}</span>
                </div>
                <div class="fsm-composite-card__children">
                    ${children.length ? children.map(child => {
                        const name = child?.name ?? child?.stateType ?? '--';
                        const current = child?.isCurrent ? ' fsm-composite-child--current' : '';
                        const nested = child?.isComposite ? ' fsm-composite-child--nested' : '';
                        const suffix = child?.isComposite ? ' / ' + t('fsmkit.child_machine') : '';
                        return `<span class="fsm-composite-child${current}${nested}">${escapeHtml(name + suffix)}</span>`;
                    }).join('') : '<span class="fsm-composite-child">' + t('fsmkit.no_child_snapshot') + '</span>'}
                </div>
            </section>`;
        }).join('')}
    </div>`;
}

function flattenFsmCompositeStates(composites) {
    const rows = [];

    function visit(node, prefix = '') {
        if (!node) return;
        const nodeName = node.id ?? node.name ?? node.stateType ?? 'State';
        const path = prefix ? `${prefix} / ${nodeName}` : nodeName;
        rows.push({ node, path });

        for (const child of node.children ?? []) {
            if (!child?.isComposite && !(Array.isArray(child?.children) && child.children.length)) continue;
            visit({
                id: child.name ?? child.stateType ?? 'State',
                childMachineName: child.childMachineName ?? '',
                machineState: child.machineState ?? '',
                children: Array.isArray(child.children) ? child.children : [],
            }, path);
        }
    }

    for (const node of composites ?? []) visit(node);
    return rows;
}

function renderFsmInsightsHtml(selectedMeta, detail, history) {
    if (!selectedMeta) {
        return emptyState('○', t('fsmkit.select_machine_hint'));
    }

    const summary = summarizeFsmHistory(history ?? []);
    const currentState = detail?.currentState ?? selectedMeta.currentState ?? '--';
    const machineState = detail?.machineState ?? selectedMeta.machineState ?? '--';
    const latest = summary.latest
        ? `${summary.latest.from ?? '?'} → ${summary.latest.to ?? '?'}`
        : t('fsmkit.no_transition');
    const hottest = summary.hottest
        ? `${summary.hottest.from} → ${summary.hottest.to} · ${t('fsmkit.visits', summary.hottest.count)}`
        : t('fsmkit.no_hot_path');
    const loopText = summary.selfLoopCount > 0
        ? t('fsmkit.self_loop_count', summary.selfLoopCount)
        : t('fsmkit.no_self_loop');

    return `<div class="fsm-insight-list">
        ${fsmInsightRow(t('fsmkit.current_state_label'), currentState, machineState)}
        ${fsmInsightRow(t('fsmkit.latest_transition'), latest, summary.latest?.time ?? '--')}
        ${fsmInsightRow(t('fsmkit.hot_path'), hottest, t('fsmkit.hot_path_hint'))}
        ${fsmInsightRow(t('fsmkit.loop_risk'), loopText, t('fsmkit.history_states', summary.uniqueStateCount))}
    </div>`;
}

function fsmInsightRow(label, value, hint) {
    return `<div class="fsm-insight-item">
        <span class="fsm-insight-item__label">${escapeHtml(label)}</span>
        <strong>${escapeHtml(value)}</strong>
        <span class="fsm-insight-item__hint">${escapeHtml(hint ?? '')}</span>
    </div>`;
}

function summarizeFsmHistory(history) {
    const edgeCounts = new Map();
    const states = new Set();
    let selfLoopCount = 0;

    for (const h of history) {
        if (h.from) states.add(h.from);
        if (h.to) states.add(h.to);
        if (!h.from || !h.to) continue;
        if (h.from === h.to) selfLoopCount++;
        const key = h.from + '→' + h.to;
        const count = edgeCounts.get(key) ?? { from: h.from, to: h.to, count: 0 };
        count.count++;
        edgeCounts.set(key, count);
    }

    let hottest = null;
    for (const edge of edgeCounts.values()) {
        if (!hottest || edge.count > hottest.count) hottest = edge;
    }

    return {
        latest: history.length ? history[history.length - 1] : null,
        hottest,
        selfLoopCount,
        uniqueStateCount: states.size,
    };
}

function bindFsmWorkbenchList() {
    $pageBody.querySelectorAll('.fsm-list-item').forEach(btn => {
        if (btn.dataset.bound === '1') return;
        btn.dataset.bound = '1';
        btn.addEventListener('click', () => {
            selectFsmWorkbench(decodeURIComponent(btn.dataset.fsm));
        });
    });
}

function cancelFsmSelectionRender() {
    if (fsmSelectionRenderKind === 'idle' && fsmSelectionRenderHandle && typeof window.cancelIdleCallback === 'function') {
        cancelIdleCallback(fsmSelectionRenderHandle);
    } else if (fsmSelectionRenderHandle) {
        clearTimeout(fsmSelectionRenderHandle);
    }
    fsmSelectionRenderHandle = 0;
    fsmSelectionRenderKind = '';
}

function scheduleFsmSelectionRender(fsmName, requestSeq, fsms, selectedMeta) {
    const selectionSeq = ++fsmSelectionRenderSeq;
    cancelFsmSelectionRender();

    const run = () => {
        fsmSelectionRenderHandle = 0;
        fsmSelectionRenderKind = '';
        if (selectionSeq !== fsmSelectionRenderSeq) return;
        if (!isCurrentFsmWorkbenchLoad(requestSeq) || selectedFsmName !== fsmName) return;
        renderFsmWorkbenchFromCache(fsms, selectedMeta, '', true);
        if (!invoke || !connected) return;
        refreshSelectedFsmWorkbenchDetail(fsmName, requestSeq, fsms)
            .catch(e => {
                if (!isCurrentFsmWorkbenchLoad(requestSeq)) return;
                $pageBody.innerHTML = panel('FsmKit 错误', `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
            });
    };

    if (typeof window.requestIdleCallback === 'function') {
        fsmSelectionRenderKind = 'idle';
        fsmSelectionRenderHandle = requestIdleCallback(run, { timeout: FSM_SELECTION_RENDER_IDLE_TIMEOUT_MS });
        return;
    }

    fsmSelectionRenderKind = 'timeout';
    fsmSelectionRenderHandle = setTimeout(run, 48);
}

function selectFsmWorkbench(fsmName) {
    if (!fsmName || selectedFsmName === fsmName) return;

    selectedFsmName = fsmName;
    const requestSeq = ++fsmWorkbenchLoadSeq;
    markFsmListSelection(fsmName);

    const selectedMeta = fsmListCache.find(f => f.name === fsmName) ?? { name: fsmName };
    if (!updateFsmWorkbenchSummaryView(fsmListCache, selectedMeta, fsmDetailCache.get(fsmName) ?? selectedMeta, '')) {
        renderFsmWorkbenchFromCache(fsmListCache, selectedMeta, '', true);
    }
    scheduleFsmSelectionRender(fsmName, requestSeq, fsmListCache, selectedMeta);
}

function bindFsmWorkbenchSearch() {
    const input = $pageBody.querySelector('[data-fsm-search-input]');
    if (input && input.dataset.bound !== 'true') {
        input.dataset.bound = 'true';
        input.addEventListener('input', event => {
            fsmListSearchTerm = event.currentTarget.value;
            applyFsmWorkbenchSearch();
        });
        input.addEventListener('keydown', event => {
            if (event.key !== 'Escape') return;
            fsmListSearchTerm = '';
            event.currentTarget.value = '';
            applyFsmWorkbenchSearch();
        });
    }

    const clear = $pageBody.querySelector('[data-fsm-search-clear]');
    if (clear && clear.dataset.bound !== 'true') {
        clear.dataset.bound = 'true';
        clear.addEventListener('click', () => {
            fsmListSearchTerm = '';
            applyFsmWorkbenchSearch();
            $pageBody.querySelector('[data-fsm-search-input]')?.focus();
        });
    }
}

function applyFsmWorkbenchSearch() {
    const root = $pageBody.querySelector('[data-fsm-workbench="root"]');
    if (!root) return;

    syncFsmWorkbenchSearchControls();
    const countBadge = root.querySelector('[data-fsm-region="count"]');
    if (countBadge) {
        countBadge.textContent = formatFsmListCount(fsmListCache);
    }

    replaceFsmWorkbenchRegion(root, 'list', renderFsmListHtml(fsmListCache, fsmWorkbenchListEmptyMessage));
    bindFsmWorkbenchList();
}

function syncFsmWorkbenchSearchControls() {
    const input = $pageBody.querySelector('[data-fsm-search-input]');
    if (input && input.value !== fsmListSearchTerm) {
        input.value = fsmListSearchTerm;
    }

    const hasQuery = !!normalizeFsmListSearchTerm(fsmListSearchTerm);
    const clear = $pageBody.querySelector('[data-fsm-search-clear]');
    if (clear) {
        clear.classList.toggle('is-empty', !hasQuery);
        clear.disabled = !hasQuery;
    }
}

function markFsmListSelection(fsmName) {
    $pageBody.querySelectorAll('.fsm-list-item').forEach(btn => {
        const isActive = decodeURIComponent(btn.dataset.fsm ?? '') === fsmName;
        btn.classList.toggle('active', isActive);
        btn.setAttribute('aria-pressed', isActive ? 'true' : 'false');
    });
}
// 在 init 中调用 setupPushListeners()
