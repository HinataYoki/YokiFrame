## 扩展边界

> 本文面向已经掌握 [UIKit 主页面](../Api/03-Tool/UIKit.md) 的开发者。
>
> 当前 API 以 [UIKit 面板管理](../Api/03-Tool/UIKit.md) 和公开源码为准。

## 当前支持范围

- `UIKit` 提供 Unity Runtime 与 Unity Editor API。
- 动画、Dialog、焦点与输入是本地 Runtime API；Workbench 只提供声明的只读观察和 Editor UserAction。
- Root 配置来自包内模板或项目显式注册的 Prefab Variant，不进入 Runtime command 或 Workbench 设置 Store。
- 默认 Root Canvas 使用 `Screen Space - Overlay`；需要相机渲染时在首次 UIKit 变更前调用 `UIKit.BindRootCamera`，并保证目标 Camera 已启用、包含 `UI` Layer，且其 Far Clip Plane 大于 Canvas 的 Plane Distance。
- 默认 `ResKitPanelLoader` 始终经由 ResKit 加载 Panel；当当前 Provider 使用类型名可寻址 location 时，可在首次物化前通过 `UIKit.GetPanelLoader().UseAddressableLocation = true` 切换资源 key。

## 动画与对话框

基础动画接入当前 transition generation。反向 Show/Hide/Close 会使前一次转换失效，晚到回调不会回写新一轮状态；内置动画不会让业务生命周期重复执行。

DOTween 位于 `Tools/UIKit/Integrations/Unity/DOTween/Runtime` 的独立可选程序集，并由 `YOKIFRAME_DOTWEEN_SUPPORT` 启用。无 DOTween 时，UIKit 仍提供一致的公开使用方式，不把 DOTween 类型泄漏到无条件 Runtime 契约。

Dialog 提供可取消等待、Alert/Confirm/Prompt、显式类型、默认类型注册和模态串行队列。Dialog Prefab 的具体视觉和字段绑定由项目自己的 `UIDialogPanel` 派生类承担。

## 焦点、安全区与输入

当前 Unity 输入切片提供：

- Root 初始化时复用或创建 EventSystem；缺少输入模块时，Input System-only 自动安装 `InputSystemUIInputModule`，Legacy/Both 自动安装 `StandaloneInputModule`。
- 默认 Selectable、焦点恢复和 Pointer/Navigation 模式切换。
- Safe Area 与屏幕变化更新。
- Unity Input System 的键盘、手柄和导航接入。

Unity Input System 接入位于 `Tools/UIKit/Integrations/Unity/InputSystem/Runtime` 的独立可选程序集；它只在 Input System-only 时注册 EventSystem 模块工厂，Both 保留 Legacy UI 模块。无 Input System 时，项目仍可直接调用 `SetFocus`、`ClearFocus`、`UIBackHandler.ExecuteBack` 和 Tab 切换入口。

## Workbench

当前 Unity Editor Interaction 发布 `UIKit/state`，ReadOnly `stats`、`get_workbench_snapshot`、`get_editor_context`，以及 UserAction `create_panel_prefab`、`generate_code_for_selection`、`add_bind_to_selection`、`remove_bind_from_selection`。只读观察不创建 UIRoot，四个写操作只修改 Unity Editor 资产或当前选择，不允许远程修改运行时 UI。

UIKit 已完成 `YokiFrame.Tooling.Application` 强类型 read model、身份校验、`telemetry -> snapshot` 回落和真实 Avalonia 只读页面，已加入 `yokiframe-workbench` Skill 与 Workbench 导航。页面展示 Root、生命周期、Cache、Modal、Panel、Stack 和截断覆盖率，支持搜索、选择、详情和诊断复制；宽屏为三栏主从布局，紧凑窗口为摘要 + 页签 + 详情。页面不提供远程 Open、Close、Hide、Show、栈修改或缓存清理，也不能替业务代码替代这些动作。

Editor Tools 提供 Panel Prefab、Bind、Panel-only 代码生成和当前 Unity 选择上下文；具体 UIElement/UIComponent 只从各自 Unity Inspector 生成。Prefab 目录、脚本目录、命名空间、程序集和代码模板保存到项目级 `UIKit/editor.*` 配置后，Workbench 与 Unity Inspector 生成入口共同读取同一份配置。已有用户 Panel 不因配置变化自动改写；类型身份不一致时拒绝更新 Designer，避免破坏 partial。代码模板选项由 Unity Registry 动态发布，内置项本地化显示，项目模板原名显示和持久化。项目通过包内模板的 Prefab Variant 与 `UIKit.SetRootPrefab` 管理 Root 配置。

## Bind 编辑边界

`Bind` 使用 `[DisallowMultipleComponent]`，一个节点只挂一个 Bind。内置 Member 通过 InspectorKit 按需对象列表选择同一 GameObject 上的多个组件，默认只展示已选项，点击添加后才打开未绑定候选；扫描、Designer 和编译后 Prefab 回填把它们展开为多个字段。新建 Bind 默认使用组件列表中最后一个非 Bind 组件。

所有 Bind 按固定 `BindType` 语义解析。`Member` 绑定节点组件，`Element` 建立 Panel 内部类型，`Component` 建立可复用类型，`Leaf` 跳过当前子树。

## Owner 生成边界

`UIPanel`、`UIElement`、`UIComponent` 共用 InspectorKit 绑定树，但生成所有权严格分离。Panel Inspector 可以创建 Panel 用户脚本、Panel Designer 和嵌套类型；生成的 Panel Designer 直接公开具体 `PanelData` 类型的 `Data`，不添加 `new`，框架侧 `UIPanel` 只通过 `IPanel` 显式实现通用数据属性。Element/Component Inspector 只为当前已经存在的具体 partial 类型更新同目录 Designer，并按自身 owner 类型规则校验内部 Bind。编译后队列保存 owner kind、完整类型名和程序集，Domain Reload 后保持相同 owner 类型。

Workbench `generate_code_for_selection` 只生成 Prefab 根上的 UIPanel；Element/Component 必须回到对应 Unity Inspector，既支持独立 Prefab，也支持 Panel Prefab 层级内的 owner，无法确认所属 Prefab、MonoScript 或标准生成目录时拒绝写文件。

## 代码模板扩展边界

`IUIKitCodeTemplate` 与 `UIKitCodeTemplateRegistry` 只存在于 Unity Editor Adapter。项目模板只能转换 CodeGenKit 生成的内存源码；文件事务、用户 partial 保护、Designer 回滚、SessionState 队列和 Prefab 回填仍由 UIKit 生成流水线拥有。具有公开无参构造的实现可由 TypeCache 自动发现，依赖项目服务的实现使用显式 `Register`；模板名必须是安全 ID，Workbench 只选择当前 `get_editor_context.codeTemplateOptions` 中存在的名称。
