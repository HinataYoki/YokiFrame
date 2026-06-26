# UIKit 面板

UIKit 当前是 Unity UI 实现与 `IUIBackend` 兼容层并存的面板系统。业务代码在 `YokiFrame` 命名空间中调用 `UIKit` 静态入口打开、显示、隐藏、关闭和压栈面板；Unity 的 GameObject / Canvas / DOTween 细节仍在 UIKit runtime 中，后续跨引擎拆分时应先把纯契约和宿主实现分离。

## 核心类型

| 类型 | 说明 |
|---|---|
| `UIKit` | 静态统一入口，负责后端安装、面板缓存、显示状态和面板栈。 |
| `IUIBackend` | 宿主 UI 后端接口，提供 `OpenPanel()`、`Show()`、`Hide()` 和 `Close()`。 |
| `IPanel` | 面板抽象，暴露 `PanelName`、`Level`、`State`、`Tag` 和 `Data`。 |
| `UIOpenRequest` | 打开面板时传给后端的请求数据。 |
| `UILevel` | 跨引擎层级值，内置 `Bg`、`Common`、`Pop`、`Guide`、`AlwayTop` 等。 |
| `PanelState` | 面板状态：`Open`、`Hide`、`Close`。 |

## 初始化与配置

| 属性/方法 | 说明 |
|-----------|------|
| `IsInitialized` | 是否已初始化。 |
| `SetBackend(backend)` | 设置 UI 后端。 |
| `GetBackend()` | 获取当前后端。 |
| `ClearBackend()` | 清除后端。 |
| `Reset()` | 重置 UIKit 状态。 |

### 缓存配置

| 属性 | 说明 |
|------|------|
| `HotCacheEnabled` | 是否启用热缓存。 |
| `OpenHot` | 打开面板时的热度。 |
| `GetHot` | 获取面板时的热度。 |
| `Weaken` | 面板衰减热度。 |

## 快速使用

Unity Adapter 或项目启动代码先安装后端：

```csharp
using YokiFrame;

UIKit.SetBackend(myUiBackend);
```

业务侧只依赖统一静态入口：

```csharp
var menu = UIKit.OpenPanel<MenuPanel>(
    UILevel.Common,
    data: null,
    tag: "main");

UIKit.HidePanel(menu);
UIKit.ShowPanel(menu);
UIKit.ClosePanel<MenuPanel>();
```

面板栈用于菜单、弹窗、引导层等互斥流程：

```csharp
var menu = UIKit.OpenPanel<MenuPanel>(UILevel.Common);
UIKit.PushPanel(menu, "Main", hidePreLevel: true);

var settings = UIKit.PushOpenPanel<SettingsPanel>(UILevel.Pop);
UIKit.PopPanel(showPreLevel: true, autoClose: true);
```

## 生成面板 Data

2.0 中 `UIPanel` 不再暴露会和生成代码冲突的公开 `Data` 属性，而是显式实现 `IPanel.Data`，用于框架内部保存打开面板时传入的 `IUIData`。UIKit 生成器仍会在 `*.Designer.cs` 中生成面板自己的强类型 `Data` 属性，例如 `LoginPanelData Data`；业务面板直接使用这个强类型属性即可。

```csharp
public partial class LoginPanel : UIPanel
{
    protected override void OnInit(IUIData uiData = null)
    {
        // 生成代码会维护 mData 和强类型 Data。
        mData = uiData as LoginPanelData ?? new LoginPanelData();
    }

    protected override void OnOpen(IUIData uiData = null)
    {
        var loginData = Data;
    }
}
```

需要从通用面板引用读取原始 `IUIData` 时，使用 `((IPanel)panel).Data`。不要在 `UIPanel` 基类上重新加公开 `Data`，否则旧生成代码里的强类型 `Data` 会再次出现 CS0108 隐藏警告。

## 面板操作 API

### 打开面板

```csharp
// 同步打开
var panel = UIKit.OpenPanel<MenuPanel>(UILevel.Common, data: null, tag: "main");

// 异步打开（Task/UniTask 风格）
var panel = await UIKit.OpenPanelAsync<MenuPanel>(UILevel.Common, data: null, token: cts.Token);
```

