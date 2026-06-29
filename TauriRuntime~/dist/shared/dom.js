// shared/dom.js
function svgIcon(name, className = 'shell-icon') {
    const icons = {
        framework: '<svg viewBox="0 0 24 24" class="' + className + '"><rect x="4.5" y="5.5" width="15" height="13" rx="2.5" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M9 18.5h6" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        docs: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M7 5.5h10.5a2 2 0 0 1 2 2v11H9a2 2 0 0 0-2 2V7.5a2 2 0 0 1 2-2Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M9.5 9h6M9.5 12h6M9.5 15h4.5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        folder: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M4.5 8.5h5.3l1.6 2h8.1v7a2 2 0 0 1-2 2h-11a2 2 0 0 1-2-2v-9Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M4.5 8.5V7a2 2 0 0 1 2-2h3l1.7 2h6.3a2 2 0 0 1 2 2v1.5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/></svg>',
        file: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M7 4.5h6.5L18 9v10.5H7v-15Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M13.5 4.5V9H18M9.5 13h5M9.5 16h4" fill="none" stroke="currentColor" stroke-width="1.55" stroke-linecap="round"/></svg>',
        bridge: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="m7 12 4-4 4 4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/><path d="m7 16 4-4 4 4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" opacity="0.55"/><path d="M15.5 8H11V3.5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        fsm: '<svg viewBox="0 0 24 24" class="' + className + '"><circle cx="7" cy="7" r="2.2" fill="currentColor"/><circle cx="17" cy="7" r="2.2" fill="currentColor"/><circle cx="12" cy="17" r="2.2" fill="currentColor"/><path d="M8.9 8.5 10.8 15M15.1 8.5 13.2 15M9.2 7h5.6" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
        event: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M12 4.5v5.2" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/><path d="m8.8 10.2 3.2 8.3 3.2-8.3" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M6.5 8.2h2.4M15.1 8.2h2.4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        pool: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M6 7.5h12M7.5 12h9M9 16.5h6" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/><rect x="5" y="5" width="14" height="14" rx="3" fill="none" stroke="currentColor" stroke-width="1.4" opacity="0.55"/></svg>',
        res: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M5.5 8.5 12 5l6.5 3.5v7L12 19l-6.5-3.5v-7Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M5.5 8.5 12 12l6.5-3.5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/></svg>',
        table: '<svg viewBox="0 0 24 24" class="' + className + '"><rect x="5" y="5.5" width="14" height="13" rx="2.2" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M5 9.5h14M5 13.5h14M9.5 5.5v13M14.5 5.5v13" fill="none" stroke="currentColor" stroke-width="1.45" stroke-linecap="round"/></svg>',
        architecture: '<svg viewBox="0 0 24 24" class="' + className + '"><rect x="5" y="5" width="14" height="14" rx="2.4" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M8.5 9h7M8.5 12h7M8.5 15h4.5" fill="none" stroke="currentColor" stroke-width="1.55" stroke-linecap="round"/><path d="M17.5 7.2 20 5M17.5 16.8 20 19M6.5 7.2 4 5M6.5 16.8 4 19" fill="none" stroke="currentColor" stroke-width="1.45" stroke-linecap="round" opacity="0.62"/></svg>',
        singleton: '<svg viewBox="0 0 24 24" class="' + className + '"><circle cx="12" cy="8" r="3.2" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M6.5 19.2c.9-3 2.9-5 5.5-5s4.6 2 5.5 5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/><path d="M18.5 6.5h1.8M3.7 6.5h1.8M12 2.8v1.4" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" opacity="0.65"/></svg>',
        action: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M6 12h12" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round"/><path d="m13 8 4 4-4 4" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        input: '<svg viewBox="0 0 24 24" class="' + className + '"><rect x="4.5" y="6.5" width="15" height="11" rx="2.4" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M8 10h.01M11 10h.01M14 10h.01M17 10h.01M8 13.5h2.2M12 13.5h4" fill="none" stroke="currentColor" stroke-width="1.9" stroke-linecap="round"/><path d="M7.5 17.5 5.6 20M16.5 17.5l1.9 2.5" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" opacity="0.65"/></svg>',
        localization: '<svg viewBox="0 0 24 24" class="' + className + '"><circle cx="12" cy="12" r="7.5" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M4.8 12h14.4M12 4.5c2 2.1 3 4.6 3 7.5s-1 5.4-3 7.5M12 4.5c-2 2.1-3 4.6-3 7.5s1 5.4 3 7.5" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round"/></svg>',
        ui: '<svg viewBox="0 0 24 24" class="' + className + '"><rect x="5" y="5.5" width="14" height="12.5" rx="2.4" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M8 9h8M8 12.2h4.2M8 15.4h7.2" fill="none" stroke="currentColor" stroke-width="1.55" stroke-linecap="round"/><path d="M9.5 18.5h5" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" opacity="0.65"/></svg>',
        audio: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M5 10.2h3.2L12.5 6v12l-4.3-4.2H5z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M15.5 9.2a4 4 0 0 1 0 5.6M17.9 6.8a7.4 7.4 0 0 1 0 10.4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        save: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M6.5 5.5h9.2L18.5 8v10.5h-13v-13Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M8.5 5.5v5h7v-5M8.5 18.5v-4.2h7v4.2" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/></svg>',
        scene: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M5.5 6.5h13v11h-13v-11Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M8.5 15.5 11 12l2 2.4 1.6-1.9 2.9 3" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/><circle cx="9" cy="9.2" r="1.1" fill="currentColor"/></svg>',
        spatial: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M5.5 7.5h13M5.5 12h13M5.5 16.5h13M7.5 5.5v13M12 5.5v13M16.5 5.5v13" fill="none" stroke="currentColor" stroke-width="1.35" stroke-linecap="round"/><circle cx="8.5" cy="8.5" r="1.3" fill="currentColor"/><circle cx="15.5" cy="13.2" r="1.3" fill="currentColor"/><circle cx="11.5" cy="16.2" r="1.3" fill="currentColor"/></svg>',
        command: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M7 7.5 4.5 10 7 12.5M17 7.5 19.5 10 17 12.5M10 16.5h4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        status: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M5.5 18.5h13M8 14.5l3-3 2 2 4-5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
        log: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M7.5 6.5h9M7.5 11.5h9M7.5 16.5h6" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/><rect x="4.5" y="4.5" width="15" height="15" rx="2.5" fill="none" stroke="currentColor" stroke-width="1.5" opacity="0.55"/></svg>',
        moon: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M14.8 3.4a8.9 8.9 0 1 0 5.8 15.7 8 8 0 1 1-5.8-15.7Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/></svg>',
        sun: '<svg viewBox="0 0 24 24" class="' + className + '"><circle cx="12" cy="12" r="4.2" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="M12 2.8v2.4M12 18.8v2.4M21.2 12h-2.4M5.2 12H2.8M18.5 5.5l-1.7 1.7M7.2 16.8l-1.7 1.7M18.5 18.5l-1.7-1.7M7.2 7.2 5.5 5.5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        warning: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M12 5.5 19 18H5L12 5.5Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M12 9v4.5M12 16.5h.01" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        empty: '<svg viewBox="0 0 24 24" class="' + className + '"><rect x="5.5" y="5.5" width="13" height="13" rx="2.5" fill="none" stroke="currentColor" stroke-width="1.5" opacity="0.55"/><path d="M9 12h6" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        search: '<svg viewBox="0 0 24 24" class="' + className + '"><circle cx="11" cy="11" r="6.5" fill="none" stroke="currentColor" stroke-width="1.7"/><path d="m16.2 16.2 3.4 3.4" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        pressure: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M5 17a7 7 0 0 1 14 0" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/><path d="m12 17 3.5-5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/><path d="M7.5 17h9" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        leak: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M12 3.8 16.5 10a6 6 0 1 1-9 0L12 3.8Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M12 9.5v4.2M12 16.7h.01" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        jump: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M9 6.5H6.5a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h9a2 2 0 0 0 2-2V15" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/><path d="M13 4.5h6.5V11" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/><path d="m11.5 12.5 7.5-7.5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/></svg>',
        package: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M6 8.2 12 5l6 3.2v7.6L12 19l-6-3.2V8.2Z" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linejoin="round"/><path d="M6 8.2 12 11.5l6-3.3M12 11.5V19" fill="none" stroke="currentColor" stroke-width="1.55" stroke-linejoin="round"/><path d="m9.2 6.5 6 3.3" fill="none" stroke="currentColor" stroke-width="1.25" stroke-linecap="round" opacity="0.58"/></svg>',
        github: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M12 4.2a7.7 7.7 0 0 0-2.4 15c.4.1.6-.2.6-.5v-1.8c-2.4.5-2.9-1-2.9-1-.4-1-.9-1.3-.9-1.3-.8-.5.1-.5.1-.5.8.1 1.3.9 1.3.9.8 1.3 2 1 2.5.8.1-.6.3-1 .5-1.2-1.9-.2-3.9-1-3.9-4.2 0-.9.3-1.7.9-2.3-.1-.2-.4-1.1.1-2.3 0 0 .7-.2 2.4.9.7-.2 1.4-.3 2.1-.3s1.4.1 2.1.3c1.6-1.1 2.4-.9 2.4-.9.5 1.2.2 2.1.1 2.3.6.6.9 1.4.9 2.3 0 3.3-2 4-3.9 4.2.3.3.6.8.6 1.6v2.4c0 .3.2.6.7.5A7.7 7.7 0 0 0 12 4.2Z" fill="currentColor"/></svg>',
        brand: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M12 2 4.5 6.4v11.1L12 22l7.5-4.5V6.4L12 2Z" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"/><path d="M4.5 6.4 12 10.8l7.5-4.4" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linejoin="round"/><path d="M12 10.8V22" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round"/></svg>',
        font: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M5.5 7.5V5.5h13v2M12 5.5v13M9 18.5h6" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/><path d="M7.5 11.5h9" fill="none" stroke="currentColor" stroke-width="1.45" stroke-linecap="round" opacity="0.55"/></svg>',
        refresh: '<svg viewBox="0 0 24 24" class="' + className + '"><path d="M18 10a6 6 0 1 0 1.2 5.9" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round"/><path d="M18 5.5v4.5h-4.5" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"/></svg>',
    };
    return icons[name] || icons.empty;
}

