// pages/audiokit-index.js
// AudioKit ID 生成器：本地配置、扫描、生成和路径选择。
const AUDIOKIT_INDEX_CONFIG_STORAGE_KEY = 'yokiframe.audiokit.indexGenerator.v1';
const AUDIOKIT_INDEX_PREVIEW_LIMIT = 80;
const AUDIOKIT_INDEX_DEFAULT_CONFIG = Object.freeze({
    scanFolder: 'Assets/Audio',
    outputPath: 'Assets/Scripts/Generated/AudioIds.cs',
    namespaceName: 'Game',
    className: 'AudioIds',
    startId: 1001,
});

let audioKitIndexStorageScope = null;
let audioKitIndexConfig = sanitizeAudioKitIndexConfig({});
const audioKitIndexState = {
    entries: [],
    log: 'AudioKit ID 生成器已准备就绪，扫描后会生成 AudioIds 与 AudioPaths.Map。',
    generatedFile: '',
    busy: '',
};

syncAudioKitProjectStorageScope({ force: true });

function getAudioKitIndexConfigStorageKey() {
    return getProjectScopedStorageKey(AUDIOKIT_INDEX_CONFIG_STORAGE_KEY);
}

function syncAudioKitProjectStorageScope(options = {}) {
    const nextScope = getProjectStorageScopeIdentifier();
    if (!options.force && audioKitIndexStorageScope === nextScope) return false;

    audioKitIndexStorageScope = nextScope;
    audioKitIndexConfig = loadAudioKitIndexConfig();
    if (!options.force) {
        audioKitIndexState.entries = [];
        audioKitIndexState.generatedFile = '';
        audioKitIndexState.busy = '';
        audioKitIndexState.log = '已切换到当前项目的 AudioKit ID 生成器配置。';
    }
    return true;
}

function loadAudioKitIndexConfig() {
    try {
        const raw = readProjectScopedStorageItem(AUDIOKIT_INDEX_CONFIG_STORAGE_KEY);
        if (raw) return sanitizeAudioKitIndexConfig(JSON.parse(raw));
    } catch (_) {
        // 损坏的本地配置不应阻断 AudioKit 页面。
    }
    return sanitizeAudioKitIndexConfig({});
}

function persistAudioKitIndexConfig() {
    audioKitIndexConfig = sanitizeAudioKitIndexConfig(audioKitIndexConfig);
    localStorage.setItem(getAudioKitIndexConfigStorageKey(), JSON.stringify(audioKitIndexConfig));
}

function sanitizeAudioKitIndexConfig(raw) {
    const config = { ...AUDIOKIT_INDEX_DEFAULT_CONFIG };
    if (raw && typeof raw === 'object') {
        Object.keys(AUDIOKIT_INDEX_DEFAULT_CONFIG).forEach(key => {
            if (Object.prototype.hasOwnProperty.call(raw, key)) config[key] = raw[key];
        });
    }
    config.scanFolder = normalizeAudioKitIndexString(config.scanFolder, AUDIOKIT_INDEX_DEFAULT_CONFIG.scanFolder);
    config.outputPath = normalizeAudioKitIndexString(config.outputPath, AUDIOKIT_INDEX_DEFAULT_CONFIG.outputPath);
    config.namespaceName = normalizeAudioKitIndexString(config.namespaceName, AUDIOKIT_INDEX_DEFAULT_CONFIG.namespaceName);
    config.className = normalizeAudioKitIndexString(config.className, AUDIOKIT_INDEX_DEFAULT_CONFIG.className);
    config.startId = normalizeAudioKitIndexStartId(config.startId);
    return config;
}

function normalizeAudioKitIndexString(value, fallback) {
    const text = String(value ?? '').trim();
    return text || fallback;
}

function normalizeAudioKitIndexStartId(value) {
    const number = Number(value);
    return Number.isInteger(number) ? number : AUDIOKIT_INDEX_DEFAULT_CONFIG.startId;
}

