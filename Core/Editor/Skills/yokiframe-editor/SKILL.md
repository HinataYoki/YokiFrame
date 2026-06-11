---
name: yokiframe-editor
description: >-
  YokiFrame 编辑器工具开发 Skill。当用户需要在 YokiFrame Tools Panel 中新增编辑器工具页、
  创建 EditorWindow、开发 Unity 编辑器扩展、使用 EditorEventCenter/EditorDataBridge/ReactiveProperty、
  或遵循 YokiFrame 编辑器 UI 规范（UI Toolkit + USS BEM + ToolPage 模式）时触发。
  编辑器代码禁止使用运行时 EventKit，必须使用 EditorEventCenter。
---

# YokiFrame 编辑器开发 Skill

## 首要原则 [MANDATORY]

```
编辑器代码 ≠ 运行时代码
编辑器事件用 EditorEventCenter（非 EventKit）
UI 用 UI Toolkit + USS（非 IMGUI）
数据驱动用响应式订阅（非 OnUpdate 轮询）
```

> **依赖**: 编辑器层无需外部包（UI Toolkit 内置）。若编辑器页面需访问运行时数据，
> 注意运行时的软依赖宏（`YOKIFRAME_UNITASK_SUPPORT` 等），参考 `yokiframe` Skill 的依赖表。

---

## 一、编辑器事件系统

编辑器禁止使用运行时 `EventKit`（PlayMode 切换时事件残留）。全部改用 `EditorEventCenter`。

### EditorEventCenter — 编辑器专用事件

```csharp
using YokiFrame.EditorTools;

// 类型事件
EditorEventCenter.Register<RefreshEvent>(this, OnRefresh);
EditorEventCenter.Send(new RefreshEvent { Data = value });
EditorEventCenter.Unregister<RefreshEvent>(this);

// 枚举事件
EditorEventCenter.Register<EditorEventType, string>(this, EditorEventType.DataChanged, OnDataChanged);
EditorEventCenter.Send(EditorEventType.DataChanged, "new data");
EditorEventCenter.Unregister<EditorEventType>(this);
```

### EditorDataBridge — 数据通道订阅

用于跨页面/跨窗口的数据推送，替代轮询。

```csharp
// 发布方
EditorDataBridge.Publish(DataChannels.CHANNEL_POOL_LIST_CHANGED, poolList);

// 订阅方（在 OnActivate 中订阅，OnDeactivate 自动清理）
protected override void OnActivate()
{
    base.OnActivate();
    Subscriptions.Add(
        EditorDataBridge.Subscribe<List<PoolDebugInfo>>(
            DataChannels.CHANNEL_POOL_LIST_CHANGED,
            OnPoolListChanged));
}
```

### ReactiveProperty<T> / ReactiveCollection<T>

响应式属性 — 值变化自动触发 UI 更新。

```csharp
// 单值
var count = new ReactiveProperty<int>(0);
count.Subscribe(v => label.text = $"Count: {v}");
count.Value = 10;  // UI 自动更新

// 集合
var items = new ReactiveCollection<string>();
items.OnItemAdded += item => AddRow(item);
items.OnItemRemoved += item => RemoveRow(item);
items.Add("new entry");

// 订阅管理
private readonly CompositeDisposable mSubscriptions = new();
mSubscriptions.Add(count.Subscribe(OnCountChanged));
// OnDeactivate: mSubscriptions.Dispose()
```

---

## 二、ToolPage 系统

### 注册页面

通过 `[YokiToolPage]` 特性声明，TypeCache 自动发现，无需手动注册。

```csharp
using YokiFrame.EditorTools;

namespace YokiFrame
{
    [YokiToolPage(
        kit: "MyKit",           // 所属 Kit 名
        name: "我的工具",        // 侧边栏显示名
        icon: KitIcons.CODE,    // 图标 ID
        priority: 50,           // 排序（越小越前）
        category: YokiPageCategory.Tool)]  // Documentation=0 / Tool=1 / System=2
    public partial class MyToolPage : YokiToolPageBase
    {
        protected override void BuildUI(VisualElement root)
        {
            // 构建 UI — 仅调用一次
            var scaffold = CreateKitPageScaffold(
                title: "我的工具",
                summary: "工具描述",
                iconId: KitIcons.CODE);

            scaffold.Toolbar.Add(CreateToolbarPrimaryButton("操作", DoAction));
            scaffold.Toolbar.Add(CreateToolbarButton("刷新", Refresh));

            var (card, body) = CreateCard("数据面板");
            body.Add(new Label("内容..."));
            scaffold.Content.Add(card);

            root.Add(scaffold.Root);
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            Subscriptions.Add(EditorDataBridge.Subscribe<...>(...));
        }

        // OnDeactivate 由基类自动清理 Subscriptions
    }
}
```

### 生命周期

```
BuildUI(VisualElement root)  → 构造 UI 树（仅一次）
OnActivate()                 → 页面激活，订阅数据
OnUpdate()                   → 可选轮询（尽量避免，用响应式替代）
OnDeactivate()               → 页面失活，自动清理 Subscriptions
```

### YokiPageCategory

| 值 | 分组 | 使用场景 |
|----|------|---------|
| `Documentation = 0` | 侧边栏「文档」 | 帮助文档、使用指南 |
| `Tool = 1` | 侧边栏「工具」 | 运行时调试、监控面板 |
| `System = 2` | 侧边栏「系统」 | 框架配置、全局功能 |

