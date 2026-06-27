// pages/spatialkit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：SpatialKit
// ═══════════════════════════════════════════════════════════════════
const spatialKitState = {
    stats: {},
    indexes: [],
    searchTerm: '',
    selectedIndexId: null,
    renderSignature: '',
};

function renderSpatialKitPage() {
    $pageBody.classList.add('content-body--spatialkit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--audiokit');
    $pageBody.classList.remove('content-body--savekit');
    $pageBody.classList.remove('content-body--localizationkit');
    $pageBody.classList.remove('content-body--scenekit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('spatialkit.title'),
        t('spatialkit.subtitle'),
        t('spatialkit.tab'),
        'spatial',
        `<button class="btn btn-primary btn-sm" onclick="refreshSpatialKit()">${t('common.refresh')}</button>`
    );
    clearTabs();
    spatialKitState.renderSignature = '';
    loadSpatialKitWorkbench();
}

async function refreshSpatialKit() { loadSpatialKitWorkbench(); }

async function refreshSpatialKitReactive(event) {
    await loadSpatialKitWorkbench();
}

function normalizeSpatialKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const indexesSource = source.indexes ?? {};
    return {
        stats: source.stats ?? {},
        indexes: Array.isArray(source.indexes)
            ? source.indexes
            : (Array.isArray(indexesSource.indexes) ? indexesSource.indexes : []),
    };
}

async function fetchSpatialKitWorkbenchState() {
    return await fetchKitWorkbenchState('SpatialKit', normalizeSpatialKitStatePayload);
}

