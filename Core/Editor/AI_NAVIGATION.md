# YokiFrame 编辑器系统 - AI 导航索引

> **AI Agent 专用**：本文件是快速定位编辑器功能的总地图，包含完整的目录结构、职责说明和依赖关系

---

## 🎯 快速定位表

| 需求场景 | 目标位置 | 说明 |
|---------|---------|------|
| 查看 Kit 文档 | `Documentation/Core/` 或 `Assets/YokiFrame/Tools/*/Editor/Documentation/` | 文档内容分散在各 Kit，由 `DocumentationModuleRegistry` 自动收集 |
| 打开编辑器窗口 | `ToolsWindow/EntryPoints/YokiToolsMenu.cs` | 快捷键: `Ctrl+E` |
| 添加新页面 | `ToolsWindow/Pages/Kits/` | 使用 `[YokiToolPage]` 特性 |
| 使用 UI 组件 | `UISystem/Components/YokiFrameUIComponents.*.cs` | 20+ 组件文件 |
| 添加样式 | `UISystem/Styling/Kits/{KitName}/` | BEM 命名规范，USS 文件 |
| 查看样式规范 | 本文档「样式系统」章节 | BEM 命名、设计令牌使用 |
| 使用响应式 | `Foundation/Reactive/` | ReactiveProperty, EditorDataBridge |
| 代码生成 | `Foundation/CodeGen/` | CodeGenKit 工具 |
| 编辑器服务 | `UISystem/Services/` | 路径、样式、依赖管理 |

---

## 📁 完整目录结构（按职责分层）

