// core/app.js
// ═══════════════════════════════════════════════════════════════════
// YokiFrame Editor —— 前端应用    v2026-06-20.1
// Native Debug Console · 页面式 SPA · Tauri IPC 桥
// ═══════════════════════════════════════════════════════════════════

const { invoke } = window.__TAURI__?.core ?? {};
const { listen } = window.__TAURI__?.event ?? {};
const BridgeDiagnostics = window.YokiBridgeDiagnostics;
const t = window.YokiI18n?.t ?? ((key) => key);

function disableNativeContextMenu(event) {
    event.preventDefault();
}

document.addEventListener('contextmenu', disableNativeContextMenu);

// ═══════════════════════════════════════════════════════════════════
// DOM 引用
// ═══════════════════════════════════════════════════════════════════
const $root = document.documentElement;
const $status = document.getElementById('status');
const $metricStrip = document.getElementById('metric-strip');
const $tabBar = document.getElementById('tab-bar');
const $pageBody = document.getElementById('page-body');
const $sidebar = document.getElementById('sidebar');
const $languageSelect = document.getElementById('language-select');
const $themeToggle = document.getElementById('theme-toggle');
const $windowClose = document.getElementById('window-close');

// ═══════════════════════════════════════════════════════════════════
// 状态
// ═══════════════════════════════════════════════════════════════════
const WIN_KEY = 'yokiframe-window-size';
const MIN_WINDOW_WIDTH = 960;
const MIN_WINDOW_HEIGHT = 640;
const THEME_KEY = 'yokiframe-theme';
const THEME_LABELS = {
    light: 'theme.light',
    dark: 'theme.dark',
};
const FONT_SETTINGS_KEY = 'yokiframe-font-settings';
const DEFAULT_FONT_PREFERENCE = { preset: 'system', customBody: '' };
const STATUS_POLL_INTERVAL_MS = 6000;
const BRIDGE_DIAGNOSTICS_INTERVAL_MS = 5000;
const KIT_REACTIVE_REFRESH_THROTTLE_MS = 220;
const POOLKIT_REACTIVE_REFRESH_THROTTLE_MS = 80;
const KIT_INTERACTION_REFRESH_DEFER_MS = 260;
const KIT_INTERACTION_IDLE_MS = 360;
const FSM_DETAIL_RECONCILE_MS = 1800;
const FSM_SELECTION_RENDER_IDLE_TIMEOUT_MS = 220;
const EVENTKIT_SNAPSHOT_RECONCILE_MS = 1600;
const EVENTKIT_MAX_RECENT_EVENTS = 160;
const WINDOW_RESIZE_RESET_IDLE_MS = 700;
const WINDOW_RESIZE_RESET_FAILSAFE_MS = 10000;
const SIDEBAR_INDICATOR_SETTLE_MS = 300;
const TAB_INDICATOR_SETTLE_MS = 240;
const WINDOW_RESIZE_DIRECTIONS = {
    north: 'North',
    south: 'South',
    east: 'East',
    west: 'West',
    'north-east': 'NorthEast',
    'north-west': 'NorthWest',
    'south-east': 'SouthEast',
    'south-west': 'SouthWest',
};
const YOKIFRAME_PACKAGE_CANDIDATES = [
    '../../Assets/YokiFrame/package.json',
    '../../../../../../package.json',
];
const YOKIFRAME_ICON_CANDIDATES = [
    'assets/yoki.png',
    '../../Assets/YokiFrame/Core/Editor/Resources/yoki.png',
    'Core/Editor/Resources/yoki.png',
    '../../../../../../Core/Editor/Resources/yoki.png',
];
const AI_SKILL_DEFAULT_SKILLS = [
    { name: 'yokiframe', labelKey: 'ai_skill.skill.yokiframe' },
    { name: 'yokiframe-usage', labelKey: 'ai_skill.skill.yokiframe_usage' },
    { name: 'yokiframe-command-bridge', labelKey: 'ai_skill.skill.command_bridge' },
    { name: 'yokiframe-editor', labelKey: 'ai_skill.skill.editor' },
];
const AI_SKILL_DEFAULT_TARGETS = [
    { id: 'claude', label: 'Claude Code', relativePath: '.claude/skills' },
    { id: 'codex', label: 'Codex', relativePath: '.codex/skills' },
    { id: 'cursor', label: 'Cursor', relativePath: '.cursor/skills' },
    { id: 'windsurf', label: 'Windsurf', relativePath: '.windsurf/skills' },
    { id: 'github-copilot', label: 'GitHub Copilot', relativePath: '.github/skills' },
    { id: 'agents', label: 'Agents', relativePath: '.agents/skills' },
];
const AI_SKILL_CUSTOM_TARGET = { id: 'custom', label: 'Custom', relativePath: '.custom/skills' };
const DEFAULT_YOKIFRAME_PACKAGE = {
    version: '2.0.0-pre',
    repository: { url: 'https://github.com/HinataYoki/YokiFrame' },
};
const FRAMEWORK_COMMAND_CATALOG = {
    System: [
        { action: 'ping', label: 'ping', descriptionKey: 'command.system.ping' },
        { action: 'status', label: 'status', descriptionKey: 'command.system.status' },
        { action: 'bridge_status', label: 'bridge_status', descriptionKey: 'command.system.bridge_status' },
        { action: 'list_commands', label: 'list_commands', descriptionKey: 'command.system.list_commands' },
    ],
    FsmKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.fsmkit.get_workbench_snapshot' },
        { action: 'list_all', label: 'list_all', descriptionKey: 'command.fsmkit.list_all' },
    ],
    EventKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.eventkit.get_workbench_snapshot' },
        { action: 'list_registrations', label: 'list_registrations', descriptionKey: 'command.eventkit.list_registrations' },
        { action: 'get_recent_events', label: 'get_recent_events', descriptionKey: 'command.eventkit.get_recent_events' },
    ],
    LogKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.logkit.get_workbench_snapshot' },
        { action: 'get_settings', label: 'get_settings', descriptionKey: 'command.logkit.get_settings' },
        { action: 'set_settings', label: 'set_settings', descriptionKey: 'command.logkit.set_settings' },
        { action: 'open_log_folder', label: 'open_log_folder', descriptionKey: 'command.logkit.open_log_folder' },
        { action: 'read_log_file', label: 'read_log_file', descriptionKey: 'command.logkit.read_log_file' },
        { action: 'decrypt_log_file', label: 'decrypt_log_file', descriptionKey: 'command.logkit.decrypt_log_file' },
        { action: 'clear_history', label: 'clear_history', descriptionKey: 'command.logkit.clear_history' },
    ],
    PoolKit: [
        { kit: 'PoolKit', action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.poolkit.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.poolkit.stats' },
        { action: 'list_pools', label: 'list_pools', descriptionKey: 'command.poolkit.list_pools' },
        { action: 'check_leak', label: 'check_leak', descriptionKey: 'command.poolkit.check_leak' },
    ],
    ActionKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.actionkit.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.actionkit.stats' },
        { action: 'set_stack_trace', label: 'set_stack_trace', descriptionKey: 'command.actionkit.set_stack_trace' },
        { action: 'clear_stack_trace', label: 'clear_stack_trace', descriptionKey: 'command.actionkit.clear_stack_trace' },
    ],
    ResKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.reskit.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.reskit.stats' },
        { action: 'list_resources', label: 'list_resources', descriptionKey: 'command.reskit.list_resources' },
        { action: 'get_unload_history', label: 'get_unload_history', descriptionKey: 'command.reskit.get_unload_history' },
    ],
    AudioKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.audiokit.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.audiokit.stats' },
        { action: 'list_voices', label: 'list_voices', descriptionKey: 'command.audiokit.list_voices' },
        { action: 'get_history', label: 'get_history', descriptionKey: 'command.audiokit.get_history' },
    ],
    SaveKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.savekit.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.savekit.stats' },
        { action: 'list_slots', label: 'list_slots', descriptionKey: 'command.savekit.list_slots' },
    ],
    LocalizationKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.localizationkit.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.localizationkit.stats' },
        { action: 'list_languages', label: 'list_languages', descriptionKey: 'command.localizationkit.list_languages' },
    ],
    SceneKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.scenekit.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.scenekit.stats' },
        { action: 'list_scenes', label: 'list_scenes', descriptionKey: 'command.scenekit.list_scenes' },
    ],
    SpatialKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.spatialkit.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.spatialkit.stats' },
        { action: 'list_indexes', label: 'list_indexes', descriptionKey: 'command.spatialkit.list_indexes' },
    ],
    InputKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.inputkit.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.inputkit.stats' },
        { action: 'list_actions', label: 'list_actions', descriptionKey: 'command.inputkit.list_actions' },
        { action: 'list_contexts', label: 'list_contexts', descriptionKey: 'command.inputkit.list_contexts' },
    ],
    UIKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.uikit.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.uikit.stats' },
        { action: 'list_panels', label: 'list_panels', descriptionKey: 'command.uikit.list_panels' },
        { action: 'list_stacks', label: 'list_stacks', descriptionKey: 'command.uikit.list_stacks' },
    ],
    Architecture: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.architecture.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.architecture.stats' },
        { action: 'list_architectures', label: 'list_architectures', descriptionKey: 'command.architecture.list_architectures' },
        { action: 'get_architecture_detail', label: 'get_architecture_detail', descriptionKey: 'command.architecture.get_architecture_detail' },
    ],
    SingletonKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.singletonkit.get_workbench_snapshot' },
        { action: 'stats', label: 'stats', descriptionKey: 'command.singletonkit.stats' },
        { action: 'list_singletons', label: 'list_singletons', descriptionKey: 'command.singletonkit.list_singletons' },
    ],
};
const KIT_PAGE_ID_TO_KIT = {
    architecture: 'Architecture',
    eventkit: 'EventKit',
    fsmkit: 'FsmKit',
    logkit: 'LogKit',
    poolkit: 'PoolKit',
    reskit: 'ResKit',
    singletonkit: 'SingletonKit',
    actionkit: 'ActionKit',
    audiokit: 'AudioKit',
    inputkit: 'InputKit',
    localizationkit: 'LocalizationKit',
    savekit: 'SaveKit',
    scenekit: 'SceneKit',
    spatialkit: 'SpatialKit',
    tablekit: 'TableKit',
    uikit: 'UIKit',
};
const KIT_EVENT_ALIASES = {
    fsm: 'fsmkit',
    pool: 'poolkit',
    event: 'eventkit',
    log: 'logkit',
    architecture: 'architecture',
    singleton: 'singletonkit',
    res: 'reskit',
    table: 'tablekit',
    audio: 'audiokit',
    save: 'savekit',
    localization: 'localizationkit',
    scene: 'scenekit',
    spatial: 'spatialkit',
    input: 'inputkit',
    ui: 'uikit',
    action: 'actionkit',
};
const EVENTKIT_REACTIVE_EVENT_TYPE = 'event_update';
const EVENTKIT_HEALTH_CLASS_BY_STATUS = {
    balanced: 'eventkit-health--balanced',
    no_receiver: 'eventkit-health--no-receiver',
    no_sender: 'eventkit-health--no-sender',
    leak_risk: 'eventkit-health--leak-risk',
};

