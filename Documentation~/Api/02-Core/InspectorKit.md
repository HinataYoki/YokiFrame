# InspectorKit Unity Inspector

## 适用场景

InspectorKit 用于在 Unity Inspector 中复用一套 UI Toolkit 字段装饰、只读状态、信息提示、分组标题和操作按钮能力。它解决的是“如何把项目组件的 Inspector 做成统一、可组合的编辑器工具界面”，不是运行时 UI，也不是完整序列化框架。

适合以下场景：

- Unity 组件需要在字段前显示配置分组标题或说明卡片。
- 某些序列化字段只允许查看，不允许在 Inspector 中直接编辑。
- Editor 工具需要给组件提供“重新生成”“校验”“刷新”等无参数操作按钮。
- Core Adapter 和各 Tool Kit 的 Unity Editor 适配层需要复用统一的 Inspector 样式和字段绑定逻辑。

InspectorKit 不提供 Odin 独立序列化、多态对象树、表达式引擎、拖拽排序或虚拟化等复杂列表系统、运行时 UI 或通用 Scene/Prefab 自动化；它提供轻量的序列化字符串列表和按需对象选择列表。它不绕过 Unity 的 `SerializedObject`、`SerializedProperty` 和 Undo 系统。

## 使用前提

InspectorKit 只支持 Unity。属性元数据可以被 Runtime 组件引用，绘制、Undo 和按钮调用只能在 Unity Editor 中执行；它不是运行时 UI，也不替代 Unity 的序列化系统。

## 快速上手

### 1. 在组件上声明元数据

```csharp
using UnityEngine;
using YokiFrame.Unity.Inspector;

public sealed class MyComponent : MonoBehaviour
{
    [InspectorSection("运行时配置")]
    [InspectorInfoBox("该配置会在启动时读取")]
    [SerializeField] private int mLevel;

    [InspectorReadOnly]
    [SerializeField] private string mGeneratedId;

    [InspectorButton("重新生成")]
    private void RegenerateId()
    {
        mGeneratedId = System.Guid.NewGuid().ToString("N");
    }
}
```

`InspectorSection` 和 `InspectorInfoBox` 只能标记字段；`InspectorReadOnly` 也只改变 Inspector 的编辑状态，不会把字段改成 C# `readonly`，更不会阻止运行时代码写入。

### 2. 声明 CustomEditor

Editor 文件中声明目标组件并继承 `InspectorKitEditor`：

```csharp
#if UNITY_EDITOR
using UnityEditor;
using YokiFrame.Unity.Inspector;

[CustomEditor(typeof(MyComponent))]
public sealed class MyComponentInspector : InspectorKitEditor
{
}
#endif
```

UI Toolkit 路径会自动创建根元素、序列化字段和按钮区域；不支持 UI Toolkit 的 Unity 版本会回退到 Unity 默认字段 Inspector，同时保留按钮绘制。

### 3. 操作结果

按钮调用针对当前 Inspector 选中的全部目标对象执行。每个目标调用前记录 Undo，成功后调用 `EditorUtility.SetDirty`，最后刷新 `SerializedObject`。按钮方法抛出异常时记录 Unity Exception，不会继续把异常向 Inspector 绘制流程抛出。

## 核心 API

### 元数据属性 API

所有元数据属性位于 `YokiFrame.Unity.Inspector`。

### `InspectorMetadataAttribute`

```csharp
public abstract class InspectorMetadataAttribute : UnityEngine.PropertyAttribute
```

这是 Inspector 字段元数据的 Unity 属性基类，本身不提供公开成员和运行时行为。`InspectorSectionAttribute`、`InspectorInfoBoxAttribute` 和 `InspectorReadOnlyAttribute` 继承它。

### `InspectorSectionAttribute`

```csharp
InspectorSectionAttribute(string title)
string Title { get; }
```

只能用于字段，`title == null` 时保存为空字符串。Editor 在对应序列化字段前插入分组标题。

### `InspectorInfoBoxAttribute`

```csharp
InspectorInfoBoxAttribute(
    string message,
    InspectorInfoBoxType type = InspectorInfoBoxType.Info)

string Message { get; }
InspectorInfoBoxType Type { get; }
```

只能用于字段，`message == null` 时保存为空字符串。Editor 会在字段前插入信息提示卡片，颜色由 `InspectorInfoBoxType` 决定。

