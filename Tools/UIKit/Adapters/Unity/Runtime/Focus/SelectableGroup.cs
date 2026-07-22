#if UNITY_2022_3_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>为一组 Selectable 提供默认焦点和显式边界导航策略。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Selectable Group")]
    public sealed class SelectableGroup : MonoBehaviour
    {
        [SerializeField] private NavigationBoundaryBehavior mLeftBoundary;
        [SerializeField] private NavigationBoundaryBehavior mRightBoundary;
        [SerializeField] private NavigationBoundaryBehavior mUpBoundary;
        [SerializeField] private NavigationBoundaryBehavior mDownBoundary;
        [SerializeField] private SelectableGroup mLeftJumpTarget;
        [SerializeField] private SelectableGroup mRightJumpTarget;
        [SerializeField] private SelectableGroup mUpJumpTarget;
        [SerializeField] private SelectableGroup mDownJumpTarget;
        [SerializeField] private Selectable mDefaultSelectable;
        private readonly List<Selectable> mSelectables = new(8);
        private bool mIsDirty = true;

        /// <summary>获取或设置默认选中控件。</summary>
        public Selectable DefaultSelectable
        {
            get { return mDefaultSelectable; }
            set { mDefaultSelectable = value; }
        }

        /// <summary>获取组内当前可交互的第一个控件。</summary>
        public Selectable GetFirstSelectable()
        {
            if (mDefaultSelectable != null && mDefaultSelectable.interactable && mDefaultSelectable.gameObject.activeInHierarchy)
                return mDefaultSelectable;
            RefreshSelectablesIfNeeded();
            for (var index = 0; index < mSelectables.Count; index++)
            {
                Selectable selectable = mSelectables[index];
                if (selectable != null && selectable.interactable && selectable.gameObject.activeInHierarchy) return selectable;
            }
            return null;
        }

        /// <summary>获取组内 Selectable 的稳定快照引用。</summary>
        public IReadOnlyList<Selectable> GetSelectables()
        {
            RefreshSelectablesIfNeeded();
            return mSelectables;
        }

        /// <summary>获取指定方向的边界处理策略。</summary>
        public NavigationBoundaryBehavior GetBoundaryBehavior(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Left: return mLeftBoundary;
                case MoveDirection.Right: return mRightBoundary;
                case MoveDirection.Up: return mUpBoundary;
                case MoveDirection.Down: return mDownBoundary;
                default: return NavigationBoundaryBehavior.Stop;
            }
        }

        /// <summary>获取指定方向的跨组跳转目标。</summary>
        public SelectableGroup GetJumpTarget(MoveDirection direction)
        {
            switch (direction)
            {
                case MoveDirection.Left: return mLeftJumpTarget;
                case MoveDirection.Right: return mRightJumpTarget;
                case MoveDirection.Up: return mUpJumpTarget;
                case MoveDirection.Down: return mDownJumpTarget;
                default: return null;
            }
        }

        /// <summary>重新计算组内控件的 Explicit Navigation。</summary>
        public void ConfigureNavigation()
        {
            RefreshSelectablesIfNeeded();
            for (var index = 0; index < mSelectables.Count; index++)
            {
                Selectable selectable = mSelectables[index];
                if (selectable == null) continue;
                Navigation navigation = selectable.navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = GetLinearTarget(index, MoveDirection.Left);
                navigation.selectOnRight = GetLinearTarget(index, MoveDirection.Right);
                navigation.selectOnUp = GetLinearTarget(index, MoveDirection.Up);
                navigation.selectOnDown = GetLinearTarget(index, MoveDirection.Down);
                selectable.navigation = navigation;
            }
        }

        /// <summary>标记子节点变化，下一次查询时刷新控件列表。</summary>
        public void SetDirty()
        {
            mIsDirty = true;
        }

        /// <summary>启用时让组在下一次配置前重新扫描子节点。</summary>
        private void OnEnable()
        {
            mIsDirty = true;
        }

        /// <summary>子节点变化时使缓存失效。</summary>
        private void OnTransformChildrenChanged()
        {
            mIsDirty = true;
        }

        /// <summary>按线性顺序计算相邻控件并应用边界行为。</summary>
        private Selectable GetLinearTarget(int index, MoveDirection direction)
        {
            int step = direction == MoveDirection.Left || direction == MoveDirection.Up ? -1 : 1;
            int targetIndex = index + step;
            if (targetIndex >= 0 && targetIndex < mSelectables.Count) return mSelectables[targetIndex];
            NavigationBoundaryBehavior behavior = GetBoundaryBehavior(direction);
            if (behavior == NavigationBoundaryBehavior.Wrap && mSelectables.Count > 0)
                return mSelectables[step < 0 ? mSelectables.Count - 1 : 0];
            if (behavior == NavigationBoundaryBehavior.JumpToGroup)
            {
                SelectableGroup target = GetJumpTarget(direction);
                return target != null ? target.GetFirstSelectable() : null;
            }
            return null;
        }

        /// <summary>刷新组内 Selectable 缓存。</summary>
        private void RefreshSelectablesIfNeeded()
        {
            if (!mIsDirty) return;
            mSelectables.Clear();
            GetComponentsInChildren(false, mSelectables);
            mIsDirty = false;
        }
    }
}
#endif