function renderAudioKitIndexGenerator() {
    const projectRoot = getAudioKitProjectRoot() || '--';
    const isBusy = !!audioKitIndexState.busy;
    const canRun = !!invoke && !isBusy;
    const scanDisabled = canRun ? '' : 'disabled';
    const generateDisabled = canRun ? '' : 'disabled';
    const busyText = audioKitIndexState.busy === 'scan'
        ? '扫描中'
        : (audioKitIndexState.busy === 'generate' ? '生成中' : 'Tauri 执行');

    return `<section class="kit-panel audiokit-index-generator">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('audio', '音频 ID 生成')}</div>
                <div class="kit-panel__desc">在 Tauri 中索引音频文件夹，生成 ID 常量和 <code>AudioPaths.Map</code>。</div>
            </div>
            <span class="kit-state-pill ${invoke ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${escapeHtml(busyText)}</span>
        </div>
        <div class="audiokit-index-layout">
            <div class="audiokit-index-form">
                ${renderAudioKitIndexTextField('scanFolder', '扫描文件夹', audioKitIndexConfig.scanFolder, 'Assets 下的音频目录')}
                ${renderAudioKitIndexTextField('outputPath', '输出文件', audioKitIndexConfig.outputPath, '生成 AudioIds.cs 的目标路径')}
                <div class="audiokit-index-form__row audiokit-index-form__row--triple">
                    ${renderAudioKitIndexInlineField('namespaceName', '命名空间', audioKitIndexConfig.namespaceName)}
                    ${renderAudioKitIndexInlineField('className', '类名', audioKitIndexConfig.className)}
                    ${renderAudioKitIndexInlineField('startId', '起始 ID', audioKitIndexConfig.startId, 'number')}
                </div>
                <div class="audiokit-index-actions">
                    <button class="btn btn-secondary btn-sm" type="button" data-audiokit-index-action="reset" ${isBusy ? 'disabled' : ''}>还原默认</button>
                    <button class="btn btn-secondary btn-sm" type="button" data-audiokit-index-action="scan" ${scanDisabled}>扫描</button>
                    <button class="btn btn-primary btn-sm" type="button" data-audiokit-index-action="generate" ${generateDisabled}>生成</button>
                </div>
                <div class="audiokit-index-meta">
                    <span>Project</span><code>${escapeHtml(projectRoot)}</code>
                    <span>Result</span><code>${escapeHtml(audioKitIndexState.generatedFile || audioKitIndexConfig.outputPath)}</code>
                </div>
                <div class="audiokit-index-log">${escapeHtml(audioKitIndexState.log || '--')}</div>
            </div>
            ${renderAudioKitIndexPreview(audioKitIndexState.entries)}
        </div>
    </section>`;
}

function renderAudioKitIndexTextField(field, label, value, hint) {
    const picker = field === 'scanFolder'
        ? `<button class="btn btn-secondary btn-sm" type="button" data-audiokit-index-pick="${field}">选择</button>`
        : '';
    return `<label class="audiokit-index-form__row">
        <span>${escapeHtml(label)}</span>
        <input type="text" value="${escapeHtml(value)}" data-audiokit-index-field="${field}">
        ${picker}
        <em>${escapeHtml(hint)}</em>
    </label>`;
}

function renderAudioKitIndexInlineField(field, label, value, type = 'text') {
    return `<label>
        <span>${escapeHtml(label)}</span>
        <input type="${escapeHtml(type)}" value="${escapeHtml(value)}" data-audiokit-index-field="${field}">
    </label>`;
}

