// pages/scenekit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：SceneKit
// ═══════════════════════════════════════════════════════════════════
const sceneKitState = {
    stats: {},
    scenes: [],
    searchTerm: '',
    selectedSceneName: null,
    renderSignature: '',
};

function renderSceneKitPage() {
    $pageBody.classList.add('content-body--scenekit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--audiokit');
    $pageBody.classList.remove('content-body--savekit');
    $pageBody.classList.remove('content-body--localizationkit');
    $pageBody.classList.remove('content-body--spatialkit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('scenekit.title'),
        t('scenekit.subtitle'),
        t('scenekit.tab'),
        'scene',
        `<button class="btn btn-primary btn-sm" onclick="refreshSceneKit()">${t('common.refresh')}</button>`
    );
    clearTabs();
    sceneKitState.renderSignature = '';
    loadSceneKitWorkbench();
}

async function refreshSceneKit() { loadSceneKitWorkbench(); }

async function refreshSceneKitReactive(event) {
    await loadSceneKitWorkbench();
}

function normalizeSceneKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const scenesSource = source.scenes ?? {};
    return {
        stats: source.stats ?? {},
        scenes: Array.isArray(source.scenes)
            ? source.scenes
            : (Array.isArray(scenesSource.scenes) ? scenesSource.scenes : []),
    };
}

async function fetchSceneKitWorkbenchState() {
    return await fetchKitWorkbenchState('SceneKit', normalizeSceneKitStatePayload);
}