async function loadSpatialKitWorkbench() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('spatial', '请连接引擎后查看空间索引状态。');
        clearMetrics();
        return;
    }

    try {
        const state = await fetchSpatialKitWorkbenchState();
        spatialKitState.stats = state.stats;
        spatialKitState.indexes = state.indexes;
        reconcileSpatialKitSelection(spatialKitState.indexes);
        clearMetrics();

        const html = renderSpatialKitWorkbench(spatialKitState.stats, spatialKitState.indexes);
        const signature = makeStableSignature({
            stats: spatialKitState.stats,
            indexes: spatialKitState.indexes,
            selected: spatialKitState.selectedIndexId,
        });
        renderWorkbenchHtmlStable(spatialKitState, html, signature, bindSpatialKitWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('SpatialKit')) {
            showRuntimeKitUnavailable('SpatialKit', 'SpatialKit 空间索引');
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function reconcileSpatialKitSelection(indexes) {
    if (!Array.isArray(indexes) || !indexes.length) {
        spatialKitState.selectedIndexId = null;
        return null;
    }

    let selected = indexes.find(item => String(item.diagnosticsId) === String(spatialKitState.selectedIndexId));
    if (!selected) {
        selected = indexes[0];
        spatialKitState.selectedIndexId = selected.diagnosticsId;
    }
    return selected;
}

function renderSpatialKitWorkbench(stats, indexes) {
    const visibleIndexes = filterSpatialKitIndexes(indexes);
    const selected = indexes.find(item => String(item.diagnosticsId) === String(spatialKitState.selectedIndexId)) ?? null;
    return `<div class="kit-workbench kit-workbench--spatial">
        <section class="kit-toolbar">
            <div>
                <div class="kit-toolbar__title">${renderKitTitle('spatial', '空间索引工作台')}</div>
                <div class="kit-toolbar__meta">Active ${escapeHtml(stats?.activeIndexCount ?? indexes.length ?? 0)} · Entities ${escapeHtml(stats?.entityCount ?? 0)} · Partitions ${escapeHtml(stats?.partitionCount ?? 0)} · Created ${escapeHtml(stats?.totalCreatedIndexCount ?? 0)}</div>
            </div>
            <div class="kit-toolbar__actions">
                <span class="kit-state-pill ${indexes.length ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${escapeHtml(indexes.length ? 'Active' : 'Empty')}</span>
            </div>
        </section>
        <div class="kit-workbench-grid kit-workbench-grid--save">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('spatial', '索引列表')}</div>
                        <div class="kit-panel__desc">HashGrid、Quadtree、Octree 的当前诊断状态</div>
                    </div>
                    <span class="kit-panel__count" data-spatialkit-visible-count>${escapeHtml(visibleIndexes.length)} / ${escapeHtml(indexes.length)}</span>
                </div>
                <div class="kit-panel__tools">${renderKitSearchInput(spatialKitState.searchTerm, 'data-spatialkit-search', '搜索索引类型、实体类型、平面或诊断 ID')}</div>
                <div class="kit-resource-list" data-kit-scroll-key="spatial-indexes">${renderSpatialKitRows(visibleIndexes)}</div>
            </section>
            ${renderSpatialKitDetailSection(selected, stats)}
            ${renderSpatialKitStatsSection(stats)}
        </div>
    </div>`;
}

function filterSpatialKitIndexes(indexes) {
    return (Array.isArray(indexes) ? indexes : []).filter(index => kitSearchMatches(spatialKitState.searchTerm, [
        index.diagnosticsId,
        index.indexKind,
        index.entityTypeName,
        index.plane,
        index.count,
    ]));
}

function renderSpatialKitRows(indexes) {
    if (!Array.isArray(indexes) || !indexes.length) {
        return emptyState('spatial', '暂无空间索引。通过 SpatialKit.CreateHashGrid/CreateQuadtree/CreateOctree 创建后会出现在这里。');
    }

    return indexes.map(index => {
        const selected = String(index.diagnosticsId) === String(spatialKitState.selectedIndexId);
        return `<button class="kit-list-row${selected ? ' active' : ''}" type="button" data-spatialkit-index="${escapeHtml(index.diagnosticsId ?? '')}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(formatSpatialKitIndexTitle(index))}</strong>
                <em>${escapeHtml(index.entityTypeName || '--')} · ${escapeHtml(index.plane || '3D')} · ${escapeHtml(index.partitionCount ?? 0)} partitions</em>
            </span>
            <span class="kit-list-row__stats">${escapeHtml(index.count ?? 0)}</span>
        </button>`;
    }).join('');
}

function renderSpatialKitDetail(index) {
    if (!index) {
        return emptyState('spatial', '选择一个空间索引后查看容量、边界和诊断 ID。');
    }

    return `<div class="kit-detail-summary kit-detail-summary--save">
        <div><span>诊断 ID</span><strong>${escapeHtml(index.diagnosticsId || '--')}</strong></div>
        <div><span>索引类型</span><strong>${escapeHtml(index.indexKind || '--')}</strong></div>
        <div><span>实体类型</span><strong>${escapeHtml(index.entityTypeName || '--')}</strong></div>
        <div><span>实体数量</span><strong>${escapeHtml(index.count ?? 0)}</strong></div>
        <div><span>分区数量</span><strong>${escapeHtml(index.partitionCount ?? 0)}</strong></div>
        <div><span>投影平面</span><strong>${escapeHtml(index.plane || '3D')}</strong></div>
        <div><span>CellSize</span><strong>${escapeHtml(formatSpatialKitNumber(index.cellSize))}</strong></div>
        <div><span>MaxDepth</span><strong>${escapeHtml(index.maxDepth || '--')}</strong></div>
        <div><span>节点阈值</span><strong>${escapeHtml(index.maxEntitiesPerNode || '--')}</strong></div>
        <div><span>创建时间</span><strong>${escapeHtml(index.createdAtUtc || '--')}</strong></div>
    </div>
    <div class="kit-note">SpatialKit 页面只读展示诊断快照。实体插入、更新、删除和查询仍在运行时代码里通过索引对象执行，不通过文件桥做高频操作。</div>
    <div class="kit-note">${escapeHtml(formatSpatialKitBounds(index))}</div>`;
}

function renderSpatialKitDetailSection(index, stats) {
    return `<section class="kit-panel kit-panel--detail" data-spatialkit-detail-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', '索引详情')}</div>
                <div class="kit-panel__desc">${escapeHtml(index ? formatSpatialKitIndexTitle(index) : '未选择')}</div>
            </div>
        </div>
        ${renderSpatialKitDetail(index)}
    </section>`;
}

function renderSpatialKitStatsSection(stats) {
    return `<section class="kit-panel kit-panel--events">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('spatial', '运行统计')}</div>
                <div class="kit-panel__desc">索引类型分布和当前实体规模</div>
            </div>
        </div>
        <div class="kit-detail-summary kit-detail-summary--save-auto">
            <div><span>活动索引</span><strong>${escapeHtml(stats?.activeIndexCount ?? 0)}</strong></div>
            <div><span>累计创建</span><strong>${escapeHtml(stats?.totalCreatedIndexCount ?? 0)}</strong></div>
            <div><span>实体总数</span><strong>${escapeHtml(stats?.entityCount ?? 0)}</strong></div>
            <div><span>分区总数</span><strong>${escapeHtml(stats?.partitionCount ?? 0)}</strong></div>
            <div><span>HashGrid</span><strong>${escapeHtml(stats?.hashGridCount ?? 0)}</strong></div>
            <div><span>Quadtree</span><strong>${escapeHtml(stats?.quadtreeCount ?? 0)}</strong></div>
            <div><span>Octree</span><strong>${escapeHtml(stats?.octreeCount ?? 0)}</strong></div>
        </div>
        <div class="kit-note">快照由 Adapter 节流发布到 SpatialKit/state；页面优先读 telemetry，再读 snapshot，缺失时才走命令桥。</div>
    </section>`;
}

function bindSpatialKitWorkbenchActions() {
    $pageBody.querySelectorAll('[data-spatialkit-index]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectSpatialKitIndex(button.dataset.spatialkitIndex);
        });
    });
    bindSpatialKitSearch();
}