### 获取面板

```csharp
var panel = UIKit.GetPanel<MenuPanel>();
```

### 显示与隐藏

```csharp
UIKit.ShowPanel<MenuPanel>();
UIKit.HidePanel<MenuPanel>();
UIKit.HideAllPanel();
```

### 关闭面板

```csharp
UIKit.ClosePanel<MenuPanel>();
UIKit.ClosePanel(panel);
UIKit.CloseAllPanel();
UIKit.CloseAllPanels();
UIKit.ClosePanelsByTag("main");
```

## 面板缓存

| 方法 | 说明 |
|------|------|
| `IsPanelCached<T>()` | 面板是否已缓存。 |
| `IsPanelCached(type)` | 面板是否已缓存。 |
| `IsPanelPreloaded<T>()` | 面板是否已预加载。 |
| `IsPanelPreloaded(type)` | 面板是否已预加载。 |
| `GetCachedPanelTypes()` | 获取已缓存面板类型列表。 |
| `GetCachedPanels()` | 获取已缓存面板列表。 |
| `GetCacheCapacity()` | 获取缓存容量。 |
| `SetCacheCapacity(capacity)` | 设置缓存容量。 |
| `PreloadPanelAsync<T>(level, token)` | 异步预加载面板（Task/UniTask 风格）。 |
| `ClearPreloadedCache<T>()` | 清除指定面板的预加载缓存。 |
| `ClearAllPreloadedCache()` | 清除所有预加载缓存。 |

## 面板栈

| 方法 | 说明 |
|------|------|
| `PushPanel(panel, hidePreLevel)` | 将面板压入默认栈。 |
| `PushPanel(panel, stackName, hidePreLevel)` | 将面板压入指定栈。 |
| `PushOpenPanel<T>(level, data, hidePreLevel)` | 打开并压入默认栈。 |
| `PushOpenPanelAsync<T>(level, data, hidePreLevel, token)` | 异步打开并压入（Task/UniTask 风格）。 |
| `PopPanel(showPreLevel, autoClose)` | 从默认栈弹出。 |
| `PopPanel(stackName, showPreLevel, autoClose)` | 从指定栈弹出。 |
| `PopPanelAsync(stackName, showPreLevel, autoClose, token)` | 异步弹出。 |
| `PeekPanel(stackName)` | 查看栈顶面板。 |
| `GetStackDepth(stackName)` | 获取栈深度。 |
| `GetAllStackNames()` | 获取所有栈名称。 |
| `IsInStack(panel)` | 面板是否在栈中。 |
| `GetPanelStackName(panel)` | 获取面板所在栈名称。 |
| `ClearStack(stackName, closeAll)` | 清空指定栈。 |

## 面板层级

| 方法 | 说明 |
|------|------|
| `SetPanelLevel(panel, level, subLevel)` | 设置面板层级。 |
| `SetPanelSubLevel(panel, subLevel)` | 设置面板子层级。 |
| `GetTopPanelAtLevel(level)` | 获取指定层级的顶部面板。 |
| `GetGlobalTopPanel()` | 获取全局顶部面板。 |
| `GetPanelsAtLevel(level)` | 获取指定层级的所有面板。 |
| `SetPanelModal(panel, isModal)` | 设置面板模态状态。 |
| `HasModalBlocker()` | 是否有模态阻断器。 |

## 对话框系统

### 设置默认类型

```csharp
UIKit.SetDefaultDialogType<MyDialogPanel>();
UIKit.SetDefaultPromptType<MyPromptPanel>();
```

### 便捷对话框

```csharp
// 警告框
UIKit.Alert("操作完成", "提示", () => { Debug.Log("closed"); });

// 确认框
UIKit.Confirm("确定删除？", "确认", confirmed =>
{
    if (confirmed) Delete();
});

// 输入框
UIKit.Prompt("请输入名称", "重命名", "默认值", (ok, value) =>
{
    if (ok) Rename(value);
});
```

### 异步便捷对话框