async function loadSceneKitWorkbench() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('scene', '请连接引擎后查看场景状态。');
        clearMetrics();
        return;
    }

    try {
        const state = await fetchSceneKitWorkbenchState();
        sceneKitState.stats = state.stats;
        sceneKitState.scenes = state.scenes;
        reconcileSceneKitSelection(sceneKitState.scenes);
        clearMetrics();

        const html = renderSceneKitWorkbench(sceneKitState.stats, sceneKitState.scenes);
        const signature = makeStableSignature({
            stats: sceneKitState.stats,
            scenes: sceneKitState.scenes,
            selected: sceneKitState.selectedSceneName,
        });
        renderWorkbenchHtmlStable(sceneKitState, html, signature, bindSceneKitWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('SceneKit')) {
            showRuntimeKitUnavailable('SceneKit', 'SceneKit 场景');
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function reconcileSceneKitSelection(scenes) {
    if (!Array.isArray(scenes) || !scenes.length) {
        sceneKitState.selectedSceneName = null;
        return null;
    }

    let selected = scenes.find(item => String(item.sceneName) === String(sceneKitState.selectedSceneName));
    if (!selected) {
        selected = scenes.find(item => item.isActive) ?? scenes[0];
        sceneKitState.selectedSceneName = selected.sceneName;
    }
    return selected;
}

function renderSceneKitWorkbench(stats, scenes) {
    const visibleScenes = filterSceneKitScenes(scenes);
    const selected = scenes.find(item => String(item.sceneName) === String(sceneKitState.selectedSceneName)) ?? null;
    return `<div class="kit-workbench kit-workbench--scene">
        <section class="kit-toolbar">
            <div>
                <div class="kit-toolbar__title">${renderKitTitle('scene', '场景工作台')}</div>
                <div class="kit-toolbar__meta">Backend: ${escapeHtml(stats?.backendName || stats?.backendType || 'None')} · Active: ${escapeHtml(stats?.activeSceneName || '--')} · Loaded ${escapeHtml(stats?.loadedSceneCount ?? scenes.length ?? 0)} · Transition ${stats?.isTransitioning ? '进行中' : '空闲'}</div>
            </div>
            <div class="kit-toolbar__actions">
                <span class="kit-state-pill ${stats?.backendType ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${escapeHtml(stats?.backendType || 'No Backend')}</span>
            </div>
        </section>
        <div class="kit-workbench-grid kit-workbench-grid--save">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('scene', '场景列表')}</div>
                        <div class="kit-panel__desc">加载状态、进度、激活状态和预加载标记</div>
                    </div>
                    <span class="kit-panel__count" data-scenekit-visible-count>${escapeHtml(visibleScenes.length)} / ${escapeHtml(scenes.length)}</span>
                </div>
                <div class="kit-panel__tools">${renderKitSearchInput(sceneKitState.searchTerm, 'data-scenekit-search', '搜索场景、状态、模式或数据类型')}</div>
                <div class="kit-resource-list" data-kit-scroll-key="scene-scenes">${renderSceneKitRows(visibleScenes)}</div>
            </section>
            ${renderSceneKitDetailSection(selected, stats)}
            ${renderSceneKitBackendSection(stats)}
        </div>
    </div>`;
}

function filterSceneKitScenes(scenes) {
    return (Array.isArray(scenes) ? scenes : []).filter(scene => kitSearchMatches(sceneKitState.searchTerm, [
        scene.sceneName,
        scene.buildIndex,
        scene.state,
        scene.loadMode,
        scene.dataType,
        scene.isActive ? 'active 激活' : '',
        scene.isPreloaded ? 'preloaded 预加载' : '',
        scene.isSuspended ? 'suspended 暂停' : '',
    ]));
}

function renderSceneKitRows(scenes) {
    if (!Array.isArray(scenes) || !scenes.length) {
        return emptyState('scene', '暂无场景记录。调用 SceneKit.LoadSceneAsync 后会显示加载状态。');
    }

    return scenes.map(scene => {
        const selected = String(scene.sceneName) === String(sceneKitState.selectedSceneName);
        const flags = [
            scene.isActive ? 'Active' : null,
            scene.isPreloaded ? 'Preload' : null,
            scene.isSuspended ? 'Suspended' : null,
        ].filter(Boolean).join(' · ');
        return `<button class="kit-list-row${selected ? ' active' : ''}" type="button" data-scenekit-scene="${escapeHtml(scene.sceneName ?? '')}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(formatSceneKitSceneTitle(scene))}</strong>
                <em>${escapeHtml(scene.state || '--')} · ${escapeHtml(percentText(scene.progress ?? 0))}${flags ? ' · ' + escapeHtml(flags) : ''}</em>
            </span>
            <span class="kit-list-row__stats">${escapeHtml(scene.loadMode || '--')}</span>
        </button>`;
    }).join('');
}

function renderSceneKitDetail(scene, stats) {
    if (!scene) {
        return emptyState('scene', '选择一个场景后查看加载进度、数据类型和维护动作。');
    }

    const unloadDisabled = scene.state === 'Unloaded' || scene.state === 'Unloading';
    return `<div class="kit-detail-summary kit-detail-summary--save">
        <div><span>场景</span><strong>${escapeHtml(scene.sceneName || '--')}</strong></div>
        <div><span>${t("common.status")}</span><strong>${escapeHtml(scene.state || '--')}</strong></div>
        <div><span>进度</span><strong>${escapeHtml(percentText(scene.progress ?? 0))}</strong></div>
        <div><span>模式</span><strong>${escapeHtml(scene.loadMode || '--')}</strong></div>
        <div><span>BuildIndex</span><strong>${escapeHtml(scene.buildIndex ?? '--')}</strong></div>
        <div><span>激活</span><strong>${escapeHtml(scene.isActive ? '是' : '否')}</strong></div>
        <div><span>暂停</span><strong>${escapeHtml(scene.isSuspended ? '是' : '否')}</strong></div>
        <div><span>预加载</span><strong>${escapeHtml(scene.isPreloaded ? '是' : '否')}</strong></div>
        <div><span>有效句柄</span><strong>${escapeHtml(scene.isValid ? '是' : '否')}</strong></div>
        <div><span>数据类型</span><strong>${escapeHtml(scene.dataType || '--')}</strong></div>
    </div>
    <div class="kit-note">SceneKit 页面只展示诊断快照；场景加载、卸载、激活仍通过统一静态入口和宿主后端执行。Unity/Godot 差异必须留在 ISceneBackend 实现里。</div>
    <div class="kit-code-action">
        <button class="btn btn-sm" data-scenekit-unload-scene="${escapeHtml(scene.sceneName ?? '')}" ${unloadDisabled ? 'disabled' : ''}>卸载场景</button>
    </div>`;
}

function renderSceneKitDetailSection(scene, stats) {
    return `<section class="kit-panel kit-panel--detail" data-scenekit-detail-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', '场景详情')}</div>
                <div class="kit-panel__desc">${escapeHtml(scene ? formatSceneKitSceneTitle(scene) : '未选择')}</div>
            </div>
        </div>
        ${renderSceneKitDetail(scene, stats)}
    </section>`;
}

function renderSceneKitBackendSection(stats) {
    return `<section class="kit-panel kit-panel--events">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('scene', '运行时后端')}</div>
                <div class="kit-panel__desc">ISceneBackend、当前激活场景和切换状态</div>
            </div>
        </div>
        <div class="kit-detail-summary kit-detail-summary--save-auto">
            <div><span>后端名</span><strong>${escapeHtml(stats?.backendName || '--')}</strong></div>
            <div><span>后端类型</span><strong>${escapeHtml(stats?.backendType || '--')}</strong></div>
            <div><span>激活场景</span><strong>${escapeHtml(stats?.activeSceneName || '--')}</strong></div>
            <div><span>加载数</span><strong>${escapeHtml(stats?.loadedSceneCount ?? 0)}</strong></div>
            <div><span>切换中</span><strong>${escapeHtml(stats?.isTransitioning ? '是' : '否')}</strong></div>
        </div>
        <div class="kit-note">快照由 Adapter 节流发布到 SceneKit/state；只有缺少 snapshot、用户点击刷新或执行卸载时才走命令桥。</div>
    </section>`;
}

function bindSceneKitWorkbenchActions() {
    $pageBody.querySelectorAll('[data-scenekit-scene]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectSceneKitScene(button.dataset.scenekitScene);
        });
    });
    $pageBody.querySelectorAll('[data-scenekit-unload-scene]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => unloadSceneKitScene(button.dataset.scenekitUnloadScene));
    });
    bindSceneKitSearch();
}

