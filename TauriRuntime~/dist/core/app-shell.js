// core/app-shell.js
// Shell 通用 UI、指示器和字体偏好
// 面板构建器
// ═══════════════════════════════════════════════════════════════════
function resolveIconName(icon) {
    const aliases = {
        '□': 'framework',
        '≡': 'docs',
        '↗': 'bridge',
        '◎': 'status',
        '●': 'pool',
        '■': 'res',
        '○': 'fsm',
        '↔': 'action',
        '!': 'warning',
        '⌕': 'search',
        '\u{1F504}': 'refresh',
        '\u{1F4CB}': 'docs',
        '\u{1F4CC}': 'status',
        '\u{1F4DC}': 'log',
        '\u{1F500}': 'bridge',
        '\u{26A0}': 'warning',
    };
    return aliases[icon] ?? icon ?? 'empty';
}

function panel(title, bodyHtml, icon = '') {
    const iconHtml = icon ? `<span class="panel-header-icon">${svgIcon(resolveIconName(icon), 'shell-icon')}</span>` : '';
    return `
        <div class="panel fade-in">
            <div class="panel-header">
                <div class="panel-header-text">${iconHtml}<span class="panel-title">${title}</span></div>
            </div>
            <div class="panel-body">${bodyHtml}</div>
        </div>`;
}

function emptyState(icon, text) {
    return `<div class="empty-state"><div class="empty-state-icon">${svgIcon(resolveIconName(icon), 'shell-icon')}</div><div class="empty-state-text">${text}</div></div>`;
}

