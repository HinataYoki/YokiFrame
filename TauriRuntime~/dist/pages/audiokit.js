// pages/audiokit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：AudioKit
// ═══════════════════════════════════════════════════════════════════
const audioKitState = {
    stats: {},
    buses: [],
    voices: [],
    history: [],
    searchTerm: '',
    selectedBus: 'All',
    selectedVoiceId: null,
    renderSignature: '',
};

const audioKitPageState = { renderSignature: '' };
const AUDIOKIT_INDEX_CONFIG_STORAGE_KEY = 'yokiframe.audiokit.indexGenerator.v1';
const AUDIOKIT_INDEX_PREVIEW_LIMIT = 80;
const AUDIOKIT_INDEX_DEFAULT_CONFIG = Object.freeze({
    scanFolder: 'Assets/Audio',
    outputPath: 'Assets/Scripts/Generated/AudioIds.cs',
    namespaceName: 'Game',
    className: 'AudioIds',
    startId: 1001,
});
let audioKitIndexConfig = loadAudioKitIndexConfig();
const audioKitIndexState = {
    entries: [],
    log: 'AudioKit ID 生成器已准备就绪，扫描后会生成 AudioIds 与 AudioPaths.Map。',
    generatedFile: '',
    busy: '',
};

const AUDIO_STRIP_DETAIL_LIMIT = 12;

function renderAudioKitPage() {
    $pageBody.classList.add('content-body--audiokit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('audiokit.title'),
        t('audiokit.subtitle'),
        t('audiokit.tab'),
        'audio',
        `<button class="btn btn-sm" onclick="clearAudioKitHistory()">${t('poolkit.clear_history')}</button><button class="btn btn-sm" onclick="stopAllAudioKit()">${t('common.all')}</button><button class="btn btn-primary btn-sm" onclick="refreshAudioKit()">${t('common.refresh')}</button>`
    );
    clearTabs();
    audioKitState.renderSignature = '';
    audioKitPageState.renderSignature = '';
    loadAudioKitWorkbench();
}

async function refreshAudioKit() { loadAudioKitWorkbench(); }

async function refreshAudioKitReactive(event) {
    await loadAudioKitWorkbench();
}

function normalizeAudioKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const busesSource = source.buses ?? {};
    const voicesSource = source.voices ?? {};
    const historySource = source.history ?? {};
    return {
        stats: source.stats ?? {},
        buses: Array.isArray(source.buses) ? source.buses : (Array.isArray(busesSource.buses) ? busesSource.buses : []),
        voices: Array.isArray(source.voices) ? source.voices : (Array.isArray(voicesSource.voices) ? voicesSource.voices : []),
        history: Array.isArray(source.history) ? source.history : (Array.isArray(historySource.history) ? historySource.history : []),
    };
}

async function fetchAudioKitWorkbenchState() {
    const telemetry = await readKitTelemetryData('AudioKit');
    if (telemetry) return normalizeAudioKitStatePayload(telemetry);

    const snapshot = await readKitSnapshotData('AudioKit');
    if (snapshot) return normalizeAudioKitStatePayload(snapshot);

    return null;
}

async function fetchAudioKitWorkbenchStateFromCommands() {
    const snapshot = await sendKitCommandData('AudioKit', 'get_workbench_snapshot');
    return normalizeAudioKitStatePayload(snapshot);
}

