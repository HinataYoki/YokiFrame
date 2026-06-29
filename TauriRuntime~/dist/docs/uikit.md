# UIKit 面板

## 当前边界

UIKit 当前仍包含 Unity UI runtime 实现，同时保留 `IUIBackend` 兼容层。业务代码统一调用 `UIKit` 静态入口；GameObject、Canvas、DOTween 等宿主细节应留在后端或 Unity 侧实现。

## 配置后端

```csharp
using YokiFrame;

UIKit.SetBackend(myUiBackend);
```

Unity 默认初始化会安装可用后端；Godot 完整接入需要独立 `IUIBackend`。

## 打开和关闭面板

```csharp
var menu = UIKit.OpenPanel<MenuPanel>(
    UILevel.Common,
    data: null,
    tag: "main");

UIKit.HidePanel(menu);
UIKit.ShowPanel(menu);
UIKit.ClosePanel<MenuPanel>();
```

异步打开：

```csharp
var panel = await UIKit.OpenPanelAsync<MenuPanel>(
    UILevel.Common,
    data: null,
    token: cts.Token);
```

## 面板栈

```csharp
var menu = UIKit.OpenPanel<MenuPanel>(UILevel.Common);
UIKit.PushPanel(menu, "Main", hidePreLevel: true);

var settings = UIKit.PushOpenPanel<SettingsPanel>(UILevel.Pop);
UIKit.PopPanel(showPreLevel: true, autoClose: true);
```

常用入口：

| 方法 | 用途 |
|---|---|
| `PushPanel(panel, stackName, hidePreLevel)` | 压入已有面板。 |
| `PushOpenPanel<T>()` | 打开并压栈。 |
| `PopPanel(showPreLevel, autoClose)` | 弹出栈顶。 |
| `PeekPanel(stackName)` | 查看栈顶。 |
| `ClearStack(stackName, closeAll)` | 清空指定栈。 |

## 面板加载

默认加载池通过 `ResKit.LoadAsset<GameObject>()` 加载面板，默认路径：

```text
Art/UIPrefab/<PanelName>
```

YooAsset 使用类型名作为 location 时：

```csharp
ResKit.SetProvider(new YooAssetResourceProvider());
UIKit.GetPanelLoader().UseAddressableLocation = true;
```

自定义加载器：

```csharp
UIKit.SetPanelLoader(new MyPanelLoaderPool());
```

## 绑定和代码生成

这套绑定和生成系统用于 Unity Editor 里的 Unity UI Prefab。它负责把 Prefab 上的 `Bind` 标记转换成 `UIPanel`、`UIElement`、`UIComponent` 的 C# partial 类，并在 Unity 编译后把引用回填到 Prefab。

Godot 项目可以继续使用 UIKit 的运行时抽象，但当前这套 Prefab 绑定和代码生成只面向 Unity。

### 推荐流程

1. 创建或准备一个 UI Prefab，Prefab 根对象名称使用面板类名，例如 `MainMenuPanel`。
2. 在 Inspector / Hierarchy 选中需要暴露给代码的节点，按 `Alt+B` 添加 `Bind` 组件。
3. 在 `Bind` Inspector 中选择绑定类型，填写字段名、类名或组件类型。
4. 对 Prefab 执行“生成 UI 代码”。
5. 等 Unity 编译完成，生成器会把面板脚本、Element、Component 挂到 Prefab 并回填引用。
6. 在用户脚本里写业务逻辑，运行时通过 `UIKit.OpenPanel<MainMenuPanel>()` 打开。

### 入口

