# UIKit 面板管理

## 适用场景

UIKit 管理 Unity UI 的 Panel 实例、Prefab 资源租约、生命周期、显式缓存、命名栈、显示层级和模态阻断。它直接使用 `GameObject`、`Canvas`、`RectTransform` 和 Unity UI，因此不是跨引擎 Kit。

- 只支持 Unity 2022.3+。
- 不提供 Godot Adapter、Godot capability 或 Godot 投影入口。
- Root 由包内模板或项目自己的 Prefab Variant 提供，不需要额外注册后端。
- 不提供热度缓存；面板实例由 UIKit 的生命周期和缓存策略管理。
- 每个 `UIPanel` 具体类型当前最多物化一个实例。

## 使用前提

UIKit 只支持 Unity，提供 Panel、层级、模态、动画、Dialog、焦点和绑定代码生成。DOTween 与 Unity Input System 是可选扩展；Godot 没有 UIKit 兼容入口。

## 快速上手

面板 Prefab 根节点需要挂载对应 `UIPanel` 派生组件。默认 ResKit location 是 `Art/UIPrefab/<PanelTypeName>`；Unity Resources Provider 下，Prefab 通常位于：

```text
Assets/Resources/Art/UIPrefab/MainMenuPanel.prefab
```

定义打开数据和面板：

```csharp
using YokiFrame;

public sealed class MainMenuData : IUIData
{
    public MainMenuData(string playerName)
    {
        PlayerName = playerName;
    }

    public string PlayerName { get; }
}

public sealed class MainMenuPanel : UIPanel
{
    /// <summary>实例物化时初始化一次不随打开轮次变化的引用。</summary>
    protected override void OnInit(IUIData data = null)
    {
    }

    /// <summary>每次打开时读取本轮业务数据并刷新内容。</summary>
    protected override void OnOpen(IUIData data = null)
    {
        var menuData = data as MainMenuData;
    }

    /// <summary>当前打开轮次关闭时释放本轮订阅。</summary>
    protected override void OnClose()
    {
    }
}
```

同步打开、隐藏、再次显示和关闭：

```csharp
var panel = UIKit.OpenPanel<MainMenuPanel>(
    level: UILevel.Common,
    data: new MainMenuData("Yoki"),
    tag: "main-menu",
    cachePolicy: PanelCachePolicy.Reusable);

panel.Hide();
panel.Show();
panel.Close();
```

异步打开：

```csharp
var panel = await UIKit.OpenPanelAsync<MainMenuPanel>(
    level: UILevel.Common,
    data: new MainMenuData("Yoki"),
    ct: cancellationToken,
    tag: "main-menu",
    cachePolicy: PanelCachePolicy.Reusable);
```

启用 `YOKIFRAME_UNITASK_SUPPORT` 时异步入口返回 `UniTask`，否则返回 `Task`。UIKit 的状态变更和 Unity API 调用必须在创建 Root 的 Unity 主线程执行。

## 核心 API

### 面板 API

| 入口 | 行为 |
|---|---|
| `OpenPanel<T>(...)` | 同步物化或复用实例，提交本轮 data、tag、level 和缓存策略并显示。 |
| `OpenPanelAsync<T>(...)` | 异步物化或复用实例；同类型加载共享 single-flight。 |
| `GetPanel<T>()` | 读取已物化实例；不创建 Root、不改变预加载状态、不更新 LRU。 |
| `ShowPanel(...)` / `IPanel.Show()` | 显示当前打开轮次中处于 Hide 的面板。 |
| `HidePanel(...)` / `IPanel.Hide()` | 隐藏面板，但保留当前打开轮次和栈归属。 |
| `ClosePanel(...)` / `IPanel.Close()` | 结束当前打开轮次，并按显式缓存策略保留或销毁实例。 |
| `HideAllPanels()` | 隐藏全部当前可见面板。 |
| `CloseAllPanels()` | 关闭全部逻辑打开面板，不卸载纯预加载或已关闭保留项。 |
| `ClosePanelsByTag(tag)` | 关闭当前打开且 tag 匹配的面板。 |

`IPanel` 公开 `Level`、`SubLevel`、`Tag`、`State`、`CachePolicy`、`IsModal` 和 `StackName` 的只读状态。`Data` 通过 `IPanel` 显式接口实现读写，以兼容已有工程；赋值只替换 owner 保存的数据，不会重新触发 `OnInit` / `OnOpen`。具体 Panel 可以继续声明自己的强类型 `Data` 属性，无需使用 `new`。内部 owner、资源 lease 和清理入口不对业务代码开放。