async function loadAudioKitWorkbench() {
    if (!invoke || !connected) {
        renderAudioKitPageContent(emptyState('audio', '请连接引擎后查看音频播放状态。'));
        return;
    }
    try {
        const snapshotState = await fetchAudioKitWorkbenchState();
        const state = snapshotState ?? await fetchAudioKitWorkbenchStateFromCommands();
        audioKitState.stats = state.stats;
        audioKitState.buses = state.buses;
        audioKitState.voices = state.voices;
        audioKitState.history = state.history;
        reconcileAudioKitBusSelection(audioKitState.buses, audioKitState.voices);
        reconcileAudioKitSelection(audioKitState.voices);
        clearMetrics();

        renderAudioKitPageContent(renderAudioKitWorkbench(audioKitState.stats, audioKitState.buses, audioKitState.voices, audioKitState.history));
    } catch (e) {
        if (!canSendRuntimeKitCommand('AudioKit')) {
            renderAudioKitPageContent(emptyState('audio', '当前宿主未开放 AudioKit 运行时命令。'));
            return;
        }
        renderAudioKitPageContent(panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!'));
    }
}

function renderAudioKitPageContent(runtimeHtml) {
    clearMetrics();
    audioKitIndexConfig = sanitizeAudioKitIndexConfig(audioKitIndexConfig);
    const html = `<div class="audiokit-page-workbench">
        <div class="audiokit-page-runtime">${runtimeHtml}</div>
        ${renderAudioKitIndexGenerator()}
    </div>`;
    const signature = makeStableSignature({
        stats: audioKitState.stats,
        buses: audioKitState.buses,
        voices: audioKitState.voices,
        history: audioKitState.history,
        selectedBus: audioKitState.selectedBus,
        selected: audioKitState.selectedVoiceId,
        indexConfig: audioKitIndexConfig,
        indexEntries: audioKitIndexState.entries,
        indexLog: audioKitIndexState.log,
        indexBusy: audioKitIndexState.busy,
        generatedFile: audioKitIndexState.generatedFile,
        runtimeHtml,
    });
    renderWorkbenchHtmlStable(audioKitPageState, html, signature, bindAudioKitPageActions);
}

function renderAudioKitCachedPage() {
    renderAudioKitPageContent(renderAudioKitWorkbench(audioKitState.stats, audioKitState.buses, audioKitState.voices, audioKitState.history));
}

function loadAudioKitIndexConfig() {
    try {
        const raw = localStorage.getItem(AUDIOKIT_INDEX_CONFIG_STORAGE_KEY);
        if (raw) return sanitizeAudioKitIndexConfig(JSON.parse(raw));
    } catch (_) {
        // 损坏的本地配置不应阻断 AudioKit 页面。
    }
    return sanitizeAudioKitIndexConfig({});
}

function persistAudioKitIndexConfig() {
    audioKitIndexConfig = sanitizeAudioKitIndexConfig(audioKitIndexConfig);
    localStorage.setItem(AUDIOKIT_INDEX_CONFIG_STORAGE_KEY, JSON.stringify(audioKitIndexConfig));
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

function bindAudioKitPageActions() {
    bindAudioKitIndexGeneratorActions();
    bindAudioKitWorkbenchActions();
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

function reconcileAudioKitSelection(voices) {
    if (!Array.isArray(voices) || !voices.length) {
        audioKitState.selectedVoiceId = null;
        return null;
    }
    let selected = voices.find(item => String(item.voiceId) === String(audioKitState.selectedVoiceId));
    if (!selected) {
        selected = voices[0];
        audioKitState.selectedVoiceId = selected.voiceId;
    }
    return selected;
}

function reconcileAudioKitBusSelection(buses, voices) {
    if (audioKitState.selectedBus === 'All') return;
    const hasBus = (Array.isArray(buses) ? buses : []).some(item => String(item.name) === String(audioKitState.selectedBus))
        || (Array.isArray(voices) ? voices : []).some(item => String(item.bus) === String(audioKitState.selectedBus));
    if (!hasBus) {
        audioKitState.selectedBus = 'All';
    }
}

function renderAudioKitWorkbench(stats, buses, voices, history) {
    const normalizedBuses = normalizeAudioKitBuses(stats, buses, voices);
    const masterBus = normalizedBuses.find(item => item.isMaster) ?? { name: 'Master', volume: stats?.masterVolume ?? 1, effectiveVolume: stats?.masterVolume ?? 1, muted: false, activeVoiceCount: voices.length };
    return `<div class="kit-workbench kit-workbench--audio">
        <section class="audio-master-strip">
            <div class="audio-master-strip__main">
                <div class="kit-toolbar__title">${renderKitTitle('audio', '音频混音台')}</div>
                <div class="kit-toolbar__meta">Backend: ${escapeHtml(stats?.backendName ?? 'None')} · Bus ${escapeHtml(normalizedBuses.length)} · 活跃 ${escapeHtml(stats?.activeVoiceCount ?? voices.length ?? 0)} · 历史 ${escapeHtml(stats?.historyCount ?? history.length ?? 0)}</div>
            </div>
            <label class="audio-master-control">
                <span>Master</span>
                <input type="range" min="0" max="1" step="0.01" value="${escapeHtml(normalizeAudioVolume(masterBus.volume))}" data-audiokit-master-volume>
                <strong>${formatAudioVolume(masterBus.effectiveVolume ?? masterBus.volume)}</strong>
            </label>
            <div class="kit-toolbar__actions">
                <button class="btn btn-sm" data-audiokit-master-mute="${masterBus.muted ? 'false' : 'true'}">${masterBus.muted ? '取消静音' : '静音'}</button>
                <button class="btn btn-sm" data-audiokit-clear-history>清空历史</button>
                <button class="btn btn-sm" data-audiokit-stop-all>停止全部</button>
            </div>
        </section>
        <section class="audio-mixer-console">
            <div class="kit-panel__head">
                <div>
                    <div class="kit-panel__title">${renderKitTitle('audio', '实时混音台')}</div>
                    <div class="kit-panel__desc">通道横向滚动显示；AudioChannel 是内置快捷入口，自定义音量通道使用 string AudioBus。</div>
                </div>
                <span class="kit-panel__count">${escapeHtml(normalizedBuses.length)}</span>
            </div>
            <div class="audio-channel-rail" data-kit-scroll-key="audio-buses">${renderAudioChannelStrips(normalizedBuses, voices, history)}</div>
        </section>
    </div>`;
}

function normalizeAudioKitBuses(stats, buses, voices) {
    const normalized = [];
    const add = (bus) => {
        if (!bus?.name) return;
        if (normalized.some(item => String(item.name).toLowerCase() === String(bus.name).toLowerCase())) return;
        normalized.push({
            name: bus.name,
            volume: Number.isFinite(Number(bus.volume)) ? Number(bus.volume) : 1,
            effectiveVolume: Number.isFinite(Number(bus.effectiveVolume)) ? Number(bus.effectiveVolume) : (Number.isFinite(Number(bus.volume)) ? Number(bus.volume) : 1),
            muted: !!bus.muted,
            isMaster: !!bus.isMaster || String(bus.name).toLowerCase() === 'master',
            isDefault: !!bus.isDefault,
            activeVoiceCount: Number(bus.activeVoiceCount ?? 0) || 0
        });
    };
    [
        { name: 'Master', volume: stats?.masterVolume ?? 1, effectiveVolume: stats?.masterVolume ?? 1, isMaster: true, isDefault: true },
        { name: 'Music', volume: stats?.musicVolume ?? 1, effectiveVolume: stats?.musicVolume ?? 1, isDefault: true },
        { name: 'Sfx', volume: stats?.sfxVolume ?? 1, effectiveVolume: stats?.sfxVolume ?? 1, isDefault: true },
        { name: 'Voice', volume: stats?.voiceVolume ?? 1, effectiveVolume: stats?.voiceVolume ?? 1, isDefault: true },
        { name: 'Ambience', volume: stats?.ambienceVolume ?? 1, effectiveVolume: stats?.ambienceVolume ?? 1, isDefault: true },
        { name: 'UI', volume: stats?.uiVolume ?? 1, effectiveVolume: stats?.uiVolume ?? 1, isDefault: true },
    ].forEach(add);
    (Array.isArray(buses) ? buses : []).forEach(add);
    (Array.isArray(voices) ? voices : []).forEach(voice => add({ name: voice.bus, volume: 1, effectiveVolume: 1, activeVoiceCount: 0 }));
    normalized.forEach(bus => {
        if (bus.isMaster) {
            bus.activeVoiceCount = Array.isArray(voices) ? voices.length : bus.activeVoiceCount;
            return;
        }
        bus.activeVoiceCount = (Array.isArray(voices) ? voices : []).filter(voice => String(voice.bus).toLowerCase() === String(bus.name).toLowerCase()).length;
    });
    return normalized.sort((a, b) => audioBusSortOrder(a.name) - audioBusSortOrder(b.name) || String(a.name).localeCompare(String(b.name)));
}

function audioBusSortOrder(name) {
    const key = String(name || '').toLowerCase();
    if (key === 'master') return 0;
    if (key === 'music') return 1;
    if (key === 'sfx') return 2;
    if (key === 'voice') return 3;
    if (key === 'ambience') return 4;
    if (key === 'ui') return 5;
    return 100;
}

function renderAudioChannelStrips(buses, voices, history) {
    const allActive = audioKitState.selectedBus === 'All';
    const allStrip = `<button class="audio-channel-strip audio-channel-strip--all${allActive ? ' active' : ''}" type="button" data-audiokit-channel-strip="All" data-audiokit-voice-bus-filter="All">
        <div class="audio-strip-head">
            <strong>ALL</strong>
            <span>${escapeHtml(Array.isArray(voices) ? voices.length : 0)}</span>
        </div>
        <div class="audio-strip-actions">
            <span class="audio-strip-chip">全部</span>
            <span class="audio-strip-chip">Voices</span>
        </div>
        <div class="audio-strip-meter"><i style="height:${escapeHtml(formatAudioMeterWidth(1))}"></i></div>
        <div class="audio-strip-value">全部播放声部</div>
        <div class="audio-strip-playback">${renderAudioStripPlayback(voices)}</div>
        <div class="audio-strip-history">${renderAudioStripHistory(history)}</div>
    </button>`;
    return allStrip + (Array.isArray(buses) ? buses : []).map(bus => {
        const selected = String(bus.name) === String(audioKitState.selectedBus);
        const volume = normalizeAudioVolume(bus.volume);
        const effective = normalizeAudioVolume(bus.effectiveVolume);
        const busVoices = filterAudioKitVoicesByBus(voices, bus.name);
        const busHistory = filterAudioKitHistoryByBus(history, bus.name);
        return `<div class="audio-channel-strip${selected ? ' active' : ''}" data-audiokit-channel-strip="${escapeHtml(bus.name)}">
            <button class="audio-strip-select" type="button" data-audiokit-voice-bus-filter="${escapeHtml(bus.name)}">
                <span class="audio-strip-head">
                    <strong>${escapeHtml(formatAudioBusLabel(bus.name))}</strong>
                    <span>${escapeHtml(bus.activeVoiceCount ?? 0)}</span>
                </span>
                <em>${bus.isDefault ? 'Default Bus' : 'Custom Bus'}</em>
            </button>
            <div class="audio-strip-actions">
                <button class="audio-strip-button" type="button" data-audiokit-bus-mute="${escapeHtml(bus.name)}" data-audiokit-bus-muted="${bus.muted ? 'false' : 'true'}">${bus.muted ? '取消 M' : 'M'}</button>
                <button class="audio-strip-button audio-strip-button--danger" type="button" data-audiokit-stop-bus="${escapeHtml(bus.name)}" ${bus.isMaster ? 'disabled' : ''}>S</button>
            </div>
            <div class="audio-strip-body">
                <div class="audio-strip-meter"><i style="height:${escapeHtml(formatAudioMeterWidth(effective))}"></i></div>
                <label class="audio-strip-fader">
                    <input type="range" min="0" max="1" step="0.01" value="${escapeHtml(volume)}" data-audiokit-bus-volume="${escapeHtml(bus.name)}" ${bus.isMaster ? 'disabled' : ''}>
                </label>
            </div>
            <div class="audio-strip-value">${formatAudioVolume(effective)}</div>
            <div class="audio-strip-playback">${renderAudioStripPlayback(busVoices)}</div>
            <div class="audio-strip-history">${renderAudioStripHistory(busHistory)}</div>
        </div>`;
    }).join('');
}

function filterAudioKitVoicesByBus(voices, busName) {
    if (String(busName).toLowerCase() === 'master') return Array.isArray(voices) ? voices : [];
    return (Array.isArray(voices) ? voices : []).filter(voice => String(voice.bus).toLowerCase() === String(busName).toLowerCase());
}

function filterAudioKitHistoryByBus(history, busName) {
    if (String(busName).toLowerCase() === 'master') return Array.isArray(history) ? history : [];
    return (Array.isArray(history) ? history : []).filter(item => String(item.bus).toLowerCase() === String(busName).toLowerCase());
}

function renderAudioStripPlayback(voices) {
    if (!Array.isArray(voices) || !voices.length) {
        return '<strong>正在播放</strong><span>未运行</span>';
    }
    const rows = voices.slice(0, AUDIO_STRIP_DETAIL_LIMIT).map(voice => {
        const title = voice.clipName ?? voice.path ?? ('Voice ' + (voice.voiceId ?? '--'));
        const state = voice.isPlaying === false ? 'Paused' : 'Playing';
        const ratio = getAudioVoiceProgressRatio(voice);
        const progress = formatAudioProgress(voice);
        const progressPercent = formatAudioProgressPercent(voice);
        return `<span class="audio-strip-playback-item">
            <span class="audio-strip-playback-title">${escapeHtml(title)}</span>
            <em>${escapeHtml(state)} · ${escapeHtml(progress)} · ${escapeHtml(progressPercent)}</em>
            <i class="audio-strip-progress" aria-hidden="true"><b style="width:${escapeHtml(formatAudioMeterWidth(ratio))}"></b></i>
        </span>`;
    }).join('');
    const more = voices.length > AUDIO_STRIP_DETAIL_LIMIT ? `<span>+${escapeHtml(voices.length - AUDIO_STRIP_DETAIL_LIMIT)} 个 Voice</span>` : '';
    return `<strong>正在播放</strong>${rows}${more}`;
}

function renderAudioStripHistory(history) {
    if (!Array.isArray(history) || !history.length) {
        return '<strong>历史</strong><span>暂无历史</span>';
    }
    const rows = history.slice(0, AUDIO_STRIP_DETAIL_LIMIT).map(renderAudioStripHistoryItem).join('');
    const more = history.length > AUDIO_STRIP_DETAIL_LIMIT ? `<span>+${escapeHtml(history.length - AUDIO_STRIP_DETAIL_LIMIT)} 条记录</span>` : '';
    return `<strong>历史</strong>${rows}${more}`;
}

function renderAudioStripHistoryItem(item) {
    const label = formatAudioHistoryLabel(item);
    const title = formatAudioHistoryTitle(item);
    const meta = formatAudioHistoryMeta(item);
    const compactPath = formatAudioCompactPath(item.path);
    const fullTarget = item?.path ?? item?.clipName ?? item?.bus ?? '';
    const pathMarkup = compactPath ? `<i class="audio-strip-history-path">${escapeHtml(compactPath)}</i>` : '';
    return `<span class="audio-strip-history-item" title="${escapeHtml(fullTarget)}">
        <b class="audio-strip-history-badge">${escapeHtml(label)}</b>
        <span class="audio-strip-history-main">
            <span class="audio-strip-history-title">${escapeHtml(title)}</span>
            <em class="audio-strip-history-meta">${escapeHtml(meta)}</em>
            ${pathMarkup}
        </span>
    </span>`;
}

function formatAudioHistoryTitle(item) {
    const clipName = String(item?.clipName ?? '').trim();
    if (clipName) return clipName;
    const path = String(item?.path ?? '').replace(/\\/g, '/');
    const leaf = path.split('/').filter(Boolean).pop();
    if (leaf) return leaf.replace(/\.[^.]+$/, '');
    const bus = String(item?.bus ?? '').trim();
    if (bus) return formatAudioBusLabel(bus) + ' 通道';
    if (item?.voiceId !== undefined && item?.voiceId !== null && item?.voiceId !== '') {
        return 'Voice ' + item.voiceId;
    }
    return '--';
}

function formatAudioHistoryMeta(item) {
    const parts = [];
    const clock = formatAudioHistoryClock(item?.timestampUtc);
    if (clock && clock !== '--') parts.push(clock);
    if (item?.bus) parts.push(formatAudioBusLabel(item.bus));
    if (item?.voiceId !== undefined && item?.voiceId !== null && item?.voiceId !== '') {
        parts.push('V' + item.voiceId);
    }
    const volume = Number(item?.volume);
    if (Number.isFinite(volume)) parts.push('Vol ' + formatAudioVolume(volume));
    const fadeOutDuration = Number(item?.fadeOutDuration);
    if (Number.isFinite(fadeOutDuration) && fadeOutDuration > 0) {
        parts.push('Fade ' + formatAudioTime(fadeOutDuration));
    }
    if (item?.loop === true) parts.push('Loop');
    if (item?.is3D === true) parts.push('3D');
    return parts.length ? parts.join(' · ') : '--';
}

function formatAudioHistoryClock(value) {
    if (!value) return '--';
    const date = new Date(value);
    if (Number.isNaN(date.getTime())) return String(value);
    return date.toLocaleTimeString([], {
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false,
    });
}

function formatAudioCompactPath(path) {
    if (!path) return '';
    const normalized = String(path).replace(/\\/g, '/').replace(/\/+/g, '/');
    const parts = normalized.split('/').filter(Boolean);
    const compact = parts.slice(-3).join('/');
    if (compact.length <= 42) return compact;
    return '...' + compact.slice(-39);
}

function formatAudioBusLabel(name) {
    const key = String(name || '').toLowerCase();
    if (key === 'music') return 'BGM';
    if (key === 'sfx') return 'SFX';
    if (key === 'ambience') return 'AMBIENT';
    return String(name || '--').toUpperCase();
}

function filterAudioKitVoices(voices) {
    return (Array.isArray(voices) ? voices : []).filter(voice => kitSearchMatches(audioKitState.searchTerm, [
        voice.voiceId,
        voice.path,
        voice.clipName,
        voice.bus,
        voice.backendName,
        voice.is3D ? '3d' : '2d',
        voice.followTargetName,
        voice.rolloffMode,
    ]) && (audioKitState.selectedBus === 'All' || String(voice.bus) === String(audioKitState.selectedBus)));
}

function renderAudioVoiceRows(voices) {
    if (!Array.isArray(voices) || !voices.length) {
        return emptyState('audio', '暂无活跃声音。播放 AudioKit.PlaySfx 或 PlayMusic 后会显示。');
    }
    return voices.map(voice => {
        const selected = String(voice.voiceId) === String(audioKitState.selectedVoiceId);
        const mode = voice.is3D ? '3D' : '2D';
        return `<div class="audio-voice-row${selected ? ' active' : ''}" data-audiokit-voice="${escapeHtml(voice.voiceId ?? '')}">
            <span class="audio-voice-row__main">
                <strong>${escapeHtml(voice.path ?? voice.clipName ?? '--')}</strong>
                <em>${escapeHtml(voice.bus ?? '--')} · ${escapeHtml(mode)} · ${formatAudioTime(voice.elapsed)} / ${formatAudioTime(voice.duration)}</em>
            </span>
            <span class="audio-voice-row__state">${voice.isPlaying === false ? 'Paused' : 'Playing'}</span>
            <span class="audio-voice-row__progress">${formatAudioProgress(voice)}</span>
            <span class="audio-voice-row__volume">${formatAudioVolume(voice.volume)}</span>
            <button class="btn btn-sm" type="button" data-audiokit-stop-voice="${escapeHtml(voice.voiceId ?? '')}">Stop</button>
        </div>`;
    }).join('');
}

function renderAudioHistory(history) {
    if (!Array.isArray(history) || !history.length) {
        return emptyState('audio', '暂无播放历史。');
    }
    return `<div class="kit-timeline" data-kit-scroll-key="audio-history">${history.slice(0, 100).map(item => `<div class="kit-timeline-row">
        <span>${escapeHtml(formatAudioHistoryLabel(item))}</span>
        <strong>${escapeHtml(item.path ?? item.bus ?? '--')}</strong>
        <em>${escapeHtml(item.timestampUtc ?? '--')}</em>
    </div>`).join('')}</div>`;
}

function formatAudioHistoryLabel(item) {
    const eventType = item?.eventType ?? 'event';
    if (eventType === 'play_started') return '播放';
    if (eventType === 'play_stopped') return '停止';
    if (eventType === 'play_stop_requested') return '淡出';
    if (eventType === 'volume_changed') return '音量';
    return eventType;
}

function bindAudioKitWorkbenchActions() {
    $pageBody.querySelectorAll('[data-audiokit-voice]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            selectAudioKitVoice(button.dataset.audiokitVoice);
        });
    });
    $pageBody.querySelectorAll('[data-audiokit-stop-voice]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => stopAudioKitVoice(button.dataset.audiokitStopVoice));
    });
    bindKitButtonClick('[data-audiokit-clear-history]', () => clearAudioKitHistory());
    bindKitButtonClick('[data-audiokit-stop-all]', () => stopAllAudioKit());
    bindKitButtonClick('[data-audiokit-master-mute]', button => muteAudioKitMaster(button.dataset.audiokitMasterMute === 'true'));
    const masterVolume = $pageBody.querySelector('[data-audiokit-master-volume]');
    if (masterVolume && masterVolume.dataset.bound !== '1') {
        masterVolume.dataset.bound = '1';
        masterVolume.addEventListener('change', () => setAudioKitMasterVolume(masterVolume.value));
    }
    $pageBody.querySelectorAll('[data-audiokit-voice-bus-filter]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => selectAudioKitBus(button.dataset.audiokitVoiceBusFilter));
    });
    $pageBody.querySelectorAll('[data-audiokit-bus-volume]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('change', () => setAudioKitBusVolume(input.dataset.audiokitBusVolume, input.value));
    });
    $pageBody.querySelectorAll('[data-audiokit-bus-mute]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => muteAudioKitBus(button.dataset.audiokitBusMute, button.dataset.audiokitBusMuted === 'true'));
    });
    $pageBody.querySelectorAll('[data-audiokit-stop-bus]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => stopAudioKitBus(button.dataset.audiokitStopBus));
    });
    bindAudioKitSearch();
}

