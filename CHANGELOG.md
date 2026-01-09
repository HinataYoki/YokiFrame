# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

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