| 位置 | 用途 |
|---|---|
| `Alt+B` | 推荐入口：给当前选中的一个或多个 GameObject 添加 `Bind`。 |
| `Add Component/YokiFrame/UIKit/Bind` | 给单个节点添加绑定组件。 |
| `Edit/UIKit/Add Bind Component` | 给当前选中对象批量添加 `Bind`。 |
| `Edit/UIKit/Remove Bind Component` | 从当前选中对象批量移除 `Bind`。 |
| Project 视图选中 Prefab 后 `Assets/UIKit - 生成 UI 代码` | 为选中 Prefab 生成或刷新代码。 |
| `UIPanel` Inspector 的“绑定树” | 查看绑定统计、错误、跳转脚本、刷新绑定树、生成 UI 代码。 |
| `Bind` Inspector | 设置绑定类型、字段名、类型、注释，查看代码预览和跳转生成代码。 |
| 工作台 UIKit 页“Unity 编辑器工具” | 创建 UIPrefab、为选中 Prefab 生成代码、批量添加或移除 Bind。 |

工作台字段：

| 字段 | 说明 |
|---|---|
| `Panel 名称` | 面板 C# 类型名，例如 `MainMenuPanel`。必须是合法 C# 标识符。 |
| `命名空间` | 生成脚本的命名空间，默认 `GameUI`。 |
| `程序集` | 编译后用于反射查找面板类型。默认 `Assembly-CSharp`；如果脚本在 asmdef 下，选择对应程序集名。 |
| `代码模板` | `Default` 生成完整生命周期；`Minimal` 只生成最少生命周期。 |
| `Prefab 目录` | 新建 Prefab 的输出目录，默认 `Assets/Resources/Art/UIPrefab`。 |
| `脚本目录` | 生成脚本根目录，默认 `Assets/Scripts/UI`。 |
| `目标 Prefab` | 为空时使用 Unity 当前 Selection；也可以填具体 Prefab 路径。 |
| `覆盖 Prefab` | 只影响创建同名 Prefab；不会保护 `.Designer.cs`。 |

### Bind 字段

| 字段 | 说明 |
|---|---|
| `Bind` | 绑定类型：`Member`、`Element`、`Component`、`Leaf`。 |
| `Name` | 生成字段名。为空时 Inspector 会按 GameObject 名建议 Pascal 命名。 |
| `AutoType` | `Member` 使用的组件类型，由 Inspector 的组件列表写入。 |
| `CustomType` | `Element` 或 `Component` 的生成类名。 |
| `Type` | 最终参与代码生成的类型。通常由 Inspector 自动写入，不手动改。 |
| `Comment` | 字段注释，目前主要用于 Inspector 预览和后续扩展。 |

命名要求：

| 项 | 要求 |
|---|---|
| Panel 名称 | 合法 C# 类型名，例如 `MainMenuPanel`。 |
| 字段名 `Name` | 合法 C# 标识符，例如 `BtnStart`、`RewardList`。 |
| 类名 `CustomType` | 合法 C# 类型名，例如 `RewardItem`、`InventorySlot`。 |
| 同一容器内字段名 | 不要重复。重复会导致校验报错、字段跳过或生成失败。 |

### 绑定类型

| 类型 | 生成结果 | 适合场景 |
|---|---|---|
| `Member` | 在最近的 Panel / Element / Component Designer 里生成一个字段。字段类型来自节点上的 Unity 组件。 | Button、Text、Image、Toggle、Slider 等普通控件引用。 |
| `Element` | 生成一个继承 `UIElement` 的面板内类，并在当前容器中生成该 Element 字段。 | 只属于当前面板的局部 UI 结构，例如一个列表项、一个设置分组。 |
| `Component` | 生成一个继承 `UIComponent` 的可复用类，并在当前容器中生成该 Component 字段。 | 多个面板共用的 UI 组件，例如头像块、货币条、通用物品格。 |
| `Leaf` | 不生成代码，并停止继续扫描该节点的子树。 | 明确不想暴露给代码的装饰节点或复杂子树。 |

`Member` 不建立新的代码作用域。它下面的子绑定会继续归属到最近的 Panel、Element 或 Component。需要嵌套对象时，把父节点改成 `Element` 或 `Component`。

`Element` 是面板内部结构，生成到当前面板目录：

