# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.6.4] - 2026-01-27

### Changed
- **PoolKit** 监控面板数字显示优化（使用/池内/容量格式，颜色图例说明）

### Fixed
- **PoolKit** 修复池内数字负数、代码跳转失效、左右侧数据不同步问题

### Removed
- **PoolKit** 移除泄露检测、时长排序、定位归还功能及相关冗余代码

## [1.6.3] - 2026-01-20

### Fixed
- **UIKit** 修复场景切换时 "Some objects were not cleaned up" 警告
  - `UIFocusHighlight`: `OnDestroy` 中立即终止 DOTween 动画（`Kill(complete: false)`），清空所有组件引用防止延迟回调持有 GameObject
  - `UIRoot.Focus`: `DisposeFocusSystem()` 改用 `DestroyImmediate`，销毁前先调用 `Hide()` 停止动画
  - `UIRoot.Level`: `ClearAllLevels()` 添加 `blocker != null` 双重判空，防止已标记销毁对象触发异常
  - `UIDebugOverlay`: `OnDestroy` 中清空 GUIStyle 引用（`mBoxStyle`/`mLabelStyle`/`mHeaderStyle`），防止场景切换时残留

## [1.6.2] - 2026-01-16

### Fixed
- **UIKit** 修复场景关闭时 UIKit GameObject 未正确清理的警告（`OnDestroy` 中模态遮罩使用 `DestroyImmediate` 立即销毁）


## [1.6.1] - 2026-01-15

### Fixed
- **PoolKit** 修复打包时 `PoolEvent`/`PoolEventType` 类型未找到的编译错误（移除非编辑器分支中对仅编辑器类型的引用）
- **UIKit** 修复 `SafeAreaAdapter.mSimulateInEditor` 字段打包时 CS0414 警告（将编辑器模拟字段用 `#if UNITY_EDITOR` 包裹）

## [1.6.0-preview.2] - 2026-01-14

### Added
- **InputKit** - 基于 Unity InputSystem 的输入管理封装
  - 类型安全的输入访问，编译时检查
  - 运行时重绑定系统（支持键盘/手柄、复合绑定、冲突处理）
  - 输入上下文系统（UI/对话/过场等场景的输入屏蔽）
  - 连招检测、输入缓冲、震动反馈
  - 设备切换检测与 UI 提示更新
- **SpatialKit** - 高性能空间索引系统
  - 空间哈希网格（HashGrid）- O(1) 查询，均匀分布最优
  - 四叉树（Quadtree）- 2D/2.5D 场景自适应分区
  - 八叉树（Octree）- 完整 3D 空间索引
  - 范围查询、最近邻查询，零 GC 分配
  - 替代 Physics.OverlapSphere 的高效方案

## [1.6.0-preview.1] - 2026-01-11

### Added
- **InputKit 文档大幅增强**
  - 新增「快速入门」章节：完整使用流程、核心 API 速查、常见问题解答
  - 新增「运行时重绑定」详细文档：
    - BindingIndex 核心概念解释（普通绑定 vs 复合绑定索引结构）
    - 查找正确 BindingIndex 的多种方法
    - 基础重绑定 UI 组件实现（UGUI）
    - 完整按键设置面板示例（键盘/手柄切换、复合绑定 WASD、重置所有）
    - 高级重绑定配置（RebindOptions、冲突处理、超时处理）
    - 绑定持久化（自动/手动、云存档、自定义实现）
    - 重置绑定与 OnBindingChanged 事件参数解析
    - 获取绑定显示名称（按控制方案、复合绑定、UI 提示）
  - 新增「输入上下文系统」详细文档：
    - 核心概念：上下文栈结构与 ActionMap 的区别
    - 创建 InputContext ScriptableObject 资产
    - 典型使用场景（UI 系统、对话系统、过场动画、教程引导、QTE 事件）
    - 在输入处理中检查上下文（IsActionBlocked）
    - 高级栈操作（PopToContext、ClearContextStack、查询状态）
    - 与 ActionMap 配合使用
    - 完整 GameInputManager 集成示例

### Fixed
- **InputKit 条件编译重构**
  - asmdef 添加 `defineConstraints: ["YOKIFRAME_INPUTSYSTEM_SUPPORT"]`，无 InputSystem 时整个程序集跳过编译
  - 移除运行时文件中冗余的 `#if ENABLE_INPUT_SYSTEM`（由 defineConstraints 统一控制）
  - `InputKit.Rebind.cs` 保留 `#if YOKIFRAME_UNITASK_SUPPORT`（依赖 UniTask 的异步重绑定功能）
  - 编辑器文件 `InputKitToolPage.cs` 使用 `YOKIFRAME_INPUTSYSTEM_SUPPORT` 替代 `ENABLE_INPUT_SYSTEM`
  - 修复无 InputSystem/UniTask 环境下的编译错误
- **UIKit 条件编译修复**
  - asmdef 添加 InputSystem 的 `versionDefines` 配置
  - `GamepadInputHandler.cs`、`UIFocusSystem.cs` 等文件使用 `YOKIFRAME_INPUTSYSTEM_SUPPORT` 替代 `ENABLE_INPUT_SYSTEM`
  - 修复无 InputSystem 环境下的编译错误
- **GestureRecognizer 长按功能完善**
  - 实现 `OnLongPress` 事件触发逻辑
  - 添加 `LongPressThreshold` 配置属性

### Changed
- InputKitDocData 章节顺序调整，快速入门移至首位

## [1.5.5] - 2026-01-10