### 生命周期

| 钩子 | 调用时机 |
|---|---|
| `OnInit(data)` | 实例物化时只调用一次；预加载时 data 可能为空。 |
| `OnOpen(data)` | 每次 Open 请求调用，提交本轮数据。 |
| `OnWillShow` / `OnShow` / `OnDidShow` | 从非可见状态进入可见状态时依次调用。 |
| `OnWillHide` / `OnHide` / `OnDidHide` | 从可见状态进入 Hide 时依次调用。 |
| `OnClose` | 当前打开轮次结束时调用；不代表实例一定销毁。 |
| `OnFocus` / `OnBlur` / `OnResume` | 面板成为栈顶、失去栈顶、上层离栈后恢复时调用。 |
| `OnBeforeDestroy` | 实例即将由 UIKit 或外部 Unity 生命周期销毁时调用一次。 |

公开状态依次覆盖 `Preloaded`、`Opening`、`Open`、`Hiding`、`Hide`、`Closing`、`Cached` 和 `Close`。生命周期钩子异常会记录到 LogKit，owner 仍继续完成状态与资源清理。

`UIPanel.OnClosed(callback)` 可登记当前打开轮次关闭后的单次回调。回调执行前已完成反索引、缓存提交或 Transient 销毁与 lease 释放，因此回调可以立即重开同类型面板。派生类需要关闭自身时使用 `CloseSelf()` 或公开 `Close()`，不要绕过 UIKit 直接销毁受管实例。

### 加载与资源所有权

默认 `ResKitPanelLoader` 使用 `ResKit.LoadAsset<GameObject>()` / `LoadAssetAsync<GameObject>()`。每次成功物化都持有独立 `IPanelPrefabLease`，实例最终销毁时释放该 lease。

当 ResKit 已接入 YooAsset，且 Panel Prefab 使用类型名作为可寻址 location 时，在首次 `OpenPanel` 或 `PreloadPanel` 前开启当前默认 loader：

```csharp
UIKit.GetPanelLoader().UseAddressableLocation = true;
```

开启后，`LoginPanel` 会通过 `ResKit.LoadAsset<GameObject>("LoginPanel")` 加载；关闭时仍使用 `Art/UIPrefab/LoginPanel` 这类路径。此开关不切换或初始化 ResKit Provider，也不影响已开始的异步加载、已物化面板和它们持有的 lease。`GetPanelLoader()` 是启动配置入口：Root 尚未创建时会按需创建默认 loader；项目使用 Root Prefab Variant 时必须先调用 `UIKit.SetRootPrefab(...)`。

同类型 Open 与 Preload 共用一次物化 single-flight：

- 多个异步等待者共享底层加载，单个等待者取消不会取消其他等待者。
- 每个成功 Open 仍提交自己的 data、tag、level 和缓存策略。
- 失败、取消、类型不匹配或晚到结果不会遗留临时实例和资源 lease。
- 同类型异步加载进行中时，同步 `OpenPanel<T>()` 不阻塞 Unity 主线程，而是明确拒绝；调用方应等待已有异步入口。

自定义加载器必须为每次成功结果返回独占、幂等释放的 lease：

```csharp
UIKit.SetPanelLoader(new ProjectPanelLoader());
```

`SetPanelLoader` 只影响后续物化；已有实例继续由创建它的 lease 释放。所有自定义 `IPanelLoader` 都需要公开 `UseAddressableLocation`；具有底层资源 location 的实现应在启用时使用 Panel 类型名，直接持有 Prefab 引用的实现则保持自身资源所有权语义。`GetPanelLoader()` 会按需创建 Root；只需观察 Root 是否存在时使用无副作用的 `UIKit.HasRoot`。

### 显式缓存与预加载

| 策略 | 关闭后的行为 |
|---|---|
| `PanelCachePolicy.Reusable` | 默认策略。进入有界 LRU，后续 Open 复用同一实例。 |
| `PanelCachePolicy.Transient` | 立即销毁实例并释放 Prefab lease。 |
| `PanelCachePolicy.Persistent` | 持续保留实例，直到显式卸载或 Root 销毁。 |

预加载只完成物化和一次 `OnInit`：