function bindSpatialKitSearch() {
    const input = $pageBody.querySelector('[data-spatialkit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            spatialKitState.searchTerm = input.value || '';
            updateSpatialKitListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-spatialkit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            spatialKitState.searchTerm = '';
            updateSpatialKitListDom();
            $pageBody.querySelector('[data-spatialkit-search]')?.focus();
        });
    }
}

function updateSpatialKitListDom() {
    const visible = filterSpatialKitIndexes(spatialKitState.indexes);
    const list = $pageBody.querySelector('.kit-resource-list');
    if (list) list.innerHTML = renderSpatialKitRows(visible);
    const count = $pageBody.querySelector('[data-spatialkit-visible-count]');
    if (count) count.textContent = `${visible.length} / ${spatialKitState.indexes.length}`;
    const input = $pageBody.querySelector('[data-spatialkit-search]');
    if (input && input.value !== spatialKitState.searchTerm) input.value = spatialKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-spatialkit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !spatialKitState.searchTerm.trim());
    bindSpatialKitWorkbenchActions();
}

function selectSpatialKitIndex(indexId) {
    if (!indexId) return;
    spatialKitState.selectedIndexId = indexId;
    $pageBody.querySelectorAll('[data-spatialkit-index]').forEach(row => {
        row.classList.toggle('active', String(row.dataset.spatialkitIndex) === String(spatialKitState.selectedIndexId));
    });
    const selected = spatialKitState.indexes.find(item => String(item.diagnosticsId) === String(spatialKitState.selectedIndexId)) ?? null;
    const detailPanel = $pageBody.querySelector('[data-spatialkit-detail-panel]');
    if (detailPanel) detailPanel.outerHTML = renderSpatialKitDetailSection(selected, spatialKitState.stats);
    bindSpatialKitWorkbenchActions();
}

function formatSpatialKitIndexTitle(index) {
    const kind = String(index?.indexKind || 'SpatialIndex').trim();
    const id = String(index?.diagnosticsId || '').trim();
    return id ? `${kind} · ${id}` : kind;
}

function formatSpatialKitNumber(value) {
    const number = Number(value);
    if (!Number.isFinite(number) || number <= 0) return '--';
    return number.toFixed(number % 1 === 0 ? 0 : 2);
}

function formatSpatialKitBounds(index) {
    if (index?.bounds2D) {
        const b = index.bounds2D;
        return `2D Bounds: x=${formatSpatialKitNumber(b.x)}, y=${formatSpatialKitNumber(b.y)}, w=${formatSpatialKitNumber(b.width)}, h=${formatSpatialKitNumber(b.height)}`;
    }
    if (index?.bounds3D) {
        const center = index.bounds3D.center ?? {};
        const size = index.bounds3D.size ?? {};
        return `3D Bounds: center(${formatSpatialKitNumber(center.x)}, ${formatSpatialKitNumber(center.y)}, ${formatSpatialKitNumber(center.z)}), size(${formatSpatialKitNumber(size.x)}, ${formatSpatialKitNumber(size.y)}, ${formatSpatialKitNumber(size.z)})`;
    }
    return '无固定边界。';
}

