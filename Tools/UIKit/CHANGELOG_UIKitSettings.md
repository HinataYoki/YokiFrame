# UIKit Canvas 配置抽离变更日志

## 变更概述

将 UIKit Canvas 相关配置从代码硬编码抽离到 `YokiFrameSettings`，实现统一配置管理。

## 变更内容

### 新增文件

1. **YokiFrameSettings.UIKit.cs** (`Assets/YokiFrame/Tools/UIKit/Runtime/Settings/`)
   - 定义 `UIKitSettings` 配置类
   - 包含 Canvas、CanvasScaler、GraphicRaycaster 配置项
   - 提供 `ResetToDefault()` 方法

2. **UIKitToolPage.Settings.cs** (`Assets/YokiFrame/Tools/UIKit/Editor/`)
   - 新增 Settings 标签页
   - 提供可视化配置界面
   - 支持实时预览和保存

### 修改文件

1. **UIRoot.cs**
   - 新增 `Config` 属性，从 `YokiFrameSettings.Instance.UIKit` 读取配置
   - 保留 `mConfig` 字段（用于缓存、焦点、对话框等配置）
   - 新增 `ApplyCanvasConfig()`、`ApplyCanvasScalerConfig()`、`ApplyGraphicRaycasterConfig()` 方法
   - 在 `InitializeComponents()` 中调用配置应用方法

2. **UIKitToolPage.cs**
   - 新增 `Settings` 标签页枚举
   - 新增 `mSettingsTabBtn` 字段
   - 更新标签页切换逻辑

3. **UIKitDocCanvas.cs**
   - 更新文档，添加配置管理说明
   - 添加代码访问配置示例

## 配置项说明

### Canvas 配置
- `RenderMode`: 渲染模式（默认：ScreenSpaceOverlay）
- `SortOrder`: 排序顺序（默认：0）
- `TargetDisplay`: 目标显示器（默认：0）
- `PixelPerfect`: 像素完美（默认：false）

### CanvasScaler 配置
- `ScaleMode`: 缩放模式（默认：ScaleWithScreenSize）
- `ReferenceResolution`: 参考分辨率（默认：3840x2160）
- `ScreenMatchMode`: 屏幕匹配模式（默认：MatchWidthOrHeight）
- `MatchWidthOrHeight`: 宽高匹配权重（默认：0）
- `ReferencePixelsPerUnit`: 参考像素每单位（默认：100）
- `PhysicalUnit`: 物理单位（默认：Points）
- `FallbackScreenDPI`: 回退屏幕 DPI（默认：96）
- `DefaultSpriteDPI`: 默认精灵 DPI（默认：96）
- `DynamicPixelsPerUnit`: 动态像素每单位（默认：1）

### GraphicRaycaster 配置
- `IgnoreReversedGraphics`: 忽略反向图形（默认：false）
- `BlockingObjects`: 阻挡对象类型（默认：None）
- `BlockingMask`: 阻挡层级（默认：-1）

## 使用方式

### 编辑器配置
1. 打开 YokiFrame Tools 窗口
2. 切换到 UIKit 标签页
3. 点击 "设置" 子标签页
4. 修改配置项
5. 点击 "保存" 按钮

### 代码访问
```csharp
// 读取配置
var config = YokiFrameSettings.Instance.UIKit;
RenderMode renderMode = config.RenderMode;
Vector2 resolution = config.ReferenceResolution;

// 修改配置（编辑器模式）
#if UNITY_EDITOR
config.ReferenceResolution = new Vector2(1920, 1080);
UnityEditor.EditorUtility.SetDirty(YokiFrameSettings.Instance);
#endif

// 重置为默认值
config.ResetToDefault();
```

## Breaking Changes

无破坏性变更。现有项目升级后：
- UIRoot Prefab 中的 Canvas 配置将被 YokiFrameSettings 覆盖
- 首次运行时会使用默认配置（3840x2160）
- 可通过编辑器界面调整为项目所需配置

## 迁移指南

1. 升级到新版本后，打开 YokiFrame Tools > UIKit > 设置
2. 检查默认配置是否符合项目需求
3. 如需调整，修改配置并保存
4. 配置保存在 `Assets/Settings/Resources/YokiFrameSettings.asset`
5. 建议将此文件纳入版本控制

## 优势

1. **统一管理**：所有 Canvas 配置集中在 YokiFrameSettings
2. **无需修改 Prefab**：配置变更不影响 UIKit Prefab
3. **快速切换**：不同项目可快速应用不同配置
4. **可视化编辑**：通过编辑器界面直观修改配置
5. **运行时读取**：支持运行时访问配置数据
6. **版本控制友好**：配置文件独立，便于团队协作

## 注意事项

1. `UIRootConfig` 仍保留用于缓存、焦点、对话框等配置
2. Canvas 配置在 UIRoot 初始化时应用，运行时修改不会生效
3. 配置文件路径：`Assets/Settings/Resources/YokiFrameSettings.asset`
4. 首次使用需手动创建 YokiFrameSettings 资源（框架会自动创建）
