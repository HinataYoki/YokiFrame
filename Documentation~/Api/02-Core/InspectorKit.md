# InspectorKit Unity Inspector

> 面向读者：编写 Unity Inspector、组件元数据或 Editor 工具的开发者
>
> 主要入口：Inspector 元数据属性、`InspectorKitEditor`、`InspectorKitUi`
>
> 运行边界：Unity Adapter；绘制和反射仅在 Unity Editor 编译
>
> 状态来源：当前 `Core/Adapters/Unity/*/Inspector` 源码

## 适用场景

InspectorKit 用于在 Unity Inspector 中复用一套 UI Toolkit 字段装饰、只读状态、信息提示、分组标题和操作按钮能力。它解决的是“如何把项目组件的 Inspector 做成统一、可组合的编辑器工具界面”，不是运行时 UI，也不是完整序列化框架。

适合以下场景：

- Unity 组件需要在字段前显示配置分组标题或说明卡片。
- 某些序列化字段只允许查看，不允许在 Inspector 中直接编辑。
- Editor 工具需要给组件提供“重新生成”“校验”“刷新”等无参数操作按钮。
- Core Adapter 和各 Tool Kit 的 Unity Editor 适配层需要复用统一的 Inspector 样式和字段绑定逻辑。

InspectorKit 不提供 Odin 独立序列化、多态对象树、表达式引擎、拖拽排序或虚拟化等复杂列表系统、运行时 UI 或通用 Scene/Prefab 自动化；它提供轻量的序列化字符串列表和按需对象选择列表。它不绕过 Unity 的 `SerializedObject`、`SerializedProperty` 和 Undo 系统。

## 入口与当前状态

```text
Core/Adapters/Unity/Runtime/Inspector/
  YokiFrame.Unity.Inspector.Runtime
Core/Adapters/Unity/Editor/Inspector/
  YokiFrame.Unity.Inspector.Editor
```

| 程序集 | 作用 | 编译边界 |
|---|---|---|
| `YokiFrame.Unity.Inspector.Runtime` | `PropertyAttribute` 和按钮元数据声明 | Unity Runtime 可引用；不引用 `UnityEditor` |
| `YokiFrame.Unity.Inspector.Editor` | UI Toolkit Inspector、反射、Undo、样式和按钮调用 | 仅 Unity Editor |
| `YokiFrame.Unity.Editor` | Unity Editor 组合程序集，可引用 Inspector Editor 程序集 | 仅 Unity Editor |

Runtime 元数据属性位于 `YokiFrame.Unity.Inspector` 命名空间。Editor API 与属性使用同一命名空间，但实际调用 `InspectorKitEditor`、`InspectorKitUi` 或样式服务的代码必须放在 Editor 程序集或 Editor 文件夹内。

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

只能用于方法，`label == null` 时保存为空字符串。当前 Editor 实现会为非特殊、参数数量为零的方法创建按钮，调用阶段还会跳过静态方法。因此应使用实例、无参数方法；静态零参数方法即使被发现并显示，也不会被执行。可选参数不会被自动填充。

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
- 方法按钮扫描公开和非公开实例方法；特殊方法、带参数方法不会创建按钮。
- 空标签显示方法名；非空 `InspectorButtonAttribute.Label` 使用自定义文本。
- InspectorKit 不执行属性校验，不负责字段值写入规则，也不提供多目标之间的业务一致性检查。

## 限制与相关资料

| 问题 | 处理 |
|---|---|
| 元数据显示但没有 Inspector 效果 | 确认目标对象使用了继承 `InspectorKitEditor` 的 `CustomEditor`，且 Editor 程序集已编译 |
| 按钮不显示 | 确认方法为实例方法、无参数、非特殊方法，并且带有 `InspectorButtonAttribute` |
| 可选参数按钮不工作 | 当前实现只接受零参数方法；为按钮提供无参数包装方法 |
| 只读字段仍可被运行时代码修改 | `InspectorReadOnly` 只限制 Inspector UI 编辑，不是运行时只读约束 |
| 自定义样式未加载 | 检查 USS 文件名是否精确为约定名称，并在资源重导入后调用 `ClearCache` |
| 多个组件样式重复添加 | 通过 `YokiFrameEditorStyleService.Apply` 加载；服务会对同一根元素去重 |
| 想在 Player 使用 `InspectorKitEditor` | Editor 类型不能进入 Player；Player 只能引用 Runtime 元数据属性，并且不获得 Inspector 渲染能力 |

### 验证与非目标

InspectorKit 的 Runtime 元数据与 Editor Inspector 分属独立程序集，验证时应确认：

- `YokiFrame.Unity.Inspector.Runtime` 不引用 `UnityEditor`。
- `YokiFrame.Unity.Inspector.Editor` 仅在 Unity Editor 编译，并依赖 Runtime 元数据程序集。
- UI Toolkit 路径使用 `SerializedObject`/`SerializedProperty`，回退路径使用 Unity 默认 Inspector。
- Inspector 按钮调用产生 Undo、Dirty 和异常日志行为。
- 样式资源按精确文件名加载。

当前不提供专用 InspectorKit Runtime 测试、Kit Interaction、Workbench 页面或 CLI action。后续 Core ResKit Unity YooAsset Inspector 和 Tool Kit Unity Editor 适配层可以复用该基础设施，但不应把 InspectorKit 宣称为一个跨引擎 Runtime Kit。
