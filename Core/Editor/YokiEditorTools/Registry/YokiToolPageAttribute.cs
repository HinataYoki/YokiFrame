#if UNITY_EDITOR
using System;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 页面分类枚举
    /// </summary>
    public enum YokiPageCategory
    {
        /// <summary>文档页面</summary>
        Documentation = 0,
        /// <summary>工具页面</summary>
        Tool = 1
    }

    /// <summary>
    /// YokiFrame 工具页面注册特性
    /// 
    /// 使用此特性标记页面类，Registry 会自动收集并注册。
    /// AI 可通过搜索 [YokiToolPage( 找到所有页面入口。
    /// 
    /// 示例:
    /// [YokiToolPage(
    ///     kit: "EventKit",
    ///     name: "事件监控",
    ///     icon: KitIcons.EVENT,
    ///     priority: 30,
    ///     category: YokiPageCategory.Tool
    /// )]
    /// public class EventKitToolPage : YokiToolPageBase { }
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class YokiToolPageAttribute : Attribute
    {
        /// <summary>
        /// 所属 Kit 名称（如 "EventKit", "PoolKit"）
        /// </summary>
        public string Kit { get; }

        /// <summary>
        /// 页面显示名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 页面图标 ID（使用 KitIcons 常量）
        /// </summary>
        public string Icon { get; }

        /// <summary>
        /// 排序优先级（越小越靠前）
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// 页面分类
        /// </summary>
        public YokiPageCategory Category { get; }

        /// <summary>
        /// 创建页面注册特性
        /// </summary>
        /// <param name="kit">所属 Kit 名称</param>
        /// <param name="name">页面显示名称</param>
        /// <param name="icon">页面图标 ID</param>
        /// <param name="priority">排序优先级（默认 100）</param>
        /// <param name="category">页面分类（默认 Tool）</param>
        public YokiToolPageAttribute(
            string kit,
            string name,
            string icon = null,
            int priority = 100,
            YokiPageCategory category = YokiPageCategory.Tool)
        {
            Kit = kit ?? string.Empty;
            Name = name ?? string.Empty;
            Icon = icon ?? KitIcons.DOCUMENT;
            Priority = priority;
            Category = category;
        }
    }
}
#endif
