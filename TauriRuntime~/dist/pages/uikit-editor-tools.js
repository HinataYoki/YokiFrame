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

async function refreshUIKitRootSettings({ silent = false } = {}) {
    if (!uikitState.editorToolsAvailable) return;
    try {
        const result = await sendKitCommandData('UIKit', 'get_ui_root_settings');
        uikitState.rootSettingsForm = normalizeUIKitRootSettings(result?.settings ?? result);
        uikitState.rootSettingsAvailable = true;
        if (!silent) {
            uikitState.rootSettingsStatusKind = 'success';
            uikitState.rootSettingsStatusMessage = 'UIKit 配置已加载。';
        }
    } catch (e) {
        uikitState.rootSettingsAvailable = false;
        if (!silent) {
            uikitState.rootSettingsStatusKind = 'error';
            uikitState.rootSettingsStatusMessage = String(e?.message ?? e);
        }
    }
}

function syncUIKitRootSettingsFromStats(stats) {
    if (!stats?.rootSettings) return;
    if (!uikitState.rootSettingsAvailable) {
        uikitState.rootSettingsForm = normalizeUIKitRootSettings(stats.rootSettings);
    }
}

function normalizeUIKitRootSettings(data) {
    const source = data?.settings ?? data?.rootSettings ?? data ?? {};
    const resolution = parseUIKitReferenceResolution(source.ReferenceResolution ?? source.referenceResolution);
    return {
        RenderMode: String(source.RenderMode ?? source.renderMode ?? 'ScreenSpaceOverlay'),
        SortOrder: toUIKitNumber(source.SortOrder ?? source.sortOrder, 0),
        TargetDisplay: toUIKitNumber(source.TargetDisplay ?? source.targetDisplay, 0),
        PixelPerfect: toUIKitBool(source.PixelPerfect ?? source.pixelPerfect, false),
        ScaleMode: String(source.ScaleMode ?? source.scaleMode ?? 'ScaleWithScreenSize'),
        ReferenceResolutionX: toUIKitNumber(source.ReferenceResolutionX ?? source.referenceResolutionX ?? resolution.x, 3840),
        ReferenceResolutionY: toUIKitNumber(source.ReferenceResolutionY ?? source.referenceResolutionY ?? resolution.y, 2160),
        ScreenMatchMode: String(source.ScreenMatchMode ?? source.screenMatchMode ?? 'MatchWidthOrHeight'),
        MatchWidthOrHeight: toUIKitNumber(source.MatchWidthOrHeight ?? source.matchWidthOrHeight, 0),
        ReferencePixelsPerUnit: toUIKitNumber(source.ReferencePixelsPerUnit ?? source.referencePixelsPerUnit, 100),
        PhysicalUnit: String(source.PhysicalUnit ?? source.physicalUnit ?? 'Points'),
        FallbackScreenDPI: toUIKitNumber(source.FallbackScreenDPI ?? source.fallbackScreenDPI, 96),
        DefaultSpriteDPI: toUIKitNumber(source.DefaultSpriteDPI ?? source.defaultSpriteDPI, 96),
        DynamicPixelsPerUnit: toUIKitNumber(source.DynamicPixelsPerUnit ?? source.dynamicPixelsPerUnit, 1),
        IgnoreReversedGraphics: toUIKitBool(source.IgnoreReversedGraphics ?? source.ignoreReversedGraphics, false),
        BlockingObjects: String(source.BlockingObjects ?? source.blockingObjects ?? 'None'),
        BlockingMask: toUIKitNumber(source.BlockingMask ?? source.blockingMask, -1),
    };
}

function parseUIKitReferenceResolution(value) {
    if (!value) return {};
    const match = String(value).match(/([\d.]+)\s*x\s*([\d.]+)/i);
    return match ? { x: Number(match[1]), y: Number(match[2]) } : {};
}

function toUIKitNumber(value, fallback) {
    const number = Number(value);
    return Number.isFinite(number) ? number : fallback;
}

function toUIKitBool(value, fallback) {
    if (typeof value === 'boolean') return value;
    if (typeof value === 'string') return value.toLowerCase() === 'true';
    return fallback;
}

function isUIKitEditorOptionValid(value, options) {
    if (!Array.isArray(options) || !options.length) return true;
    return options.includes(String(value ?? ''));
}