### Added
- **PoolKit 编辑器工具**
  - 新增对象池运行时监控面板，支持实时查看所有池的状态
  - Master-Detail 布局：左侧池列表 + 右侧详情面板
  - HUD 概览：搜索框 + 统计卡片（总数/活跃/空闲/峰值）
  - 活跃对象卡片：显示对象名、借出时长、调用来源
  - 操作按钮：代码跳转、Hierarchy 定位、强制归还
  - 可展开堆栈详情：点击卡片展开完整调用堆栈
  - 事件日志面板：实时显示 Spawn/Return/Create/Destroy 事件
  - 追踪/堆栈开关：工具栏快捷切换
  - 响应式数据更新：通过 EditorDataBridge 订阅运行时数据变化
  - 活跃时间实时更新：每秒刷新借出时长显示
  - 代码跳转功能：点击"代码"按钮跳转到借出代码位置

## [1.5.4] - 2026-01-10

### Changed
- **配置系统重构**
  - 新增 `YokiFrameSettings` 统一配置 ScriptableObject，集中管理各 Kit 运行时配置
  - 配置文件自动创建到 `Assets/Settings/Resources/YokiFrameSettings.asset`
  - 支持 Package 模式：配置存储在用户项目中，Package 代码只读
  - 运行时通过 `Resources.Load` 加载配置，确保打包后正常生效

### Fixed
- 修复 KitLogger 真机 IMGUI 配置打包后不生效的问题（原 JSON 配置不会被打包）

### Removed
- 移除 `KitLoggerSettings.cs`，配置迁移至 `YokiFrameSettings.LogKit`
- 移除 `ProjectSettings/KitLoggerSettings.json`

## [1.5.3] - 2026-01-10

### Changed
- **框架结构重构**
  - `Core/Runtime/Kit/` 扁平化，移除 `Kit` 中间层，所有基础模块直接位于 `Core/Runtime/` 下
  - `Core/Editor/CodeGenKit/Editor/` 扁平化，移除多余的 `Editor` 中间层
  - `Core/Editor/Drawers/ResKit/YooAsset/` 合并到 `Core/Editor/ToolPages/ResKit/YooAsset/`
  - 删除 `Core/Editor/Drawers/` 目录
  - `YokiFrame.ResKit.YooAsset.Editor` 程序集合并到 `YokiFrame.Core.Editor`
- **文件组织优化**
  - `SceneKit/Runtime/Partial/` 下的文件移至 `Runtime/` 根目录
  - `AudioKit/Runtime/Partial/` 下的文件移至 `Runtime/` 根目录
  - `UIKit/Runtime/Partial/` 下的文件移至 `Runtime/` 根目录
  - `SaveKit/Runtime/Partial/` 下的文件移至 `Runtime/` 根目录
- **命名规范修正**
  - `ToolPages/KitLogger/` 重命名为 `ToolPages/LogKit/`（命名一致性）
  - `UIKit/Runtime/UICreater/` 修正为 `UIKit/Runtime/UICreator/`（拼写错误）
  - `SceneKit/Runtime/Unitask/` 修正为 `SceneKit/Runtime/UniTask/`（拼写错误）

## [1.5.2] - 2026-01-10

### Added
- `YooInit` 一键初始化 YooAsset 并自动配置 ResKit 加载器
  - 统一 API：`InitAsync()` 有 UniTask 时返回 `UniTask`，无 UniTask 时返回 `IEnumerator`
  - 智能包查找 `FindPackageForPath()` 自动定位资源所在包
  - 多包配置支持，第一个包为默认包
  - `LoadRawAsync()` / `LoadRawFileData()` / `LoadRawFileText()` 原始文件加载
  - `UnloadUnusedAssetsAsync()` 卸载所有包未使用资源
  - 自定义模式扩展：`HostModeHandler` / `WebModeHandler` / `CustomHandler` 委托
  - 支持 `CustomPlayMode` 自定义运行模式
- ResKit 文档拆分为子模块便于快速定位
  - 完整初始化示例下增加子目录导航
  - 补充自定义加密完整用法文档
  - 补充 CustomPlayMode 使用说明

### Changed
- UIKit 程序集新增 YooAsset 可选引用
- SceneKit 程序集新增 YooAsset 可选引用
- `YooAssetRawFileLoader` / `YooAssetRawFileLoaderUniTask` 支持智能包查找

## [1.5.1] - 2026-01-10

### Fixed
- 修复 Unity 2021.3 中打开 YokiFrame 编辑器面板时的 NullReferenceException（TextField 内部结构兼容性）

## [1.5.0] - 2026-01-10

### Added
- ESC 快捷键关闭 YokiFrame Tools 面板
- `YokiFrameUIComponents.FixScrollViewDragger()` 确保 ScrollView 滚动条正常工作
- `HighlightIndicator` 可复用高亮动画组件

### Fixed
- 修复 ScrollView 滚动条无法拖动的问题（drag-container 尺寸异常）
- 修复文档页面滚动后点击标题出现双重高亮的问题
- 修复 UIKit 动画文档中 API 名称错误（CreateScaleIn → CreatePopIn，CreateSequence → CreateSequential）
- 修复 UIKit 动画文档中 DOTween 支持说明（YokiFrame 自动检测依赖，无需手动添加宏定义）
- 修复 LogKit 文档中 IMGUI API 名称错误（KitLogger.EnableIMGUI → KitLoggerIMGUI.Enable）
- 修复 README 中 KitLoggerIMGUI API 调用错误
- 补充 UIKit 动画文档中直接使用 DOTween 动画类的示例（DOTweenFadeAnimation、DOTweenScaleAnimation）

### Changed
- 侧边栏 YokiFrame 图标区域固定在顶部，不随内容滚动
- 优化文档页面导航高亮动画逻辑
