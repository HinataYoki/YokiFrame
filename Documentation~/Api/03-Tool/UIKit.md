# UIKit 面板管理

> 面向读者：需要管理 Unity Panel、Prefab、焦点、动画或 Editor 绑定生成的开发者
>
> 主要入口：`UIKit`、`UIPanel`
>
> 运行边界：Unity 专属 Runtime/Editor；不提供 Godot 后端或跨宿主 UI 契约
>
> 状态来源：`Documentation~/Api/00-GettingStarted/Kit_Status.md`

## 适用场景

UIKit 管理 Unity UI 的 Panel 实例、Prefab 资源租约、生命周期、显式缓存、命名栈、显示层级和模态阻断。它直接使用 `GameObject`、`Canvas`、`RectTransform` 和 Unity UI，因此不是跨引擎 Kit。

- 只支持 Unity 2022.3+，全部 Runtime 位于 `Tools/UIKit/Adapters/Unity/Runtime`。
- 不提供 Godot Adapter、Godot capability 或 Godot 投影入口。
- 不存在 `IUIBackend`、`UIKit.SetBackend()` 或默认后端工厂。
- 不存在 `OpenHot`、`GetHot`、`Weaken` 或热度缓存。
- 每个 `UIPanel` 具体类型当前最多物化一个实例。

## 入口与当前状态

Unity 专属 Runtime、基础动画、Dialog、焦点/导航、安全区、Canvas 优化、Runtime 调试、Unity Editor Interaction、Avalonia Runtime 诊断页和 Editor Tools 均已实现；DOTween 与 Unity Input System 通过独立可选 Integration 接入。

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

通过 Inspector 配置或 `UIPanel.SetShowAnimation` / `SetHideAnimation` 设置后，UIKit 会在 Show/Hide 状态机中进入 `Opening` / `Hiding`，完成回调只在 transition generation 仍有效时提交终态；反向操作、Close 或 Root 销毁会停止旧动画，晚到回调不能覆盖新状态。

```csharp
panel.SetShowAnimation(new FadeAnimation(0.2f, 0f, 1f));
panel.SetHideAnimation(UIAnimationFactory.CreateParallel()
    .Add(new FadeAnimation(0.15f, 1f, 0f))
    .Add(new ScaleAnimation(0.15f, Vector3.one, new Vector3(0.95f, 0.95f, 1f))));
```

安装 DOTween 并启用 `YOKIFRAME_DOTWEEN_SUPPORT` 后，可选程序集 `YokiFrame.UIKit.DOTween` 提供 `DOTweenFadeAnimation`、`DOTweenScaleAnimation`、`DOTweenSlideAnimation`。无 DOTween 时公开 Panel 动画入口保持不变。

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

`SafeAreaAdapter` 响应 `ScreenInfo` 尺寸/方向变化；`UIDynamicElement` 用嵌套 Canvas 隔离频繁 rebuild；`CanvasBatchHint` 集中设置像素对齐、排序覆盖和 Raycaster。安装 Unity Input System 后，独立 `YokiFrame.UIKit.InputSystem` 程序集同时负责 Input System-only 的 EventSystem 模块安装；其中的 `UIKitInputSystemNavigator` 将项目提供的 `InputActionReference` 映射到 Navigate、Submit、Cancel 和 Tab，并按 `GamepadConfig` 处理死区、方向切换、首次重复延迟、持续重复、Pointer/Navigation 自动检测和光标显隐。

`UIKit.CaptureRuntimeDiagnostics()` 返回 Player 可用的只读快照，`UIDebugOverlay.Show/Hide/Toggle` 显示面板、栈与焦点摘要；它们不编译 Editor Interaction 或远程 Runtime mutation。

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

Canvas、CanvasScaler、GraphicRaycaster 和项目附加组件直接在 Prefab Variant 中配置。`UIRoot` 使用 InspectorKit 专用 Inspector 展示 Root 概览、面板加载和缓存策略，并公开三个序列化字段：`Prefab Path Prefix` 默认 `Art/UIPrefab`，`Use Addressable Location` 默认关闭，`Reusable Cache Capacity` 默认 `8`。它们用于初始化当前默认 loader；运行时设置 `UIKit.GetPanelLoader().UseAddressableLocation` 只覆盖当前 Root 的后续加载，不会回写 Prefab 或 `runtime-settings.json`，Workbench 也不提供 Root Settings 页面。

包内默认模板的 Canvas 是 `Screen Space - Overlay`；项目若需要由相机拍摄，应在首次 UIKit 变更前或首次打开面板前绑定一个**已启用**的场景 Camera：

```csharp
[SerializeField] private Camera mUiCamera;

private void Awake()
{
    UIKit.BindRootCamera(mUiCamera);
}
```