```csharp
bool loaded = await UIKit.PreloadPanelAsync<InventoryPanel>(
    level: UILevel.Common,
    ct: cancellationToken,
    cachePolicy: PanelCachePolicy.Reusable);

if (loaded && UIKit.IsPanelPreloaded<InventoryPanel>())
{
    UIKit.OpenPanel<InventoryPanel>();
}
```

缓存与卸载入口：

| 入口 | 行为 |
|---|---|
| `IsPanelLoaded<T>()` | 判断是否已有物化实例。 |
| `IsPanelPreloaded<T>()` | 判断实例是否仍是从未打开的预加载项。 |
| `GetLoadedPanelTypes()` / `GetLoadedPanels()` | 返回稳定排序快照，不创建 Root。 |
| `ReusableCacheCapacity` | Reusable LRU 容量，默认 8；调小会立即淘汰超额 inactive 项。 |
| `UnloadPanel<T>()` | 卸载预加载或已关闭保留实例；不会隐式关闭活动面板。 |
| `ClearReusableCache()` | 清空 inactive Reusable 项，不影响 Persistent 或活动面板。 |

### 命名栈

默认栈名是 `UIKit.DEFAULT_STACK`，值为 `main`。也可以使用 1 至 128 字符的业务栈名：

```csharp
var menu = UIKit.PushOpenPanel<MainMenuPanel>(
    level: UILevel.Common,
    stackName: "main");

var settings = UIKit.PushOpenPanel<SettingsPanel>(
    level: UILevel.Pop,
    hidePrevious: true,
    stackName: "main");

IPanel popped = UIKit.PopPanel(
    stackName: "main",
    showPrevious: true,
    autoClose: true);
```

`PushPanel` 只能接收已经 Open 或 Hide 的受管面板。压入新面板时旧栈顶收到 `OnBlur`，按参数隐藏；Pop 或直接关闭栈顶会使用同一恢复路径，让新栈顶依次 Show、`OnResume`、`OnFocus`。

查询与维护入口包括 `PeekPanel`、`GetStackDepth`、`GetAllStackNames`、`IsInStack`、`GetPanelStackName` 和 `ClearStack`。这些查询不会创建 Root。

### 层级与模态

预定义层级按排序值从低到高为 `AlwayBottom`、`Bg`、`Hud`、`Common`、`Toast`、`Pop`、`Guide`、`AlwayTop`、`CanvasPanel`。`default(UILevel)` 等于 `UILevel.Common`；也可以通过 `new UILevel(order)` 定义业务层级。

```csharp
UIKit.SetPanelLevel(panel, UILevel.Pop, subLevel: 10);
UIKit.SetPanelSubLevel(panel, subLevel: 20);
UIKit.SetPanelModal(panel, true);

IPanel top = UIKit.GetGlobalTopPanel();
bool blocked = UIKit.HasModalBlocker();
```

模态 blocker 是与面板同层、紧邻面板下方的全屏半透明 Unity `Image`。只有可见模态面板持有 blocker；隐藏、关闭、移层或销毁时由 owner 统一清理。

### 动画与 Dialog

`FadeAnimation`、`ScaleAnimation`、`SlideAnimation` 与 `CompositeAnimation` 实现统一 `IUIAnimation`，不依赖第三方库。`UIAnimationFactory` 支持配置创建、Fade/Scale/Slide 快捷入口，以及 Parallel/Sequential 组合。`UIPanel` 的显示/隐藏配置使用 `SerializeReference` 保存 `FadeAnimationConfig`、`ScaleAnimationConfig`、`SlideAnimationConfig` 或嵌套 `CompositeAnimationConfig`；Inspector 类型菜单可直接选择和编辑曲线、时长与参数。

通过 Inspector 配置或 `UIPanel.SetShowAnimation` / `SetHideAnimation` 设置后，UIKit 会在 Show/Hide 流程中播放动画；反向操作、Close 或 Root 销毁会停止未完成的动画。

```csharp
panel.SetShowAnimation(new FadeAnimation(0.2f, 0f, 1f));
panel.SetHideAnimation(UIAnimationFactory.CreateParallel()
    .Add(new FadeAnimation(0.15f, 1f, 0f))
    .Add(new ScaleAnimation(0.15f, Vector3.one, new Vector3(0.95f, 0.95f, 1f))));
```

安装 DOTween 并启用 `YOKIFRAME_DOTWEEN_SUPPORT` 后即可使用 DOTween 动画扩展；未安装时公开 Panel 动画入口保持不变。