function applyTheme(theme, { silent = false } = {}) {
    return shellI18n?.applyTheme(theme, { silent }) ?? theme;
}

function applyStaticTranslations() {
    shellI18n?.applyStaticTranslations();
}

function applyLocale(locale) {
    shellI18n?.applyLocale(locale);
}

function setHero(title, summary, eyebrow, iconName, actionsHtml = '') {
    currentHeroMeta = { title, summary, eyebrow, iconName };
    currentHeroActionsHtml = actionsHtml || '';
    updateExistingHeroIntroCard();
    scheduleHeroActionPromotion();
    return { title, summary, eyebrow, iconName, actionsHtml };
}

function clearHero() {
    currentHeroMeta = null;
    currentHeroActionsHtml = '';
    if (heroActionPromotionFrame) {
        cancelAnimationFrame(heroActionPromotionFrame);
        heroActionPromotionFrame = 0;
    }
    $pageBody?.querySelector('[data-hero-intro-card="1"]')?.remove();
    $pageBody?.querySelectorAll('[data-promoted-hero-actions="1"]').forEach(element => element.remove());
}

function updateExistingHeroIntroCard() {
    if (!$pageBody || !currentHeroMeta || !shouldKeepHeroIntroCard()) return;
    const existing = $pageBody.querySelector('[data-hero-intro-card="1"]');
    if (!existing) return;

    const template = document.createElement('template');
    template.innerHTML = renderHeroIntroCard(currentHeroMeta, currentHeroActionsHtml);
    const card = template.content.firstElementChild;
    if (card) existing.replaceWith(card);
}

