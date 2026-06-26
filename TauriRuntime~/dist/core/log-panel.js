// core/log-panel.js
// 系统日志面板与复制操作。
const logBuffer = [];

function formatLogLine(entry) {
    return `${entry.time} ${entry.message}`;
}

async function copyLogPanelText() {
    if (!navigator.clipboard?.writeText) return;
    const text = logBuffer.length
        ? logBuffer.map(formatLogLine).join('\n')
        : t('system.no_log');
    try {
        await navigator.clipboard.writeText(text);
    } catch (_) {
        // 剪贴板权限由宿主环境决定，失败时保持日志面板可手动选择复制。
    }
}

function addLog(message, level = 'info') {
    const now = new Date().toLocaleTimeString();
    logBuffer.push({ time: now, message, level });
    if (logBuffer.length > 200) {
        logBuffer.shift(); // 控制内存上限。
        if (renderedLogCount > 0) renderedLogCount -= 1;
    }
    // 如果当前位于 System 页面，则实时更新日志面板。
    if (activePage === 'system') scheduleLogPanelRender();
}
function clearLog() {
    logBuffer.length = 0;
    renderedLogCount = 0;
    if (activePage === 'system') scheduleLogPanelRender();
}
function scheduleLogPanelRender() {
    if (pendingLogRenderFrame || activePage !== 'system') return;
    pendingLogRenderFrame = requestAnimationFrame(() => {
        pendingLogRenderFrame = 0;
        if (activePage === 'system') renderLogPanel();
    });
}
function createLogEntry(e) {
    const row = document.createElement('div');
    row.className = `log-entry ${e.level}`;

    const time = document.createElement('span');
    time.className = 'time';
    time.textContent = e.time;

    const message = document.createElement('span');
    message.className = 'log-message';
    message.textContent = e.message;

    row.append(time, message);
    return row;
}
function renderLogPanel() {
    const container = document.getElementById('log-panel-body');
    if (!container) return;
    if (!container.childElementCount) renderedLogCount = 0;
    if (!logBuffer.length) {
        renderedLogCount = 0;
        container.dataset.emptyLog = 'true';
        container.replaceChildren(createLogEntry({ time: '--:--:--', message: t('system.no_log'), level: 'system' }));
        return;
    }

    if (container.dataset.emptyLog === 'true') {
        container.replaceChildren();
        delete container.dataset.emptyLog;
        renderedLogCount = 0;
    }
    while (container.childElementCount > logBuffer.length) {
        container.firstElementChild?.remove();
    }
    if (renderedLogCount > logBuffer.length || renderedLogCount > container.childElementCount) {
        renderedLogCount = container.childElementCount;
    }
    for (let i = renderedLogCount; i < logBuffer.length; i++) {
        container.appendChild(createLogEntry(logBuffer[i]));
    }
    renderedLogCount = logBuffer.length;
    container.scrollTop = container.scrollHeight;
}

// ═══════════════════════════════════════════════════════════════════
// 连接
// ═══════════════════════════════════════════════════════════════════