let connected = false;
let lastConnectedState = null;
let activePage = 'system';
let activeTab = null;
let latestStatusSummary = null;
let latestStatusRaw = null;
let latestBridgeSummary = null;
let latestPingSummary = null;
let latestBridgeStatusResponse = null;
let bridgeDiagnosticsInFlight = false;
let lastBridgeDiagnosticsAt = 0;
let navigationSeq = 0;
let pendingNavigationRenderTask = 0;
let yokiFrameEditorBootstrapped = false;
let statusPollInFlight = false;
let statusPollQueued = false;
let statusPollResumeTimer = 0;
let pendingLogRenderFrame = 0;
let renderedLogCount = 0;
let pendingSystemPanelFrame = 0;
let lastFrameworkStatusPanelHtml = '';
let lastBridgeHealthPanelHtml = '';
let activeTheme = localStorage.getItem(THEME_KEY) || 'light';
let fontPreferenceState = readStoredFontPreference();
let commandComposerState = { kit: 'System', action: 'ping' };
let aiSkillInstallerStatus = null;
let aiSkillInstallerInFlight = false;
let aiSkillSelectedName = 'yokiframe';
let frameworkCommandCatalog = null;
let frameworkCommandCatalogLoaded = false;
let frameworkCommandCatalogInFlight = false;
let yokiFramePackageInfo = DEFAULT_YOKIFRAME_PACKAGE;
let _winSaveTimer = 0;
let _windowShown = false;
let workspaceResizeFrame = 0;
let minimumWindowResizeInFlight = false;
let nativeResizeResetTimer = 0;
let nativeResizeDragActive = false;
const kitReactiveRefreshHandlers = new Map();
const kitReactiveRefreshState = new Map();
const pushListenerUnlisteners = [];
let pushListenersReady = false;
let pushListenerSetupPromise = null;
let kitInteractionActiveUntil = 0;
let kitInteractionIdleTimer = 0;
let sidebarKitAvailabilitySyncInProgress = false;
let currentHeroMeta = null;
let currentHeroActionsHtml = '';
let heroActionPromotionFrame = 0;
let sidebarIndicatorMotionTimer = 0;
let tabIndicatorMotionTimer = 0;
const HERO_CARD_TARGET_SELECTOR = '.kit-toolbar, .audio-master-strip, .tablekit-command-center, .kit-panel, .panel, .doc-hero, .developer-context-strip';
const HERO_INTRO_CARD_PAGE_IDS = new Set(['fsmkit', 'logkit', 'poolkit', 'reskit', 'singletonkit']);

