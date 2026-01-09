#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit 编辑器工具文档
    /// </summary>
    internal static class SceneKitDocEditor
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "编辑器工具",
                Description = "SceneKit 提供编辑器工具面板，用于查看和管理运行时场景。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "使用编辑器工具",
                        Code = @"// 快捷键：Ctrl+E 打开 YokiFrame Tools 面板
// 选择 SceneKit 标签页

// 功能：
// - 查看所有已加载场景列表
// - 查看场景状态（加载中/已加载/卸载中）
// - 查看加载进度和暂停状态
// - 卸载指定场景
// - 设置活动场景
// - 卸载所有非活动场景",
                        Explanation = "编辑器工具在运行时自动刷新，方便调试场景管理。"
                    }
                }
            };
        }
    }
}
#endif