```text
Assets/Scripts/UI/<PanelName>/UIElement/<ElementType>.cs
Assets/Scripts/UI/<PanelName>/UIElement/<ElementType>.Designer.cs
```

默认命名空间：

```text
GameUI.<PanelName>UIElement
```

`Component` 是跨面板复用结构，生成到公共目录：

```text
Assets/Scripts/UI/UIComponent/<ComponentType>.cs
Assets/Scripts/UI/UIComponent/<ComponentType>.Designer.cs
```

Component 下不能定义 Element。如果一个结构需要被 Component 拥有，就继续用 `Member` 或 `Component`；如果它只属于某个面板，就把它放在 Panel 或 Element 下。

`Leaf` 用来明确截断生成。生成器遇到 `Leaf` 后不会为该节点生成字段，也不会继续扫描它的子节点。

### 生成文件

默认输入：

```text
PanelName       MainMenuPanel
ScriptFolder    Assets/Scripts/UI
PrefabFolder    Assets/Resources/Art/UIPrefab
Namespace       GameUI
```

默认输出：

```text
Assets/Resources/Art/UIPrefab/MainMenuPanel.prefab

Assets/Scripts/UI/MainMenuPanel/MainMenuPanel.cs
Assets/Scripts/UI/MainMenuPanel/MainMenuPanel.Designer.cs

Assets/Scripts/UI/MainMenuPanel/UIElement/<ElementType>.cs
Assets/Scripts/UI/MainMenuPanel/UIElement/<ElementType>.Designer.cs

Assets/Scripts/UI/UIComponent/<ComponentType>.cs
Assets/Scripts/UI/UIComponent/<ComponentType>.Designer.cs
```

按占位符看，面板核心文件是：

```text
Assets/Scripts/UI/<PanelName>/<PanelName>.cs
Assets/Scripts/UI/<PanelName>/<PanelName>.Designer.cs
```

文件职责：

| 文件 | 是否覆盖 | 写什么 |
|---|---|---|
| `<PanelName>.cs` | 已存在时不覆盖 | 面板业务逻辑、事件注册、按钮点击、打开参数处理。 |
| `<PanelName>.Designer.cs` | 每次生成都会重写 | 绑定字段、`Data` 属性、`ClearUIComponents()`。 |
| `<ElementType>.cs` | 已存在时不覆盖 | Element 自己的业务逻辑。 |
| `<ElementType>.Designer.cs` | 每次生成都会重写 | Element 内部绑定字段和 `Clear()`。 |
| `<ComponentType>.cs` | 已存在时不覆盖 | 可复用 UI 组件逻辑。 |
| `<ComponentType>.Designer.cs` | 每次生成都会重写 | Component 内部绑定字段和 `Clear()`。 |

只改非 Designer 文件。`.Designer.cs` 是生成物，下一次生成会被覆盖。

### 代码模板

`Default` 面板脚本会生成：

```csharp
protected override void OnInit(IUIData uiData = null)
protected override void OnOpen(IUIData uiData = null)
protected override void OnShow()
protected override void OnHide()
protected override void OnClose()
```

`Minimal` 面板脚本只生成：

```csharp
protected override void OnInit(IUIData uiData = null)
protected override void OnClose()
```

两种模板都会生成 `<PanelName>Data : IUIData`，并在 `OnInit` 中初始化 `mData`。

### 生成代码示例

Prefab 绑定：

```text
MainMenuPanel
└── Panel
    ├── BtnStart       Member    UnityEngine.UI.Button
    ├── RewardItem     Element   RewardItem
    │   ├── Icon        Member    UnityEngine.UI.Image
    │   └── CountText   Member    UnityEngine.UI.Text
    └── PlayerCard     Component PlayerCard
        └── NameText    Member    UnityEngine.UI.Text
```

`MainMenuPanel.Designer.cs`：