function setConnectionStatus(isConnected) {
    connected = isConnected;
    $status.textContent = isConnected ? t('common.connected') : t('common.disconnected');
    $status.className = `status-badge ${isConnected ? 'connected' : 'disconnected'}`;
    syncFrameworkCommandControls();
    if (isConnected) {
        void refreshFrameworkCommandCatalog({ force: true });
    } else {
        frameworkCommandCatalogLoaded = false;
    }
}

function summarizeStatus(status) {
    if (BridgeDiagnostics?.summarizeStatus) {
        return BridgeDiagnostics.summarizeStatus(status) ?? {};
    }
    const engine = status?.engines?.[0] ?? {};
    return {
        connected: status?.connected ?? false,
        engineId: engine.engineId ?? '--',
        engineLabel: `${engine.engine ?? '--'} ${engine.version ?? ''}`.trim() || '--',
        heartbeatPath: engine.heartbeatPath ?? '--',
        projectPath: engine.projectPath ?? '--',
    };
}

function summarizePing(response) {
    if (BridgeDiagnostics?.summarizePing) {
        return BridgeDiagnostics.summarizePing(response) ?? {};
    }
    return {
        statusLabel: response?.status ?? '--',
        requestId: response?.requestId ?? '--',
        engineId: response?.engineId ?? '--',
        reason: response?.status === 'error' ? formatBridgeError(response) : JSON.stringify(response?.data ?? {}),
    };
}