function renderUIKitRootSettingsSection(rootSettings) {
    if (!uikitState.editorToolsAvailable) {
        return renderUIKitRootSettingsReadOnly(rootSettings);
    }

    const form = normalizeUIKitRootSettings(uikitState.rootSettingsForm);
    const activeResolution = `${form.ReferenceResolutionX}x${form.ReferenceResolutionY}`;
    const statusClass = uikitState.rootSettingsStatusKind === 'error'
        ? ' uikit-root-settings__status--error'
        : (uikitState.rootSettingsStatusKind === 'success' ? ' uikit-root-settings__status--success' : '');
    return `<section class="uikit-root-settings uikit-root-settings--primary" data-uikit-root-settings>
        <div class="uikit-root-settings__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('ui', 'UIRoot 设置')}</div>
                <div class="kit-panel__desc">保存到 Assets/Settings/Resources/UIKitSettings.asset；运行时由 Resources.Load("UIKitSettings") 读取。</div>
            </div>
            <div class="uikit-root-settings__actions">
                <button class="btn btn-primary btn-sm" type="button" data-uikit-save-root-settings>保存</button>
                <button class="btn btn-secondary btn-sm" type="button" data-uikit-reset-root-settings>重置默认</button>
            </div>
        </div>
        <div class="uikit-root-settings__metrics">
            ${renderUIKitRootMetric('渲染模式', form.RenderMode, 'Canvas 当前渲染策略')}
            ${renderUIKitRootMetric('缩放模式', form.ScaleMode, 'CanvasScaler 当前缩放策略')}
            ${renderUIKitRootMetric('排序顺序', form.SortOrder, '默认 Sorting Order')}
            ${renderUIKitRootMetric('拦截类型', form.BlockingObjects, 'Raycaster 阻挡对象类型')}
        </div>
        <div class="uikit-root-settings__body">
            <div class="uikit-root-settings__group uikit-root-settings__group--canvas">
                <div class="uikit-root-settings__group-head">
                    <strong>Canvas 配置</strong>
                    <em>管理 UIRoot Canvas 的渲染模式、排序与显示器参数。</em>
                </div>
                <div class="uikit-root-settings__grid uikit-root-settings__grid--canvas">
                    ${renderUIKitRootSelectField('RenderMode', '渲染模式', form.RenderMode, ['ScreenSpaceOverlay', 'ScreenSpaceCamera', 'WorldSpace'])}
                    ${renderUIKitRootNumberField('SortOrder', '排序顺序', form.SortOrder, 1)}
                    ${renderUIKitRootNumberField('TargetDisplay', '目标显示器', form.TargetDisplay, 1)}
                    ${renderUIKitRootToggleField('PixelPerfect', '像素完美', form.PixelPerfect)}
                </div>
            </div>
            <div class="uikit-root-settings__group uikit-root-settings__group--scale">
                <div class="uikit-root-settings__group-head">
                    <strong>CanvasScaler 配置</strong>
                    <em>统一设置参考分辨率、屏幕匹配、DPI 和像素单位。</em>
                </div>
                <div class="uikit-root-settings__presets">
                    ${renderUIKitRootResolutionPreset(1920, 1080, activeResolution)}
                    ${renderUIKitRootResolutionPreset(2560, 1440, activeResolution)}
                    ${renderUIKitRootResolutionPreset(3840, 2160, activeResolution)}
                </div>
                <div class="uikit-root-settings__grid uikit-root-settings__grid--scale">
                    ${renderUIKitRootSelectField('ScaleMode', '缩放模式', form.ScaleMode, ['ConstantPixelSize', 'ScaleWithScreenSize', 'ConstantPhysicalSize'])}
                    ${renderUIKitRootNumberField('ReferenceResolutionX', '参考宽度', form.ReferenceResolutionX, 1)}
                    ${renderUIKitRootNumberField('ReferenceResolutionY', '参考高度', form.ReferenceResolutionY, 1)}
                    ${renderUIKitRootSelectField('ScreenMatchMode', '屏幕匹配', form.ScreenMatchMode, ['MatchWidthOrHeight', 'Expand', 'Shrink'])}
                    ${renderUIKitRootRangeField('MatchWidthOrHeight', '宽高匹配权重', form.MatchWidthOrHeight, 0, 1, 0.01)}
                    ${renderUIKitRootNumberField('ReferencePixelsPerUnit', '参考像素/单位', form.ReferencePixelsPerUnit, 1)}
                    ${renderUIKitRootSelectField('PhysicalUnit', '物理单位', form.PhysicalUnit, ['Centimeters', 'Millimeters', 'Inches', 'Points', 'Picas'])}
                    ${renderUIKitRootNumberField('FallbackScreenDPI', '回退屏幕 DPI', form.FallbackScreenDPI, 1)}
                    ${renderUIKitRootNumberField('DefaultSpriteDPI', '默认精灵 DPI', form.DefaultSpriteDPI, 1)}
                    ${renderUIKitRootNumberField('DynamicPixelsPerUnit', '动态像素/单位', form.DynamicPixelsPerUnit, 0.01)}
                </div>
            </div>
            <div class="uikit-root-settings__group uikit-root-settings__group--raycaster">
                <div class="uikit-root-settings__group-head">
                    <strong>GraphicRaycaster 配置</strong>
                    <em>配置 UI 射线拦截路径与反向图形忽略策略。</em>
                </div>
                <div class="uikit-root-settings__grid uikit-root-settings__grid--raycaster">
                    ${renderUIKitRootToggleField('IgnoreReversedGraphics', '忽略反向图形', form.IgnoreReversedGraphics)}
                    ${renderUIKitRootSelectField('BlockingObjects', '阻挡对象', form.BlockingObjects, ['None', 'TwoD', 'ThreeD', 'All'])}
                    ${renderUIKitRootNumberField('BlockingMask', '阻挡层级', form.BlockingMask, 1)}
                </div>
            </div>
        </div>
        <div class="uikit-root-settings__status${statusClass}">${escapeHtml(uikitState.rootSettingsStatusMessage || '修改后点击保存。运行中的 UIRoot 不会被强制重建。')}</div>
    </section>`;
}

