// pages/savekit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：SaveKit
// ═══════════════════════════════════════════════════════════════════
const saveKitState = {
    stats: {},
    slots: [],
    autoSave: {},
    searchTerm: '',
    selectedSlotId: null,
    renderSignature: '',
};

function renderSaveKitPage() {
    $pageBody.classList.add('content-body--savekit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--audiokit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('savekit.title'),
        t('savekit.subtitle'),
        t('savekit.tab'),
        'save',
        `<button class="btn btn-sm" onclick="disableSaveKitAutoSave()">${t('common.disable')}</button><button class="btn btn-primary btn-sm" onclick="refreshSaveKit()">${t('common.refresh')}</button>`
    );
    clearTabs();
    saveKitState.renderSignature = '';
    loadSaveKitWorkbench();
}

async function refreshSaveKit() { loadSaveKitWorkbench(); }

async function refreshSaveKitReactive(event) {
    await loadSaveKitWorkbench();
}

function normalizeSaveKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const slotsSource = source.slots ?? {};
    return {
        stats: source.stats ?? {},
        slots: Array.isArray(source.slots) ? source.slots : (Array.isArray(slotsSource.slots) ? slotsSource.slots : []),
        autoSave: source.autoSave ?? {},
    };
}

async function fetchSaveKitWorkbenchState() {
    const telemetry = await readKitTelemetryData('SaveKit');
    if (telemetry) return normalizeSaveKitStatePayload(telemetry);

    const snapshot = await readKitSnapshotData('SaveKit');
    if (snapshot) return normalizeSaveKitStatePayload(snapshot);

    return null;
}

async function fetchSaveKitWorkbenchStateFromCommands() {
    const snapshot = await sendKitCommandData('SaveKit', 'get_workbench_snapshot');
    return normalizeSaveKitStatePayload(snapshot);
}

