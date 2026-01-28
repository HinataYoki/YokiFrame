using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 边界行为
    /// </summary>
    public enum NavigationBoundaryBehavior
    {
        /// <summary>
        /// 停止在边界
        /// </summary>
        Stop,
        
        /// <summary>
        /// 循环到另一端
        /// </summary>
        Wrap,
        
        /// <summary>
        /// 跳转到指定组
        /// </summary>
        JumpToGroup
    }

    /// <summary>
    /// 可选择元素组 - 定义导航区域和边界行为
    /// </summary>
    [DisallowMultipleComponent]
    public class SelectableGroup : MonoBehaviour
    {
        #region 配置

        [SerializeField] private NavigationBoundaryBehavior mLeftBoundary = NavigationBoundaryBehavior.Stop;
        [SerializeField] private NavigationBoundaryBehavior mRightBoundary = NavigationBoundaryBehavior.Stop;
        [SerializeField] private NavigationBoundaryBehavior mUpBoundary = NavigationBoundaryBehavior.Stop;
        [SerializeField] private NavigationBoundaryBehavior mDownBoundary = NavigationBoundaryBehavior.Stop;

        [SerializeField] private SelectableGroup mLeftJumpTarget;
        [SerializeField] private SelectableGroup mRightJumpTarget;
        [SerializeField] private SelectableGroup mUpJumpTarget;
        [SerializeField] private SelectableGroup mDownJumpTarget;

        [SerializeField] private Selectable mDefaultSelectable;

        #endregion

        #region 属性

        /// <summary>
        /// 左边界行为
        /// </summary>
        public NavigationBoundaryBehavior LeftBoundary
        {
            get => mLeftBoundary;
            set => mLeftBoundary = value;
        }

        /// <summary>
        /// 右边界行为
        /// </summary>
        public NavigationBoundaryBehavior RightBoundary
        {
            get => mRightBoundary;
            set => mRightBoundary = value;
        }

        /// <summary>
        /// 上边界行为
        /// </summary>
        public NavigationBoundaryBehavior UpBoundary
        {
            get => mUpBoundary;
            set => mUpBoundary = value;
        }

        /// <summary>
        /// 下边界行为
        /// </summary>
        public NavigationBoundaryBehavior DownBoundary
        {
            get => mDownBoundary;
            set => mDownBoundary = value;
        }

        /// <summary>
        /// 默认选中元素
        /// </summary>
        public Selectable DefaultSelectable
        {
            get => mDefaultSelectable;
            set => mDefaultSelectable = value;
        }

        #endregion

        #region 缓存

        private List<Selectable> mSelectables;
        private bool mIsDirty = true;

        #endregion

        #region 生命周期

        private void OnEnable()
        {
            mIsDirty = true;
        }

        private void OnTransformChildrenChanged()
        {
            mIsDirty = true;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 获取组内所有可选择元素
        /// </summary>
        public IReadOnlyList<Selectable> GetSelectables()
        {
            RefreshSelectablesIfNeeded();
            return mSelectables;
        }

        /// <summary>
        /// 获取组内第一个可交互元素
        /// </summary>
        public Selectable GetFirstSelectable()
        {
            if (mDefaultSelectable != null && mDefaultSelectable.interactable)
            {
                return mDefaultSelectable;
            }

            RefreshSelectablesIfNeeded();
            foreach (var selectable in mSelectables)
            {
                if (selectable != null && selectable.interactable)
                {
                    return selectable;
                }
            }
            return null;
        }

        /// <summary>
        /// 获取边界跳转目标
        /// </summary>
        public SelectableGroup GetJumpTarget(MoveDirection direction)
        {
            return direction switch
            {
                MoveDirection.Left => mLeftJumpTarget,
                MoveDirection.Right => mRightJumpTarget,
                MoveDirection.Up => mUpJumpTarget,
                MoveDirection.Down => mDownJumpTarget,
                _ => null
            };
        }

        /// <summary>
        /// 获取边界行为
        /// </summary>
        public NavigationBoundaryBehavior GetBoundaryBehavior(MoveDirection direction)
        {
            return direction switch
            {
                MoveDirection.Left => mLeftBoundary,
                MoveDirection.Right => mRightBoundary,
                MoveDirection.Up => mUpBoundary,
                MoveDirection.Down => mDownBoundary,
                _ => NavigationBoundaryBehavior.Stop
            };
        }

        /// <summary>
        /// 设置边界跳转目标
        /// </summary>
        public void SetJumpTarget(MoveDirection direction, SelectableGroup target)
        {
            switch (direction)
            {
                case MoveDirection.Left:
                    mLeftJumpTarget = target;
                    break;
                case MoveDirection.Right:
                    mRightJumpTarget = target;
                    break;
                case MoveDirection.Up:
                    mUpJumpTarget = target;
                    break;
                case MoveDirection.Down:
                    mDownJumpTarget = target;
                    break;
            }
        }

        /// <summary>
        /// 标记需要刷新
        /// </summary>
        public void SetDirty()
        {
            mIsDirty = true;
        }

        /// <summary>
        /// 配置组内元素的导航
        /// </summary>
        public void ConfigureNavigation()
        {
            RefreshSelectablesIfNeeded();
            
            if (mSelectables.Count == 0) return;

            for (int i = 0; i < mSelectables.Count; i++)
            {
                var selectable = mSelectables[i];
                if (selectable == null) continue;

                var nav = selectable.navigation;
                nav.mode = Navigation.Mode.Explicit;

                // 设置上下左右导航
                nav.selectOnLeft = GetNavigationTarget(i, MoveDirection.Left);
                nav.selectOnRight = GetNavigationTarget(i, MoveDirection.Right);
                nav.selectOnUp = GetNavigationTarget(i, MoveDirection.Up);
                nav.selectOnDown = GetNavigationTarget(i, MoveDirection.Down);

                selectable.navigation = nav;
            }
        }

        #endregion

        #region 私有方法

        private void RefreshSelectablesIfNeeded()
        {
            if (!mIsDirty && mSelectables != null) return;

            mSelectables ??= new List<Selectable>(8);
            mSelectables.Clear();

            GetComponentsInChildren(false, mSelectables);
            mIsDirty = false;
        }

        private Selectable GetNavigationTarget(int currentIndex, MoveDirection direction)
        {
            RefreshSelectablesIfNeeded();
            
            int targetIndex = GetTargetIndex(currentIndex, direction);
            
            if (targetIndex < 0 || targetIndex >= mSelectables.Count)
            {
                // 到达边界
                var behavior = GetBoundaryBehavior(direction);
                switch (behavior)
                {
                    case NavigationBoundaryBehavior.Stop:
                        return null;
                    
                    case NavigationBoundaryBehavior.Wrap:
                        targetIndex = direction is MoveDirection.Left or MoveDirection.Up 
                            ? mSelectables.Count - 1 
                            : 0;
                        break;
                    
                    case NavigationBoundaryBehavior.JumpToGroup:
                        var jumpTarget = GetJumpTarget(direction);
                        if (jumpTarget != default)
                        {
                            return jumpTarget.GetFirstSelectable();
                        }
                        return null;
                }
            }

            if (targetIndex >= 0 && targetIndex < mSelectables.Count)
            {
                return mSelectables[targetIndex];
            }
            return null;
        }

        private int GetTargetIndex(int currentIndex, MoveDirection direction)
        {
            // 简单的线性导航，可以根据需要扩展为网格导航
            return direction switch
            {
                MoveDirection.Left or MoveDirection.Up => currentIndex - 1,
                MoveDirection.Right or MoveDirection.Down => currentIndex + 1,
                _ => currentIndex
            };
        }

        #endregion
    }
}