### `InspectorReadOnlyAttribute`

```csharp
InspectorReadOnlyAttribute()
```

只能用于字段。Inspector 中对应 `PropertyField` 会被设为不可交互并应用只读样式；它不改变字段的序列化格式、访问级别或运行时可写性。

### `InspectorButtonAttribute`

```csharp
InspectorButtonAttribute(string label)
string Label { get; }
```

只能用于方法，`label == null` 时保存为空字符串。当前 Editor 实现会为非静态、非特殊、参数数量为零的方法创建按钮。因此应使用实例、无参数方法；可选参数不会被自动填充。

### `InspectorInfoBoxType`

| 值 | 用途 |
|---|---|
| `Info` | 普通说明，使用信息样式 |
| `Success` | 成功或已完成状态 |
| `Warning` | 警告状态 |
| `Error` | 错误状态 |

### `InspectorKitEditor`

```csharp
public abstract class InspectorKitEditor : UnityEditor.Editor
```

这是推荐的 CustomEditor 基类。派生类通常只需要添加 `[CustomEditor(typeof(TargetType))]`，不需要重写方法。

### `CreateInspectorGUI`

```csharp
public override VisualElement CreateInspectorGUI()
```

创建 UI Toolkit Inspector：

1. 创建并应用 InspectorKit 样式的根元素。
2. 从当前 `SerializedObject` 读取顶层可见序列化字段，跳过 Unity 的 `m_Script` 字段。
3. 按字段反射元数据插入 section 和 info box。
4. 创建并绑定 `PropertyField`；标记 `InspectorReadOnly` 的字段设为不可交互。
5. 发现按钮方法时追加操作按钮区域。

当 `target` 为空时只返回样式根元素。字段和按钮均通过 InspectorKitUi 构建。

### `OnInspectorGUI`

```csharp
public override void OnInspectorGUI()
```

这是 UI Toolkit 不可用时的回退入口：调用 `serializedObject.Update()`，绘制 Unity 默认 Inspector，再扫描按钮方法并使用 `GUILayout.Button` 绘制，最后调用 `serializedObject.ApplyModifiedProperties()`。回退路径保留按钮能力，但不绘制 InspectorKit 的 section、info box 和只读 UI Toolkit 样式。

### 按钮调用约束

按钮方法通过 `BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic` 扫描。调用时会：

- 跳过空方法、静态方法和带参数方法。
- 对每个选中对象调用 `Undo.RecordObject`。
- 反射调用实例方法，成功后调用 `EditorUtility.SetDirty`。
- 捕获 `TargetInvocationException` 并记录其内部异常。
- 调用结束后刷新 `SerializedObject`。

### `InspectorKitUi`

```csharp
public static class InspectorKitUi
```

这是 UI Toolkit 低层构建工具，适合自定义 Inspector 需要复用字段、按钮或提示组件时调用。

| API | 说明 |
|---|---|
| `VisualElement CreateRoot()` | 创建带 `yoki-editor-inspector` class 和 Inspector profile 样式的根元素 |
| `VisualElement CreatePropertyFields(SerializedObject serializedObject, Type targetType)` | 创建并绑定顶层可见序列化字段，读取字段元数据 |
| `CreatePropertyFields(..., Func<SerializedProperty, bool> includeProperty)` | 只绘制通过筛选的顶层字段，仍保留字段元数据和序列化绑定 |
| `VisualElement CreateActionButtons(Type targetType, Action<MethodInfo> invoke)` | 扫描零参数 InspectorButton 方法并创建按钮区域 |
| `VisualElement CreateSection(string title)` | 创建 Inspector 分组标题元素 |
| `VisualElement CreateInfoBox(string message, InspectorInfoBoxType type)` | 创建指定级别的信息提示卡片 |

### 通用组合构件

InspectorKit 的目标不是让每个 YooAsset 或 Tool Kit 重新写一套 Editor UI，而是让专用 Drawer 只描述数据语义。常用组合 API 包括：