```
Core/Editor/
├── AI_NAVIGATION.md              ← 你在这里（AI 导航索引）
│
├── Foundation/                    ← 基础设施层（最底层，纯工具）
│   ├── Reactive/                  ← 响应式编程基础
│   │   ├── ReactiveProperty.cs        - 响应式属性
│   │   ├── ReactiveCollection.cs      - 响应式集合
│   │   ├── EditorDataBridge.cs        - 数据通道（编辑器专用）
│   │   ├── EditorEventCenter.cs       - 事件中心（编辑器专用）
│   │   ├── EditorPool.cs              - 对象池（编辑器专用）
│   │   ├── Debounce.cs / Throttle.cs  - 防抖/节流
│   │   └── Disposable.cs              - 资源管理
│   │
│   ├── CodeGen/                   ← 代码生成工具
│   │   ├── Code/                      - 代码元素（Using, Comment, etc.）
│   │   ├── CodeAttribute/             - 特性代码
│   │   ├── CodeMember/                - 成员代码（Field, Method, Property）
│   │   ├── CodeScope/                 - 作用域（Class, Namespace）
│   │   ├── CodeWriter/                - 代码写入器
│   │   └── Common/                    - 通用定义（AccessModifier）
│   │
│   └── Utilities/                 ← 通用工具类
│       └── YokiFrameEditorUtility.cs  - 编辑器通用工具
│
├── UISystem/                      ← UI 系统层（服务层）
│   ├── Components/                ← UI 组件库（20+ 组件文件）
│   │   ├── YokiFrameUIComponents.cs           - 主入口
│   │   ├── YokiFrameUIComponents.Cards.cs     - 卡片组件
│   │   ├── YokiFrameUIComponents.Buttons.cs   - 按钮组件
│   │   ├── YokiFrameUIComponents.Inputs.cs    - 输入组件
│   │   ├── YokiFrameUIComponents.Lists.cs     - 列表组件
│   │   ├── YokiFrameUIComponents.Layouts.cs   - 布局组件
│   │   ├── YokiFrameUIComponents.Badges.cs    - 徽章组件
│   │   ├── YokiFrameUIComponents.Tabs.cs      - 标签页组件
│   │   ├── YokiFrameUIComponents.Modals.cs    - 模态框组件
│   │   ├── YokiFrameUIComponents.Tooltips.cs  - 提示组件
│   │   ├── YokiFrameUIComponents.Progress.cs  - 进度组件
│   │   ├── YokiFrameUIComponents.Charts.cs    - 图表组件
│   │   ├── YokiFrameUIComponents.Trees.cs     - 树形组件
│   │   ├── YokiFrameUIComponents.Tables.cs    - 表格组件
│   │   ├── YokiFrameUIComponents.Forms.cs     - 表单组件
│   │   ├── YokiFrameUIComponents.Menus.cs     - 菜单组件
│   │   ├── YokiFrameUIComponents.Panels.cs    - 面板组件
│   │   ├── YokiFrameUIComponents.Alerts.cs    - 警告组件
│   │   ├── YokiFrameUIComponents.Spinners.cs  - 加载组件
│   │   ├── YokiFrameUIComponents.Avatars.cs   - 头像组件
│   │   └── CSharpSyntaxHighlighter.cs         - C# 语法高亮
│   │
│   ├── Styling/                   ← 样式系统（BEM 规范）
│   │   ├── YokiEditorStyleAttribute.cs    - 样式注册特性
│   │   ├── YokiStyleRegistry.cs           - 样式注册表
│   │   ├── Tokens/                        - 设计令牌（CSS 变量）
│   │   │   └── YokiTokens.uss                 - 颜色/间距/圆角/字体
│   │   ├── Core/                          - 核心样式
│   │   │   └── YokiCoreComponents.uss         - 基础组件样式
│   │   ├── Shell/                         - 窗口壳样式
│   │   │   └── YokiWindowShell.uss            - 窗口布局样式
│   │   └── Kits/                          - Kit 专用样式（8 个 Kits）
│   │       ├── ActionKit/ActionKit.uss        - 30+ BEM 类
│   │       ├── AudioKit/AudioKit.uss          - 20+ BEM 类
│   │       ├── UIKit/UIKit.uss                - 80+ BEM 类
│   │       ├── BuffKit/BuffKit.uss
│   │       ├── LocalizationKit/LocalizationKit.uss
│   │       ├── SaveKit/SaveKit.uss
│   │       ├── SceneKit/SceneKit.uss
│   │       └── SpatialKit/SpatialKit.uss
│   │
│   ├── Resources/                 ← 资源文件
│   │   ├── Icons/
│   │   └── Fonts/
│   │
│   └── Services/                  ← UI 服务
│       ├── YokiStyleService.cs            - 样式服务
│       ├── YokiEditorPaths.cs             - 路径服务
│       └── DependencyDefineService.cs     - 依赖管理服务
│
├── Documentation/                 ← 文档注册与 Core 文档内容
│   ├── DocumentationModuleRegistry.cs - 文档模块发现与排序
│   ├── Core/                      ← 核心层文档（10 个 Kit）
│   │   ├── EventKit/              - 事件系统文档
│   │   ├── PoolKit/               - 对象池文档
│   │   ├── FsmKit/                - 状态机文档
│   │   ├── SingletonKit/          - 单例模式文档
│   │   ├── ResKit/                - 资源管理文档
│   │   ├── LogKit/                - 日志系统文档
│   │   ├── Architecture/          - 架构模式文档
│   │   ├── FluentApi/             - 流式 API 文档
│   │   ├── ToolClass/             - 工具类文档
│   │   └── CodeGenKit/            - 代码生成文档
│   │
│
├── Tools/*/Editor/Documentation/  ← 工具层文档内容（按 Kit 分散存放）
│   ├── UIKit/                     - UI 管理文档
│   ├── AudioKit/                  - 音频管理文档
│   ├── ActionKit/                 - 动作序列文档
│   ├── TableKit/                  - 配置表文档
│   ├── SaveKit/                   - 存档系统文档
│   ├── SceneKit/                  - 场景管理文档
│   ├── InputKit/                  - 输入系统文档
│   ├── LocalizationKit/           - 本地化文档
│   ├── BuffKit/                   - Buff 系统文档
│   └── SpatialKit/                - 空间查询文档
│
└── ToolsWindow/                   ← 编辑器窗口（应用层）
    ├── EntryPoints/               ← 菜单入口
    │   └── YokiToolsMenu.cs           - 主菜单（Ctrl+E）
    │
    ├── Windows/                   ← 窗口实现
    │   ├── YokiToolsWindow.cs         - 主窗口
    │   ├── YokiToolsWindow.Sidebar.cs - 侧边栏（partial）
    │   ├── YokiToolsWindow.Content.cs - 内容区（partial）
    │   └── YokiPagePopoutWindow.cs    - 弹出窗口
    │
    ├── Pages/                     ← 页面系统
    │   ├── IYokiToolPage.cs           - 页面接口
    │   ├── YokiToolPageBase.cs        - 页面基类
    │   └── Kits/                      - 各 Kit 的页面
    │       ├── EventKit/
    │       ├── PoolKit/
    │       ├── ResKit/
    │       │   └── ResDebugger.cs     - ResKit 调试器
    │       └── ...
    │
    └── Registry/                  ← 注册中心
        ├── YokiToolPageAttribute.cs   - 页面元数据特性
        └── YokiToolPageRegistry.cs    - 页面注册表
```

---

## 🔗 依赖方向（单向，禁止反向）

```
ToolsWindow ──┐
              ├──→ Documentation ──→ UISystem ──→ Foundation
(应用层)      │    (应用层)         (服务层)     (基础层)
              └────────────────────────────────────────────→
```

### 依赖规则表

