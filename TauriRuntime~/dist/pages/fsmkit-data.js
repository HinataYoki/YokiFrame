// pages/fsmkit-data.js
// FsmKit 数据流：列表读取、详情/历史命令、工作台快照和缺失选择修复。
function normalizeFsmListPayload(data) {
    const source = data?.data ?? data ?? {};
    if (BridgeDiagnostics?.extractFsmSnapshotList) {
        const extracted = BridgeDiagnostics.extractFsmSnapshotList(source);
        if (Array.isArray(extracted)) return extracted;
    }
    return Array.isArray(source.fsms) ? source.fsms : [];
}

// 拉取 FSM 列表，刷新缓存与指标。返回 fsms 数组（失败返回 null）。
async function fetchFsmList() {
    const fsms = await fetchKitWorkbenchState('FsmKit', normalizeFsmListPayload, {
        commandAction: 'list_all',
    });
    if (!Array.isArray(fsms)) return [];
    fsmListCache = fsms;
    return fsms;
}

async function fetchFsmCommandList() {
    const fsms = await fetchKitWorkbenchState('FsmKit', normalizeFsmListPayload, {
        forceCommandRefresh: true,
        commandAction: 'list_all',
    });
    if (!Array.isArray(fsms)) return null;
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

