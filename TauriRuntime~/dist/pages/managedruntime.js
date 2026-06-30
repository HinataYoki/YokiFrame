// pages/managedruntime.js
// ═══════════════════════════════════════════════════════════════════
// 页面：ManagedRuntimeKit
// ═══════════════════════════════════════════════════════════════════
const managedRuntimeState = {
    snapshot: null,
    selectedBackendId: null,
    lastResult: null,
    renderSignature: '',
};

const MANAGED_RUNTIME_ACTION_HINTS = [
    { actionId: 'enable_backend', labelKey: 'managedruntime.action.enable_backend', descriptionKey: 'managedruntime.action_desc.enable_backend', fallback: 'Unity LeanCLR / Godot .NET' },
    { actionId: 'install_local_il2cpp', labelKey: 'managedruntime.action.install_local_il2cpp', descriptionKey: 'managedruntime.action_desc.install_local_il2cpp', fallback: 'Unity LeanCLR local IL2CPP' },
    { actionId: 'generate_aot_rules', labelKey: 'managedruntime.action.generate_aot_rules', descriptionKey: 'managedruntime.action_desc.generate_aot_rules', fallback: 'AOT rule generator' },
    { actionId: 'compile_dll', labelKey: 'managedruntime.action.compile_dll', descriptionKey: 'managedruntime.action_desc.compile_dll', fallback: 'managed DLL build' },
    { actionId: 'build_player', labelKey: 'managedruntime.action.build_player', descriptionKey: 'managedruntime.action_desc.build_player', fallback: 'Unity or Godot player build' },
    { actionId: 'run_setup_pipeline', labelKey: 'managedruntime.action.run_setup_pipeline', descriptionKey: 'managedruntime.action_desc.run_setup_pipeline', fallback: 'setup pipeline' },
    { actionId: 'open_backend_settings', labelKey: 'managedruntime.action.open_backend_settings', descriptionKey: 'managedruntime.action_desc.open_backend_settings', fallback: 'Unity LeanCLR settings' },
];

const MANAGED_RUNTIME_AVAILABILITY_KEYS = {
    available: 'managedruntime.availability.available',
    notinstalled: 'managedruntime.availability.not_installed',
    unavailable: 'managedruntime.availability.unavailable',
};

const MANAGED_RUNTIME_CAPABILITY_KEYS = {
    HostExecution: 'managedruntime.capability.host_execution',
    AssemblyInspection: 'managedruntime.capability.assembly_inspection',
    DynamicAssemblyLoad: 'managedruntime.capability.dynamic_assembly_load',
    AotCompilation: 'managedruntime.capability.aot_compilation',
    Interpreter: 'managedruntime.capability.interpreter',
    HotUpdateAssembly: 'managedruntime.capability.hot_update_assembly',
    BuildPipelineControl: 'managedruntime.capability.build_pipeline_control',
    Diagnostics: 'managedruntime.capability.diagnostics',
};

const MANAGED_RUNTIME_HOST_KEYS = {
    Host: 'managedruntime.host.host',
    Unity: 'managedruntime.host.unity',
    Godot: 'managedruntime.host.godot',
};

const MANAGED_RUNTIME_MODE_KEYS = {
    Host: 'managedruntime.mode.host',
    'AOT + Interpreter': 'managedruntime.mode.aot_interpreter',
    'Godot .NET': 'managedruntime.mode.godot_dotnet',
};

function renderManagedRuntimePage() {
    $pageBody.classList.add('content-body--managedruntime');
    setHero(
        t('managedruntime.title'),
        t('managedruntime.subtitle'),
        t('managedruntime.tab'),
        'status',
        `<button class="btn btn-primary btn-sm" onclick="refreshManagedRuntime()">${t('common.refresh')}</button>`
    );
    clearTabs();
    managedRuntimeState.renderSignature = '';
    loadManagedRuntimeWorkbench();
}

async function refreshManagedRuntime() { loadManagedRuntimeWorkbench(); }

function normalizeManagedRuntimeStatePayload(data) {
    const source = data?.data ?? data ?? {};
    const backends = Array.isArray(source.backends)
        ? source.backends
        : (Array.isArray(source) ? source : []);
    return {
        currentBackendId: source.currentBackendId ?? '',
        validation: source.validation ?? null,
        backends: backends.map(normalizeManagedRuntimeBackend),
    };
}