| 层级 | 可依赖 | 禁止依赖 | 说明 |
|------|--------|----------|------|
| Foundation | 无 | UISystem, Documentation, ToolsWindow | 纯工具，零依赖 |
| UISystem | Foundation | Documentation, ToolsWindow | 服务层，仅依赖基础层 |
| Documentation | UISystem, Foundation | ToolsWindow | 文档页面，可用 UI 组件 |
| ToolsWindow | Documentation, UISystem, Foundation | 无 | 应用层，可依赖所有下层 |

---

## 🏷️ 命名规则（便于 grep 检索）

| 类型 | 命名模式 | 示例 | 搜索关键词 |
|------|----------|------|-----------|
| 索引文件 | `AI_NAVIGATION.md` | `Core/Editor/AI_NAVIGATION.md` | `AI_NAVIGATION` |
| 基础设施 | `Foundation/*` | `Foundation/Reactive/` | `Foundation` |
| UI 系统 | `UISystem/*` | `UISystem/Components/` | `UISystem` |
| 文档系统 | `Documentation/*` + `Tools/*/Editor/Documentation/*` | `Documentation/Core/EventKit/` | `Documentation` |
| 窗口系统 | `ToolsWindow/*` | `ToolsWindow/Pages/` | `ToolsWindow` |
| 注册中心 | `*Registry*` | `YokiToolPageRegistry.cs` | `Registry` |
| 服务层 | `*Service*` | `YokiStyleService.cs` | `Service` |
| 组件 | `*Components*` | `YokiFrameUIComponents.cs` | `Components` |
| 页面 | `*Page*` | `EventKitToolPage.cs` | `Page` |
| 特性 | `*Attribute*` | `YokiToolPageAttribute.cs` | `Attribute` |

---

## 📐 核心概念

### 1. 响应式编程（Foundation/Reactive）

**编辑器专用工具**（禁止使用运行时 EventKit）：

| 工具 | 用途 | 示例 |
|------|------|------|
| `ReactiveProperty<T>` | 响应式属性 | `var count = new ReactiveProperty<int>(0);` |
| `ReactiveCollection<T>` | 响应式集合 | `var items = new ReactiveCollection<Item>();` |
| `EditorDataBridge` | 数据通道订阅 | `EditorDataBridge.Subscribe<T>("channel", OnData);` |
| `EditorEventCenter` | 类型/枚举事件 | `EditorEventCenter.Register<MyEvent>(this, OnEvent);` |
| `Debounce` / `Throttle` | 防抖/节流 | `var debounce = new Debounce(0.5f);` |

### 2. UI 组件（UISystem/Components）

**20+ 组件类型**：
- Cards, Buttons, Inputs, Lists, Layouts
- Badges, Tabs, Modals, Tooltips, Progress
- Charts, Trees, Tables, Forms, Menus
- Panels, Alerts, Spinners, Avatars

**使用方式**：
```csharp
var card = YokiFrameUIComponents.CreateCard("标题", "内容");
var button = YokiFrameUIComponents.CreatePrimaryButton("点击", OnClick);
```

### 3. 样式系统（UISystem/Styling）

**BEM 命名规范**：
```
Block:    .yoki-{kit}-{block}              → .yoki-pool-card
Element:  .yoki-{kit}-{block}__{element}   → .yoki-pool-card__header
Modifier: .yoki-{kit}-{block}--{modifier}  → .yoki-pool-card--warning
```

**设计令牌（YokiTokens.uss）**：
```css
/* 颜色 */
var(--yoki-brand-primary)      /* 主题色 */
var(--yoki-text-primary)       /* 主文本 */
var(--yoki-layer-card)         /* 卡片背景 */

/* 间距 */
var(--yoki-spacing-xs)         /* 4px */
var(--yoki-spacing-sm)         /* 8px */
var(--yoki-spacing-md)         /* 12px */
var(--yoki-spacing-lg)         /* 16px */

/* 圆角 */
var(--yoki-radius-sm)          /* 4px */
var(--yoki-radius-md)          /* 6px */
var(--yoki-radius-lg)          /* 8px */
```

**样式注册**：
```csharp
[assembly: YokiEditorStyle("EventKit", "Kits/EventKit/EventKit.uss", priority: 100)]
```

**样式优先级**：
- Core 层 Kits: 10-90
- Tools 层 Kits: 100-170
- 数字越小优先级越高

**已完成样式迁移的 Kits**：
- ActionKit (30+ BEM 类)
- AudioKit (20+ BEM 类)
- UIKit (80+ BEM 类)
- BuffKit, LocalizationKit, SaveKit, SceneKit, SpatialKit

**使用方式**：
```csharp
// C# 中添加 BEM 类
element.AddToClassList("yoki-pool-card");
element.AddToClassList("yoki-pool-card--warning");

// 优先使用共享样式类，局部动态状态可接受少量内联样式
// 首选：element.AddToClassList("yoki-pool-card--error");
// 次选：element.style.backgroundColor = new StyleColor(Color.red);
```