Dialog 使用 `UIDialogPanel` 派生 Prefab、默认类型注册和串行模态队列：

```csharp
UIKit.SetDefaultDialogType<ProjectDialogPanel>();
UIKit.SetDefaultPromptType<ProjectPromptPanel>();

bool confirmed = await UIKit.ConfirmAsync("确定删除存档吗？");
var prompt = await UIKit.PromptAsync("输入角色名", defaultValue: "Player");
```

`Alert`、`Confirm`、`Prompt`、`ShowDialog<T>` 和运行时 `Type` 重载均有回调/异步入口。启用 `YOKIFRAME_UNITASK_SUPPORT` 时异步入口返回 `UniTask`，否则返回 `Task`。取消令牌只取消当前等待者；`ClearDialogQueue` 对未显示项提交 Cancel，不强制关闭当前活动 Dialog。

### 焦点、布局与 Runtime 调试

`UIRoot` 初始化时会立即确保 EventSystem 可用：优先复用场景现有实例，其次启用模板内置节点，最后才创建新节点；已有输入模块保持不变，缺少模块时按项目 Active Input Handling 自动补齐。Input System-only 项目由 `YokiFrame.UIKit.InputSystem` 安装 `InputSystemUIInputModule`，Legacy Input Manager 或 Both 使用 `StandaloneInputModule`。`UIKit.EnsureEventSystem()` 可显式取得该实例；`SetFocus`、`ClearFocus`、`SetInputMode`、`RestoreLastFocus`、`CurrentFocus`、`InputMode` 和 `FindFirstSelectable` 直接操作 UIKit 焦点状态。`GamepadNavigator` 接受项目实现的 `IGamepadInput`，提供移动、Submit、Cancel、Tab、Menu、死区和长按重复，不绑定具体输入包。`UIPanel` 可配置 `AutoFocusOnShow` 与默认 Selectable；隐藏时会记住当前子焦点，导航模式重新显示时按“记忆 -> 默认 -> 首个可交互控件”恢复，关闭或销毁后清理记忆。`SelectableGroup`、`UIAutoNavigation`、`UINavigationGrid`、`UIBackHandler`、`UITabGroup`、`UISelectableExtension` 与 `UIFocusHighlight` 提供可组合导航组件。

`SafeAreaAdapter`、`UIDynamicElement` 和 `CanvasBatchHint` 可用于安全区、频繁布局和 Canvas 设置。安装 Unity Input System 后，可以使用对应扩展把项目输入动作映射到导航、确认、取消和 Tab；未安装时仍可调用 UIKit 的基础焦点 API。

`UIKit.CaptureRuntimeDiagnostics()` 和 `UIDebugOverlay` 可在运行时显示面板、栈与焦点摘要；它们只读，不会替业务修改 UI 状态。

### Root Prefab 与项目定制

变更操作按需创建唯一 `UIRoot`。`UIKit.Root` 只返回当前已有 Root，查询本身不会创建。包内 `Resources/UIKit.prefab` 是只读默认模板，结构包含 `UIKit/UIRoot`、`UIKit/EventSystem`、`UIKit/UICamera`：Canvas、CanvasScaler 与 GraphicRaycaster 挂在 `UIRoot` 上，内置 EventSystem 默认禁用，缺少场景 EventSystem 时才启用并补齐输入模块；`UICamera` 是禁用的普通 Camera 占位节点，不携带 URP 组件。模板缺失时的程序化兜底同样使用 `UIKit/UIRoot` 作为稳定根层级，不创建额外的 `[YokiFrame]` 前缀。预加载与关闭缓存使用的 `Storage` 只在运行时挂到 `UIRoot` 下创建。

项目需要定制 Root 时，在项目 `Assets` 下创建包内模板的 Prefab Variant，并在第一次 UIKit 变更调用前显式注册：

```csharp
[SerializeField] private GameObject mUIKitRootPrefab;

private void Awake()
{
    UIKit.SetRootPrefab(mUIKitRootPrefab);
}
```

创建顺序固定为“场景中已有 Root -> `SetRootPrefab` 显式 Prefab -> 包内默认模板 -> 模板缺失时的最小动态兜底”。Root 与 Panel 实例都会恢复各自 Prefab 资产名，不保留 Unity 自动追加的 `(Clone)`。显式 Prefab 必须包含且只能包含一个 `UIRoot`，Root 创建后再次设置会抛出 `InvalidOperationException`。显式选择在 `UIRoot.Dispose()` 后继续生效，一次应用生命周期只需配置一次。