function scheduleHeroActionPromotion() {
    if (!$pageBody || (!currentHeroMeta && !currentHeroActionsHtml) || heroActionPromotionFrame) return;
    heroActionPromotionFrame = requestAnimationFrame(() => {
        heroActionPromotionFrame = 0;
        if (isPageLoadingStateVisible()) {
            setTimeout(scheduleHeroActionPromotion, 48);
            return;
        }
        promoteHeroActionsToFirstCard();
        ensureHeroIntroCard();
        if ((currentHeroMeta || currentHeroActionsHtml) &&
            !$pageBody.querySelector('[data-promoted-hero-actions="1"], [data-hero-intro-card="1"]') &&
            !$pageBody.querySelector(HERO_CARD_TARGET_SELECTOR)) {
            setTimeout(scheduleHeroActionPromotion, 48);
        }
    });
}

function isPageLoadingStateVisible() {
    return !!$pageBody?.querySelector('[data-page-loading-state="1"]');
}

function promoteHeroActionsToFirstCard() {
    if (!$pageBody || !currentHeroActionsHtml) return;
    if ($pageBody.querySelector('[data-promoted-hero-actions="1"]')) return;
    if (shouldKeepHeroIntroCard()) return;

    const target = $pageBody.querySelector(HERO_CARD_TARGET_SELECTOR);
    if (!target) return;

    const actions = createPromotedHeroActions(target);
    if (!actions) return;

    let container = target.querySelector('.kit-toolbar__actions, .kit-panel__actions, .tablekit-command-center__buttons, .log-header-actions');
    if (!container) {
        container = document.createElement('div');
        container.className = target.matches('.kit-toolbar, .audio-master-strip, .tablekit-command-center, .doc-hero, .developer-context-strip')
            ? 'kit-toolbar__actions'
            : 'kit-panel__actions';

        const head = target.querySelector('.kit-panel__head, .tool-pane-header, .panel-header, .log-header');
        (head || target).appendChild(container);
    }

    container.appendChild(actions);
}