function normalizeManagedRuntimeBackend(backend) {
    const source = backend ?? {};
    return {
        ...source,
        settings: normalizeManagedRuntimeSettings(source.settings),
    };
}

function normalizeManagedRuntimeSettings(settings) {
    if (!settings || typeof settings !== 'object') return null;
    return {
        supported: settings.supported !== false,
        backendId: settings.backendId ?? '',
        source: settings.source ?? '',
        settingsPath: settings.settingsPath ?? '',
        installRootDir: settings.installRootDir ?? '',
        localIl2CppPath: settings.localIl2CppPath ?? '',
        canOpenNativePanel: settings.canOpenNativePanel === true,
        enable: settings.enable === true,
        layoutValidation: settings.layoutValidation === true,
        enablePgoProfile: settings.enablePgoProfile === true,
        ruleFiles: Array.isArray(settings.ruleFiles) ? settings.ruleFiles : [],
        pgoRuleFiles: Array.isArray(settings.pgoRuleFiles) ? settings.pgoRuleFiles : [],
        lazyLoadedAssemblyNames: Array.isArray(settings.lazyLoadedAssemblyNames) ? settings.lazyLoadedAssemblyNames : [],
        hotUpdateAssemblyNames: Array.isArray(settings.hotUpdateAssemblyNames) ? settings.hotUpdateAssemblyNames : [],
        gcMode: settings.gcMode || 'MarkSweep',
        enableGCDebug: settings.enableGCDebug === true,
        message: settings.message ?? '',
    };
}

async function fetchManagedRuntimeWorkbenchState() {
    return await fetchKitWorkbenchState('ManagedRuntimeKit', normalizeManagedRuntimeStatePayload, {
        skipTelemetry: true,
        forceCommandRefresh: true,
    });
}

async function loadManagedRuntimeWorkbench() {
    if (!invoke || !connected) {
        $pageBody.innerHTML = emptyState('runtime', t('managedruntime.need_runtime_bridge'));
        clearMetrics();
        return;
    }

    try {
        const state = await fetchManagedRuntimeWorkbenchState();
        managedRuntimeState.snapshot = state;
        reconcileManagedRuntimeSelection(state);
        clearMetrics();

        const html = renderManagedRuntimeWorkbench(state);
        const signature = makeStableSignature({
            state,
            selected: managedRuntimeState.selectedBackendId,
            result: managedRuntimeState.lastResult,
        });
        renderWorkbenchHtmlStable(managedRuntimeState, html, signature, bindManagedRuntimeActions);
    } catch (e) {
        if (!canSendRuntimeKitCommand('ManagedRuntimeKit')) {
            showRuntimeKitUnavailable('ManagedRuntimeKit', t('managedruntime.short_label'));
            return;
        }
        $pageBody.innerHTML = panel(t('common.error'), `<span style="color:var(--error)">${escapeHtml(e)}</span>`, '!');
    }
}

function reconcileManagedRuntimeSelection(state) {
    const backends = Array.isArray(state?.backends) ? state.backends : [];
    if (!backends.length) {
        managedRuntimeState.selectedBackendId = null;
        return null;
    }

    let selected = backends.find(item => item.backendId === managedRuntimeState.selectedBackendId);
    if (!selected && state.currentBackendId) {
        selected = backends.find(item => item.backendId === state.currentBackendId);
    }
    if (!selected) selected = backends[0];
    managedRuntimeState.selectedBackendId = selected.backendId;
    return selected;
}

function renderManagedRuntimeWorkbench(state) {
    const backends = Array.isArray(state?.backends) ? state.backends : [];
    const selected = backends.find(item => item.backendId === managedRuntimeState.selectedBackendId) ?? null;
    return `<div class="kit-workbench kit-workbench--managedruntime">
        <div class="kit-workbench-grid kit-workbench-grid--managedruntime">
            <section class="kit-panel kit-panel--list">
                <div class="kit-panel__head">
                    <div>
                        <div class="kit-panel__title">${renderKitTitle('status', t('managedruntime.runtime_backends'))}</div>
                        <div class="kit-panel__desc">${t('managedruntime.current_backend', escapeHtml(state?.currentBackendId || '--'))}</div>
                    </div>
                    <span class="kit-panel__count">${escapeHtml(backends.length)}</span>
                </div>
                <div class="kit-resource-list" data-kit-scroll-key="managedruntime-backends">${renderManagedRuntimeBackends(backends)}</div>
            </section>
            ${renderManagedRuntimeDetail(selected, state?.validation)}
        </div>
    </div>`;
}

