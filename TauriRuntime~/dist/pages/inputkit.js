// pages/inputkit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：InputKit
// ═══════════════════════════════════════════════════════════════════
const inputKitState = {
    stats: {},
    actions: [],
    contexts: {
        activeContexts: [],
        registeredContexts: [],
        enabledActionMaps: [],
        currentContext: null,
    },
    searchTerm: '',
    selectedKind: 'action',
    selectedId: null,
    renderSignature: '',
};

function renderInputKitPage() {
    $pageBody.classList.add('content-body--inputkit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--audiokit');
    $pageBody.classList.remove('content-body--savekit');
    $pageBody.classList.remove('content-body--localizationkit');
    $pageBody.classList.remove('content-body--scenekit');
    $pageBody.classList.remove('content-body--spatialkit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('inputkit.title'),
        t('inputkit.subtitle'),
        t('inputkit.tab'),
        'input',
        `<button class="btn btn-primary btn-sm" onclick="refreshInputKit()">${t('common.refresh')}</button>`
    );
    clearTabs();
    inputKitState.renderSignature = '';
    loadInputKitWorkbench();
}

async function refreshInputKit() { loadInputKitWorkbench(); }

async function refreshInputKitReactive(event) {
    await loadInputKitWorkbench();
}

function normalizeInputKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const actionsSource = source.actions ?? {};
    const contextsSource = source.contexts ?? {};
    return {
        stats: source.stats ?? {},
        actions: Array.isArray(source.actions)
            ? source.actions
            : (Array.isArray(actionsSource.actions) ? actionsSource.actions : []),
        contexts: {
            activeContexts: Array.isArray(contextsSource.activeContexts) ? contextsSource.activeContexts : [],
            registeredContexts: Array.isArray(contextsSource.registeredContexts) ? contextsSource.registeredContexts : [],
            enabledActionMaps: Array.isArray(contextsSource.enabledActionMaps) ? contextsSource.enabledActionMaps : [],
            currentContext: contextsSource.currentContext ?? null,
        },
    };
}

async function fetchInputKitWorkbenchState() {
    const telemetry = await readKitTelemetryData('InputKit');
    if (telemetry) return normalizeInputKitStatePayload(telemetry);

    const snapshot = await readKitSnapshotData('InputKit');
    if (snapshot) return normalizeInputKitStatePayload(snapshot);

    return null;
}

async function fetchInputKitWorkbenchStateFromCommands() {
    const snapshot = await sendKitCommandData('InputKit', 'get_workbench_snapshot');
    return normalizeInputKitStatePayload(snapshot);
}