function renderUIKitRootSettingsReadOnly(rootSettings) {
    const normalized = normalizeUIKitRootSettings(rootSettings);
    const pixelPerfect = normalized.PixelPerfect ? '是' : '否';
    const ignoreReversed = normalized.IgnoreReversedGraphics ? '是' : '否';
    return `<section class="uikit-root-settings uikit-root-settings--readonly" data-uikit-root-settings>
        <div class="uikit-root-settings__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('ui', 'UIRoot 设置')}</div>
                <div class="kit-panel__desc">当前宿主只提供只读配置快照。</div>
            </div>
        </div>
        <div class="uikit-root-settings__body">
            <div class="kit-detail-summary kit-detail-summary--save-auto">
                <div><span>渲染模式</span><strong>${escapeHtml(normalized.RenderMode || '--')}</strong></div>
                <div><span>排序顺序</span><strong>${escapeHtml(normalized.SortOrder ?? '--')}</strong></div>
                <div><span>目标显示器</span><strong>${escapeHtml(normalized.TargetDisplay ?? '--')}</strong></div>
                <div><span>像素完美</span><strong>${escapeHtml(pixelPerfect)}</strong></div>
                <div><span>缩放模式</span><strong>${escapeHtml(normalized.ScaleMode || '--')}</strong></div>
                <div><span>参考分辨率</span><strong>${escapeHtml(`${normalized.ReferenceResolutionX} x ${normalized.ReferenceResolutionY}`)}</strong></div>
                <div><span>屏幕匹配</span><strong>${escapeHtml(normalized.ScreenMatchMode || '--')}</strong></div>
                <div><span>宽高匹配权重</span><strong>${escapeHtml(normalized.MatchWidthOrHeight ?? '--')}</strong></div>
                <div><span>参考像素/单位</span><strong>${escapeHtml(normalized.ReferencePixelsPerUnit ?? '--')}</strong></div>
                <div><span>物理单位</span><strong>${escapeHtml(normalized.PhysicalUnit || '--')}</strong></div>
                <div><span>回退屏幕 DPI</span><strong>${escapeHtml(normalized.FallbackScreenDPI ?? '--')}</strong></div>
                <div><span>默认精灵 DPI</span><strong>${escapeHtml(normalized.DefaultSpriteDPI ?? '--')}</strong></div>
                <div><span>动态像素/单位</span><strong>${escapeHtml(normalized.DynamicPixelsPerUnit ?? '--')}</strong></div>
                <div><span>忽略反向图形</span><strong>${escapeHtml(ignoreReversed)}</strong></div>
                <div><span>阻挡对象</span><strong>${escapeHtml(normalized.BlockingObjects || '--')}</strong></div>
                <div><span>阻挡层级</span><strong>${escapeHtml(normalized.BlockingMask ?? '--')}</strong></div>
            </div>
        </div>
    </section>`;
}