function renderManagedRuntimeBackends(backends) {
    if (!Array.isArray(backends) || !backends.length) {
        return emptyState('runtime', t('managedruntime.no_backends'));
    }

    return backends.map(item => {
        const selected = item.backendId === managedRuntimeState.selectedBackendId;
        const availability = String(item.availability ?? '');
        const available = availability.toLowerCase() === 'available';
        return `<button class="kit-list-row${selected ? ' active' : ''}" type="button" data-managedruntime-backend="${escapeHtml(item.backendId ?? '')}">
            <span class="kit-list-row__main">
                <strong>${escapeHtml(getManagedRuntimeBackendLabel(item))}</strong>
                <em>${escapeHtml(formatManagedRuntimeHostName(item.hostName))} · ${escapeHtml(formatManagedRuntimeMode(item.executionMode))}</em>
            </span>
            <span class="kit-state-pill ${available ? 'kit-state-pill--ok' : 'kit-state-pill--muted'}">${escapeHtml(formatManagedRuntimeAvailability(availability))}</span>
        </button>`;
    }).join('');
}

function renderManagedRuntimeDetail(backend, validation) {
    if (!backend) {
        return `<section class="kit-panel kit-panel--detail">${emptyState('runtime', t('managedruntime.select_backend_hint'))}</section>`;
    }

    return `<section class="kit-panel kit-panel--detail" data-managedruntime-detail-panel>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('status', getManagedRuntimeBackendLabel(backend))}</div>
                <div class="kit-panel__desc">${escapeHtml(getManagedRuntimeBackendDescription(backend))}</div>
            </div>
            <span class="kit-state-pill">${escapeHtml(formatManagedRuntimeHostName(backend.hostName))}</span>
        </div>
        <div class="kit-detail-summary kit-detail-summary--managedruntime">
            <div><span>${t('managedruntime.target')}</span><strong>${escapeHtml(backend.targetName ?? '--')}</strong></div>
            <div><span>${t('managedruntime.mode')}</span><strong>${escapeHtml(formatManagedRuntimeMode(backend.executionMode))}</strong></div>
            <div><span>${t('managedruntime.availability')}</span><strong>${escapeHtml(formatManagedRuntimeAvailability(backend.availability))}</strong></div>
            <div><span>${t('managedruntime.capabilities')}</span><strong>${formatManagedRuntimeCapabilities(backend.capabilities)}</strong></div>
        </div>
        <div class="managedruntime-actions">${renderManagedRuntimeActionButtons(backend)}</div>
        ${renderManagedRuntimeSettingsPanel(backend)}
        ${renderManagedRuntimeFeedback(validation)}
    </section>`;
}

function renderManagedRuntimeActionButtons(backend) {
    const actions = Array.isArray(backend?.actions) ? backend.actions : [];
    if (!actions.length) {
        return emptyState('runtime', t('managedruntime.no_actions'));
    }

    return actions.map(action => {
        const supported = action.supported !== false;
        const caution = action.destructive ? ' managedruntime-action--danger' : '';
        const label = getManagedRuntimeActionLabel(action);
        const description = getManagedRuntimeActionDescription(action);
        return `<button class="managedruntime-action${caution}" type="button" ${supported ? '' : 'disabled'} data-managedruntime-action="${escapeHtml(action.actionId ?? '')}" title="${escapeHtml(description)}">
            <span>
                <strong>${escapeHtml(label)}</strong>
                <em>${escapeHtml(action.requiresConfirmation ? t('managedruntime.requires_confirmation') : t('managedruntime.direct_run'))}</em>
            </span>
            <small>${supported ? t('managedruntime.run') : t('managedruntime.unavailable')}</small>
        </button>`;
    }).join('');
}

