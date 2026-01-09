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
                Title = "Canvas 优化",
                Description = "UIKit 提供 Canvas 动静分离优化方案，通过嵌套 Canvas 隔离频繁更新的 UI 元素。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "UIDynamicElement 组件",
                        Code = @"// UIDynamicElement 自动创建嵌套 Canvas
// 使用方式：
// 1. 选择需要频繁更新的 UI 元素
// 2. 添加 UIDynamicElement 组件
// 3. 完成！

// 适用场景：
// - 血条、蓝条等实时更新的数值显示
// - 计时器、倒计时文本
// - 动画进度条",
                        Explanation = "UIDynamicElement 是最简单的动静分离方案。"
                    },
                    new()
                    {
                        Title = "UIDynamicElement 配置",
                        Code = @"// Inspector 配置项：
// - Enable Raycast: 是否需要射线检测
// - Auto Initialize: 是否在 Awake 时自动初始化

// 代码使用：
Canvas nestedCanvas = mDynamicElement.Canvas;
bool isInit = mDynamicElement.IsInitialized;
mDynamicElement.EnableRaycast = false;
mDynamicElement.ForceRebuild();",
                        Explanation = "大多数情况下使用默认配置即可。"
                    },
                    new()
                    {
                        Title = "CanvasBatchHint 组件",
                        Code = @"// CanvasBatchHint 用于配置 Canvas 的渲染优化选项
var hint = GetComponent<CanvasBatchHint>();

// 设置排序顺序
hint.SetSortingOrder(100);

// 控制 Raycaster
hint.SetRaycasterEnabled(false);

// 控制像素对齐
hint.PixelPerfect = true;",
                        Explanation = "CanvasBatchHint 提供更细粒度的 Canvas 控制。"
                    }
                }
            };
        }
    }
}
#endif
