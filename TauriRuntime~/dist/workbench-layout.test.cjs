const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');
const vm = require('node:vm');

const distDir = __dirname;

function readDistFile(fileName) {
    const content = readRawDistFile(fileName);
    if (fileName === 'style.css') {
        return expandCssImports(content, fileName);
    }
    return content;
}

function readRawDistFile(fileName) {
    return fs.readFileSync(path.join(distDir, fileName), 'utf8');
}

function expandCssImports(content, fromFileName, seen = new Set()) {
    return content.replace(/@import\s+url\(["']?([^"')]+)["']?\);\s*/g, (statement, importPath) => {
        const normalizedPath = path.normalize(path.join(path.dirname(fromFileName), importPath));
        if (seen.has(normalizedPath)) return '';
        seen.add(normalizedPath);
        const imported = fs.readFileSync(path.join(distDir, normalizedPath), 'utf8');
        return `\n/* ${normalizedPath} */\n${expandCssImports(imported, normalizedPath, seen)}\n`;
    });
}

function findWorkspaceRoot() {
    let dir = distDir;
    for (let i = 0; i < 12; i++) {
        const markers = [
            path.join(dir, 'Assets', 'YokiFrame', 'Core', 'Runtime', 'CommandBridge', 'Handlers', 'FsmKitCommandHandler.cs'),
        ];
        if (markers.some(marker => fs.existsSync(marker))) return dir;
        const parent = path.dirname(dir);
        if (parent === dir) break;
        dir = parent;
    }
    return path.join(distDir, '..', '..');
}

function resolveWorkspaceFile(...segments) {
    const root = findWorkspaceRoot();
    return path.join(root, ...segments);
}

function resolveTauriSourceFile(...segments) {
    return resolveWorkspaceFile('YokiFrameTools', 'TauriEditor', ...segments);
}

function readWorkspaceFile(...segments) {
    return fs.readFileSync(resolveWorkspaceFile(...segments), 'utf8');
}

function readTauriSourceFile(...segments) {
    return fs.readFileSync(resolveTauriSourceFile(...segments), 'utf8');
}

function readFrontendScripts() {
    const files = [
        'i18n/zh-cn-shell.js',
        'i18n/zh-cn-core-kits.js',
        'i18n/zh-cn-tool-kits.js',
        'i18n/zh-cn.js',
        'i18n/en-us-shell.js',
        'i18n/en-us-core-kits.js',
        'i18n/en-us-tool-kits.js',
        'i18n/en-us.js',
        'i18n.js',
        'core/app.js',
        'core/app-shell.js',
        'core/app-state.js',
        'core/app-status.js',
        'core/window-state.js',
        'core/log-panel.js',
        'core/command-bridge.js',
        'core/ai-skill-installer.js',
        'core/brand-assets.js',
        'core/kit-reactive-refresh.js',
        'shared/dom.js',
        'shared/kit-bridge.js',
        'shared/kit-ui.js',
        'pages/system.js',
        'pages/actionkit-data.js',
        'pages/actionkit-render.js',
        'pages/actionkit-interactions.js',
        'pages/actionkit.js',
        'pages/poolkit.js',
        'pages/fsmkit-graph.js',
        'pages/fsmkit-graph-interactions.js',
        'pages/fsmkit-data.js',
        'pages/fsmkit-workbench.js',
        'pages/fsmkit-detail.js',
        'pages/fsmkit-interactions.js',
        'pages/fsmkit.js',
        'pages/eventkit-scan.js',
        'pages/eventkit-data.js',
        'pages/eventkit-monitor.js',
        'pages/eventkit.js',
        'pages/reskit.js',
        'pages/architecture.js',
        'pages/singletonkit.js',
        'pages/logkit-render.js',
        'pages/logkit-viewer.js',
        'pages/logkit.js',
        'pages/audiokit-index.js',
        'pages/audiokit-mixer.js',
        'pages/audiokit.js',
        'pages/savekit.js',
        'pages/localizationkit.js',
        'pages/scenekit.js',
        'pages/spatialkit.js',
        'pages/uikit-editor-tools.js',
        'pages/uikit-render.js',
        'pages/uikit.js',
        'pages/tablekit-preview.js',
        'pages/tablekit-state.js',
        'pages/tablekit-render.js',
        'pages/tablekit-actions.js',
        'pages/tablekit.js',
        'pages/docs.js',
        'core/router.js',
        'core/i18n-bindings.js',
        'main.js',
    ];
    return files.map(file => `\n// ${file}\n${readDistFile(file)}`).join('\n');
}

function loadDistExpression(fileName, expression, context = {}) {
    const source = readDistFile(fileName);
    return vm.runInNewContext(`${source}\n;(${expression});`, context);
}

function countTextLines(content) {
    return content.split(/\r?\n/).length;
}

function getScriptOrder() {
    const html = readDistFile('index.html');
    return [...html.matchAll(/<script src="([^"]+)"><\/script>/g)].map(match => match[1]);
}

function extractCssBlock(css, selector) {
    const start = css.indexOf(selector);
    assert.ok(start >= 0, `${selector} should exist`);
    const open = css.indexOf('{', start);
    assert.ok(open > start, `${selector} should have a block`);
    let depth = 0;
    for (let i = open; i < css.length; i++) {
        const char = css[i];
        if (char === '{') depth++;
        if (char === '}') {
            depth--;
            if (depth === 0) return css.slice(start, i + 1);
        }
    }
    assert.fail(`${selector} should close its block`);
}

function extractKeyframes(css) {
    return [...css.matchAll(/@keyframes\s+[-\w]+\s*\{([\s\S]*?)\n\}/g)]
        .map(match => match[0])
        .join('\n');
}

function workspaceFileExists(...segments) {
    return fs.existsSync(resolveWorkspaceFile(...segments));
}

function extractSidebarGroups(html) {
    const groups = [];
    const sectionPattern = /<section class="sidebar-group">([\s\S]*?)<\/section>/g;
    let sectionMatch;
    while ((sectionMatch = sectionPattern.exec(html))) {
        const section = sectionMatch[1];
        const header = section.match(/<div class="sidebar-group-header"(?:\s+[^>]*)?>([^<]+)<\/div>/)?.[1];
        if (!header) continue;
        const labels = [...section.matchAll(/<span class="item-label"(?:\s+[^>]*)?>([^<]+)<\/span>/g)].map(match => match[1]);
        groups.push({ header, labels });
    }
    return groups;
}

function readPageSource(pageFileName) {
    return readDistFile(path.join('pages', pageFileName));
}