function bindSceneKitSearch() {
    const input = $pageBody.querySelector('[data-scenekit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            sceneKitState.searchTerm = input.value || '';
            updateSceneKitListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-scenekit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            sceneKitState.searchTerm = '';
            updateSceneKitListDom();
            $pageBody.querySelector('[data-scenekit-search]')?.focus();
        });
    }
}

function updateSceneKitListDom() {
    const visible = filterSceneKitScenes(sceneKitState.scenes);
    const list = $pageBody.querySelector('.kit-resource-list');
    if (list) list.innerHTML = renderSceneKitRows(visible);
    const count = $pageBody.querySelector('[data-scenekit-visible-count]');
    if (count) count.textContent = `${visible.length} / ${sceneKitState.scenes.length}`;
    const input = $pageBody.querySelector('[data-scenekit-search]');
    if (input && input.value !== sceneKitState.searchTerm) input.value = sceneKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-scenekit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !sceneKitState.searchTerm.trim());
    bindSceneKitWorkbenchActions();
}

function selectSceneKitScene(sceneName) {
    if (!sceneName) return;
    sceneKitState.selectedSceneName = sceneName;
    $pageBody.querySelectorAll('[data-scenekit-scene]').forEach(row => {
        row.classList.toggle('active', String(row.dataset.scenekitScene) === String(sceneKitState.selectedSceneName));
    });
    const selected = sceneKitState.scenes.find(item => String(item.sceneName) === String(sceneKitState.selectedSceneName)) ?? null;
    const detailPanel = $pageBody.querySelector('[data-scenekit-detail-panel]');
    if (detailPanel) detailPanel.outerHTML = renderSceneKitDetailSection(selected, sceneKitState.stats);
    bindSceneKitWorkbenchActions();
}

async function unloadSceneKitScene(sceneName) {
    if (!invoke || !connected || !sceneName) return;
    await sendKitCommandData('SceneKit', 'unload_scene', { sceneName });
    if (String(sceneKitState.selectedSceneName) === String(sceneName)) {
        sceneKitState.selectedSceneName = null;
    }
    await loadSceneKitWorkbench();
}

function formatSceneKitSceneTitle(scene) {
    const name = String(scene?.sceneName || '').trim();
    return name || `Scene ${scene?.buildIndex ?? '--'}`;
}