function summarizeBridgeStatus(payload) {
    if (BridgeDiagnostics?.summarizeBridgeStatus) {
        return BridgeDiagnostics.summarizeBridgeStatus(payload) ?? {};
    }
    return {
        queueLabel: payload?.pendingCommandCount ?? '--',
        deadletterLabel: payload?.deadletterCount ?? '--',
        resultLabel: payload?.resultCount ?? '--',
        storageLabel: payload?.storageLabel ?? '--',
        oldestFileLabel: payload?.oldestCommandFile ?? '--',
        backpressureLabel: payload?.backpressureLabel ?? '--',
        bridgeBusyLabel: payload?.bridgeBusy ?? '--',
        lastErrorLabel: payload?.lastError ?? '--',
    };
}

function isWindowInteractive() {
    if (document.hidden) return false;
    if (typeof document.hasFocus === 'function' && !document.hasFocus()) return false;
    return true;
}

function handleInteractiveFocusResume() {
    if (document.hidden) return;
    clearTimeout(statusPollResumeTimer);
    statusPollResumeTimer = setTimeout(() => {
        statusPollResumeTimer = 0;
        pollStatus({ force: true });
    }, 120);
}

async function pollStatus({ force = false } = {}) {
    if (!invoke) return;
    if (!force && !isWindowInteractive()) return;
    if (statusPollInFlight) {
        statusPollQueued = true;
        return;
    }

    statusPollInFlight = true;
    try {
        const result = await invoke('get_status');
        const status = JSON.parse(result);
        latestStatusRaw = status;
        latestStatusSummary = summarizeStatus(status);
        syncSidebarKitAvailability();
        const isConnected = status.connected;
        if (isConnected !== lastConnectedState) {
            lastConnectedState = isConnected;
            setConnectionStatus(isConnected);
            if (isConnected) {
                const list = (status.engines ?? []).map(e => `${e.engineId ?? e.engine}:${e.engine} v${e.version ?? ''} ${e.connected === false ? '(stale)' : ''}`.trim()).join(', ');
                addLog(t('connection.engine_connected', list), 'system');
            } else {
                addLog(t('connection.engine_disconnected'), 'error');
            }
        }
        if (activePage === 'system') {
            scheduleSystemPanelUpdate();
            if (isConnected && status.engines) {
                refreshBridgeDiagnosticsThrottled();
            }
        }
        if (activePage === 'tablekit') {
            renderTableKitRegistryStatus();
        }
    } catch (_) { /* IPC unavailable */ }
    finally {
        statusPollInFlight = false;
        if (statusPollQueued) {
            statusPollQueued = false;
            if (isWindowInteractive()) {
                setTimeout(() => pollStatus({ force: true }), 0);
            }
        }
    }
}

