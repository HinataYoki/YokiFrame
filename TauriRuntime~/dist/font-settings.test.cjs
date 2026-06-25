const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

const distDir = __dirname;

function readDistFile(fileName) {
    return fs.readFileSync(path.join(distDir, fileName), 'utf8');
}

function expandCssImports(content, fromFileName, seen = new Set()) {
    return content.replace(/@import\s+url\(["']?([^"')]+)["']?\);\s*/g, (statement, importPath) => {
        const normalizedPath = path.normalize(path.join(path.dirname(fromFileName), importPath));
        if (seen.has(normalizedPath)) return '';
        seen.add(normalizedPath);
        const imported = readDistFile(normalizedPath);
        return `\n${expandCssImports(imported, normalizedPath, seen)}\n`;
    });
}

function readFrontendScripts() {
    return [
        'core/app.js',
        'core/window-state.js',
        'core/log-panel.js',
        'core/command-bridge.js',
        'core/ai-skill-installer.js',
        'core/brand-assets.js',
        'core/kit-reactive-refresh.js',
        'shared/dom.js',
        'pages/system.js',
        'main.js',
    ].map(file => readDistFile(file)).join('\n');
}

function loadFontSettings() {
    const helperPath = path.join(distDir, 'font-settings.js');
    assert.ok(fs.existsSync(helperPath), 'font-settings.js should exist next to the Tauri frontend bundle');

    delete global.window;
    delete global.YokiFontSettings;
    global.window = global;
    delete require.cache[require.resolve(helperPath)];
    require(helperPath);
    assert.ok(global.YokiFontSettings, 'font settings helper should attach to window');
    return global.YokiFontSettings;
}

test('font settings helper normalizes custom font families and rejects CSS control syntax', () => {
    const fontSettings = loadFontSettings();

    assert.equal(
        fontSettings.normalizeFontFamily('  "LXGW WenKai", Microsoft YaHei UI \n'),
        '"LXGW WenKai", Microsoft YaHei UI'
    );
    assert.equal(fontSettings.normalizeFontFamily('url(https://example.com/font.woff2)'), '');
    assert.equal(fontSettings.normalizeFontFamily('Bad; color: red'), 'Bad color: red');
});

test('font settings resolve and apply custom body font with default fallbacks', () => {
    const fontSettings = loadFontSettings();
    const root = {
        style: {
            values: new Map(),
            setProperty(name, value) {
                this.values.set(name, value);
            },
            removeProperty(name) {
                this.values.delete(name);
            },
        },
    };

    const state = fontSettings.normalizeFontSettings({
        preset: 'custom',
        customBody: '"LXGW WenKai"',
    });

    assert.equal(state.preset, 'custom');
    assert.equal(state.customBody, '"LXGW WenKai"');
    fontSettings.applyFontSettings(root, state);

    assert.equal(
        root.style.values.get('--font-body'),
        '"LXGW WenKai", "Segoe UI", "Microsoft YaHei UI", "PingFang SC", system-ui, sans-serif'
    );
    assert.equal(
        root.style.values.get('--font-display'),
        '"LXGW WenKai", "Segoe UI", "Microsoft YaHei UI", "PingFang SC", system-ui, sans-serif'
    );
    assert.equal(
        root.style.values.get('--font-mono'),
        '"JetBrains Mono", "Cascadia Code", ui-monospace, monospace'
    );
});

test('framework window exposes persisted font controls in the system overview', () => {
    const html = readDistFile('index.html');
    const js = readFrontendScripts();
    const css = expandCssImports(readDistFile('style.css'), 'style.css');

    assert.match(html, /<script src="font-settings\.js"><\/script>/);
    assert.match(js, /const FONT_SETTINGS_KEY\s*=\s*'yokiframe-font-settings'/);
    assert.match(js, /function renderFontPreferencePanel\(/);
    assert.match(js, /function bindFontPreferenceControls\(/);
    assert.match(js, /id="font-preset-select"/);
    assert.match(js, /id="font-custom-input"/);
    assert.match(js, /id="framework-font-value"/);
    assert.match(css, /\.font-preference/);
    assert.match(css, /\.font-preference__controls/);
});

test('font preference startup is guarded so failures cannot block shell controls', () => {
    const js = readFrontendScripts();
    const startup = readDistFile('main.js');
    const startupIndex = startup.indexOf('applyTheme(activeTheme, { silent: true });');

    assert.ok(startupIndex >= 0, 'startup sequence should apply theme before binding shell controls');
    assert.match(js, /function readStoredFontPreference\(/);
    assert.match(js, /function applyInitialFontPreference\(/);
    assert.match(js, /catch\s*\(e\)\s*\{[\s\S]*?console\.warn\('\[YokiFrame\] 字体偏好读取失败'/);
    assert.match(js, /catch\s*\(e\)\s*\{[\s\S]*?console\.warn\('\[YokiFrame\] 字体偏好应用失败'/);
    assert.match(startup.slice(startupIndex), /applyInitialFontPreference\(\);\s*bindShellControls\(\);/);
    assert.doesNotMatch(startup.slice(startupIndex, startupIndex + 180), /applyFontPreference\(\{ silent: true \}\);/);
});
