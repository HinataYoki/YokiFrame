// ═══════════════════════════════════════════════════════════════════
// YokiFrame Editor —— Shell i18n and chrome bindings
// ═══════════════════════════════════════════════════════════════════

(function initYokiShellI18n(global) {
    const DEFAULT_LOCALE = 'zh-CN';

    function create(options) {
        const documentRef = options.document || global.document;
        const windowRef = options.window || global;
        const t = options.t || ((key) => key);
        const themeLabels = options.themeLabels || {};

        function getActiveTheme() {
            return options.getActiveTheme?.() || 'light';
        }

        function setActiveTheme(theme) {
            options.setActiveTheme?.(theme);
        }

        function applyTheme(theme, { silent = false } = {}) {
            const activeTheme = theme === 'dark' ? 'dark' : 'light';
            setActiveTheme(activeTheme);
            options.root?.setAttribute('data-theme', activeTheme);
            windowRef.localStorage?.setItem(options.themeKey, activeTheme);

            if (options.themeToggle) {
                const nextTheme = activeTheme === 'light' ? 'dark' : 'light';
                const nextLabel = t(`theme.${nextTheme}`);
                options.themeToggle.innerHTML = options.svgIcon?.(activeTheme === 'light' ? 'moon' : 'sun') || '';
                options.themeToggle.setAttribute('title', t('theme.switch_to', nextLabel));
                options.themeToggle.setAttribute('aria-label', t('theme.switch_to', nextLabel));
            }

            if (!silent) {
                options.addLog?.(t('theme.switched', t(themeLabels[activeTheme])), 'system');
            }

            return activeTheme;
        }

        function applyTranslatedAttribute(node, attributeName, key) {
            if (!node || !key) return;
            node.setAttribute(attributeName, t(key));
        }

        function applyStaticTranslations() {
            documentRef.querySelectorAll('[data-i18n]').forEach(node => {
                node.textContent = t(node.dataset.i18n);
            });
            documentRef.querySelectorAll('[data-i18n-title]').forEach(node => {
                applyTranslatedAttribute(node, 'title', node.dataset.i18nTitle);
            });
            documentRef.querySelectorAll('[data-i18n-aria-label]').forEach(node => {
                applyTranslatedAttribute(node, 'aria-label', node.dataset.i18nAriaLabel);
            });
            documentRef.title = t('app.title');
        }

        function updateFrameworkThemeSummary() {
            const themeValue = documentRef.getElementById('framework-theme-value');
            const modeValue = documentRef.getElementById('framework-mode-value');
            const activeTheme = getActiveTheme();

            if (themeValue) {
                themeValue.textContent = t(themeLabels[activeTheme]);
            }
            if (modeValue) {
                modeValue.textContent = activeTheme === 'light' ? t('font.apple_light') : t('font.apple_dark');
            }
        }

        function applyLocale(locale) {
            global.YokiI18n?.setLocale(locale);
            applyStaticTranslations();
            options.setConnectionStatus?.(options.isConnected?.() || false);
            applyTheme(getActiveTheme(), { silent: true });
            updateFrameworkThemeSummary();
        }

        function bindShellControls() {
            if (options.languageSelect) {
                options.languageSelect.value = global.YokiI18n?.getLocale() || DEFAULT_LOCALE;
                options.languageSelect.addEventListener('change', () => {
                    const locale = options.languageSelect.value;
                    applyLocale(locale);
                    options.addLog?.(t('connection.language_switched', locale), 'system');
                    options.refreshCurrentPage?.();
                });
            }

            if (options.themeToggle) {
                options.themeToggle.addEventListener('click', () => {
                    applyTheme(getActiveTheme() === 'light' ? 'dark' : 'light');
                    updateFrameworkThemeSummary();
                });
            }

            if (options.windowClose) {
                options.windowClose.addEventListener('click', async () => {
                    const currentWindow = global.__TAURI__?.window?.getCurrentWindow?.();
                    if (currentWindow && typeof currentWindow.close === 'function') {
                        await currentWindow.close();
                        return;
                    }
                    windowRef.close();
                });
            }
        }

        return {
            applyLocale,
            applyStaticTranslations,
            applyTheme,
            bindShellControls,
            updateFrameworkThemeSummary,
        };
    }

    window.YokiShellI18n = { create };
})(window);