function bindAudioKitSearch() {
    const input = $pageBody.querySelector('[data-audiokit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            audioKitState.searchTerm = input.value || '';
            updateAudioKitListDom();
        });
    }

    const clear = $pageBody.querySelector('[data-audiokit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            audioKitState.searchTerm = '';
            updateAudioKitListDom();
            $pageBody.querySelector('[data-audiokit-search]')?.focus();
        });
    }
}

function updateAudioKitListDom() {
    const visible = filterAudioKitVoices(audioKitState.voices);
    const list = $pageBody.querySelector('.audio-voice-table');
    if (list) list.innerHTML = renderAudioVoiceRows(visible);
    const count = $pageBody.querySelector('[data-audiokit-visible-count]');
    if (count) count.textContent = `${visible.length} / ${audioKitState.voices.length}`;
    const input = $pageBody.querySelector('[data-audiokit-search]');
    if (input && input.value !== audioKitState.searchTerm) input.value = audioKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-audiokit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !audioKitState.searchTerm.trim());
    bindAudioKitWorkbenchActions();
}

function selectAudioKitVoice(voiceId) {
    if (!voiceId) return;
    audioKitState.selectedVoiceId = voiceId;
    $pageBody.querySelectorAll('[data-audiokit-voice]').forEach(row => {
        row.classList.toggle('active', String(row.dataset.audiokitVoice) === String(audioKitState.selectedVoiceId));
    });
    bindAudioKitWorkbenchActions();
}