function renderUIKitRootMetric(label, value, hint) {
    return `<div class="uikit-root-settings__metric">
        <span>${escapeHtml(label)}</span>
        <strong>${escapeHtml(value ?? '--')}</strong>
        <em>${escapeHtml(hint)}</em>
    </div>`;
}

function renderUIKitRootSelectField(field, label, value, options) {
    const items = options.map(option => `<option value="${escapeHtml(option)}"${option === value ? ' selected' : ''}>${escapeHtml(option)}</option>`).join('');
    return `<label class="uikit-editor-field">
        <span>${escapeHtml(label)}</span>
        <select class="cmd-select" data-uikit-root-setting="${escapeHtml(field)}">${items}</select>
    </label>`;
}

function renderUIKitRootNumberField(field, label, value, step = 1, min = null, max = null) {
    return `<label class="uikit-editor-field">
        <span>${escapeHtml(label)}</span>
        <input class="cmd-input" type="number" step="${escapeHtml(step)}"${min === null ? '' : ` min="${escapeHtml(min)}"`}${max === null ? '' : ` max="${escapeHtml(max)}"`} data-uikit-root-setting="${escapeHtml(field)}" value="${escapeHtml(value)}">
    </label>`;
}

function renderUIKitRootRangeField(field, label, value, min, max, step) {
    return `<label class="uikit-editor-field uikit-root-settings__field--range">
        <span>${escapeHtml(label)}<em>0 = 宽度，1 = 高度</em></span>
        <div class="uikit-root-settings__range-control">
            <input type="range" min="${escapeHtml(min)}" max="${escapeHtml(max)}" step="${escapeHtml(step)}" data-uikit-root-setting="${escapeHtml(field)}" value="${escapeHtml(value)}">
            <input class="cmd-input" type="number" min="${escapeHtml(min)}" max="${escapeHtml(max)}" step="${escapeHtml(step)}" data-uikit-root-setting="${escapeHtml(field)}" value="${escapeHtml(value)}">
        </div>
    </label>`;
}

function renderUIKitRootToggleField(field, label, checked) {
    return renderKitToggle(label, checked, `data-uikit-root-setting="${escapeHtml(field)}"`);
}

