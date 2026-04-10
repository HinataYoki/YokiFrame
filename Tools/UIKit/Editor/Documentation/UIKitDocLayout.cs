#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// UIKit 布局与适配文档
    /// </summary>
    internal static class UIKitDocLayout
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "布局与适配",
                Description = "UIKit 提供安全区适配、屏幕信息监听等布局工具。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "SafeAreaAdapter 组件",
                        Code = @"// SafeAreaAdapter 自动适配设备安全区（刘海屏、圆角等）
// 在需要适配的 UI 容器上添加 SafeAreaAdapter 组件

// Inspector 配置：
// - Edges: 需要适配的边缘（Left/Right/Top/Bottom/All）
// - Simulate In Editor: 是否在编辑器中模拟安全区

// 边缘选项：
// SafeAreaEdge.All        - 适配所有边缘
// SafeAreaEdge.Horizontal - 仅适配左右
// SafeAreaEdge.Vertical   - 仅适配上下
// SafeAreaEdge.Top        - 仅适配顶部（刘海）",
                        Explanation = "SafeAreaAdapter 会自动响应屏幕旋转和分辨率变化。"
                    },
                    new()
                    {
                        Title = "SafeAreaAdapter 代码使用",
                        Code = @"var adapter = GetComponent<SafeAreaAdapter>();

// 动态修改适配边缘
adapter.Edges = SafeAreaEdge.Top | SafeAreaEdge.Bottom;

// 强制刷新
adapter.Refresh();

// 获取安全区信息
Rect safeArea = adapter.CurrentSafeArea;
float topInset = adapter.GetInset(SafeAreaEdge.Top);
Vector4 insets = adapter.GetInsets();",
                        Explanation = "代码方式适合需要动态控制适配行为的场景。"
                    }
                }
            };
        }
    }
}
#endif
