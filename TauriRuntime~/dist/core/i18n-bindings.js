// core/i18n-bindings.js
const shellI18n = window.YokiShellI18n?.create({
    document,
    window,
    t,
    root: $root,
    languageSelect: $languageSelect,
    themeToggle: $themeToggle,
    windowClose: $windowClose,
    themeKey: THEME_KEY,
    themeLabels: THEME_LABELS,
    svgIcon,
    addLog,
    setConnectionStatus,
    refreshCurrentPage,
    getActiveTheme: () => activeTheme,
    setActiveTheme: theme => { activeTheme = theme; },
    isConnected: () => connected,
}) ?? null;


function applyTheme(theme, { silent = false } = {}) {
    return shellI18n?.applyTheme(theme, { silent }) ?? theme;
}

function applyStaticTranslations() {
    shellI18n?.applyStaticTranslations();
}

function applyLocale(locale) {
    shellI18n?.applyLocale(locale);
}

