// pages/logkit-viewer.js
// LogKit 查看器交互和文件命令
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

function syncLogKitViewerDom() {
    const root = $pageBody.querySelector('[data-logkit-workbench="root"]');
    if (!root) {
        renderLogKitWorkbenchFromState();
        return;
    }

    const normalizedSettings = normalizeLogKitSettings(logKitState.settings);
    const visibleHistory = filterLogKitHistory(logKitState.history);
    const selectedKind = logKitState.selectedLogKind || 'editor';
    const viewerMeta = logKitState.viewerMeta ?? {};
    const selectedPath = viewerMeta.path || (selectedKind === 'player' ? getLogKitFilePath(normalizedSettings, 'player') : getLogKitFilePath(normalizedSettings, 'editor'));
    const selectedSize = viewerMeta.sizeBytes == null ? '--' : formatLogKitBytes(viewerMeta.sizeBytes);
    const modifiedText = viewerMeta.modifiedUtc ? formatSaveKitDate(viewerMeta.modifiedUtc) : '--';
    const existsText = viewerMeta.error ? '读取失败' : (viewerMeta.exists === false ? '未找到' : (viewerMeta.decrypted ? '已解密' : '可查看'));

    const tabs = root.querySelector('[data-logkit-viewer-tabs]');
    if (tabs) {
        tabs.querySelectorAll('[data-logkit-view-file]').forEach(button => {
            const kind = button.dataset.logkitViewFile || 'editor';
            button.classList.toggle('btn-primary', kind === selectedKind);
            button.classList.toggle('btn-secondary', kind !== selectedKind);
        });
        const pickButton = tabs.querySelector('[data-logkit-pick-encrypted]');
        if (pickButton) {
            pickButton.classList.toggle('btn-primary', selectedKind === 'decrypted');
            pickButton.classList.toggle('btn-secondary', selectedKind !== 'decrypted');
        }
    }

    const summary = root.querySelector('[data-logkit-viewer-summary]');
    if (summary) {
        summary.innerHTML = `<div><span>文件</span><strong>${escapeHtml(viewerMeta.fileName || getLogKitViewerKindLabel(selectedKind))}</strong></div>
            <div><span>大小</span><strong>${escapeHtml(selectedSize)}</strong></div>
            <div><span>行数</span><strong>${escapeHtml(viewerMeta.lineCount ?? '--')}</strong></div>
            <div><span>修改时间</span><strong>${escapeHtml(modifiedText)}</strong></div>`;
    }

    const selectedPathNode = root.querySelector('[data-logkit-selected-path]');
    if (selectedPathNode) {
        selectedPathNode.innerHTML = `<span>${escapeHtml(logKitState.viewerTitle || getLogKitViewerKindLabel(selectedKind))}</span><code title="${escapeHtml(selectedPath)}">${escapeHtml(selectedPath || '--')}</code>`;
    }

    const content = root.querySelector('[data-logkit-viewer-content]');
    if (content) {
        content.innerHTML = renderLogKitViewerContent(logKitState.viewerContent || '', viewerMeta, selectedKind);
    }

    const history = root.querySelector('[data-logkit-history]');
    if (history) {
        history.innerHTML = renderLogKitHistory(visibleHistory);
    }

    const count = root.querySelector('[data-logkit-visible-count]');
    if (count) {
        count.textContent = `${visibleHistory.length} / ${logKitState.history.length}`;
    }

    const status = root.querySelector('[data-logkit-viewer-panel] .kit-panel__count');
    if (status) {
        status.textContent = escapeHtml(existsText);
    }

    const search = root.querySelector('[data-logkit-search]');
    if (search && search.value !== logKitState.searchTerm) search.value = logKitState.searchTerm;
    const clear = root.querySelector('[data-logkit-search-clear]');
    if (clear) clear.classList.toggle('is-empty', !logKitState.searchTerm.trim());

    logKitState.renderSignature = makeStableSignature({
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
    await refreshLogKitViewerState();
    syncLogKitViewerDom();
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
    syncLogKitViewerDom();
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

function renderLogKitWorkbenchFromState() {
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
}