function renderAudioKitIndexPreview(entries) {
    const normalized = Array.isArray(entries) ? entries : [];
    if (!normalized.length) {
        return `<div class="audiokit-index-preview">
            ${emptyState('audio', '尚未扫描音频文件。')}
        </div>`;
    }
    const rows = normalized.slice(0, AUDIOKIT_INDEX_PREVIEW_LIMIT).map(entry => `<div class="audiokit-index-row">
        <strong>${escapeHtml(entry.constantName ?? entry.constant_name ?? '--')}</strong>
        <span>${escapeHtml(entry.id ?? '--')}</span>
        <code>${escapeHtml(entry.path ?? '--')}</code>
    </div>`).join('');
    const more = normalized.length > AUDIOKIT_INDEX_PREVIEW_LIMIT
        ? `<div class="audiokit-index-row audiokit-index-row--more">+${escapeHtml(normalized.length - AUDIOKIT_INDEX_PREVIEW_LIMIT)} 个文件</div>`
        : '';
    return `<div class="audiokit-index-preview">
        <div class="audiokit-index-preview__head">
            <strong>扫描结果</strong>
            <span>${escapeHtml(normalized.length)} 个文件</span>
        </div>
        <div class="audiokit-index-preview__rows" data-kit-scroll-key="audiokit-index-preview">${rows}${more}</div>
    </div>`;
}

function bindAudioKitIndexGeneratorActions() {
    $pageBody.querySelectorAll('[data-audiokit-index-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('change', () => updateAudioKitIndexConfigField(input.dataset.audiokitIndexField, input.value));
    });
    bindKitButtonClick('[data-audiokit-index-action="reset"]', resetAudioKitIndexConfig);
    bindKitButtonClick('[data-audiokit-index-action="scan"]', () => void scanAudioKitIndexFiles());
    bindKitButtonClick('[data-audiokit-index-action="generate"]', () => void generateAudioKitIndexCode());
    bindKitButtonClick('[data-audiokit-index-pick="scanFolder"]', () => void pickAudioKitIndexScanFolder());
}

function updateAudioKitIndexConfigField(field, value) {
    if (!Object.prototype.hasOwnProperty.call(AUDIOKIT_INDEX_DEFAULT_CONFIG, field)) return;
    audioKitIndexConfig = sanitizeAudioKitIndexConfig({ ...audioKitIndexConfig, [field]: value });
    if (field === 'scanFolder' || field === 'startId') {
        audioKitIndexState.entries = [];
        audioKitIndexState.generatedFile = '';
    }
    persistAudioKitIndexConfig();
    renderAudioKitCachedPage();
}

function resetAudioKitIndexConfig() {
    if (audioKitIndexState.busy) return;
    audioKitIndexConfig = sanitizeAudioKitIndexConfig({});
    audioKitIndexState.entries = [];
    audioKitIndexState.generatedFile = '';
    audioKitIndexState.log = '已还原 AudioKit ID 生成器默认配置。';
    persistAudioKitIndexConfig();
    renderAudioKitCachedPage();
}

async function pickAudioKitIndexScanFolder() {
    if (!invoke || audioKitIndexState.busy) return;
    const projectRoot = getAudioKitProjectRoot();
    const initialPath = projectRoot
        ? resolveAudioKitProjectPath(projectRoot, audioKitIndexConfig.scanFolder)
        : audioKitIndexConfig.scanFolder;
    try {
        const selected = await invoke('pick_folder', { initialPath, projectRoot });
        if (!selected) return;
        updateAudioKitIndexConfigField('scanFolder', normalizeAudioKitPickedPath(selected, projectRoot));
    } catch (error) {
        audioKitIndexState.log = `选择扫描文件夹失败：${formatAudioKitIndexError(error)}`;
        renderAudioKitCachedPage();
    }
}

async function scanAudioKitIndexFiles() {
    if (!invoke || audioKitIndexState.busy) return;
    audioKitIndexState.busy = 'scan';
    audioKitIndexState.log = '正在扫描 AudioKit 音频文件...';
    renderAudioKitCachedPage();
    try {
        const result = normalizeAudioKitIndexResult(await invoke('audiokit_scan_audio_files', {
            projectRoot: getAudioKitProjectRoot(),
            scanFolder: audioKitIndexConfig.scanFolder,
            startId: audioKitIndexConfig.startId,
        }));
        audioKitIndexState.entries = result.entries;
        audioKitIndexState.generatedFile = '';
        audioKitIndexState.log = result.log || `已扫描 ${result.entries.length} 个音频文件。`;
    } catch (error) {
        audioKitIndexState.entries = [];
        audioKitIndexState.log = `扫描失败：${formatAudioKitIndexError(error)}`;
    } finally {
        audioKitIndexState.busy = '';
        renderAudioKitCachedPage();
    }
}

