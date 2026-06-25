// pages/localizationkit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：LocalizationKit
// ═══════════════════════════════════════════════════════════════════
const localizationKitState = {
    stats: {},
    languages: [],
    searchTerm: '',
    selectedLanguageId: null,
    renderSignature: '',
};

function renderLocalizationKitPage() {
    $pageBody.classList.add('content-body--localizationkit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--audiokit');
    $pageBody.classList.remove('content-body--savekit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('localizationkit.title'),
        t('localizationkit.subtitle'),
        t('localizationkit.tab'),
        'localization',
        `<button class="btn btn-primary btn-sm" onclick="refreshLocalizationKit()">${t('common.refresh')}</button>`
    );
    clearTabs();
    localizationKitState.renderSignature = '';
    loadLocalizationKitWorkbench();
}

async function refreshLocalizationKit() { loadLocalizationKitWorkbench(); }

async function refreshLocalizationKitReactive(event) {
    await loadLocalizationKitWorkbench();
}

function normalizeLocalizationKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const languagesSource = source.languages ?? {};
    return {
        stats: source.stats ?? {},
        languages: Array.isArray(source.languages)
            ? source.languages
            : (Array.isArray(languagesSource.languages) ? languagesSource.languages : []),
    };
}

async function fetchLocalizationKitWorkbenchState() {
    const telemetry = await readKitTelemetryData('LocalizationKit');
    if (telemetry) return normalizeLocalizationKitStatePayload(telemetry);

    const snapshot = await readKitSnapshotData('LocalizationKit');
    if (snapshot) return normalizeLocalizationKitStatePayload(snapshot);

    return null;
}

async function fetchLocalizationKitWorkbenchStateFromCommands() {
    const snapshot = await sendKitCommandData('LocalizationKit', 'get_workbench_snapshot');
    return normalizeLocalizationKitStatePayload(snapshot);
}