function ensureHeroIntroCard() {
    if (!$pageBody || !currentHeroMeta) return;
    if ($pageBody.querySelector('[data-hero-intro-card="1"]')) return;

    const target = $pageBody.querySelector(HERO_CARD_TARGET_SELECTOR);
    if (target && !shouldKeepHeroIntroCard()) return;

    const template = document.createElement('template');
    template.innerHTML = renderHeroIntroCard(currentHeroMeta, currentHeroActionsHtml);
    const card = template.content.firstElementChild;
    if (!card) return;
    $pageBody.prepend(card);
}

function shouldKeepHeroIntroCard() {
    return HERO_INTRO_CARD_PAGE_IDS.has(activePage);
}

function renderHeroIntroCard(meta, actionsHtml = '') {
    const iconHtml = meta?.iconName ? svgIcon(meta.iconName, 'hero-intro-card__icon') : '';
    const summaryHtml = meta?.summary ? `<p>${escapeHtml(meta.summary)}</p>` : '';
    const actions = actionsHtml ? `<div class="hero-intro-card__actions" data-promoted-hero-actions="1">${actionsHtml}</div>` : '';
    return `<section class="hero-intro-card fade-in" data-hero-intro-card="1">
        <div class="hero-intro-card__main">
            <div class="hero-intro-card__title-row">${iconHtml}<div><h1>${escapeHtml(meta?.title ?? '')}</h1></div></div>
            ${summaryHtml}
        </div>
        ${actions}
    </section>`;
}