async function generateAudioKitIndexCode() {
    if (!invoke || audioKitIndexState.busy) return;
    audioKitIndexState.busy = 'generate';
    audioKitIndexState.log = '正在生成 AudioIds.cs...';
    renderAudioKitCachedPage();
    try {
        const result = normalizeAudioKitIndexResult(await invoke('audiokit_generate_audio_ids', {
            projectRoot: getAudioKitProjectRoot(),
            config: audioKitIndexConfig,
        }));
        audioKitIndexState.entries = result.entries;
        audioKitIndexState.generatedFile = result.generatedFile || audioKitIndexConfig.outputPath;
        audioKitIndexState.log = result.log || `已生成 ${result.entries.length} 个音频 ID。`;
    } catch (error) {
        audioKitIndexState.log = `生成失败：${formatAudioKitIndexError(error)}`;
    } finally {
        audioKitIndexState.busy = '';
        renderAudioKitCachedPage();
    }
}

function normalizeAudioKitIndexResult(raw) {
    if (!raw) return { success: false, log: 'AudioKit 后端没有返回结果。', entries: [] };
    if (typeof raw === 'string') {
        try {
            return normalizeAudioKitIndexResult(JSON.parse(raw));
        } catch (_) {
            return { success: true, log: raw, entries: [] };
        }
    }
    return {
        success: raw.success !== false,
        log: String(raw.log ?? ''),
        entries: Array.isArray(raw.entries) ? raw.entries : [],
        generatedFile: raw.generatedFile || raw.generated_file || '',
    };
}

function getAudioKitProjectRoot() {
    const summaryProjectRoot = latestStatusSummary?.projectPath && latestStatusSummary.projectPath !== '--'
        ? normalizeAudioKitPath(latestStatusSummary.projectPath)
        : '';
    if (summaryProjectRoot) return summaryProjectRoot;

    const engines = Array.isArray(latestStatusRaw?.engines) ? latestStatusRaw.engines : [];
    const engine = engines.find(candidate => candidate?.projectPath && candidate.connected !== false)
        || engines.find(candidate => candidate?.projectPath);
    const projectRoot = engine?.projectPath && engine.projectPath !== '--' ? normalizeAudioKitPath(engine.projectPath) : '';
    return projectRoot || null;
}

function resolveAudioKitProjectPath(projectRoot, value) {
    const path = normalizeAudioKitPath(value);
    if (isAudioKitAbsolutePath(path)) return path;
    return joinAudioKitPath(projectRoot, path);
}

function normalizeAudioKitPickedPath(value, projectRoot) {
    const path = normalizeAudioKitPath(value).trim();
    if (!path) return '';

    const root = normalizeAudioKitPath(projectRoot || '').replace(/\/+$/, '');
    if (!root || !isAudioKitAbsolutePath(path) || !isAudioKitAbsolutePath(root)) {
        return path;
    }

    const pathKey = path.toLowerCase();
    const rootKey = root.toLowerCase();
    if (pathKey === rootKey) return '.';
    const prefix = root.endsWith('/') ? root : `${root}/`;
    if (pathKey.startsWith(prefix.toLowerCase())) {
        return path.slice(prefix.length);
    }
    return path;
}

function joinAudioKitPath(left, right) {
    const a = normalizeAudioKitPath(left).replace(/\/+$/, '');
    const b = normalizeAudioKitPath(right).replace(/^\/+/, '');
    if (!a) return b;
    if (!b) return a;
    return `${a}/${b}`;
}

function isAudioKitAbsolutePath(path) {
    return /^[A-Za-z]:\//.test(path) || path.startsWith('/') || path.startsWith('//');
}

function normalizeAudioKitPath(path) {
    return String(path ?? '').replace(/\\/g, '/');
}

function formatAudioKitIndexError(error) {
    return String(error?.message ?? error ?? '未知错误');
}