| API | 说明 |
|---|---|
| `CreatePanel(string title)` | 创建带标题的配置面板 |
| `CreateCard(string title, string stateKey, InspectorCardInitialState initialState, Action<VisualElement> buildContent)` | 创建可折叠且可持久化展开状态的卡片 |
| `CreateFieldRow` / `CreateStackedFieldRow` | 创建横向紧凑字段行，或适合窄 Inspector 的标签在上纵向字段行 |
| `CreatePropertyRow` / `CreateStringRow` / `CreateIntegerRow` | 创建已绑定 `SerializedProperty` 的常用字段行；字符串字段也提供外部设置回调重载 |
| `CreateSwitchRow` | 创建滑块式布尔开关；支持 `SerializedProperty` 双向同步或普通回调，`CreateToggleRow` 转发到该外观 |
| `CreateDropdownRow` | 创建索引回调式下拉字段，适合过滤枚举值 |
| `CreateStringList` | 创建序列化字符串列表；`InspectorStringListOptions.IsReadOnly` 可隐藏增删入口并禁止文本编辑 |
| `CreateSelectionList<T>` | 创建只展示已选对象、通过 Unity 候选菜单按需添加的对象列表；支持详情区、最小数量和删除入口 |
| `CreateActionButton` / `CreateButtonRow` / `CreateCompactButtonRow` | 创建统一语义按钮、等宽操作区或靠左自动换行的紧凑按钮组 |
| `CreateFoldoutSection` | 创建可嵌套、可持久化展开状态的轻量折叠区 |
| `CreateHierarchyView` / `CreateHierarchyItem` / `CreateHierarchyLegend` | 创建带缩进、折叠、选择、强调色和图例的紧凑层级列表 |
| `Refresh` | 局部刷新条件字段或外部设置内容 |

例如，专用配置 Drawer 可以只保留字段映射：

```csharp
VisualElement card = InspectorKitUi.CreateCard(
    "基础配置",
    "MyKit.Basic",
    InspectorCardInitialState.Expanded,
    content =>
    {
        content.Add(InspectorKitUi.CreateStringRow(nameProperty, "名称"));
        content.Add(InspectorKitUi.CreateSwitchRow(enabledProperty, "启用"));
    });
```

卡片、字段行、滑块开关、嵌套折叠、列表增删、按需对象候选菜单、按钮颜色、圆角、间距和展开状态均由 InspectorKit 负责。专用 Integration 只处理对象 Provider、SerializedProperty 写回、条件字段和第三方 API 调用。

`CreatePropertyFields` 或 `CreateActionButtons` 的必要参数为空时返回空容器，不抛出异常。`CreatePropertyFields` 会从目标类型向基类查找字段，绑定 Unity `SerializedProperty`，并跳过 `m_Script`；筛选重载对每个顶层属性调用一次 `includeProperty`，适合把框架字段和派生业务字段分区。

`CreateActionButtons` 只负责创建按钮，点击后把对应 `MethodInfo` 交给 `invoke`。它不自行执行方法，也不负责 Undo 和 Dirty；这些职责由 `InspectorKitEditor` 的调用回调承担。

### UIPanel 派生字段与第三方绘制工具

`UIPanel` 的自定义 Inspector 会把“其他属性”卡片的派生序列化字段放进局部 `IMGUIContainer`，并通过 `EditorGUILayout.PropertyField` 绘制。这样 Unity 的 `PropertyDrawer` / `PropertyHandler` 管线仍然可用；TriInspector、Odin 等工具的常见字段元数据也会由兼容层映射，不会因为 InspectorKit 的 UI Toolkit 外壳而完全丢失。

该兼容层不引用任何第三方程序集，而是按属性类型名读取常见元数据：`Title`、`InfoBox` / `HelpBox`、`LabelText`、`PropertyTooltip` / `Tooltip`、`ReadOnly` 和 `ListDrawerSettings.AlwaysExpanded`。它还会过滤 UIPanel 框架字段与生成的 `mData`，保留派生面板自己的业务字段和代码生成字段。

兼容层还会在字段之后扫描派生面板的实例方法，并识别 `ButtonAttribute`（TriInspector/Odin）和 `InspectorButtonAttribute`。按钮只接受非静态、非特殊、非泛型且无参数的方法；按钮文本优先读取 `Name`、`Label` 或 `Text`，没有自定义文本时回退到方法名。点击后会对当前 Inspector 选中的全部目标执行 Undo、方法调用和 Dirty 标记。

兼容范围限于可由 `SerializedProperty` 表示的字段和上述无参数方法按钮；`ShowInInspector` 属性、带参数按钮、条件表达式、完整第三方 CustomEditor 或需要第三方对象树的功能不能嵌入该卡片。需要这些能力时，应为具体类型提供专用 CustomEditor，或直接使用对应工具的完整 Inspector。