`BindRootCamera` / `UIRoot.BindWorldCamera` 会把 Root Canvas 切换为 `Screen Space - Camera` 并设置 `worldCamera`。此模式下必须确认 Canvas 的 `Plane Distance` 小于该 Camera 的 `Far Clip Plane`（保留足够余量）；否则 UI 位于远裁剪面外，Camera 不会渲染它。还应确认该 Camera 的 Culling Mask 包含 `UI` Layer；使用 URP 时，需要按项目的 Camera Stack 配置让该 Camera 参与最终输出。包内 `UICamera` 占位节点默认禁用，项目要使用它时应在 Root Prefab Variant 中显式启用并完成上述绑定。

## 生命周期与错误边界

- `UIKit.Root`、面板/栈等查询、加载状态和 Interaction 读取不会创建 Root；`UIKit.GetPanelLoader()`、`SetPanelLoader`、Open、Preload 和绑定 Camera 等启动配置或变更操作可以按需创建
- Root 创建后不能替换 Prefab；项目定制必须在首次 UIKit 变更前通过 `UIKit.SetRootPrefab` 完成
- Panel 的加载、single-flight、缓存和关闭由 owner 统一调度；业务不应绕过生命周期直接销毁受管 Panel
- Editor Provider、Telemetry、Command、生成和 Prefab 回填不进入 Unity Player 或 Godot

## 宿主与工具入口

### Interaction 与 Workbench 状态

Unity Editor 的 Provider 发布 `UIKit/state`，`schemaVersion=1`，包含三个 ReadOnly action 和四个 Unity Editor UserAction。UIKit 的 Selection、资产路径和 Editor 模式字段统一消费 Core Unity Editor Context Provider；公共 Provider 发布 `UnityEditor/state`，并通过 `UnityEditor/get_context` 提供 `GlobalObjectId`、Asset GUID、Scene、Prefab Stage 与 Editor revision，查询不会创建 UIRoot：

```powershell
& $YOKI command send --engine unity-editor --kit UIKit --action stats --project <projectRoot>
& $YOKI command send --engine unity-editor --kit UIKit --action get_workbench_snapshot --project <projectRoot>
& $YOKI command send --engine unity-editor --kit UIKit --action get_editor_context --project <projectRoot>
```

周期观察优先读取 `telemetry read --kit UIKit --name state`，失败后回落 snapshot。Provider 查询空状态不会创建 UIRoot；Unity Player 与 Godot 不编译或发布 UIKit Interaction。

UIKit Workbench 页面已加入导航并固定为 Unity 专属，包含 Runtime 与 Editor Tools 两个任务。Runtime 通过 `telemetry -> snapshot` 回落提供 Root、Stats、生命周期、Cache、Modal、Panel 和 Stack 强类型数据；页面支持 Panel/Stack 搜索、选择、详情、诊断复制，并保留 stale、error、empty、offline 和 truncated 证据。宽屏使用三栏 Summary + 列表 + Inspector，紧凑窗口切换为指标摘要 + Panels/Stacks 页签 + 下方详情，列表禁用横向滚动并使用虚拟化。Editor Tools 使用独占窗口的面板创建表单，不再展示 Unity 当前选择卡片、手动保存、添加 Bind 或移除 Bind；生成代码会在提交前自动回读最新 Unity 上下文。

Editor UserAction 只作用于当前 Unity Editor 选择，不是 Runtime UI 远程控制：`create_panel_prefab` 创建 Panel Prefab 与初始代码，`generate_code_for_selection` 只为 Prefab 根上的 `UIPanel` 生成绑定代码，`add_bind_to_selection` / `remove_bind_from_selection` 批量修改 Bind 组件。变更 payload 包含六个生成字段；调用方可以追加 `expectedContextRevision` 与 `targetGlobalObjectId`，上下文过期或目标不再选中时 Unity 会拒绝写入。操作后 Workbench 回读 `get_editor_context`。

Editor Tools 的 Prefab 目录、脚本目录、命名空间、目标程序集和代码模板是项目级 Editor 配置。首次进入页面时优先读取 `UIKitEditorSettingsService` 的已保存值；项目尚未保存时才采用 Unity Provider 返回的默认值。目标程序集选择器消费 `get_editor_context.assemblyNames`：Unity 只回传项目 Assets 中可承载生成脚本的 Player 程序集，`Assembly-CSharp` 固定排在首位；已保存程序集不再存在时页面回退到该默认项。代码模板选择器消费 `get_editor_context.codeTemplateOptions`：`Default`、`Minimal` 显示为“默认”“精简”，项目模板保留 Registry 名称；已保存模板不再存在时页面明确提示并回退到 Provider 默认项。创建预制体或生成代码前，五个字段会通过 `YokiFrameProjectSettingsStore` 自动写入 Unity `EditorProject` 文档的 `UIKit/editor.*` 键，持锁重读并原子替换，保留其它 Kit 条目和 UIKit 未声明键；一次性的 Panel 类型名不持久化。读取、保存或操作失败会显示在表单操作区上方，不再静默回退。

### Bind、Prefab 与代码生成

`Bind`、`AbstractBind`、`UIElement`、`UIComponent` 是 Unity Runtime 序列化组件；扫描器、Inspector、固定 BindType 解析、Prefab 创建、代码生成和编译后回填全部位于 `Tools/UIKit/Adapters/Unity/Editor`，不会进入 Player。

