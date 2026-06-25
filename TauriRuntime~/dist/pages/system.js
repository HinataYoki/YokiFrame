// pages/system.js
function renderSystemPage() {
    setHero(
        t('system.title'),
        t('system.subtitle'),
        t('system.tab'),
        'framework'
    );

    clearTabs();
    clearMetrics();
    $pageBody.innerHTML = `
        ${renderDeveloperContextStrip()}
        <div class="framework-dashboard">
            <div class="framework-stack framework-stack--primary">
                ${renderEngineStatusCard()}
                ${renderSystemLogPanel()}
            </div>
            <div class="framework-stack framework-stack--secondary">
                ${renderAiSkillInstallPanel()}
            </div>
        </div>`;

    setFrameworkCommandState(commandComposerState.kit, commandComposerState.action);
    bindFrameworkCommandComposer();
    bindAiSkillInstallerPanel();
    bindFontPreferenceControls();
    void refreshFrameworkCommandCatalog();
    void refreshAiSkillInstallerStatus();
    document.getElementById('log-copy-btn')?.addEventListener('click', () => void copyLogPanelText());
    document.getElementById('log-clear-btn')?.addEventListener('click', clearLog);
    lastFrameworkStatusPanelHtml = '';
    lastBridgeHealthPanelHtml = '';
    renderedLogCount = 0;
    renderLogPanel();
    scheduleSystemPanelUpdate({ force: true });
    if (!statusPollInFlight) {
        void pollStatus({ force: true });
    }
}

function renderEngineStatusCard() {
    return `
        <section class="panel fade-in framework-engine-panel" aria-label="${t('system.engine_status')}">
            <div class="panel-header">
                <div class="panel-header-text">
                    <span class="panel-header-icon">${svgIcon('framework')}</span>
                    <span class="panel-title">${t('system.engine_status')}</span>
                </div>
            </div>
            <div class="panel-body framework-engine-panel__body">
                <div class="framework-engine-panel__section framework-engine-panel__section--status" id="framework-status-panel">
                    ${renderFrameworkStatusContent()}
                </div>
                <div class="framework-engine-panel__section framework-engine-panel__section--bridge" id="bridge-health-panel">
                    <div class="framework-engine-panel__section-header">
                        <span class="panel-header-icon">${svgIcon('status')}</span>
                        <strong>${t('system.bridge_status')}</strong>
                    </div>
                    ${renderBridgeHealthContent()}
                </div>
                <div class="framework-engine-panel__controls">
                    <div class="framework-engine-panel__section framework-engine-panel__section--command">
                        <div class="framework-engine-panel__section-header">
                            <span class="panel-header-icon">${svgIcon('command')}</span>
                            <strong>${t('command_panel.title')}</strong>
                        </div>
                        ${renderFrameworkCommandContent()}
                    </div>
                    <div class="framework-engine-panel__section framework-engine-panel__section--font">
                        <div class="framework-engine-panel__section-header">
                            <span class="panel-header-icon">${svgIcon('font')}</span>
                            <strong>${t('font.title')}</strong>
                        </div>
                        ${renderFontPreferenceContent()}
                    </div>
                </div>
            </div>
        </section>`;
}

function renderSystemLogPanel() {
    return `
        <div class="panel fade-in" id="log-panel">
            <div class="log-header">
                <div class="panel-header-text">
                    <span class="panel-header-icon">${svgIcon('log')}</span>
                    <span class="log-header-title">${t('system.log_title')}</span>
                </div>
                <div class="log-header-actions">
                    <button class="log-clear-btn" id="log-copy-btn" type="button">${t('common.copy')}</button>
                    <button class="log-clear-btn" id="log-clear-btn" type="button">${t('common.clear')}</button>
                </div>
            </div>
            <div id="log-panel-body" class="log-output"></div>
        </div>`;
}

// 快捷操作辅助函数（暴露到全局供 onclick 使用）
window.quickPing = async () => {
    await runFrameworkCommand('System', 'ping');
    setTimeout(() => {
        void refreshBridgeDiagnosticsThrottled(true);
    }, 0);
};
window.quickStatus = async () => {
    await runFrameworkCommand('System', 'status');
};
window.quickBridgeStatus = async () => {
    await runFrameworkCommand('System', 'bridge_status');
};