function selectAudioKitBus(busName) {
    audioKitState.selectedBus = busName || 'All';
    $pageBody.querySelectorAll('[data-audiokit-channel-strip]').forEach(row => {
        row.classList.toggle('active', String(row.dataset.audiokitChannelStrip) === String(audioKitState.selectedBus));
    });
    updateAudioKitListDom();
}

async function stopAudioKitVoice(voiceId) {
    if (!invoke || !connected || !voiceId) return;
    await sendKitCommandData('AudioKit', 'stop_voice', { voiceId: Number(voiceId) });
    await loadAudioKitWorkbench();
}

async function stopAllAudioKit() {
    if (!invoke || !connected) return;
    await sendKitCommandData('AudioKit', 'stop_all');
    await loadAudioKitWorkbench();
}

async function stopAudioKitBus(bus) {
    if (!invoke || !connected || !bus) return;
    await sendKitCommandData('AudioKit', 'stop_bus', { bus });
    await loadAudioKitWorkbench();
}

async function setAudioKitMasterVolume(volume) {
    if (!invoke || !connected) return;
    await sendKitCommandData('AudioKit', 'set_master_volume', { volume: Number(volume) });
    await loadAudioKitWorkbench();
}

async function setAudioKitBusVolume(bus, volume) {
    if (!invoke || !connected || !bus) return;
    await sendKitCommandData('AudioKit', 'set_bus_volume', { bus, volume: Number(volume) });
    await loadAudioKitWorkbench();
}