### `YokiFrameEditorStyleService`

```csharp
public static class YokiFrameEditorStyleService
```

### `Apply`

```csharp
public static void Apply(
    VisualElement root,
    YokiFrameEditorStyleProfile profile)
```

向根元素加载并去重添加样式表。`root == null` 时直接返回。两种 profile 都加载 Tokens、Components 和 AdvancedComponents；`Inspector` profile 额外加载 `InspectorKit.uss` 与 `InspectorKitHierarchy.uss`。样式通过 `AssetDatabase.FindAssets` 按精确文件名和 `.uss` 后缀查找，因此不依赖固定包根路径。

### `ClearCache`

```csharp
public static void ClearCache()
```

清空四类样式表缓存。Unity 域重载、资源重新导入或样式文件变更后可以调用，下一次 `Apply` 会重新查找资源。

### `YokiFrameEditorStyleProfile`

| 值 | 作用 |
|---|---|
| `Core` | 加载通用设计令牌、组件和扩展组件样式 |
| `Inspector` | 在通用样式基础上追加 `InspectorKit.uss` 与 `InspectorKitHierarchy.uss` |

### `YokiFrameEditorStyleSheet`

| 值 | 对应资源 |
|---|---|
| `Tokens` | `YokiFrameEditorTokens.uss` |
| `Components` | `YokiFrameEditorComponents.uss` |
| `AdvancedComponents` | `YokiFrameEditorAdvancedComponents.uss` |
| `InspectorKit` | `InspectorKit.uss` |
| `InspectorKitHierarchy` | `InspectorKitHierarchy.uss` |

Inspector 专属 class 使用 `yoki-editor-inspector` 命名空间，包括 section、info box、只读字段和按钮区域。样式资源由共享服务缓存，不应由各个 CustomEditor 重复手动加载。

## 生命周期与错误边界

- 字段元数据通过反射读取，并从目标类型逐级向基类查找。
- UI Toolkit Inspector 只为顶层可见序列化字段创建 `PropertyField`。
- Unity 的 `m_Script` 字段始终跳过，不显示为普通字段。
- `InspectorSection` 和 `InspectorInfoBox` 的顺序由字段在 Unity 序列化对象中的迭代顺序决定。
- 方法按钮扫描公开和非公开实例方法；静态、特殊、泛型、带参数方法不会创建按钮。`ButtonAttribute` 与 `InspectorButtonAttribute` 均按类型名兼容，不要求包根引用第三方程序集。
- 空标签显示方法名；非空 `InspectorButtonAttribute.Label` 使用自定义文本。
- InspectorKit 不执行属性校验，不负责字段值写入规则，也不提供多目标之间的业务一致性检查。

## 在工具中查看

InspectorKit 没有独立 Workbench 页面；它的效果直接显示在 Unity Inspector 中。运行时 UI、跨引擎工具和项目业务状态仍由其它 Kit 或宿主编辑器负责。

## 限制与相关资料

| 问题 | 处理 |
|---|---|
| 元数据显示但没有 Inspector 效果 | 确认目标对象使用了继承 `InspectorKitEditor` 的 `CustomEditor`，且编辑器脚本已编译 |
| 按钮不显示 | 确认方法为实例方法、无参数、非特殊方法，并且带有 `InspectorButtonAttribute` 或兼容的 `ButtonAttribute` |
| 可选参数按钮不工作 | 当前实现只接受零参数方法；为按钮提供无参数包装方法 |
| 只读字段仍可被运行时代码修改 | `InspectorReadOnly` 只限制 Inspector UI 编辑，不是运行时只读约束 |
| 自定义样式未加载 | 检查 USS 文件名是否精确为约定名称，并在资源重导入后调用 `ClearCache` |
| 多个组件样式重复添加 | 通过 `YokiFrameEditorStyleService.Apply` 加载；服务会对同一根元素去重 |
| 想在游戏运行时使用 Inspector | InspectorKit 只改变 Unity Editor 面板；运行时请使用组件自己的公开属性和方法 |

InspectorKit 只覆盖 Unity Inspector 的元数据、绘制和按钮调用，不提供跨引擎实现。遇到绘制问题时，先确认目标组件使用了继承 `InspectorKitEditor` 的 `CustomEditor`，并检查 Unity Editor 的 Console。