async function loadLocalizationKitWorkbench() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('search', '请连接引擎后查看本地化状态。');
        clearMetrics();
        return;
    }
    try {
        const snapshotState = await fetchLocalizationKitWorkbenchState();
        const state = snapshotState ?? await fetchLocalizationKitWorkbenchStateFromCommands();
        localizationKitState.stats = state.stats;
        localizationKitState.languages = state.languages;
        reconcileLocalizationKitSelection(localizationKitState.languages);
        clearMetrics();

        const html = renderLocalizationKitWorkbench(localizationKitState.stats, localizationKitState.languages);
        const signature = makeStableSignature({
            stats: localizationKitState.stats,
            languages: localizationKitState.languages,
            selected: localizationKitState.selectedLanguageId,
        });
        renderWorkbenchHtmlStable(localizationKitState, html, signature, bindLocalizationKitWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('LocalizationKit')) {
            showRuntimeKitUnavailable('LocalizationKit', 'LocalizationKit 本地化');
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function reconcileLocalizationKitSelection(languages) {
    if (!Array.isArray(languages) || !languages.length) {
        localizationKitState.selectedLanguageId = null;
        return null;
    }
    let selected = languages.find(item => String(item.id) === String(localizationKitState.selectedLanguageId));
    if (!selected) {
        selected = languages.find(item => item.isCurrent) ?? languages[0];
        localizationKitState.selectedLanguageId = selected.id;
    }
    return selected;
}

function renderLocalizationKitWorkbench(stats, languages) {
    const visibleLanguages = filterLocalizationKitLanguages(languages);
    const selected = languages.find(item => String(item.id) === String(localizationKitState.selectedLanguageId)) ?? null;
    return `<div class="kit-workbench kit-workbench--localization">
        <section class="kit-toolbar">
            <div>
                <div class="kit-toolbar__title">${renderKitTitle('search', '本地化工作台')}</div>
                <div class="kit-toolbar__meta">当前 ${escapeHtml(stats?.currentLanguage || '--')} · 默认 ${escapeHtml(stats?.defaultLanguage || '--')} · 语言 ${escapeHtml(stats?.availableLanguageCount ?? languages.length ?? 0)} · Binder ${escapeHtml(stats?.binderCount ?? 0)} · 文本缓存 ${escapeHtml(stats?.textCacheCount ?? 0)} · 复数缓存 ${escapeHtml(stats?.pluralCacheCount ?? 0)}</div>
            </div>
            <div class="kit-toolbar__actions">
                <span class="kit-state-pill ${stats?.providerType ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${escapeHtml(stats?.providerType || 'No Provider')}</span>
            </div>
        </section>
        <div class="kit-workbench-grid kit-workbench-grid--save">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('search', '语言列表')}</div>
                        <div class="kit-panel__desc">当前语言、默认语言、加载状态和表信息</div>
                    </div>
                    <span class="kit-panel__count" data-localizationkit-visible-count>${escapeHtml(visibleLanguages.length)} / ${escapeHtml(languages.length)}</span>
                </div>
                <div class="kit-panel__tools">${renderKitSearchInput(localizationKitState.searchTerm, 'data-localizationkit-search', '搜索语言、ID、加载状态或文本表')}</div>
                <div class="kit-resource-list" data-kit-scroll-key="localization-languages">${renderLocalizationKitRows(visibleLanguages)}</div>
            </section>
            ${renderLocalizationKitDetailSection(selected, stats)}
            ${renderLocalizationKitRuntimeSection(stats)}
        </div>
    </div>`;
}

function filterLocalizationKitLanguages(languages) {
    return (Array.isArray(languages) ? languages : []).filter(language => kitSearchMatches(localizationKitState.searchTerm, [
        language.id,
        language.numericId,
        language.displayNameTextId,
        language.nativeNameTextId,
        language.iconSpriteId,
        language.isLoaded ? 'loaded 已加载' : 'unloaded 未加载',
        language.isCurrent ? 'current 当前' : '',
        language.isDefault ? 'default 默认' : '',
    ]));
}

function renderLocalizationKitRows(languages) {
    if (!Array.isArray(languages) || !languages.length) {
        return emptyState('search', '暂无语言数据。调用 LocalizationKit.SetProvider 后会显示支持语言。');
    }
    return languages.map(language => {
        const selected = String(language.id) === String(localizationKitState.selectedLanguageId);
        const chips = [
            language.isCurrent ? '当前' : null,
            language.isDefault ? '默认' : null,
            language.isLoaded ? '已加载' : '未加载',
        ].filter(Boolean).join(' · ');
        return `<button class="kit-list-row${selected ? ' active' : ''}" type="button" data-localizationkit-language="${escapeHtml(language.id ?? '')}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(formatLocalizationKitLanguageTitle(language))}</strong>
                <em>${escapeHtml(chips)}</em>
            </span>
            <span class="kit-list-row__stats">id ${escapeHtml(language.numericId ?? '--')}</span>
        </button>`;
    }).join('');
}

function renderLocalizationKitDetail(language) {
    if (!language) {
        return emptyState('search', '选择一个语言后查看表信息并可切换当前语言。');
    }
    return `<div class="kit-detail-summary kit-detail-summary--save">
        <div><span>语言</span><strong>${escapeHtml(language.id ?? '--')}</strong></div>
        <div><span>数值 ID</span><strong>${escapeHtml(language.numericId ?? '--')}</strong></div>
        <div><span>显示名文本</span><strong>${escapeHtml(language.displayNameTextId ?? 0)}</strong></div>
        <div><span>原生名文本</span><strong>${escapeHtml(language.nativeNameTextId ?? 0)}</strong></div>
        <div><span>图标 ID</span><strong>${escapeHtml(language.iconSpriteId ?? 0)}</strong></div>
        <div><span>加载</span><strong>${escapeHtml(language.isLoaded ? '已加载' : '未加载')}</strong></div>
    </div>
    <div class="kit-note">LocalizationKit 的 Tauri 页面只读取 provider/formatter/cache/binder 诊断状态；切换语言会通过命令桥调用统一静态入口。</div>
    <div class="kit-code-action">
        <button class="btn btn-sm" data-localizationkit-set-language="${escapeHtml(language.id ?? '')}" ${language.isCurrent ? 'disabled' : ''}>设为当前语言</button>
    </div>`;
}

function renderLocalizationKitDetailSection(language, stats) {
    return `<section class="kit-panel kit-panel--detail" data-localizationkit-detail-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', '语言详情')}</div>
                <div class="kit-panel__desc">${escapeHtml(language ? formatLocalizationKitLanguageTitle(language) : '未选择')}</div>
            </div>
        </div>
        ${renderLocalizationKitDetail(language)}
    </section>`;
}

function renderLocalizationKitRuntimeSection(stats) {
    return `<section class="kit-panel kit-panel--events">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', '运行时后端')}</div>
                <div class="kit-panel__desc">Provider、Formatter、缓存和 Binder 状态</div>
            </div>
        </div>
        <div class="kit-detail-summary kit-detail-summary--save-auto">
            <div><span>Provider</span><strong>${escapeHtml(stats?.providerType || '--')}</strong></div>
            <div><span>Formatter</span><strong>${escapeHtml(stats?.formatterType || '--')}</strong></div>
            <div><span>Binder</span><strong>${escapeHtml(stats?.binderCount ?? 0)}</strong></div>
            <div><span>缓存</span><strong>${escapeHtml((stats?.textCacheCount ?? 0) + ' / ' + (stats?.pluralCacheCount ?? 0))}</strong></div>
        </div>
        <div class="kit-note">Unity 和 Godot 的差异应放在 provider 或 Adapter 后端里，业务仍使用 LocalizationKit.Get / SetLanguage 统一入口。</div>
    </section>`;
}

function bindLocalizationKitWorkbenchActions() {
    $pageBody.querySelectorAll('[data-localizationkit-language]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectLocalizationKitLanguage(button.dataset.localizationkitLanguage);
        });
    });
    $pageBody.querySelectorAll('[data-localizationkit-set-language]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => setLocalizationKitLanguage(button.dataset.localizationkitSetLanguage));
    });
    bindLocalizationKitSearch();
}