// HTML 文本转义（进 DOM 前必经，防注入与破坏标记）。
function escapeHtml(s) {
    return String(s)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

// CSS 选择器转义：用于把任意 id 安全拼进 querySelector('#'+id)。
// 优先用原生 CSS.escape，回退到对非单词字符做反斜杠转义。
function cssEscape(s) {
    if (window.CSS && typeof window.CSS.escape === 'function') return window.CSS.escape(s);
    return String(s).replace(/[^\w-]/g, '\\$&');
}

function prefersReducedMotion() {
    return !!window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches;
}

function syncSidebarActiveIndicator() {
    updateSidebarActiveIndicator(activePage);
}

function getSidebarScrollRoot() {
    return $sidebar?.querySelector('.sidebar-scroll-card') || $sidebar;
}

function updateSidebarActiveIndicator(pageId = activePage, options = {}) {
    if (!$sidebar) return;
    const { scroll = true, animate = true } = options;
    const scrollRoot = getSidebarScrollRoot();
    const indicator = scrollRoot?.querySelector('.sidebar-active-indicator');
    const item = scrollRoot?.querySelector(`.sidebar-item[data-page="${cssEscape(pageId)}"]`);
    if (!indicator || !item) return;

    if (scroll) {
        item.scrollIntoView({
            block: 'nearest',
            inline: 'nearest',
            behavior: prefersReducedMotion() ? 'auto' : 'smooth',
        });
        requestAnimationFrame(() => updateSidebarActiveIndicator(pageId, { scroll: false, animate: false }));
    }

    const sidebarRect = scrollRoot.getBoundingClientRect();
    const itemRect = item.getBoundingClientRect();
    const top = itemRect.top - sidebarRect.top + scrollRoot.scrollTop;
    const nextTransform = `translateY(${Math.round(top)}px)`;
    const didMove = indicator.style.transform && indicator.style.transform !== nextTransform;

    indicator.style.height = `${Math.round(itemRect.height)}px`;
    indicator.style.transform = nextTransform;
    indicator.classList.add('is-visible');

    if (!animate || !didMove) return;

    if (sidebarIndicatorMotionTimer) {
        clearTimeout(sidebarIndicatorMotionTimer);
    }
    indicator.classList.remove('is-settling');
    indicator.classList.add('is-moving');
    sidebarIndicatorMotionTimer = setTimeout(() => {
        indicator.classList.remove('is-moving');
        indicator.classList.add('is-settling');
        sidebarIndicatorMotionTimer = setTimeout(() => {
            indicator.classList.remove('is-settling');
            sidebarIndicatorMotionTimer = 0;
        }, SIDEBAR_INDICATOR_SETTLE_MS);
    }, SIDEBAR_INDICATOR_SETTLE_MS);
}

function bindShellControls() {
    shellI18n?.bindShellControls();
}

function updateFrameworkThemeSummary() {
    shellI18n?.updateFrameworkThemeSummary();
}

function getFontSettingsHelper() {
    return window.YokiFontSettings || null;
}

function readStoredFontPreference() {
    const helper = getFontSettingsHelper();
    if (!helper) return { ...DEFAULT_FONT_PREFERENCE };

    try {
        return helper.readFontSettings(localStorage, FONT_SETTINGS_KEY);
    } catch (e) {
        console.warn('[YokiFrame] 字体偏好读取失败', e);
        return { ...DEFAULT_FONT_PREFERENCE };
    }
}

function normalizeFontPreference(value) {
    const helper = getFontSettingsHelper();
    return helper ? helper.normalizeFontSettings(value) : { ...DEFAULT_FONT_PREFERENCE };
}

function getResolvedFontPreference() {
    const helper = getFontSettingsHelper();
    if (!helper) {
        return {
            preset: 'system',
            customBody: '',
            label: t('font.default'),
            hint: t('font.module_not_loaded'),
        };
    }

    return helper.resolveFontSettings(fontPreferenceState);
}

function applyFontPreference({ silent = false } = {}) {
    const helper = getFontSettingsHelper();
    if (!helper) return null;

    fontPreferenceState = normalizeFontPreference(fontPreferenceState);
    const resolved = helper.applyFontSettings($root, fontPreferenceState);
    helper.writeFontSettings(localStorage, FONT_SETTINGS_KEY, fontPreferenceState);
    updateFrameworkFontSummary(resolved);

    if (!silent && resolved) {
        addLog(t('connection.font_switched', resolved.label), 'system');
    }

    return resolved;
}

function applyInitialFontPreference() {
    try {
        applyFontPreference({ silent: true });
    } catch (e) {
        console.warn('[YokiFrame] 字体偏好应用失败', e);
        fontPreferenceState = { ...DEFAULT_FONT_PREFERENCE };
    }
}

function renderFontPresetOptions() {
    const helper = getFontSettingsHelper();
    const presets = helper?.PRESETS || [];
    return presets.map(preset => {
        const selected = preset.id === fontPreferenceState.preset ? ' selected' : '';
        return `<option value="${escapeHtml(preset.id)}"${selected}>${escapeHtml(preset.label)}</option>`;
    }).join('');
}

function renderFontPreferencePanel() {
    return panel(t('font.title'),
        renderFontPreferenceContent(),
        'font');
}

function renderFontPreferenceContent() {
    const resolved = getResolvedFontPreference();
    const customDisabled = fontPreferenceState.preset !== 'custom' ? ' disabled' : '';
    return `<div class="font-preference">
        <div class="font-preference__summary">
            <span>${t('font.current')}</span>
            <strong id="framework-font-value">${escapeHtml(resolved.label)}</strong>
            <code id="framework-font-hint">${escapeHtml(resolved.hint || '')}</code>
        </div>
        <div class="font-preference__controls">
            <label class="font-preference__field">
                <span>${t('font.preset')}</span>
                <select id="font-preset-select" class="cmd-select" aria-label="${t('font.preset')}">
                    ${renderFontPresetOptions()}
                </select>
            </label>
            <label class="font-preference__field font-preference__field--custom">
                <span>${t('font.custom')}</span>
                <input id="font-custom-input" class="cmd-input" type="text" value="${escapeHtml(fontPreferenceState.customBody)}" placeholder="LXGW WenKai"${customDisabled}>
            </label>
            <button id="font-reset-btn" class="btn btn-secondary btn-sm" type="button">${t('font.reset')}</button>
        </div>
    </div>`;
}

function updateFrameworkFontSummary(resolved = getResolvedFontPreference()) {
    const fontValue = document.getElementById('framework-font-value');
    const fontHint = document.getElementById('framework-font-hint');
    const presetSelect = document.getElementById('font-preset-select');
    const customInput = document.getElementById('font-custom-input');

    if (fontValue) fontValue.textContent = resolved.label;
    if (fontHint) fontHint.textContent = resolved.hint || '';
    if (presetSelect) presetSelect.value = fontPreferenceState.preset;
    if (customInput) {
        customInput.disabled = fontPreferenceState.preset !== 'custom';
        if (customInput.value !== fontPreferenceState.customBody) {
            customInput.value = fontPreferenceState.customBody;
        }
    }
}

function bindFontPreferenceControls() {
    const helper = getFontSettingsHelper();
    if (!helper) return;

    const presetSelect = document.getElementById('font-preset-select');
    const customInput = document.getElementById('font-custom-input');
    const resetButton = document.getElementById('font-reset-btn');

    if (presetSelect && presetSelect.dataset.bound !== '1') {
        presetSelect.dataset.bound = '1';
        presetSelect.addEventListener('change', () => {
            fontPreferenceState = normalizeFontPreference({
                preset: presetSelect.value,
                customBody: customInput?.value || fontPreferenceState.customBody,
            });
            applyFontPreference();
            updateFrameworkFontSummary();
            if (fontPreferenceState.preset === 'custom') customInput?.focus();
        });
    }

    if (customInput && customInput.dataset.bound !== '1') {
        customInput.dataset.bound = '1';
        customInput.addEventListener('input', () => {
            fontPreferenceState = normalizeFontPreference({
                preset: 'custom',
                customBody: customInput.value,
            });
            applyFontPreference({ silent: true });
        });
        customInput.addEventListener('change', () => {
            fontPreferenceState = normalizeFontPreference({
                preset: 'custom',
                customBody: customInput.value,
            });
            applyFontPreference();
        });
    }

    if (resetButton && resetButton.dataset.bound !== '1') {
        resetButton.dataset.bound = '1';
        resetButton.addEventListener('click', () => {
            fontPreferenceState = normalizeFontPreference(DEFAULT_FONT_PREFERENCE);
            applyFontPreference();
            updateFrameworkFontSummary();
        });
    }
}

// ═══════════════════════════════════════════════════════════════════
