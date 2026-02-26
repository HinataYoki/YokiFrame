#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LogKit IMGUI 操作方式文档
    /// </summary>
    internal static class LogKitDocIMGUIOperation
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "IMGUI 操作方式",
                Description = "IMGUI 日志窗口支持多种交互方式，适配 PC 和移动端。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "交互操作",
                        Code = @"// === PC 端 ===
// 按 ` 键（数字1左边）切换窗口显示/隐藏

// === 移动端 ===
// 三指同时触摸切换窗口显示/隐藏

// === 窗口内操作 ===
// Clear      - 清空所有日志
// Collapse   - 合并重复日志
// AutoScroll - 自动滚动到最新日志
// Time       - 显示/隐藏时间戳
// Log/Warn/Error - 过滤日志类型
// X          - 关闭窗口

// === 自定义触发方式 ===
var imgui = KitLoggerIMGUI.Instance;
imgui.ToggleKey = KeyCode.F12;      // 修改触发按键
imgui.ToggleTouchCount = 4;         // 修改触发手指数"
                    }
                }
            };
        }
    }
}
#endif