async function loadInputKitWorkbench() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('input', '请连接引擎后查看输入状态。');
        clearMetrics();
        return;
    }

    try {
        const snapshotState = await fetchInputKitWorkbenchState();
        const state = snapshotState ?? await fetchInputKitWorkbenchStateFromCommands();
        inputKitState.stats = state.stats;
        inputKitState.actions = state.actions;
        inputKitState.contexts = state.contexts;
        reconcileInputKitSelection();
        clearMetrics();

        const html = renderInputKitWorkbench(inputKitState.stats, inputKitState.actions, inputKitState.contexts);
        const signature = makeStableSignature({
            stats: inputKitState.stats,
            actions: inputKitState.actions,
            contexts: inputKitState.contexts,
            selectedKind: inputKitState.selectedKind,
            selectedId: inputKitState.selectedId,
        });
        renderWorkbenchHtmlStable(inputKitState, html, signature, bindInputKitWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('InputKit')) {
            showRuntimeKitUnavailable('InputKit', 'InputKit 输入');
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function reconcileInputKitSelection() {
    const selectedAction = inputKitState.selectedKind === 'action'
        ? findInputKitAction(inputKitState.selectedId)
        : null;
    if (selectedAction) return selectedAction;

    const selectedContext = inputKitState.selectedKind === 'context'
        ? findInputKitContext(inputKitState.selectedId)
        : null;
    if (selectedContext) return selectedContext;

    const pressed = inputKitState.actions.find(action => action.isPressed) ?? inputKitState.actions[0] ?? null;
    if (pressed) {
        inputKitState.selectedKind = 'action';
        inputKitState.selectedId = pressed.actionName;
        return pressed;
    }

    const currentContext = inputKitState.contexts.currentContext
        ?? inputKitState.contexts.activeContexts[inputKitState.contexts.activeContexts.length - 1]
        ?? inputKitState.contexts.registeredContexts[0]
        ?? null;
    if (currentContext) {
        inputKitState.selectedKind = 'context';
        inputKitState.selectedId = currentContext.contextName;
        return currentContext;
    }

    inputKitState.selectedKind = 'action';
    inputKitState.selectedId = null;
    return null;
}

function renderInputKitWorkbench(stats, actions, contexts) {
    const visibleActions = filterInputKitActions(actions);
    const visibleContexts = filterInputKitContexts(contexts);
    const selectedAction = inputKitState.selectedKind === 'action' ? findInputKitAction(inputKitState.selectedId) : null;
    const selectedContext = inputKitState.selectedKind === 'context' ? findInputKitContext(inputKitState.selectedId) : null;
    return `<div class="kit-workbench kit-workbench--input">
        <section class="kit-toolbar">
            <div>
                <div class="kit-toolbar__title">${renderKitTitle('input', '输入工作台')}</div>
                <div class="kit-toolbar__meta">Backend: ${escapeHtml(stats?.backendName || 'None')} · Device ${escapeHtml(stats?.currentDeviceType || 'Unknown')} · Actions ${escapeHtml(stats?.actionCount ?? actions.length ?? 0)} · Contexts ${escapeHtml(stats?.activeContextCount ?? 0)} / ${escapeHtml(stats?.registeredContextCount ?? 0)}</div>
            </div>
            <div class="kit-toolbar__actions">
                <span class="kit-state-pill ${stats?.isInitialized ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${escapeHtml(stats?.isInitialized ? 'Initialized' : 'No Backend')}</span>
            </div>
        </section>
        <div class="kit-workbench-grid kit-workbench-grid--save">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('input', '动作与上下文')}</div>
                        <div class="kit-panel__desc">当前帧动作状态、上下文栈和启用 ActionMap</div>
                    </div>
                    <span class="kit-panel__count" data-inputkit-visible-count>${escapeHtml(visibleActions.length)} / ${escapeHtml(actions.length)}</span>
                </div>
                <div class="kit-panel__tools">${renderKitSearchInput(inputKitState.searchTerm, 'data-inputkit-search', '搜索动作、上下文或 ActionMap')}</div>
                <div class="kit-note">动作</div>
                <div class="kit-resource-list" data-kit-scroll-key="input-actions" data-inputkit-action-list>${renderInputKitActionRows(visibleActions)}</div>
                <div class="kit-note">上下文</div>
                <div class="kit-resource-list" data-kit-scroll-key="input-contexts" data-inputkit-context-list>${renderInputKitContextRows(visibleContexts)}</div>
            </section>
            ${renderInputKitDetailSection(selectedAction, selectedContext)}
            ${renderInputKitStatsSection(stats, contexts)}
        </div>
    </div>`;
}

function filterInputKitActions(actions) {
    return (Array.isArray(actions) ? actions : []).filter(action => kitSearchMatches(inputKitState.searchTerm, [
        action.actionName,
        formatInputKitActionStatus(action),
        action.value,
        action.lastChangedAt,
    ]));
}

function filterInputKitContexts(contexts) {
    return getInputKitContextRows(contexts).filter(row => kitSearchMatches(inputKitState.searchTerm, [
        row.context.contextName,
        row.context.priority,
        row.kind,
        ...(row.context.enabledActionMaps ?? []),
        ...(row.context.blockedActions ?? []),
    ]));
}

function getInputKitContextRows(contexts = inputKitState.contexts) {
    const active = Array.isArray(contexts?.activeContexts) ? contexts.activeContexts : [];
    const registered = Array.isArray(contexts?.registeredContexts) ? contexts.registeredContexts : [];
    const rows = active.map(context => ({ kind: 'active', context }));
    registered.forEach(context => {
        const exists = active.some(activeContext => String(activeContext.contextName) === String(context.contextName));
        if (!exists) rows.push({ kind: 'registered', context });
    });
    return rows;
}

function renderInputKitActionRows(actions) {
    if (!Array.isArray(actions) || !actions.length) {
        return emptyState('input', '暂无动作状态。宿主后端 Poll 后会通过 IInputStateWriter 写入动作。');
    }

    return actions.map(action => {
        const selected = inputKitState.selectedKind === 'action' && String(action.actionName) === String(inputKitState.selectedId);
        return `<button class="kit-list-row${selected ? ' active' : ''}" type="button" data-inputkit-action="${escapeHtml(action.actionName ?? '')}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(action.actionName || '--')}</strong>
                <em>${escapeHtml(formatInputKitActionStatus(action))} · Value ${escapeHtml(formatInputKitNumber(action.value))} · Last ${escapeHtml(formatInputKitNumber(action.lastChangedAt))}</em>
            </span>
            <span class="kit-list-row__stats">${escapeHtml(action.isPressed ? 'Pressed' : 'Idle')}</span>
        </button>`;
    }).join('');
}

function renderInputKitContextRows(rows) {
    if (!Array.isArray(rows) || !rows.length) {
        return emptyState('input', '暂无上下文。调用 InputKit.RegisterContext/PushContext 后会显示。');
    }

    return rows.map(row => {
        const context = row.context ?? {};
        const selected = inputKitState.selectedKind === 'context' && String(context.contextName) === String(inputKitState.selectedId);
        return `<button class="kit-list-row${selected ? ' active' : ''}" type="button" data-inputkit-context="${escapeHtml(context.contextName ?? '')}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(context.contextName || '--')}</strong>
                <em>${escapeHtml(row.kind === 'active' ? 'Active' : 'Registered')} · Priority ${escapeHtml(context.priority ?? 0)} · Maps ${escapeHtml((context.enabledActionMaps ?? []).length)}</em>
            </span>
            <span class="kit-list-row__stats">${escapeHtml(context.blockAllLowerPriority ? 'Block' : 'Pass')}</span>
        </button>`;
    }).join('');
}

function renderInputKitDetailSection(action, context) {
    return `<section class="kit-panel kit-panel--detail" data-inputkit-detail-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', '输入详情')}</div>
                <div class="kit-panel__desc">${escapeHtml(action?.actionName || context?.contextName || '未选择')}</div>
            </div>
        </div>
        ${renderInputKitDetail(action, context)}
    </section>`;
}

function renderInputKitDetail(action, context) {
    if (action) {
        return `<div class="kit-detail-summary kit-detail-summary--save">
            <div><span>动作</span><strong>${escapeHtml(action.actionName || '--')}</strong></div>
            <div><span>${t("common.status")}</span><strong>${escapeHtml(formatInputKitActionStatus(action))}</strong></div>
            <div><span>Pressed</span><strong>${escapeHtml(action.isPressed ? '是' : '否')}</strong></div>
            <div><span>Down</span><strong>${escapeHtml(action.wasPressedThisFrame ? '是' : '否')}</strong></div>
            <div><span>Up</span><strong>${escapeHtml(action.wasReleasedThisFrame ? '是' : '否')}</strong></div>
            <div><span>Value</span><strong>${escapeHtml(formatInputKitNumber(action.value))}</strong></div>
            <div><span>LastChangedAt</span><strong>${escapeHtml(formatInputKitNumber(action.lastChangedAt))}</strong></div>
        </div>
        <div class="kit-note">InputKit 命令桥只读展示诊断快照，不通过文件桥注入按键、模拟输入或修改绑定。</div>`;
    }

    if (context) {
        return `<div class="kit-detail-summary kit-detail-summary--save">
            <div><span>上下文</span><strong>${escapeHtml(context.contextName || '--')}</strong></div>
            <div><span>Priority</span><strong>${escapeHtml(context.priority ?? 0)}</strong></div>
            <div><span>StackIndex</span><strong>${escapeHtml(context.stackIndex ?? '--')}</strong></div>
            <div><span>阻断低优先级</span><strong>${escapeHtml(context.blockAllLowerPriority ? '是' : '否')}</strong></div>
            <div><span>ActionMaps</span><strong>${escapeHtml(formatInputKitStringList(context.enabledActionMaps))}</strong></div>
            <div><span>BlockedActions</span><strong>${escapeHtml(formatInputKitStringList(context.blockedActions))}</strong></div>
        </div>
        <div class="kit-note">上下文切换仍由运行时代码调用 InputKit.PushContext/PopContext；工作台只观察当前状态。</div>`;
    }

    return emptyState('input', '选择一个动作或上下文后查看详细状态。');
}

function renderInputKitStatsSection(stats, contexts) {
    return `<section class="kit-panel kit-panel--events">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('input', '运行统计')}</div>
                <div class="kit-panel__desc">后端、设备、缓冲窗口和当前 ActionMap</div>
            </div>
        </div>
        <div class="kit-detail-summary kit-detail-summary--save-auto">
            <div><span>后端</span><strong>${escapeHtml(stats?.backendName || '--')}</strong></div>
            <div><span>设备</span><strong>${escapeHtml(stats?.currentDeviceType || 'Unknown')}</strong></div>
            <div><span>手柄连接</span><strong>${escapeHtml(stats?.isGamepadConnected ? '是' : '否')}</strong></div>
            <div><span>动作总数</span><strong>${escapeHtml(stats?.actionCount ?? 0)}</strong></div>
            <div><span>按下动作</span><strong>${escapeHtml(stats?.pressedActionCount ?? 0)}</strong></div>
            <div><span>释放动作</span><strong>${escapeHtml(stats?.releasedActionCount ?? 0)}</strong></div>
            <div><span>缓冲输入</span><strong>${escapeHtml(stats?.bufferedInputCount ?? 0)}</strong></div>
            <div><span>缓冲窗口</span><strong>${escapeHtml(formatInputKitNumber(stats?.bufferWindowMs))} ms</strong></div>
            <div><span>ActionMaps</span><strong>${escapeHtml(formatInputKitStringList(contexts?.enabledActionMaps))}</strong></div>
        </div>
        <div class="kit-note">快照由 Adapter 节流发布到 InputKit/state；页面优先读 telemetry，再读 snapshot，缺失时才走命令桥。</div>
    </section>`;
}

function bindInputKitWorkbenchActions() {
    $pageBody.querySelectorAll('[data-inputkit-action]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectInputKitAction(button.dataset.inputkitAction);
        });
    });
    $pageBody.querySelectorAll('[data-inputkit-context]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectInputKitContext(button.dataset.inputkitContext);
        });
    });
    bindInputKitSearch();
}