### 基类提供的 UI 组件

```csharp
// 页面骨架 — 自带 Hero 头图 + Toolbar + StatusBar + Content 区
var scaffold = CreateKitPageScaffold(title, summary, iconId);
scaffold.Hero       // 头图区
scaffold.Toolbar    // 工具栏区
scaffold.StatusBar  // 状态横幅区
scaffold.Content    // 主工作区

// 工具栏
var toolbar = CreateToolbar();                    // 工具栏容器
CreateToolbarPrimaryButton("主操作", onClick);    // 高亮按钮
CreateToolbarButton("次操作", onClick);            // 普通按钮
CreateToolbarSpacer();                           // 弹性间距
CreateToolbarToggle("开关", value, onChanged);    // 切换开关

// 卡片/面板
var (card, body) = CreateCard("标题");           // 标准卡片
var (card, valueLabel) = CreateKitMetricCard("标题", "数值", "提示");  // 指标卡片
var (panel, body) = CreateKitSectionPanel("标题", "描述");             // 分区面板
var banner = CreateKitStatusBanner("标题", "消息", HelpBoxType.Info); // 状态横幅
var strip = CreateKitMetricStrip();              // 指标条容器

// 带图标按钮
CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", onClick);
CreateActionButtonWithIcon(KitIcons.DELETE, "删除", onClick, isDanger: true);
```

### KitIcons 常用图标常量

```
CATEGORY_CORE, CATEGORY_TOOLS, EVENTKIT, POOLKIT, FSMKIT, SINGLETON, RESKIT, KITLOGGER,
ARCHITECTURE, FLUENTAPI, TOOLCLASS, UIKIT, AUDIOKIT, ACTIONKIT, SAVEKIT, SCENEKIT,
BUFFKIT, SPATIALKIT, INPUTKIT, LOCALIZATIONKIT, TABLEKIT
SETTINGS, CODE, DOCUMENT, FOLDER, REFRESH, COPY, DELETE, PLAY, PAUSE, STOP,
SUCCESS, WARNING, ERROR, INFO, SEND, RECEIVE, CLOCK, CHECK, GAMEPAD, KEYBOARD, TOUCH
```

---

## 三、独立 EditorWindow

如果页面需要独立窗口（不在 Tools Panel 内），继承 `YokiMonitorWindowBase`：

```csharp
public class MyMonitorWindow : YokiMonitorWindowBase
{
    protected override float RefreshIntervalSeconds => 1f;  // 仅 PlayMode 刷新
    protected override string MonitorKitName => "MyKit";

    protected override void BuildMonitorUI(VisualElement root)
    {
        root.Add(new Label("监控内容"));
    }

    protected override void RefreshMonitorData() { /* 定时刷新逻辑 */ }
    protected override void OnMonitorEnabled() { /* 窗口打开 */ }
    protected override void OnMonitorDisabled() { /* 窗口关闭 */ }
}

// 菜单入口
[MenuItem("YokiFrame/MyKit/Monitor")]
public static void Open() => GetWindow<MyMonitorWindow>("MyKit Monitor");
```

简单 EditorWindow（不继承基类）直接使用 `EditorWindow.GetWindow<T>()` + `CreateGUI()`。

---

## 四、USS 样式规范 (BEM)

```
.yoki-{kit}-{block}              → .yoki-pool-card
.yoki-{kit}-{block}__{element}   → .yoki-pool-card__header
.yoki-{kit}-{block}--{modifier}  → .yoki-pool-card--warning
```

文件位置: `Core/Editor/UISystem/Styling/Kits/{KitName}/{KitName}.uss`

UNITY_EDITOR 宏包裹所有编辑器代码。

---

## 五、禁止模式清单

| 禁止 | 替代 | 原因 |
|------|------|------|
| 编辑器用 `EventKit.Type` / `EventKit.Enum` | `EditorEventCenter` | PlayMode 切换事件残留 |
| `OnUpdate()` 轮询刷新 UI | `EditorDataBridge.Subscribe()` / `ReactiveProperty` | 性能浪费 |
| `style.xxx = new StyleColor(...)` | `AddToClassList("class-name")` | USS 样式类复用 |
| `Q<T>()` 每次调用 | `QueryCached<T>()` | 缓存查询结果 |
| IMGUI (`OnGUI`) | UI Toolkit (`CreateGUI` / `BuildUI`) | YF3 规范 |
| 硬编码色值/字号 | USS 样式类 | YF4 声明式分离 |
| Emoji 字符 | `KitIcons` 图标常量 | YF6 禁用 Emoji |

---

## 六、目录结构约定

```
Core/Editor/ToolsWindow/Pages/Kits/{KitName}/
├── Page/
│   ├── {KitName}ToolPage.cs        ← 主入口（partial class）
│   ├── {KitName}ToolPage.UI.cs     ← UI 构建细节（可选）
│   └── {KitName}ViewModel.cs      ← 响应式 ViewModel（可选）
├── Bridge/
│   └── {KitName}EditorChannelProvider.cs  ← 数据通道发布
└── Diagnostics/
    └── {KitName}Debugger.cs        ← 运行时数据采集

Core/Editor/UISystem/Styling/Kits/{KitName}/
└── {KitName}.uss                   ← Kit 独用样式
```
