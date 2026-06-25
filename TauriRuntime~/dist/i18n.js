// ═══════════════════════════════════════════════════════════════════
// YokiFrame Editor — i18n 多语言模块
// ═══════════════════════════════════════════════════════════════════

const YokiI18n = (() => {
    const STORAGE_KEY = 'yokiframe-language';
    const DEFAULT_LOCALE = 'zh-CN';

    // 语言资源
    const translations = {
        'zh-CN': zhCN(),
        'en-US': enUS(),
    };

    let currentLocale = localStorage.getItem(STORAGE_KEY) || DEFAULT_LOCALE;

    function t(key, ...args) {
        const dict = translations[currentLocale] || translations[DEFAULT_LOCALE];
        const template = dict[key] ?? key;
        if (args.length === 0) return template;
        return template.replace(/\{(\d+)\}/g, (_, i) => args[Number(i)] ?? '');
    }

    function setLocale(locale) {
        if (!translations[locale]) return;
        currentLocale = locale;
        localStorage.setItem(STORAGE_KEY, locale);
        document.documentElement.lang = locale;
    }

    function getLocale() {
        return currentLocale;
    }

    function getSupportedLocales() {
        return Object.keys(translations);
    }

    return { t, setLocale, getLocale, getSupportedLocales };
})();

window.YokiI18n = YokiI18n;
