// pages/uikit-editor-tools.js
// UIKit Unity 编辑器工具
async function refreshUIKitEditorToolState() {
    try {
        const state = await sendKitCommandData('UIKit', 'get_editor_tool_state');
        uikitState.editorToolState = normalizeUIKitEditorToolState(state);
    } catch (e) {
        uikitState.editorToolState = null;
    }
}

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
    const defaults = toolState?.defaults ?? {};
    const assemblies = normalizeUIKitEditorOptions(toolState?.assemblies, [defaults.assemblyName ?? 'Assembly-CSharp']);
    const codeTemplates = normalizeUIKitEditorOptions(toolState?.codeTemplates, [defaults.codeTemplate ?? 'Default']);

    if (!isUIKitEditorOptionValid(uikitState.editorForm.assemblyName, assemblies)) {
        uikitState.editorForm.assemblyName = assemblies[0] ?? defaults.assemblyName ?? 'Assembly-CSharp';
    }

    if (!isUIKitEditorOptionValid(uikitState.editorForm.codeTemplate, codeTemplates)) {
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
}

function isUIKitEditorOptionValid(value, options) {
    if (!Array.isArray(options) || !options.length) return true;
    return options.includes(String(value ?? ''));
}

function renderUIKitEditorToolsSection() {
    const form = uikitState.editorForm;
    const toolState = uikitState.editorToolState;
    const selectedCount = toolState ? toolState.selectedObjectCount : 0;
    const activeAssetPath = toolState?.activeAssetPath || '未选择 Prefab 资源';
    const canGenerate = toolState?.canGenerateCode ? '可生成代码' : '请选择 UIPrefab';
    const assemblyOptions = normalizeUIKitEditorOptions(toolState?.assemblies, [toolState?.defaults?.assemblyName ?? form.assemblyName ?? 'Assembly-CSharp']);
    const templateOptions = normalizeUIKitEditorOptions(toolState?.codeTemplates, [toolState?.defaults?.codeTemplate ?? form.codeTemplate ?? 'Default']);
    const statusClass = uikitState.editorStatusKind === 'error'
        ? ' uikit-editor-tools__status--error'
        : (uikitState.editorStatusKind === 'success' ? ' uikit-editor-tools__status--success' : '');

    return `<section class="kit-panel uikit-editor-tools">
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('ui', 'Unity 编辑器工具')}</div>
                <div class="kit-panel__desc">创建 UIPrefab、生成绑定代码，并对 Unity Selection 添加或移除 Bind。</div>
            </div>
            <span class="kit-panel__count">${escapeHtml(selectedCount)} 个选中对象</span>
        </div>
        <div class="uikit-editor-tools__body">
            <div class="uikit-editor-tools__summary">
                <span>${escapeHtml(canGenerate)}</span>
                <code>${escapeHtml(activeAssetPath)}</code>
            </div>
            <div class="uikit-editor-tools__grid">
                ${renderUIKitEditorField('panelName', 'Panel 名称', form.panelName, '例如 MainMenuPanel')}
                ${renderUIKitEditorField('scriptNamespace', '命名空间', form.scriptNamespace, '生成 Panel / Data 的 namespace')}
                ${renderUIKitEditorSelectField('assemblyName', '程序集', form.assemblyName, assemblyOptions, '用于编译后反射绑定 Prefab 组件')}
                ${renderUIKitEditorSelectField('codeTemplate', '代码模板', form.codeTemplate, templateOptions, '选择生成代码的结构')}
                ${renderUIKitEditorField('prefabFolder', 'Prefab 目录', form.prefabFolder, 'Assets/Resources/Art/UIPrefab')}
                ${renderUIKitEditorField('scriptFolder', '脚本目录', form.scriptFolder, 'Assets/Scripts/UI')}
                ${renderUIKitEditorField('prefabPath', '目标 Prefab', form.prefabPath, '可留空，生成代码时使用 Unity 当前选择')}
                <label class="uikit-editor-field uikit-editor-field--toggle">
                    <span>覆盖 Prefab<em>同名 Prefab 存在时允许替换</em></span>
                    <input type="checkbox" data-uikit-editor-field="overwrite"${form.overwrite ? ' checked' : ''}>
                </label>
            </div>
            <div class="uikit-editor-tools__actions">
                <button class="btn btn-primary btn-sm" type="button" data-uikit-create-panel>创建 UIPrefab</button>
                <button class="btn btn-secondary btn-sm" type="button" data-uikit-generate-code>为选中 Prefab 生成代码</button>
                <button class="btn btn-secondary btn-sm" type="button" data-uikit-add-bind>给选中对象添加 Bind</button>
                <button class="btn btn-secondary btn-sm" type="button" data-uikit-remove-bind>移除选中对象 Bind</button>
            </div>
            <div class="uikit-editor-tools__status${statusClass}">${escapeHtml(uikitState.editorStatusMessage || '等待操作。生成代码后 Unity 会在下一次编译完成时回填 Prefab 引用。')}</div>
        </div>
    </section>`;
}