```csharp
namespace GameUI
{
    public partial class MainMenuPanel
    {
        public UnityEngine.UI.Button BtnStart;
        public GameUI.MainMenuPanelUIElement.RewardItem RewardItem;
        public PlayerCard PlayerCard;

        [SerializeField]
        private MainMenuPanelData mData;

        public MainMenuPanelData Data
        {
            get { return mData; }
        }

        protected override void ClearUIComponents()
        {
            BtnStart = default;
            RewardItem = default;
            PlayerCard = default;
            mData = null;
        }
    }
}
```

### 引用回填

生成代码后，Unity 需要先编译出新的类型。编译完成后，生成器会自动处理等待队列：

1. 找到 `命名空间 + Panel 名称` 对应的 `UIPanel` 类型。
2. 如果 Prefab 根对象没有该组件，则自动添加。
3. 重新扫描 Prefab 上的 `Bind`。
4. `Member` 回填节点上的目标 Unity 组件。
5. `Element` 和 `Component` 回填节点上的生成组件；如果组件不存在，则自动添加。
6. 递归回填 Element / Component 内部字段。

如果回填没有发生，先看 Unity Console 是否有编译错误，再检查工作台里的 `程序集`、`命名空间`、`Panel 名称` 是否和生成脚本一致。

### 常用做法

| 任务 | 做法 |
|---|---|
| 新建面板 | 工作台填写 `Panel 名称`，点击 `创建 UIPrefab`，搭 UI 后选中节点按 `Alt+B` 添加 Bind，再生成代码。 |
| 接入已有 Prefab | 选中 Prefab，给关键节点按 `Alt+B` 添加 Bind，普通控件用 `Member`，局部结构用 `Element`，复用结构用 `Component`。 |
| 给按钮加字段 | 选中按钮，按 `Alt+B`，类型选 `Member`，字段名填 `BtnStart`，组件选 `UnityEngine.UI.Button`。 |
| 做可复用列表项 | 列表项根节点用 `Component`，类名如 `InventorySlot`，内部子控件继续用 `Member`。 |

### 自定义绑定生成

UI 生成代码可以自定义，入口是绑定类型策略接口 `IBindTypeStrategy`。生成器扫描 Prefab 后，会按每个 `BindType` 取对应策略，决定字段类型、脚本路径、命名空间、基类、是否生成独立类文件，以及子绑定是否合法。

适合用它自定义：

| 需求 | 改什么 |
|---|---|
| 改 Element / Component 的生成目录 | `GetScriptPath()`。 |
| 改生成类命名空间 | `GetNamespace()`。 |
| 改生成字段类型 | `GetFullTypeName()`。 |
| 改生成类基类 | `GetBaseClassName()`。 |
| 新增一种绑定语义 | 新增 `BindType` 值和对应 `IBindTypeStrategy`。 |
| 限制子节点规则 | `ValidateChild()`。 |

最小结构：

```csharp
using YokiFrame;

public sealed class MyBindStrategy : IBindTypeStrategy
{
    public BindType Type => BindType.Component;
    public string DisplayName => "组件";
    public bool RequiresClassFile => true;
    public bool CanContainChildren => true;
    public bool SupportsConversion => true;
    public bool ShouldSkipCodeGen => false;

    public string InferTypeName(AbstractBind bind)
    {
        return bind != null ? bind.name : null;
    }

    public bool ValidateChild(BindType childType, out string reason)
    {
        reason = null;
        return true;
    }

    public string GetFullTypeName(BindCodeInfo bindInfo, IBindCodeGenContext context)
    {
        return bindInfo.Type;
    }

    public string GetScriptPath(BindCodeInfo bindInfo, IBindCodeGenContext context, bool isDesigner)
    {
        var fileName = isDesigner ? $"{bindInfo.Type}.Designer.cs" : $"{bindInfo.Type}.cs";
        return $"{context.ScriptRootPath}/UIComponent/{fileName}";
    }

    public string GetNamespace(IBindCodeGenContext context)
    {
        return context.ScriptNamespace;
    }

    public string GetBaseClassName()
    {
        return nameof(UIComponent);
    }
}
```