async function muteAudioKitMaster(muted) {
    if (!invoke || !connected) return;
    await sendKitCommandData('AudioKit', 'mute_master', { muted: !!muted });
    await loadAudioKitWorkbench();
}

async function muteAudioKitBus(bus, muted) {
    if (!invoke || !connected || !bus) return;
    await sendKitCommandData('AudioKit', 'mute_bus', { bus, muted: !!muted });
    await loadAudioKitWorkbench();
}

async function clearAudioKitHistory() {
    if (!invoke || !connected) return;
    await sendKitCommandData('AudioKit', 'clear_history');
    await loadAudioKitWorkbench();
}

function formatAudioVolume(value) {
    return percentText(Number(value ?? 0));
}

function normalizeAudioVolume(value) {
    const number = Number(value);
    if (!Number.isFinite(number)) return 0;
    return Math.max(0, Math.min(1, number));
}

function formatAudioMeterWidth(value) {
    return Math.round(normalizeAudioVolume(value) * 100) + '%';
}

function formatAudioTime(value) {
    const number = Number(value);
    if (!Number.isFinite(number) || number <= 0) return '0.0s';
    return number.toFixed(1) + 's';
}

function formatAudioProgress(voice) {
    return `${formatAudioTime(voice?.elapsed)} / ${formatAudioTime(voice?.duration)}`;
}

function getAudioVoiceProgressRatio(voice) {
    const duration = Number(voice?.duration);
    if (!Number.isFinite(duration) || duration <= 0) return 0;
    const elapsed = Number(voice?.elapsed);
    if (!Number.isFinite(elapsed) || elapsed <= 0) return 0;
    return Math.max(0, Math.min(1, elapsed / duration));
}

function formatAudioProgressPercent(voice) {
    const duration = Number(voice?.duration);
    if (!Number.isFinite(duration) || duration <= 0) return '--';
    return percentText(getAudioVoiceProgressRatio(voice));
}

function formatAudioNumber(value, decimals) {
    const number = Number(value);
    if (!Number.isFinite(number)) return '--';
    return number.toFixed(decimals);
}

function formatAudioPosition(position) {
    if (!position) return '位置 --';
    const x = formatAudioNumber(position.x ?? position.X, 1);
    const y = formatAudioNumber(position.y ?? position.Y, 1);
    const z = formatAudioNumber(position.z ?? position.Z, 1);
    return `位置 (${x}, ${y}, ${z})`;
}