function bindInputKitSearch() {
    const input = $pageBody.querySelector('[data-inputkit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            inputKitState.searchTerm = input.value || '';
            updateInputKitListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-inputkit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            inputKitState.searchTerm = '';
            updateInputKitListDom();
            $pageBody.querySelector('[data-inputkit-search]')?.focus();
        });
    }
}

function updateInputKitListDom() {
    const visibleActions = filterInputKitActions(inputKitState.actions);
    const visibleContexts = filterInputKitContexts(inputKitState.contexts);
    const actionList = $pageBody.querySelector('[data-inputkit-action-list]');
    if (actionList) actionList.innerHTML = renderInputKitActionRows(visibleActions);
    const contextList = $pageBody.querySelector('[data-inputkit-context-list]');
    if (contextList) contextList.innerHTML = renderInputKitContextRows(visibleContexts);
    const count = $pageBody.querySelector('[data-inputkit-visible-count]');
    if (count) count.textContent = `${visibleActions.length} / ${inputKitState.actions.length}`;
    const input = $pageBody.querySelector('[data-inputkit-search]');
    if (input && input.value !== inputKitState.searchTerm) input.value = inputKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-inputkit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !inputKitState.searchTerm.trim());
    bindInputKitWorkbenchActions();
}

function selectInputKitAction(actionName) {
    if (!actionName) return;
    inputKitState.selectedKind = 'action';
    inputKitState.selectedId = actionName;
    syncInputKitSelectionDom();
}

