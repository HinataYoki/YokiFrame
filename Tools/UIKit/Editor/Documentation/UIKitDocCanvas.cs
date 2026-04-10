#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit Canvas 优化文档
    /// </summary>
    internal static class UIKitDocCanvas
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "Canvas 配置与优化",
                Description = "UIKit Canvas 配置已从代码抽离到 YokiFrameSettings，支持统一管理和运行时应用。同时提供动静分离方案优化渲染性能。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "Canvas 配置管理",
                        Code = @"// Canvas 配置已独立为 UIKitSettings ScriptableObject
// 路径：Assets/Settings/Resources/UIKitSettings.asset

// 编辑器配置方式：
// 1. 打开 YokiFrame Tools 窗口
// 2. 切换到 UIKit 标签页
// 3. 点击 ""设置"" 子标签页
// 4. 修改 Canvas/CanvasScaler/GraphicRaycaster 配置
// 5. 点击 ""保存"" 按钮

// 配置项包括：
// - Canvas: 渲染模式、排序顺序、像素完美
// - CanvasScaler: 缩放模式、参考分辨率(默认3840x2160)、匹配权重
// - GraphicRaycaster: 阻挡对象、阻挡层级

// 运行时自动应用：
// UIRoot 初始化时会自动从 UIKitSettings 读取配置并应用到 Canvas 组件",
                        Explanation = "配置统一管理，无需修改 Prefab，支持不同项目快速切换配置。UIKitSettings 独立于 Core，避免依赖污染。"
                    },
                    new()
                    {
                        Title = "代码访问配置",
                        Code = @"// 访问 UIKit 配置
var config = UIKitSettings.Instance;

// 读取配置
RenderMode renderMode = config.RenderMode;
Vector2 resolution = config.ReferenceResolution;
float matchWeight = config.MatchWidthOrHeight;

// 修改配置（编辑器模式）
#if UNITY_EDITOR
config.ReferenceResolution = new Vector2(1920, 1080);
config.MatchWidthOrHeight = 0.5f;
UnityEditor.EditorUtility.SetDirty(config);
UnityEditor.AssetDatabase.SaveAssets();
#endif

// 重置为默认值
config.ResetToDefault();",
                        Explanation = "运行时可读取配置，编辑器模式可修改配置。UIKitSettings 独立管理，不依赖 YokiFrameSettings。"
                    },
                    new()
                    {
                        Title = "UIDynamicElement 组件",
                        Code = @"// UIDynamicElement 自动创建嵌套 Canvas，实现动静分离
// 使用方式：在需要频繁更新的 UI 元素上添加组件即可

// 适用场景：
// - 血条、蓝条等实时更新的数值显示
// - 计时器、倒计时文本
// - 动画进度条
// - 频繁变化的列表项内容

// 不需要使用的场景：
// - 静态背景、边框（不会触发 rebuild）
// - 按钮点击（不会触发 rebuild）
// - 偶尔更新的文本",
                        Explanation = "UIDynamicElement 是最简单的动静分离方案，添加组件即可生效。"
                    },
                    new()
                    {
                        Title = "UIDynamicElement API",
                        Code = @"var element = GetComponent<UIDynamicElement>();

// 属性
Canvas canvas = element.Canvas;           // 嵌套 Canvas 引用
bool isInit = element.IsInitialized;      // 是否已初始化
element.EnableRaycast = false;            // 控制射线检测

// 方法
element.Initialize();    // 手动初始化（AutoInitialize=false 时使用）
element.ForceRebuild();  // 强制刷新 Canvas

// Inspector 配置：
// - Enable Raycast: 是否需要射线检测（有交互元素时启用）
// - Auto Initialize: 是否在 Awake 时自动初始化",
                        Explanation = "大多数情况下使用默认配置即可，无需代码操作。"
                    },
                    new()
                    {
                        Title = "CanvasBatchHint 组件",
                        Code = @"// CanvasBatchHint 用于配置 Canvas 的渲染优化选项
var hint = GetComponent<CanvasBatchHint>();

// 设置排序顺序
hint.SetSortingOrder(100);

// 控制 Raycaster（无交互的 Canvas 可禁用以提升性能）
hint.SetRaycasterEnabled(false);

// 控制像素对齐（减少模糊但可能影响性能）
hint.PixelPerfect = true;

// 获取关联的 Canvas
Canvas canvas = hint.Canvas;

// Inspector 配置：
// - Pixel Perfect: 像素对齐
// - Override Sorting: 是否覆盖排序层
// - Sorting Order: 排序顺序
// - Disable Raycaster: 禁用射线检测",
                        Explanation = "CanvasBatchHint 提供更细粒度的 Canvas 控制，适合需要精确配置的场景。"
                    }
                }
            };
        }
    }
}
#endif
