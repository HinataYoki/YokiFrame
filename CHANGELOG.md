# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