Canvas、CanvasScaler、GraphicRaycaster 和项目附加组件直接在 Prefab Variant 中配置。Root 还可以配置 Prefab 路径前缀、是否使用可寻址位置和 Reusable 缓存容量；运行时通过 `UIKit.GetPanelLoader()` 调整的值只影响后续加载。

包内默认模板的 Canvas 是 `Screen Space - Overlay`；项目若需要由相机拍摄，应在首次 UIKit 变更前或首次打开面板前绑定一个**已启用**的场景 Camera：

```csharp
[SerializeField] private Camera mUiCamera;

private void Awake()
{
    UIKit.BindRootCamera(mUiCamera);
}
```

`BindRootCamera` / `UIRoot.BindWorldCamera` 会把 Root Canvas 切换为 `Screen Space - Camera` 并设置 `worldCamera`。此模式下必须确认 Canvas 的 `Plane Distance` 小于该 Camera 的 `Far Clip Plane`（保留足够余量）；否则 UI 位于远裁剪面外，Camera 不会渲染它。还应确认该 Camera 的 Culling Mask 包含 `UI` Layer；使用 URP 时，需要按项目的 Camera Stack 配置让该 Camera 参与最终输出。包内 `UICamera` 占位节点默认禁用，项目要使用它时应在 Root Prefab Variant 中显式启用并完成上述绑定。

## 使用流程

1. 准备 Panel Prefab 和项目自己的 Prefab Variant。
2. 在首次调用 UIKit 前注册项目 Root Prefab。
3. 用 `UIKit.OpenPanel<TPanel>()`、`UIKit.ClosePanel<TPanel>()` 管理面板生命周期。
4. 需要动画或对话框时，使用 Panel 的公开 API；不要从 Workbench 远程控制 Runtime UI。
5. 需要绑定代码时，在 Unity Inspector 中选择 Panel、Element 或 Component owner，再生成 Designer。

```csharp
PausePanel panel = await UIKit.OpenPanelAsync<PausePanel>();
bool confirmed = await UIKit.ConfirmAsync("是否暂停游戏？");
UIKit.ClosePanel(panel);
```

`OpenPanelAsync` 在未安装 UniTask 时返回 `Task`，启用 `YOKIFRAME_UNITASK_SUPPORT` 后返回 `UniTask`。Panel 类型默认只保留一个实例；关闭、销毁或切换场景前，取消仍在等待的加载和 Dialog。

绑定代码的 owner 分工如下：

| owner | 生成位置 | 用途 |
| --- | --- | --- |
| `UIPanel` | Panel Prefab 根 | 生成 Panel 用户脚本、Designer 和绑定数据 |
| `UIElement` | Element 节点 | 生成 Panel 内部可复用元素 |
| `UIComponent` | Component 节点 | 生成可复用 UI 组件 |

一个节点只挂一个 `Bind`。用户 partial 与 Designer 分开保存，重新生成不会覆盖用户 partial。Designer 文件会带有 `YokiFrame UIKit` 自动生成头部，请勿直接修改其中内容。

## 生命周期与错误边界

- `UIKit.Root`、面板/栈查询和加载状态读取不会创建 Root；`GetPanelLoader`、Open、Preload 和绑定 Camera 等配置或变更操作可以按需创建
- Root 创建后不能替换 Prefab；项目定制必须在首次 UIKit 变更前通过 `UIKit.SetRootPrefab` 完成
- Panel 的加载、single-flight、缓存和关闭由 owner 统一调度；业务不应绕过生命周期直接销毁受管 Panel
- Workbench 只用于查看摘要；面板、Prefab 和绑定代码仍在 Unity Editor 中运行。

## 在工具中查看

Workbench 可以只读查看 Unity Runtime UI 摘要，并在 Unity Editor 中作为 Panel Prefab 创建与 Panel 代码生成的统一入口提供操作表单。它不会远程打开、关闭或清理 Runtime UI；Element 和 Component 的专属生成入口仍在各自 Inspector，Unity 不再提供独立的 Panel 创建菜单或窗口。

## 限制与相关资料

UIKit 的 Runtime 与 Editor API 仅支持 Unity；面板、Root 和代码生成流程见本页“使用流程”。