// ═══════════════════════════════════════════════════════════════════
// 命令桥
// ═══════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════
// 指标辅助
// ═══════════════════════════════════════════════════════════════════

// ═══════════════════════════════════════════════════════════════════
// 面板构建器
// ═══════════════════════════════════════════════════════════════════
function resolveIconName(icon) {
    const aliases = {
        '□': 'framework',
        '≡': 'docs',
        '↗': 'bridge',
        '◎': 'status',
        '●': 'pool',
        '■': 'res',
        '○': 'fsm',
        '↔': 'action',
        '!': 'warning',
        '⌕': 'search',
        '\u{1F504}': 'refresh',
        '\u{1F4CB}': 'docs',
        '\u{1F4CC}': 'status',
        '\u{1F4DC}': 'log',
        '\u{1F500}': 'bridge',
        '\u{26A0}': 'warning',
    };
    return aliases[icon] ?? icon ?? 'empty';
}

function panel(title, bodyHtml, icon = '') {
    const iconHtml = icon ? `<span class="panel-header-icon">${svgIcon(resolveIconName(icon), 'shell-icon')}</span>` : '';
    return `
        <div class="panel fade-in">
            <div class="panel-header">
                <div class="panel-header-text">${iconHtml}<span class="panel-title">${title}</span></div>
            </div>
            <div class="panel-body">${bodyHtml}</div>
        </div>`;
}

function emptyState(icon, text) {
    return `<div class="empty-state"><div class="empty-state-icon">${svgIcon(resolveIconName(icon), 'shell-icon')}</div><div class="empty-state-text">${text}</div></div>`;
}