function createPromotedHeroActions(target) {
    const template = document.createElement('template');
    template.innerHTML = currentHeroActionsHtml;
    const actions = Array.from(template.content.children)
        .filter(element => element.tagName === 'BUTTON')
        .filter(element => !cardAlreadyHasAction(target, element));

    if (!actions.length) return null;

    const group = document.createElement('div');
    group.className = 'promoted-hero-actions';
    group.dataset.promotedHeroActions = '1';
    actions.forEach(action => {
        if (!action.getAttribute('type')) action.setAttribute('type', 'button');
        group.appendChild(action);
    });
    return group;
}

function cardAlreadyHasAction(target, action) {
    const actionText = normalizeActionIdentity(action.textContent);
    const actionClick = normalizeActionIdentity(action.getAttribute('onclick'));
    return Array.from(target.querySelectorAll('button')).some(button => {
        if (button.closest('[data-promoted-hero-actions="1"]')) return false;
        const buttonText = normalizeActionIdentity(button.textContent);
        const buttonClick = normalizeActionIdentity(button.getAttribute('onclick'));
        return (actionClick && actionClick === buttonClick) || (actionText && actionText === buttonText);
    });
}

function normalizeActionIdentity(value) {
    return String(value || '').replace(/\s+/g, '').trim();
}


function renderMetrics(cards) {
    $metricStrip.innerHTML = cards.map(c => `
        <div class="metric-card">
            <div class="metric-card__title">${c.title}</div>
            <div class="metric-card__value">${c.value}</div>
            ${c.hint ? `<div class="metric-card__hint">${c.hint}</div>` : ''}
        </div>
    `).join('');
    $metricStrip.removeAttribute('hidden');
}

function clearMetrics() { if ($metricStrip) $metricStrip.setAttribute('hidden', ''); }

// ═══════════════════════════════════════════════════════════════════
// 标签页辅助
// ═══════════════════════════════════════════════════════════════════
function ensureTabActiveIndicator() {
    if (!$tabBar) return null;
    let indicator = $tabBar.querySelector('.tab-active-indicator');
    if (!indicator) {
        indicator = document.createElement('span');
        indicator.className = 'tab-active-indicator';
        indicator.setAttribute('aria-hidden', 'true');
        $tabBar.appendChild(indicator);
    }
    return indicator;
}

function syncTabActiveIndicator(tabId = activeTab, options = {}) {
    if (!$tabBar) return;
    const { scroll = true, animate = true } = options;
    const indicator = ensureTabActiveIndicator();
    const button = $tabBar.querySelector(`.tab-button[data-tab="${cssEscape(tabId)}"]`) || $tabBar.querySelector('.tab-button.active');
    if (!indicator || !button) {
        indicator?.classList.remove('is-visible', 'is-moving', 'is-settling');
        return;
    }

    if (scroll) {
        button.scrollIntoView({
            block: 'nearest',
            inline: 'nearest',
            behavior: prefersReducedMotion() ? 'auto' : 'smooth',
        });
        requestAnimationFrame(() => syncTabActiveIndicator(tabId, { scroll: false, animate: false }));
    }

    const tabBarRect = $tabBar.getBoundingClientRect();
    const buttonRect = button.getBoundingClientRect();
    const left = buttonRect.left - tabBarRect.left + $tabBar.scrollLeft;
    const nextTransform = `translateX(${Math.round(left)}px)`;
    const didMove = indicator.style.transform && indicator.style.transform !== nextTransform;

    indicator.style.width = `${Math.round(buttonRect.width)}px`;
    indicator.style.transform = nextTransform;
    indicator.classList.add('is-visible');

    if (!animate || !didMove) return;

    if (tabIndicatorMotionTimer) {
        clearTimeout(tabIndicatorMotionTimer);
    }
    indicator.classList.remove('is-settling');
    indicator.classList.add('is-moving');
    tabIndicatorMotionTimer = setTimeout(() => {
        indicator.classList.remove('is-moving');
        indicator.classList.add('is-settling');
        tabIndicatorMotionTimer = setTimeout(() => {
            indicator.classList.remove('is-settling');
            tabIndicatorMotionTimer = 0;
        }, TAB_INDICATOR_SETTLE_MS);
    }, TAB_INDICATOR_SETTLE_MS);
}