注册：

```csharp
using UnityEditor;
using YokiFrame;

[InitializeOnLoad]
public static class MyUIKitBindStrategyBootstrap
{
    static MyUIKitBindStrategyBootstrap()
    {
        BindStrategyRegistry.Register(new MyBindStrategy());
    }
}
```

注意：

| 规则 | 原因 |
|---|---|
| 注册代码放 Editor 程序集 | 生成脚本发生在 Unity Editor。 |
| 重写内置 `BindType` 会影响所有对应绑定 | `Register()` 会用 `strategy.Type` 覆盖旧策略。 |
| `.Designer.cs` 仍由生成器重写 | 自定义策略改变生成位置和类型，不改变 Designer 文件的覆盖规则。 |
| Panel 根脚本模板只有 `Default` / `Minimal` | `IBindTypeStrategy` 控制绑定类型生成，不控制 `<PanelName>.cs` 的生命周期模板。 |

### 常见问题

| 问题 | 处理方式 |
|---|---|
| `Panel 名称必须是合法 C# 类型名` | Prefab 名或工作台 `Panel 名称` 改成合法类型名，例如 `MainMenuPanel`。 |
| `命名空间不合法` | 使用点分隔的 C# 标识符，例如 `GameUI` 或 `Game.UI`。 |
| `Member 绑定需要选择组件类型` | 在 `Bind` Inspector 的组件列表中选目标组件；节点上没有组件时先添加组件。 |
| 生成后字段为空 | 等 Unity 编译完成；若仍为空，检查 Console 编译错误、程序集、命名空间和 Prefab 路径。 |
| 绑定树提示字段名重复 | 同一个 Panel / Element / Component 容器内改成唯一字段名。 |
| `Component` 下有 `Element` 报错 | 把 `Element` 移到 Panel 或 Element 下，或者改成 `Component`。 |
| `代码未生成` | 对 Prefab 执行“生成 UI 代码”，并确认脚本目录在 `Assets` 下。 |
| 修改 `.Designer.cs` 后丢失 | `.Designer.cs` 会重写；把逻辑移到同名非 Designer partial 文件。 |

## 对话框

```csharp
UIKit.SetDefaultDialogType<MyDialogPanel>();

UIKit.Alert("操作完成", "提示");

UIKit.Confirm("确定删除？", "确认", confirmed =>
{
    if (confirmed)
    {
        Delete();
    }
});

(bool ok, string value) = await UIKit.PromptAsync(
    "请输入名称",
    "重命名",
    "默认值",
    cts.Token);
```

## 工作台诊断

UIKit 页面用于查看面板缓存、面板状态、层级、栈和 Unity Editor 面板生成工具。

| 在工作台里看什么 | 用途 |
|---|---|
| Panel 列表 | 查看面板是否已加载、打开、隐藏或关闭。 |
| UI Level / Layer | 判断层级是否正确。 |
| Stack | 检查压栈、弹栈、返回顺序。 |
| Cache | 判断面板是否被缓存或重复创建。 |
| Prefab 工具 | Unity 下辅助创建或绑定面板 Prefab。 |

面板打不开时，先看 Panel 列表和加载路径，再看 ResKit Provider。返回异常时，看 Stack 中的顺序和栈顶。工作台不直接替业务代码打开、关闭或压栈面板。

## 常见坑

| 问题 | 处理方式 |
|---|---|
| 面板打不开 | 检查后端、面板路径、ResKit Provider。 |
| YooAsset 找不到面板 | 检查 location，必要时启用 `UseAddressableLocation`。 |
| 栈返回异常 | 检查是否同一面板重复压栈，或 `autoClose` 关闭了前一层。 |
| 工作台没有 UI 数据 | 先看 System 页连接状态，再确认项目已启用 UIKit 状态发布。 |
| 想在工作台里打开面板 | 不直接操作；让运行时代码调用 `UIKit`。 |
