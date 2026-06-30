// core/app-state.js
// 应用常量和共享状态
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
const FSMKIT_REACTIVE_REFRESH_THROTTLE_MS = 80;
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
    version: '2.0.0-preview',
    repository: { url: 'https://github.com/HinataYoki/YokiFrame' },
};
const FRAMEWORK_COMMAND_CATALOG = {
    System: [
        { action: 'ping', label: 'ping', descriptionKey: 'command.system.ping' },
        { action: 'status', label: 'status', descriptionKey: 'command.system.status' },
        { action: 'bridge_status', label: 'bridge_status', descriptionKey: 'command.system.bridge_status' },
        { action: 'bridge_status_detail', label: 'bridge_status_detail', descriptionKey: 'command.system.bridge_status_detail' },
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
    ManagedRuntimeKit: [
        { action: 'get_workbench_snapshot', label: 'get_workbench_snapshot', descriptionKey: 'command.managedruntimekit.get_workbench_snapshot' },
        { action: 'list_backends', label: 'list_backends', descriptionKey: 'command.managedruntimekit.list_backends' },
        { action: 'validate', label: 'validate', descriptionKey: 'command.managedruntimekit.validate' },
        { action: 'select_backend', label: 'select_backend', descriptionKey: 'command.managedruntimekit.select_backend' },
        { action: 'run_action', label: 'run_action', descriptionKey: 'command.managedruntimekit.run_action' },
        { action: 'get_backend_settings', label: 'get_backend_settings', descriptionKey: 'command.managedruntimekit.get_backend_settings' },
        { action: 'save_backend_settings', label: 'save_backend_settings', descriptionKey: 'command.managedruntimekit.save_backend_settings' },
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
    managedruntime: 'ManagedRuntimeKit',
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
    managedruntime: 'managedruntime',
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
const HERO_INTRO_CARD_PAGE_IDS = new Set(['fsmkit', 'eventkit', 'logkit', 'poolkit', 'reskit', 'singletonkit']);

function normalizeProjectStorageScopePath(value) {
    const normalized = String(value ?? '').replace(/\\/g, '/').trim().replace(/\/+$/, '');
    if (/^[A-Za-z]:\//.test(normalized)) {
        return `${normalized.charAt(0).toLowerCase()}${normalized.slice(1)}`;
    }
    return normalized;
}

function getProjectStorageScopeIdentifier(status = latestStatusRaw, summary = latestStatusSummary) {
    const summaryPath = summary?.projectPath && summary.projectPath !== '--'
        ? normalizeProjectStorageScopePath(summary.projectPath)
        : '';
    if (summaryPath) return summaryPath;

    const engines = Array.isArray(status?.engines) ? status.engines : [];
    const engine = engines.find(candidate => candidate?.projectPath && candidate.connected !== false)
        || engines.find(candidate => candidate?.projectPath);
    return engine?.projectPath && engine.projectPath !== '--'
        ? normalizeProjectStorageScopePath(engine.projectPath)
        : '';
}

function getProjectScopedStorageKey(baseKey) {
    const scope = getProjectStorageScopeIdentifier();
    return scope ? `${baseKey}::project::${encodeURIComponent(scope)}` : baseKey;
}

function readProjectScopedStorageItem(baseKey) {
    const scopedKey = getProjectScopedStorageKey(baseKey);
    return localStorage.getItem(scopedKey);
}

function writeProjectScopedStorageItem(baseKey, value) {
    localStorage.setItem(getProjectScopedStorageKey(baseKey), value);
}

function syncProjectScopedEditorStorage() {
    let changed = false;
    if (typeof syncTableKitProjectStorageScope === 'function') {
        changed = syncTableKitProjectStorageScope() || changed;
    }
    if (typeof syncAudioKitProjectStorageScope === 'function') {
        changed = syncAudioKitProjectStorageScope() || changed;
    }
    if (typeof syncGraphKitProjectStorageScope === 'function') {
        changed = syncGraphKitProjectStorageScope() || changed;
    }
    return changed;
}
