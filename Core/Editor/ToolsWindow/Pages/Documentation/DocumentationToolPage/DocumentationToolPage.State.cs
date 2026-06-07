#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SessionState persistence for documentation page browsing state.
    /// Survives domain reload (script compilation) so the user stays on the
    /// same documentation page after code changes trigger a recompile.
    /// </summary>
    public partial class DocumentationToolPage
    {
        private const string SESSION_KEY_SELECTED_MODULE = "YokiFrame.Documentation.SelectedModule";
        private const string SESSION_KEY_SCROLL_OFFSET = "YokiFrame.Documentation.ScrollOffset";

        #region Module Key

        /// <summary>
        /// Builds a stable unique key for a documentation module.
        /// </summary>
        private static string GetModuleKey(DocModule module)
        {
            if (module == null || string.IsNullOrEmpty(module.Name))
            {
                return null;
            }

            return $"{module.Category}|{module.Name}";
        }

        /// <summary>
        /// Saves the currently selected module key to SessionState.
        /// </summary>
        private static void SaveSelectedModule(string moduleKey)
        {
            SessionState.SetString(SESSION_KEY_SELECTED_MODULE, moduleKey ?? string.Empty);
        }

        /// <summary>
        /// Loads the previously selected module key from SessionState.
        /// </summary>
        private static string LoadSelectedModuleKey()
        {
            return SessionState.GetString(SESSION_KEY_SELECTED_MODULE, string.Empty);
        }

        #endregion

        #region Scroll Offset

        private IVisualElementScheduledItem mScrollSaveItem;

        /// <summary>
        /// Persists the current content scroll position to SessionState.
        /// </summary>
        private void SaveScrollOffset()
        {
            if (mContentScrollView == null)
            {
                return;
            }

            SessionState.SetFloat(SESSION_KEY_SCROLL_OFFSET, mContentScrollView.scrollOffset.y);
        }

        /// <summary>
        /// Clears the saved scroll offset so the next page starts at the top.
        /// Called when the user manually switches to a different documentation module.
        /// </summary>
        private static void ClearSavedScrollOffset()
        {
            SessionState.EraseString(SESSION_KEY_SCROLL_OFFSET);
        }

        /// <summary>
        /// Debounced scroll save — postpones the save until scrolling pauses for 500 ms.
        /// Call from <see cref="OnContentScrollChanged"/>.
        /// </summary>
        private void ScheduleScrollSave()
        {
            mScrollSaveItem?.Pause();
            mScrollSaveItem = mContentScrollView?.schedule.Execute(SaveScrollOffset);
            mScrollSaveItem?.ExecuteLater(500);
        }

        /// <summary>
        /// Reads the persisted scroll offset from SessionState.
        /// </summary>
        private static float LoadScrollOffset()
        {
            return SessionState.GetFloat(SESSION_KEY_SCROLL_OFFSET, 0f);
        }

        /// <summary>
        /// Restores the content scroll position after a domain reload.
        /// Scheduled with a short delay to allow the content layout to finish.
        /// </summary>
        private void RestoreScrollOffset()
        {
            float savedOffset = LoadScrollOffset();
            if (savedOffset <= 0f || mContentScrollView == null)
            {
                return;
            }

            mContentScrollView.schedule.Execute(() =>
            {
                if (mContentScrollView != null)
                {
                    mContentScrollView.scrollOffset = new Vector2(0, savedOffset);
                }
            }).ExecuteLater(100);
        }

        #endregion

        #region Restore

        /// <summary>
        /// Resolves the module index to restore from SessionState.
        /// Returns 0 if no saved state or the saved module no longer exists.
        /// </summary>
        private int GetRestoredModuleIndex()
        {
            var savedKey = LoadSelectedModuleKey();
            if (string.IsNullOrEmpty(savedKey))
            {
                return 0;
            }

            for (int i = 0; i < mModules.Count; i++)
            {
                if (mModules[i] != null && GetModuleKey(mModules[i]) == savedKey)
                {
                    return i;
                }
            }

            return 0;
        }

        #endregion
    }
}
#endif