function bindLocalizationKitSearch() {
    const input = $pageBody.querySelector('[data-localizationkit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            localizationKitState.searchTerm = input.value || '';
            updateLocalizationKitListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-localizationkit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            localizationKitState.searchTerm = '';
            updateLocalizationKitListDom();
            $pageBody.querySelector('[data-localizationkit-search]')?.focus();
        });
    }
}

function updateLocalizationKitListDom() {
    const visible = filterLocalizationKitLanguages(localizationKitState.languages);
    const list = $pageBody.querySelector('.kit-resource-list');
    if (list) list.innerHTML = renderLocalizationKitRows(visible);
    const count = $pageBody.querySelector('[data-localizationkit-visible-count]');
    if (count) count.textContent = `${visible.length} / ${localizationKitState.languages.length}`;
    const input = $pageBody.querySelector('[data-localizationkit-search]');
    if (input && input.value !== localizationKitState.searchTerm) input.value = localizationKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-localizationkit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !localizationKitState.searchTerm.trim());
    bindLocalizationKitWorkbenchActions();
}

function selectLocalizationKitLanguage(languageId) {
    if (!languageId) return;
    localizationKitState.selectedLanguageId = languageId;
    $pageBody.querySelectorAll('[data-localizationkit-language]').forEach(row => {
        row.classList.toggle('active', String(row.dataset.localizationkitLanguage) === String(localizationKitState.selectedLanguageId));
    });
    const selected = localizationKitState.languages.find(item => String(item.id) === String(localizationKitState.selectedLanguageId)) ?? null;
    const detailPanel = $pageBody.querySelector('[data-localizationkit-detail-panel]');
    if (detailPanel) detailPanel.outerHTML = renderLocalizationKitDetailSection(selected, localizationKitState.stats);
    bindLocalizationKitWorkbenchActions();
}

async function setLocalizationKitLanguage(languageId) {
    if (!invoke || !connected || !languageId) return;
    await sendKitCommandData('LocalizationKit', 'set_language', { language: languageId });
    localizationKitState.selectedLanguageId = languageId;
    await loadLocalizationKitWorkbench();
}

function formatLocalizationKitLanguageTitle(language) {
    return language?.id || `Language ${language?.numericId ?? '--'}`;
}

