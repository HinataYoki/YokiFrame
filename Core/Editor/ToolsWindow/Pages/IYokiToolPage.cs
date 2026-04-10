#if UNITY_EDITOR
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Contract implemented by every YokiFrame tool page.
    /// </summary>
    /// <remarks>
    /// New pages should usually inherit from <see cref="YokiToolPageBase"/> so they automatically get the
    /// shared lifecycle, query cache, and subscription cleanup behavior.
    /// </remarks>
    public interface IYokiToolPage
    {
        /// <summary>
        /// Display name shown in the tools window.
        /// </summary>
        string PageName { get; }

        /// <summary>
        /// Icon id used by the page. Prefer constants from <c>KitIcons</c>.
        /// </summary>
        string PageIcon { get; }

        /// <summary>
        /// Sorting priority, lower values appear first.
        /// </summary>
        int Priority { get; }

        /// <summary>
        /// Creates the page root UI.
        /// </summary>
        /// <returns>Root visual element for the page.</returns>
        VisualElement CreateUI();

        /// <summary>
        /// Called when the page becomes active.
        /// </summary>
        void OnActivate();

        /// <summary>
        /// Called when the page is deactivated.
        /// </summary>
        void OnDeactivate();

        /// <summary>
        /// Legacy polling update hook.
        /// </summary>
        void OnUpdate();
    }
}
#endif