function renderUIKitRootResolutionPreset(width, height, activeResolution) {
    const resolution = `${width}x${height}`;
    return `<button class="uikit-root-settings__preset${resolution === activeResolution ? ' active' : ''}" type="button" data-uikit-resolution-preset="${escapeHtml(resolution)}">${escapeHtml(width)} x ${escapeHtml(height)}</button>`;
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

    return `<section class="kit-panel uikit-editor-tools" data-uikit-editor-tools>
        <div class="kit-panel__head">
            <div>
                <div class="kit-panel__title">${renderKitTitle('ui', 'UI 面板创建')}</div>
                <div class="kit-panel__desc">创建 UIPrefab、生成 Panel 代码和维护 Bind 组件</div>
            </div>
            <span class="kit-panel__count">${escapeHtml(selectedCount)} 个选中</span>
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
                ${renderKitToggle('覆盖 Prefab', form.overwrite, 'data-uikit-editor-field="overwrite"', '同名 Prefab 存在时允许替换')}
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

function buildUIKitRootSettingsPayload() {
    const form = normalizeUIKitRootSettings(uikitState.rootSettingsForm);
    return {
        RenderMode: String(form.RenderMode),
        SortOrder: Number(form.SortOrder),
        TargetDisplay: Number(form.TargetDisplay),
        PixelPerfect: !!form.PixelPerfect,
        ScaleMode: String(form.ScaleMode),
        ReferenceResolutionX: Number(form.ReferenceResolutionX),
        ReferenceResolutionY: Number(form.ReferenceResolutionY),
        ScreenMatchMode: String(form.ScreenMatchMode),
        MatchWidthOrHeight: Number(form.MatchWidthOrHeight),
        ReferencePixelsPerUnit: Number(form.ReferencePixelsPerUnit),
        PhysicalUnit: String(form.PhysicalUnit),
        FallbackScreenDPI: Number(form.FallbackScreenDPI),
        DefaultSpriteDPI: Number(form.DefaultSpriteDPI),
        DynamicPixelsPerUnit: Number(form.DynamicPixelsPerUnit),
        IgnoreReversedGraphics: !!form.IgnoreReversedGraphics,
        BlockingObjects: String(form.BlockingObjects),
        BlockingMask: Number(form.BlockingMask),
    };
}

async function runUIKitRootSettingsCommand(action) {
    uikitState.rootSettingsStatusKind = 'info';
    uikitState.rootSettingsStatusMessage = action === 'reset' ? '正在重置 UIKit 配置...' : '正在保存 UIKit 配置...';
    renderUIKitWorkbenchFromState();

    try {
        const result = action === 'reset'
            ? await sendKitCommandData('UIKit', 'reset_ui_root_settings')
            : await sendKitCommandData('UIKit', 'save_ui_root_settings', buildUIKitRootSettingsPayload());
        uikitState.rootSettingsForm = normalizeUIKitRootSettings(result?.settings ?? result);
        uikitState.rootSettingsAvailable = true;
        uikitState.rootSettingsStatusKind = 'success';
        uikitState.rootSettingsStatusMessage = result?.message || 'UIKit 配置已更新。';
        await loadUIKitWorkbench();
    } catch (e) {
        uikitState.rootSettingsStatusKind = 'error';
        uikitState.rootSettingsStatusMessage = String(e?.message ?? e);
        renderUIKitWorkbenchFromState();
    }
}

async function runUIKitEditorCommand(action) {
    uikitState.editorStatusKind = 'info';
    uikitState.editorStatusMessage = '正在执行 UIKit 编辑器命令...';
    renderUIKitWorkbenchFromState();

    try {
        let result;
        if (action === 'create_panel_prefab') {
            result = await sendKitCommandData('UIKit', 'create_panel_prefab', buildUIKitEditorPayload());
            if (result?.prefabPath) {
                uikitState.editorForm.prefabPath = result.prefabPath;
                saveUIKitEditorForm();
            }
        } else if (action === 'generate_code_for_selection') {
            result = await sendKitCommandData('UIKit', 'generate_code_for_selection', buildUIKitEditorPayload());
            if (result?.prefabPath) {
                uikitState.editorForm.prefabPath = result.prefabPath;
                saveUIKitEditorForm();
            }
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
            saveUIKitEditorForm();
            if (eventName === 'change') void saveUIKitEditorSettings();
        });
        if (eventName !== 'change') {
            input.addEventListener('change', () => void saveUIKitEditorSettings());
        }
    });

    bindKitButtonClick('[data-uikit-create-panel]', () => void runUIKitEditorCommand('create_panel_prefab'));
    bindKitButtonClick('[data-uikit-generate-code]', () => void runUIKitEditorCommand('generate_code_for_selection'));
    bindKitButtonClick('[data-uikit-add-bind]', () => void runUIKitEditorCommand('add_bind_to_selection'));
    bindKitButtonClick('[data-uikit-remove-bind]', () => void runUIKitEditorCommand('remove_bind_from_selection'));
    bindUIKitRootSettings();
}

function bindUIKitRootSettings() {
    $pageBody.querySelectorAll('[data-uikit-root-setting]').forEach(input => {
        if (input.dataset.bound === '1') return;
        input.dataset.bound = '1';
        const field = input.dataset.uikitRootSetting;
        const eventName = input.type === 'checkbox' || input.tagName === 'SELECT' ? 'change' : 'input';
        input.addEventListener(eventName, () => {
            uikitState.rootSettingsForm[field] = input.type === 'checkbox'
                ? input.checked
                : (input.type === 'number' || input.type === 'range' ? Number(input.value) : input.value);
            syncUIKitRootSettingInputs(field, uikitState.rootSettingsForm[field]);
        });
    });

    $pageBody.querySelectorAll('[data-uikit-resolution-preset]').forEach(button => {
        if (button.dataset.bound === '1') return;
        button.dataset.bound = '1';
        button.addEventListener('click', () => {
            const [width, height] = String(button.dataset.uikitResolutionPreset || '').split('x').map(Number);
            if (!Number.isFinite(width) || !Number.isFinite(height)) return;
            uikitState.rootSettingsForm.ReferenceResolutionX = width;
            uikitState.rootSettingsForm.ReferenceResolutionY = height;
            renderUIKitWorkbenchFromState();
        });
    });

    bindKitButtonClick('[data-uikit-save-root-settings]', () => void runUIKitRootSettingsCommand('save'));
    bindKitButtonClick('[data-uikit-reset-root-settings]', () => void runUIKitRootSettingsCommand('reset'));
}

function syncUIKitRootSettingInputs(field, value) {
    $pageBody.querySelectorAll(`[data-uikit-root-setting="${field}"]`).forEach(input => {
        if (input.type === 'checkbox') {
            input.checked = !!value;
        } else if (String(input.value) !== String(value)) {
            input.value = value;
        }
    });
}
