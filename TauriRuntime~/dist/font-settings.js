(function (global) {
    const DEFAULT_BODY_FONT = '"Segoe UI", "Microsoft YaHei UI", "PingFang SC", system-ui, sans-serif';
    const DEFAULT_MONO_FONT = '"JetBrains Mono", "Cascadia Code", ui-monospace, monospace';

    const PRESETS = [
        {
            id: 'system',
            label: '默认字体',
            hint: 'Segoe UI / Microsoft YaHei UI / PingFang SC',
            body: DEFAULT_BODY_FONT,
        },
        {
            id: 'microsoft-yahei',
            label: '微软雅黑 UI',
            hint: 'Windows 中文界面优先',
            body: '"Microsoft YaHei UI", "Segoe UI", "PingFang SC", system-ui, sans-serif',
        },
        {
            id: 'pingfang',
            label: '苹方 / PingFang',
            hint: 'macOS 中文界面优先',
            body: '"PingFang SC", "Microsoft YaHei UI", "Segoe UI", system-ui, sans-serif',
        },
        {
            id: 'custom',
            label: '自定义字体',
            hint: '输入本机已安装字体名',
            body: null,
        },
    ];

    function getPreset(presetId) {
        return PRESETS.find(preset => preset.id === presetId) || PRESETS[0];
    }

    function normalizeFontFamily(value) {
        if (typeof value !== 'string') return '';

        const normalized = value
            .replace(/[\u0000-\u001f\u007f]/g, ' ')
            .replace(/[;{}<>]/g, '')
            .replace(/\s+/g, ' ')
            .trim();

        if (!normalized || /(?:url|expression)\s*\(|@import/i.test(normalized)) return '';
        return normalized.length > 160 ? normalized.slice(0, 160).trim() : normalized;
    }

    function parseSettings(value) {
        if (!value) return {};
        if (typeof value === 'object') return value;
        if (typeof value !== 'string') return {};

        try {
            const parsed = JSON.parse(value);
            return parsed && typeof parsed === 'object' ? parsed : {};
        } catch {
            return {};
        }
    }

    function normalizeFontSettings(value) {
        const parsed = parseSettings(value);
        const preset = getPreset(parsed.preset).id;
        return {
            preset,
            customBody: normalizeFontFamily(parsed.customBody),
        };
    }

    function resolveFontSettings(value) {
        const state = normalizeFontSettings(value);
        const preset = getPreset(state.preset);

        if (state.preset === 'custom' && state.customBody) {
            const stack = `${state.customBody}, ${DEFAULT_BODY_FONT}`;
            return {
                ...state,
                body: stack,
                display: stack,
                mono: DEFAULT_MONO_FONT,
                label: state.customBody,
                hint: '自定义界面字体',
            };
        }

        return {
            ...state,
            body: preset.body || DEFAULT_BODY_FONT,
            display: preset.body || DEFAULT_BODY_FONT,
            mono: DEFAULT_MONO_FONT,
            label: preset.label,
            hint: preset.hint,
        };
    }

    function applyFontSettings(root, value) {
        const target = root || global.document?.documentElement;
        if (!target?.style) return null;

        const resolved = resolveFontSettings(value);
        target.style.setProperty('--font-body', resolved.body);
        target.style.setProperty('--font-display', resolved.display);
        target.style.setProperty('--font-mono', resolved.mono);
        return resolved;
    }

    function readFontSettings(storage, key) {
        if (!storage || !key) return normalizeFontSettings(null);

        try {
            return normalizeFontSettings(storage.getItem(key));
        } catch {
            return normalizeFontSettings(null);
        }
    }

    function writeFontSettings(storage, key, value) {
        if (!storage || !key) return normalizeFontSettings(value);

        const state = normalizeFontSettings(value);
        try {
            storage.setItem(key, JSON.stringify(state));
        } catch {
            return state;
        }
        return state;
    }

    global.YokiFontSettings = {
        DEFAULT_BODY_FONT,
        DEFAULT_MONO_FONT,
        PRESETS,
        getPreset,
        normalizeFontFamily,
        normalizeFontSettings,
        resolveFontSettings,
        applyFontSettings,
        readFontSettings,
        writeFontSettings,
    };
})(typeof window !== 'undefined' ? window : globalThis);