// HTML 文本转义（进 DOM 前必经，防注入与破坏标记）。
function escapeHtml(s) {
    return String(s)
        .replace(/&/g, '&amp;')
        .replace(/</g, '&lt;')
        .replace(/>/g, '&gt;')
        .replace(/"/g, '&quot;');
}

// CSS 选择器转义：用于把任意 id 安全拼进 querySelector('#'+id)。
// 优先用原生 CSS.escape，回退到对非单词字符做反斜杠转义。
function cssEscape(s) {
    if (window.CSS && typeof window.CSS.escape === 'function') return window.CSS.escape(s);
    return String(s).replace(/[^\w-]/g, '\\$&');
}

function prefersReducedMotion() {
    return !!window.matchMedia?.('(prefers-reduced-motion: reduce)')?.matches;
}

function syncSidebarActiveIndicator() {
    updateSidebarActiveIndicator(activePage);
}

function getSidebarScrollRoot() {
    return $sidebar?.querySelector('.sidebar-scroll-card') || $sidebar;
}

function updateSidebarActiveIndicator(pageId = activePage, options = {}) {
    if (!$sidebar) return;
    const { scroll = true, animate = true } = options;
    const scrollRoot = getSidebarScrollRoot();
    const indicator = scrollRoot?.querySelector('.sidebar-active-indicator');
    const item = scrollRoot?.querySelector(`.sidebar-item[data-page="${cssEscape(pageId)}"]`);
    if (!indicator || !item) return;

    if (scroll) {
        item.scrollIntoView({
            block: 'nearest',
            inline: 'nearest',
            behavior: prefersReducedMotion() ? 'auto' : 'smooth',
        });
        requestAnimationFrame(() => updateSidebarActiveIndicator(pageId, { scroll: false, animate: false }));
    }

    const sidebarRect = scrollRoot.getBoundingClientRect();
    const itemRect = item.getBoundingClientRect();
    const top = itemRect.top - sidebarRect.top + scrollRoot.scrollTop;
    const nextTransform = `translateY(${Math.round(top)}px)`;
    const didMove = indicator.style.transform && indicator.style.transform !== nextTransform;

    indicator.style.height = `${Math.round(itemRect.height)}px`;
    indicator.style.transform = nextTransform;
    indicator.classList.add('is-visible');

    if (!animate || !didMove) return;

    if (sidebarIndicatorMotionTimer) {
        clearTimeout(sidebarIndicatorMotionTimer);
    }
    indicator.classList.remove('is-settling');
    indicator.classList.add('is-moving');
    sidebarIndicatorMotionTimer = setTimeout(() => {
        indicator.classList.remove('is-moving');
        indicator.classList.add('is-settling');
        sidebarIndicatorMotionTimer = setTimeout(() => {
            indicator.classList.remove('is-settling');
            sidebarIndicatorMotionTimer = 0;
        }, SIDEBAR_INDICATOR_SETTLE_MS);
    }, SIDEBAR_INDICATOR_SETTLE_MS);
}

function bindShellControls() {
    shellI18n?.bindShellControls();
}

function updateFrameworkThemeSummary() {
    shellI18n?.updateFrameworkThemeSummary();
}

function getFontSettingsHelper() {
    return window.YokiFontSettings || null;
}

function readStoredFontPreference() {
    const helper = getFontSettingsHelper();
    if (!helper) return { ...DEFAULT_FONT_PREFERENCE };

    try {
        return helper.readFontSettings(localStorage, FONT_SETTINGS_KEY);
    } catch (e) {
        console.warn('[YokiFrame] 字体偏好读取失败', e);
        return { ...DEFAULT_FONT_PREFERENCE };
    }
}

function normalizeFontPreference(value) {
    const helper = getFontSettingsHelper();
    return helper ? helper.normalizeFontSettings(value) : { ...DEFAULT_FONT_PREFERENCE };
}

function getResolvedFontPreference() {
    const helper = getFontSettingsHelper();
    if (!helper) {
        return {
            preset: 'system',
            customBody: '',
            label: t('font.default'),
            hint: t('font.module_not_loaded'),
        };
    }

    return helper.resolveFontSettings(fontPreferenceState);
}

function applyFontPreference({ silent = false } = {}) {
    const helper = getFontSettingsHelper();
    if (!helper) return null;

    fontPreferenceState = normalizeFontPreference(fontPreferenceState);
    const resolved = helper.applyFontSettings($root, fontPreferenceState);
    helper.writeFontSettings(localStorage, FONT_SETTINGS_KEY, fontPreferenceState);
    updateFrameworkFontSummary(resolved);

    if (!silent && resolved) {
        addLog(t('connection.font_switched', resolved.label), 'system');
    }

    return resolved;
}

function applyInitialFontPreference() {
    try {
        applyFontPreference({ silent: true });
    } catch (e) {
        console.warn('[YokiFrame] 字体偏好应用失败', e);
        fontPreferenceState = { ...DEFAULT_FONT_PREFERENCE };
    }
}

function renderFontPresetOptions() {
    const helper = getFontSettingsHelper();
    const presets = helper?.PRESETS || [];
    return presets.map(preset => {
        const selected = preset.id === fontPreferenceState.preset ? ' selected' : '';
        return `<option value="${escapeHtml(preset.id)}"${selected}>${escapeHtml(preset.label)}</option>`;
    }).join('');
}

function renderFontPreferencePanel() {
    return panel(t('font.title'),
        renderFontPreferenceContent(),
        'font');
}

function renderFontPreferenceContent() {
    const resolved = getResolvedFontPreference();
    const customDisabled = fontPreferenceState.preset !== 'custom' ? ' disabled' : '';
    return `<div class="font-preference">
        <div class="font-preference__summary">
            <span>${t('font.current')}</span>
            <strong id="framework-font-value">${escapeHtml(resolved.label)}</strong>
            <code id="framework-font-hint">${escapeHtml(resolved.hint || '')}</code>
        </div>
        <div class="font-preference__controls">
            <label class="font-preference__field">
                <span>${t('font.preset')}</span>
                <select id="font-preset-select" class="cmd-select" aria-label="${t('font.preset')}">
                    ${renderFontPresetOptions()}
                </select>
            </label>
            <label class="font-preference__field font-preference__field--custom">
                <span>${t('font.custom')}</span>
                <input id="font-custom-input" class="cmd-input" type="text" value="${escapeHtml(fontPreferenceState.customBody)}" placeholder="LXGW WenKai"${customDisabled}>
            </label>
            <button id="font-reset-btn" class="btn btn-secondary btn-sm" type="button">${t('font.reset')}</button>
        </div>
    </div>`;
}

function updateFrameworkFontSummary(resolved = getResolvedFontPreference()) {
    const fontValue = document.getElementById('framework-font-value');
    const fontHint = document.getElementById('framework-font-hint');
    const presetSelect = document.getElementById('font-preset-select');
    const customInput = document.getElementById('font-custom-input');

    if (fontValue) fontValue.textContent = resolved.label;
    if (fontHint) fontHint.textContent = resolved.hint || '';
    if (presetSelect) presetSelect.value = fontPreferenceState.preset;
    if (customInput) {
        customInput.disabled = fontPreferenceState.preset !== 'custom';
        if (customInput.value !== fontPreferenceState.customBody) {
            customInput.value = fontPreferenceState.customBody;
        }
    }
}

function bindFontPreferenceControls() {
    const helper = getFontSettingsHelper();
    if (!helper) return;

    const presetSelect = document.getElementById('font-preset-select');
    const customInput = document.getElementById('font-custom-input');
    const resetButton = document.getElementById('font-reset-btn');

    if (presetSelect && presetSelect.dataset.bound !== '1') {
        presetSelect.dataset.bound = '1';
        presetSelect.addEventListener('change', () => {
            fontPreferenceState = normalizeFontPreference({
                preset: presetSelect.value,
                customBody: customInput?.value || fontPreferenceState.customBody,
            });
            applyFontPreference();
            updateFrameworkFontSummary();
            if (fontPreferenceState.preset === 'custom') customInput?.focus();
        });
    }

    if (customInput && customInput.dataset.bound !== '1') {
        customInput.dataset.bound = '1';
        customInput.addEventListener('input', () => {
            fontPreferenceState = normalizeFontPreference({
                preset: 'custom',
                customBody: customInput.value,
            });
            applyFontPreference({ silent: true });
        });
        customInput.addEventListener('change', () => {
            fontPreferenceState = normalizeFontPreference({
                preset: 'custom',
                customBody: customInput.value,
            });
            applyFontPreference();
        });
    }

    if (resetButton && resetButton.dataset.bound !== '1') {
        resetButton.dataset.bound = '1';
        resetButton.addEventListener('click', () => {
            fontPreferenceState = normalizeFontPreference(DEFAULT_FONT_PREFERENCE);
            applyFontPreference();
            updateFrameworkFontSummary();
        });
    }
}

// ═══════════════════════════════════════════════════════════════════
// 页面：System 概览
// ═══════════════════════════════════════════════════════════════════
function renderSystemMetrics(engines) {
    const count = engines?.length ?? 0;
    renderMetrics([
        { title: t('system.metrics.engine'), value: count, hint: t('system.metrics.engine_hint') },
        { title: t('system.metrics.bridge'), value: 'File I/O', hint: t('system.metrics.bridge_hint') },
        { title: t('system.metrics.frontend'), value: 'Tauri', hint: t('system.metrics.frontend_hint') },
    ]);
}

async function refreshBridgeDiagnosticsThrottled(force = false) {
    if (!invoke || !connected || bridgeDiagnosticsInFlight) return;
    const now = Date.now();
    if (!force && now - lastBridgeDiagnosticsAt < BRIDGE_DIAGNOSTICS_INTERVAL_MS) return;

    bridgeDiagnosticsInFlight = true;
    lastBridgeDiagnosticsAt = now;
    try {
        const response = await sendCommand({
            kit: 'System',
            action: 'bridge_status',
            syncControls: false,
            silentLog: true,
        });
        if (response) {
            latestBridgeStatusResponse = response;
            latestBridgeSummary = summarizeBridgeStatus(response.data ?? response);
        }
    } catch (e) {
        latestBridgeSummary = {
            queueLabel: '--',
            deadletterLabel: '--',
            resultLabel: '--',
            storageLabel: '--',
            oldestFileLabel: '--',
            backpressureLabel: '--',
            bridgeBusyLabel: '--',
            lastErrorLabel: String(e)
        };
    } finally {
        bridgeDiagnosticsInFlight = false;
        scheduleSystemPanelUpdate();
    }
}

function renderFrameworkStatusPanel() {
    return panel(t('system.engine_status'),
        renderFrameworkStatusContent(),
        'framework');
}

function renderFrameworkStatusContent() {
    const status = latestStatusSummary ?? {};
    const hasStatus = latestStatusSummary !== null;
    const isConnected = hasStatus ? !!status.connected : connected;
    const engineId = status.engineId && status.engineId !== '--' ? status.engineId : '--';
    const engineLabel = status.engineLabel ?? '--';
    const engineCount = Number.isFinite(Number(status.engineCount)) ? Number(status.engineCount) : (isConnected ? 1 : 0);
    const commandRoot = engineId !== '--'
        ? `.yokiframe/engines/${engineId}/commands`
        : '.yokiframe/engines/<engineId>/commands';
    return `<div class="framework-grid framework-grid--status">
        ${diagnosticTile(t('system.connection'), hasStatus ? (isConnected ? t('common.connected') : t('common.disconnected')) : t('system.checking'), hasStatus ? (isConnected ? engineLabel : t('system.waiting_heartbeat')) : t('system.reading'), hasStatus ? (isConnected ? 'success' : 'error') : 'warning')}
        ${diagnosticTile(t('system.engine'), engineId, engineCount ? t('system.registry_count', engineCount, engineLabel) : t('system.no_host_found'))}
        ${diagnosticTile(t('system.heartbeat_file'), status.heartbeatPath ?? '--', t('system.heartbeat_hint'))}
        ${diagnosticTile(t('system.command_path'), commandRoot, t('system.command_path_hint'))}
        ${diagnosticTile(t('system.event_stream'), 'JSONL', t('system.event_stream_hint'))}
    </div>`;
}

function updateFrameworkStatusPanel(force = false) {
    const container = document.getElementById('framework-status-panel');
    if (!container) return;
    const html = renderFrameworkStatusContent();
    if (!force && html === lastFrameworkStatusPanelHtml) return;
    container.innerHTML = html;
    lastFrameworkStatusPanelHtml = html;
}

function renderDeveloperContextStrip() {
    const status = latestStatusSummary ?? {};
    const hostLabel = status.engineLabel && status.engineLabel !== '--' ? status.engineLabel : 'Unity / Godot';
    const engineId = status.engineId && status.engineId !== '--' ? status.engineId : t('system.waiting_host');
    const bridgeLabel = latestBridgeSummary?.queueLabel && latestBridgeSummary.queueLabel !== '--'
        ? `${t('system.queue')} ${latestBridgeSummary.queueLabel}`
        : 'FileBridge';
    return `
        <section class="developer-context-strip" aria-label="${t('system.workbench_context')}">
            <div class="developer-context-strip__main">
                <span class="developer-context-strip__label">Native Debug Console</span>
                <strong>${t('system.workbench_title')}</strong>
                <span>${t('system.workbench_desc')}</span>
            </div>
            <div class="developer-context-strip__meta">
                <span><b>${t('system.host_connection')}</b><code>${escapeHtml(hostLabel)}</code></span>
                <span><b>${t('command_panel.refresh_bridge')}</b><code>${escapeHtml(engineId)}</code></span>
                <span><b>${t('system.kit_diagnostic')}</b><code>${escapeHtml(bridgeLabel)}</code></span>
            </div>
        </section>`;
}

function renderBridgeHealthPanel() {
    return panel(t('system.bridge_status'),
        renderBridgeHealthContent(),
        'status');
}

function renderBridgeHealthContent() {
    const bridge = latestBridgeSummary ?? {};
    const raw = latestBridgeStatusResponse?.data ?? latestBridgeStatusResponse ?? {};
    return `<div class="diagnostic-grid diagnostic-grid--compact">
        ${diagnosticTile(t('system.queue'), bridge.queueLabel ?? '--', 'pending / processing')}
        ${diagnosticTile(t('common.result'), bridge.resultLabel ?? '--', `deadletter ${bridge.deadletterLabel ?? '--'}`)}
        ${diagnosticTile(t('system.backpressure'), bridge.backpressureLabel ?? '--', `BridgeBusy ${bridge.bridgeBusyLabel ?? '--'}`, raw.backpressureActive ? 'warning' : 'success')}
        ${diagnosticTile(t('system.recent_error'), bridge.lastErrorLabel ?? '--', raw.lastPollLimitReason ? `limit ${raw.lastPollLimitReason}` : t('system.no_activity_limit'), bridge.lastErrorLabel && bridge.lastErrorLabel !== '--' ? 'error' : 'success')}
    </div>`;
}

function diagnosticTile(label, value, hint = '', tone = 'info') {
    return `<div class="diagnostic-tile diagnostic-tile--${tone}">
        <div class="diagnostic-tile__label">${escapeHtml(label)}</div>
        <div class="diagnostic-tile__value">${escapeHtml(value)}</div>
        ${hint ? `<div class="diagnostic-tile__hint">${escapeHtml(hint)}</div>` : ''}
    </div>`;
}

function updateBridgeHealthPanel(force = false) {
    const container = document.getElementById('bridge-health-panel');
    if (!container) return;
    const html = renderBridgeHealthContent();
    if (!force && html === lastBridgeHealthPanelHtml) return;
    container.innerHTML = html;
    lastBridgeHealthPanelHtml = html;
}

function scheduleSystemPanelUpdate({ force = false } = {}) {
    if (activePage !== 'system') return;
    if (pendingSystemPanelFrame) return;
    pendingSystemPanelFrame = requestAnimationFrame(() => {
        pendingSystemPanelFrame = 0;
        if (activePage !== 'system') return;
        updateFrameworkStatusPanel(force);
        updateFrameworkThemeSummary();
        updateFrameworkFontSummary();
        updateBridgeHealthPanel(force);
    });
}
