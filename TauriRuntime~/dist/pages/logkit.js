// pages/logkit.js
// ═══════════════════════════════════════════════════════════════════
// 页面：LogKit
// ═══════════════════════════════════════════════════════════════════
const LOGKIT_LEVEL_OPTIONS = ['Debug', 'Info', 'Warning', 'Error'];
const logKitState = {
    stats: {},
    settings: {},
    history: [],
    searchTerm: '',
    selectedLogKind: 'editor',
    viewerTitle: t('logkit.editor_log'),
    viewerContent: '',
    viewerMeta: null,
    decryptSourcePath: '',
    actionMessage: '',
    actionTone: 'success',
    renderSignature: '',
};

function renderLogKitPage() {
    $pageBody.classList.add('content-body--logkit');
    $pageBody.classList.remove('content-body--poolkit');
    $pageBody.classList.remove('content-body--reskit');
    $pageBody.classList.remove('content-body--singletonkit');
    setHero(
        t('logkit.title'),
        t('logkit.subtitle'),
        t('logkit.tab'),
        'log',
        `<button class="btn btn-sm" onclick="clearLogKitHistory()">${t('logkit.clear_history')}</button><button class="btn btn-sm" onclick="resetLogKitSettings()">${t('common.reset')}</button><button class="btn btn-primary btn-sm" onclick="refreshLogKit()">${t('common.refresh')}</button>`
    );
    clearTabs();
    logKitState.renderSignature = '';
    loadLogKitWorkbench();
}

async function refreshLogKit() { loadLogKitWorkbench({ forceCommandRefresh: true }); }

async function refreshLogKitReactive(event) {
    await loadLogKitWorkbench();
}

function normalizeLogKitStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const historySource = source.history ?? {};
    return {
        stats: source.stats ?? {},
        settings: normalizeLogKitSettings(source.settings ?? {}),
        history: Array.isArray(source.history)
            ? source.history
            : (Array.isArray(historySource.entries) ? historySource.entries : []),
    };
}

function normalizeLogKitSettings(settings) {
    const source = settings ?? {};
    return {
        enabled: source.enabled ?? true,
        minimumLevel: source.minimumLevel ?? 'Debug',
        saveLogInEditor: source.saveLogInEditor ?? false,
        saveLogInPlayer: source.saveLogInPlayer ?? true,
        enableIMGUIInPlayer: source.enableIMGUIInPlayer ?? false,
        enableEncryption: source.enableEncryption ?? true,
        maxQueueSize: source.maxQueueSize ?? 20000,
        maxSameLogCount: source.maxSameLogCount ?? 50,
        maxRetentionDays: source.maxRetentionDays ?? 15,
        maxFileSizeMB: source.maxFileSizeMB ?? 100,
        imguiMaxLogCount: source.imguiMaxLogCount ?? 200,
        logDirectory: source.logDirectory ?? '',
        editorFileName: source.editorFileName ?? 'yoki_editor.log',
        playerFileName: source.playerFileName ?? 'yoki_player.log',
        assetResourcePath: source.assetResourcePath ?? 'YokiFrameRuntimeSettings',
        assetPath: source.assetPath ?? 'Assets/Settings/Resources/YokiFrameRuntimeSettings.asset',
    };
}

async function fetchLogKitWorkbenchState() {
    const telemetry = await readKitTelemetryData('LogKit');
    if (telemetry) return normalizeLogKitStatePayload(telemetry);

    const snapshot = await readKitSnapshotData('LogKit');
    if (snapshot) return normalizeLogKitStatePayload(snapshot);

    return null;
}

async function fetchLogKitWorkbenchStateFromCommands() {
    const snapshot = await sendKitCommandData('LogKit', 'get_workbench_snapshot');
    return normalizeLogKitStatePayload(snapshot);
}