function renderUIKitEditorField(field, label, value, hint) {
    return `<label class="uikit-editor-field">
        <span>${escapeHtml(label)}${hint ? `<em>${escapeHtml(hint)}</em>` : ''}</span>
        <input class="cmd-input" type="text" data-uikit-editor-field="${escapeHtml(field)}" value="${escapeHtml(value ?? '')}">
    </label>`;
}

function renderUIKitEditorSelectField(field, label, value, options, hint) {
    const normalizedOptions = normalizeUIKitEditorOptions(options, [value]);
    const selectedValue = String(value ?? '');
    const items = normalizedOptions.map(option => {
        const optionValue = String(option ?? '');
        return `<option value="${escapeHtml(optionValue)}"${optionValue === selectedValue ? ' selected' : ''}>${escapeHtml(optionValue)}</option>`;
    }).join('');
    return `<label class="uikit-editor-field">
        <span>${escapeHtml(label)}${hint ? `<em>${escapeHtml(hint)}</em>` : ''}</span>
        <select class="cmd-select" data-uikit-editor-field="${escapeHtml(field)}">${items}</select>
    </label>`;
}

function buildUIKitEditorPayload() {
    const form = uikitState.editorForm;
    return {
        PanelName: String(form.panelName ?? '').trim(),
        ScriptNamespace: String(form.scriptNamespace ?? '').trim(),
        PrefabFolder: String(form.prefabFolder ?? '').trim(),
        ScriptFolder: String(form.scriptFolder ?? '').trim(),
        PrefabPath: String(form.prefabPath ?? '').trim(),
        AssemblyName: String(form.assemblyName ?? '').trim(),
        CodeTemplate: String(form.codeTemplate ?? '').trim(),
        Overwrite: !!form.overwrite,
    };
}

async function runUIKitEditorCommand(action) {
    uikitState.editorStatusKind = 'info';
    uikitState.editorStatusMessage = '正在执行 UIKit 编辑器命令...';
    renderUIKitWorkbenchFromState();

    try {
        let result;
        if (action === 'create_panel_prefab') {
            result = await sendKitCommandData('UIKit', 'create_panel_prefab', buildUIKitEditorPayload());
            if (result?.prefabPath) uikitState.editorForm.prefabPath = result.prefabPath;
        } else if (action === 'generate_code_for_selection') {
            result = await sendKitCommandData('UIKit', 'generate_code_for_selection', buildUIKitEditorPayload());
            if (result?.prefabPath) uikitState.editorForm.prefabPath = result.prefabPath;
        } else if (action === 'add_bind_to_selection') {
            result = await sendKitCommandData('UIKit', 'add_bind_to_selection');
        } else if (action === 'remove_bind_from_selection') {
            result = await sendKitCommandData('UIKit', 'remove_bind_from_selection');
        } else {
            throw new Error(`Unsupported UIKit editor action: ${action}`);
        }

        uikitState.editorStatusKind = 'success';
        uikitState.editorStatusMessage = formatUIKitEditorResult(result);
        await loadUIKitWorkbench();
    } catch (e) {
        uikitState.editorStatusKind = 'error';
        uikitState.editorStatusMessage = String(e?.message ?? e);
        renderUIKitWorkbenchFromState();
    }
}

function formatUIKitEditorResult(result) {
    if (!result) return '命令已完成。';
    const parts = [result.message].filter(Boolean);
    if (result.prefabPath) parts.push(result.prefabPath);
    if (Number.isFinite(Number(result.changedCount))) parts.push(`Changed ${result.changedCount}`);
    if (Number.isFinite(Number(result.skippedCount)) && Number(result.skippedCount) > 0) parts.push(`Skipped ${result.skippedCount}`);
    if (result.requiresCompile) parts.push('等待 Unity 编译后回填序列化引用');
    return parts.join(' · ') || '命令已完成。';
}

function bindUIKitEditorTools() {
    $pageBody.querySelectorAll('[data-uikit-editor-field]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        const field = input.dataset.uikitEditorField;
        const eventName = input.type === 'checkbox' || input.tagName === 'SELECT' ? 'change' : 'input';
        input.addEventListener(eventName, () => {
            uikitState.editorForm[field] = input.type === 'checkbox' ? input.checked : input.value;
        });
    });

    bindKitButtonClick('[data-uikit-create-panel]', () => void runUIKitEditorCommand('create_panel_prefab'));
    bindKitButtonClick('[data-uikit-generate-code]', () => void runUIKitEditorCommand('generate_code_for_selection'));
    bindKitButtonClick('[data-uikit-add-bind]', () => void runUIKitEditorCommand('add_bind_to_selection'));
    bindKitButtonClick('[data-uikit-remove-bind]', () => void runUIKitEditorCommand('remove_bind_from_selection'));
}