function renderManagedRuntimeSettingsPanel(backend) {
    const settings = normalizeManagedRuntimeSettings(backend?.settings);
    const isLeanClr = String(backend?.backendId ?? '').toLowerCase() === 'leanclr';
    if (!settings && !isLeanClr) return '';

    if (!settings || settings.supported === false) {
        const message = settings?.message || t('managedruntime.settings_unavailable');
        return `<div class="managedruntime-settings managedruntime-settings--empty">
            <div class="kit-note kit-note--compact">${escapeHtml(message)}</div>
        </div>`;
    }

    return `<div class="managedruntime-settings" data-managedruntime-settings-panel>
        <div class="managedruntime-settings__head">
            <div>
                <strong>${t('managedruntime.leanclr_settings')}</strong>
                <span>${t('managedruntime.leanclr_settings_desc')}</span>
            </div>
            <div class="managedruntime-settings__actions">
                <button class="btn btn-secondary btn-sm" type="button" data-managedruntime-open-native-settings>${t('managedruntime.open_native_settings')}</button>
                <button class="btn btn-primary btn-sm" type="button" data-managedruntime-settings-save>${t('managedruntime.save_settings')}</button>
            </div>
        </div>
        <div class="kit-setting-grid managedruntime-setting-grid">
            ${renderKitToggle(t('managedruntime.setting.enable'), !!settings.enable, 'data-managedruntime-setting="enable"')}
            ${renderKitToggle(t('managedruntime.setting.layout_validation'), !!settings.layoutValidation, 'data-managedruntime-setting="layoutValidation"')}
            ${renderKitToggle(t('managedruntime.setting.enable_pgo_profile'), !!settings.enablePgoProfile, 'data-managedruntime-setting="enablePgoProfile"')}
            ${renderKitToggle(t('managedruntime.setting.enable_gc_debug'), !!settings.enableGCDebug, 'data-managedruntime-setting="enableGCDebug"')}
        </div>
        <div class="managedruntime-settings-fields">
            ${renderManagedRuntimeTextarea(t('managedruntime.setting.rule_files'), 'ruleFilesText', managedRuntimeArrayText(settings.ruleFiles), 'ProjectSettings/YokiFrame/ManagedRuntime/LeanCLR/aot.xml')}
            ${renderManagedRuntimeTextarea(t('managedruntime.setting.pgo_rule_files'), 'pgoRuleFilesText', managedRuntimeArrayText(settings.pgoRuleFiles), t('managedruntime.placeholder.one_per_line'))}
            ${renderManagedRuntimeTextarea(t('managedruntime.setting.lazy_loaded_assemblies'), 'lazyLoadedAssemblyNamesText', managedRuntimeArrayText(settings.lazyLoadedAssemblyNames), 'Game.Logic')}
            ${renderManagedRuntimeTextarea(t('managedruntime.setting.hot_update_assemblies'), 'hotUpdateAssemblyNamesText', managedRuntimeArrayText(settings.hotUpdateAssemblyNames), 'Game.HotUpdate')}
            <label class="kit-number-field managedruntime-settings-field">
                <span>${t('managedruntime.setting.gc_mode')}</span>
                <select class="cmd-select" data-managedruntime-setting="gcMode">
                    <option value="MarkSweep"${settings.gcMode === 'MarkSweep' ? ' selected' : ''}>MarkSweep</option>
                    <option value="Zero"${settings.gcMode === 'Zero' ? ' selected' : ''}>Zero</option>
                </select>
            </label>
        </div>
        <div class="managedruntime-settings__meta">
            <span>${t('managedruntime.settings_source')}</span>
            <code>${escapeHtml(settings.source || settings.settingsPath || '--')}</code>
            ${settings.installRootDir ? `<code>${escapeHtml(settings.installRootDir)}</code>` : ''}
        </div>
    </div>`;
}

function renderManagedRuntimeTextarea(label, key, value, placeholder) {
    return `<label class="managedruntime-settings-field">
        <span>${escapeHtml(label)}</span>
        <textarea class="cmd-input managedruntime-settings-textarea" data-managedruntime-setting="${escapeHtml(key)}" spellcheck="false" placeholder="${escapeHtml(placeholder ?? '')}">${escapeHtml(value ?? '')}</textarea>
    </label>`;
}