function selectInputKitContext(contextName) {
    if (!contextName) return;
    inputKitState.selectedKind = 'context';
    inputKitState.selectedId = contextName;
    syncInputKitSelectionDom();
}

function syncInputKitSelectionDom() {
    $pageBody.querySelectorAll('[data-inputkit-action]').forEach(row => {
        row.classList.toggle('active',
            inputKitState.selectedKind === 'action' && String(row.dataset.inputkitAction) === String(inputKitState.selectedId));
    });
    $pageBody.querySelectorAll('[data-inputkit-context]').forEach(row => {
        row.classList.toggle('active',
            inputKitState.selectedKind === 'context' && String(row.dataset.inputkitContext) === String(inputKitState.selectedId));
    });
    const action = inputKitState.selectedKind === 'action' ? findInputKitAction(inputKitState.selectedId) : null;
    const context = inputKitState.selectedKind === 'context' ? findInputKitContext(inputKitState.selectedId) : null;
    const detailPanel = $pageBody.querySelector('[data-inputkit-detail-panel]');
    if (detailPanel) detailPanel.outerHTML = renderInputKitDetailSection(action, context);
    bindInputKitWorkbenchActions();
}

function findInputKitAction(actionName) {
    return inputKitState.actions.find(action => String(action.actionName) === String(actionName)) ?? null;
}

function findInputKitContext(contextName) {
    const rows = getInputKitContextRows(inputKitState.contexts);
    const row = rows.find(item => String(item.context?.contextName) === String(contextName));
    return row?.context ?? null;
}

function formatInputKitActionStatus(action) {
    if (!action) return 'Idle';
    if (action.wasPressedThisFrame) return 'Pressed This Frame';
    if (action.wasReleasedThisFrame) return 'Released This Frame';
    if (action.isPressed) return 'Pressed';
    return 'Idle';
}

function formatInputKitNumber(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) return '--';
    return number.toFixed(number % 1 === 0 ? 0 : 3);
}

function formatInputKitStringList(values) {
    if (!Array.isArray(values) || !values.length) return '--';
    return values.filter(Boolean).join(', ') || '--';
}

