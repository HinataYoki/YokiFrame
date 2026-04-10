#if UNITY_EDITOR
using System;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Category for registered tool pages.
    /// </summary>
    public enum YokiPageCategory
    {
        /// <summary>
        /// Documentation page.
        /// </summary>
        Documentation = 0,

        /// <summary>
        /// Interactive tool page.
        /// </summary>
        Tool = 1
    }

    /// <summary>
    /// Declarative registration metadata for a YokiFrame tool page.
    /// </summary>
    /// <remarks>
    /// Types marked with this attribute are discovered automatically by <see cref="YokiToolPageRegistry"/>.
    /// This attribute is the canonical entry point for kit-owned editor pages under the shared tools window.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public sealed class YokiToolPageAttribute : Attribute
    {
        /// <summary>
        /// Owning kit name, such as <c>EventKit</c> or <c>AudioKit</c>.
        /// </summary>
        public string Kit { get; }

        /// <summary>
        /// Display name shown in the tools window.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Icon id used by the page. Prefer constants from <c>KitIcons</c>.
        /// </summary>
        public string Icon { get; }

        /// <summary>
        /// Sorting priority, lower values appear first.
        /// </summary>
        public int Priority { get; }

        /// <summary>
        /// Page category.
        /// </summary>
        public YokiPageCategory Category { get; }

        /// <summary>
        /// Creates page registration metadata.
        /// </summary>
        /// <param name="kit">Owning kit name.</param>
        /// <param name="name">Display name.</param>
        /// <param name="icon">Icon id.</param>
        /// <param name="priority">Sorting priority. Default is 100.</param>
        /// <param name="category">Page category. Default is <see cref="YokiPageCategory.Tool"/>.</param>
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