async function loadSaveKitWorkbench() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('save', '请连接引擎后查看存档状态。');
        clearMetrics();
        return;
    }
    try {
        const snapshotState = await fetchSaveKitWorkbenchState();
        const state = snapshotState ?? await fetchSaveKitWorkbenchStateFromCommands();
        saveKitState.stats = state.stats;
        saveKitState.slots = state.slots;
        saveKitState.autoSave = state.autoSave;
        reconcileSaveKitSelection(saveKitState.slots);
        clearMetrics();

        const html = renderSaveKitWorkbench(saveKitState.stats, saveKitState.slots, saveKitState.autoSave);
        const signature = makeStableSignature({
            stats: saveKitState.stats,
            slots: saveKitState.slots,
            autoSave: saveKitState.autoSave,
            selected: saveKitState.selectedSlotId
        });
        renderWorkbenchHtmlStable(saveKitState, html, signature, bindSaveKitWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('SaveKit')) {
            showRuntimeKitUnavailable('SaveKit', 'SaveKit 存档');
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function reconcileSaveKitSelection(slots) {
    if (!Array.isArray(slots) || !slots.length) {
        saveKitState.selectedSlotId = null;
        return null;
    }
    let selected = slots.find(item => String(item.slotId) === String(saveKitState.selectedSlotId));
    if (!selected) {
        selected = slots[0];
        saveKitState.selectedSlotId = selected.slotId;
    }
    return selected;
}

function renderSaveKitWorkbench(stats, slots, autoSave) {
    const visibleSlots = filterSaveKitSlots(slots);
    const selected = slots.find(item => String(item.slotId) === String(saveKitState.selectedSlotId)) ?? null;
    return `<div class="kit-workbench kit-workbench--save">
        <section class="kit-toolbar">
            <div>
                <div class="kit-toolbar__title">${renderKitTitle('save', '存档工作台')}</div>
                <div class="kit-toolbar__meta">Storage: ${escapeHtml(stats?.storageType || 'None')} · Serializer: ${escapeHtml(stats?.serializerType || 'None')} · 槽位 ${escapeHtml(stats?.slotCount ?? slots.length ?? 0)} / ${escapeHtml(stats?.maxSlots ?? 0)} · 加密 ${stats?.hasEncryptor ? '已启用' : '未启用'}</div>
            </div>
            <div class="kit-toolbar__actions">
                <button class="btn btn-sm" data-savekit-disable-auto-save ${autoSave?.autoSaveEnabled ? '' : 'disabled'}>关闭自动保存</button>
            </div>
        </section>
        <div class="kit-workbench-grid kit-workbench-grid--save">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('save', '存档槽')}</div>
                        <div class="kit-panel__desc">槽位、版本和保存时间</div>
                    </div>
                    <span class="kit-panel__count" data-savekit-visible-count>${escapeHtml(visibleSlots.length)} / ${escapeHtml(slots.length)}</span>
                </div>
                <div class="kit-panel__tools">${renderKitSearchInput(saveKitState.searchTerm, 'data-savekit-search', '搜索槽位、名称、版本或时间')}</div>
                <div class="kit-resource-list" data-kit-scroll-key="save-slots">${renderSaveKitRows(visibleSlots)}</div>
            </section>
            ${renderSaveKitDetailSection(selected, stats)}
            ${renderSaveKitAutoSaveSection(autoSave)}
        </div>
    </div>`;
}

function filterSaveKitSlots(slots) {
    return (Array.isArray(slots) ? slots : []).filter(slot => kitSearchMatches(saveKitState.searchTerm, [
        slot.slotId,
        slot.version,
        slot.displayName,
        slot.createdAtUtc,
        slot.savedAtUtc,
    ]));
}

function renderSaveKitRows(slots) {
    if (!Array.isArray(slots) || !slots.length) {
        return emptyState('save', '暂无存档槽。调用 SaveKit.Save 后会显示存档元数据。');
    }
    return slots.map(slot => {
        const selected = String(slot.slotId) === String(saveKitState.selectedSlotId);
        return `<button class="kit-list-row${selected ? ' active' : ''}" type="button" data-savekit-slot="${escapeHtml(slot.slotId ?? '')}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(formatSaveKitSlotTitle(slot))}</strong>
                <em>v${escapeHtml(slot.version ?? '--')} · ${escapeHtml(formatSaveKitDate(slot.savedAtUtc || slot.lastSavedTimestamp))}</em>
            </span>
            <span class="kit-list-row__stats">slot ${escapeHtml(slot.slotId ?? '--')}</span>
        </button>`;
    }).join('');
}

function renderSaveKitDetail(slot, stats) {
    if (!slot) {
        return emptyState('save', '选择存档槽后查看元数据和维护动作。');
    }
    return `<div class="kit-detail-summary kit-detail-summary--save">
        <div><span>槽位</span><strong>${escapeHtml(slot.slotId ?? '--')}</strong></div>
        <div><span>版本</span><strong>${escapeHtml(slot.version ?? '--')}</strong></div>
        <div><span>创建</span><strong>${escapeHtml(formatSaveKitDate(slot.createdAtUtc || slot.createdTimestamp))}</strong></div>
        <div><span>保存</span><strong>${escapeHtml(formatSaveKitDate(slot.savedAtUtc || slot.lastSavedTimestamp))}</strong></div>
    </div>
    <div class="kit-code-path">${escapeHtml(slot.displayName || formatSaveKitSlotTitle(slot))}</div>
    <div class="kit-note">当前命令桥只暴露存档元数据和维护动作，不传输真实存档 payload。Storage/Serializer/Encryptor 后端由 Unity、Godot 或项目自定义 Adapter 注入。</div>
    <div class="kit-code-action">
        <button class="btn btn-sm" data-savekit-delete-slot="${escapeHtml(slot.slotId ?? '')}">删除槽位</button>
    </div>`;
}

function renderSaveKitDetailSection(slot, stats) {
    return `<section class="kit-panel kit-panel--detail" data-savekit-detail-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', '槽位详情')}</div>
                <div class="kit-panel__desc">${escapeHtml(slot ? formatSaveKitSlotTitle(slot) : '未选择')}</div>
            </div>
        </div>
        ${renderSaveKitDetail(slot, stats)}
    </section>`;
}

function renderSaveKitAutoSaveSection(autoSave) {
    const enabled = !!autoSave?.autoSaveEnabled;
    const elapsed = Number(autoSave?.autoSaveElapsedSeconds ?? 0);
    const interval = Number(autoSave?.autoSaveIntervalSeconds ?? 0);
    const progress = interval > 0 ? Math.min(1, elapsed / interval) : 0;
    return `<section class="kit-panel kit-panel--events">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('refresh', '自动保存')}</div>
                <div class="kit-panel__desc">宿主 Tick 驱动，状态通过 snapshot/telemetry 发布</div>
            </div>
            <span class="kit-state-pill ${enabled ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${enabled ? 'Enabled' : 'Disabled'}</span>
        </div>
        <div class="kit-detail-summary kit-detail-summary--save-auto">
            <div><span>目标槽</span><strong>${escapeHtml(enabled ? autoSave.autoSaveSlotId ?? '--' : '--')}</strong></div>
            <div><span>间隔</span><strong>${escapeHtml(formatSaveKitSeconds(interval))}</strong></div>
            <div><span>已计时</span><strong>${escapeHtml(formatSaveKitSeconds(elapsed))}</strong></div>
            <div><span>进度</span><strong>${escapeHtml(percentText(progress))}</strong></div>
        </div>
        <div class="kit-note">${enabled ? '自动保存启用中；Tauri 只显示和关闭，不创建存档数据。' : '自动保存未启用。业务代码可通过 SaveKit.EnableAutoSave 开启。'}</div>
    </section>`;
}

function bindSaveKitWorkbenchActions() {
    $pageBody.querySelectorAll('[data-savekit-slot]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectSaveKitSlot(button.dataset.savekitSlot);
        });
    });
    $pageBody.querySelectorAll('[data-savekit-delete-slot]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => deleteSaveKitSlot(button.dataset.savekitDeleteSlot));
    });
    bindKitButtonClick('[data-savekit-disable-auto-save]', () => disableSaveKitAutoSave());
    bindSaveKitSearch();
}