### 4. 页面注册（ToolsWindow/Registry）

**页面特性**：
```csharp
[YokiToolPage(
    kit: "EventKit",
    name: "EventKit",
    icon: KitIcons.EVENTKIT,
    priority: 30,
    category: YokiPageCategory.Tool
)]
public class EventKitToolPage : YokiToolPageBase
{
    protected override void BuildUI(VisualElement root) { }
}
```

---

## 🚀 常见任务快速指南

### 添加新的 Kit 页面

1. 创建页面类：`ToolsWindow/Pages/Kits/{KitName}/{KitName}ToolPage.cs`
2. 添加 `[YokiToolPage]` 特性
3. 继承 `YokiToolPageBase`
4. 实现 `BuildUI(root)`，按需重写 `OnActivate()` / `OnDeactivate()`
5. （可选）创建样式：`UISystem/Styling/Kits/{KitName}/{KitName}.uss`
6. （可选）注册样式：`[assembly: YokiEditorStyle("{KitName}", "Kits/{KitName}/{KitName}.uss")]`

### 添加新的文档页面

1. 在对应 Kit 的文档目录创建 `*Doc*.cs`
   Core Kit: `Documentation/Core/{KitName}/`
   Tool Kit: `Assets/YokiFrame/Tools/{KitName}/Editor/Documentation/`
2. 在对应 `*DocData.cs` 中汇总 section
3. 让该 Kit 自己的 `IDocumentationModuleProvider` 返回模块元数据
4. 文档页统一由 `DocumentationToolPage` 渲染，无需为每篇文档单独创建 `[YokiToolPage]`

### 使用响应式数据

```csharp
// 1. 订阅数据通道
protected override void OnActivate()
{
    base.OnActivate();
    Subscriptions.Add(
        EditorDataBridge.Subscribe<List<Data>>(
            "MyChannel",
            OnDataChanged));
}

// 2. 发布数据
EditorDataBridge.Publish("MyChannel", myData);

// 3. 使用响应式属性
var count = new ReactiveProperty<int>(0);
count.Subscribe(value => Debug.Log($"Count: {value}"));
count.Value = 10; // 触发订阅
```

### 添加 UI 组件

1. 在 `UISystem/Components/YokiFrameUIComponents.{Category}.cs` 中添加方法
2. 遵循 BEM 命名规范添加样式类
3. 在 `UISystem/Styling/Core/YokiCoreComponents.uss` 中定义样式
4. 使用 `AddToClassList()` 应用样式

---

## 🔧 路径服务（UISystem/Services/YokiEditorPaths.cs）

**常用路径常量**：
```csharp
// 窗口根路径
YokiEditorPaths.GetEditorToolsRoot()

// 样式根路径
YokiEditorPaths.GetStylingRoot()

// 组合子路径
YokiEditorPaths.CombineWithEditorToolsRoot("Pages/Kits")
```

---

## 📊 统计信息

| 类型 | 数量 | 位置 |
|------|------|------|
| 核心层 Kit | 10 | `Documentation/Core/` |
| 工具层 Kit | 10 | `Assets/YokiFrame/Tools/*/Editor/Documentation/` |
| UI 组件类型 | 20+ | `UISystem/Components/` |
| 样式文件 | 3 层 | `UISystem/Styling/` (Tokens/Core/Kits) |
| 编辑器服务 | 3 | `UISystem/Services/` |
| 响应式工具 | 10+ | `Foundation/Reactive/` |

---

## 🐛 常见问题

### Q: 页面没有出现在窗口中？
A: 检查 `[YokiToolPage]` 特性参数，确保 `kit + name + category` 配置正确，并且类实现了 `IYokiToolPage`（通常继承 `YokiToolPageBase`）

### Q: 样式没有生效？
A: 检查 `[YokiEditorStyle]` 特性，确保 stylePath 相对于 `UISystem/Styling/`

### Q: 如何调试响应式数据？
A: 使用 `EditorDataBridge.Subscribe()` 订阅数据通道，在回调中打印日志

### Q: 编辑器代码能用运行时 EventKit 吗？
A: 禁止！必须使用 `EditorEventCenter` 和 `EditorDataBridge`，避免 PlayMode 污染

---

## 📚 相关文档

- [迁移历史](EDITOR_STRUCTURE_MIGRATION.md) - 重构过程记录
- [YokiFrame 规范](.kiro/steering/yokiframe-guidelines.md) - 框架开发规范
- [Unity 规范](.kiro/steering/unity-general-guidelines.md) - Unity 通用开发规范
- [功能索引](.kiro/steering/yokiframe-index.md) - 功能模块速查表