function renderManagedRuntimeFeedback(validation) {
    const resultHtml = renderManagedRuntimeResult(managedRuntimeState.lastResult);
    const diagnosticsHtml = renderManagedRuntimeDiagnostics(validation);
    if (!resultHtml && !diagnosticsHtml) return '';

    return `<div class="managedruntime-feedback">
        ${resultHtml}
        ${diagnosticsHtml}
    </div>`;
}

function renderManagedRuntimeResult(result) {
    if (!result) return '';
    const ok = result.success === true;
    return `<div class="managedruntime-result ${ok ? 'managedruntime-result--ok' : 'managedruntime-result--error'}">
        <strong>${escapeHtml(getManagedRuntimeActionLabel(result))}</strong>
        <span>${escapeHtml(formatManagedRuntimeResultMessage(result))}</span>
        ${result.data ? `<pre>${escapeHtml(JSON.stringify(result.data, null, 2))}</pre>` : ''}
    </div>`;
}

function renderManagedRuntimeDiagnostics(validation) {
    const diagnostics = (Array.isArray(validation?.diagnostics) ? validation.diagnostics : [])
        .filter(item => {
            const severity = String(item?.severity ?? '').toLowerCase();
            return severity === 'warning' || severity === 'error';
        });
    if (!diagnostics.length) return '';
    return `<div class="managedruntime-diagnostics">${diagnostics.map(item => `<div>
        <strong>${escapeHtml(formatManagedRuntimeDiagnosticSeverity(item.severity))}</strong>
        <span>${escapeHtml(formatManagedRuntimeDiagnosticMessage(item))}</span>
    </div>`).join('')}</div>`;
}

function bindManagedRuntimeActions() {
    $pageBody.querySelectorAll('[data-managedruntime-backend]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => selectManagedRuntimeBackend(button.dataset.managedruntimeBackend));
    });

    $pageBody.querySelectorAll('[data-managedruntime-action]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => runManagedRuntimeAction(button.dataset.managedruntimeAction));
    });

    bindKitButtonClick('[data-managedruntime-settings-save]', () => saveManagedRuntimeSettings());
    bindKitButtonClick('[data-managedruntime-open-native-settings]', () => runManagedRuntimeAction('open_backend_settings'));
}

function selectManagedRuntimeBackend(backendId) {
    if (!backendId) return;
    managedRuntimeState.selectedBackendId = backendId;
    loadManagedRuntimeWorkbench();
}

async function runManagedRuntimeAction(actionId) {
    const backend = getSelectedManagedRuntimeBackend();
    const action = (backend?.actions ?? []).find(item => item.actionId === actionId);
    if (!backend || !action || action.supported === false) return;

    const actionPayload = {};
    if (action.requiresConfirmation || action.destructive) {
        const label = getManagedRuntimeActionLabel(action);
        const description = getManagedRuntimeActionDescription(action);
        if (!confirm(`${label}\n\n${description}`)) return;
        actionPayload.confirmed = true;
    }
    if (actionId === 'build_player') {
        const outputPath = prompt(t('managedruntime.build_output_path'));
        if (!outputPath) return;
        actionPayload.outputPath = outputPath;
    }

    try {
        managedRuntimeState.lastResult = await sendKitCommandData('ManagedRuntimeKit', 'run_action', {
            backendId: backend.backendId,
            actionId,
            payload: actionPayload,
        });
    } catch (e) {
        managedRuntimeState.lastResult = {
            success: false,
            backendId: backend.backendId,
            actionId,
            message: String(e?.message ?? e),
        };
    }
    await loadManagedRuntimeWorkbench();
}

async function saveManagedRuntimeSettings() {
    const backend = getSelectedManagedRuntimeBackend();
    if (!backend) return;

    const panel = $pageBody.querySelector('[data-managedruntime-settings-panel]');
    if (!panel) return;

    const settings = {};
    panel.querySelectorAll('[data-managedruntime-setting]').forEach(input => {
        const key = input.dataset.managedruntimeSetting;
        if (!key) return;
        if (input.type === 'checkbox') {
            settings[key] = input.checked;
        } else {
            settings[key] = input.value ?? '';
        }
    });

    try {
        managedRuntimeState.lastResult = await sendKitCommandData('ManagedRuntimeKit', 'save_backend_settings', {
            backendId: backend.backendId,
            settings,
        });
    } catch (e) {
        managedRuntimeState.lastResult = {
            success: false,
            backendId: backend.backendId,
            actionId: 'save_backend_settings',
            message: String(e?.message ?? e),
        };
    }
    await loadManagedRuntimeWorkbench();
}

