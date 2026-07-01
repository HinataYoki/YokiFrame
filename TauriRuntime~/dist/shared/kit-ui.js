function canShowStaticKitWorkbench(kit, engine = null) {
    if (kit !== 'EventKit') return false;
    const targetEngine = engine ?? getSelectedEventKitEngine?.();
    return engineSupportsCapability(targetEngine, 'static_scan');
}

function showRuntimeKitUnavailable(kit, label = kit) {
    clearMetrics();
    $pageBody.innerHTML = emptyState('bridge', t('runtime.need_bridge_short', label));
}

function syncSidebarKitAvailability() {
    if (!$sidebar || sidebarKitAvailabilitySyncInProgress) return;

    sidebarKitAvailabilitySyncInProgress = true;
    try {
        const targetEngine = getSelectedEngineForNavigation();
        const items = $sidebar.querySelectorAll('.sidebar-item[data-kit]');
        let fallbackPage = null;
        for (const item of items) {
            const kit = item.dataset.kit || KIT_PAGE_ID_TO_KIT[item.dataset.page];
            const available = !targetEngine || engineSupportsKit(targetEngine, kit);
            item.hidden = !available;
            if (activePage === item.dataset.page && !available) {
                fallbackPage = 'system';
            }
        }
        syncSidebarGroupVisibility();
        syncSidebarActiveIndicator();
        syncDocsAvailability();
        if (fallbackPage) {
            navigateTo(fallbackPage);
        }
    } finally {
        sidebarKitAvailabilitySyncInProgress = false;
    }
}

function syncSidebarGroupVisibility() {
    if (!$sidebar) return;

    $sidebar.querySelectorAll('.sidebar-group').forEach(group => {
        const items = Array.from(group.querySelectorAll('.sidebar-item'));
        if (!items.length) return;
        group.hidden = items.every(item => item.hidden);
    });
}

async function openKitCodeLocation(filePath, line) {
    if (!invoke || !connected || !filePath) return;
    try {
        await sendKitCommandData('System', 'open_code_location', {
            filePath,
            line: Number(line || 0) || 1,
        });
        addLog(t('source.open', `${filePath}${line ? ':' + line : ''}`), 'system');
    } catch (e) {
        addLog(t('source.open_failed', e), 'error');
    }
}

function bindKitCodeJumpButtons(root = $pageBody) {
    root.querySelectorAll('[data-kit-open-code]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            openKitCodeLocation(button.dataset.kitOpenCode, button.dataset.kitOpenLine);
        });
    });
}

function bindKitToggleChange(selector, handler, root = $pageBody) {
    const input = root.querySelector(selector);
    if (!input || input.dataset.bound === '1') return;
    input.dataset.bound = '1';
    input.addEventListener('change', handler);
}

function bindKitButtonClick(selector, handler, root = $pageBody) {
    const button = root.querySelector(selector);
    if (!button || button.dataset.bound === '1') return;
    button.dataset.bound = '1';
    button.addEventListener('click', handler);
}

function captureKitWorkbenchUiState(root = $pageBody) {
    const active = document.activeElement;
    const focus = active && root.contains(active) && active.matches('input, textarea, select, button')
        ? {
            selector: buildKitFocusSelector(active),
            value: active.value,
            selectionStart: active.selectionStart,
            selectionEnd: active.selectionEnd,
        }
        : null;
    const scrolls = Array.from(root.querySelectorAll('[data-kit-scroll-key]')).map(element => ({
        key: element.dataset.kitScrollKey,
        top: element.scrollTop,
        left: element.scrollLeft,
    }));

    return {
        bodyTop: $pageBody.scrollTop,
        bodyLeft: $pageBody.scrollLeft,
        scrolls,
        focus,
    };
}

function buildKitFocusSelector(element) {
    if (element.hasAttribute('data-tablekit-field')) {
        return `[data-tablekit-field="${escapeCssAttributeValue(element.dataset.tablekitField)}"]`;
    }
    if (element.hasAttribute('data-tablekit-pick-path')) {
        return `[data-tablekit-pick-path="${escapeCssAttributeValue(element.dataset.tablekitPickPath)}"]`;
    }
    if (element.hasAttribute('data-tablekit-extra-pick-folder')) {
        return `[data-tablekit-extra-pick-folder="${escapeCssAttributeValue(element.dataset.tablekitExtraPickFolder)}"]`;
    }
    if (element.hasAttribute('data-tablekit-toggle')) {
        return `[data-tablekit-toggle="${escapeCssAttributeValue(element.dataset.tablekitToggle)}"]`;
    }
    if (element.hasAttribute('data-tablekit-extra-field')) {
        return `[data-tablekit-extra-index="${escapeCssAttributeValue(element.dataset.tablekitExtraIndex)}"][data-tablekit-extra-field="${escapeCssAttributeValue(element.dataset.tablekitExtraField)}"]`;
    }
    const attrs = [
        'data-poolkit-search',
        'data-architecture-search',
        'data-actionkit-search',
        'data-reskit-search',
        'data-logkit-search',
        'data-tablekit-search',
        'data-singletonkit-search',
        'data-audiokit-search',
        'data-savekit-search',
        'data-spatialkit-search',
        'data-uikit-search',
    ];
    for (let i = 0; i < attrs.length; i++) {
        if (element.hasAttribute(attrs[i])) return `[${attrs[i]}]`;
    }
    return null;
}

