const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const test = require('node:test');

function findWorkspaceRoot() {
    let dir = __dirname;
    for (let i = 0; i < 12; i++) {
        const marker = path.join(dir, 'Assets', 'YokiFrame', 'Tools', 'UIKit', 'Runtime', 'YokiFrame.UIKit.asmdef');
        if (fs.existsSync(marker)) return dir;
        const parent = path.dirname(dir);
        if (parent === dir) break;
        dir = parent;
    }
    throw new Error('Unable to locate YokiFrame workspace root');
}

const workspaceRoot = findWorkspaceRoot();

function readWorkspaceFile(...segments) {
    return fs.readFileSync(path.join(workspaceRoot, ...segments), 'utf8');
}

function assertWorkspaceFileExists(...segments) {
    const filePath = path.join(workspaceRoot, ...segments);
    assert.ok(fs.existsSync(filePath), `${filePath} should exist`);
}

test('UIKit restores UIPanel custom inspector with panel settings and bind tree actions', () => {
    assertWorkspaceFileExists('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'Inspectors', 'UIPanelInspector.cs');
    assertWorkspaceFileExists('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'Inspectors', 'UIPanelInspectorStyles.uss');
    const source = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'Inspectors', 'UIPanelInspector.cs');
    const style = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'Inspectors', 'UIPanelInspectorStyles.uss');

    assert.match(source, /\[CustomEditor\(typeof\(UIPanel\),\s*true\)\]/);
    assert.match(source, /CreateInspectorGUI\(/);
    assert.match(source, /uipanel-inspector/);
    assert.match(source, /uipanel-section/);
    assert.match(source, /uipanel-section-panelconfig/);
    assert.match(source, /uipanel-panelconfig-foldout/);
    assert.match(source, /ANIMATION_SECTION_FOLDOUT_KEY/);
    assert.match(source, /FOCUS_SECTION_FOLDOUT_KEY/);
    assert.match(source, /CreateSubSection\("动画设置",\s*"uipanel-subsection-animation",\s*ANIMATION_SECTION_FOLDOUT_KEY\)/);
    assert.match(source, /CreateSubSection\("焦点设置",\s*"uipanel-subsection-focus",\s*FOCUS_SECTION_FOLDOUT_KEY\)/);
    assert.match(source, /SessionState\.GetBool\(sessionStateKey,\s*true\)/);
    assert.match(source, /SessionState\.SetBool\(sessionStateKey/);
    assert.match(source, /SessionState\.GetString/);
    assert.match(source, /SessionState\.SetString/);
    assert.match(source, /uipanel-subsection/);
    assert.match(source, /uipanel-subsection-foldout/);
    assert.match(source, /uipanel-helpbox/);
    assert.match(source, /面板设置/);
    assert.match(source, /动画设置/);
    assert.match(source, /焦点设置/);
    assert.match(source, /uipanel-section-bindtree/);
    assert.match(source, /uipanel-bindtree-foldout/);
    assert.match(source, /uipanel-bindtree-container/);
    assert.match(source, /uipanel-bindtree-node/);
    assert.match(source, /uipanel-bindtree-stats/);
    assert.match(source, /uipanel-validation-summary/);
    assert.match(source, /CreateBindTreeLegend/);
    assert.match(source, /绑定树/);
    assert.match(source, /打开脚本/);
    assert.match(source, /刷新绑定树/);
    assert.match(source, /生成 UI 代码/);
    assert.match(source, /UIKitPanelPrefabCreator\.GenerateCodeForPrefab/);
    assert.match(source, /private\s+int\s+RenderBindChildren/);
    assert.match(source, /var\s+renderedCount\s*=\s*RenderBindChildren/);
    assert.doesNotMatch(source, /private\s+struct\s+BindTreeStats/);

    assert.match(style, /\.uipanel-inspector/);
    assert.match(style, /\.uipanel-section-panelconfig/);
    assert.match(style, /\.uipanel-bindtree-container/);
    assert.match(style, /\.uipanel-bindtree-node/);
    assert.match(style, /\.uipanel-validation-summary/);
});

test('UIKit restores AbstractBind custom inspector with quick conversion and code preview', () => {
    assertWorkspaceFileExists('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'Inspectors', 'AbstractBindInspector.cs');
    assertWorkspaceFileExists('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'Bind', 'BindInspectorStyles.uss');
    const source = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'Inspectors', 'AbstractBindInspector.cs');
    const style = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'Bind', 'BindInspectorStyles.uss');

    assert.match(source, /\[CustomEditor\(typeof\(AbstractBind\),\s*true\)\]/);
    assert.match(source, /\[CanEditMultipleObjects\]/);
    assert.match(source, /bind-inspector/);
    assert.match(source, /bind-container/);
    assert.match(source, /bind-row/);
    assert.match(source, /bind-label/);
    assert.match(source, /bind-field/);
    assert.match(source, /TOKENS_STYLE_SHEET_PATH\s*=\s*"Assets\/YokiFrame\/Adapters\/Unity\/Editor\/UISystem\/Styling\/Tokens\/YokiTokens\.uss"/);
    assert.match(source, /CORE_STYLE_SHEET_PATH\s*=\s*"Assets\/YokiFrame\/Adapters\/Unity\/Editor\/UISystem\/Styling\/Core\/YokiCoreComponents\.uss"/);
    assert.match(source, /AddStyleSheet\(root,\s*TOKENS_STYLE_SHEET_PATH\)/);
    assert.match(source, /AddStyleSheet\(root,\s*CORE_STYLE_SHEET_PATH\)/);
    assert.match(source, /mBindTypeField\.AddToClassList\("yoki-field-row__field"\)/);
    assert.match(source, /mBindTypeField\.AddToClassList\("bind-dropdown-field"\)/);
    assert.match(source, /mComponentPopup\.AddToClassList\("yoki-field-row__field"\)/);
    assert.match(source, /mComponentPopup\.AddToClassList\("bind-dropdown-field"\)/);
    assert.match(source, /type-convert-row/);
    assert.match(source, /type-convert-buttons/);
    assert.match(source, /type-convert-btn/);
    assert.match(source, /绑定类型/);
    assert.match(source, /快速转换/);
    assert.match(source, /→ Member/);
    assert.match(source, /→ Element/);
    assert.match(source, /→ Component/);
    assert.match(source, /字段名称/);
    assert.match(source, /组件列表/);
    assert.match(source, /suggestion-row/);
    assert.match(source, /bind-path-row/);
    assert.match(source, /路径/);
    assert.match(source, /code-preview-foldout/);
    assert.match(source, /code-preview-text/);
    assert.match(source, /代码预览/);
    assert.match(source, /CODE_PREVIEW_FOLDOUT_KEY/);
    assert.match(source, /SessionState\.GetBool\(CODE_PREVIEW_FOLDOUT_KEY,\s*false\)/);
    assert.match(source, /SessionState\.SetBool\(CODE_PREVIEW_FOLDOUT_KEY/);
    assert.match(source, /\[SerializeField\]/);
    assert.match(source, /jump-to-code-btn/);
    assert.match(source, /跳转到代码/);

    assert.match(style, /\.bind-inspector/);
    assert.match(style, /--layer-card:\s*rgb\(45,\s*47,\s*54\)/);
    assert.match(style, /\.bind-container/);
    assert.match(style, /\.bind-row/);
    assert.match(style, /\.bind-field TextField > TextInput/);
    assert.match(style, /\.bind-field EnumField > VisualElement/);
    assert.match(style, /\.bind-dropdown-field\s*>\s*\.unity-base-field__input/);
    assert.match(style, /\.bind-dropdown-field\s*>\s*\.unity-base-popup-field__input/);
    assert.match(style, /\.bind-dropdown-field\s+\.unity-base-popup-field__text/);
    assert.match(style, /\.bind-dropdown-field\s+\.unity-base-popup-field__arrow/);
    assert.match(style, /padding:\s*var\(--spacing-sm\)\s+var\(--spacing-md\)/);
    assert.match(style, /\.type-convert-btn/);
    assert.match(style, /rgba\(33,\s*150,\s*243,\s*0\.1\)/);
    assert.match(style, /\.code-preview-text/);
    assert.match(style, /\.jump-to-code-btn/);
});

test('UIKit restores bind component shortcut menus', () => {
    assertWorkspaceFileExists('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'Bind', 'BindShortcuts.cs');
    const source = readWorkspaceFile('Assets', 'YokiFrame', 'Tools', 'UIKit', 'Editor', 'Bind', 'BindShortcuts.cs');

    assert.match(source, /\[MenuItem\("Assets\/UIKit - 生成 UI 代码"/);
    assert.match(source, /\[MenuItem\("Edit\/UIKit\/Add Bind Component &b"/);
    assert.match(source, /\[MenuItem\("Edit\/UIKit\/Remove Bind Component &%b"/);
    assert.match(source, /Undo\.AddComponent<Bind>/);
    assert.match(source, /Undo\.DestroyObjectImmediate/);
    assert.match(source, /UIKitPanelPrefabCreator\.GenerateCodeForPrefab/);
});