async function loadLogKitWorkbench({ forceCommandRefresh = false } = {}) {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('log', t('logkit.need_runtime'));
        clearMetrics();
        return;
    }

    try {
        const snapshotState = forceCommandRefresh ? null : await fetchLogKitWorkbenchState();
        const state = snapshotState ?? await fetchLogKitWorkbenchStateFromCommands();
        logKitState.stats = state.stats;
        logKitState.settings = state.settings;
        logKitState.history = state.history;
        await refreshLogKitViewerState();
        clearMetrics();

        const html = renderLogKitWorkbench(logKitState.stats, logKitState.settings, logKitState.history);
        const signature = makeStableSignature({
            stats: logKitState.stats,
            settings: logKitState.settings,
            history: logKitState.history,
            search: logKitState.searchTerm,
            selectedLogKind: logKitState.selectedLogKind,
            viewerTitle: logKitState.viewerTitle,
            viewerContent: logKitState.viewerContent,
            viewerMeta: logKitState.viewerMeta,
            decryptSourcePath: logKitState.decryptSourcePath,
            actionMessage: logKitState.actionMessage,
            actionTone: logKitState.actionTone,
        });
        renderWorkbenchHtmlStable(logKitState, html, signature, bindLogKitWorkbenchActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('LogKit')) {
            showRuntimeKitUnavailable('LogKit', t('logkit.title'));
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function renderLogKitWorkbench(stats, settings, history) {
    const normalized = normalizeLogKitSettings(settings);
    const visibleHistory = filterLogKitHistory(history);
    return `<div class="kit-workbench kit-workbench--logkit">
        <div class="kit-workbench-grid kit-workbench-grid--logkit">
            ${renderLogKitSettingsSection(stats, normalized)}
            ${renderLogKitViewerSection(normalized, visibleHistory, history)}
        </div>
    </div>`;
}

function renderLogKitSettingsSection(stats, settings) {
    return `<section class="kit-panel kit-panel--detail logkit-settings-panel">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', t('logkit.runtime_config'))}</div>
                <div class="kit-panel__desc">${t('logkit.runtime_config_desc')}</div>
            </div>
            <span class="kit-state-pill ${settings.enabled ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${settings.enabled ? 'Enabled' : 'Disabled'}</span>
        </div>
        <div class="kit-detail-summary kit-detail-summary--logkit">
            <div><span>Logger</span><strong>${escapeHtml(stats?.loggerName ?? 'None')}</strong></div>
            <div><span>History</span><strong>${escapeHtml(stats?.historyCount ?? 0)}</strong></div>
            <div><span>Level</span><strong>${escapeHtml(settings.minimumLevel)}</strong></div>
            <div><span>Writer</span><strong>${settings.saveLogInEditor || settings.saveLogInPlayer ? '可写入' : '关闭'}</strong></div>
        </div>
        <div class="kit-setting-grid">
            ${renderKitToggle(t('logkit.output'), !!settings.enabled, 'data-logkit-setting="enabled"')}
            ${renderKitToggle(t('logkit.editor_write'), !!settings.saveLogInEditor, 'data-logkit-setting="saveLogInEditor"')}
            ${renderKitToggle(t('logkit.player_write'), !!settings.saveLogInPlayer, 'data-logkit-setting="saveLogInPlayer"')}
            ${renderKitToggle(t('logkit.encryption'), !!settings.enableEncryption, 'data-logkit-setting="enableEncryption"')}
            ${renderKitToggle(t('logkit.player_imgui'), !!settings.enableIMGUIInPlayer, 'data-logkit-setting="enableIMGUIInPlayer"')}
        </div>
        <div class="kit-number-grid">
            <label class="kit-number-field"><span>${t('logkit.min_level')}</span>${renderLogKitLevelSelect(settings.minimumLevel)}</label>
            ${renderLogKitNumberField(t('logkit.queue_limit'), 'maxQueueSize', settings.maxQueueSize, 1)}
            ${renderLogKitNumberField(t('logkit.repeat_threshold'), 'maxSameLogCount', settings.maxSameLogCount, 0)}
            ${renderLogKitNumberField(t('logkit.retention_days'), 'maxRetentionDays', settings.maxRetentionDays, 1)}
            ${renderLogKitNumberField(t('logkit.file_size_mb'), 'maxFileSizeMB', settings.maxFileSizeMB, 1)}
            ${renderLogKitNumberField(t('logkit.imgui_count'), 'imguiMaxLogCount', settings.imguiMaxLogCount, 1)}
        </div>
        <div class="logkit-storage-fields">
            ${renderLogKitStorageField(t('logkit.log_path'), 'logDirectory', settings.logDirectory, getLogKitDirectoryText(settings), 'Application.persistentDataPath/LogFiles')}
            ${renderLogKitStorageField(t('logkit.editor_log_file'), 'editorFileName', settings.editorFileName, getLogKitFilePath(settings, 'editor'), 'yoki_editor.log')}
            ${renderLogKitStorageField(t('logkit.player_log_file'), 'playerFileName', settings.playerFileName, getLogKitFilePath(settings, 'player'), 'yoki_player.log')}
        </div>
        ${logKitState.actionMessage ? `<div class="kit-note kit-note--compact kit-note--${escapeHtml(logKitState.actionTone)}">${escapeHtml(logKitState.actionMessage)}</div>` : ''}
    </section>`;
}

function renderLogKitStorageField(label, key, value, resolvedPath, placeholder) {
    return `<div class="logkit-storage-field">
        <span>${escapeHtml(label)}</span>
        <div class="logkit-storage-field__control">
            <input class="cmd-input" type="text" data-logkit-text="${escapeHtml(key)}" value="${escapeHtml(value ?? '')}" placeholder="${escapeHtml(placeholder ?? '')}">
            <button class="btn btn-secondary btn-sm logkit-storage-field__jump" type="button" data-logkit-open-folder title="打开日志文件夹">${svgIcon('folder', 'shell-icon')}<span>打开</span></button>
        </div>
        <code>${escapeHtml(resolvedPath || '--')}</code>
    </div>`;
}

function renderLogKitViewerSection(settings, visibleHistory, history) {
    const meta = logKitState.viewerMeta ?? {};
    const selectedKind = logKitState.selectedLogKind || 'editor';
    const viewerContent = logKitState.viewerContent || '';
    const selectedPath = meta.path || (selectedKind === 'player' ? getLogKitFilePath(settings, 'player') : getLogKitFilePath(settings, 'editor'));
    const selectedSize = meta.sizeBytes == null ? '--' : formatLogKitBytes(meta.sizeBytes);
    const modifiedText = meta.modifiedUtc ? formatSaveKitDate(meta.modifiedUtc) : '--';
    const existsText = meta.exists === false ? '未找到' : (meta.decrypted ? '已解密' : '可查看');
    return `<section class="kit-panel kit-panel--events logkit-viewer">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('log', '日志查看')}</div>
                <div class="kit-panel__desc">选择运行时日志文件或选择加密日志后直接查看内容</div>
            </div>
            <span class="kit-panel__count">${escapeHtml(existsText)}</span>
        </div>
        <div class="logkit-viewer-tabs">
            <button class="btn btn-sm ${selectedKind === 'editor' ? 'btn-primary' : 'btn-secondary'}" type="button" data-logkit-view-file="editor">${svgIcon('file', 'shell-icon')}<span>Editor</span></button>
            <button class="btn btn-sm ${selectedKind === 'player' ? 'btn-primary' : 'btn-secondary'}" type="button" data-logkit-view-file="player">${svgIcon('file', 'shell-icon')}<span>Player</span></button>
            <button class="btn btn-sm ${selectedKind === 'decrypted' ? 'btn-primary' : 'btn-secondary'}" type="button" data-logkit-pick-encrypted>${svgIcon('folder', 'shell-icon')}<span>选择并解密</span></button>
        </div>
        <div class="kit-detail-summary kit-detail-summary--logkit-viewer">
            <div><span>文件</span><strong>${escapeHtml(meta.fileName || getLogKitViewerKindLabel(selectedKind))}</strong></div>
            <div><span>大小</span><strong>${escapeHtml(selectedSize)}</strong></div>
            <div><span>行数</span><strong>${escapeHtml(meta.lineCount ?? '--')}</strong></div>
            <div><span>修改时间</span><strong>${escapeHtml(modifiedText)}</strong></div>
        </div>
        <div class="logkit-selected-path">
            <span>${escapeHtml(logKitState.viewerTitle || getLogKitViewerKindLabel(selectedKind))}</span>
            <code title="${escapeHtml(selectedPath)}">${escapeHtml(selectedPath || '--')}</code>
        </div>
        ${renderLogKitViewerContent(viewerContent, meta, selectedKind)}
        <div class="logkit-memory-history">
            <div class="logkit-memory-history__head">
                <div>
                    <strong>内存历史</strong>
                    <span>Base LogKit 最近日志，按最新优先显示</span>
                </div>
                <em data-logkit-visible-count>${escapeHtml(visibleHistory.length)} / ${escapeHtml(history.length)}</em>
            </div>
            <div class="kit-panel__tools">${renderKitSearchInput(logKitState.searchTerm, 'data-logkit-search', '搜索级别、消息、上下文或异常')}</div>
            <div data-logkit-history>${renderLogKitHistory(visibleHistory)}</div>
        </div>
    </section>`;
}

function renderLogKitViewerContent(content, meta, selectedKind) {
    if (meta?.error) {
        return `<div class="logkit-viewer-output-wrap">${emptyState('warning', `读取日志失败：${escapeHtml(meta.error)}`)}</div>`;
    }

    if (selectedKind !== 'decrypted' && meta?.exists === false) {
        return `<div class="logkit-viewer-output-wrap">${emptyState('file', '日志文件还不存在。打开对应运行时写入开关后，产生日志会写到这里。')}</div>`;
    }

    if (!content) {
        const message = selectedKind === 'decrypted'
            ? '选择一个加密日志文件后，解密结果会显示在这里。'
            : '当前日志文件为空。';
        return `<div class="logkit-viewer-output-wrap">${emptyState('log', message)}</div>`;
    }

    const note = meta?.truncated ? '<div class="kit-note kit-note--compact">文件较大，当前只显示末尾预览。</div>' : '';
    return `<div class="logkit-viewer-output-wrap">
        ${note}
        <pre class="logkit-viewer-output" data-kit-scroll-key="logkit-viewer">${escapeHtml(content)}</pre>
    </div>`;
}

function renderLogKitNumberField(label, key, value, min) {
    const safeValue = Number.isFinite(Number(value)) ? Number(value) : min;
    return `<label class="kit-number-field">
        <span>${escapeHtml(label)}</span>
        <input type="number" min="${escapeHtml(min)}" step="1" data-logkit-number="${escapeHtml(key)}" value="${escapeHtml(safeValue)}">
    </label>`;
}

function renderLogKitLevelSelect(value) {
    const current = LOGKIT_LEVEL_OPTIONS.includes(value) ? value : 'Debug';
    return `<select data-logkit-level>${LOGKIT_LEVEL_OPTIONS.map(level =>
        `<option value="${escapeHtml(level)}"${level === current ? ' selected' : ''}>${escapeHtml(level)}</option>`
    ).join('')}</select>`;
}

function filterLogKitHistory(entries) {
    return (Array.isArray(entries) ? entries : []).filter(entry => kitSearchMatches(logKitState.searchTerm, [
        entry.level,
        entry.message,
        entry.context,
        entry.exceptionType,
        entry.exceptionMessage,
        entry.stackTrace,
        entry.timestampUtc,
    ]));
}

function renderLogKitHistory(entries) {
    if (!Array.isArray(entries) || !entries.length) {
        return emptyState('log', logKitState.searchTerm ? '没有匹配当前搜索的日志。' : '暂无日志。调用 LogKit.Log/Warning/Error 后会显示。');
    }

    return `<div class="kit-timeline" data-kit-scroll-key="logkit-history">${entries.slice(0, 160).map(renderLogKitHistoryRow).join('')}</div>`;
}

function renderLogKitHistoryRow(entry) {
    const level = String(entry?.level ?? '--');
    const message = String(entry?.message ?? '');
    const exception = entry?.exceptionMessage ? ` · ${entry.exceptionType || 'Exception'}: ${entry.exceptionMessage}` : '';
    return `<div class="kit-timeline-row">
        <span>${escapeHtml(level)}</span>
        <strong title="${escapeHtml(message + exception)}">${escapeHtml(message || '--')}${escapeHtml(exception)}</strong>
        <em>${escapeHtml(formatSaveKitDate(entry?.timestampUtc))}</em>
    </div>`;
}

function bindLogKitWorkbenchActions() {
    $pageBody.querySelectorAll('[data-logkit-open-folder]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => openLogKitFolder());
    });
    $pageBody.querySelectorAll('[data-logkit-view-file]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => selectLogKitFile(button.dataset.logkitViewFile));
    });
    bindKitButtonClick('[data-logkit-pick-encrypted]', () => pickEncryptedLogFile());
    $pageBody.querySelectorAll('[data-logkit-setting]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('change', () => updateLogKitSetting(input.dataset.logkitSetting, input.checked));
    });
    $pageBody.querySelectorAll('[data-logkit-text]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('change', () => {
            const key = input.dataset.logkitText;
            const value = key === 'logDirectory' ? input.value : input.value.trim();
            updateLogKitSetting(key, value);
        });
    });
    $pageBody.querySelectorAll('[data-logkit-number]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        input.addEventListener('change', () => updateLogKitSetting(input.dataset.logkitNumber, Math.max(Number(input.min || 0), Math.floor(Number(input.value || 0)))));
    });
    const levelSelect = $pageBody.querySelector('[data-logkit-level]');
    if (levelSelect && levelSelect.dataset.bound !== '1') {
        levelSelect.dataset.bound = '1';
        levelSelect.addEventListener('change', () => updateLogKitSetting('minimumLevel', levelSelect.value));
    }
    bindLogKitSearch();
}