`UIPanel`、`UIElement`、`UIComponent` 与 `Bind` 的自定义 Inspector 统一使用 InspectorKit。三类绑定 owner 共用同一套可折叠绑定树、节点定位、扫描诊断、脚本打开和刷新控件，并分别显示“生成 UIPanel/UIElement/UIComponent 代码”入口；`UIPanel` 额外提供动画与焦点设置，Element/Component 显示具体类型、脚本路径和其它序列化属性。`Bind` 提供类型下拉、Member/Element/Component 快捷转换、只展示已选项的按需组件列表、每个已选组件的独立字段名、生成类型、命名建议、路径、多字段代码预览、层级校验和代码跳转。展开状态通过 InspectorKit 持久化。

`BindType` 的数值固定为 `Member=0`、`Element=1`、`Component=2`、`Leaf=3`。新建 Member 默认选择 `GetComponents<Component>()` 中最后一个非 `AbstractBind` 组件；只有 Transform 时选择 Transform。InspectorKit 的 Member 列表只显示已选组件，点击“添加组件”后才从未绑定的同节点非 Bind 组件中选择；内置 Member 可在一个 `Bind` 中选择多个组件，扫描器按选择顺序展开为多个字段，并使用“节点 PascalCase 名 + 组件短类型名”生成字段名，同类型重复时追加稳定序号。列表至少保留一个目标，所有 Bind 均按固定 `BindType` 语义解析。Element 建立 Panel 内部类型，Component 建立可复用类型，Leaf 跳过当前子树。空引用、跨节点目标、重复组件和重复 Member 字段名阻断生成，重复 Element/Component 类型只保留一个 owner 字段并给出 warning；Component 下禁止 Element。

`UILevel` 使用 InspectorKit 专有 PropertyDrawer：预定义层级通过下拉选择，自定义排序值保留独立整数编辑，并同时提供 IMGUI 回退。`UIPanelValidator.ValidatePanel`、`ValidatePrefab` 与 `ValidateAllPanelsInScene` 只读检查 Bind、引用、Canvas、动画和焦点配置；UIPanel Inspector 的绑定树会显示这些诊断，不会自动修改 Prefab。

快捷入口包括 `YokiFrame/UIKit/Create Panel Prefab`、三个绑定 owner Inspector、`Edit/UIKit/Add Bind Component`（`Alt+B`）和 `Edit/UIKit/Remove Bind Component`（`Alt+Ctrl+B`）。Panel Inspector 生成 Panel 用户脚本、Designer 和嵌套类型；具体 UIElement/UIComponent Inspector 只更新当前已有 partial 类型的 Designer 与内部嵌套类型，组件只需属于某个 Prefab 并位于标准生成目录，支持位于 Panel Prefab 层级内部，回填时按相对 Prefab 根路径定位 owner。添加 Bind 后在 Inspector 的组件列表中按需添加同节点组件；Designer 与编译后 Prefab 回填会为每个已选目标生成并写入独立引用。未保存项目配置时默认 Prefab 输出 `Assets/Resources/Art/UIPrefab`、脚本输出 `Assets/Scripts/UI`、命名空间 `GameUI`；保存后的 `UIKit/editor.*` 配置同时被 Workbench 和 Unity Inspector 生成入口读取。配置用于新生成代码，不会自动重写已有用户 partial；现有 Panel 的类型名、命名空间或程序集与目标配置不一致时生成入口会拒绝写入，项目必须先显式调整用户脚本或项目配置。每个新建用户 partial 和可再生 Designer 文件固定导入 `UnityEngine`、`UnityEngine.UI`、`YokiFrame`，用户 partial 不覆盖，Designer 按文件集事务生成，任一文件失败会回滚本次文件集。Unity 编译完成后由 SessionState 队列按 Panel/Element/Component owner 类型回填引用。

### 项目代码模板

Editor-only `IUIKitCodeTemplate` 提供 `Name`、`Description` 和 `Transform(part, context, generatedSource)`。`Name` 必须符合 SafeId；具有公开无参构造的实现由 `UIKitCodeTemplateRegistry` 通过 TypeCache 自动发现，也可以由项目显式 `Register` / `Unregister`。模板目录固定以 `Default`、`Minimal` 开头，其余项目模板按 ordinal 排序；两个内置模板不能注销。

模板只接收 Panel/Binding 的用户或 Designer 文件角色、稳定字符串上下文和 CodeGenKit 已生成的完整内存源码。它不能写文件、修改 Prefab 或接管编译后回填；空转换结果会阻断生成。用户 partial 仍只在文件不存在时进入转换，Designer 和其它可再生文件仍由现有文件集事务统一提交和回滚，因此项目模板不会绕过用户脚本保护。

## 限制与相关资料

UIKit 的 Runtime 与 Editor API 仅支持 Unity；具体扩展边界见下方“扩展边界”。