function renderTabButtons(tabs, activeId = tabs[0]?.id ?? null) {
    return `${tabs.map(t => `
        <button class="tab-button${t.id === activeId ? ' active' : ''}" data-tab="${t.id}">${t.label}</button>
    `).join('')}<span class="tab-active-indicator" aria-hidden="true"></span>`;
}

function renderTabs(tabs, onSelect) {
    activeTab = tabs[0]?.id ?? null;
    $tabBar.innerHTML = renderTabButtons(tabs, activeTab);
    $tabBar.querySelectorAll('.tab-button').forEach(btn => {
        btn.addEventListener('click', () => {
            $tabBar.querySelectorAll('.tab-button').forEach(b => b.classList.remove('active'));
            btn.classList.add('active');
            activeTab = btn.dataset.tab;
            syncTabActiveIndicator(activeTab);
            if (onSelect) onSelect(activeTab);
        });
    });
    syncTabActiveIndicator(activeTab, { scroll: false, animate: false });
}

function clearTabs() {
    activeTab = null;
    if (tabIndicatorMotionTimer) {
        clearTimeout(tabIndicatorMotionTimer);
        tabIndicatorMotionTimer = 0;
    }
    $tabBar.innerHTML = '';
}

function renderPageLoadingState() {
    return `<div class="page-loading-state" data-page-loading-state="1" role="status" aria-live="polite">
        <span class="page-loading-state__text">${t('common.loading')}</span>
    </div>`;
}

function currentPageLoadToken(pageId = activePage) {
    return { pageId, navigationSeq };
}

function isCurrentPageLoad(pageLoadToken) {
    return !!pageLoadToken && isCurrentNavigation(pageLoadToken.navigationSeq, pageLoadToken.pageId);
}

function setPageBodyForLoad(pageLoadToken, html) {
    if (!isCurrentPageLoad(pageLoadToken)) return false;
    $pageBody.innerHTML = html;
    scheduleHeroActionPromotion();
    return true;
}

function renderMetricsForLoad(pageLoadToken, cards) {
    if (!isCurrentPageLoad(pageLoadToken)) return false;
    renderMetrics(cards);
    return true;
}

function clearMetricsForLoad(pageLoadToken) {
    if (!isCurrentPageLoad(pageLoadToken)) return false;
    clearMetrics();
    return true;
}

function makeStableSignature(value) {
    try {
        return JSON.stringify(value, (_, item) => {
            if (!item || typeof item !== 'object' || Array.isArray(item)) return item;
            const sorted = {};
            Object.keys(item).sort().forEach(key => { sorted[key] = item[key]; });
            return sorted;
        });
    } catch (_) {
        return String(Date.now());
    }
}

function signaturePart(value) {
    if (value === null || value === undefined) return '';
    return String(value);
}

function makeRowEdgeSignature(rows, pick) {
    if (!Array.isArray(rows) || !rows.length) return '0';
    const first = rows[0] ?? {};
    const last = rows[rows.length - 1] ?? {};
    return `${rows.length}:${pick(first)}:${pick(last)}`;
}

function makeRowsSignature(rows, pick) {
    if (!Array.isArray(rows) || !rows.length) return '0';
    let out = '';
    for (let i = 0; i < rows.length; i++) {
        if (i) out += '^';
        out += `${i}:${pick(rows[i] ?? {})}`;
    }
    return out;
}

function makeFsmListSignature(fsms) {
    return makeRowsSignature(fsms, fsm => [
        signaturePart(fsm?.name),
        signaturePart(fsm?.currentState),
        signaturePart(fsm?.machineState),
        signaturePart(fsm?.stateCount),
    ].join('|'));
}