function bindLogKitSearch() {
    const input = $pageBody.querySelector('[data-logkit-search]');
    if (input && input.dataset.bound !== '1') {
        input.dataset.bound = '1';
        input.addEventListener('input', () => {
            logKitState.searchTerm = input.value || '';
            updateLogKitHistoryDom();
        });
    }

    const clear = $pageBody.querySelector('[data-logkit-search-clear]');
    if (clear && clear.dataset.bound !== '1') {
        clear.dataset.bound = '1';
        clear.addEventListener('click', () => {
            logKitState.searchTerm = '';
            updateLogKitHistoryDom();
            $pageBody.querySelector('[data-logkit-search]')?.focus();
        });
    }
}

function updateLogKitHistoryDom() {
    const visible = filterLogKitHistory(logKitState.history);
    const container = $pageBody.querySelector('[data-logkit-history]');
    if (container) container.innerHTML = renderLogKitHistory(visible);
    const count = $pageBody.querySelector('[data-logkit-visible-count]');
    if (count) count.textContent = `${visible.length} / ${logKitState.history.length}`;
    const input = $pageBody.querySelector('[data-logkit-search]');
    if (input && input.value !== logKitState.searchTerm) input.value = logKitState.searchTerm;
    const clear = $pageBody.querySelector('[data-logkit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !logKitState.searchTerm.trim());
    bindLogKitWorkbenchActions();
}

async function updateLogKitSetting(key, value) {
    if (!invoke || !connected || !key) return;
    await sendKitCommandData('LogKit', 'set_settings', { [key]: value });
    await loadLogKitWorkbench({ forceCommandRefresh: true });
}

async function clearLogKitHistory() {
    if (!invoke || !connected) return;
    await sendKitCommandData('LogKit', 'clear_history');
    await loadLogKitWorkbench({ forceCommandRefresh: true });
}

async function resetLogKitSettings() {
    if (!invoke || !connected) return;
    await sendKitCommandData('LogKit', 'reset_settings');
    logKitState.selectedLogKind = 'editor';
    await loadLogKitWorkbench({ forceCommandRefresh: true });
}

async function openLogKitFolder() {
    if (!invoke || !connected) return;
    try {
        const result = await sendKitCommandData('LogKit', 'open_log_folder');
        const directory = result?.directory || result?.revealedPath || '';
        setLogKitActionMessage(directory ? `已打开日志目录：${directory}` : '已打开日志目录。', 'success');
    } catch (e) {
        setLogKitActionMessage(`打开日志目录失败：${e}`, 'error');
    }
    await loadLogKitWorkbench({ forceCommandRefresh: true });
}

async function selectLogKitFile(kind) {
    const nextKind = kind === 'player' ? 'player' : 'editor';
    logKitState.selectedLogKind = nextKind;
    await loadLogKitWorkbench({ forceCommandRefresh: true });
}

async function pickEncryptedLogFile() {
    if (!invoke || !connected) return;
    try {
        const initialPath = logKitState.settings?.logDirectory || '';
        const selected = await invoke('pick_file', { initialPath, extension: 'log', projectRoot: '' });
        if (!selected) return;
        logKitState.decryptSourcePath = selected;
        await decryptLogKitFile(selected);
    } catch (e) {
        setLogKitActionMessage(`选择加密日志失败：${e}`, 'error');
        await loadLogKitWorkbench({ forceCommandRefresh: true });
    }
}

async function decryptLogKitFile(filePath) {
    if (!invoke || !connected) return;
    try {
        const result = await sendKitCommandData('LogKit', 'decrypt_log_file', { filePath });
        setLogKitViewerFromResult('decrypted', result);
        const outputPath = result?.path || '';
        setLogKitActionMessage(outputPath ? `已解密并显示：${outputPath}` : '已解密并显示日志。', 'success');
    } catch (e) {
        setLogKitActionMessage(`解密日志失败：${e}`, 'error');
    }
    await loadLogKitWorkbench({ forceCommandRefresh: true });
}

function setLogKitActionMessage(message, tone = 'success') {
    logKitState.actionMessage = message || '';
    logKitState.actionTone = tone === 'error' ? 'error' : 'success';
}

async function refreshLogKitViewerState() {
    const kind = logKitState.selectedLogKind === 'player' ? 'player'
        : (logKitState.selectedLogKind === 'decrypted' ? 'decrypted' : 'editor');

    if (kind === 'decrypted') {
        logKitState.viewerTitle = logKitState.viewerTitle || '解密日志';
        return;
    }

    try {
        const result = await sendKitCommandData('LogKit', 'read_log_file', { kind });
        setLogKitViewerFromResult(kind, result);
    } catch (e) {
        logKitState.viewerTitle = getLogKitViewerKindLabel(kind);
        logKitState.viewerContent = '';
        logKitState.viewerMeta = { error: String(e?.message ?? e ?? '未知错误') };
    }
}

function setLogKitViewerFromResult(kind, result) {
    const normalizedKind = kind === 'player' ? 'player' : (kind === 'decrypted' ? 'decrypted' : 'editor');
    logKitState.selectedLogKind = normalizedKind;
    logKitState.viewerMeta = result ?? {};
    logKitState.viewerContent = result?.content ?? '';
    logKitState.viewerTitle = normalizedKind === 'decrypted'
        ? '解密日志'
        : getLogKitViewerKindLabel(normalizedKind);
}

function getLogKitDirectoryText(settings) {
    return settings?.logDirectory ? settings.logDirectory : 'Application.persistentDataPath/LogFiles';
}

function getLogKitFileName(settings, kind) {
    if (kind === 'player') return settings?.playerFileName || 'yoki_player.log';
    return settings?.editorFileName || 'yoki_editor.log';
}

function getLogKitFilePath(settings, kind) {
    const directory = getLogKitDirectoryText(settings);
    const fileName = getLogKitFileName(settings, kind);
    if (!directory || directory.endsWith('/') || directory.endsWith('\\')) return `${directory}${fileName}`;
    return `${directory}/${fileName}`;
}

function getLogKitViewerKindLabel(kind) {
    if (kind === 'player') return 'Player 日志';
    if (kind === 'decrypted') return '解密日志';
    return 'Editor 日志';
}

function formatLogKitBytes(value) {
    const bytes = Number(value);
    if (!Number.isFinite(bytes) || bytes < 0) return '--';
    if (bytes < 1024) return `${bytes} B`;
    const units = ['KB', 'MB', 'GB'];
    let size = bytes / 1024;
    for (let i = 0; i < units.length; i++) {
        if (size < 1024 || i === units.length - 1) {
            return `${size.toFixed(size >= 10 ? 1 : 2)} ${units[i]}`;
        }
        size /= 1024;
    }
    return `${bytes} B`;
}