test('frontend script entrypoint is split into core shared and page modules', () => {
    const html = readDistFile('index.html');
    const requiredScripts = [
        'i18n/zh-cn-shell.js',
        'i18n/zh-cn-core-kits.js',
        'i18n/zh-cn-tool-kits.js',
        'i18n/zh-cn.js',
        'i18n/en-us-shell.js',
        'i18n/en-us-core-kits.js',
        'i18n/en-us-tool-kits.js',
        'i18n/en-us.js',
        'i18n.js',
        'core/app.js',
        'core/app-shell.js',
        'core/app-state.js',
        'core/app-status.js',
        'core/window-state.js',
        'core/log-panel.js',
        'core/command-bridge.js',
        'core/ai-skill-installer.js',
        'core/brand-assets.js',
        'core/kit-reactive-refresh.js',
        'shared/dom.js',
        'shared/kit-bridge.js',
        'shared/kit-ui.js',
        'pages/system.js',
        'pages/actionkit-data.js',
        'pages/actionkit-render.js',
        'pages/actionkit-interactions.js',
        'pages/actionkit.js',
        'pages/fsmkit-graph.js',
        'pages/fsmkit.js',
        'pages/eventkit-scan.js',
        'pages/eventkit.js',
        'pages/logkit-render.js',
        'pages/logkit-viewer.js',
        'pages/logkit.js',
        'pages/uikit-editor-tools.js',
        'pages/uikit-render.js',
        'pages/uikit.js',
        'pages/tablekit-preview.js',
        'pages/tablekit.js',
        'core/router.js',
        'core/i18n-bindings.js',
        'main.js',
    ];

    let previousIndex = -1;
    for (const script of requiredScripts) {
        const filePath = path.join(distDir, script);
        assert.ok(fs.existsSync(filePath), `${script} should exist`);
        const scriptTag = `<script src="${script}"></script>`;
        const index = html.indexOf(scriptTag);
        assert.notEqual(index, -1, `${scriptTag} should be loaded by index.html`);
        assert.ok(index > previousIndex, `${script} should load after the previous frontend module`);
        previousIndex = index;
    }

    const main = readDistFile('main.js');
    const mainLineCount = main.split(/\r?\n/).length;
    assert.ok(mainLineCount <= 260, `main.js should stay as a thin startup file, got ${mainLineCount} lines`);
    assert.match(main, /function registerYokiFramePages\(/);
    assert.match(main, /function bootstrapYokiFrameEditor\(/);
    assert.doesNotMatch(main, /function renderFsmKitPage\(/);
    assert.doesNotMatch(main, /function renderEventKitPage\(/);
    assert.doesNotMatch(main, /function renderTableKitPage\(/);
    assert.doesNotMatch(main, /function renderLogKitPage\(/);
});

test('large frontend modules are split by functional responsibility', () => {
    const html = readDistFile('index.html');
    const modules = [
        ['i18n/zh-cn-shell.js', /function zhCNShellTranslations\(/],
        ['i18n/zh-cn-core-kits.js', /function zhCNCoreKitTranslations\(/],
        ['i18n/zh-cn-tool-kits.js', /function zhCNToolKitTranslations\(/],
        ['i18n/zh-cn.js', /function zhCN\(/],
        ['i18n/en-us-shell.js', /function enUSShellTranslations\(/],
        ['i18n/en-us-core-kits.js', /function enUSCoreKitTranslations\(/],
        ['i18n/en-us-tool-kits.js', /function enUSToolKitTranslations\(/],
        ['i18n/en-us.js', /function enUS\(/],
        ['core/app-shell.js', /function panel\(/],
        ['core/app-state.js', /const FRAMEWORK_COMMAND_CATALOG\s*=/],
        ['core/app-status.js', /async function pollStatus\(/],
        ['core/window-state.js', /function restoreWindowState\(/],
        ['core/log-panel.js', /function renderLogPanel\(/],
        ['core/command-bridge.js', /async function sendCommand\(/],
        ['core/ai-skill-installer.js', /function renderAiSkillInstallPanel\(/],
        ['core/brand-assets.js', /function applyYokiFrameBrandAssets\(/],
        ['core/kit-reactive-refresh.js', /function registerKitReactiveRefresh\(/],
        ['shared/kit-bridge.js', /async function sendKitCommandData\(/],
        ['pages/fsmkit-graph.js', /function renderFsmGraphSvg\(/],
        ['pages/fsmkit-graph-interactions.js', /function bindFsmGraphInteractions\(/],
        ['pages/fsmkit-data.js', /async function fetchFsmList\(/],
        ['pages/fsmkit-workbench.js', /function renderFsmWorkbenchShell\(/],
        ['pages/fsmkit-detail.js', /function renderFsmInsightsHtml\(/],
        ['pages/fsmkit-interactions.js', /function selectFsmWorkbench\(/],
        ['pages/eventkit-scan.js', /async function runEventKitCodeScan\(/],
        ['pages/eventkit-data.js', /async function fetchEventKitMonitorSnapshot\(/],
        ['pages/eventkit-monitor.js', /function renderEventKitMonitorHtml\(/],
        ['pages/actionkit-data.js', /function normalizeActionKitStatePayload\(/],
        ['pages/actionkit-render.js', /function renderActionKitWorkbench\(/],
        ['pages/actionkit-interactions.js', /function bindActionKitWorkbenchActions\(/],
        ['pages/logkit-render.js', /function renderLogKitWorkbench\(/],
        ['pages/logkit-viewer.js', /function syncLogKitViewerDom\(/],
        ['pages/uikit-editor-tools.js', /function renderUIKitEditorToolsSection\(/],
        ['pages/uikit-render.js', /function renderUIKitWorkbench\(/],
        ['pages/tablekit-preview.js', /function renderTableKitPreviewPanel\(/],
        ['pages/tablekit-state.js', /function loadTableKitConfig\(/],
        ['pages/tablekit-render.js', /function renderTableKitEnvironmentPanel\(/],
        ['pages/tablekit-actions.js', /async function runTableKitLuban\(/],
    ];

    for (const [modulePath, marker] of modules) {
        const source = readDistFile(modulePath);
        assert.match(html, new RegExp(`<script src="${modulePath.replace('/', '\\/')}"><\\/script>`), `${modulePath} should be loaded by index.html`);
        assert.match(source, marker, `${modulePath} should keep its functional owner marker`);
        assert.ok(source.split(/\r?\n/).length <= 500, `${modulePath} should stay at or below 500 lines`);
    }

    const thinEntrypoints = [
        ['i18n.js', 90],
        ['core/app.js', 160],
        ['pages/actionkit.js', 500],
        ['pages/logkit.js', 500],
        ['pages/uikit.js', 500],
        ['pages/fsmkit.js', 500],
        ['pages/eventkit.js', 500],
        ['pages/tablekit.js', 500],
    ];

    for (const [modulePath, maxLines] of thinEntrypoints) {
        const source = readDistFile(modulePath);
        assert.ok(source.split(/\r?\n/).length <= maxLines, `${modulePath} should stay below ${maxLines} lines after functional split`);
    }

    const i18nEntry = readDistFile('i18n.js');
    assert.doesNotMatch(i18nEntry, /function zhCN\(/);
    assert.doesNotMatch(i18nEntry, /function enUS\(/);
    assert.doesNotMatch(readDistFile('core/app.js'), /async function pollStatus\(/);
    assert.doesNotMatch(readDistFile('core/app.js'), /const FRAMEWORK_COMMAND_CATALOG\s*=/);
    assert.doesNotMatch(readDistFile('pages/fsmkit.js'), /function renderFsmGraphSvg\(/);
    assert.doesNotMatch(readDistFile('pages/eventkit.js'), /async function runEventKitCodeScan\(/);
    assert.doesNotMatch(readDistFile('pages/tablekit.js'), /function renderTableKitPreviewPanel\(/);
});

test('production frontend scripts stay within the 500 line module budget', () => {
    const ignored = new Set(['workbench-layout.test.cjs']);
    const jsFiles = [];
    const walk = dir => {
        for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
            const fullPath = path.join(dir, entry.name);
            const relativePath = path.relative(distDir, fullPath).replace(/\\/g, '/');
            if (entry.isDirectory()) {
                if (relativePath === 'docs') continue;
                walk(fullPath);
                continue;
            }
            if (!entry.isFile() || !entry.name.endsWith('.js') || ignored.has(relativePath)) continue;
            jsFiles.push(relativePath);
        }
    };

    walk(distDir);
    const oversized = jsFiles
        .map(fileName => [fileName, countTextLines(readDistFile(fileName))])
        .filter(([, lines]) => lines > 500);
    assert.deepEqual(oversized, [], 'production JS modules should stay at or below 500 lines');
});

test('shared kit bridge owns transport helpers while kit-ui stays presentation-only', () => {
    const bridge = readDistFile('shared/kit-bridge.js');
    const ui = readDistFile('shared/kit-ui.js');

    for (const marker of [
        /async function sendKitCommandData\(/,
        /function getPreferredEngineId\(/,
        /function canSendRuntimeKitCommand\(/,
        /function engineSupportsCapability\(/,
        /function engineSupportsKitFeature\(/,
        /function parseBridgePayload\(/,
        /async function readKitTelemetryData\(/,
        /async function readKitSnapshotData\(/,
        /async function fetchKitWorkbenchState\(/,
    ]) {
        assert.match(bridge, marker);
        assert.doesNotMatch(ui, marker);
    }

    for (const marker of [
        /function canShowStaticKitWorkbench\(/,
        /function showRuntimeKitUnavailable\(/,
        /function getSelectedEngineForNavigation\(/,
        /function syncSidebarKitAvailability\(/,
        /function syncSidebarGroupVisibility\(/,
        /async function openKitCodeLocation\(/,
        /function renderWorkbenchHtmlStable\(/,
    ]) {
        if (String(marker).includes('getSelectedEngineForNavigation')) {
            assert.match(bridge, marker);
            assert.doesNotMatch(ui, marker);
            continue;
        }
        assert.match(ui, marker);
        assert.doesNotMatch(bridge, marker);
    }
});

test('stylesheet entrypoint is split into focused CSS modules', () => {
    const entryCss = readRawDistFile('style.css');
    const expandedCss = readDistFile('style.css');
    const modules = [
        'styles/tokens.css',
        'styles/motion.css',
        'styles/shell.css',
        'styles/workbench.css',
        'styles/kits.css',
        'styles/system.css',
        'styles/eventkit.css',
        'styles/fsmkit.css',
        'styles/docs.css',
        'styles/fsmkit-graph.css',
        'styles/markdown.css',
        'styles/responsive.css',
    ];

    assert.ok(entryCss.split(/\r?\n/).length <= 24, 'style.css should stay as a thin module entrypoint');
    for (const modulePath of modules) {
        assert.match(entryCss, new RegExp(`@import url\\("${modulePath.replace('/', '\\/')}"\\);`));
        assert.ok(fs.existsSync(path.join(distDir, modulePath)), `${modulePath} should exist`);
    }
    assert.match(expandedCss, /--kit-header-min-height:\s*88px/);
    assert.match(expandedCss, /\.workspace-shell\s*\{/);
    assert.match(expandedCss, /\.hero-intro-card,\s*\n\.kit-toolbar,\s*\n\.audio-master-strip\s*\{/);
    assert.match(expandedCss, /\.eventkit-workbench\s*\{/);
    assert.match(expandedCss, /\.fsm-workbench\s*\{/);
    assert.match(expandedCss, /\.doc-layout\s*\{/);
    assert.match(expandedCss, /\.md-h\s*\{/);
});

test('motion system adds purposeful transitions with reduced-motion safeguards', () => {
    const entryCss = readRawDistFile('style.css');
    const css = readDistFile('style.css');
    const routerSource = readDistFile('core/router.js');
    const motionCss = readDistFile('styles/motion.css');

    assert.match(entryCss, /@import url\("styles\/motion\.css"\);/);
    for (const token of [
        '--motion-instant',
        '--motion-fast',
        '--motion-normal',
        '--motion-slow',
        '--ease-standard',
        '--ease-emphasized',
    ]) {
        assert.match(css, new RegExp(`${token}:\\s*[^;]+;`), `${token} should be defined`);
    }

    assert.match(motionCss, /@keyframes yoki-page-enter/);
    assert.match(motionCss, /@keyframes yoki-surface-enter/);
    assert.match(motionCss, /@keyframes yoki-window-open/);
    assert.match(motionCss, /\.workspace-shell\s*\{[\s\S]*?animation:\s*yoki-window-open\s+80ms\s+var\(--ease-emphasized\)\s+both/);
    assert.match(motionCss, /transform-origin:\s*center center/);
    assert.match(motionCss, /transform:\s*scale\(0\.99\)/);
    assert.match(motionCss, /@keyframes yoki-status-pulse/);
    assert.match(motionCss, /@keyframes yoki-shimmer/);
    assert.match(motionCss, /@media \(prefers-reduced-motion:\s*reduce\)/);
    assert.match(motionCss, /\.content-body\.is-navigating/);
    assert.match(motionCss, /\.content-body\.is-navigation-settling/);
    assert.match(routerSource, /function markNavigationMotion\(/);
    assert.match(routerSource, /\$pageBody\.classList\.add\('is-navigating'\)/);
    assert.match(routerSource, /\$pageBody\.classList\.add\('is-navigation-settling'\)/);

    const motionTargets = [
        '.btn',
        '.titlebar-tool',
        '.sidebar-item',
        '.tab-button',
        '.metric-card',
        '.diagnostic-tile',
        '.kit-panel',
        '.kit-list-row',
        '.data-table tbody tr',
        '.log-entry',
        '.tablekit-fold-card',
        '.eventkit-v1-row',
        '.fsm-list-item',
    ];
    for (const selector of motionTargets) {
        const block = extractCssBlock(motionCss, selector);
        assert.match(block, /transition:/, `${selector} should declare a transition`);
    }

    const keyframes = extractKeyframes(motionCss);
    assert.doesNotMatch(keyframes, /\b(?:width|height|top|left|right|bottom|margin|padding)\s*:/, 'motion keyframes should avoid layout-driving properties');
    assert.match(css, /animation-duration:\s*0\.001ms\s*!important/);
    assert.match(css, /transition-duration:\s*0\.001ms\s*!important/);
    assert.match(motionCss, /\.workspace-shell\s*\{[\s\S]*?animation:\s*none\s*!important/);
});

test('YokiFrame brand chrome uses package metadata and package-relative artwork', () => {
    const html = readDistFile('index.html');
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const packageJson = JSON.parse(readWorkspaceFile('Assets', 'YokiFrame', 'package.json'));

    assert.match(packageJson.version, /^2\.0\.0-/);
    assert.match(js, new RegExp(`version:\\s*'${packageJson.version.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}'`));
    assert.match(packageJson.repository.url, /^https:\/\/github\.com\/HinataYoki\/YokiFrame(?:\.git)?$/);
    assert.match(js, /function normalizeRepositoryUrl\(/);
    assert.match(js, /\.replace\(\/\\\.git\$\/,\s*''\)/);
    assert.match(html, /class="titlebar-brand"/);
    assert.match(html, /id="titlebar-brand-icon"/);
    assert.match(html, /src="assets\/yoki\.png"/);
    assert.match(html, /data-runtime-src="\.\.\/\.\.\/\.\.\/\.\.\/\.\.\/\.\.\/Core\/Editor\/Resources\/yoki\.png"/);
    assert.ok(fs.existsSync(path.join(distDir, 'assets', 'yoki.png')), 'runtime dist should carry the brand icon next to index.html');
    assert.match(html, /YokiFrame/);
    assert.doesNotMatch(html, /v2\.0 Preview/);
    assert.match(html, /sidebar-package-card/);
    assert.match(html, /data-package-version/);
    assert.match(html, /data-package-link/);
    assert.doesNotMatch(html, /<strong>YokiFrame<\/strong>/);
    assert.doesNotMatch(html, /class="sidebar-package-row"/);
    assert.match(js, /\.\.\/\.\.\/Assets\/YokiFrame\/Core\/Editor\/Resources\/yoki\.png/);
    assert.match(js, /assets\/yoki\.png/);
    assert.match(js, /Core\/Editor\/Resources\/yoki\.png/);
    assert.match(js, /icon\.onerror\s*=\s*\(\)\s*=>/);
    assert.match(js, /\.\.\/\.\.\/Assets\/YokiFrame\/package\.json/);
    assert.match(js, /async function loadYokiFramePackageInfo\(/);
    assert.match(js, /function applyYokiFrameBrandAssets\(/);
    assert.match(js, /function bindPackageExternalLinks\(/);
    assert.match(js, /querySelectorAll\('\[data-package-link\]'\)/);
    assert.match(js, /addEventListener\('click'/);
    assert.match(js, /invoke\('open_external_url',\s*\{\s*url\s*\}\)/);
    assert.match(js, /event\.preventDefault\(\)/);
    assert.match(js, /if\s*\(!invoke\)\s*return/);
    assert.match(css, /\.titlebar-brand/);
    assert.match(css, /\.sidebar-package-card/);
    assert.doesNotMatch(html + js + css, /[A-Za-z]:[\\/](?!\/)/);
});

test('sidebar navigation groups live in a rounded scroll card outside the version footer', () => {
    const html = readDistFile('index.html');
    const css = readDistFile('style.css');

    const navCardStart = html.indexOf('<div class="sidebar-scroll-card"');
    const footerStart = html.indexOf('<section class="sidebar-footer"');
    assert.ok(navCardStart >= 0, 'sidebar scroll card should exist');
    assert.ok(footerStart > navCardStart, 'version footer should sit after the scroll card content');
    assert.match(html.slice(navCardStart, footerStart), /<\/div>\s*$/, 'sidebar scroll card should close before the footer');

    const navCardHtml = html.slice(navCardStart, footerStart);
    for (const header of ['工作台', 'Architecture', 'Core', 'Tool']) {
        assert.match(navCardHtml, new RegExp(`<div class="sidebar-group-header"(?:\\s+[^>]*)?>${header}</div>`));
    }
    assert.match(navCardHtml, /sidebar-active-indicator/);
    assert.doesNotMatch(navCardHtml, /sidebar-footer/);
    assert.doesNotMatch(navCardHtml, /data-package-version/);

    const sidebarCss = css.slice(css.indexOf('.workspace-nav'), css.indexOf('.sidebar-active-indicator'));
    const navCardCss = css.slice(css.indexOf('.sidebar-scroll-card'), css.indexOf('.sidebar-scroll-card::-webkit-scrollbar'));
    assert.match(sidebarCss, /overflow:\s*hidden/);
    assert.match(sidebarCss, /gap:\s*var\(--sp-md\)/);
    assert.match(navCardCss, /border-radius:\s*var\(--r-lg\)/);
    assert.match(navCardCss, /overflow-y:\s*auto/);
    assert.match(navCardCss, /flex:\s*1\s+1\s+auto/);
    assert.match(navCardCss, /min-height:\s*0/);
});

test('workspace separates the sidebar as a rounded floating rail instead of a hard divider', () => {
    const css = readDistFile('style.css');
    const bodyCss = css.slice(css.indexOf('.workspace-body'), css.indexOf('.workspace-nav'));
    const sidebarCss = css.slice(css.indexOf('.workspace-nav'), css.indexOf('.sidebar-scroll-card'));
    const contentCss = css.slice(css.indexOf('.workspace-content'), css.indexOf('.metric-strip'));

    assert.match(bodyCss, /grid-template-columns:\s*var\(--nav-width\)\s+minmax\(0,\s*1fr\)/);
    assert.match(bodyCss, /gap:\s*var\(--sp-md\)/);
    assert.match(bodyCss, /padding:\s*var\(--sp-md\)/);
    assert.match(sidebarCss, /border:\s*1px solid var\(--hairline\)/);
    assert.match(sidebarCss, /border-radius:\s*var\(--r-lg\)/);
    assert.match(sidebarCss, /background:\s*var\(--nav-overlay\)/);
    assert.match(sidebarCss, /box-shadow:\s*var\(--shadow-panel\),\s*10px 0 24px rgba\(0,\s*0,\s*0,\s*0\.10\)/);
    assert.doesNotMatch(sidebarCss, /border-right:\s*1px solid var\(--hairline\)/);
    assert.doesNotMatch(sidebarCss, /inset -1px 0 0/);
    assert.match(contentCss, /border-radius:\s*var\(--r-lg\)/);
    assert.match(contentCss, /background:\s*var\(--surface-overlay\)/);
    assert.match(contentCss, /border:\s*1px solid var\(--hairline\)/);
    assert.match(contentCss, /box-shadow:\s*var\(--shadow-panel\)/);
});

test('sidebar footer stays anchored at the bottom and only shows version plus GitHub', () => {
    const html = readDistFile('index.html');
    const css = readDistFile('style.css');

    const footerStart = html.indexOf('<section class="sidebar-footer"');
    const footerEnd = html.indexOf('</section>', footerStart);
    assert.ok(footerStart >= 0 && footerEnd > footerStart, 'sidebar footer should exist');
    const footerHtml = html.slice(footerStart, footerEnd);
    const footerCss = css.slice(css.indexOf('.sidebar-footer'), css.indexOf('.sidebar-package-card'));

    assert.match(footerHtml, /data-package-version/);
    assert.match(footerHtml, /data-package-link/);
    assert.match(footerHtml, />GitHub</);
    assert.doesNotMatch(footerHtml, />YokiFrame</);
    assert.doesNotMatch(footerHtml, /sidebar-package-row/);
    assert.doesNotMatch(footerCss, /position:\s*sticky/);
    assert.doesNotMatch(footerCss, /overflow-y:\s*auto/);
});

test('page header omits tool icon and dependency group eyebrow', () => {
    const html = readDistFile('index.html');
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.doesNotMatch(html, /id="hero-icon"/);
    assert.doesNotMatch(html, /id="hero-eyebrow"/);
    assert.doesNotMatch(css, /\.kit-header-icon/);
    assert.doesNotMatch(css, /\.kit-header-eyebrow/);
    assert.doesNotMatch(js, /\$heroEyebrow/);
    assert.doesNotMatch(js, /\$heroIcon/);
});

test('workspace removes the redundant global Kit header chrome', () => {
    const html = readDistFile('index.html');
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.doesNotMatch(html, /id="kit-header"/);
    assert.doesNotMatch(html, /id="hero-title"|id="hero-summary"|id="hero-actions"/);
    assert.doesNotMatch(css, /\.kit-header(?:\s|,|\{|[.#:-])/);
    assert.doesNotMatch(js, /\$heroTitle|\$heroSummary|\$heroActions/);
    assert.match(js, /let currentHeroActionsHtml\s*=\s*''/);
    assert.match(js, /function promoteHeroActionsToFirstCard\(/);
    assert.match(js, /data-promoted-hero-actions="1"/);
    assert.match(css, /\.content-body\s*\{[\s\S]*?padding:\s*var\(--sp-md\)\s+var\(--sp-xl\)/);
    assert.match(css, /\.content-body--fsmkit\s*\{[\s\S]*?padding:\s*var\(--sp-md\)/);
    assert.match(css, /\.hero-intro-card,\s*\n\.kit-toolbar,\s*\n\.audio-master-strip\s*\{[\s\S]*?-webkit-app-region:\s*no-drag/);
    assert.match(css, /\.promoted-hero-actions/);
});

test('workspace preserves a rounded in-page intro card when no Kit card can receive the header', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /let currentHeroMeta\s*=\s*null/);
    assert.match(js, /function ensureHeroIntroCard\(/);
    assert.match(js, /function renderHeroIntroCard\(/);
    assert.match(js, /data-hero-intro-card="1"/);
    assert.match(js, /const HERO_CARD_TARGET_SELECTOR\s*=/);
    assert.doesNotMatch(js.match(/const HERO_CARD_TARGET_SELECTOR\s*=\s*[\s\S]*?;/)?.[0] || '', /\.tool-pane/);
    assert.match(js, /promoteHeroActionsToFirstCard\(\)[\s\S]*?ensureHeroIntroCard\(\)/);
    assert.match(css, /\.hero-intro-card\s*\{[\s\S]*?border-radius:\s*var\(--r-md\)/);
    assert.match(css, /\.hero-intro-card__actions/);
});

test('non-doc workbench top islands share one compact header contract without eyebrow labels', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    const heroRenderer = js.match(/function renderHeroIntroCard\(meta,\s*actionsHtml\s*=\s*''\)\s*\{[\s\S]*?\n\}/)?.[0] || '';
    const sharedHeaderCss = css.match(/\.hero-intro-card,\s*\n\.kit-toolbar,\s*\n\.audio-master-strip\s*\{[\s\S]*?\}/)?.[0] || '';
    const heroCss = css.match(/\.hero-intro-card\s*\{[\s\S]*?\}/)?.[0] || '';
    const toolbarCss = css.match(/\.kit-toolbar\s*\{[\s\S]*?\}/)?.[0] || '';
    const audioCss = css.match(/\.audio-master-strip\s*\{\s*display:\s*grid;[\s\S]*?\}/)?.[0] || '';
    const tableCss = css.match(/\.tablekit-command-center\s*\{[\s\S]*?\}/)?.[0] || '';
    const narrowSharedCss = css.match(/@media \(max-width:\s*760px\)\s*\{[\s\S]*?\.hero-intro-card,\s*\n\s*\.kit-toolbar,\s*\n\s*\.audio-master-strip\s*\{[\s\S]*?\n\s*\}/)?.[0] || '';
    const narrowActionsCss = css.match(/@media \(max-width:\s*760px\)\s*\{[\s\S]*?\.hero-intro-card__actions,\s*\n\s*\.kit-toolbar__actions,\s*\n\s*\.audio-master-strip\s+\.kit-toolbar__actions\s*\{[\s\S]*?\n\s*\}/)?.[0] || '';

    assert.ok(heroRenderer, 'intro card renderer should exist');
    assert.doesNotMatch(heroRenderer, /hero-intro-card__eyebrow/);
    assert.doesNotMatch(heroRenderer, /meta\?\.eyebrow/);
    assert.match(css, /--kit-header-min-height:\s*88px/);
    assert.match(sharedHeaderCss, /min-height:\s*var\(--kit-header-min-height\)/);
    assert.match(sharedHeaderCss, /padding:\s*var\(--kit-header-pad-y\)\s+var\(--kit-header-pad-x\)/);
    assert.match(sharedHeaderCss, /border-radius:\s*var\(--r-md\)/);
    assert.match(sharedHeaderCss, /-webkit-app-region:\s*no-drag/);
    assert.match(heroCss, /align-items:\s*center/);
    assert.match(toolbarCss, /align-items:\s*center/);
    assert.match(audioCss, /grid-template-columns:\s*minmax\(220px,\s*1fr\)\s+minmax\(220px,\s*360px\)\s+auto/);
    assert.match(tableCss, /min-height:\s*var\(--kit-header-min-height\)/);
    assert.match(narrowSharedCss, /align-items:\s*stretch/);
    assert.match(narrowActionsCss, /justify-content:\s*flex-start/);
});

test('runtime Kit workbenches keep the rounded intro card after async refresh', () => {
    const js = readFrontendScripts();

    const pageSet = js.match(/const HERO_INTRO_CARD_PAGE_IDS\s*=\s*new Set\(\[([\s\S]*?)\]\);/);
    assert.ok(pageSet, 'intro-card forced page set should exist');
    for (const pageId of ['fsmkit', 'logkit', 'poolkit', 'reskit', 'singletonkit']) {
        assert.match(pageSet[1], new RegExp(`'${pageId}'`));
    }

    assert.match(js, /function shouldKeepHeroIntroCard\(\)/);
    assert.match(js, /HERO_INTRO_CARD_PAGE_IDS\.has\(activePage\)/);
    assert.match(js, /function promoteHeroActionsToFirstCard\(\)\s*\{[\s\S]*?if\s*\(shouldKeepHeroIntroCard\(\)\)\s*return;/);
    assert.match(js, /function ensureHeroIntroCard\(\)\s*\{[\s\S]*?const target = \$pageBody\.querySelector\(HERO_CARD_TARGET_SELECTOR\);[\s\S]*?if\s*\(target && !shouldKeepHeroIntroCard\(\)\)\s*return;/);
    assert.match(js, /function renderFsmWorkbenchShell\([\s\S]*?\$pageBody\.innerHTML = `[\s\S]*?`;\s*scheduleHeroActionPromotion\(\);/);
});

test('tool navigation no longer writes a transient loading placeholder', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const navigateToBody = js.slice(js.indexOf('function navigateTo(pageId)'), js.indexOf('// 侧边栏点击处理'));

    assert.match(js, /function renderPageLoadingState\(\)/);
    assert.match(js, /data-page-loading-state="1"/);
    assert.match(js, /t\('common\.loading'\)/);
    assert.match(js, /function isPageLoadingStateVisible\(\)/);
    assert.match(js, /function scheduleHeroActionPromotion\(\)\s*\{[\s\S]*?if\s*\(isPageLoadingStateVisible\(\)\)\s*\{[\s\S]*?setTimeout\(scheduleHeroActionPromotion,\s*48\);[\s\S]*?return;[\s\S]*?\}/);
    assert.match(navigateToBody, /scheduleNavigationRender\(pageId,\s*requestSeq\);/);
    assert.doesNotMatch(navigateToBody, /\$pageBody\.innerHTML\s*=\s*renderPageLoadingState\(\);/);
    assert.match(css, /\.page-loading-state\s*\{[\s\S]*?display:\s*grid[\s\S]*?place-items:\s*center/);
    assert.match(css, /\.page-loading-state__text\s*\{[\s\S]*?color:\s*var\(--ink-subtle\)/);
});

test('UIKit places the runtime information card before editor tools', () => {
    const js = readFrontendScripts();
    const start = js.indexOf('function renderUIKitWorkbench(');
    const end = js.indexOf('function filterUIKitPanels(', start);
    const source = js.slice(start, end);

    const infoCardIndex = source.indexOf('<section class="kit-toolbar">');
    const editorToolsIndex = source.indexOf('${editorTools}');
    assert.ok(infoCardIndex >= 0, 'UIKit should render the runtime information toolbar');
    assert.ok(editorToolsIndex >= 0, 'UIKit should still render editor tools when available');
    assert.ok(infoCardIndex < editorToolsIndex, 'runtime information card should be the first UIKit card');
});

test('TableKit top rounded card uses the shared Kit toolbar style', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const start = js.indexOf('function renderTableKitGeneratorStatus(');
    const end = js.indexOf('function renderTableKitEnvironmentPanel(', start);
    const source = js.slice(start, end);
    const tableKitCardCss = css.match(/\.tablekit-command-center\s*\{[\s\S]*?\}/)?.[0] || '';

    assert.match(source, /<section class="kit-toolbar tablekit-command-center">/);
    assert.doesNotMatch(source, /<section class="tablekit-command-center">/);
    assert.doesNotMatch(tableKitCardCss, /background:\s*linear-gradient/);
    assert.doesNotMatch(tableKitCardCss, /box-shadow:/);
    assert.doesNotMatch(tableKitCardCss, /border-radius:\s*var\(--r-md\)/);
});

test('TableKit target and code selectors live in environment configuration instead of the top island', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const generatorStart = js.indexOf('function renderTableKitGeneratorStatus(');
    const environmentStart = js.indexOf('function renderTableKitEnvironmentPanel(', generatorStart);
    const consoleStart = js.indexOf('function renderTableKitConsole(', environmentStart);
    const targetGridCss = css.match(/\.tablekit-form-grid--targets\s*\{[\s\S]*?\}/)?.[0] || '';
    const targetFieldCss = css.match(/\.tablekit-form-grid--targets\s+\.tablekit-field\s*\{[\s\S]*?\}/)?.[0] || '';

    assert.ok(generatorStart >= 0, 'TableKit generator renderer should exist');
    assert.ok(environmentStart > generatorStart, 'TableKit environment panel should follow the generator renderer');
    assert.ok(consoleStart > environmentStart, 'TableKit console renderer should follow the environment panel');

    const generatorSource = js.slice(generatorStart, environmentStart);
    const environmentSource = js.slice(environmentStart, consoleStart);

    assert.doesNotMatch(generatorSource, /tablekit-command-center__controls/);
    assert.doesNotMatch(generatorSource, /renderTableKitInlineSelectField\('target'/);
    assert.doesNotMatch(generatorSource, /renderTableKitInlineSelectField\('codeTarget'/);
    assert.match(environmentSource, /renderTableKitSelectField\('target',\s*'Target'/);
    assert.match(environmentSource, /renderTableKitSelectField\('codeTarget',\s*'Code'/);
    assert.match(targetGridCss, /repeat\(auto-fit,\s*minmax\(150px,\s*1fr\)\)/);
    assert.match(targetFieldCss, /grid-template-columns:\s*minmax\(0,\s*1fr\)/);
    assert.match(targetFieldCss, /border-bottom:\s*0/);
});

test('frontend disables the native context menu', () => {
    const js = readFrontendScripts();

    assert.match(js, /function disableNativeContextMenu\(event\)\s*\{[\s\S]*?event\.preventDefault\(\);[\s\S]*?\}/);
    assert.match(js, /document\.addEventListener\('contextmenu',\s*disableNativeContextMenu\)/);
});

test('workbench shell defaults to Chinese and exposes framework, docs, Core/Tool, and language controls', () => {
    const html = readDistFile('index.html');

    assert.match(html, /<html\s+lang="zh-CN"/);
    assert.match(html, /data-theme="light"/);
    assert.match(html, /id="language-select"/);
    assert.match(html, /id="theme-toggle"/);
    assert.match(html, /id="window-close"/);
    assert.match(html, /titlebar-drag-region/);
    assert.match(html, /data-page="system"/);
    assert.match(html, />框架</);
    assert.match(html, /data-page="docs"/);
    assert.match(html, />文档</);
    assert.match(html, /data-page="architecture"/);
    assert.match(html, />Architecture</);
    assert.match(html, /data-page="fsmkit"/);
    assert.match(html, /data-page="logkit"/);
    assert.match(html, />LogKit</);
    assert.match(html, /data-page="audiokit"/);
    assert.match(html, />AudioKit</);
    assert.match(html, /data-page="uikit"/);
    assert.match(html, />UIKit</);
    assert.doesNotMatch(html, /data-page="buffkit"/);
    assert.doesNotMatch(html, />BuffKit</);
    assert.match(html, /data-page="savekit"/);
    assert.match(html, />SaveKit</);
    assert.match(html, /data-page="localizationkit"/);
    assert.match(html, />LocalizationKit</);
    assert.match(html, /data-page="scenekit"/);
    assert.match(html, />SceneKit</);
    assert.match(html, /data-page="spatialkit"/);
    assert.match(html, />SpatialKit</);
    assert.match(html, />Core</);
    assert.match(html, />Tool</);
    assert.doesNotMatch(html, /id="cmd-bar"/);
    assert.doesNotMatch(html, /workspace-search/);
    assert.doesNotMatch(html, /titlebar-title/);
    assert.doesNotMatch(html, /titlebar-subtitle/);
    assert.doesNotMatch(html, /data-page="bridge"/);
    assert.doesNotMatch(html, /FileBridge v2/);
    assert.doesNotMatch(html, /默认语言：中文/);
    assert.doesNotMatch(html, /fonts\.googleapis\.com/);
    assert.match(html, /<svg[\s\S]*?<\/svg>/);
    assert.doesNotMatch(html, />□</);
    assert.doesNotMatch(html, />≡</);
    assert.doesNotMatch(html, />↗</);
});

test('main sidebar groups Kits by dependency layer and sorts each layer alphabetically', () => {
    const html = readDistFile('index.html');
    const groups = extractSidebarGroups(html);

    assert.deepEqual(groups.map(group => group.header), ['工作台', 'Architecture', 'Core', 'Tool']);
    assert.deepEqual(groups[0].labels, ['框架', '文档']);
    assert.deepEqual(groups[1].labels, ['Architecture']);
    assert.deepEqual(groups[2].labels, ['EventKit', 'FsmKit', 'LogKit', 'PoolKit', 'ResKit', 'SingletonKit']);
    assert.deepEqual(groups[3].labels, ['ActionKit', 'AudioKit', 'GraphKit', 'LocalizationKit', 'SaveKit', 'SceneKit', 'SpatialKit', 'TableKit', 'UIKit']);
    assert.doesNotMatch(html, /<div class="sidebar-group-header">工具<\/div>/);
});

test('sidebar and docs hide Kit entries that the selected engine does not implement', () => {
    const html = readDistFile('index.html');
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const unityHost = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Editor', 'CommandBridge', 'UnityCommandBridgeHost.Heartbeat.cs');
    const godotEditorPlugin = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Godot', 'Editor', 'addons', 'yokiframe', 'plugin.gd');
    const godotRuntimeHost = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Godot', 'Runtime', 'Core', 'CommandBridge', 'Runtime', 'GodotCommandBridgeHost.cs');
    const navigateToBody = js.slice(js.indexOf('function navigateTo(pageId)'), js.indexOf('// 侧边栏点击处理'));
    const syncSidebarBody = js.slice(js.indexOf('function syncSidebarKitAvailability()'), js.indexOf('async function openKitCodeLocation'));
    const docsPageBody = js.slice(js.indexOf('async function renderDocsPage()'), js.indexOf('function getDocNavTitle'));
    const godotImplementedKits = '["System","Architecture","EventKit","FsmKit","LogKit","PoolKit","ResKit","SingletonKit","ActionKit","LocalizationKit","SaveKit","SceneKit","SpatialKit","TableKit"]';
    const normalizeRegistrySource = source => source.replace(/\\/g, '');

    assert.match(html, /data-page="uikit"[\s\S]*?data-kit="UIKit"/);
    assert.match(html, /data-page="eventkit"[\s\S]*?data-kit="EventKit"/);
    assert.match(js, /const KIT_PAGE_ID_TO_KIT/);
    assert.match(js, /function getWorkbenchStatus\(/);
    assert.match(js, /function getWorkbenchEngines\(/);
    assert.match(js, /function getSelectedEngineForNavigation\(/);
    assert.match(js, /function engineSupportsKit\(engine,\s*kit\)/);
    assert.match(js, /function engineSupportsKitFeature\(engine,\s*kit,\s*feature\)/);
    assert.match(js, /function syncSidebarKitAvailability\(/);
    assert.match(syncSidebarBody, /item\.hidden\s*=\s*!available/);
    assert.match(syncSidebarBody, /syncSidebarGroupVisibility\(\)/);
    assert.match(syncSidebarBody, /fallbackPage\s*=\s*'system'/);
    assert.match(syncSidebarBody, /navigateTo\(fallbackPage\)/);
    assert.match(navigateToBody, /engineSupportsKit\(getSelectedEngineForNavigation\(\),\s*targetKit\)/);
    assert.match(js, /function syncSidebarGroupVisibility\(/);
    assert.match(js, /function getVisibleDocsForCurrentEngine\(/);
    assert.match(js, /function getDocKit\(doc\)/);
    assert.match(docsPageBody, /getVisibleDocsForCurrentEngine\(\)/);
    assert.match(css, /\.sidebar-item\[hidden\][\s\S]*?\.sidebar-group\[hidden\][\s\S]*?display:\s*none\s*!important/);
    assert.match(js, /function canSendRuntimeKitCommand\(kit\)/);
    assert.match(js, /function showRuntimeKitUnavailable\(kit,\s*label\s*=\s*kit\)/);
    assert.match(js, /engineSupportsKitFeature\(targetEngine,\s*'UIKit',\s*'ui_editor_tools'\)/);
    assert.match(unityHost, /\\"implementedKits\\":\[/);
    assert.match(unityHost, /\\"kitFeatures\\":\{/);
    assert.match(unityHost, /\\"UIKit\\":\[\\"runtime\\",\\"snapshots\\",\\"telemetry\\",\\"ui_editor_tools\\"\]/);
    assert.ok(normalizeRegistrySource(godotEditorPlugin).includes(godotImplementedKits), 'Godot editor registry should expose Godot-supported Kits.');
    assert.match(normalizeRegistrySource(godotEditorPlugin), /"implementedKits":/);
    assert.match(normalizeRegistrySource(godotEditorPlugin), /"TableKit":\["tauri_config","registry_optional_dependencies"\]/);
    assert.doesNotMatch(godotEditorPlugin, /"implementedKits":\[[^\]]*"AudioKit"/);
    assert.doesNotMatch(normalizeRegistrySource(godotEditorPlugin), /GODOT_IMPLEMENTED_KITS_JSON[^\n]*"UIKit"/);
    assert.ok(normalizeRegistrySource(godotRuntimeHost).includes(godotImplementedKits), 'Godot runtime registry should expose Godot-supported Kits.');
    assert.match(godotRuntimeHost, /\\"TableKit\\":\[\\"tauri_config\\",\\"registry_optional_dependencies\\"\]/);
    assert.doesNotMatch(godotRuntimeHost, /"implementedKits":\[[^\]]*"AudioKit"/);
    assert.doesNotMatch(normalizeRegistrySource(godotRuntimeHost), /IMPLEMENTED_KITS_JSON[^\n]*"UIKit"/);
});

test('Tauri workbench keeps colorful sidebar Kit icons without deleted BuffKit entries', () => {
    const html = readDistFile('index.html');
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    for (const token of [
        'framework',
        'docs',
        'architecture',
        'audiokit',
        'eventkit',
        'fsmkit',
        'logkit',
        'poolkit',
        'reskit',
        'singletonkit',
        'actionkit',
        'localizationkit',
        'savekit',
        'scenekit',
        'spatialkit',
        'tablekit',
        'uikit',
    ]) {
        assert.match(css, new RegExp(`--kit-icon-${token}:`), `missing color token for ${token}`);
    }

    assert.match(css, /\.sidebar-item\[data-page="eventkit"\]/);
    assert.match(css, /\.sidebar-item\[data-page="localizationkit"\]/);
    assert.match(css, /\.sidebar-item\s+\.item-icon\s*\{[\s\S]*color:\s*var\(--kit-icon-color/);
    assert.doesNotMatch(css, /\.kit-header-icon/);
    assert.doesNotMatch(js, /const ICON_TONES\s*=\s*\{/);
    assert.doesNotMatch(html + js + css, /data-page="buffkit"|>BuffKit<|buffkit|buff:/i);
});

test('Godot editor plugin exposes a lightweight command bridge for System ping', () => {
    const plugin = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Godot', 'Editor', 'addons', 'yokiframe', 'plugin.gd');

    assert.match(plugin, /COMMAND_POLL_INTERVAL_SECONDS/);
    assert.match(plugin, /_start_command_poll_timer/);
    assert.match(plugin, /_poll_editor_commands/);
    assert.match(plugin, /_handle_editor_command/);
    assert.match(plugin, /\\"capabilities\\":\[\\"commands\\",\\"heartbeat\\",\\"bridge_status\\",\\"static_scan\\"\]/);
    assert.match(plugin, /\\"message\\":\\"pong\\"/);
    assert.match(plugin, /\\"kit\\":\\"/);
    assert.match(plugin, /"ping_response"/);
});

test('Godot editor plugin resolves Tauri panel binary per desktop platform', () => {
    const plugin = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Godot', 'Editor', 'addons', 'yokiframe', 'plugin.gd');

    assert.match(plugin, /func _resolve_tauri_binary_path\(runtime_dir: String\) -> String:/);
    assert.match(plugin, /OS\.get_name\(\)/);
    assert.match(plugin, /os_name\s*==\s*"Windows"/);
    assert.match(plugin, /os_name\s*==\s*"macOS"/);
    assert.match(plugin, /os_name\s*==\s*"Linux"/);
    assert.match(plugin, /runtime_dir\.path_join\("yokiframe-tauri-editor\.exe"\)/);
    assert.match(plugin, /runtime_dir\.path_join\("yokiframe-tauri-editor\.app"\)/);
    assert.match(plugin, /Contents\/MacOS\/yokiframe-tauri-editor/);
    assert.match(plugin, /runtime_dir\.path_join\("yokiframe-tauri-editor"\)/);
    const openPanelBody = plugin.slice(plugin.indexOf('func _open_panel()'), plugin.indexOf('func _resolve_package_root()'));
    assert.doesNotMatch(openPanelBody, /OS\.get_name\(\) != "Windows"/);
}
);

test('Godot editor plugin passes the Godot editor HWND to Tauri on Windows', () => {
    const plugin = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Godot', 'Editor', 'addons', 'yokiframe', 'plugin.gd');
    const openPanelBody = plugin.slice(plugin.indexOf('func _open_panel()'), plugin.indexOf('func _resolve_tauri_binary_path('));

    assert.match(plugin, /const YOKI_OWNER_HWND = "YOKI_OWNER_HWND"/);
    assert.match(openPanelBody, /_resolve_owner_hwnd\(\)/);
    assert.match(openPanelBody, /OS\.set_environment\(YOKI_OWNER_HWND,\s*str\(owner_hwnd\)\)/);
    assert.match(openPanelBody, /OS\.unset_environment\(YOKI_OWNER_HWND\)/);
    assert.match(plugin, /func _resolve_owner_hwnd\(\) -> int:/);
    assert.match(plugin, /OS\.get_name\(\)\s*!=\s*"Windows"/);
    assert.match(plugin, /DisplayServer\.window_get_native_handle\(DisplayServer\.WINDOW_HANDLE,\s*DisplayServer\.MAIN_WINDOW_ID\)/);
});

test('stylesheet defines the reference blue gradient light theme for the workbench shell', () => {
    const css = readDistFile('style.css');

    assert.match(css, /--canvas:\s*linear-gradient\(160deg,\s*#eefcf4\s+0%,\s*#d8eff8\s+45%,\s*#ffffeb\s+100%\)/i);
    assert.match(css, /--primary:\s*#00a5d8/i);
    assert.match(css, /--accent-soft:\s*rgba\(115,\s*255,\s*238,\s*0\.20\)/i);
    assert.match(css, /--focus-ring:\s*0\s+0\s+0\s+3px\s+rgba\(0,\s*165,\s*216,\s*0\.24\)/i);
    assert.match(css, /--chrome-material:/);
    assert.match(css, /--control-material:/);
    assert.match(css, /--shadow-window:/);
    assert.match(css, /html\[data-theme="dark"\]/);
    assert.match(css, /--canvas:\s*#1d1d1f/i);
    const lightThemeCss = css.slice(css.indexOf(':root'), css.indexOf('html[data-theme="dark"]'));
    assert.doesNotMatch(lightThemeCss, /#007aff/i);
    assert.doesNotMatch(lightThemeCss, /rgba\(0,\s*122,\s*255/i);
    assert.match(css, /\.workspace-shell/);
    assert.match(css, /\.workspace-nav/);
    assert.match(css, /\.workspace-content/);
    assert.match(css, /\.developer-context-strip/);
    assert.doesNotMatch(css, /--canvas:\s*#010102/i);
    assert.doesNotMatch(css, /#f4f7f2/i);
    assert.doesNotMatch(css, /#50a87b/i);
    assert.doesNotMatch(css, /linear-gradient\(145deg,\s*var\(--primary\),\s*var\(--accent\)\)/);
});

test('system workbench applies unified compact visual rhythm for cards and diagnostics', () => {
    const css = readDistFile('style.css');
    const systemPage = readDistFile('pages/system.js');
    const routerSource = readDistFile('core/router.js');

    assert.match(css, /--panel-pad-x:\s*16px/);
    assert.match(css, /--panel-pad-y:\s*16px/);
    assert.match(css, /--panel-header-height:\s*40px/);
    assert.match(css, /--tile-min-height:\s*92px/);
    assert.match(css, /\.panel\s*\{[\s\S]*?margin-bottom:\s*0/);
    assert.match(css, /\.panel-body\s*\{[\s\S]*?padding:\s*var\(--panel-pad-y\)\s+var\(--panel-pad-x\)/);
    assert.match(css, /\.framework-dashboard\s*\{[\s\S]*?grid-template-columns:\s*minmax\(680px,\s*1fr\)\s+minmax\(360px,\s*0\.58fr\)/);
    assert.match(css, /\.framework-engine-panel__body\s*\{[\s\S]*?display:\s*grid/);
    assert.match(css, /\.framework-engine-panel__controls\s*\{[\s\S]*?grid-template-columns:\s*minmax\(0,\s*1fr\)\s+minmax\(0,\s*0\.82fr\)/);
    assert.match(css, /\.framework-grid--status\s*\{[\s\S]*?grid-template-columns:\s*repeat\(5,\s*minmax\(0,\s*1fr\)\)/);
    assert.doesNotMatch(css, /\.framework-grid--status\s+\.diagnostic-tile:first-child\s*\{[\s\S]*?grid-row:\s*span 2/);
    assert.match(css, /\.diagnostic-tile\s*\{[\s\S]*?min-height:\s*var\(--tile-min-height\)/);
    assert.match(css, /\.framework-engine-panel\s+\.diagnostic-tile\s*\{[\s\S]*?min-height:\s*86px/);
    assert.match(css, /\.diagnostic-tile__value\s*\{[\s\S]*?font-size:\s*var\(--fs-body-sm\)/);
    assert.match(css, /\.content-body--system\s*\{[\s\S]*?height:\s*100%[\s\S]*?overflow:\s*hidden/);
    assert.match(css, /\.content-body--system\s*\{[\s\S]*?display:\s*grid[\s\S]*?grid-template-rows:\s*auto\s+minmax\(0,\s*1fr\)/);
    assert.match(css, /\.framework-dashboard\s*\{[\s\S]*?align-items:\s*stretch[\s\S]*?min-height:\s*0[\s\S]*?height:\s*100%/);
    assert.match(css, /\.framework-stack,\s*\.tool-stack\s*\{[\s\S]*?min-height:\s*0/);
    assert.match(css, /\.framework-stack--primary\s*\{[\s\S]*?display:\s*grid[\s\S]*?grid-template-rows:\s*auto\s+minmax\(0,\s*1fr\)/);
    assert.match(css, /\.framework-stack--secondary\s*\{[\s\S]*?display:\s*grid[\s\S]*?grid-template-rows:\s*minmax\(0,\s*1fr\)/);
    assert.match(css, /\.framework-stack--primary\s+#log-panel\s*\{[\s\S]*?display:\s*grid[\s\S]*?grid-template-rows:\s*var\(--panel-header-height\)\s+minmax\(0,\s*1fr\)[\s\S]*?min-height:\s*0/);
    assert.match(css, /\.framework-stack--primary\s+#log-panel\s+\.log-output\s*\{[\s\S]*?height:\s*100%[\s\S]*?min-height:\s*0[\s\S]*?max-height:\s*none/);
    assert.match(css, /\.framework-stack--secondary\s+>\s+\.panel\s*\{[\s\S]*?display:\s*grid[\s\S]*?grid-template-rows:\s*var\(--panel-header-height\)\s+minmax\(0,\s*1fr\)[\s\S]*?min-height:\s*0/);
    assert.match(css, /\.framework-stack--secondary\s+\.panel-body\s*\{[\s\S]*?min-height:\s*0[\s\S]*?overflow:\s*hidden/);
    assert.match(css, /\.framework-stack--secondary\s+\.ai-skill-installer\s*\{[\s\S]*?height:\s*100%[\s\S]*?min-height:\s*0[\s\S]*?overflow:\s*auto/);
    assert.match(css, /\.ai-skill-target-grid\s*\{[\s\S]*?grid-template-columns:\s*repeat\(2,\s*minmax\(0,\s*1fr\)\)/);
    assert.match(css, /\.ai-skill-target-card\s*\{[\s\S]*?grid-template-rows:\s*auto\s+minmax\(32px,\s*1fr\)\s+auto/);
    assert.match(css, /\.ai-skill-target-card__actions\s*\{[\s\S]*?margin-top:\s*auto/);
    assert.match(css, /@media\s*\(max-width:\s*1360px\)[\s\S]*?\.framework-dashboard\s*\{[\s\S]*?grid-template-columns:\s*minmax\(0,\s*1fr\)/);
    assert.match(css, /@media\s*\(max-width:\s*1360px\)[\s\S]*?\.content-body--system\s*\{[\s\S]*?overflow-y:\s*auto/);
    assert.match(css, /@media\s*\(max-width:\s*1360px\)[\s\S]*?\.framework-grid--status\s*\{[\s\S]*?grid-template-columns:\s*repeat\(3,\s*minmax\(0,\s*1fr\)\)/);
    assert.match(css, /@media\s*\(max-width:\s*1360px\)[\s\S]*?\.diagnostic-grid--compact\s*\{[\s\S]*?grid-template-columns:\s*repeat\(2,\s*minmax\(0,\s*1fr\)\)/);
    assert.doesNotMatch(css, /\.framework-stack--primary\s+#log-panel\s+\.log-output\s*\{[\s\S]*?max-height:\s*min\(32vh,\s*340px\)/);
    assert.match(routerSource, /\$pageBody\.classList\.remove\('content-body--system'\)/);
    assert.match(routerSource, /\$pageBody\.classList\.add\(`content-body--\$\{pageId\}`\)/);
    assert.match(systemPage, /<div class="framework-stack framework-stack--primary">/);
    assert.match(systemPage, /<div class="framework-stack framework-stack--secondary">/);
    assert.match(systemPage, /renderEngineStatusCard\(\)/);
    assert.match(systemPage, /renderSystemLogPanel\(\)/);
});

test('frontend renders Chinese framework, theme controls, svg icons, and FsmKit workbench sections', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /t\('system\.title'\)/);
    assert.match(js, /t\('system\.engine_status'\)/);
    assert.match(js, /framework-status-panel/);
    assert.match(js, /bridge-health-panel/);
    assert.match(js, /t\('system\.log_title'\)/);
    assert.match(js, /t\('command_panel\.title'\)/);
    assert.match(js, /light:\s*'theme\.light'/);
    assert.match(js, /dark:\s*'theme\.dark'/);
    assert.match(js, /Unity \/ Godot/);
    assert.match(js, /developer-context-strip/);
    assert.match(js, /Native Debug Console/);
    assert.match(js, /t\('system\.host_connection'\)/);
    assert.match(js, /命令桥/);
    assert.match(js, /t\('system\.kit_diagnostic'\)/);
    assert.doesNotMatch(js, /面向 Unity \/ Godot 游戏开发者的人类调试工作台/);
    assert.match(js, /t\('fsmkit\.active_machines'\)/);
    assert.match(js, /t\('fsmkit\.state_flow_graph'\)/);
    assert.match(js, /t\('fsmkit\.event_insights'\)/);
    assert.match(js, /fsm-state-card/);
    assert.match(js, /fsm-insight-list/);
    assert.match(js, /t\('fsmkit\.transition_history'\)/);
    assert.match(js, /language-select/);
    assert.match(js, /theme-toggle/);
    assert.match(js, /function svgIcon\(/);
    assert.match(js, /restoreWindowState\(\{\s*showAfter:\s*true\s*\}\)/);
    assert.match(js, /invoke\('mark_panel_window_ready'\)/);
    assert.doesNotMatch(css, /\.kit-header\s*\{/);
    assert.match(css, /\.hero-intro-card,\s*\n\.kit-toolbar,\s*\n\.audio-master-strip\s*\{[\s\S]*?-webkit-app-region:\s*no-drag/);
    assert.doesNotMatch(js, /setTimeout\(showWindowOnce,\s*1200\)/);
    assert.doesNotMatch(js, /setTimeout\(\(\)\s*=>\s*restoreWindowState\(\),\s*3000\)/);
    assert.doesNotMatch(js, /renderBridgePage\(/);
    assert.doesNotMatch(js, /data-page="bridge"/);
    assert.doesNotMatch(js, /工作台偏好/);
});

test('framework overview replaces responsibility panel with command bridge query selectors', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const systemSection = js.slice(js.indexOf('// 页面：System 概览'), js.indexOf('// 快捷操作辅助函数'));
    const commandSection = js.slice(js.indexOf('// core/command-bridge.js'), js.indexOf('// core/ai-skill-installer.js'));

    assert.match(js, /const FRAMEWORK_COMMAND_CATALOG\s*=/);
    assert.match(js, /Architecture:\s*\[[\s\S]*?get_workbench_snapshot[\s\S]*?list_architectures/);
    assert.match(js, /System['"]?,?\s*[\s\S]*?list_commands/);
    assert.match(commandSection, /async function refreshFrameworkCommandCatalog\(/);
    assert.match(commandSection, /function applyFrameworkCommandCatalogResponse\(/);
    assert.match(commandSection, /sendCommand\(\{\s*kit:\s*'System',\s*action:\s*'list_commands'/);
    assert.match(js, /System:\s*\[/);
    assert.match(js, /kit:\s*'PoolKit'/);
    assert.match(js, /action:\s*'get_workbench_snapshot'/);
    assert.match(commandSection, /function renderFrameworkCommandKitOptions\(/);
    assert.match(commandSection, /function renderFrameworkCommandActionOptions\(/);
    assert.match(commandSection, /function updateFrameworkActionOptions\(/);
    assert.match(commandSection, /id="framework-action-select"/);
    assert.match(commandSection, /aria-label="\$\{t\('command_panel\.action_label'\)\}"/);
    assert.match(commandSection, /data-command-preset-kit="System"/);
    assert.match(commandSection, /data-command-preset-action="bridge_status"/);
    assert.match(systemSection, /renderFrameworkCommandContent\(\)/);
    assert.doesNotMatch(systemSection, /renderDeveloperContextPanel/);
    assert.doesNotMatch(systemSection, /工作台职责/);
    assert.doesNotMatch(systemSection, /developer-context-panel/);
    assert.doesNotMatch(systemSection, /framework-action-input/);
    assert.doesNotMatch(systemSection, /placeholder="输入 action/);
    assert.match(css, /\.framework-command__form/);
    assert.match(css, /\.framework-command__hint/);
});

test('system command and font controls stay bounded at narrow widths', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const commandSection = js.slice(js.indexOf('// core/command-bridge.js'), js.indexOf('// core/ai-skill-installer.js'));

    assert.match(commandSection, /const desc = getFrameworkCommandDescription\(item\)/);
    assert.match(commandSection, /<option value="\$\{escapeHtml\(item\.action\)\}" title="\$\{escapeHtml\(desc\)\}"\$\{selected\}>/);
    assert.match(commandSection, />\$\{escapeHtml\(item\.label \|\| item\.action\)\}<\/option>/);
    assert.doesNotMatch(commandSection, /const label = desc \? `\$\{item\.label\} - \$\{desc\}` : item\.label/);

    assert.match(css, /\.framework-engine-panel__controls\s*\{[\s\S]*?min-width:\s*0/);
    assert.match(css, /\.framework-engine-panel__section--command,\s*\.framework-engine-panel__section--font\s*\{[\s\S]*?overflow:\s*hidden/);
    assert.match(css, /\.framework-command\s*\{[\s\S]*?min-width:\s*0/);
    assert.match(css, /\.framework-command__form\s*\{[\s\S]*?grid-template-columns:\s*minmax\(108px,\s*0\.54fr\)\s+minmax\(0,\s*1fr\)\s+minmax\(56px,\s*auto\)[\s\S]*?min-width:\s*0/);
    assert.match(css, /\.framework-command__form\s+\.cmd-select\s*\{[\s\S]*?min-width:\s*0/);
    assert.match(css, /\.font-preference\s*\{[\s\S]*?min-width:\s*0/);
    assert.match(css, /\.font-preference__controls\s*\{[\s\S]*?grid-template-columns:\s*minmax\(132px,\s*0\.72fr\)\s+minmax\(0,\s*1fr\)\s+minmax\(54px,\s*auto\)[\s\S]*?min-width:\s*0/);
    assert.match(css, /\.font-preference__controls\s+\.cmd-select,\s*\.font-preference__controls\s+\.cmd-input\s*\{[\s\S]*?min-width:\s*0/);
    assert.match(css, /@media\s*\(max-width:\s*1120px\)[\s\S]*?\.framework-engine-panel__controls\s*\{[\s\S]*?grid-template-columns:\s*minmax\(0,\s*1fr\)/);
});

test('Architecture workbench exposes live instances and registered services through the command bridge', () => {
    const html = readDistFile('index.html');
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const handler = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'CommandBridge', 'Handlers', 'ArchitectureCommandHandler.cs');
    const registry = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Architecture', 'ArchitectureDebugInfo.cs');
    const unityHost = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Editor', 'CommandBridge', 'UnityCommandBridgeHost.cs');
    const godotHost = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Godot', 'Runtime', 'Core', 'CommandBridge', 'Runtime', 'GodotCommandBridgeHost.cs');

    const groups = extractSidebarGroups(html);
    assert.deepEqual(groups[1], { header: 'Architecture', labels: ['Architecture'] });
    assert.match(html, /data-page="architecture"/);
    assert.match(js, /function renderArchitecturePage\(\)/);
    assert.match(js, /\$pageBody\.classList\.add\('content-body--architecture'\)/);
    assert.match(js, /setHero\(\s*t\('architecture\.title'\)/);
    assert.match(js, /function fetchArchitectureWorkbenchState\(\{\s*forceCommandRefresh\s*=\s*false\s*\}\s*=\s*\{\}\)/);
    assert.match(js, /fetchKitWorkbenchState\('Architecture',\s*normalizeArchitectureStatePayload/);
    assert.doesNotMatch(js, /function fetchArchitectureWorkbenchStateFromCommands\(\)/);
    assert.match(js, /forceCommandRefresh:\s*true/);
    assert.match(js, /function normalizeArchitectureStatePayload\(/);
    assert.match(js, /architectureKitState/);
    assert.match(js, /data-architecture-search/);
    assert.match(js, /data-architecture-type/);
    assert.match(js, /t\('architecture\.registered_services'\)/);
    assert.match(js, /architectureCount/);
    assert.match(js, /serviceCount/);
    assert.match(js, /registerPage\('architecture',\s*\{\s*render:\s*renderArchitecturePage\s*\}\)/);
    assert.match(js, /registerKitReactiveRefresh\('architecture'/);
    assert.match(css, /\.content-body--architecture/);
    assert.match(css, /\.kit-workbench-grid--architecture/);
    assert.match(css, /\.kit-detail-summary--architecture/);
    assert.match(handler, /public sealed class ArchitectureCommandHandler : IKitCommandHandler/);
    assert.match(handler, /KitName\s*=>\s*"Architecture"/);
    assert.match(handler, /"stats"[\s\S]*"get_workbench_snapshot"[\s\S]*"list_architectures"/);
    assert.match(registry, /public sealed class ArchitectureDebugInfo/);
    assert.match(registry, /public sealed class ArchitectureServiceDebugInfo/);
    assert.match(registry, /public static class ArchitectureRegistry/);
    assert.match(unityHost, /Register\(new ArchitectureCommandHandler\(\)\)/);
    assert.match(godotHost, /Register\(new ArchitectureCommandHandler\(\)\)/);
});

test('window sizing keeps the workbench visible and throttles resize layout work', () => {
    const js = readFrontendScripts();
    const tauriConfig = JSON.parse(readTauriSourceFile('src-tauri', 'tauri.conf.json'));
    const mainWindow = tauriConfig.app.windows.find(window => window.label === 'main');

    assert.ok(mainWindow, 'main Tauri window config should exist');
    assert.equal(mainWindow.minWidth, 960);
    assert.equal(mainWindow.minHeight, 640);
    assert.match(js, /const MIN_WINDOW_WIDTH\s*=\s*960/);
    assert.match(js, /const MIN_WINDOW_HEIGHT\s*=\s*640/);
    assert.match(js, /function clampWindowSize\(/);
    assert.match(js, /Math\.max\(MIN_WINDOW_WIDTH,\s*Math\.round\(w\)\)/);
    assert.match(js, /Math\.max\(MIN_WINDOW_HEIGHT,\s*Math\.round\(h\)\)/);
    assert.match(js, /function scheduleWorkspaceResizeWork\(/);
    assert.match(js, /let workspaceResizeFrame\s*=\s*0/);
    assert.match(js, /requestAnimationFrame\(\(\)\s*=>\s*\{[\s\S]*?syncSidebarActiveIndicator\(\)/);
    assert.doesNotMatch(js, /window\.addEventListener\('resize',\s*\(\)\s*=>\s*\{\s*scheduleFsmCurrentStateFit\(\);\s*syncSidebarActiveIndicator\(\);\s*\}\)/);
});

test('window persistence timer skips hidden or unfocused windows', () => {
    const js = readFrontendScripts();
    const persistence = js.match(/function initWindowPersistence\(\) \{[\s\S]*?\n\}/)?.[0] || '';

    assert.match(js, /function isWindowInteractive\(\)/);
    assert.match(persistence, /setInterval\(\(\) => \{[\s\S]*?if\s*\(!isWindowInteractive\(\)\)\s*return;[\s\S]*?saveWindowState\(\);[\s\S]*?refinePositionFromTauri\(\);/);
});

test('main window resizes through internal handles while suppressing idle native edge hit-test', () => {
    const html = readDistFile('index.html');
    const css = readDistFile('style.css');
    const js = readTauriSourceFile('src-tauri', 'src', 'main.rs');
    const frontend = readFrontendScripts();
    const tauriConfig = JSON.parse(readTauriSourceFile('src-tauri', 'tauri.conf.json'));
    const capability = JSON.parse(readTauriSourceFile('src-tauri', 'capabilities', 'default.json'));
    const mainWindow = tauriConfig.app.windows.find(window => window.label === 'main');

    assert.ok(mainWindow, 'main Tauri window config should exist');
    assert.equal(mainWindow.resizable, true);
    assert.match(js, /set_resizable\(false\)/);
    assert.ok(capability.permissions.includes('core:window:allow-set-resizable'));
    assert.ok(capability.permissions.includes('core:window:allow-start-resize-dragging'));
    assert.match(html, /data-window-resize="north"/);
    assert.match(html, /data-window-resize="south-east"/);
    assert.match(css, /\.window-resize-handle/);
    assert.match(frontend, /function bindWindowResizeHandles\(/);
    assert.match(frontend, /WINDOW_RESIZE_DIRECTIONS/);
    assert.match(frontend, /'north-east':\s*'NorthEast'/);
    assert.match(frontend, /'south-east':\s*'SouthEast'/);
    assert.match(frontend, /startResizeDragging\(nativeDirection\)/);
    assert.doesNotMatch(frontend, /startResizeDragging\(direction\)/);
    assert.match(frontend, /setResizable\(true\)/);
    assert.match(frontend, /setResizable\(false\)/);
});

test('system log panel wraps long lines and supports copying rendered text', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /id="log-copy-btn"/);
    assert.match(js, /function copyLogPanelText\(/);
    assert.match(js, /navigator\.clipboard\.writeText/);
    assert.match(js, /document\.createElement\('div'\)/);
    assert.match(js, /\.textContent\s*=\s*e\.message/);
    assert.doesNotMatch(js, /<span class="time">\$\{e\.time\}<\/span>\$\{e\.message\}/);
    assert.match(css, /\.log-output\s*\{[^}]*user-select:\s*text/);
    assert.match(css, /\.log-entry\s*\{[^}]*white-space:\s*pre-wrap/);
    assert.match(css, /\.log-entry\s*\{[^}]*overflow-wrap:\s*anywhere/);
    assert.doesNotMatch(css, /\.log-entry\s*\{[^}]*white-space:\s*nowrap/);
});

test('framework overview keeps engine status and log in the primary stack while AI skills stay secondary', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const rust = readTauriSourceFile('src-tauri', 'src', 'main.rs');
    const systemPage = readDistFile('pages/system.js');

    const primaryStackIndex = systemPage.indexOf('<div class="framework-stack framework-stack--primary">');
    const secondaryStackIndex = systemPage.indexOf('<div class="framework-stack framework-stack--secondary">');
    const engineCardIndex = systemPage.indexOf('renderEngineStatusCard()');
    const logPanelIndex = systemPage.indexOf('renderSystemLogPanel()');
    const installPanelIndex = systemPage.indexOf('renderAiSkillInstallPanel()');
    assert.ok(primaryStackIndex >= 0, 'System page should render a primary stack');
    assert.ok(secondaryStackIndex > primaryStackIndex, 'System page should render the secondary stack after the primary stack');
    assert.ok(engineCardIndex > primaryStackIndex && engineCardIndex < secondaryStackIndex, 'Engine status card should sit in the primary stack');
    assert.ok(logPanelIndex > engineCardIndex && logPanelIndex < secondaryStackIndex, 'Log panel should sit below the engine status card in the primary stack');
    assert.ok(installPanelIndex >= 0, 'System page should render the AI Skill install card');
    assert.ok(installPanelIndex > secondaryStackIndex, 'AI Skill install card should sit in the secondary stack');

    assert.match(js, /function renderAiSkillInstallPanel\(/);
    assert.match(js, /function bindAiSkillInstallerPanel\(/);
    assert.match(js, /async function refreshAiSkillInstallerStatus\(/);
    assert.match(js, /invoke\('list_ai_skills'/);
    assert.match(js, /invoke\('install_ai_skill'/);
    assert.match(js, /invoke\('uninstall_ai_skill'/);
    assert.match(js, /data-ai-skill-name="yokiframe"/);
    assert.match(js, /data-ai-skill-name="yokiframe-command-bridge"/);
    assert.match(js, /data-ai-skill-name="yokiframe-editor"/);
    assert.match(js, /data-ai-skill-target="codex"/);
    assert.match(js, /data-ai-skill-target="claude"/);
    assert.match(js, /data-ai-skill-custom-install/);
    assert.match(js, /t\('ai_skill\.title'\)/);

    assert.match(css, /\.ai-skill-installer/);
    assert.match(css, /\.ai-skill-target-grid/);
    assert.match(css, /\.ai-skill-target-card/);

    assert.match(rust, /fn list_ai_skills\(/);
    assert.match(rust, /fn install_ai_skill\(/);
    assert.match(rust, /fn uninstall_ai_skill\(/);
    assert.match(rust, /tauri::generate_handler!\[[\s\S]*list_ai_skills[\s\S]*install_ai_skill[\s\S]*uninstall_ai_skill/);
});

test('YokiFrame package carries all AI skill source folders', () => {
    for (const skillName of ['yokiframe', 'yokiframe-command-bridge', 'yokiframe-editor']) {
        const skillFile = resolveWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Editor', 'Skills', skillName, 'SKILL.md');
        assert.ok(fs.existsSync(skillFile), `${skillName} should be packaged under Assets/YokiFrame/Core/Editor/Skills`);
        const body = fs.readFileSync(skillFile, 'utf8');
        assert.match(body, new RegExp(`name:\\s*${skillName}`));
        assert.match(body, /description:/);
    }
    assert.ok(
        workspaceFileExists('Assets', 'YokiFrame', 'Core', 'Editor', 'Skills', 'yokiframe-command-bridge', 'references', 'command-catalog.md'),
        'yokiframe-command-bridge should package its command catalog'
    );
}
);

test('system log rendering is frame-batched and appends only changed rows', () => {
    const js = readFrontendScripts();

    assert.match(js, /let pendingLogRenderFrame\s*=\s*0/);
    assert.match(js, /let renderedLogCount\s*=\s*0/);
    assert.match(js, /function scheduleLogPanelRender\(/);
    assert.match(js, /pendingLogRenderFrame\s*=\s*requestAnimationFrame\(\(\)\s*=>\s*\{/);
    assert.match(js, /scheduleLogPanelRender\(\)/);
    assert.match(js, /appendChild\(createLogEntry\(logBuffer\[i\]\)\)/);
    assert.match(js, /while\s*\(container\.childElementCount\s*>\s*logBuffer\.length\)/);
    assert.match(js, /container\.scrollTop\s*=\s*container\.scrollHeight/);
    assert.doesNotMatch(js, /logBuffer\.forEach\(e\s*=>\s*fragment\.appendChild\(createLogEntry\(e\)\)\)/);
    assert.doesNotMatch(js, /container\.replaceChildren\(fragment\)/);
});

test('system status panels coalesce poll updates and skip unchanged DOM writes', () => {
    const js = readFrontendScripts();
    const pollStatusStart = js.indexOf('async function pollStatus');
    const commandBridgeStart = js.indexOf('// Command Bridge', pollStatusStart);
    const pollStatusSource = js.slice(pollStatusStart, commandBridgeStart);

    assert.match(js, /let pendingSystemPanelFrame\s*=\s*0/);
    assert.match(js, /let lastFrameworkStatusPanelHtml\s*=\s*''/);
    assert.match(js, /let lastBridgeHealthPanelHtml\s*=\s*''/);
    assert.match(js, /function scheduleSystemPanelUpdate\(/);
    assert.match(js, /pendingSystemPanelFrame\s*=\s*requestAnimationFrame\(\(\)\s*=>\s*\{/);
    assert.match(pollStatusSource, /scheduleSystemPanelUpdate\(\)/);
    assert.doesNotMatch(pollStatusSource, /updateFrameworkStatusPanel\(\);\s*updateFrameworkThemeSummary\(\);\s*updateBridgeHealthPanel\(\);/);
    assert.match(js, /if\s*\(!force\s*&&\s*html\s*===\s*lastFrameworkStatusPanelHtml\)\s*return/);
    assert.match(js, /if\s*\(!force\s*&&\s*html\s*===\s*lastBridgeHealthPanelHtml\)\s*return/);
});

test('Kit push events route through a shared throttled reactive refresh bus', () => {
    const js = readFrontendScripts();

    assert.match(js, /KIT_REACTIVE_REFRESH_THROTTLE_MS\s*=\s*220/);
    assert.match(js, /FSMKIT_REACTIVE_REFRESH_THROTTLE_MS\s*=\s*80/);
    assert.match(js, /KIT_INTERACTION_REFRESH_DEFER_MS/);
    assert.match(js, /function markKitInteractionActive\(/);
    assert.match(js, /function getKitInteractionRefreshDelay\(/);
    assert.match(js, /\$pageBody\.addEventListener\('scroll',\s*markKitInteractionActive/);
    assert.match(js, /\$pageBody\.addEventListener\('wheel',\s*markKitInteractionActive/);
    assert.match(js, /const kitReactiveRefreshHandlers\s*=\s*new Map\(\)/);
    assert.match(js, /const kitReactiveRefreshState\s*=\s*new Map\(\)/);
    assert.match(js, /function makeStableSignature\(value\)/);
    assert.match(js, /function registerKitReactiveRefresh\(/);
    assert.match(js, /function scheduleKitReactiveRefresh\(/);
    assert.match(js, /async function flushKitReactiveRefresh\(/);
    assert.match(js, /setTimeout\(\(\)\s*=>\s*flushKitReactiveRefresh\(kitId\)/);
    assert.match(js, /registerKitReactiveRefresh\('fsmkit'/);
    assert.match(js, /refresh:\s*refreshFsmKitReactive/);
    assert.match(js, /throttleMs:\s*FSMKIT_REACTIVE_REFRESH_THROTTLE_MS/);
    assert.match(js, /refresh:\s*refreshEventKitReactive/);
    assert.doesNotMatch(js, /FSM_WORKBENCH_REFRESH_THROTTLE_MS/);
    assert.doesNotMatch(js, /pendingFsmWorkbenchRefreshReason/);
    assert.doesNotMatch(js, /function scheduleFsmWorkbenchRefresh\(/);
    assert.doesNotMatch(js, /function flushFsmWorkbenchRefresh\(/);

    const setupMatch = js.match(/async function setupPushListeners\(\) \{([\s\S]*?)\n\}/);
    assert.ok(setupMatch, 'setupPushListeners should exist');
    assert.match(setupMatch[1], /scheduleKitReactiveRefresh\(data/);
    assert.match(setupMatch[1], /yoki-telemetry/);
    assert.doesNotMatch(setupMatch[1], /data\?\.type !== 'fsm_update'/);
    assert.doesNotMatch(setupMatch[1], /loadFsmWorkbench\(/);
});

test('frontend startup and push listeners are idempotent to avoid duplicate runtime work', () => {
    const js = readFrontendScripts();
    const startup = js.match(/function bootstrapYokiFrameEditor\(\) \{[\s\S]*?\n\}/)?.[0] || '';
    const pushListeners = js.match(/async function setupPushListeners\(\) \{[\s\S]*?\n\}/)?.[0] || '';

    assert.match(js, /let yokiFrameEditorBootstrapped\s*=\s*false/);
    assert.match(startup, /if\s*\(yokiFrameEditorBootstrapped\)\s*return/);
    assert.match(startup, /yokiFrameEditorBootstrapped\s*=\s*true/);
    assert.match(js, /let pushListenersReady\s*=\s*false/);
    assert.match(js, /let pushListenerSetupPromise\s*=\s*null/);
    assert.match(js, /const pushListenerUnlisteners\s*=\s*\[\]/);
    assert.match(pushListeners, /if\s*\(pushListenersReady\)\s*return/);
    assert.match(pushListeners, /if\s*\(pushListenerSetupPromise\)\s*return pushListenerSetupPromise/);
    assert.match(pushListeners, /pushListenersReady\s*=\s*true/);
    assert.match(js, /function teardownPushListeners\(\)/);
    assert.match(js, /pushListenerUnlisteners\.forEach/);
    assert.match(startup, /window\.addEventListener\('beforeunload',\s*teardownPushListeners\)/);
});

test('startup diagnostics are stripped from release launch path', () => {
    const windowState = readDistFile('core/window-state.js');
    const startupSource = readDistFile('main.js');
    const launcher = readWorkspaceFile(
        'Assets',
        'YokiFrame',
        'Core',
        'Runtime',
        'Adapters',
        'Unity',
        'Editor',
        'TauriBridge',
        'TauriLauncher.cs',
    );
    const launcherProcess = readWorkspaceFile(
        'Assets',
        'YokiFrame',
        'Core',
        'Runtime',
        'Adapters',
        'Unity',
        'Editor',
        'TauriBridge',
        'TauriLauncher.Process.cs',
    );

    assert.match(windowState, /invoke\('mark_panel_window_ready'\)/);
    assert.doesNotMatch(windowState, /markStartup/);
    assert.doesNotMatch(windowState, /mark_startup_event/);
    assert.doesNotMatch(windowState, /frontend\.first\.paint|frontend\.raf|frontend\.ready|frontend\.restore/);
    assert.doesNotMatch(startupSource, /markStartup/);
    assert.doesNotMatch(launcher, /TauriStartup|LogStartup|CreateStartupTraceId|STARTUP_TRACE|BuildStartupLogLine/);
    assert.doesNotMatch(launcherProcess, /TauriStartup|LogStartup|STARTUP_TRACE|ShouldForwardTauriStdoutToUnity/);
});

test('FsmKit stable refresh bindings do not stack duplicate event listeners', () => {
    const js = readFrontendScripts();
    const graphBindings = js.match(/function bindFsmGraphInteractions\(\) \{[\s\S]*?\n\}/)?.[0] || '';
    const listBindings = js.match(/function bindFsmWorkbenchList\(\) \{[\s\S]*?\n\}/)?.[0] || '';

    assert.match(graphBindings, /if\s*\(fit && fit\.dataset\.bound !== '1'\)/);
    assert.match(graphBindings, /fit\.dataset\.bound\s*=\s*'1'/);
    assert.match(graphBindings, /if\s*\(zoomIn && zoomIn\.dataset\.bound !== '1'\)/);
    assert.match(graphBindings, /if\s*\(zoomOut && zoomOut\.dataset\.bound !== '1'\)/);
    assert.match(graphBindings, /if\s*\(scroll && scroll\.dataset\.bound !== '1'\)/);
    assert.match(graphBindings, /scroll\.dataset\.bound\s*=\s*'1'/);
    assert.match(listBindings, /if\s*\(btn\.dataset\.bound === '1'\)\s*return/);
    assert.match(listBindings, /btn\.dataset\.bound\s*=\s*'1'/);
});

test('stable workbench renderer repaints the loading placeholder when the cached signature matches', () => {
    const js = readFrontendScripts();
    const stableRenderer = js.match(/function renderWorkbenchHtmlStable\(state,\s*html,\s*signature,\s*bind\)\s*\{[\s\S]*?\n\}/)?.[0] || '';

    assert.ok(stableRenderer, 'stable workbench renderer should exist');
    assert.match(stableRenderer, /isPageLoadingStateVisible\(\)/);
    assert.match(stableRenderer, /state\.renderSignature === signature[\s\S]*!isPageLoadingStateVisible\(\)/);
    assert.match(stableRenderer, /\$pageBody\.innerHTML = html/);
});

test('Kit reactive refreshes avoid full workbench fetches on high-frequency ticks', () => {
    const js = readFrontendScripts();
    const eventBridge = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Editor', 'TauriBridge', 'CommandBridge', 'EventKitEditorDataBridge.cs');

    const fsmReactive = js.match(/async function refreshFsmKitReactive\(event\) \{[\s\S]*?\n\}/);
    assert.ok(fsmReactive, 'FsmKit reactive refresh should be capturable');
    assert.match(fsmReactive[0], /refreshFsmWorkbenchCurrentState\(event\)/);
    assert.match(fsmReactive[0], /scheduleFsmDetailReconcile\(reason\)/);
    assert.doesNotMatch(fsmReactive[0], /fetchFsmWorkbenchSnapshot/);

    assert.match(js, /const FSM_DETAIL_RECONCILE_MS\s*=\s*1800/);
    assert.match(js, /function applyFsmWorkbenchListSnapshot\(/);
    assert.match(js, /function mergeFsmRealtimeDetail\(selectedMeta\)/);
    assert.match(js, /function mergeFsmRealtimeStateTree\(states,\s*currentStateId\)/);
    assert.match(js, /liveDetail\.states\s*=\s*mergeFsmRealtimeStateTree\(cachedDetail\.states,\s*liveDetail\.currentStateId\)/);
    assert.match(js, /renderOrUpdateFsmWorkbench\(fsms,\s*selectedMeta,\s*liveDetail,\s*history,\s*'',\s*preferInPlace\)/);
    assert.doesNotMatch(js, /appendFsmRealtimeTransition/);
    assert.doesNotMatch(js, /fsmRealtimeCurrentStateByMachine/);
    assert.match(js, /function scheduleFsmDetailReconcile\(/);
    assert.match(js, /const requestSeq\s*=\s*fsmWorkbenchLoadSeq/);

    const eventReactive = js.match(/async function refreshEventKitReactive\(event\) \{[\s\S]*?\n\}/);
    assert.ok(eventReactive, 'EventKit reactive refresh should be capturable');
    assert.match(eventReactive[0], /applyEventKitRealtimePayload\(payload\)/);
    assert.match(eventReactive[0], /scheduleEventKitSnapshotReconcile\(\)/);
    assert.doesNotMatch(eventReactive[0], /fetchEventKitMonitorSnapshot/);
    assert.match(js, /const EVENTKIT_SNAPSHOT_RECONCILE_MS\s*=\s*1600/);
    assert.match(js, /function applyEventKitRealtimePayload\(payload\)/);
    assert.match(js, /function scheduleEventKitSnapshotReconcile\(options = \{\}\)/);
    assert.match(js, /EVENTKIT_MAX_RECENT_EVENTS/);
    assert.match(js, /function scheduleEventKitMonitorPartialRender\(/);
    assert.match(js, /function renderEventKitMonitorPartialUpdate\(/);
    assert.match(js, /eventKitMonitorPendingRowIds/);
    assert.match(eventReactive[0], /scheduleEventKitMonitorPartialRender\(\)/);
    assert.doesNotMatch(eventReactive[0], /renderEventKitWorkbench\(\)/);

    assert.match(eventBridge, /private static EventRecord sLatestEvent/);
    assert.match(eventBridge, /\\\"record\\\":/);
    assert.match(eventBridge, /AppendEventRecordJson\(sb, sLatestEvent\)/);
});

test('navigation clicks update immediately while page renders and stale refreshes are guarded', () => {
    const js = readFrontendScripts();

    assert.match(js, /let navigationSeq\s*=\s*0/);
    assert.match(js, /let pendingNavigationRenderTask\s*=\s*0/);
    assert.match(js, /function isCurrentNavigation\(/);
    assert.match(js, /function scheduleNavigationRender\(/);
    assert.match(js, /function handleInteractiveFocusResume\(/);
    assert.match(js, /statusPollInFlight/);
    assert.match(js, /statusPollQueued/);
    assert.match(js, /document\.hasFocus\(\)/);
    assert.match(js, /window\.addEventListener\('focus',\s*handleInteractiveFocusResume\)/);
    assert.match(js, /document\.addEventListener\('visibilitychange'/);
    assert.match(js, /const requestSeq\s*=\s*\+\+navigationSeq/);
    assert.match(js, /const pageLoadToken\s*=\s*currentPageLoadToken\(pageId\)/);
    assert.match(js, /const PAGE_LOADING_STATE_PAGE_IDS\s*=\s*new Set\(\[\s*'architecture',\s*'tablekit'\s*\]\)/);
    assert.match(js, /function shouldShowPageLoadingState\(pageId\)/);
    assert.match(js, /if\s*\(shouldShowPageLoadingState\(pageId\)\)\s*\{\s*setPageBodyForLoad\(pageLoadToken,\s*renderPageLoadingState\(\)\);/);
    assert.match(js, /scheduleNavigationRender\(pageId,\s*requestSeq\)/);
    assert.match(js, /isCurrentNavigation\(refreshNavigationSeq,\s*handler\.pageId\)/);
    assert.match(js, /activePage\s*===\s*'fsmkit'/);
});

test('EventKit async registration loads cannot overwrite another active page', () => {
    const js = readFrontendScripts();

    assert.match(js, /function currentPageLoadToken\(/);
    assert.match(js, /function isCurrentPageLoad\(/);
    assert.match(js, /function setPageBodyForLoad\(/);
    assert.match(js, /loadEventRegistrations\(currentPageLoadToken\('eventkit'\)\)/);

    const eventKitLoader = js.match(/async function loadEventRegistrations\(pageLoadToken[\s\S]*?\n\}/);
    assert.ok(eventKitLoader, 'EventKit loader should accept a page load token');
    assert.match(eventKitLoader[0], /if\s*\(!isCurrentPageLoad\(pageLoadToken\)\)\s*return/);
    assert.match(eventKitLoader[0], /setPageBodyForLoad\(pageLoadToken/);

    const fullEventKitLoader = js.match(/async function loadEventRegistrations\(pageLoadToken[\s\S]*?async function fetchEventKitMonitorSnapshot/);
    assert.ok(fullEventKitLoader, 'EventKit loader should be capturable');
    assert.doesNotMatch(fullEventKitLoader[0], /renderMetricsForLoad/);
    assert.doesNotMatch(fullEventKitLoader[0], /类型事件|枚举事件|字符串事件/);
});

test('EventKit workbench merges realtime monitor and code scan into one engine-aware flow', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const pageRenderer = js.match(/function renderEventKitPage\(\)[\s\S]*?async function refreshEventKit/);
    const monitorRenderer = js.match(/function renderEventKitMonitorHtml\(view\)[\s\S]*?function scheduleEventKitMonitorPartialRender/);

    assert.match(js, /t\('eventkit\.title'\)/);
    assert.match(js, /t\('eventkit\.subtitle'\)/);
    assert.match(js, /clearTabs\(\)/);
    assert.match(js, /t\('eventkit\.scan_code'\)/);
    assert.ok(pageRenderer, 'EventKit page renderer should be capturable');
    assert.ok(monitorRenderer, 'EventKit monitor renderer should be capturable');
    assert.match(js, /function setEventKitHero\(\)/);
    assert.match(js, /function renderEventKitHeroActions\(/);
    assert.match(pageRenderer[0], /setEventKitHero\(\)/);
    assert.doesNotMatch(pageRenderer[0], /runEventKitCodeScan/);
    assert.doesNotMatch(pageRenderer[0], /renderEventKitTabs/);
    assert.doesNotMatch(js, /eventkit-engine-strip/);
    assert.match(js, /eventkit-monitor-workbench/);
    assert.doesNotMatch(js, /eventkit-scan-workbench/);
    assert.match(js, /eventkit-v1-relation-head/);
    assert.match(js, /eventkit-v1-row/);
    assert.match(js, /eventkit-v1-event-column/);
    assert.match(js, /eventkit-v1-unregister-stack/);
    assert.match(js, /eventkit-v1-event-node/);
    assert.match(js, /function renderEventKitScanUnregisters/);
    assert.match(js, /renderEventKitScanUnregisters\(unregisterFiles\)/);
    assert.match(js, /data-eventkit-monitor-row/);
    assert.doesNotMatch(js, /function renderEventKitUnifiedToolbar/);
    assert.doesNotMatch(monitorRenderer[0], /eventkit-v1-toolbar--unified/);
    assert.doesNotMatch(monitorRenderer[0], /data-eventkit-region="insights"/);
    assert.match(js, /normalizeEventKitScanEvents/);
    assert.match(js, /filterEventKitUnifiedRows/);
    assert.match(js, /function renderEventKitEngineInline/);
    assert.match(js, /function scheduleEventKitAutoScan\(/);
    assert.match(pageRenderer[0], /scheduleEventKitAutoScan\(\)/);
    assert.match(js, /data-eventkit-engine-select/);
    assert.match(js, /latestStatusRaw/);
    assert.match(js, /projectPath/);

    assert.match(css, /\.eventkit-workbench/);
    assert.doesNotMatch(css, /\.eventkit-engine-strip/);
    assert.match(css, /\.eventkit-engine-inline/);
    assert.match(css, /\.eventkit-monitor-workbench/);
    assert.match(css, /\.eventkit-v1-main/);
    assert.match(css, /\.eventkit-v1-map/);
    assert.match(css, /\.eventkit-v1-side/);
    assert.match(css, /\.eventkit-v1-relation-head/);
    assert.match(css, /\.eventkit-v1-relations/);
    assert.match(css, /\.eventkit-v1-event-column/);
    assert.match(css, /\.eventkit-v1-unregister-stack/);
    assert.match(css, /\.eventkit-v1-event-node/);
    assert.match(css, /\.eventkit-v1-timeline/);
    assert.match(css, /\.eventkit-v1-code-stack/);
    assert.match(css, /\.eventkit-v1-code-link/);
    assert.match(css, /\.content-body--eventkit\s*{[\s\S]*display:\s*flex/);
    assert.match(css, /\.eventkit-hero-actions\s*{[\s\S]*display:\s*flex/);
    assert.match(css, /\.eventkit-hero-actions \.eventkit-scan-search\s*{[\s\S]*flex:\s*1\s+1\s+180px/);
    assert.doesNotMatch(css, /\.eventkit-v1-toolbar--unified\s*\{/);
    assert.match(css, /\.eventkit-v1-row\s*{[\s\S]*grid-template-columns:\s*minmax\(220px,\s*1fr\)\s*minmax\(300px,\s*360px\)\s*minmax\(220px,\s*1fr\)/);
    assert.match(css, /\.eventkit-v1-row\s*{[\s\S]*flex:\s*0 0 auto/);
    assert.match(css, /\.eventkit-v1-row\s*{[\s\S]*min-height:\s*138px/);
    assert.doesNotMatch(css, /\.eventkit-v1-row\s*{[^}]*grid-template-rows:\s*max-content/);
    assert.doesNotMatch(css, /\.eventkit-v1-event-column\s*{[^}]*height:\s*max-content/);
    assert.match(css, /\.eventkit-v1-sidecell--sender \.eventkit-v1-location-list\s*{[\s\S]*justify-items:\s*end/);
    assert.match(css, /\.eventkit-v1-sidecell--receiver \.eventkit-v1-location-list\s*{[\s\S]*justify-items:\s*start/);
    assert.match(css, /\.eventkit-v1-code-stack \.eventkit-v1-location-list\s*{[\s\S]*max-height:\s*none/);
    assert.doesNotMatch(css, /\.eventkit-v1-detail-scroll--registry/);
});

test('EventKit frontend splits data and monitor rendering without adding redundant flow layers', () => {
    const dataModule = 'pages/eventkit-data.js';
    const monitorModule = 'pages/eventkit-monitor.js';
    const pageModule = 'pages/eventkit.js';
    const scripts = getScriptOrder();

    for (const fileName of [dataModule, monitorModule, pageModule]) {
        assert.ok(fs.existsSync(path.join(distDir, fileName)), `${fileName} should exist`);
    }

    const scanOrder = scripts.indexOf('pages/eventkit-scan.js');
    const dataOrder = scripts.indexOf(dataModule);
    const monitorOrder = scripts.indexOf(monitorModule);
    const pageOrder = scripts.indexOf(pageModule);
    assert.ok(scanOrder >= 0 && scanOrder < dataOrder, 'EventKit scan helpers should load before data merge helpers');
    assert.ok(dataOrder >= 0 && dataOrder < monitorOrder, 'EventKit data helpers should load before monitor rendering');
    assert.ok(monitorOrder >= 0 && monitorOrder < pageOrder, 'EventKit monitor renderer should load before the page shell');

    const dataJs = readDistFile(dataModule);
    const monitorJs = readDistFile(monitorModule);
    const pageJs = readDistFile(pageModule);

    for (const [fileName, source] of [[dataModule, dataJs], [monitorModule, monitorJs], [pageModule, pageJs]]) {
        assert.ok(countTextLines(source) <= 500, `${fileName} should stay at or below 500 lines`);
    }

    assert.match(dataJs, /async function fetchEventKitMonitorSnapshot\(/);
    assert.match(dataJs, /function normalizeEventKitMonitorPayload\(/);
    assert.match(dataJs, /function buildEventKitMonitorRows\(/);
    assert.match(monitorJs, /function renderEventKitMonitorHtml\(/);
    assert.match(monitorJs, /function renderEventKitMonitorDetail\(/);
    assert.match(pageJs, /function renderEventKitPage\(/);
    assert.match(pageJs, /async function refreshEventKitReactive\(event\)/);

    assert.doesNotMatch(pageJs, /function normalizeEventKitMonitorPayload\(/);
    assert.doesNotMatch(pageJs, /function renderEventKitMonitorHtml\(/);
    assert.doesNotMatch(dataJs + monitorJs + pageJs, /fetchEventKitMonitorSnapshotFromCommands/);
    assert.doesNotMatch(dataJs + monitorJs + pageJs, /const\s+snapshotState\s*=/);
});

test('EventKit monitor prefers engine-scoped snapshot and falls back to command response', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const eventHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'CommandBridge', 'Handlers', 'EventKitCommandHandler.cs');
    const eventLookup = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'CommandBridge', 'Handlers', 'EventKitCommandHandler.EventLookup.cs');

    assert.match(js, /function fetchEventKitMonitorSnapshot/);
    assert.match(js, /read_snapshot/);
    assert.match(js, /kit:\s*'EventKit'/);
    assert.match(js, /snapshot:\s*'state'/);
    assert.match(js, /action:\s*'get_workbench_snapshot'/);
    assert.match(js, /function canShowStaticKitWorkbench\(kit,\s*engine/);
    assert.match(js, /function createEmptyEventKitRuntimeSnapshot/);
    assert.match(js, /if\s*\(!canSendRuntimeKitCommand\('EventKit'\)\s*&&\s*canShowStaticKitWorkbench\('EventKit',\s*engine\)\)\s*\{\s*return createEmptyEventKitRuntimeSnapshot/);
    assert.match(js, /recentEvents/);
    assert.match(js, /registerKitReactiveRefresh\('eventkit'/);
    assert.match(js, /event_update/);
    assert.match(js, /data-eventkit-region="timeline"/);
    assert.doesNotMatch(js, /data-eventkit-region="registry"/);
    assert.doesNotMatch(js, /代码位置卡片/);
    assert.doesNotMatch(js, /data-eventkit-region="insights"/);
    assert.match(js, /buildEventKitMonitorRows/);
    assert.match(js, /eventKitMonitorRowOrder/);
    assert.match(js, /function orderEventKitMonitorRows/);
    assert.match(js, /renderEventKitMonitorDetail/);
    assert.match(js, /formatEventKitTopologyEmptyMessage/);
    assert.match(js, /hasScanData && scanEvents\.length \? buildEventKitMonitorRows/);
    assert.match(js, /const getExisting = \(channel, key, payloadType\) =>/);
    assert.doesNotMatch(js, /Live \$\{escapeHtml\(row\.handlerCount\)\} handlers/);
    assert.doesNotMatch(js, /运行时监听器/);
    assert.doesNotMatch(js, /当前监听/);
    assert.match(js, /const hasScannedTopology = row\.staticSendCount > 0 \|\|/);
    assert.doesNotMatch(js, /const hasRuntimeTopology = row\.sendCount > 0 \|\| row\.senderLocations\.length > 0 \|\| row\.receiverLocations\.length > 0 \|\| row\.unregisterLocations\.length > 0;/);
    assert.match(js, /t\('eventkit\.auto_scan_preparing'\)/);
    assert.match(js, /eventKitScanCacheKey/);
    assert.match(js, /function captureEventKitScrollState/);
    assert.match(js, /function restoreEventKitScrollState/);
    assert.match(js, /EVENTKIT_SCROLL_SELECTORS/);
    assert.match(js, /let eventKitMonitorRenderSignature\s*=\s*''/);
    assert.match(js, /data-eventkit-workbench="root"/);
    assert.match(js, /data-eventkit-region="monitor"/);
    assert.match(js, /function makeEventKitMonitorSignature\(/);
    assert.match(js, /const nextSignature\s*=\s*makeEventKitMonitorSignature\(\s*data,\s*eventKitScanCache,\s*eventKitScanSearchTerm,\s*selectedEventKitEngineId,\s*selectedEventKitMonitorKey,\s*selectedEventKitScanKey\s*\)/);
    assert.match(js, /if\s*\(nextSignature === eventKitMonitorRenderSignature\)\s*\{/);
    const workbenchRenderer = js.match(/function renderEventKitWorkbench\(\) \{[\s\S]*?\n\}/);
    assert.ok(workbenchRenderer, 'EventKit workbench renderer should be capturable');
    assert.doesNotMatch(workbenchRenderer[0], /renderEventKitMonitorContent\(eventKitMonitorCache\)[\s\S]*?<\/section>/);
    assert.match(js, /eventKitActiveTab !== 'monitor'/);
    assert.match(js, /function updateEventKitMonitorSelection\(/);
    assert.match(js, /updateEventKitMonitorSelection\(value\)/);
    assert.doesNotMatch(js, /selectedEventKitMonitorKey = value;\s*renderEventKitWorkbench\(\)/);
    assert.match(js, /eventKitAnimatedRowKey/);
    assert.match(js, /function prepareEventKitMonitorAnimation/);
    assert.match(js, /function renderEventKitFlightOverlay/);
    assert.match(js, /sourceFile/);
    assert.match(js, /sourceLine/);
    assert.match(js, /formatEventKitPayloadType/);
    assert.match(js, /function renderEventKitPayloadBadge/);
    assert.match(js, /normalizeEventKitChannel\(row\?\.channel\) === 'Type'/);
    assert.match(js, /eventkit-payload-pill/);
    assert.match(js, /payloadType/);
    assert.doesNotMatch(js, /最近发送候选/);
    assert.doesNotMatch(js, /最近发送源/);
    assert.doesNotMatch(js, /<code>\$\{escapeHtml\(group\.filePath\)\}<\/code>/);
    assert.match(css, /\.eventkit-v1-node-badges/);
    assert.match(css, /\.eventkit-v1-detail-badges/);
    assert.match(css, /\.eventkit-payload-pill/);
    assert.doesNotMatch(css, /\.eventkit-v1-node-source/);
    assert.doesNotMatch(css, /\.eventkit-v1-file-group__head code/);
    assert.match(js, /latestSourceLocation/);
    assert.match(css, /\.eventkit-v1-flight/);
    assert.match(css, /@keyframes eventkit-flight-to-hub/);
    assert.match(css, /@keyframes eventkit-flight-to-receiver/);
    assert.match(eventHandler, /get_workbench_snapshot/);
    assert.match(eventHandler, /case "get_workbench_snapshot":/);
    assert.match(eventHandler, /get_event/);
    assert.match(eventHandler, /get_recent_events/);
    assert.match(eventLookup, /runtimeListenerCount/);
});

test('EventKit detail summary and timeline share one natural side card', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const detailTopBlock = extractCssBlock(css, '.eventkit-v1-detail-top');

    assert.match(js, /<section class="eventkit-v1-detail eventkit-v1-detail-card">/);
    assert.match(js, /eventKitStatChip\(t\('eventkit\.stat_cumulative'\),\s*row\.sendCount,\s*'send'\)/);
    assert.match(js, /eventKitStatChip\(t\('eventkit\.stat_recent'\),\s*matchingEvents\.length,\s*'history'\)/);
    assert.match(js, /eventKitStatChip\(t\('eventkit\.stat_static'\),\s*row\.senderLocations\.length \+ row\.receiverLocations\.length \+ row\.unregisterLocations\.length,\s*'handler'\)/);
    assert.match(css, /\.eventkit-v1-detail-card\s*\{[\s\S]*?border:\s*1px solid var\(--hairline\)/);
    assert.match(css, /\.eventkit-v1-detail-card\s*\{[\s\S]*?background:\s*var\(--surface-1\)/);
    assert.match(detailTopBlock, /border-bottom:\s*1px solid var\(--hairline\)/);
    assert.doesNotMatch(detailTopBlock, /border:\s*1px solid var\(--hairline\)/);
    assert.match(css, /\.eventkit-v1-detail-stats\s*\{[\s\S]*?grid-template-columns:\s*repeat\(3,\s*minmax\(0,\s*1fr\)\)/);
    assert.doesNotMatch(css, /\.eventkit-v1-detail-stats\s*\{[\s\S]*?grid-template-columns:\s*repeat\(2,\s*minmax\(0,\s*1fr\)\)/);
    assert.match(css, /\.eventkit-v1-detail-scroll\s*\{[\s\S]*?border:\s*0/);
    assert.match(css, /\.eventkit-v1-detail-scroll\s*\{[\s\S]*?background:\s*transparent/);
    assert.match(css, /\.eventkit-v1-timeline,\s*\n\.eventkit-v1-quick-list\s*\{[\s\S]*?flex:\s*1\s+1\s+auto/);
});

test('EventKit code scan invokes the Rust scanner and renders sender receiver health', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const rust = readTauriSourceFile('src-tauri', 'src', 'main.rs');
    const typeEvent = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'EventKit', 'TypeEvent.cs');
    const editorHook = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'EventKit', 'EasyEventEditorHook.cs');
    const unityBridge = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Editor', 'TauriBridge', 'CommandBridge', 'EventKitEditorDataBridge.cs');

    assert.match(js, /async function runEventKitCodeScan/);
    assert.match(js, /invoke\('scan_eventkit_code'/);
    assert.match(js, /eventKitScanExcludeEditor/);
    assert.match(js, /excludeEditor:\s*eventKitScanExcludeEditor/);
    assert.match(js, /function renderEventKitHeroActions/);
    assert.match(js, /data-eventkit-exclude-editor/);
    assert.match(js, /t\('eventkit\.exclude_editor'\)/);
    assert.match(js, /t\('eventkit\.scan_code'\)/);
    assert.match(js, /open_eventkit_code_location/);
    assert.match(js, /data-eventkit-open-file/);
    assert.match(js, /function openEventKitCodeLocation/);
    assert.match(js, /scan\.unmatchedSendCount/);
    assert.match(js, /scan\.unmatchedRegisterCount/);
    assert.match(js, /deprecatedStringEventCount/);
    assert.match(js, /renderEventKitScanResults/);
    assert.match(js, /eventkit-health--no-receiver/);
    assert.match(js, /eventkit-health--no-sender/);
    assert.match(js, /eventkit-health--leak-risk/);
    assert.match(js, /data-eventkit-scan-search/);
    assert.match(js, /data-eventkit-scan-row/);
    assert.match(js, /function eventKitEventLocations/);
    assert.match(js, /function groupEventKitLocationsByFile/);
    assert.match(js, /function renderEventKitLocationGroup/);
    assert.match(js, /eventkit-v1-file-group/);
    assert.match(js, /eventkit-v1-line-chip/);
    assert.match(js, /sendFiles/);
    assert.match(js, /registerFiles/);
    assert.match(js, /unregisterFiles/);
    assert.match(js, /renderEventKitScanNavigator/);

    assert.match(rust, /fn open_eventkit_code_location/);
    assert.match(rust, /resolve_eventkit_code_location/);
    assert.match(rust, /open_code_location/);
    assert.match(rust, /ensure_path_within_root/);
    assert.match(typeEvent, /#if UNITY_EDITOR \|\| GODOT[\s\S]*CallerFilePath/);
    assert.match(editorHook, /Action<string, string, object, string, int> OnSend/);
    assert.match(unityBridge, /sourceFile/);
    assert.match(unityBridge, /sourceLine/);
    assert.match(unityBridge, /NormalizeSourceFile/);
    assert.match(css, /\.eventkit-v1-file-group/);
    assert.match(css, /\.eventkit-v1-line-chip/);
});

test('FsmKit workbench uses a composite bridge snapshot before falling back to split requests', () => {
    const js = readFrontendScripts();
    const handler = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'CommandBridge', 'Handlers', 'FsmKitCommandHandler.cs');

    assert.match(js, /function fetchFsmWorkbenchSnapshot\(/);
    assert.match(js, /action:\s*'get_workbench_snapshot'/);
    assert.match(js, /const snapshot\s*=\s*await fetchFsmWorkbenchSnapshot\(requestedFsmName\)/);
    assert.match(js, /snapshot\.history/);
    assert.match(js, /Promise\.all\(\[\s*fetchFsmStateDetail\(requestedFsmName\),\s*fetchFsmHistory\(requestedFsmName\)\s*\]\)/);

    assert.match(handler, /get_workbench_snapshot/);
    assert.match(handler, /case "get_workbench_snapshot":/);
    assert.match(handler, /private static string GetWorkbenchSnapshot\(/);
});

test('FsmKit current-state list prefers shared memory telemetry before file snapshot fallback', () => {
    const js = readFrontendScripts();
    const rust = readTauriSourceFile('src-tauri', 'src', 'main.rs');
    const handler = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'CommandBridge', 'Handlers', 'FsmKitCommandHandler.cs');

    assert.match(js, /function fetchKitWorkbenchState\(kit,\s*normalize,\s*options = \{\}\)/);
    assert.match(js, /function fetchFsmList\(/);
    assert.match(js, /function normalizeFsmListPayload\(/);
    assert.match(js, /fetchKitWorkbenchState\('FsmKit',\s*normalizeFsmListPayload/);
    assert.doesNotMatch(js, /skipTelemetry:\s*true,\s*commandAction:\s*'list_all'/);
    assert.match(js, /forceCommandRefresh:\s*true/);
    assert.match(js, /sendKitCommandData\('FsmKit',\s*'get_state'/);
    assert.match(js, /sendKitCommandData\('FsmKit',\s*'get_history'/);
    assert.match(js, /sendKitCommandData\('FsmKit',\s*'get_workbench_snapshot',\s*\{\s*fsmName\s*\}\)/);
    assert.match(handler, /AppendStateDebugFields\(sb,\s*fsm,\s*true\)/);
    assert.doesNotMatch(js, /invoke\('send_command',\s*\{\s*kit:\s*'FsmKit'/);
    assert.doesNotMatch(js, /readKitTelemetryData\('FsmKit'\)/);
    assert.doesNotMatch(js, /readKitSnapshotData\('FsmKit'\)/);
    assert.doesNotMatch(js, /fetchFsmTelemetryList\(/);
    assert.doesNotMatch(js, /fetchFsmSnapshotList\(/);
    assert.doesNotMatch(js, /sendKitCommandData\('FsmKit',\s*'list_all'\)/);

    assert.match(rust, /fn read_telemetry\(/);
    assert.match(rust, /read_telemetry_frame_from_buffer/);
    assert.match(rust, /OpenFileMappingW|shm_open/);
    assert.match(rust, /read_telemetry,/);
});

test('FsmKit frontend splits data workbench and interactions without local bridge wrappers', () => {
    const dataModule = 'pages/fsmkit-data.js';
    const workbenchModule = 'pages/fsmkit-workbench.js';
    const detailModule = 'pages/fsmkit-detail.js';
    const interactionsModule = 'pages/fsmkit-interactions.js';
    const pageModule = 'pages/fsmkit.js';
    const scripts = getScriptOrder();

    for (const fileName of [dataModule, workbenchModule, detailModule, interactionsModule, pageModule]) {
        assert.ok(fs.existsSync(path.join(distDir, fileName)), `${fileName} should exist`);
    }

    const graphOrder = scripts.indexOf('pages/fsmkit-graph.js');
    const dataOrder = scripts.indexOf(dataModule);
    const workbenchOrder = scripts.indexOf(workbenchModule);
    const detailOrder = scripts.indexOf(detailModule);
    const interactionsOrder = scripts.indexOf(interactionsModule);
    const pageOrder = scripts.indexOf(pageModule);
    assert.ok(graphOrder >= 0 && graphOrder < dataOrder, 'FsmKit graph helpers should load before data/workbench helpers');
    assert.ok(dataOrder >= 0 && dataOrder < workbenchOrder, 'FsmKit data helpers should load before workbench rendering');
    assert.ok(workbenchOrder >= 0 && workbenchOrder < detailOrder, 'FsmKit workbench shell should load before detail helpers');
    assert.ok(detailOrder >= 0 && detailOrder < interactionsOrder, 'FsmKit detail helpers should load before interactions');
    assert.ok(interactionsOrder >= 0 && interactionsOrder < pageOrder, 'FsmKit interactions should load before the page shell');

    const dataJs = readDistFile(dataModule);
    const workbenchJs = readDistFile(workbenchModule);
    const detailJs = readDistFile(detailModule);
    const interactionsJs = readDistFile(interactionsModule);
    const pageJs = readDistFile(pageModule);
    const graphJs = readDistFile('pages/fsmkit-graph.js');

    for (const [fileName, source] of [[dataModule, dataJs], [workbenchModule, workbenchJs], [detailModule, detailJs], [interactionsModule, interactionsJs], [pageModule, pageJs]]) {
        assert.ok(countTextLines(source) <= 500, `${fileName} should stay at or below 500 lines`);
    }

    assert.match(dataJs, /async function fetchFsmList\(/);
    assert.match(dataJs, /async function fetchFsmWorkbenchSnapshot\(/);
    assert.match(workbenchJs, /function renderFsmWorkbenchShell\(/);
    assert.match(workbenchJs, /function renderFsmDetailRegionHtml\(/);
    assert.match(detailJs, /function renderFsmInsightsHtml\(/);
    assert.match(detailJs, /function scheduleFsmCurrentStateFit\(/);
    assert.match(interactionsJs, /function selectFsmWorkbench\(/);
    assert.match(interactionsJs, /function applyFsmWorkbenchSearch\(/);
    assert.match(pageJs, /function renderFsmKitPage\(/);
    assert.match(pageJs, /async function refreshFsmKitReactive\(event\)/);

    assert.doesNotMatch(pageJs, /function renderFsmWorkbenchShell\(/);
    assert.doesNotMatch(pageJs, /async function fetchFsmList\(/);
    const combined = dataJs + workbenchJs + detailJs + interactionsJs + pageJs + graphJs;
    assert.doesNotMatch(combined, /fetchFsm.*FromCommands/);
    assert.doesNotMatch(combined, /const\s+snapshotState\s*=/);
    assert.doesNotMatch(combined, /function activateFsmTab\(/);
    assert.doesNotMatch(combined, /async function loadFsmGraph\(/);
    assert.doesNotMatch(combined, /async function loadFsmList\(/);
    assert.doesNotMatch(combined, /async function loadFsmState\(/);
    assert.doesNotMatch(combined, /async function loadFsmHistory\(/);
    assert.doesNotMatch(combined, /function fsmSelector\(/);
});

test('runtime Kit state pages delegate command fallback to the shared kit bridge', () => {
    const bridge = readDistFile('shared/kit-bridge.js');
    const pages = [
        ['architecture.js', 'Architecture', 'fetchArchitectureWorkbenchState'],
        ['actionkit.js', 'ActionKit', 'fetchActionKitWorkbenchState'],
        ['poolkit.js', 'PoolKit', 'fetchPoolKitWorkbenchState'],
        ['reskit.js', 'ResKit', 'fetchResKitWorkbenchState'],
        ['singletonkit.js', 'SingletonKit', 'fetchSingletonKitWorkbenchState'],
        ['logkit.js', 'LogKit', 'fetchLogKitWorkbenchState'],
        ['audiokit.js', 'AudioKit', 'fetchAudioKitWorkbenchState'],
        ['savekit.js', 'SaveKit', 'fetchSaveKitWorkbenchState'],
        ['localizationkit.js', 'LocalizationKit', 'fetchLocalizationKitWorkbenchState'],
        ['scenekit.js', 'SceneKit', 'fetchSceneKitWorkbenchState'],
        ['spatialkit.js', 'SpatialKit', 'fetchSpatialKitWorkbenchState'],
        ['uikit.js', 'UIKit', 'fetchUIKitWorkbenchState'],
    ];

    assert.match(bridge, /async function fetchKitWorkbenchState\(kit,\s*normalize,\s*options = \{\}\)/);
    assert.match(bridge, /data\s*=\s*await readKitTelemetryData\(kit,\s*telemetryName\)/);
    assert.match(bridge, /data\s*=\s*await readKitSnapshotData\(kit,\s*snapshotName\)/);
    assert.match(bridge, /data\s*=\s*await sendKitCommandData\(kit,\s*commandAction,\s*commandPayload\)/);

    for (const [fileName, kit, fetchName] of pages) {
        const source = readPageSource(fileName);
        assert.match(source, new RegExp(`function ${fetchName}\\(`), `${fileName} should keep one page-local state fetcher`);
        assert.match(source, new RegExp(`fetchKitWorkbenchState\\('${kit}',`), `${fileName} should call the shared state bridge`);
        assert.doesNotMatch(source, /WorkbenchStateFromCommands/, `${fileName} should not keep a duplicate command-only state wrapper`);
        assert.doesNotMatch(source, /const\s+snapshotState\s*=/, `${fileName} should not split shared bridge fallback into two local reads`);
        assert.doesNotMatch(source, /snapshotState\s*\?\?\s*await\s*fetch[A-Za-z]+WorkbenchStateFromCommands/, `${fileName} should not re-run command fallback after shared fetch`);
    }
});

test('PoolKit ResKit SingletonKit LogKit AudioKit SaveKit LocalizationKit SceneKit SpatialKit and UIKit prefer telemetry snapshots before command fallback', () => {
    const js = readFrontendScripts();
    const rust = readTauriSourceFile('src-tauri', 'src', 'main.rs');
    const publisher = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Editor', 'TauriBridge', 'CommandBridge', 'KitStateSnapshotPublisher.cs');
    const host = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Editor', 'CommandBridge', 'UnityCommandBridgeHost.cs');
    const hostHeartbeat = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Editor', 'CommandBridge', 'UnityCommandBridgeHost.Heartbeat.cs');
    const godotHost = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Godot', 'Runtime', 'Core', 'CommandBridge', 'Runtime', 'GodotCommandBridgeHost.cs');
    const godotPublisher = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Godot', 'Runtime', 'Core', 'CommandBridge', 'Runtime', 'GodotKitStateSnapshotPublisher.cs');
    const sharedTelemetry = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Editor', 'TauriBridge', 'CommandBridge', 'SharedMemoryTelemetry.cs');
    const unityTelemetry = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Editor', 'TauriBridge', 'CommandBridge', 'UnitySharedMemoryTelemetry.cs');
    const poolHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'CommandBridge', 'Handlers', 'PoolKitCommandHandler.cs');
    const logHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'CommandBridge', 'Handlers', 'LogKitCommandHandler.cs');
    const runtimeSettings = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Runtime', 'Core', 'Settings', 'Runtime', 'YokiFrameRuntimeSettings.cs');
    const audioHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'AudioKit', 'Runtime', 'CommandBridge', 'AudioKitCommandHandler.cs');
    const saveHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'SaveKit', 'Runtime', 'CommandBridge', 'SaveKitCommandHandler.cs');
    assert.ok(
        workspaceFileExists('Assets', 'YokiFrame', 'Tools', 'LocalizationKit', 'Runtime', 'CommandBridge', 'LocalizationKitCommandHandler.cs'),
        'LocalizationKit should provide a Runtime command bridge handler'
    );
    assert.ok(
        workspaceFileExists('Assets', 'YokiFrame', 'Tools', 'SceneKit', 'Runtime', 'CommandBridge', 'SceneKitCommandHandler.cs'),
        'SceneKit should provide a Runtime command bridge handler'
    );
    assert.ok(
        workspaceFileExists('Assets', 'YokiFrame', 'Tools', 'SpatialKit', 'Runtime', 'CommandBridge', 'SpatialKitCommandHandler.cs'),
        'SpatialKit should provide a Runtime command bridge handler'
    );
    assert.ok(
        workspaceFileExists('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Runtime', 'CommandBridge', 'UIKitCommandHandler.cs'),
        'UIKit should provide a Runtime command bridge handler'
    );
    const localizationHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'LocalizationKit', 'Runtime', 'CommandBridge', 'LocalizationKitCommandHandler.cs');
    const sceneHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'SceneKit', 'Runtime', 'CommandBridge', 'SceneKitCommandHandler.cs');
    const spatialHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'SpatialKit', 'Runtime', 'CommandBridge', 'SpatialKitCommandHandler.cs');
    const uiHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Runtime', 'CommandBridge', 'UIKitCommandHandler.cs');

    assert.match(js, /function fetchKitWorkbenchState\(kit,\s*normalize,\s*options = \{\}\)/);
    assert.match(js, /function canSendRuntimeKitCommand\(kit\)/);
    assert.match(js, /function showRuntimeKitUnavailable\(kit,\s*label/);
    assert.match(js, /t\('runtime\.need_bridge'/);
    assert.match(js, /t\('runtime\.need_bridge_short'/);
    assert.match(js, /return engineSupportsCapability\(engine,\s*'commands'\)\s*&&\s*engineSupportsCapability\(engine,\s*'snapshots'\)/);
    assert.match(js, /if\s*\(!canSendRuntimeKitCommand\(kit\)\)\s*\{\s*throw new Error\(runtimeKitUnavailableMessage\(kit\)\);\s*\}/);
    assert.match(js, /function getPreferredEngineId\(options = \{\}\)/);
    assert.match(js, /function engineSupportsCapability\(engine,\s*capability\)/);
    assert.match(js, /getPreferredEngineId\(\{\s*capability:\s*'telemetry'\s*\}\)/);
    assert.match(js, /getPreferredEngineId\(\{\s*capability:\s*'snapshots'\s*\}\)/);
    assert.match(js, /function fetchPoolKitWorkbenchState\(\{\s*forceCommandRefresh\s*=\s*false\s*\}\s*=\s*\{\}\)/);
    assert.match(js, /fetchKitWorkbenchState\('PoolKit',\s*normalizePoolKitStatePayload,\s*\{\s*forceCommandRefresh:\s*forceCommandRefresh/);
    assert.match(js, /details:\s*Array\.isArray\(source\.details\)/);
    assert.match(js, /function findPoolKitSnapshotDetail\(poolName\)/);
    assert.match(js, /const snapshotDetail\s*=\s*findPoolKitSnapshotDetail\(selectedPool\.name\)/);
    assert.doesNotMatch(js, /fetchPoolKitWorkbenchStateFromCommands/);
    assert.match(js, /function fetchResKitWorkbenchState\(\)/);
    assert.match(js, /fetchKitWorkbenchState\('ResKit',\s*normalizeResKitStatePayload/);
    assert.doesNotMatch(js, /fetchResKitWorkbenchStateFromCommands/);
    assert.match(js, /function fetchSingletonKitWorkbenchState\(\)/);
    assert.match(js, /fetchKitWorkbenchState\('SingletonKit',\s*normalizeSingletonKitStatePayload/);
    assert.doesNotMatch(js, /fetchSingletonKitWorkbenchStateFromCommands/);
    assert.match(js, /function fetchAudioKitWorkbenchState\(\)/);
    assert.match(js, /fetchKitWorkbenchState\('AudioKit',\s*normalizeAudioKitStatePayload/);
    assert.doesNotMatch(js, /fetchAudioKitWorkbenchStateFromCommands/);
    assert.match(js, /function renderLogKitPage\(/);
    assert.match(js, /function fetchLogKitWorkbenchState\(\{\s*forceCommandRefresh\s*=\s*false\s*\}\s*=\s*\{\}\)/);
    assert.match(js, /fetchKitWorkbenchState\('LogKit',\s*normalizeLogKitStatePayload/);
    assert.doesNotMatch(js, /fetchLogKitWorkbenchStateFromCommands/);
    assert.match(js, /sendKitCommandData\('LogKit',\s*'set_settings'/);
    assert.match(js, /sendKitCommandData\('LogKit',\s*'open_log_folder'/);
    assert.match(js, /sendKitCommandData\('LogKit',\s*'decrypt_log_file'/);
    assert.match(js, /data-logkit-setting="saveLogInEditor"/);
    assert.match(js, /data-logkit-setting="saveLogInPlayer"/);
    assert.match(js, /data-logkit-setting="enableEncryption"/);
    assert.match(js, /data-logkit-setting="enableIMGUIInPlayer"/);
    assert.match(js, /data-logkit-open-folder/);
    assert.match(js, /data-logkit-pick-encrypted/);
    assert.match(js, /sendKitCommandData\('LogKit',\s*'read_log_file'/);
    assert.match(js, /function fetchSaveKitWorkbenchState\(\)/);
    assert.match(js, /fetchKitWorkbenchState\('SaveKit',\s*normalizeSaveKitStatePayload/);
    assert.doesNotMatch(js, /fetchSaveKitWorkbenchStateFromCommands/);
    assert.match(js, /sendKitCommandData\('SaveKit',\s*'delete_slot'/);
    assert.match(js, /sendKitCommandData\('SaveKit',\s*'disable_auto_save'/);
    assert.match(js, /function fetchLocalizationKitWorkbenchState\(\)/);
    assert.match(js, /fetchKitWorkbenchState\('LocalizationKit',\s*normalizeLocalizationKitStatePayload/);
    assert.doesNotMatch(js, /fetchLocalizationKitWorkbenchStateFromCommands/);
    assert.match(js, /sendKitCommandData\('LocalizationKit',\s*'set_language'/);
    assert.match(js, /function fetchSceneKitWorkbenchState\(\)/);
    assert.match(js, /fetchKitWorkbenchState\('SceneKit',\s*normalizeSceneKitStatePayload/);
    assert.doesNotMatch(js, /fetchSceneKitWorkbenchStateFromCommands/);
    assert.match(js, /sendKitCommandData\('SceneKit',\s*'unload_scene'/);
    assert.match(js, /function fetchSpatialKitWorkbenchState\(\)/);
    assert.match(js, /fetchKitWorkbenchState\('SpatialKit',\s*normalizeSpatialKitStatePayload/);
    assert.doesNotMatch(js, /fetchSpatialKitWorkbenchStateFromCommands/);
    assert.doesNotMatch(js, /sendKitCommandData\('SpatialKit',\s*'(insert|update|remove)/);
    assert.match(js, /function fetchUIKitWorkbenchState\(\)/);
    assert.match(js, /fetchKitWorkbenchState\('UIKit',\s*normalizeUIKitStatePayload/);
    assert.doesNotMatch(js, /fetchUIKitWorkbenchStateFromCommands/);
    assert.doesNotMatch(js, /sendKitCommandData\('UIKit',\s*'(open|close|show|hide|push|pop)/);
    assert.doesNotMatch(js, /readKitTelemetryData\('PoolKit'\)/);
    assert.doesNotMatch(js, /readKitSnapshotData\('PoolKit'\)/);
    assert.match(js, /await loadPoolWorkbench\(\{\s*forceCommandRefresh:\s*true,\s*forceDetailRefresh:\s*true\s*\}\)/);
    assert.match(js, /trackingEnabled:\s*nextStack\s*\?\s*true\s*:\s*!!poolKitState\.stats\?\.trackingEnabled/);
    assert.match(js, /eventHistoryEnabled:\s*nextStack\s*\?\s*true\s*:\s*!!poolKitState\.stats\?\.eventHistoryEnabled/);
    assert.match(js, /registerKitReactiveRefresh\('poolkit'/);
    assert.match(js, /registerKitReactiveRefresh\('reskit'/);
    assert.match(js, /registerKitReactiveRefresh\('singletonkit'/);
    assert.match(js, /registerKitReactiveRefresh\('logkit'/);
    assert.match(js, /registerKitReactiveRefresh\('audiokit'/);
    assert.doesNotMatch(js, /registerKitReactiveRefresh\('buffkit'/);
    assert.match(js, /registerKitReactiveRefresh\('savekit'/);
    assert.match(js, /registerKitReactiveRefresh\('localizationkit'/);
    assert.match(js, /registerKitReactiveRefresh\('scenekit'/);
    assert.match(js, /registerKitReactiveRefresh\('spatialkit'/);
    assert.match(js, /registerKitReactiveRefresh\('uikit'/);
    assert.match(js, /data-poolkit-search/);
    assert.match(js, /data-reskit-search/);
    assert.match(js, /data-singletonkit-search/);
    assert.match(js, /data-logkit-search/);
    assert.match(js, /data-audiokit-search/);
    assert.doesNotMatch(js, /data-buffkit-search/);
    assert.match(js, /data-savekit-search/);
    assert.match(js, /data-localizationkit-search/);
    assert.match(js, /data-scenekit-search/);
    assert.match(js, /data-spatialkit-search/);
    assert.match(js, /data-uikit-search/);

    assert.match(rust, /kit:\s*"PoolKit"/);
    assert.match(rust, /kit:\s*"ResKit"/);
    assert.match(rust, /kit:\s*"SingletonKit"/);
    assert.match(rust, /kit:\s*"AudioKit"/);
    assert.doesNotMatch(rust, /kit:\s*"BuffKit"/);
    assert.match(rust, /kit:\s*"SaveKit"/);
    assert.match(rust, /kit:\s*"LocalizationKit"/);
    assert.match(rust, /kit:\s*"SceneKit"/);
    assert.match(rust, /kit:\s*"SpatialKit"/);
    assert.match(rust, /kit:\s*"UIKit"/);

    assert.match(publisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*POOL_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(publisher, /POOL_TRACKING_PREF_KEY/);
    assert.match(publisher, /RestorePoolMonitorPreferences\(\)/);
    assert.match(publisher, /RestoreAndPublishPoolMonitorPreferences\(string yokiframeRoot\)/);
    assert.match(publisher, /ApplyPoolTrackingCommand\(string payloadJson\)/);
    assert.match(publisher, /PublishIfChanged\(GetDefaultYokiframeRoot\(\),\s*POOL_KIT_NAME,\s*sPoolPublisher,\s*BuildPoolPayloadJson,\s*ref sLastPoolPayloadJson,\s*true\)/);
    assert.match(publisher, /PoolDebugger\.ClearRuntimeMonitorState\(\)/);
    assert.match(publisher, /PlayModeStateChange\.EnteredPlayMode/);
    assert.match(publisher, /PlayModeStateChange\.ExitingPlayMode/);
    assert.match(publisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*RES_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(publisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*SINGLETON_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(publisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*LOG_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(publisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*AUDIO_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.doesNotMatch(publisher, /BUFF_KIT_NAME/);
    assert.doesNotMatch(publisher, /BUFF_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(publisher, /SAVE_KIT_NAME/);
    assert.match(publisher, /SAVE_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(publisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*SAVE_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(publisher, /LOCALIZATION_KIT_NAME/);
    assert.match(publisher, /LOCALIZATION_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(publisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*LOCALIZATION_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(publisher, /SCENE_KIT_NAME/);
    assert.match(publisher, /SCENE_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(publisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*SCENE_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(publisher, /SPATIAL_KIT_NAME/);
    assert.match(publisher, /SPATIAL_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(publisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*SPATIAL_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(publisher, /UI_KIT_NAME/);
    assert.match(publisher, /UI_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(publisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*UI_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(publisher, /OptionalKitCommandHandlerRegistry\.TryCreate/);
    assert.match(publisher, /UnitySharedMemoryTelemetry\.TryWriteLatest\(ENGINE_ID,\s*kitName,\s*SNAPSHOT_NAME,\s*payloadJson\)/);
    assert.match(sharedTelemetry, /public static class AdapterSharedMemoryTelemetry/);
    assert.match(sharedTelemetry, /MemoryMappedFile\.CreateOrOpen/);
    assert.match(unityTelemetry, /AdapterSharedMemoryTelemetry\.TryWriteLatest/);
    assert.doesNotMatch(unityTelemetry, /MemoryMappedFile\.CreateOrOpen/);
    assert.match(publisher, /HandleAction\("get_workbench_snapshot",\s*"\{\}"\)/);
    assert.doesNotMatch(publisher, /HandleAction\("list_resources",\s*"\{\}"\)/);
    assert.doesNotMatch(publisher, /HandleAction\("list_singletons",\s*"\{\}"\)/);
    assert.doesNotMatch(publisher, /HandleAction\("list_voices",\s*"\{\}"\)/);
    assert.match(host, /KitStateSnapshotPublisher\.TryPublishAll\(sYokiframeRoot\)/);
    assert.match(host, /KitStateSnapshotPublisher\.RestoreAndPublishPoolMonitorPreferences\(sYokiframeRoot\)/);
    assert.match(host, /Dispatcher\.Register\(new UnityPoolKitCommandHandler\(\)\)/);
    assert.match(host, /UnityRuntimeKitSettingsStore/);
    assert.doesNotMatch(host, /BUFFKIT_COMMAND_HANDLER_TYPE/);
    assert.match(host, /SAVEKIT_COMMAND_HANDLER_TYPE/);
    assert.match(host, /LOCALIZATIONKIT_COMMAND_HANDLER_TYPE/);
    assert.match(host, /SCENEKIT_COMMAND_HANDLER_TYPE/);
    assert.match(host, /SPATIALKIT_COMMAND_HANDLER_TYPE/);
    assert.match(host, /UIKIT_COMMAND_HANDLER_TYPE/);
    assert.match(host, /OptionalKitCommandHandlerRegistry\.TryRegister/);
    assert.match(hostHeartbeat, /\\\"snapshots\\\",\\\"telemetry\\\"/);
    assert.match(godotHost, /LOCALIZATION_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(godotHost, /OptionalKitCommandHandlerRegistry\.TryRegister\(mDispatcher,\s*LOCALIZATION_KIT_COMMAND_HANDLER_TYPE\)/);
    assert.match(godotHost, /SCENE_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(godotHost, /OptionalKitCommandHandlerRegistry\.TryRegister\(mDispatcher,\s*SCENE_KIT_COMMAND_HANDLER_TYPE\)/);
    assert.match(godotHost, /SPATIAL_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(godotHost, /OptionalKitCommandHandlerRegistry\.TryRegister\(mDispatcher,\s*SPATIAL_KIT_COMMAND_HANDLER_TYPE\)/);
    assert.match(godotHost, /\\\"snapshots\\\",\\\"telemetry\\\"/);
    assert.match(godotPublisher, /LOCALIZATION_KIT_NAME/);
    assert.match(godotPublisher, /LOCALIZATION_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(godotPublisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*LOCALIZATION_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(godotPublisher, /SCENE_KIT_NAME/);
    assert.match(godotPublisher, /SCENE_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(godotPublisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*SCENE_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(godotPublisher, /scene_update/);
    assert.match(godotPublisher, /SPATIAL_KIT_NAME/);
    assert.match(godotPublisher, /SPATIAL_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(godotPublisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*SPATIAL_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(godotPublisher, /spatial_update/);
    assert.match(godotPublisher, /AdapterSharedMemoryTelemetry\.TryWriteLatest/);
    assert.match(poolHandler, /"get_workbench_snapshot"/);
    assert.match(poolHandler, /private static string GetWorkbenchSnapshot\(/);
    assert.match(poolHandler, /if\s*\(stackTraceEnabled\)/);
    assert.match(poolHandler, /\\\"details\\\":/);
    assert.match(poolHandler, /MAX_SNAPSHOT_ACTIVE_OBJECTS_PER_POOL/);
    assert.match(poolHandler, /activeObjectTruncated/);
    assert.match(poolHandler, /inactiveObjectTotal/);
    assert.match(poolHandler, /inactiveObjects/);
    assert.match(logHandler, /"get_settings"/);
    assert.match(logHandler, /"set_settings"/);
    assert.match(logHandler, /"get_workbench_snapshot"/);
    assert.match(logHandler, /settings/);
    assert.match(runtimeSettings, /RESOURCES_PATH\s*=\s*"YokiFrameRuntimeSettings"/);
    assert.match(runtimeSettings, /CreateLogKitOptions/);
    assert.match(runtimeSettings, /SetValue\(string kit,\s*string key,\s*string value\)/);
    assert.match(audioHandler, /"get_workbench_snapshot"/);
    assert.match(audioHandler, /private static string GetWorkbenchSnapshot\(/);
    assert.match(audioHandler, /stats/);
    assert.match(audioHandler, /voices/);
    assert.match(audioHandler, /history/);
    assert.match(saveHandler, /"get_workbench_snapshot"/);
    assert.match(saveHandler, /"delete_slot"/);
    assert.match(saveHandler, /"disable_auto_save"/);
    assert.match(saveHandler, /private static string BuildWorkbenchSnapshotJson\(/);
    assert.match(saveHandler, /BuildStatsJson/);
    assert.match(saveHandler, /BuildSlotsJson/);
    assert.match(saveHandler, /BuildAutoSaveJson/);
    assert.match(localizationHandler, /"get_workbench_snapshot"/);
    assert.match(localizationHandler, /"set_language"/);
    assert.match(localizationHandler, /private static string BuildWorkbenchSnapshotJson\(/);
    assert.match(localizationHandler, /BuildStatsJson/);
    assert.match(localizationHandler, /BuildLanguagesJson/);
    assert.match(sceneHandler, /"get_workbench_snapshot"/);
    assert.match(sceneHandler, /"unload_scene"/);
    assert.match(sceneHandler, /BuildWorkbenchSnapshotJson/);
    assert.match(sceneHandler, /BuildStatsJson/);
    assert.match(sceneHandler, /BuildScenesJson/);
    assert.match(spatialHandler, /"get_workbench_snapshot"/);
    assert.match(spatialHandler, /"list_indexes"/);
    assert.match(spatialHandler, /BuildWorkbenchSnapshotJson/);
    assert.match(spatialHandler, /BuildStatsJson/);
    assert.match(spatialHandler, /BuildIndexesJson/);
    assert.match(uiHandler, /"get_workbench_snapshot"/);
    assert.match(uiHandler, /"list_panels"/);
    assert.match(uiHandler, /"list_stacks"/);
    assert.match(uiHandler, /BuildWorkbenchSnapshotJson/);
    assert.match(uiHandler, /BuildStatsJson/);
    assert.match(uiHandler, /BuildPanelsJson/);
    assert.match(uiHandler, /BuildStacksJson/);
});

test('AudioKit workbench restores the 1.0 mixer rail with horizontally scrollable bus strips', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const audioHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'AudioKit', 'Runtime', 'CommandBridge', 'AudioKitCommandHandler.cs');
    const audioKitRuntime = [
        'AudioKit.cs',
        'AudioKit.Volume.cs',
        'AudioKit.Control.cs',
    ].map(fileName => readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'AudioKit', 'Runtime', 'Public', fileName)).join('\n');

    const start = js.indexOf('function renderAudioKitWorkbench(');
    const end = js.indexOf('function filterAudioKitVoices', start);
    assert.ok(start >= 0 && end > start, 'AudioKit renderer should be present');
    const renderer = js.slice(start, end);
    const audioChannelStripRule = css.match(/\.audio-channel-strip\s*\{([\s\S]*?)\n\}/)?.[1] ?? '';

    assert.match(js, /buses:\s*Array\.isArray\(source\.buses\)/);
    assert.match(renderer, /audio-mixer-console/);
    assert.match(renderer, /audio-channel-rail/);
    assert.match(renderer, /audio-channel-strip/);
    assert.match(renderer, /audio-strip-fader/);
    assert.match(renderer, /audio-strip-playback/);
    assert.match(renderer, /audio-strip-history/);
    assert.match(renderer, /data-audiokit-channel-strip/);
    assert.match(renderer, /data-audiokit-master-volume/);
    assert.match(renderer, /data-audiokit-bus-volume/);
    assert.match(renderer, /data-audiokit-bus-mute/);
    assert.match(renderer, /data-audiokit-stop-bus/);
    assert.doesNotMatch(renderer, /kit-workbench-grid--audio/);
    assert.doesNotMatch(renderer, /audio-console-summary/);
    assert.doesNotMatch(renderer, /audio-bus-table/);
    assert.doesNotMatch(renderer, /audio-voice-panel/);
    assert.doesNotMatch(renderer, /audio-history-panel/);
    assert.doesNotMatch(renderer, /audio-mixer-shell/);

    assert.match(js, /function renderAudioChannelStrips\(/);
    assert.match(js, /function setAudioKitBusVolume\(/);
    assert.match(js, /function muteAudioKitBus\(/);
    assert.match(js, /function stopAudioKitBus\(/);
    assert.match(js, /function renderAudioStripPlayback\(/);
    assert.match(js, /function renderAudioStripHistory\(/);
    assert.match(js, /function renderAudioStripHistoryItem\(/);
    assert.match(js, /function formatAudioHistoryTitle\(/);
    assert.match(js, /function formatAudioHistoryMeta\(/);
    assert.match(js, /function formatAudioHistoryClock\(/);
    assert.match(js, /function formatAudioCompactPath\(/);
    assert.match(js, /function formatAudioProgressPercent\(/);
    assert.match(js, /function getAudioVoiceProgressRatio\(/);
    assert.match(js, /const AUDIO_STRIP_DETAIL_LIMIT\s*=\s*12/);
    assert.match(js, /audio-strip-playback-item/);
    assert.match(js, /audio-strip-history-item/);
    assert.match(js, /audio-strip-history-badge/);
    assert.match(js, /audio-strip-history-title/);
    assert.match(js, /audio-strip-history-meta/);
    assert.match(js, /audio-strip-history-path/);
    assert.match(js, /audio-strip-progress/);
    assert.match(js, /formatAudioProgressPercent\(voice\)/);
    assert.match(js, /formatAudioHistoryMeta\(item\)/);
    assert.match(js, /formatAudioCompactPath\(item\.path\)/);
    assert.doesNotMatch(js, /voices\.slice\(0,\s*3\)/);
    assert.doesNotMatch(js, /history\.slice\(0,\s*3\)/);
    assert.match(js, /sendKitCommandData\('AudioKit',\s*'set_master_volume'/);
    assert.match(js, /sendKitCommandData\('AudioKit',\s*'set_bus_volume'/);
    assert.match(js, /sendKitCommandData\('AudioKit',\s*'mute_bus'/);
    assert.match(js, /sendKitCommandData\('AudioKit',\s*'stop_bus'/);

    assert.match(css, /\.audio-mixer-console/);
    assert.match(css, /\.audio-channel-rail/);
    assert.match(css, /overflow-x:\s*auto/);
    assert.match(css, /\.audio-channel-strip/);
    assert.match(css, /\.audio-strip-fader/);
    assert.match(css, /\.audio-strip-playback/);
    assert.match(css, /\.audio-strip-history/);
    assert.match(css, /\.audio-master-strip/);
    assert.match(css, /\.kit-workbench--audio\s*\{[\s\S]*?height:\s*100%/);
    assert.match(css, /\.audio-mixer-console\s*\{[\s\S]*?flex:\s*1\s+1\s+auto/);
    assert.match(css, /\.audio-mixer-console\s*\{[\s\S]*?grid-template-rows:\s*auto\s+minmax\(0,\s*1fr\)/);
    assert.match(css, /\.audio-channel-rail\s*\{[\s\S]*?height:\s*100%/);
    assert.match(css, /\.audio-channel-rail\s*\{[\s\S]*?grid-auto-columns:\s*minmax\(282px,\s*360px\)/);
    assert.match(css, /\.audio-channel-rail\s*\{[\s\S]*?grid-auto-rows:\s*auto/);
    assert.match(css, /\.audio-channel-rail\s*\{[\s\S]*?overflow-y:\s*auto/);
    assert.match(css, /\.audio-channel-strip\s*\{[\s\S]*?height:\s*var\(--audio-strip-card-height\)/);
    assert.match(css, /\.audio-channel-strip\s*\{[\s\S]*?min-height:\s*var\(--audio-strip-card-height\)/);
    assert.match(css, /--audio-strip-card-height:\s*820px/);
    assert.match(css, /--audio-strip-control-height:\s*204px/);
    assert.match(css, /--audio-strip-playback-height:\s*180px/);
    assert.match(css, /--audio-strip-history-height:\s*180px/);
    assert.match(css, /\.audio-channel-strip\s*\{[\s\S]*?grid-template-rows:\s*auto\s+auto\s+var\(--audio-strip-control-height\)\s+auto\s+minmax\(var\(--audio-strip-playback-height\),\s*1fr\)\s+minmax\(var\(--audio-strip-history-height\),\s*1fr\)/);
    assert.doesNotMatch(audioChannelStripRule, /height:\s*100%/);
    assert.doesNotMatch(css, /\.audio-channel-strip\s*\{[\s\S]*?grid-template-rows:[\s\S]*?minmax\(176px,\s*1fr\)/);
    assert.match(css, /\.audio-strip-playback,\s*\n\.audio-strip-history\s*\{[\s\S]*?min-height:\s*0/);
    assert.match(css, /\.audio-strip-playback,\s*\n\.audio-strip-history\s*\{[\s\S]*?overflow-y:\s*auto/);
    assert.match(css, /\.audio-strip-playback\s*>\s*span,\s*\n\.audio-strip-history\s*>\s*span\s*\{[\s\S]*?flex:\s*0\s+0\s+auto/);
    assert.match(css, /\.audio-strip-playback-item/);
    assert.match(css, /\.audio-strip-history-item/);
    assert.match(css, /\.audio-strip-history-badge/);
    assert.match(css, /\.audio-strip-history-main/);
    assert.match(css, /\.audio-strip-history-title/);
    assert.match(css, /\.audio-strip-history-meta/);
    assert.match(css, /\.audio-strip-history-path/);
    assert.match(css, /\.audio-strip-history-item\s*\{[\s\S]*?grid-template-columns:\s*auto\s+minmax\(0,\s*1fr\)/);
    assert.match(css, /\.audio-strip-history-item\s*\{[\s\S]*?text-align:\s*left/);
    assert.match(css, /\.audio-strip-history-title\s*\{[\s\S]*?white-space:\s*nowrap/);
    assert.match(css, /\.audio-strip-progress/);
    assert.match(css, /\.audio-strip-progress\s+b/);
    assert.doesNotMatch(css, /\.audio-console-summary/);
    assert.doesNotMatch(css, /\.audio-voice-panel/);
    assert.doesNotMatch(css, /\.audio-history-panel/);

    assert.match(audioKitRuntime, /public static void SetBusVolume\(string bus, float volume\)/);
    assert.match(audioKitRuntime, /public static float GetBusVolume\(string bus\)/);
    assert.match(audioKitRuntime, /public static void MuteBus\(string bus, bool mute\)/);
    assert.match(audioKitRuntime, /public static void StopBus\(string bus\)/);
    assert.match(audioHandler, /"set_master_volume"/);
    assert.match(audioHandler, /"set_bus_volume"/);
    assert.match(audioHandler, /"mute_bus"/);
    assert.match(audioHandler, /"stop_bus"/);
    assert.match(audioHandler, /\\"buses\\"/);
});

test('AudioKit code generator is hosted by Tauri instead of Unity Editor windows', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /AUDIOKIT_INDEX_DEFAULT_CONFIG/);
    assert.match(js, /function renderAudioKitIndexGenerator/);
    assert.match(js, /renderAudioKitIndexTextField\('scanFolder'/);
    assert.match(js, /renderAudioKitIndexTextField\('outputPath'/);
    assert.match(js, /data-audiokit-index-field="\$\{field\}"/);
    assert.match(js, /data-audiokit-index-action="scan"/);
    assert.match(js, /data-audiokit-index-action="generate"/);
    assert.match(js, /invoke\('audiokit_scan_audio_files'/);
    assert.match(js, /invoke\('audiokit_generate_audio_ids'/);
    assert.match(js, /AudioPaths\.Map/);
    assert.match(css, /\.audiokit-index-generator/);
    assert.match(css, /\.audiokit-index-preview/);

    assert.equal(
        workspaceFileExists('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Runtime', 'Tool', 'AudioKit', 'Editor', 'AudioKitIndexGeneratorWindow.cs'),
        false,
        'AudioKit ID generator should not be implemented as a Unity EditorWindow'
    );
    assert.equal(
        workspaceFileExists('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Runtime', 'Tool', 'AudioKit', 'Editor', 'YokiFrame.Unity.AudioKit.Editor.asmdef'),
        false,
        'AudioKit generator should not add a Unity editor asmdef'
    );
});

test('AudioKit ID generator sits below the runtime mixer and lets the page scroll', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const start = js.indexOf('function renderAudioKitPageContent(runtimeHtml)');
    const end = js.indexOf('function renderAudioKitCachedPage()', start);
    assert.ok(start >= 0 && end > start, 'AudioKit page renderer should be capturable');
    const renderer = js.slice(start, end);
    const runtimeIndex = renderer.indexOf('${runtimeHtml}');
    const generatorIndex = renderer.indexOf('${renderAudioKitIndexGenerator()}');
    const contentBodyRule = css.match(/\.content-body--audiokit\s*\{([\s\S]*?)\n\}/)?.[1] ?? '';
    const pageWorkbenchRule = css.match(/\.audiokit-page-workbench\s*\{([\s\S]*?)\n\}/)?.[1] ?? '';

    assert.ok(runtimeIndex >= 0, 'runtime mixer should be rendered in the AudioKit page');
    assert.ok(generatorIndex > runtimeIndex, 'AudioKit ID generator should be rendered below the runtime mixer');
    assert.match(contentBodyRule, /overflow-y:\s*auto/);
    assert.match(contentBodyRule, /overflow-x:\s*hidden/);
    assert.match(pageWorkbenchRule, /display:\s*flex/);
    assert.match(pageWorkbenchRule, /flex-direction:\s*column/);
    assert.match(pageWorkbenchRule, /overflow:\s*visible/);
    assert.doesNotMatch(pageWorkbenchRule, /overflow:\s*hidden/);
});

test('AudioKit frontend splits generator and mixer into focused modules under the line budget', () => {
    const indexModule = 'pages/audiokit-index.js';
    const mixerModule = 'pages/audiokit-mixer.js';
    const pageModule = 'pages/audiokit.js';
    const scripts = getScriptOrder();

    for (const fileName of [indexModule, mixerModule, pageModule]) {
        assert.ok(fs.existsSync(path.join(distDir, fileName)), `${fileName} should exist`);
    }

    const indexOrder = scripts.indexOf(indexModule);
    const mixerOrder = scripts.indexOf(mixerModule);
    const pageOrder = scripts.indexOf(pageModule);
    assert.ok(indexOrder >= 0 && indexOrder < pageOrder, 'AudioKit ID generator should load before the page shell');
    assert.ok(mixerOrder >= 0 && mixerOrder < pageOrder, 'AudioKit mixer renderer should load before the page shell');

    const indexJs = readDistFile(indexModule);
    const mixerJs = readDistFile(mixerModule);
    const pageJs = readDistFile(pageModule);

    for (const [fileName, source] of [[indexModule, indexJs], [mixerModule, mixerJs], [pageModule, pageJs]]) {
        assert.ok(countTextLines(source) <= 500, `${fileName} should stay at or below 500 lines`);
    }

    assert.match(indexJs, /AUDIOKIT_INDEX_DEFAULT_CONFIG/);
    assert.match(indexJs, /function renderAudioKitIndexGenerator/);
    assert.match(indexJs, /invoke\('audiokit_scan_audio_files'/);
    assert.match(indexJs, /invoke\('audiokit_generate_audio_ids'/);
    assert.match(mixerJs, /function renderAudioKitWorkbench/);
    assert.match(mixerJs, /function renderAudioChannelStrips/);
    assert.doesNotMatch(pageJs, /AUDIOKIT_INDEX_DEFAULT_CONFIG/);
    assert.doesNotMatch(pageJs, /function renderAudioKitIndexGenerator/);
    assert.doesNotMatch(pageJs, /function renderAudioKitWorkbench/);
});

test('ActionKit LogKit UIKit and app shell split without redundant flow layers', () => {
    const scripts = getScriptOrder();
    const groups = [
        {
            name: 'ActionKit',
            files: ['pages/actionkit-data.js', 'pages/actionkit-render.js', 'pages/actionkit-interactions.js', 'pages/actionkit.js'],
            page: 'pages/actionkit.js',
            pageMustOwn: /async function loadActionKitWorkbench\(/,
            movedOut: [/function renderActionKitWorkbench\(/, /function bindActionKitWorkbenchActions\(/],
            sharedBridge: /fetchKitWorkbenchState\('ActionKit',\s*normalizeActionKitStatePayload/,
        },
        {
            name: 'LogKit',
            files: ['pages/logkit-render.js', 'pages/logkit-viewer.js', 'pages/logkit.js'],
            page: 'pages/logkit.js',
            pageMustOwn: /async function loadLogKitWorkbench\(/,
            movedOut: [/function renderLogKitWorkbench\(/, /function syncLogKitViewerDom\(/],
            sharedBridge: /fetchKitWorkbenchState\('LogKit',\s*normalizeLogKitStatePayload/,
        },
        {
            name: 'UIKit',
            files: ['pages/uikit-editor-tools.js', 'pages/uikit-render.js', 'pages/uikit.js'],
            page: 'pages/uikit.js',
            pageMustOwn: /async function loadUIKitWorkbench\(/,
            movedOut: [/function renderUIKitWorkbench\(/, /function renderUIKitEditorToolsSection\(/],
            sharedBridge: /fetchKitWorkbenchState\('UIKit',\s*normalizeUIKitStatePayload/,
        },
    ];

    for (const group of groups) {
        const fileSources = group.files.map(fileName => [fileName, readDistFile(fileName)]);
        for (const [fileName, source] of fileSources) {
            assert.ok(countTextLines(source) <= 500, `${fileName} should stay at or below 500 lines`);
        }

        const indexes = group.files.map(fileName => scripts.indexOf(fileName));
        assert.ok(indexes.every(index => index >= 0), `${group.name} split files should be loaded`);
        assert.deepEqual([...indexes].sort((a, b) => a - b), indexes, `${group.name} split files should load in dependency order`);

        const combined = fileSources.map(([, source]) => source).join('\n');
        const pageSource = readDistFile(group.page);
        assert.match(pageSource, group.pageMustOwn, `${group.name} page should own only the page load flow`);
        assert.match(combined, group.sharedBridge, `${group.name} should keep using the shared kit bridge`);
        for (const pattern of group.movedOut) {
            assert.doesNotMatch(pageSource, pattern, `${group.name} page should not keep moved rendering/interaction code`);
        }
        assert.doesNotMatch(combined, /\b(?:EventBus|MessageBus|DataStore|TransportAdapter|FlowController|WorkbenchManager|StoreManager)\b/);
        assert.doesNotMatch(combined, /FromCommands/);
    }

    const coreFiles = ['core/app.js', 'core/app-shell.js', 'core/app-state.js', 'core/app-status.js'];
    const coreIndexes = coreFiles.map(fileName => scripts.indexOf(fileName));
    assert.ok(coreIndexes.every(index => index >= 0), 'core app split files should be loaded');
    assert.deepEqual([...coreIndexes].sort((a, b) => a - b), coreIndexes, 'core app split files should load in dependency order');
    assert.match(readDistFile('core/app.js'), /document\.addEventListener\('contextmenu'/);
    assert.match(readDistFile('core/app-shell.js'), /function renderFontPreferencePanel\(/);
    assert.match(readDistFile('core/app-state.js'), /let activePage\s*=\s*'system'/);
    assert.match(readDistFile('core/app-status.js'), /async function pollStatus\(/);
});

test('Unity AudioKit exposes an optional FMOD backend only inside the Unity adapter layer', () => {
    const unityRuntimeAsmdef = JSON.parse(readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Runtime', 'YokiFrame.Unity.Runtime.asmdef'));
    const fmodAsmdef = JSON.parse(readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Runtime', 'Tool', 'AudioKit', 'Runtime', 'FMOD', 'YokiFrame.Unity.AudioKit.FMOD.asmdef'));
    const backend = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Runtime', 'Tool', 'AudioKit', 'Runtime', 'FMOD', 'FmodAudioKitBackend.cs');
    const legacyBackend = fs.readFileSync('F:/YokiFrame/Assets/YokiFrame/Tools/AudioKit/Runtime/Integration/FMOD/FmodAudioBackend.cs', 'utf8');
    const audioKitRuntimeAsmdef = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'AudioKit', 'Runtime', 'YokiFrame.AudioKit.asmdef');
    const audioKitRuntimeFiles = fs.readdirSync(resolveWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'AudioKit', 'Runtime'), { recursive: true })
        .map(item => String(item).replace(/\\/g, '/'));

    assert.match(backend, /#if !GODOT && YOKIFRAME_FMOD_SUPPORT/);
    assert.match(backend, /namespace YokiFrame\.Unity/);
    assert.match(backend, /public sealed class FmodAudioKitBackend\s*:\s*IAudioBackend,\s*IDisposable/);
    assert.match(backend, /public string BackendName\s*=>\s*"Unity\.FMOD"/);
    assert.match(backend, /RuntimeManager\.GetEventDescription/);
    assert.match(backend, /FMOD\.Studio\.EventInstance/);
    assert.match(backend, /RuntimeUtils\.To3DAttributes/);
    assert.match(backend, /SetBusVolume\(string bus,\s*float volume\)/);
    assert.match(backend, /GetActiveVoices\(List<AudioVoiceDebugInfo> result\)/);
    assert.match(backend, /StopBus\(string bus\)/);
    assert.match(backend, /private sealed class VoiceState/);
    assert.ok(fmodAsmdef.references.includes('YokiFrame.Unity.Runtime'), 'FMOD adapter asmdef should build on the Unity runtime adapter');
    assert.ok(fmodAsmdef.references.includes('YokiFrame.AudioKit'), 'FMOD adapter asmdef should implement AudioKit backend contracts');
    assert.ok(fmodAsmdef.references.includes('FMODUnity'), 'FMOD adapter asmdef should reference FMODUnity');
    assert.deepEqual(fmodAsmdef.defineConstraints, ['YOKIFRAME_FMOD_SUPPORT']);
    assert.doesNotMatch(JSON.stringify(unityRuntimeAsmdef), /FMODUnity/);
    assert.doesNotMatch(audioKitRuntimeAsmdef, /FMODUnity/);
    assert.equal(audioKitRuntimeFiles.some(file => /FMOD|Fmod/.test(file)), false, 'cross-engine AudioKit runtime should not carry Unity FMOD files');
    assert.match(legacyBackend, /public sealed class FmodAudioBackend/);
});

test('UIKit page exposes Unity editor Prefab creation and binding commands through Tauri', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const runtimeHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Runtime', 'CommandBridge', 'UIKitCommandHandler.cs');

    assert.ok(
        workspaceFileExists('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'YokiFrame.UIKit.Editor.asmdef'),
        'UIKit editor assembly should be isolated from runtime and Unity adapters'
    );
    assert.ok(
        workspaceFileExists('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'CommandBridge', 'UnityUIKitCommandHandler.cs'),
        'Unity-only UIKit editor commands should live in the UIKit editor assembly'
    );
    assert.ok(
        workspaceFileExists('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'PanelCreation', 'UIKitPanelPrefabCreator.cs'),
        'UIKit panel prefab creation should be a reusable editor service'
    );

    const editorHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'CommandBridge', 'UnityUIKitCommandHandler.cs');
    const panelCreator = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'PanelCreation', 'UIKitPanelPrefabCreator.cs');
    const panelCreatorCodeGen = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'PanelCreation', 'UIKitPanelPrefabCreator.CodeGen.cs');

    assert.match(editorHandler, /public string KitName\s*\{\s*get\s*\{\s*return "UIKit";\s*\}/);
    assert.match(editorHandler, /new UIKitCommandHandler\(\)/);
    assert.match(editorHandler, /"get_editor_tool_state"/);
    assert.match(editorHandler, /"create_panel_prefab"/);
    assert.match(editorHandler, /"generate_code_for_selection"/);
    assert.match(editorHandler, /"add_bind_to_selection"/);
    assert.match(editorHandler, /"remove_bind_from_selection"/);
    assert.match(panelCreator, /PrefabUtility\.SaveAsPrefabAsset/);
    assert.match(panelCreator, /AddComponent<RectTransform>\(\)/);
    assert.match(panelCreator, /AddComponent<UnityEngine\.UI\.Image>\(\)/);
    assert.match(panelCreatorCodeGen, /WritePanelScript/);
    assert.match(panelCreatorCodeGen, /WritePanelDesignerScript/);
    assert.match(panelCreator, /Assets\/Resources\/Art\/UIPrefab/);
    assert.match(panelCreator, /Assets\/Scripts\/UI/);
    assert.match(panelCreator, /DEFAULT_ASSEMBLY_NAME\s*=\s*"Assembly-CSharp"/);
    assert.match(panelCreator, /DEFAULT_CODE_TEMPLATE\s*=\s*"Default"/);
    assert.match(panelCreator, /MINIMAL_CODE_TEMPLATE\s*=\s*"Minimal"/);
    const panelCreateRequest = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'PanelCreation', 'UIKitPanelCreateRequest.cs');
    const panelCreatorBindings = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'PanelCreation', 'UIKitPanelPrefabCreator.Bindings.cs');
    assert.match(panelCreateRequest, /public string AssemblyName/);
    assert.match(panelCreateRequest, /public string CodeTemplate/);
    assert.match(panelCreatorBindings, /ResolveType\(scriptNamespace \+ "\." \+ panelName,\s*assemblyName\)/);
    assert.match(editorHandler, /BuildAssembliesJson\(\)/);
    assert.match(editorHandler, /BuildCodeTemplatesJson\(\)/);
    assert.match(editorHandler, /assemblyName[\s\S]*DEFAULT_ASSEMBLY_NAME/);
    assert.match(editorHandler, /codeTemplate[\s\S]*DEFAULT_CODE_TEMPLATE/);
    assert.match(js, /function renderUIKitEditorToolsSection\(/);
    assert.match(js, /assemblyName:\s*'Assembly-CSharp'/);
    assert.match(js, /codeTemplate:\s*'Default'/);
    assert.match(js, /assemblies:\s*normalizeUIKitEditorOptions/);
    assert.match(js, /codeTemplates:\s*normalizeUIKitEditorOptions/);
    assert.match(js, /renderUIKitEditorSelectField\('assemblyName',\s*'程序集'/);
    assert.match(js, /renderUIKitEditorSelectField\('codeTemplate',\s*'代码模板'/);
    assert.match(js, /AssemblyName:\s*String\(form\.assemblyName/);
    assert.match(js, /CodeTemplate:\s*String\(form\.codeTemplate/);
    assert.match(js, /kit-workbench-grid--uikit/);
    assert.match(js, /data-uikit-root-settings/);
    assert.match(js, /UIRoot 设置/);
    assert.match(js, /data-uikit-create-panel/);
    assert.match(js, /sendKitCommandData\('UIKit',\s*'create_panel_prefab'/);
    assert.match(js, /sendKitCommandData\('UIKit',\s*'generate_code_for_selection'/);
    assert.match(js, /sendKitCommandData\('UIKit',\s*'add_bind_to_selection'/);
    assert.match(js, /sendKitCommandData\('UIKit',\s*'remove_bind_from_selection'/);
    assert.match(js, /engineSupportsKitFeature\(.*'UIKit'.*'ui_editor_tools'/);
    assert.match(css, /\.uikit-editor-tools/);
    assert.match(css, /\.content-body--uikit\s*\{[\s\S]*overflow-y:\s*auto/);
    assert.match(css, /\.kit-workbench-grid--uikit\s*\{[\s\S]*grid-template-columns/);
    assert.match(css, /\.uikit-editor-field \.cmd-select/);
    assert.match(runtimeHandler, /BuildRootSettingsJson/);
    assert.match(runtimeHandler, /rootSettings/);
    assert.match(runtimeHandler, /只输出面板缓存和面板栈诊断/);
});

test('ActionKit workbench visualizes deeply nested action structures through an outline flow monitor', () => {
    const html = readDistFile('index.html');
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const publisher = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Editor', 'TauriBridge', 'CommandBridge', 'KitStateSnapshotPublisher.cs');
    const host = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Unity', 'Editor', 'CommandBridge', 'UnityCommandBridgeHost.cs');
    const godotHost = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Godot', 'Runtime', 'Core', 'CommandBridge', 'Runtime', 'GodotCommandBridgeHost.cs');
    const godotPublisher = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'Adapters', 'Godot', 'Runtime', 'Core', 'CommandBridge', 'Runtime', 'GodotKitStateSnapshotPublisher.cs');
    const scheduler = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'ActionKit', 'Runtime', 'Core', 'Executor', 'ActionKitScheduler.cs');

    assert.match(html, /data-page="actionkit"/);
    assert.match(html, />ActionKit</);

    assert.match(js, /ActionKit:\s*\[[\s\S]*?get_workbench_snapshot[\s\S]*?set_stack_trace[\s\S]*?clear_stack_trace/);
    assert.match(js, /function renderActionKitPage\(/);
    assert.match(js, /function fetchActionKitWorkbenchState\(\{\s*forceCommandRefresh\s*=\s*false\s*\}\s*=\s*\{\}\)/);
    assert.match(js, /function fetchKitWorkbenchState\(kit,\s*normalize,\s*options = \{\}\)/);
    assert.match(js, /fetchKitWorkbenchState\('ActionKit',\s*normalizeActionKitStatePayload/);
    assert.doesNotMatch(js, /fetchActionKitWorkbenchStateFromCommands/);
    assert.match(js, /function renderActionKitFlowMap\(/);
    assert.match(js, /function renderActionKitTreeRows\(/);
    assert.match(js, /function getActionKitNodePath\(/);
    assert.match(js, /function renderActionKitNodeDetail\(/);
    assert.match(js, /function flattenActionKitNodes\(/);
    assert.doesNotMatch(js, /currentChildIndex:\s*Number\(source\.currentChildIndex\s*\?\?\s*-1\)\s*\|\|\s*-1/);
    assert.match(js, /data-actionkit-search/);
    assert.match(js, /data-actionkit-stack/);
    assert.match(js, /data-actionkit-clear-stack/);
    assert.match(js, /function toggleActionKitStackTrace\(/);
    assert.match(js, /function clearActionKitStackTrace\(/);
    assert.match(js, /registerKitReactiveRefresh\('actionkit'/);
    assert.match(js, /registerPage\('actionkit',\s*\{\s*render:\s*renderActionKitPage\s*\}\)/);
    assert.match(js, /content-body--actionkit/);
    assert.match(js, /actionkit-structure-shell/);
    assert.match(js, /actionkit-tree-row/);
    assert.match(js, /--action-depth/);
    assert.match(js, /actionkit-inspector-drawer/);
    assert.match(js, /data-actionkit-workbench="root"/);
    assert.match(js, /data-actionkit-breadcrumb/);
    assert.match(js, /data-actionkit-detail-panel/);
    assert.doesNotMatch(js, /class="actionkit-node/);
    assert.doesNotMatch(js, /actionkit-node__children/);

    assert.match(css, /\.actionkit-structure-shell/);
    assert.match(css, /\.actionkit-root-rail/);
    assert.match(css, /\.actionkit-root-list/);
    assert.match(css, /\.actionkit-tree-stage/);
    assert.match(css, /\.actionkit-tree-scroll/);
    assert.match(css, /\.actionkit-tree-row/);
    assert.match(css, /\.actionkit-tree-row--sequence/);
    assert.match(css, /\.actionkit-tree-row--parallel/);
    assert.match(css, /\.actionkit-tree-row--repeat/);
    assert.match(css, /padding-left:\s*calc\(var\(--action-depth/);
    assert.match(css, /\.actionkit-inspector-drawer/);
    assert.doesNotMatch(css, /\.kit-workbench-grid--actionkit/);
    assert.doesNotMatch(css, /\.actionkit-node\b/);
    assert.doesNotMatch(css, /\.actionkit-detail-card/);

    assert.ok(
        workspaceFileExists('Assets', 'YokiFrame', 'Tools', 'ActionKit', 'Runtime', 'Diagnostics', 'ActionStackTraceService.cs'),
        'ActionKit should keep stack tracing in a default-off runtime diagnostics service'
    );
    assert.ok(
        workspaceFileExists('Assets', 'YokiFrame', 'Tools', 'ActionKit', 'Runtime', 'CommandBridge', 'ActionKitCommandHandler.cs'),
        'ActionKit should expose a Runtime command bridge handler'
    );
    const stackService = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'ActionKit', 'Runtime', 'Diagnostics', 'ActionStackTraceService.cs');
    const handler = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'ActionKit', 'Runtime', 'CommandBridge', 'ActionKitCommandHandler.cs');
    assert.match(stackService, /public static class ActionStackTraceService/);
    assert.match(stackService, /Enabled\s*\{\s*get/);
    assert.match(stackService, /Register\(ulong actionId/);
    assert.match(handler, /SupportedActions[\s\S]*"get_workbench_snapshot"[\s\S]*"set_stack_trace"[\s\S]*"clear_stack_trace"/);
    assert.match(handler, /BuildActionNodeJson/);
    assert.match(handler, /ActionStackTraceService/);
    assert.match(scheduler, /GetExecutingActions/);

    assert.match(publisher, /ACTION_KIT_NAME/);
    assert.match(publisher, /ACTION_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(publisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*ACTION_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(publisher, /PublishOptionalIfChanged\(yokiframeRoot,\s*ACTION_KIT_NAME/);
    assert.match(publisher, /BuildActionPayloadJson/);
    assert.match(host, /ACTIONKIT_COMMAND_HANDLER_TYPE/);
    assert.match(host, /OptionalKitCommandHandlerRegistry\.TryRegister\(Dispatcher,\s*ACTIONKIT_COMMAND_HANDLER_TYPE\)/);
    assert.match(godotHost, /ACTION_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(godotHost, /OptionalKitCommandHandlerRegistry\.TryRegister\(mDispatcher,\s*ACTION_KIT_COMMAND_HANDLER_TYPE\)/);
    assert.match(godotPublisher, /ACTION_KIT_NAME/);
    assert.match(godotPublisher, /ACTION_KIT_COMMAND_HANDLER_TYPE/);
    assert.match(godotPublisher, /CommandBridgeSnapshotPublisher\(ENGINE_ID,\s*ACTION_KIT_NAME,\s*SNAPSHOT_NAME/);
    assert.match(godotPublisher, /action_update/);
});

test('ActionKit node selection updates in place without re-rendering the whole workbench', () => {
    const js = readFrontendScripts();
    const bindStart = js.indexOf('function bindActionKitNodeClicks()');
    const syncStart = js.indexOf('function syncActionKitSelectionDom()');
    const renderStart = js.indexOf('function renderActionKitWorkbenchFromState()', syncStart);
    const bindSource = js.slice(bindStart, syncStart);
    const syncSource = js.slice(syncStart, renderStart);

    assert.match(js, /function syncActionKitSelectionDom\(/);
    assert.match(bindSource, /syncActionKitSelectionDom\(\)/);
    assert.doesNotMatch(bindSource, /renderActionKitWorkbenchFromState\(\)/);
    assert.match(syncSource, /data-actionkit-workbench="root"/);
    assert.match(syncSource, /data-actionkit-breadcrumb/);
    assert.match(syncSource, /data-actionkit-detail-panel/);
    assert.match(syncSource, /querySelectorAll\('\[data-actionkit-node\]'\)/);
    assert.doesNotMatch(syncSource, /renderWorkbenchHtmlStable\(/);
});

test('LogKit viewer selection and decrypted preview update in place without full workbench refresh', () => {
    const js = readFrontendScripts();
    const renderStart = js.indexOf('function renderLogKitWorkbenchFromState()');
    const syncStart = js.indexOf('function syncLogKitViewerDom()');
    const selectStart = js.indexOf('async function selectLogKitFile(kind)');
    const decryptStart = js.indexOf('async function decryptLogKitFile(filePath)');
    const refreshStart = js.indexOf('async function refreshLogKitViewerState()');
    const selectEnd = js.indexOf('async function pickEncryptedLogFile()', selectStart);
    const decryptEnd = js.indexOf('function setLogKitActionMessage(', decryptStart);
    const syncEnd = renderStart > syncStart ? renderStart : js.length;
    const selectSource = js.slice(selectStart, selectEnd);
    const decryptSource = js.slice(decryptStart, decryptEnd);
    const syncSource = js.slice(syncStart, syncEnd);

    assert.match(js, /data-logkit-workbench="root"/);
    assert.match(js, /data-logkit-viewer-panel/);
    assert.match(js, /data-logkit-viewer-tabs/);
    assert.match(js, /data-logkit-viewer-summary/);
    assert.match(js, /data-logkit-selected-path/);
    assert.match(js, /data-logkit-viewer-content/);
    assert.match(js, /function syncLogKitViewerDom\(/);
    assert.match(js, /function renderLogKitWorkbenchFromState\(/);
    assert.match(selectSource, /await refreshLogKitViewerState\(\);\s*syncLogKitViewerDom\(\);/);
    assert.match(decryptSource, /syncLogKitViewerDom\(\);/);
    assert.doesNotMatch(selectSource, /await loadLogKitWorkbench\(\{ forceCommandRefresh: true \}\);/);
    assert.doesNotMatch(decryptSource, /await loadLogKitWorkbench\(\{ forceCommandRefresh: true \}\);/);
    assert.match(syncSource, /querySelector\('\[data-logkit-viewer-tabs\]'\)/);
    assert.match(syncSource, /querySelector\('\[data-logkit-history\]'\)/);
    assert.doesNotMatch(syncSource, /renderWorkbenchHtmlStable\(/);
});

test('PoolKit ResKit SingletonKit and LogKit avoid duplicate workbench toolbar cards', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    const poolSection = js.slice(js.indexOf('// 页面：PoolKit'), js.indexOf('// 页面：FsmKit'));
    const resSection = js.slice(js.indexOf('// 页面：ResKit'), js.indexOf('// 页面：SingletonKit'));
    const singletonSection = js.slice(js.indexOf('// 页面：SingletonKit'), js.indexOf('// 页面：LogKit'));
    const logSection = js.slice(js.indexOf('// 页面：LogKit'), js.indexOf('// 页面：AudioKit'));
    const poolRenderSection = js.slice(js.indexOf('function renderPoolKitWorkbench('), js.indexOf('function filterPoolKitPools'));
    const resRenderSection = js.slice(js.indexOf('function renderResKitWorkbench('), js.indexOf('function filterResKitResources'));
    const singletonRenderSection = js.slice(js.indexOf('function renderSingletonKitWorkbench('), js.indexOf('function filterSingletonKitRows'));
    const logRenderSection = js.slice(js.indexOf('function renderLogKitWorkbench('), js.indexOf('function renderLogKitSettingsSection'));

    assert.match(js, /function renderKitTitle\(iconName,\s*label\)/);
    assert.match(poolSection, /renderKitTitle\('pressure',\s*t\('poolkit\.current_pool'\)\)/);
    assert.match(poolSection, /renderKitTitle\('log',\s*t\('poolkit\.event_stream'\)\)/);
    assert.match(poolSection, /function formatPoolLimit\(value\)/);
    assert.match(poolSection, /function renderPoolUsageBar\(activeCount,\s*inactiveCount,\s*totalCount/);
    assert.match(poolSection, /function getPoolKitRecentEventByPool\(events\)/);
    assert.match(poolSection, /kit-pool-event-chip/);
    assert.match(poolSection, /t\('poolkit\.just_spawned'\)/);
    assert.match(poolSection, /t\('poolkit\.just_returned'\)/);
    assert.match(js, /function formatKitCodeLocation\(location\)/);
    assert.match(js, /function renderKitCodeJumpButton\(filePath,\s*line/);
    assert.match(js, /function renderKitToggle\(label,\s*checked,\s*attrName/);
    assert.match(js, /function bindKitToggleChange\(selector,\s*handler/);
    assert.match(js, /function bindKitButtonClick\(selector,\s*handler/);
    assert.match(poolSection, /kit-detail-summary--pool/);
    assert.match(poolSection, /kit-pool-object-section/);
    assert.match(poolSection, /t\('poolkit\.pooled_objects_list'\)/);
    assert.match(poolSection, /kit-mini-row__jump/);
    assert.match(poolSection, /setHero\([\s\S]*?onclick="togglePoolKitTracking\(\)"/);
    assert.match(poolSection, /setHero\([\s\S]*?onclick="togglePoolKitStackLocation\(\)"/);
    assert.match(poolSection, /setHero\([\s\S]*?onclick="runPoolKitLeakCheck\(\)"/);
    assert.match(poolSection, /setHero\([\s\S]*?onclick="clearPoolKitHistory\(\)"/);
    assert.match(resSection, /renderKitTitle\('jump',\s*t\('reskit\.detail_title'\)\)/);
    assert.match(resSection, /setHero\([\s\S]*?onclick="toggleResKitLoadLocationTracking\(\)"/);
    assert.match(resSection, /setHero\([\s\S]*?onclick="clearResKitHistory\(\)"/);
    assert.match(singletonSection, /renderKitTitle\('status',\s*t\('singletonkit\.lifecycle_detail'\)\)/);
    assert.match(singletonSection, /setHero\([\s\S]*?onclick="refreshSingletonKit\(\)"/);
    assert.match(logSection, /setHero\([\s\S]*?onclick="clearLogKitHistory\(\)"/);
    assert.match(logSection, /setHero\([\s\S]*?onclick="resetLogKitSettings\(\)"/);
    assert.doesNotMatch(poolRenderSection, /renderKitTitle\('pool',\s*'对象池工作台'\)/);
    assert.doesNotMatch(poolRenderSection, /data-poolkit-tracking/);
    assert.doesNotMatch(poolRenderSection, /data-poolkit-stack/);
    assert.doesNotMatch(poolRenderSection, /data-poolkit-clear-history/);
    assert.doesNotMatch(poolRenderSection, /data-poolkit-leak/);
    assert.doesNotMatch(resRenderSection, /renderKitTitle\('res',\s*'资源工作台'\)/);
    assert.doesNotMatch(resRenderSection, /data-reskit-tracking/);
    assert.doesNotMatch(resRenderSection, /data-reskit-clear-history/);
    assert.doesNotMatch(singletonRenderSection, /renderKitTitle\('singleton',\s*'单例注册表'\)/);
    assert.doesNotMatch(logRenderSection, /renderKitTitle\('log',\s*'日志工作台'\)/);
    assert.doesNotMatch(logRenderSection, /data-logkit-clear-history/);
    assert.doesNotMatch(logRenderSection, /data-logkit-reset-settings/);
    assert.match(css, /\.kit-title-with-icon/);
    assert.match(css, /\.kit-title-icon/);
    assert.match(css, /\.kit-toggle/);
    assert.match(css, /\.kit-toggle__track/);
    assert.match(css, /\.kit-usage--split/);
    assert.match(css, /\.kit-usage__active/);
    assert.match(css, /\.kit-usage__idle/);
    assert.doesNotMatch(css, /transition:\s*width/);
    assert.match(css, /\.kit-pool-event-chip/);
    assert.match(css, /@keyframes poolkit-segment-pulse/);
    assert.match(css, /\.kit-code-jump/);
    assert.match(css, /\.kit-detail-summary--pool/);
    assert.match(css, /\.kit-pool-object-sections/);
    assert.match(css, /\.kit-mini-row__jump/);
    assert.match(poolSection, /kit-workbench-grid--pool/);
    assert.match(resSection, /kit-workbench-grid--res/);
    assert.match(singletonSection, /kit-workbench-grid--singleton/);
    assert.match(poolSection, /clearMetrics\(\)/);
    assert.match(resSection, /clearMetrics\(\)/);
    assert.doesNotMatch(poolSection, /setMetrics\(/);
    assert.doesNotMatch(resSection, /setMetrics\(/);
    assert.doesNotMatch(poolSection, /diagnosticTile\(/);
    assert.doesNotMatch(resSection, /diagnosticTile\(/);
});

test('PoolKit and ResKit expose code jump locations through the unified System opener', () => {
    const js = readFrontendScripts();
    const poolHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'CommandBridge', 'Handlers', 'PoolKitCommandHandler.cs');
    const resHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'CommandBridge', 'Handlers', 'ResKitCommandHandler.cs');
    const systemHandler = readWorkspaceFile('Assets', 'YokiFrame', 'Core', 'Runtime', 'CommandBridge', 'Handlers', 'SystemCommandHandler.cs');

    assert.match(js, /function openKitCodeLocation\(filePath,\s*line\)/);
    assert.match(js, /sendKitCommandData\('System',\s*'open_code_location'/);
    assert.match(js, /data-kit-open-code/);
    assert.match(js, /renderKitCodeJumpButton\(obj\.sourceFile,\s*obj\.sourceLine,\s*t\('poolkit\.location'\)/);
    assert.match(js, /t\('poolkit\.open_location'\)/);
    assert.match(js, /renderKitCodeJumpButton\(resource\.sourceFile,\s*resource\.sourceLine,\s*t\('reskit\.load_location'\)/);
    assert.match(js, /t\('source\.not_recorded'\)/);
    assert.match(poolHandler, /sourceFile/);
    assert.match(poolHandler, /sourceLine/);
    assert.match(poolHandler, /AppendPoolDetail/);
    assert.match(resHandler, /sourceFile/);
    assert.match(resHandler, /sourceLine/);
    assert.match(resHandler, /EnableLoadLocationTracking/);
    assert.match(systemHandler, /open_code_location/);
});

test('FsmKit workbench exposes stable update anchors for in-place refresh', () => {
    const js = readFrontendScripts();

    assert.match(js, /let fsmWorkbenchRenderSignature\s*=\s*''/);
    assert.match(js, /function updateFsmWorkbenchView\(/);
    assert.match(js, /function replaceFsmWorkbenchRegion\(/);
    assert.match(js, /function makeFsmWorkbenchSignature\(/);
    assert.match(js, /const nextSignature\s*=\s*makeFsmWorkbenchSignature\(fsms,\s*selectedMeta,\s*detail,\s*history,\s*emptyMessage\)/);
    assert.match(js, /if\s*\(preferInPlace && nextSignature === fsmWorkbenchRenderSignature\)\s*\{\s*return;\s*\}/);
    assert.match(js, /data-fsm-workbench="root"/);
    assert.match(js, /data-fsm-region="list"/);
    assert.match(js, /data-fsm-region="detail"/);
    assert.match(js, /data-fsm-region="matrix"/);
    assert.match(js, /data-fsm-region="insights"/);
    assert.match(js, /data-fsm-region="history"/);
    assert.match(js, /get_state_events/);
});

test('FsmKit workbench ignores stale async refreshes after selecting another FSM', () => {
    const js = readFrontendScripts();

    assert.match(js, /let fsmWorkbenchLoadSeq\s*=\s*0/);
    assert.match(js, /const requestSeq\s*=\s*\+\+fsmWorkbenchLoadSeq/);
    assert.match(js, /function isCurrentFsmWorkbenchLoad\(/);
    assert.match(js, /if\s*\(!isCurrentFsmWorkbenchLoad\(requestSeq\)\)\s*return/);
    assert.match(js, /function markFsmListSelection\(/);
});

test('FsmKit selection renders cached data immediately before bridge detail fetches complete', () => {
    const js = readFrontendScripts();

    assert.match(js, /const fsmDetailCache\s*=\s*new Map\(\)/);
    assert.match(js, /const fsmHistoryCache\s*=\s*new Map\(\)/);
    assert.match(js, /function selectFsmWorkbench\(/);
    assert.match(js, /function updateFsmWorkbenchSummaryView\(/);
    assert.match(js, /function scheduleFsmSelectionRender\(/);
    assert.match(js, /requestIdleCallback/);
    assert.match(js, /FSM_SELECTION_RENDER_IDLE_TIMEOUT_MS/);
    assert.match(js, /renderFsmWorkbenchFromCache\(fsmListCache/);
    assert.match(js, /updateFsmWorkbenchSummaryView\(fsmListCache/);
    assert.match(js, /scheduleFsmSelectionRender\(fsmName,\s*requestSeq,\s*fsmListCache,\s*selectedMeta\)/);
    assert.match(js, /function refreshSelectedFsmWorkbenchDetail\(/);
    assert.match(js, /Promise\.all\(\[\s*fetchFsmStateDetail\(requestedFsmName\),\s*fetchFsmHistory\(requestedFsmName\)\s*\]\)/);
    assert.match(js, /selectFsmWorkbench\(decodeURIComponent\(btn\.dataset\.fsm/);
    assert.doesNotMatch(js, /markFsmListSelection\(selectedFsmName\);\s*loadFsmWorkbench\(\{ preferInPlace: true \}\)/);

    const selection = js.match(/function selectFsmWorkbench\(fsmName\) \{[\s\S]*?\n\}/);
    assert.ok(selection, 'selection handler should be capturable');
    assert.match(selection[0], /markFsmListSelection\(fsmName\)/);
    assert.match(selection[0], /updateFsmWorkbenchSummaryView/);
    assert.match(selection[0], /scheduleFsmSelectionRender/);
    assert.doesNotMatch(selection[0], /refreshSelectedFsmWorkbenchDetail/);
});

test('FsmKit workbench reconciles stale snapshot selections instead of showing not found errors', () => {
    const js = readFrontendScripts();

    assert.match(js, /function isFsmNotFoundError\(/);
    assert.match(js, /async function reconcileFsmWorkbenchAfterMissingSelection\(/);
    assert.match(js, /async function fetchFsmCommandList\(/);
    assert.match(js, /catch\s*\(e\)\s*\{[\s\S]*?isFsmNotFoundError\(e\)[\s\S]*?reconcileFsmWorkbenchAfterMissingSelection\(requestedFsmName,\s*requestSeq/);
    assert.match(js, /fsmDetailCache\.delete\(missingFsmName\)/);
    assert.match(js, /fsmHistoryCache\.delete\(missingFsmName\)/);
});

test('FsmKit active FSM list keeps a fixed card rhythm for long state names', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /class="fsm-list-content"/);
    assert.match(js, /class="fsm-list-state"/);
    assert.match(js, /class="fsm-list-count"/);
    assert.match(css, /\.fsm-list-item\s*\{[\s\S]*?min-height:\s*76px/);
    assert.match(css, /\.fsm-list-meta\s*\{[\s\S]*?grid-template-columns:\s*minmax\(0,\s*1fr\)\s*auto\s*max-content/);
    assert.match(css, /\.fsm-list-state[\s\S]*?text-overflow:\s*ellipsis/);
    assert.match(css, /\.fsm-list-count[\s\S]*?white-space:\s*nowrap/);
});

test('FsmKit active FSM list supports search filtering and animated selection changes', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /fsmListSearchTerm/);
    assert.match(js, /data-fsm-search-input/);
    assert.match(js, /function bindFsmWorkbenchSearch\(/);
    assert.match(js, /function applyFsmWorkbenchSearch\(/);
    assert.match(js, /function filterFsmListItems\(/);
    assert.match(css, /\.fsm-list-search/);
    assert.match(css, /\.fsm-list-item\.active[\s\S]*?animation:\s*fsm-list-select-in/);
    assert.match(css, /\.fsm-list-item\.active[\s\S]*?transform:\s*translateX/);
});

test('sidebar selection uses a sliding highlight indicator', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const indicatorSourceStart = js.indexOf('function updateSidebarActiveIndicator(');
    const indicatorSourceEnd = js.indexOf('function bindShellControls()', indicatorSourceStart);
    const indicatorSource = js.slice(indicatorSourceStart, indicatorSourceEnd);

    assert.match(js, /sidebar-active-indicator/);
    assert.match(js, /updateSidebarActiveIndicator\(/);
    assert.match(js, /syncSidebarActiveIndicator\(/);
    assert.match(indicatorSource, /item\.scrollIntoView\(\{\s*block:\s*'nearest',\s*inline:\s*'nearest'/);
    assert.match(indicatorSource, /behavior:\s*prefersReducedMotion\(\)\s*\?\s*'auto'\s*:\s*'smooth'/);
    assert.match(indicatorSource, /indicator\.classList\.add\('is-moving'\)/);
    assert.match(indicatorSource, /indicator\.classList\.add\('is-settling'\)/);
    assert.match(indicatorSource, /requestAnimationFrame\(\(\)\s*=>\s*updateSidebarActiveIndicator/);
    assert.match(css, /\.sidebar-active-indicator/);
    assert.match(css, /\.sidebar-active-indicator\.is-moving/);
    assert.match(css, /\.sidebar-active-indicator\.is-settling/);
    assert.match(css, /@keyframes\s+yoki-sidebar-indicator-settle/);
    assert.match(css, /transition:\s*transform/);
    assert.doesNotMatch(css, /transition:\s*height/);
});

test('page tabs use a sliding underline indicator when selection changes', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const tabSourceStart = js.indexOf('function syncTabActiveIndicator(');
    const tabSourceEnd = js.indexOf('function renderTabButtons(', tabSourceStart);
    const tabSource = js.slice(tabSourceStart, tabSourceEnd);

    assert.match(js, /tab-active-indicator/);
    assert.match(js, /syncTabActiveIndicator\(/);
    assert.match(js, /ensureTabActiveIndicator\(/);
    assert.match(tabSource, /button\.scrollIntoView\(\{\s*block:\s*'nearest',\s*inline:\s*'nearest'/);
    assert.match(tabSource, /behavior:\s*prefersReducedMotion\(\)\s*\?\s*'auto'\s*:\s*'smooth'/);
    assert.match(tabSource, /indicator\.style\.width\s*=\s*`\$\{Math\.round\(buttonRect\.width\)\}px`/);
    assert.match(tabSource, /indicator\.style\.transform\s*=\s*nextTransform/);
    assert.match(tabSource, /indicator\.classList\.add\('is-moving'\)/);
    assert.match(tabSource, /indicator\.classList\.add\('is-settling'\)/);
    assert.match(js, /syncTabActiveIndicator\(activeTab\)/);
    assert.match(js, /syncTabActiveIndicator\(eventKitActiveTab\)/);
    assert.doesNotMatch(js, /function activateFsmTab\(/);
    assert.match(css, /\.tab-active-indicator/);
    assert.match(css, /\.tab-active-indicator\.is-moving/);
    assert.match(css, /\.tab-active-indicator\.is-settling/);
    assert.match(css, /@keyframes\s+yoki-tab-indicator-settle/);
    assert.match(css, /\.tab-bar\s*\{[\s\S]*?padding:[^;]*var\(--sp-sm\)[^;]*var\(--sp-xl\)[^;]*5px/);
    assert.match(css, /\.tab-active-indicator\s*\{[\s\S]*?z-index:\s*2/);
    assert.match(css, /transition:\s*transform/);
    const tabMotionBlock = css.match(/\.tab-active-indicator\s*\{[\s\S]*?\n\}/g)
        ?.find(block => /transition:/.test(block)) ?? '';
    assert.doesNotMatch(tabMotionBlock, /transition:[\s\S]*left/);
});

test('FsmKit current state header keeps long names on one line without growing the card', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /data-fsm-current-state-value/);
    assert.match(js, /function fitFsmCurrentStateValueText\(/);
    assert.match(js, /scheduleFsmCurrentStateFit\(/);
    assert.match(css, /\.fsm-state-card__value\s*\{[\s\S]*?white-space:\s*nowrap/);
    assert.match(css, /\.fsm-state-card__value\s*\{[\s\S]*?overflow:\s*hidden/);
    assert.match(css, /\.fsm-state-card__meta\s*\{[\s\S]*?white-space:\s*nowrap/);
});

test('FsmKit graph animates the latest transition into the current node', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /latestTransition/);
    assert.match(js, /fsm-edge--latest/);
    assert.match(js, /fsm-edge-flow-dot/);
    assert.match(js, /animateMotion/);
    assert.match(css, /\.fsm-edge--latest/);
    assert.match(css, /\.fsm-edge-flow-dot/);
    assert.match(css, /@keyframes\s+fsm-edge-flow/);
    assert.match(css, /@keyframes\s+fsm-node-arrive/);
});

test('FsmKit graph prefers realtime current state over stale detail tree markers', () => {
    const buildFsmGraph = loadDistExpression('pages/fsmkit-graph.js', 'buildFsmGraph');
    const graph = buildFsmGraph([], 'PatrolState', [
        { id: 0, name: 'Dead', isCurrent: true },
        { id: 1, name: 'PatrolState', isCurrent: false },
    ]);

    assert.equal(graph.currentState, 'PatrolState');
    assert.equal(graph.startId, 'PatrolState');
});

test('FsmKit graph maps realtime current state id onto the selected FSM state tree', () => {
    const buildFsmGraph = loadDistExpression('pages/fsmkit-graph.js', 'buildFsmGraph');
    const graph = buildFsmGraph([], 'PatrolState', [
        { id: 0, name: 'Dead', isCurrent: true },
        { id: 1, name: 'Patrol', isCurrent: false },
    ], 1);

    assert.equal(graph.currentState, 'Patrol');
    assert.equal(graph.startId, 'Patrol');
    assert.equal(graph.nodeIndex.has('PatrolState'), false);
});

test('FsmKit detail card prefers realtime selected metadata over stale cached detail', () => {
    const renderFsmDetail = loadDistExpression('pages/fsmkit-workbench.js', 'renderFsmDetail', {
        t: (key, ...args) => [key, ...args].join(' '),
        escapeHtml: value => String(value ?? ''),
    });
    const html = renderFsmDetail(
        { fsmName: 'EnemyFSM', currentState: 'Dead', currentStateId: 3, machineState: 'Running', stateCount: 4 },
        { name: 'EnemyFSM', currentState: 'PatrolState', currentStateId: 1, machineState: 'Running', stateCount: 4 },
    );

    assert.match(html, /PatrolState/);
    assert.match(html, /State Id 1/);
    assert.doesNotMatch(html, /Dead/);
    assert.doesNotMatch(html, /State Id 3/);
});

test('FsmKit graph folds state visit counts into nodes without a lower state matrix', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const start = js.indexOf('function renderFsmMatrixHtml');
    const end = js.indexOf('function renderFsmHistoryHtml', start);

    assert.ok(start >= 0 && end > start, 'renderFsmMatrixHtml should stay adjacent to history rendering');
    const matrixRenderer = js.slice(start, end);

    assert.doesNotMatch(matrixRenderer, /fsm-state-board/);
    assert.doesNotMatch(matrixRenderer, /renderFsmStateCards/);
    assert.doesNotMatch(matrixRenderer, /状态矩阵/);
    assert.match(js, /class="fsm-node-visit-count"/);
    assert.match(js, /x="\$\{FSM_G\.nodeW - 12\}"[\s\S]*text-anchor="end"[\s\S]*>\$\{t\("fsmkit\.visits",\s*escapeHtml\(n\.count\)\)\}<\/text>/);
    assert.match(css, /\.fsm-node-label/);
    assert.match(css, /\.fsm-node-visit-count/);
});

test('FsmKit graph preserves viewport and exposes composite state rendering', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /function captureFsmGraphViewport\(/);
    assert.match(js, /function restoreFsmGraphViewport\(/);
    assert.match(js, /function fitFsmGraphToViewport\(/);
    assert.match(js, /fsmGraphViewportByMachine/);
    assert.match(js, /data-fsm-name/);
    assert.match(js, /renderFsmCompositeGroups/);
    assert.match(js, /flattenFsmCompositeStates/);
    assert.match(js, /fsm-composite-state/);
    assert.match(js, /fsm-composite-child--nested/);
    assert.match(js, /t\('fsmkit\.hierarchical_machine'\)/);
    assert.match(css, /\.fsm-composite-state/);
    assert.match(css, /\.fsm-viewport-controls/);
    assert.match(css, /\.fsm-composite-child--nested/);
});

test('FsmKit graph uses a circular layout with pan and wheel zoom interactions', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /function layoutFsmGraphCircular\(/);
    assert.match(js, /function fsmNodeAnchorPoint\(/);
    assert.match(js, /class="fsm-graph-pan"/);
    assert.match(js, /data-pan-x/);
    assert.match(js, /data-pan-y/);
    assert.match(js, /wheel/);
    assert.match(js, /pointerdown/);
    assert.match(js, /pointermove/);
    assert.match(js, /pointerup/);
    assert.match(css, /\.fsm-graph-scroll--panning/);
    assert.match(css, /\.fsm-graph-pan/);
});

test('FsmKit circular layout keeps node order stable across current-state changes', () => {
    const js = readFrontendScripts();

    assert.match(js, /function sortFsmGraphNodesForCircularLayout\(/);
    assert.match(js, /orderIndex/);
    assert.match(js, /sortFsmGraphNodesForCircularLayout\(graph\.nodes/);
    assert.doesNotMatch(js, /findIndex\(n\s*=>\s*n\.id\s*===\s*graph\.currentState\)/);
});

test('FsmKit transition history stays inside a bounded scroll region', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /fsm-history-scroll/);
    assert.match(js, /fsm-timeline-card/);
    assert.match(css, /\.fsm-history-scroll/);
    assert.match(css, /\.fsm-timeline-card/);
    assert.match(css, /\.fsm-timeline-card__time/);
    assert.match(css, /\.fsm-timeline-card__route/);
    assert.match(css, /\.fsm-history-scroll\s*\{[\s\S]*?min-height:\s*0/);
    assert.match(css, /\.fsm-history-scroll\s*\{[\s\S]*?overflow:\s*auto/);
});

test('FsmKit transition history card fills the remaining right column height', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /<div class="tool-pane">[\s\S]*?<div class="tool-pane-title">\$\{t\('fsmkit\.event_insights'\)\}<\/div>[\s\S]*?data-fsm-region="insights"/);
    assert.match(js, /class="tool-pane fsm-history-pane"[\s\S]*?<div class="tool-pane-title">\$\{t\('fsmkit\.transition_history'\)\}<\/div>[\s\S]*?data-fsm-region="history"/);
    assert.doesNotMatch(js, /class="tool-pane fsm-history-pane"[\s\S]*?<div class="tool-pane-title">\$\{t\('fsmkit\.event_insights'\)\}<\/div>/);
    assert.match(css, /\.fsm-history-pane\s*\{[\s\S]*?flex:\s*1\s+1\s+0/);
    assert.match(css, /\.fsm-history-pane\s*>\s*\.tool-pane-body\s*\{[\s\S]*?flex:\s*1\s+1\s+auto/);
    assert.match(css, /\.fsm-history-pane\s+\.empty-state\s*\{[\s\S]*?height:\s*100%/);
    assert.match(css, /\.fsm-history-scroll\s*\{[\s\S]*?flex:\s*1\s+1\s+auto/);
    assert.doesNotMatch(css, /\.fsm-history-scroll\s*\{[\s\S]*?max-height:\s*min\(42vh,\s*520px\)/);
});

test('FsmKit workbench is a compact single-screen layout with centered graph canvas', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');

    assert.match(js, /\$pageBody\.classList\.add\('content-body--fsmkit'\)/);
    assert.match(js, /\$pageBody\.classList\.remove\('content-body--fsmkit'\)/);
    assert.doesNotMatch(js, /按实例切换观察目标/);
    assert.doesNotMatch(js, /把当前状态、运行状态和状态规模放在第一视线/);
    assert.doesNotMatch(js, /用分层图和状态矩阵一起展示当前态、访问频率和转换关系/);
    assert.doesNotMatch(js, /<button class="btn btn-secondary btn-sm" type="button" disabled>记录<\/button>/);
    assert.match(css, /\.content-body--fsmkit\s*\{[\s\S]*?overflow:\s*hidden/);
    assert.match(css, /\.content-body--fsmkit\s*\{[\s\S]*?display:\s*flex/);
    assert.match(css, /\.fsm-workbench\s*\{[\s\S]*?flex:\s*1\s+1\s+0/);
    assert.match(css, /\.fsm-graph-scroll\s*\{[\s\S]*?display:\s*grid[\s\S]*?place-items:\s*center/);
    assert.match(css, /\.fsm-graph-pan\s*\{[\s\S]*?margin:\s*auto/);
});

test('TableKit frontend splits local state rendering and Tauri actions without command bridge layers', () => {
    const stateModule = 'pages/tablekit-state.js';
    const renderModule = 'pages/tablekit-render.js';
    const actionsModule = 'pages/tablekit-actions.js';
    const pageModule = 'pages/tablekit.js';
    const scripts = getScriptOrder();

    for (const fileName of [stateModule, renderModule, actionsModule, pageModule]) {
        assert.ok(fs.existsSync(path.join(distDir, fileName)), `${fileName} should exist`);
    }

    const previewOrder = scripts.indexOf('pages/tablekit-preview.js');
    const stateOrder = scripts.indexOf(stateModule);
    const renderOrder = scripts.indexOf(renderModule);
    const actionsOrder = scripts.indexOf(actionsModule);
    const pageOrder = scripts.indexOf(pageModule);
    assert.ok(previewOrder >= 0 && previewOrder < stateOrder, 'TableKit preview helpers should load before TableKit state/render modules');
    assert.ok(stateOrder >= 0 && stateOrder < renderOrder, 'TableKit state should load before rendering');
    assert.ok(renderOrder >= 0 && renderOrder < actionsOrder, 'TableKit rendering should load before actions bind through data attributes');
    assert.ok(actionsOrder >= 0 && actionsOrder < pageOrder, 'TableKit actions should load before the page shell');

    const stateJs = readDistFile(stateModule);
    const renderJs = readDistFile(renderModule);
    const actionsJs = readDistFile(actionsModule);
    const pageJs = readDistFile(pageModule);
    const combined = stateJs + renderJs + actionsJs + pageJs;

    for (const [fileName, source] of [[stateModule, stateJs], [renderModule, renderJs], [actionsModule, actionsJs], [pageModule, pageJs]]) {
        assert.ok(countTextLines(source) <= 500, `${fileName} should stay at or below 500 lines`);
    }

    assert.match(stateJs, /function loadTableKitConfig\(/);
    assert.match(stateJs, /function getTableKitLubanStatus\(/);
    assert.match(renderJs, /function renderTableKitRegistryStatus\(/);
    assert.match(renderJs, /function renderTableKitEnvironmentPanel\(/);
    assert.match(actionsJs, /async function runTableKitLuban\(/);
    assert.match(actionsJs, /async function handleTableKitGenerate\(/);
    assert.match(pageJs, /function renderTableKitPage\(/);

    assert.doesNotMatch(pageJs, /function renderTableKitEnvironmentPanel\(/);
    assert.doesNotMatch(pageJs, /async function runTableKitLuban\(/);
    assert.doesNotMatch(combined, /sendKitCommandData\('TableKit'/);
    assert.doesNotMatch(combined, /readKitSnapshotData\('TableKit'/);
    assert.doesNotMatch(combined, /readKitTelemetryData\('TableKit'/);
    assert.doesNotMatch(combined, /invoke\('send_command',\s*\{\s*kit:\s*['"]TableKit/);
});

test('TableKit page reads Luban availability from engine registry instead of runtime commands', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const tablekitDoc = readDistFile('docs/tablekit.md');
    const rustBackend = readTauriSourceFile('src-tauri', 'src', 'main.rs');
    const scripts = getScriptOrder();

    assert.match(js, /function renderTableKitPage\(\)/);
    assert.ok(scripts.indexOf('pages/tablekit-actions.js') < scripts.indexOf('pages/tablekit.js'), 'TableKit actions should load before page shell');

    const tableKitSource = [
        readDistFile('pages/tablekit-state.js'),
        readDistFile('pages/tablekit-render.js'),
        readDistFile('pages/tablekit-actions.js'),
        readDistFile('pages/tablekit-preview.js'),
        readDistFile('pages/tablekit.js'),
    ].join('\n');
    const environmentStart = tableKitSource.indexOf('function renderTableKitEnvironmentPanel(');
    const consoleStart = tableKitSource.indexOf('function renderTableKitConsole(');
    const previewStart = tableKitSource.indexOf('function renderTableKitPreviewPanel(');
    const environmentSource = tableKitSource.slice(environmentStart, consoleStart);
    const consoleSource = tableKitSource.slice(consoleStart, previewStart);
    assert.match(js, /optionalDependencies/);
    assert.match(js, /function getTableKitLubanStatus/);
    assert.match(js, /YOKIFRAME_LUBAN_SUPPORT/);
    assert.match(tableKitSource, /renderTableKitLubanInstallGuide\(status,\s*config\)/);
    assert.match(tableKitSource, /function getTableKitConsoleSignature/);
    assert.match(tableKitSource, /function getTableKitPreviewSignature/);
    assert.match(tableKitSource, /console:\s*getTableKitConsoleSignature\(\)/);
    assert.match(tableKitSource, /preview:\s*getTableKitPreviewSignature\(\)/);
    assert.match(tableKitSource, /previewVersion/);
    assert.doesNotMatch(tableKitSource, /preview:\s*tableKitPreviewState/);
    assert.match(tableKitSource, /Luban 运行时环境未安装/);
    assert.match(tableKitSource, /下载 Luban/);
    assert.match(tableKitSource, /Tools\/Luban\/Luban\.dll/);
    assert.match(tableKitSource, /MiniTemplate/);
    assert.match(tableKitSource, /Directory\.Build\.props/);
    assert.match(tableKitSource, /重启 Godot 或重新启用 YokiFrame 插件/);
    assert.match(tableKitSource, /TABLEKIT_CONFIG_STORAGE_KEY/);
    assert.match(tableKitSource, /环境与路径配置/);
    assert.match(tableKitSource, /生成选项/);
    assert.match(tableKitSource, /TABLEKIT_COLLAPSE_STORAGE_KEY/);
    assert.match(tableKitSource, /function loadTableKitCollapsedSections/);
    assert.match(tableKitSource, /function toggleTableKitCollapsedSection/);
    assert.match(tableKitSource, /getProjectScopedStorageKey\(TABLEKIT_COLLAPSE_STORAGE_KEY\)/);
    assert.match(tableKitSource, /tablekit-config-log-shell/);
    assert.match(tableKitSource, /tablekit-config-log-grid/);
    assert.match(environmentSource, /renderTableKitCollapsibleGroup\('lubanEnvironment',\s*'Luban 环境'/);
    assert.match(environmentSource, /renderTableKitCollapsibleGroup\('outputPaths',\s*'数据路径'/);
    assert.match(environmentSource, /renderTableKitCollapsibleGroup\('extraOutputs',\s*'额外输出目标'/);
    assert.match(tableKitSource, /data-tablekit-collapse="\$\{escapeHtml\(id\)\}"/);
    assert.match(tableKitSource, /aria-expanded="\$\{escapeHtml\(String\(!collapsed\)\)\}"/);
    assert.match(tableKitSource, /querySelectorAll\('\[data-tablekit-collapse\]'\)/);
    assert.doesNotMatch(tableKitSource, /function renderTableKitBuildOptions/);
    assert.doesNotMatch(tableKitSource, /构建选项/);
    assert.match(tableKitSource, /控制台/);
    assert.match(tableKitSource, /数据预览与配置表信息/);
    assert.match(tableKitSource, /renderTableKitTextField\('lubanWorkDir'/);
    assert.match(tableKitSource, /renderTableKitTextField\('outputCodeDir'/);
    assert.match(tableKitSource, /data-tablekit-field="\$\{escapeHtml\(field\)\}"/);
    assert.match(tableKitSource, /function pickTableKitPath/);
    assert.match(tableKitSource, /invoke\('pick_folder'/);
    assert.match(tableKitSource, /invoke\('pick_file'/);
    assert.match(tableKitSource, /invoke\('pick_folder',\s*\{\s*initialPath,\s*projectRoot\s*\}\)/);
    assert.match(tableKitSource, /invoke\('pick_file',\s*\{\s*initialPath,\s*extension:\s*picker\.extension\s*\|\|\s*'',\s*projectRoot\s*\}\)/);
    assert.match(tableKitSource, /data-tablekit-pick-path/);
    assert.match(tableKitSource, /data-tablekit-extra-pick-folder/);
    assert.match(tableKitSource, /readonly aria-readonly="true"/);
    assert.match(tableKitSource, /data-tablekit-preview-tree/);
    assert.match(tableKitSource, /data-tablekit-table-list/);
    assert.match(tableKitSource, /function renderTableKitPreviewInspector/);
    assert.match(tableKitSource, /function renderTableKitRecordList/);
    assert.match(tableKitSource, /function renderTableKitFieldMatrix/);
    assert.match(tableKitSource, /function renderTableKitJsonBlock/);
    assert.match(tableKitSource, /function flattenTableKitPreviewRows/);
    assert.match(tableKitSource, /function getTableKitValueType/);
    assert.match(tableKitSource, /tablekit-preview-inspector/);
    assert.match(tableKitSource, /tablekit-record-list/);
    assert.match(tableKitSource, /tablekit-field-matrix/);
    assert.match(tableKitSource, /tablekit-json-block/);
    assert.match(tableKitSource, /data-tablekit-action="copy-console"/);
    assert.match(tableKitSource, /Godot 使用 \.csproj\/Directory\.Build\.props/);
    assert.match(environmentSource, /data-tablekit-toggle="useAssemblyDefinition"/);
    assert.match(environmentSource, /data-tablekit-toggle="generateExternalTypeUtil"/);
    assert.match(environmentSource, /data-tablekit-toggle="useAsyncLoading"/);
    assert.match(environmentSource, /data-tablekit-toggle="useRawResourceLoading"/);
    assert.match(environmentSource, /原始资源加载/);
    assert.match(tableKitSource, /useRawResourceLoading:\s*true/);
    assert.match(js, /outputCodeDir:\s*'Assets\/Scripts\/TableKit\//);
    assert.match(tableKitSource, /function renderTableKitConsole/);
    assert.match(tableKitSource, /function renderTableKitPreviewPanel/);
    assert.match(tableKitSource, /getProjectScopedStorageKey\(TABLEKIT_CONFIG_STORAGE_KEY\)/);
    assert.doesNotMatch(environmentSource, /tablekit-section--wide/);
    assert.doesNotMatch(environmentSource, /宿主模式/);
    assert.doesNotMatch(environmentSource, /data-tablekit-field="engineMode"/);
    assert.doesNotMatch(tableKitSource, /生成器待接入|执行器待接入|TABLEKIT_TAURI_GENERATOR_BACKEND_READY/);
    assert.match(tableKitSource, /invoke\('tablekit_open_config'/);
    assert.match(tableKitSource, /invoke\('tablekit_run_luban'/);
    assert.match(tableKitSource, /runTableKitLuban\('generate'\)/);
    assert.match(tableKitSource, /runTableKitLuban\('validate'\)/);
    assert.match(consoleSource, /tablekit-section--console/);
    assert.doesNotMatch(tableKitSource, /sendKitCommandData\('TableKit'/);
    assert.doesNotMatch(tableKitSource, /readKitSnapshotData\('TableKit'/);
    assert.doesNotMatch(tableKitSource, /readKitTelemetryData\('TableKit'/);
    assert.doesNotMatch(tableKitSource, /invoke\('send_command',\s*\{\s*kit:\s*['"]TableKit/);
    assert.doesNotMatch(tableKitSource, /尚未接入系统路径选择器/);
    assert.match(rustBackend, /fn pick_folder/);
    assert.match(rustBackend, /fn pick_file/);
    assert.match(rustBackend, /fn tablekit_open_config/);
    assert.match(rustBackend, /fn tablekit_run_luban/);
    assert.match(rustBackend, /pick_folder,/);
    assert.match(rustBackend, /pick_file,/);
    assert.match(rustBackend, /tablekit_open_config,/);
    assert.match(rustBackend, /tablekit_run_luban\s*,?\s*\]/);
    assert.doesNotMatch(rustBackend, /kit:\s*"TableKit"/);
    assert.match(css, /\.cmd-select\s*\{[\s\S]*?appearance:\s*none/);
    assert.match(css, /\.cmd-path-control/);
    assert.match(css, /\.cmd-path-display/);
    assert.match(css, /\.tablekit-config-log-shell\s*\{[\s\S]*?border-radius:\s*var\(--r-md\)/);
    assert.match(css, /\.tablekit-config-log-grid\s*\{[\s\S]*?grid-template-columns:\s*minmax\(0,\s*1fr\)\s+minmax\(0,\s*1fr\)/);
    assert.match(css, /\.tablekit-config-log-grid\s*\{[\s\S]*?align-items:\s*stretch/);
    assert.match(css, /\.tablekit-fold-card\s*\{[\s\S]*?border-radius:\s*var\(--r-md\)/);
    assert.match(css, /\.tablekit-fold-card\[data-collapsed="true"\]\s+\.tablekit-fold-card__body\s*\{[\s\S]*?display:\s*none/);
    assert.match(css, /\.tablekit-install-guide\s*\{[\s\S]*?border:\s*1px solid/);
    assert.match(css, /\.tablekit-install-guide__steps\s*\{[\s\S]*?counter-reset:\s*tablekit-install-step/);
    assert.match(css, /\.tablekit-section--console\s*\{[\s\S]*?display:\s*flex[\s\S]*?flex-direction:\s*column/);
    assert.match(css, /\.tablekit-console\s*\{[\s\S]*?height:\s*0/);
    assert.match(css, /\.tablekit-console\s*\{[\s\S]*?flex:\s*1\s+1\s+0/);
    assert.match(css, /\.tablekit-console\s*\{[\s\S]*?overflow:\s*auto/);
    assert.doesNotMatch(css.match(/\.tablekit-console\s*\{[\s\S]*?\}/)?.[0] || '', /max-height:\s*(?:none|clamp)/);
    assert.match(css, /\.tablekit-console-line::before\s*\{[\s\S]*?border-radius:\s*var\(--r-pill\)/);
    assert.match(css, /\.tablekit-console-line--info strong\s*\{[\s\S]*?color:\s*#7cc7ff/);
    assert.match(css, /\.tablekit-console-line--success em\s*\{[\s\S]*?color:\s*#b7f7c8/);
    assert.match(css, /\.tablekit-console-line--warning em\s*\{[\s\S]*?color:\s*#ffe2a3/);
    assert.match(css, /\.tablekit-console-line--error em\s*\{[\s\S]*?color:\s*#ffbab4/);
    assert.match(css, /\.tablekit-preview-layout\s*\{[\s\S]*?grid-template-columns:\s*minmax\(180px,\s*0\.24fr\)\s+minmax\(0,\s*1fr\)/);
    assert.match(css, /\.tablekit-record-list\s*\{[\s\S]*?overflow:\s*auto/);
    assert.match(css, /\.tablekit-field-matrix\s*\{[\s\S]*?grid-template-columns:\s*repeat\(auto-fit,\s*minmax\(220px,\s*1fr\)\)/);
    assert.match(css, /\.tablekit-field-card__value\s*\{[\s\S]*?font-family:\s*var\(--font-mono\)/);
    assert.match(css, /\.tablekit-json-block\s*\{[\s\S]*?white-space:\s*pre-wrap/);
    assert.match(css, /\.tablekit-type-pill--number/);
    assert.match(css, /\.tablekit-type-pill--boolean/);
    const consoleCssStart = css.indexOf('.tablekit-section--console');
    const consoleCssEnd = css.indexOf('.tablekit-panel-actions', consoleCssStart);
    const consoleSectionCss = css.slice(consoleCssStart, consoleCssEnd);
    assert.ok(consoleCssStart >= 0, 'tablekit console section styles should exist');
    assert.doesNotMatch(consoleSectionCss, /grid-column:\s*1\s*\/\s*-1/);
    assert.match(css, /\.tablekit-console-status code\s*\{[\s\S]*?overflow-wrap:\s*anywhere/);
    assert.match(css, /\.tablekit-console-line em\s*\{[\s\S]*?word-break:\s*break-word/);
    assert.match(tablekitDoc, /Tauri 的 TableKit 页面只读取 engine registry/);
    assert.match(tablekitDoc, /页面不会读取 `TableKit\/state` snapshot/);
    assert.match(tablekitDoc, /Tauri 后端通过 `dotnet Luban\.dll` 执行生成和验证/);
    assert.doesNotMatch(tablekitDoc, /当前页面只完成|后续 Tauri 后端命令接入|Luban 执行器待接入/);
});

test('project level editor caches are scoped by connected project path', () => {
    const appStateSource = readDistFile('core/app-state.js');
    const appStatusSource = readDistFile('core/app-status.js');
    const tableKitSource = readDistFile('pages/tablekit-state.js');
    const audioKitSource = readDistFile('pages/audiokit-index.js');
    const graphKitSource = readDistFile('pages/graphkit-state.js');

    assert.match(appStateSource, /function getProjectScopedStorageKey\(/);
    assert.match(appStateSource, /function getProjectStorageScopeIdentifier\(/);
    assert.match(appStateSource, /encodeURIComponent\(scope\)/);
    assert.doesNotMatch(appStateSource, /localStorage\.getItem\(baseKey\)/);
    assert.match(appStatusSource, /syncProjectScopedEditorStorage\(\)/);

    assert.match(tableKitSource, /syncTableKitProjectStorageScope\(/);
    assert.match(tableKitSource, /getProjectScopedStorageKey\(TABLEKIT_CONFIG_STORAGE_KEY\)/);
    assert.match(tableKitSource, /getProjectScopedStorageKey\(TABLEKIT_CONSOLE_STORAGE_KEY\)/);
    assert.match(tableKitSource, /getProjectScopedStorageKey\(TABLEKIT_COLLAPSE_STORAGE_KEY\)/);
    assert.doesNotMatch(tableKitSource, /localStorage\.setItem\(TABLEKIT_CONFIG_STORAGE_KEY/);

    assert.match(audioKitSource, /syncAudioKitProjectStorageScope\(/);
    assert.match(audioKitSource, /getProjectScopedStorageKey\(AUDIOKIT_INDEX_CONFIG_STORAGE_KEY\)/);
    assert.doesNotMatch(audioKitSource, /localStorage\.setItem\(AUDIOKIT_INDEX_CONFIG_STORAGE_KEY/);

    assert.match(graphKitSource, /syncGraphKitProjectStorageScope\(/);
    assert.match(graphKitSource, /getProjectScopedStorageKey\(GRAPHKIT_STORAGE_KEY\)/);
    assert.doesNotMatch(graphKitSource, /localStorage\.setItem\(GRAPHKIT_STORAGE_KEY/);
});

test('runtime Kit pages guard command bridge calls before sending to editor-only hosts', () => {
    const js = readFrontendScripts();
    const architectureStart = js.indexOf('async function loadArchitectureWorkbench(');
    const architectureEnd = js.indexOf('function reconcileArchitectureSelection(', architectureStart);
    const actionStart = js.indexOf('async function loadActionKitWorkbench(');
    const actionEnd = js.indexOf('// pages/poolkit.js', actionStart);

    assert.ok(architectureStart >= 0, 'Architecture loader should exist');
    assert.ok(architectureEnd > architectureStart, 'Architecture loader should have a bounded source block');
    assert.ok(actionStart >= 0, 'ActionKit loader should exist');
    assert.ok(actionEnd > actionStart, 'ActionKit loader should have a bounded source block');

    const architectureSource = js.slice(architectureStart, architectureEnd);
    const actionSource = js.slice(actionStart, actionEnd);
    const architectureGuard = architectureSource.indexOf("canSendRuntimeKitCommand('Architecture')");
    const architectureFetch = architectureSource.indexOf('fetchArchitectureWorkbenchState');
    const actionGuard = actionSource.indexOf("canSendRuntimeKitCommand('ActionKit')");
    const actionFetch = actionSource.indexOf('fetchActionKitWorkbenchState');

    assert.ok(architectureGuard >= 0, 'Architecture loader should check runtime command availability.');
    assert.ok(architectureFetch >= 0, 'Architecture loader should use the shared workbench state fetcher.');
    assert.ok(actionGuard >= 0, 'ActionKit loader should check runtime command availability.');
    assert.ok(actionFetch >= 0, 'ActionKit loader should use the shared workbench state fetcher.');
    assert.doesNotMatch(architectureSource, /WorkbenchStateFromCommands/);
    assert.doesNotMatch(actionSource, /WorkbenchStateFromCommands/);
    assert.match(architectureSource, /showRuntimeKitUnavailable\('Architecture',\s*t\('architecture\.instance_label'\)\)/);
    assert.match(actionSource, /showRuntimeKitUnavailable\('ActionKit',\s*t\('actionkit\.title'\)\)/);
});

test('docs navigation includes runtime API plus workbench and command bridge notes', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const docsIndex = readDistFile('docs/index.json');
    const quickStartDoc = readDistFile('docs/quick-start.md');
    const architectureDoc = readDistFile('docs/architecture.md');
    const fsmkitDoc = readDistFile('docs/fsmkit.md');
    const eventkitDoc = readDistFile('docs/eventkit.md');
    const poolkitDoc = readDistFile('docs/poolkit.md');
    const reskitDoc = readDistFile('docs/reskit.md');
    const singletonkitDoc = readDistFile('docs/singletonkit.md');
    const actionkitDoc = readDistFile('docs/actionkit.md');
    const audiokitDoc = readDistFile('docs/audiokit.md');
    const savekitDoc = readDistFile('docs/savekit.md');
    const thirdPartyRecommendationsDoc = readDistFile('docs/third-party-recommendations.md');
    const thirdPartyIndexDoc = readDistFile('docs/third-party-index.md');
    assert.ok(fs.existsSync(path.join(distDir, 'docs', 'localizationkit.md')), 'LocalizationKit Tauri doc should exist');
    assert.ok(fs.existsSync(path.join(distDir, 'docs', 'scenekit.md')), 'SceneKit Tauri doc should exist');
    assert.ok(fs.existsSync(path.join(distDir, 'docs', 'spatialkit.md')), 'SpatialKit Tauri doc should exist');
    assert.ok(fs.existsSync(path.join(distDir, 'docs', 'uikit.md')), 'UIKit Tauri doc should exist');
    assert.ok(fs.existsSync(path.join(distDir, 'docs', 'third-party-recommendations.md')), 'third-party recommendation doc should exist');
    assert.ok(fs.existsSync(path.join(distDir, 'docs', 'third-party-index.md')), 'third-party index doc should exist');
    const localizationkitDoc = readDistFile('docs/localizationkit.md');
    const scenekitDoc = readDistFile('docs/scenekit.md');
    const spatialkitDoc = readDistFile('docs/spatialkit.md');
    const uikitDoc = readDistFile('docs/uikit.md');

    assert.match(docsIndex, /eventkit/);
    assert.match(docsIndex, /poolkit/);
    assert.match(docsIndex, /reskit/);
    assert.match(docsIndex, /singletonkit/);
    assert.match(docsIndex, /architecture/);
    assert.doesNotMatch(docsIndex, /api-reference/);
    assert.match(docsIndex, /actionkit/);
    assert.match(docsIndex, /audiokit/);
    assert.doesNotMatch(docsIndex, /buffkit/);
    assert.match(docsIndex, /savekit/);
    assert.match(docsIndex, /localizationkit/);
    assert.match(docsIndex, /scenekit/);
    assert.match(docsIndex, /spatialkit/);
    assert.match(docsIndex, /uikit/);

    const docsBundle = [
        docsIndex,
        quickStartDoc,
        architectureDoc,
        fsmkitDoc,
        eventkitDoc,
        poolkitDoc,
        reskitDoc,
        singletonkitDoc,
        actionkitDoc,
        audiokitDoc,
        savekitDoc,
        localizationkitDoc,
        scenekitDoc,
        spatialkitDoc,
        uikitDoc,
        thirdPartyRecommendationsDoc,
        thirdPartyIndexDoc,
    ].join('\n');
    assert.match(docsBundle, /命令桥/);
    assert.match(docsBundle, /工作台/);
    assert.match(docsBundle, /Tauri/);
    assert.match(docsBundle, /snapshot|telemetry/);
    assert.match(quickStartDoc, /## 8\. API 接口速查/);
    assert.match(quickStartDoc, /EventKit\.Type/);
    assert.match(quickStartDoc, /IEngineLogger/);
    assert.match(quickStartDoc, /SpatialKit/);
    assert.match(architectureDoc, /IArchitecture/);
    assert.match(architectureDoc, /AbstractService/);
    assert.match(architectureDoc, /AbstractModel/);
    assert.match(audiokitDoc, /get_workbench_snapshot/);
    assert.match(audiokitDoc, /stats/);
    assert.match(audiokitDoc, /voices/);
    assert.match(audiokitDoc, /history/);
    assert.match(uikitDoc, /UIKit\/state/);
    assert.match(uikitDoc, /get_workbench_snapshot/);
    assert.match(uikitDoc, /list_panels/);
    assert.match(uikitDoc, /list_stacks/);
    assert.match(uikitDoc, /IUIBackend/);
    assert.match(savekitDoc, /get_workbench_snapshot/);
    assert.match(savekitDoc, /delete_slot/);
    assert.match(savekitDoc, /disable_auto_save/);
    assert.match(savekitDoc, /SaveKit\/state/);
    assert.match(localizationkitDoc, /get_workbench_snapshot/);
    assert.match(localizationkitDoc, /set_language/);
    assert.match(localizationkitDoc, /LocalizationKit\/state/);
    assert.match(scenekitDoc, /SceneKit\/state/);
    assert.match(scenekitDoc, /get_workbench_snapshot/);
    assert.match(scenekitDoc, /unload_scene/);
    assert.match(scenekitDoc, /ISceneBackend/);
    assert.match(spatialkitDoc, /SpatialKit\/state/);
    assert.match(spatialkitDoc, /get_workbench_snapshot/);
    assert.match(spatialkitDoc, /list_indexes/);
    assert.match(spatialkitDoc, /ISpatialIndex/);
    assert.match(js, /doc-hero/);
    assert.match(js, /renderDocArticleShell\(/);
    assert.match(css, /\.doc-hero/);
    assert.match(css, /\.doc-chip/);
    assert.match(css, /\.md-code-header/);
    assert.match(css, /\.tok-type/);
    assert.match(css, /\.tok-method/);
    assert.match(css, /\.doc-nav-item\[data-doc\]/);
});

test('docs sidebar uses dependency-layer groups and colorful icon navigation entries', () => {
    const js = readFrontendScripts();
    const css = readDistFile('style.css');
    const docsIndex = JSON.parse(readDistFile('docs/index.json'));
    const groups = [...new Set(docsIndex.docs.map(doc => doc.group))];
    const groupById = Object.fromEntries(docsIndex.docs.map(doc => [doc.id, doc.group]));
    const docsById = Object.fromEntries(docsIndex.docs.map(doc => [doc.id, doc]));

    assert.deepEqual(groups, ['入门', '架构', 'Core', 'Tool', 'Reference']);

    for (const id of ['overview', 'quick-start']) {
        assert.equal(groupById[id], '入门');
    }

    assert.equal(groupById.architecture, '架构');
    assert.ok(!docsIndex.docs.some(doc => doc.id === 'api-reference'));

    const coreIds = docsIndex.docs.filter(doc => doc.group === 'Core').map(doc => doc.id);
    const toolIds = docsIndex.docs.filter(doc => doc.group === 'Tool').map(doc => doc.id);
    const referenceIds = docsIndex.docs.filter(doc => doc.group === 'Reference').map(doc => doc.id);

    assert.deepEqual(coreIds, ['codegenkit', 'eventkit', 'fsmkit', 'poolkit', 'reskit', 'singletonkit']);
    assert.deepEqual(toolIds, ['actionkit', 'audiokit', 'localizationkit', 'savekit', 'scenekit', 'spatialkit', 'tablekit', 'uikit']);
    assert.deepEqual(referenceIds, ['third-party-recommendations', 'third-party-index']);

    for (const id of ['eventkit', 'fsmkit', 'poolkit', 'reskit', 'singletonkit', 'codegenkit']) {
        assert.equal(groupById[id], 'Core');
    }

    for (const id of ['actionkit', 'audiokit', 'uikit', 'savekit', 'localizationkit', 'scenekit', 'spatialkit', 'tablekit']) {
        assert.equal(groupById[id], 'Tool');
    }

    assert.equal(groupById['third-party-recommendations'], 'Reference');
    assert.equal(groupById['third-party-index'], 'Reference');

    assert.equal(docsById.eventkit.title, 'EventKit 事件');
    assert.equal(docsById.eventkit.navTitle, 'EventKit');

    for (const [id, label] of Object.entries({
        fsmkit: 'FsmKit',
        poolkit: 'PoolKit',
        reskit: 'ResKit',
        singletonkit: 'SingletonKit',
        tablekit: 'TableKit',
        actionkit: 'ActionKit',
        audiokit: 'AudioKit',
        uikit: 'UIKit',
        savekit: 'SaveKit',
        localizationkit: 'LocalizationKit',
        scenekit: 'SceneKit',
        spatialkit: 'SpatialKit',
    })) {
        assert.equal(docsById[id].navTitle, label);
    }

    for (const id of [
        'overview',
        'quick-start',
        'architecture',
        'eventkit',
        'fsmkit',
        'poolkit',
        'reskit',
        'singletonkit',
        'actionkit',
        'audiokit',
        'localizationkit',
        'savekit',
        'scenekit',
        'spatialkit',
        'tablekit',
        'uikit',
        'third-party-recommendations',
        'third-party-index',
    ]) {
        assert.ok(docsById[id].icon, `${id} should declare a nav icon`);
    }

    assert.equal(docsById['third-party-recommendations'].title, '第三方库推荐');
    assert.equal(docsById['third-party-index'].title, '第三方库索引');

    assert.match(js, /function getDocNavTitle\(/);
    assert.match(js, /getDocNavTitle\(d\)/);
    assert.match(js, /function getDocIconName\(/);
    assert.match(js, /function getDocIconTone\(/);
    assert.match(js, /doc-nav-item-icon/);
    assert.match(js, /data-doc-icon-tone/);
    assert.match(js, /svgIcon\(getDocIconName\(d\),\s*'doc-nav-item-svg'\)/);
    assert.match(css, /\.doc-nav-item-icon\s*\{[\s\S]*?color:\s*var\(--kit-icon-color/);
    assert.match(css, /\.doc-nav-item-svg/);
    assert.doesNotMatch(js, /doc-nav-item-summary/);
});

test('reference docs restore the 1.0 third-party library index without BuffKit', () => {
    const docsIndex = JSON.parse(readDistFile('docs/index.json'));
    const recommendationDoc = readDistFile('docs/third-party-recommendations.md');
    const indexDoc = readDistFile('docs/third-party-index.md');
    const docsBundle = [JSON.stringify(docsIndex), recommendationDoc, indexDoc].join('\n');

    assert.ok(docsIndex.docs.some(doc => doc.id === 'third-party-recommendations' && doc.group === 'Reference'));
    assert.ok(docsIndex.docs.some(doc => doc.id === 'third-party-index' && doc.group === 'Reference'));
    for (const libraryName of ['UniTask', 'YooAsset', 'Luban', 'FMOD', 'DOTween', 'Unity Input System', 'ZString', 'Nino']) {
        assert.match(indexDoc, new RegExp(libraryName.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')));
    }
    assert.match(recommendationDoc, /AIBridge/);
    assert.match(recommendationDoc, /Unity MCP/);
    assert.match(recommendationDoc, /文件 I\/O/);
    assert.match(indexDoc, /YOKIFRAME_UNITASK_SUPPORT/);
    assert.match(indexDoc, /YOKIFRAME_YOOASSET_SUPPORT/);
    assert.match(indexDoc, /YOKIFRAME_LUBAN_SUPPORT/);
    assert.match(indexDoc, /YOKIFRAME_NINO_SUPPORT/);
    assert.doesNotMatch(docsBundle, /BuffKit|buffkit/);
});
