#if !GODOT
#if UNITY_EDITOR
using System;

namespace YokiFrame.Unity
{
    /// <summary>
    /// YokiFrame 编辑器样式注册特性
    /// 
    /// 使用此特性显式声明 Kit 的样式表路径。
    /// AI 可通过搜索 [YokiEditorStyle( 找到所有 Kit 样式入口。
    /// 
    /// 示例:
    /// [assembly: YokiEditorStyle(
    ///     "EventKit", 
    ///     "Runtime/Core/EventKit/Editor/Styling/EventKit.uss"
    /// )]
    /// </summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    public sealed class YokiEditorStyleAttribute : Attribute
    {
        /// <summary>
        /// Kit 名称（如 "EventKit", "PoolKit"）
        /// </summary>
        public string Kit { get; }
        
        /// <summary>
        /// 样式表资源路径。
        /// 
        /// 推荐写法：Kit 样式使用相对 Unity Adapter 根的 "Runtime/..." 路径，例如 "Runtime/Core/EventKit/Editor/Styling/EventKit.uss"。
        /// 通用编辑器样式仍可使用相对于 UISystem/Styling 的路径，例如 "Core/YokiCoreComponents.uss"。
        /// 也支持直接写 AssetDatabase 路径（以 "Assets/" 或 "Packages/" 开头）。
        /// 
        /// 解析逻辑由 YokiStyleService + YokiEditorPaths 统一处理，可同时兼容 Packages/、Assets/、Plugins 三种安装方式。
        /// </summary>
        public string StyleSheetPath { get; }
        
        /// <summary>
        /// 加载优先级（越小越先加载，默认 0）
        /// </summary>
        public int Priority { get; }
        
        /// <summary>
        /// 创建样式注册特性
        /// </summary>
        /// <param name="kit">Kit 名称</param>
        /// <param name="styleSheetPath">样式表资源路径</param>
        /// <param name="priority">加载优先级（默认 0）</param>
        public YokiEditorStyleAttribute(string kit, string styleSheetPath, int priority = 0)
        {
            Kit = kit ?? string.Empty;
            StyleSheetPath = styleSheetPath ?? string.Empty;
            Priority = priority;
        }
    }
}
#endif
#endif
