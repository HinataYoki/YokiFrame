#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// LogKit IMGUI 日志显示文档
    /// </summary>
    internal static class LogKitDocIMGUI
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "IMGUI 日志显示",
                Description = "在打包后启用 IMGUI 日志窗口，实时查看运行时日志。支持日志过滤、折叠、自动滚动等功能。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "启用 IMGUI",
                        Code = @"// 启用 IMGUI 日志显示
KitLoggerIMGUI.Enable();

// 指定最大日志条数
KitLoggerIMGUI.Enable(maxLogCount: 500);

// 禁用 IMGUI
KitLoggerIMGUI.Disable();

// 获取实例进行配置
var imgui = KitLoggerIMGUI.Enable();
imgui.ShowTimestamp = true;    // 显示时间戳
imgui.AutoScroll = true;       // 自动滚动
imgui.WindowAlpha = 0.9f;      // 窗口透明度
imgui.Filter = KitLoggerIMGUI.LogTypeFilter.All; // 日志过滤"
                    }
                }
            };
        }
    }
}
#endif
