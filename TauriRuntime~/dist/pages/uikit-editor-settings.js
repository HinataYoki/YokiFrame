// pages/uikit-editor-settings.js
// UIKit 编辑器生成参数同步
function normalizeUIKitEditorToolState(data) {
    const source = data?.data ?? data ?? {};
    const defaults = source.defaults ?? {};
    const assemblyName = defaults.assemblyName ?? 'Assembly-CSharp';
    const codeTemplate = defaults.codeTemplate ?? 'Default';
    return {
        available: source.available !== false,
        selectedObjectCount: Number(source.selectedObjectCount ?? 0),
        activeAssetPath: source.activeAssetPath ?? '',
        canGenerateCode: !!source.canGenerateCode,
        defaults: {
            prefabFolder: defaults.prefabFolder ?? 'Assets/Resources/Art/UIPrefab',
            scriptFolder: defaults.scriptFolder ?? 'Assets/Scripts/UI',
            namespace: defaults.namespace ?? 'GameUI',
            assemblyName,
            codeTemplate,
        },
        assemblies: normalizeUIKitEditorOptions(source.assemblies, [assemblyName]),
        codeTemplates: normalizeUIKitEditorOptions(source.codeTemplates, [codeTemplate]),
    };
}

function normalizeUIKitEditorOptions(values, fallbackValues) {
    const normalized = [];
    const addValue = value => {
        const text = String(value ?? '').trim();
        if (!text || normalized.includes(text)) return;
        normalized.push(text);
    };
    if (Array.isArray(fallbackValues)) {
        for (const value of fallbackValues) addValue(value);
    } else {
        addValue(fallbackValues);
    }
    if (Array.isArray(values)) {
        for (const value of values) addValue(value);
    }
    return normalized;
}

function syncUIKitEditorFormDefaults(toolState) {
    const before = JSON.stringify(uikitState.editorForm);
    const defaults = toolState?.defaults ?? {};
    const assemblies = normalizeUIKitEditorOptions(toolState?.assemblies, [defaults.assemblyName ?? 'Assembly-CSharp']);
    const codeTemplates = normalizeUIKitEditorOptions(toolState?.codeTemplates, [defaults.codeTemplate ?? 'Default']);

    if (!uikitEditorFormLoadedFromStorage) {
        uikitState.editorForm.scriptNamespace = defaults.namespace ?? 'GameUI';
        uikitState.editorForm.prefabFolder = defaults.prefabFolder ?? 'Assets/Resources/Art/UIPrefab';
        uikitState.editorForm.scriptFolder = defaults.scriptFolder ?? 'Assets/Scripts/UI';
        uikitState.editorForm.assemblyName = defaults.assemblyName ?? 'Assembly-CSharp';
        uikitState.editorForm.codeTemplate = defaults.codeTemplate ?? 'Default';
    } else if (!isUIKitEditorOptionValid(uikitState.editorForm.assemblyName, assemblies)) {
        uikitState.editorForm.assemblyName = assemblies[0] ?? defaults.assemblyName ?? 'Assembly-CSharp';
    }

    if (uikitEditorFormLoadedFromStorage && !isUIKitEditorOptionValid(uikitState.editorForm.codeTemplate, codeTemplates)) {
        uikitState.editorForm.codeTemplate = codeTemplates[0] ?? defaults.codeTemplate ?? 'Default';
    }

    if (!String(uikitState.editorForm.scriptNamespace ?? '').trim()) {
        uikitState.editorForm.scriptNamespace = defaults.namespace ?? 'GameUI';
    }

    if (!String(uikitState.editorForm.prefabFolder ?? '').trim()) {
        uikitState.editorForm.prefabFolder = defaults.prefabFolder ?? 'Assets/Resources/Art/UIPrefab';
    }

    if (!String(uikitState.editorForm.scriptFolder ?? '').trim()) {
        uikitState.editorForm.scriptFolder = defaults.scriptFolder ?? 'Assets/Scripts/UI';
    }

    if (JSON.stringify(uikitState.editorForm) !== before) saveUIKitEditorForm();
}

async function saveUIKitEditorSettings({ silent = false } = {}) {
    if (!uikitState.editorToolsAvailable) return;

    const payload = buildUIKitEditorPayload();
    const signature = JSON.stringify([
        payload.ScriptNamespace,
        payload.PrefabFolder,
        payload.ScriptFolder,
        payload.AssemblyName,
        payload.CodeTemplate,
    ]);
    if (signature === uikitState.editorSettingsSyncSignature) return;

    try {
        await sendKitCommandData('UIKit', 'save_editor_tool_settings', payload);
        uikitState.editorSettingsSyncSignature = signature;
    } catch (e) {
        if (!silent) {
            uikitState.editorStatusKind = 'error';
            uikitState.editorStatusMessage = String(e?.message ?? e);
            renderUIKitWorkbenchFromState();
        }
    }
}