function makeFsmDetailSignature(detail) {
    if (!detail) return 'none';
    const statesCount = Array.isArray(detail.states) ? detail.states.length : 0;
    const compositesCount = Array.isArray(detail.composites) ? detail.composites.length : 0;
    return [
        detail.error ? 'error' : 'ok',
        signaturePart(detail.error),
        signaturePart(detail.fsmName),
        signaturePart(detail.currentState),
        signaturePart(detail.currentStateId),
        signaturePart(detail.machineState),
        signaturePart(detail.stateCount),
        statesCount,
        compositesCount,
    ].join('|');
}

function makeFsmHistorySignature(history) {
    return makeRowEdgeSignature(history, item => [
        signaturePart(item?.from),
        signaturePart(item?.to),
        signaturePart(item?.time ?? item?.timestamp ?? item?.at),
    ].join('>'));
}

function makeFsmWorkbenchSignature(fsms, selectedMeta, detail, history, emptyMessage) {
    return [
        makeFsmListSignature(fsms),
        signaturePart(selectedMeta?.name ?? selectedFsmName ?? ''),
        makeFsmDetailSignature(detail),
        makeFsmHistorySignature(history),
        signaturePart(emptyMessage),
        signaturePart(fsmListSearchTerm),
    ].join('||');
}

function makeEventKitRegistrationSignature(regs) {
    const typeEvents = regs?.typeEvents ?? [];
    const enumEvents = regs?.enumEvents ?? [];
    const stringEvents = regs?.stringEvents ?? [];
    const pick = row => [
        signaturePart(row?.channel),
        signaturePart(row?.key ?? row?.type ?? row?.eventKey),
        signaturePart(row?.payloadType),
        signaturePart(row?.handlerCount ?? row?.count),
    ].join('|');
    return [
        makeRowsSignature(typeEvents, pick),
        makeRowsSignature(enumEvents, pick),
        makeRowsSignature(stringEvents, pick),
    ].join('||');
}

function makeEventKitRecentEventsSignature(events) {
    return makeRowEdgeSignature(events, event => makeEventKitRecentEventSignature(event ?? {}));
}

function makeEventKitScanSignature(scan) {
    if (!scan) return 'no-scan';
    if (scan.error) return `error:${signaturePart(scan.error)}`;
    const summary = scan.summary ?? {};
    const events = Array.isArray(scan.events) ? scan.events : [];
    const pick = event => [
        normalizeEventKitChannel(event?.channel),
        signaturePart(event?.eventKey ?? event?.key),
        signaturePart(event?.payloadType),
        signaturePart(event?.sendCount),
        signaturePart(event?.registerCount),
        signaturePart(event?.unregisterCount),
    ].join('|');
    return [
        signaturePart(summary.sendCount),
        signaturePart(summary.registerCount),
        signaturePart(summary.unregisterCount),
        signaturePart(summary.unmatchedSendCount),
        signaturePart(summary.unmatchedRegisterCount),
        signaturePart(summary.deprecatedStringEventCount),
        makeRowsSignature(events, pick),
    ].join('||');
}

function makeEventKitMonitorSignature(data, scan, search, engineId, selectedMonitorKey, selectedScanKey) {
    const counts = normalizeEventKitCounts(data?.counts);
    return [
        counts.typeEvents,
        counts.enumEvents,
        counts.stringEvents,
        counts.totalEvents,
        counts.totalHandlers,
        makeEventKitRegistrationSignature(data?.registrations),
        makeEventKitRecentEventsSignature(data?.recentEvents),
        makeEventKitScanSignature(scan),
        signaturePart(search),
        signaturePart(engineId),
        signaturePart(selectedMonitorKey),
        signaturePart(selectedScanKey),
    ].join('||');
}

function getKitInteractionRemainingMs() {
    return Math.max(0, kitInteractionActiveUntil - Date.now());
}