function getSelectedManagedRuntimeBackend() {
    return (managedRuntimeState.snapshot?.backends ?? [])
        .find(item => item.backendId === managedRuntimeState.selectedBackendId) ?? null;
}

function getManagedRuntimeActionHint(actionId) {
    return MANAGED_RUNTIME_ACTION_HINTS.find(item => item.actionId === actionId) ?? null;
}

function getManagedRuntimeActionLabel(action) {
    const actionId = action?.actionId ?? '';
    if (actionId === 'get_backend_settings') return t('managedruntime.action.get_backend_settings');
    if (actionId === 'save_backend_settings') return t('managedruntime.action.save_backend_settings');

    const hint = getManagedRuntimeActionHint(actionId);
    if (hint?.labelKey) {
        const translated = t(hint.labelKey);
        if (translated !== hint.labelKey) return translated;
    }
    return action?.displayName || hint?.fallback || actionId || '--';
}

function getManagedRuntimeActionDescription(action) {
    const hint = getManagedRuntimeActionHint(action?.actionId);
    if (hint?.descriptionKey) {
        const translated = t(hint.descriptionKey);
        if (translated !== hint.descriptionKey) return translated;
    }
    return action?.description || '';
}

function getManagedRuntimeBackendLabel(backend) {
    const backendId = String(backend?.backendId ?? '');
    if (backendId.toLowerCase() === 'default') return t('managedruntime.backend.default');
    if (backendId.toLowerCase() === 'leanclr') return t('managedruntime.backend.leanclr_unity');
    return backend?.displayName ?? backendId ?? '--';
}

function getManagedRuntimeBackendDescription(backend) {
    const backendId = String(backend?.backendId ?? '');
    if (backendId.toLowerCase() === 'default') return t('managedruntime.backend.default_desc');
    return backend?.description ?? '';
}

function formatManagedRuntimeHostName(value) {
    const raw = String(value ?? '');
    const key = MANAGED_RUNTIME_HOST_KEYS[raw];
    return key ? t(key) : (raw || '--');
}

function formatManagedRuntimeMode(value) {
    const raw = String(value ?? '');
    const key = MANAGED_RUNTIME_MODE_KEYS[raw];
    return key ? t(key) : (raw || '--');
}

function formatManagedRuntimeAvailability(value) {
    const key = MANAGED_RUNTIME_AVAILABILITY_KEYS[String(value ?? '').replace(/\s+/g, '').toLowerCase()];
    return key ? t(key) : (value || '--');
}

function formatManagedRuntimeDiagnosticSeverity(value) {
    const key = String(value ?? '').toLowerCase();
    if (key === 'info') return t('common.info');
    if (key === 'warning') return t('common.warning');
    if (key === 'error') return t('common.error');
    return value || '--';
}

function formatManagedRuntimeDiagnosticMessage(item) {
    const code = String(item?.code ?? '');
    if (code.length > 0) {
        const translated = t(code);
        if (translated !== code) return translated;
    }
    return item?.message ?? '--';
}

function formatManagedRuntimeResultMessage(result) {
    const actionId = String(result?.actionId ?? '');
    if (actionId.length > 0) {
        const key = `managedruntime.message.${actionId}`;
        const translated = t(key);
        if (translated !== key) return translated;
    }
    return result?.message ?? '--';
}

function formatManagedRuntimeCapabilities(value) {
    const items = String(value ?? '')
        .split(',')
        .map(item => item.trim())
        .filter(Boolean);
    if (!items.length) return escapeHtml(value || '--');
    return items.map(item => {
        const key = MANAGED_RUNTIME_CAPABILITY_KEYS[item];
        return escapeHtml(key ? t(key) : item);
    }).join('<br>');
}

function managedRuntimeArrayText(values) {
    return (Array.isArray(values) ? values : [])
        .filter(value => value != null && String(value).trim().length > 0)
        .join('\n');
}