function escapeCssAttributeValue(value) {
    return String(value ?? '').replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}

function restoreKitWorkbenchUiState(uiState, root = $pageBody) {
    if (!uiState) return;

    $pageBody.scrollTop = uiState.bodyTop || 0;
    $pageBody.scrollLeft = uiState.bodyLeft || 0;

    if (Array.isArray(uiState.scrolls)) {
        uiState.scrolls.forEach(item => {
            if (!item?.key) return;
            const element = root.querySelector(`[data-kit-scroll-key="${item.key}"]`);
            if (!element) return;
            element.scrollTop = item.top || 0;
            element.scrollLeft = item.left || 0;
        });
    }

    if (uiState.focus?.selector) {
        const nextFocus = root.querySelector(uiState.focus.selector);
        if (nextFocus) {
            nextFocus.focus({ preventScroll: true });
            if (typeof nextFocus.setSelectionRange === 'function') {
                const start = uiState.focus.selectionStart ?? String(uiState.focus.value ?? '').length;
                const end = uiState.focus.selectionEnd ?? start;
                nextFocus.setSelectionRange(start, end);
            }
        }
    }
}

function renderWorkbenchHtmlStable(state, html, signature, bind) {
    if (state.renderSignature === signature && !isPageLoadingStateVisible()) {
        if (typeof bind === 'function') bind();
        return;
    }
    const uiState = captureKitWorkbenchUiState();
    state.renderSignature = signature;
    $pageBody.innerHTML = html;
    if (typeof bind === 'function') bind();
    scheduleHeroActionPromotion();
    requestAnimationFrame(() => {
        restoreKitWorkbenchUiState(uiState);
    });
}

function compactNumber(value) {
    if (value === null || value === undefined || value === '') return '--';
    return String(value);
}

function percentText(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) return '0%';
    return Math.max(0, Math.min(100, Math.round(number * 100))) + '%';
}

function kitSearchMatches(term, values) {
    const query = String(term || '').trim().toLowerCase();
    if (!query) return true;
    return values.some(value => String(value ?? '').toLowerCase().includes(query));
}

function renderKitSearchInput(value, attrName, placeholder) {
    const hasQuery = !!String(value || '').trim();
    return `<label class="kit-search" role="search">
        ${svgIcon('search', 'kit-search__icon')}
        <input class="kit-search__input" type="search" ${attrName} value="${escapeHtml(value || '')}" placeholder="${escapeHtml(placeholder)}">
        <button class="kit-search__clear${hasQuery ? '' : ' is-empty'}" type="button" ${attrName}-clear title="${t('common.clear_search')}">${svgIcon('empty', 'kit-search__clear-icon')}</button>
    </label>`;
}

function renderKitTitle(iconName, label) {
    return `<div class="kit-title-with-icon">${svgIcon(iconName, 'kit-title-icon')}<span>${escapeHtml(label)}</span></div>`;
}

function normalizeKitCodeLocation(filePath, line) {
    const normalizedPath = String(filePath || '').replace(/\\/g, '/').trim();
    if (!normalizedPath) return null;
    const lineNumber = Number(line);
    return {
        filePath: normalizedPath,
        line: Number.isFinite(lineNumber) && lineNumber > 0 ? Math.floor(lineNumber) : null,
    };
}

function formatKitCodeLocation(location) {
    if (!location) return '--';
    const fileName = location.filePath.split('/').pop() || location.filePath;
    return location.line ? `${fileName}:${location.line}` : fileName;
}

function renderKitCodeJumpButton(filePath, line, label = null, extraClass = '') {
    const location = normalizeKitCodeLocation(filePath, line);
    if (!location) {
        return `<span class="kit-code-jump kit-code-jump--missing">${t('source.not_recorded')}</span>`;
    }
    const display = label || formatKitCodeLocation(location);
    const title = location.line ? `${location.filePath}:${location.line}` : location.filePath;
    return `<button class="kit-code-jump ${escapeHtml(extraClass)}" type="button" data-kit-open-code="${escapeHtml(location.filePath)}" data-kit-open-line="${escapeHtml(location.line ?? 1)}" title="${escapeHtml(title)}">
        ${svgIcon('jump', 'shell-icon')}
        <span>${escapeHtml(display)}</span>
    </button>`;
}

function renderKitToggle(label, checked, attrName, title = '') {
    return `<label class="kit-toggle"${title ? ` title="${escapeHtml(title)}"` : ''}>
        <input type="checkbox" ${attrName}${checked ? ' checked' : ''}>
        <span class="kit-toggle__track" aria-hidden="true"><span class="kit-toggle__thumb"></span></span>
        <span class="kit-toggle__label">${escapeHtml(label)}</span>
    </label>`;
}

