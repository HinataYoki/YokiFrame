const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const distDir = __dirname;

function findWorkspaceRoot() {
    let dir = distDir;
    for (let i = 0; i < 12; i++) {
        const sourceI18n = path.join(dir, 'YokiFrameTools', 'TauriEditor', 'dist', 'i18n.js');
        const runtimeI18n = path.join(dir, 'Assets', 'YokiFrame', 'TauriRuntime~', 'dist', 'i18n.js');
        if (fs.existsSync(sourceI18n) && fs.existsSync(runtimeI18n)) return dir;
        const parent = path.dirname(dir);
        if (parent === dir) break;
        dir = parent;
    }
    return path.join(distDir, '..', '..');
}

const workspaceRoot = findWorkspaceRoot();
const sourceDistDir = path.join(workspaceRoot, 'YokiFrameTools', 'TauriEditor', 'dist');

function resolveRuntimeDistDir() {
    return path.join(workspaceRoot, 'Assets', 'YokiFrame', 'TauriRuntime~', 'dist');
}

const runtimeDistDir = resolveRuntimeDistDir();

function readDistFile(fileName) {
    return fs.readFileSync(path.join(distDir, fileName), 'utf8');
}

function readFrontendScripts() {
    return [
        'i18n/zh-cn.js',
        'i18n/en-us.js',
        'i18n.js',
        'core/app.js',
        'core/window-state.js',
        'core/log-panel.js',
        'core/command-bridge.js',
        'core/ai-skill-installer.js',
        'core/brand-assets.js',
        'core/kit-reactive-refresh.js',
        'shared/dom.js',
        'core/router.js',
        'core/i18n-bindings.js',
        'main.js',
    ].map(file => readDistFile(file)).join('\n');
}

function loadI18n(locale = 'zh-CN') {
    const store = new Map([['yokiframe-language', locale]]);
    const document = {
        title: '',
        documentElement: {
            lang: '',
            setAttribute(name, value) {
                this[name] = value;
            },
        },
        querySelectorAll() {
            return [];
        },
    };
    const localStorage = {
        getItem(key) {
            return store.has(key) ? store.get(key) : null;
        },
        setItem(key, value) {
            store.set(key, String(value));
        },
    };
    const window = { document, localStorage };
    const context = { window, document, localStorage };
    vm.createContext(context);
    vm.runInContext(readDistFile('i18n/zh-cn.js'), context, { filename: 'i18n/zh-cn.js' });
    vm.runInContext(readDistFile('i18n/en-us.js'), context, { filename: 'i18n/en-us.js' });
    vm.runInContext(readDistFile('i18n.js'), context, { filename: 'i18n.js' });
    return { i18n: window.YokiI18n, document, store };
}

test('i18n helper attaches to window and formats both supported locales', () => {
    const { i18n, document, store } = loadI18n();

    assert.ok(i18n, 'i18n helper should be available as window.YokiI18n for main.js');
    assert.equal(i18n.t('sidebar.framework'), '框架');
    assert.equal(i18n.t('theme.switch_to', '深色控制台'), '切换到深色控制台');

    i18n.setLocale('en-US');
    assert.equal(i18n.getLocale(), 'en-US');
    assert.equal(document.documentElement.lang, 'en-US');
    assert.equal(store.get('yokiframe-language'), 'en-US');
    assert.equal(i18n.t('sidebar.framework'), 'Framework');
    assert.equal(i18n.t('theme.switch_to', 'Dark Console'), 'Switch to Dark Console');
});

test('static workbench shell declares translation keys applied outside page renders', () => {
    const html = readDistFile('index.html');
    const js = readFrontendScripts();
    const shellJs = readDistFile('shell-i18n.js');

    assert.match(html, /<title data-i18n="app\.title">/);
    assert.match(html, /id="status"[^>]*data-i18n="common\.disconnected"/);
    assert.match(html, /id="language-select"[^>]*data-i18n-aria-label="common\.language"/);
    assert.match(html, /id="window-close"[^>]*data-i18n-aria-label="window\.close"[^>]*data-i18n-title="window\.close"/);
    assert.match(html, /<nav id="sidebar"[^>]*data-i18n-aria-label="sidebar\.main_navigation"/);
    assert.match(html, /class="sidebar-group-header"[^>]*data-i18n="sidebar\.workbench"/);
    assert.match(html, /class="item-label"[^>]*data-i18n="sidebar\.framework"/);
    assert.match(html, /class="item-label"[^>]*data-i18n="sidebar\.docs"/);
    assert.match(html, /<script src="i18n\.js"><\/script>\s*<script src="shell-i18n\.js"><\/script>[\s\S]*<script src="core\/i18n-bindings\.js"><\/script>\s*<script src="main\.js"><\/script>/);
    assert.match(shellJs, /window\.YokiShellI18n/);
    assert.match(shellJs, /function applyStaticTranslations\(/);
    assert.doesNotMatch(js, /function applyTranslatedAttribute\(/);
    assert.match(js, /window\.YokiShellI18n\?\.create/);
    assert.match(js, /function applyLocale\(/);
    assert.match(js, /applyLocale\(locale\)/);
    assert.match(js, /applyStaticTranslations\(\);\s*applyTheme\(activeTheme,\s*\{\s*silent:\s*true\s*\}\);/);
});

test('runtime Tauri dist carries the same i18n bundle and script order', () => {
    const sourceI18nPath = path.join(sourceDistDir, 'i18n.js');
    const sourceZhPath = path.join(sourceDistDir, 'i18n', 'zh-cn.js');
    const sourceEnPath = path.join(sourceDistDir, 'i18n', 'en-us.js');
    const sourceShellPath = path.join(sourceDistDir, 'shell-i18n.js');
    const runtimeI18nPath = path.join(runtimeDistDir, 'i18n.js');
    const runtimeZhPath = path.join(runtimeDistDir, 'i18n', 'zh-cn.js');
    const runtimeEnPath = path.join(runtimeDistDir, 'i18n', 'en-us.js');
    const runtimeShellPath = path.join(runtimeDistDir, 'shell-i18n.js');
    const runtimeHtmlPath = path.join(runtimeDistDir, 'index.html');

    assert.ok(fs.existsSync(runtimeI18nPath), 'Unity runtime dist should include i18n.js');
    assert.ok(fs.existsSync(runtimeZhPath), 'Unity runtime dist should include i18n/zh-cn.js');
    assert.ok(fs.existsSync(runtimeEnPath), 'Unity runtime dist should include i18n/en-us.js');
    assert.ok(fs.existsSync(runtimeShellPath), 'Unity runtime dist should include shell-i18n.js');
    assert.equal(fs.readFileSync(runtimeI18nPath, 'utf8'), fs.readFileSync(sourceI18nPath, 'utf8'));
    assert.equal(fs.readFileSync(runtimeZhPath, 'utf8'), fs.readFileSync(sourceZhPath, 'utf8'));
    assert.equal(fs.readFileSync(runtimeEnPath, 'utf8'), fs.readFileSync(sourceEnPath, 'utf8'));
    assert.equal(fs.readFileSync(runtimeShellPath, 'utf8'), fs.readFileSync(sourceShellPath, 'utf8'));

    const runtimeHtml = fs.readFileSync(runtimeHtmlPath, 'utf8');
    assert.match(runtimeHtml, /<script src="i18n\/zh-cn\.js"><\/script>\s*<script src="i18n\/en-us\.js"><\/script>\s*<script src="i18n\.js"><\/script>\s*<script src="shell-i18n\.js"><\/script>[\s\S]*<script src="core\/i18n-bindings\.js"><\/script>\s*<script src="main\.js"><\/script>/);
});