function bindSaveKitSearch() {
    const input = $pageBody.querySelector('[data-savekit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            saveKitState.searchTerm = input.value || '';
            updateSaveKitListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-savekit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            saveKitState.searchTerm = '';
            updateSaveKitListDom();
            $pageBody.querySelector('[data-savekit-search]')?.focus();
        });
    }
}

function updateSaveKitListDom() {
    const visible = filterSaveKitSlots(saveKitState.slots);
    const list = $pageBody.querySelector('.kit-resource-list');
    if (list) list.innerHTML = renderSaveKitRows(visible);
    const count = $pageBody.querySelector('[data-savekit-visible-count]');
    if (count) count.textContent = `${visible.length} / ${saveKitState.slots.length}`;
    const input = $pageBody.querySelector('[data-savekit-search]');
    if (input && input.value !== saveKitState.searchTerm) input.value = saveKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-savekit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !saveKitState.searchTerm.trim());
    bindSaveKitWorkbenchActions();
}

function selectSaveKitSlot(slotId) {
    if (slotId === null || slotId === undefined || slotId === '') return;
    saveKitState.selectedSlotId = slotId;
    $pageBody.querySelectorAll('[data-savekit-slot]').forEach(row => {
        row.classList.toggle('active', String(row.dataset.savekitSlot) === String(saveKitState.selectedSlotId));
    });
    const selected = saveKitState.slots.find(item => String(item.slotId) === String(saveKitState.selectedSlotId)) ?? null;
    const detailPanel = $pageBody.querySelector('[data-savekit-detail-panel]');
    if (detailPanel) detailPanel.outerHTML = renderSaveKitDetailSection(selected, saveKitState.stats);
    bindSaveKitWorkbenchActions();
}

async function deleteSaveKitSlot(slotId) {
    if (!invoke || !connected || slotId === null || slotId === undefined || slotId === '') return;
    await sendKitCommandData('SaveKit', 'delete_slot', { slotId: Number(slotId) });
    if (String(saveKitState.selectedSlotId) === String(slotId)) {
        saveKitState.selectedSlotId = null;
    }
    await loadSaveKitWorkbench();
}

async function disableSaveKitAutoSave() {
    if (!invoke || !connected) return;
    await sendKitCommandData('SaveKit', 'disable_auto_save');
    await loadSaveKitWorkbench();
}

function formatSaveKitSlotTitle(slot) {
    const name = String(slot?.displayName || '').trim();
    return name || `Slot ${slot?.slotId ?? '--'}`;
}

function formatSaveKitDate(value) {
    if (value === null || value === undefined || value === '') return '--';
    if (typeof value === 'number') {
        const date = new Date(value * 1000);
        return Number.isNaN(date.getTime()) ? '--' : date.toLocaleString();
    }
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? String(value) : date.toLocaleString();
}

function formatSaveKitSeconds(value) {
    const number = Number(value);
    if (!Number.isFinite(number) || number <= 0) return '0.0s';
    return number.toFixed(1) + 's';
}