```csharp
await UIKit.AlertAsync("操作完成", "提示", cts.Token);
bool confirmed = await UIKit.ConfirmAsync("确定删除？", "确认", cts.Token);
(bool ok, string value) = await UIKit.PromptAsync("请输入名称", "重命名", "默认值", cts.Token);
```

### 通用对话框

```csharp
var config = new DialogConfig
{
    Title = "自定义对话框",
    Message = "消息内容"
};

UIKit.ShowDialog(config, result =>
{
    Debug.Log($"Result: {result}");
});

// 异步版本
var result = await UIKit.ShowDialogAsync(config, cts.Token);
```

### 对话框状态

| 属性/方法 | 说明 |
|-----------|------|
| `HasActiveDialog` | 是否有活跃对话框。 |
| `DialogQueueCount` | 对话框队列数量。 |
| `ClearDialogQueue()` | 清空对话框队列。 |

## 焦点系统

| 属性/方法 | 说明 |
|-----------|------|
| `FocusSystemEnabled` | 焦点系统是否启用。 |
| `GetInputMode()` | 获取当前输入模式。 |
| `SetFocus(gameObject)` | 设置焦点到 GameObject。 |
| `SetFocus(selectable)` | 设置焦点到 Selectable。 |
| `ClearFocus()` | 清除焦点。 |
| `GetCurrentFocus()` | 获取当前焦点对象。 |

## 面板加载器

```csharp
UIKit.SetPanelLoader(new MyPanelLoaderPool());
```

默认加载池通过 `ResKit.LoadAsset<GameObject>()` 加载面板。默认路径是 `Art/UIPrefab/<PanelName>`；如果当前 ResKit Provider 是 YooAsset 且面板使用类型名作为可寻址 location，可开启可寻址模式：

```csharp
ResKit.SetProvider(new YooAssetResourceProvider());
UIKit.GetPanelLoader().UseAddressableLocation = true;

// 如果还没有创建 UIKit 当前加载池，也可以先设置新建默认池的全局默认值
DefaultPanelLoaderPool.DefaultUseAddressableLocation = true;
```

## 命令桥

UIKit 已接入文件命令桥。AI、Tauri 和脚本优先使用 engine-scoped v2 路径：

```json
{
  "protocolVersion": 2,
  "engineId": "unity-editor",
  "source": "codex",
  "createdAtUtc": "2026-06-23T12:00:00Z",
  "requestId": "codex-ui-001",
  "kit": "UIKit",
  "action": "get_workbench_snapshot",
  "payload": {}
}
```

| action | payload | 说明 |
|---|---|---|
| `get_workbench_snapshot` | `{}` | 返回 `stats`、`panels` 和 `stacks`。 |
| `stats` | `{}` | 返回后端、面板数量、打开/隐藏/关闭数量和栈统计。 |
| `list_panels` | `{}` | 返回当前面板缓存和栈内面板列表。 |
| `list_stacks` | `{}` | 返回当前面板栈、深度、顶部面板和栈内顺序。 |

命令桥是只读诊断入口，不提供打开、关闭、切换或创建面板的命令。真实 UI 行为仍由业务代码调用 `UIKit` 静态入口，并由当前宿主后端执行。

## Tauri 工作台

UIKit 页面读取顺序为：

1. `read_telemetry("UIKit", "state")`
2. `read_snapshot("UIKit", "state")`
3. `send_command("UIKit", "get_workbench_snapshot")`

Unity `KitStateSnapshotPublisher` 通过可选 handler 发布 `UIKit/state`。Godot 接入需要先提供独立 `IUIBackend` 和 snapshot publisher 后再声明完整支持。页面只在缺少 telemetry/snapshot 或用户点击刷新时走命令桥，避免把 UI 热路径变成跨进程控制流。

## AI 诊断入口

AI 默认优先读取：

```text
.yokiframe/engines/<engineId>/snapshots/UIKit/state.json
```

snapshot 缺失、过期或需要显式刷新时，再发送 `UIKit/get_workbench_snapshot`、`UIKit/stats`、`UIKit/list_panels` 或 `UIKit/list_stacks`。面板打开、关闭和栈切换不通过 `.yokiframe` 执行。
