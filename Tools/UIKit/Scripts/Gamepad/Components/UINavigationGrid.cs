using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 网格导航布局 - 支持二维网格形式的 UI 导航
    /// </summary>
    [DisallowMultipleComponent]
    public class UINavigationGrid : MonoBehaviour
    {
        #region 配置

        [Header("网格设置")]
        [Tooltip("每行元素数量（0 表示自动计算）")]
        [SerializeField] private int mColumnsPerRow;

        [Tooltip("导航模式")]
        [SerializeField] private GridNavigationMode mNavigationMode = GridNavigationMode.Automatic;

        [Header("边界行为")]
        [SerializeField] private NavigationBoundaryBehavior mLeftBoundary = NavigationBoundaryBehavior.Stop;
        [SerializeField] private NavigationBoundaryBehavior mRightBoundary = NavigationBoundaryBehavior.Stop;
        [SerializeField] private NavigationBoundaryBehavior mUpBoundary = NavigationBoundaryBehavior.Stop;
        [SerializeField] private NavigationBoundaryBehavior mDownBoundary = NavigationBoundaryBehavior.Stop;

        [Header("跳转目标")]
        [SerializeField] private UINavigationGrid mLeftJumpTarget;
        [SerializeField] private UINavigationGrid mRightJumpTarget;
        [SerializeField] private UINavigationGrid mUpJumpTarget;
        [SerializeField] private UINavigationGrid mDownJumpTarget;

        [Header("默认选中")]
        [SerializeField] private Selectable mDefaultSelectable;

        #endregion

        #region 缓存

        private readonly List<Selectable> mSelectables = new(16);
        private bool mIsDirty = true;
        private int mCachedColumns;

        #endregion

        #region 属性

        /// <summary>
        /// 每行列数
        /// </summary>
        public int ColumnsPerRow
        {
            get => mColumnsPerRow;
            set
            {
                mColumnsPerRow = value;
                mIsDirty = true;
            }
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
        /// 获取所有可选元素
        /// </summary>
        public IReadOnlyList<Selectable> GetSelectables()
        {
            RefreshIfNeeded();
            return mSelectables;
        }

        /// <summary>
        /// 获取第一个可交互元素
        /// </summary>
        public Selectable GetFirstSelectable()
        {
            if (mDefaultSelectable != null && mDefaultSelectable.interactable)
            {
                return mDefaultSelectable;
            }

            RefreshIfNeeded();
            for (int i = 0; i < mSelectables.Count; i++)
            {
                if (mSelectables[i] != null && mSelectables[i].interactable)
                {
                    return mSelectables[i];
                }
            }
            return null;
        }

        /// <summary>
        /// 获取指定位置的元素
        /// </summary>
        public Selectable GetSelectableAt(int row, int column)
        {
            RefreshIfNeeded();
            int index = row * mCachedColumns + column;
            if (index >= 0 && index < mSelectables.Count)
            {
                return mSelectables[index];
            }
            return null;
        }

        /// <summary>
        /// 获取元素的行列位置
        /// </summary>
        public (int row, int column) GetPosition(Selectable selectable)
        {
            RefreshIfNeeded();
            int index = mSelectables.IndexOf(selectable);
            if (index < 0) return (-1, -1);
            return (index / mCachedColumns, index % mCachedColumns);
        }

        /// <summary>
        /// 配置网格导航
        /// </summary>
        public void ConfigureNavigation()
        {
            RefreshIfNeeded();
            if (mSelectables.Count == 0) return;

            for (int i = 0; i < mSelectables.Count; i++)
            {
                var selectable = mSelectables[i];
                if (selectable == null) continue;

                int row = i / mCachedColumns;
                int col = i % mCachedColumns;

                var nav = selectable.navigation;
                nav.mode = Navigation.Mode.Explicit;

                nav.selectOnUp = GetNavigationTarget(row, col, MoveDirection.Up);
                nav.selectOnDown = GetNavigationTarget(row, col, MoveDirection.Down);
                nav.selectOnLeft = GetNavigationTarget(row, col, MoveDirection.Left);
                nav.selectOnRight = GetNavigationTarget(row, col, MoveDirection.Right);

                selectable.navigation = nav;
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
        /// 设置边界跳转目标
        /// </summary>
        public void SetJumpTarget(MoveDirection direction, UINavigationGrid target)
        {
            switch (direction)
            {
                case MoveDirection.Left: mLeftJumpTarget = target; break;
                case MoveDirection.Right: mRightJumpTarget = target; break;
                case MoveDirection.Up: mUpJumpTarget = target; break;
                case MoveDirection.Down: mDownJumpTarget = target; break;
            }
        }

        #endregion

        #region 私有方法

        private void RefreshIfNeeded()
        {
            if (!mIsDirty) return;
            mIsDirty = false;

            mSelectables.Clear();
            GetComponentsInChildren(false, mSelectables);

            // 计算列数
            mCachedColumns = mColumnsPerRow > 0 ? mColumnsPerRow : CalculateColumns();
        }

        private int CalculateColumns()
        {
            if (mNavigationMode == GridNavigationMode.Automatic && mSelectables.Count > 0)
            {
                // 尝试从 GridLayoutGroup 获取
                var grid = GetComponent<GridLayoutGroup>();
                if (grid != null && grid.constraint == GridLayoutGroup.Constraint.FixedColumnCount)
                {
                    return grid.constraintCount;
                }

                // 尝试从 HorizontalLayoutGroup 推断
                if (GetComponent<HorizontalLayoutGroup>() != null)
                {
                    return mSelectables.Count;
                }

                // 尝试从 VerticalLayoutGroup 推断
                if (GetComponent<VerticalLayoutGroup>() != null)
                {
                    return 1;
                }

                // 默认：基于位置计算
                return CalculateColumnsFromPositions();
            }

            return Mathf.Max(1, mColumnsPerRow);
        }

        private int CalculateColumnsFromPositions()
        {
            if (mSelectables.Count <= 1) return 1;

            // 统计同一行的元素数量
            float firstY = mSelectables[0].transform.position.y;
            int count = 1;
            const float TOLERANCE = 10f;

            for (int i = 1; i < mSelectables.Count; i++)
            {
                if (Mathf.Abs(mSelectables[i].transform.position.y - firstY) < TOLERANCE)
                {
                    count++;
                }
                else
                {
                    break;
                }
            }

            return count;
        }

        private Selectable GetNavigationTarget(int row, int col, MoveDirection direction)
        {
            int targetRow = row;
            int targetCol = col;
            int totalRows = Mathf.CeilToInt((float)mSelectables.Count / mCachedColumns);

            switch (direction)
            {
                case MoveDirection.Up:
                    targetRow--;
                    break;
                case MoveDirection.Down:
                    targetRow++;
                    break;
                case MoveDirection.Left:
                    targetCol--;
                    break;
                case MoveDirection.Right:
                    targetCol++;
                    break;
            }

            // 检查边界
            bool outOfBounds = targetRow < 0 || targetRow >= totalRows ||
                              targetCol < 0 || targetCol >= mCachedColumns;

            if (outOfBounds)
            {
                return HandleBoundary(direction, row, col, totalRows);
            }

            // 检查目标索引是否有效
            int targetIndex = targetRow * mCachedColumns + targetCol;
            if (targetIndex >= 0 && targetIndex < mSelectables.Count)
            {
                var target = mSelectables[targetIndex];
                if (target != null && target.interactable)
                {
                    return target;
                }
            }

            return null;
        }

        private Selectable HandleBoundary(MoveDirection direction, int row, int col, int totalRows)
        {
            var behavior = GetBoundaryBehavior(direction);

            switch (behavior)
            {
                case NavigationBoundaryBehavior.Stop:
                    return null;

                case NavigationBoundaryBehavior.Wrap:
                    return GetWrapTarget(direction, row, col, totalRows);

                case NavigationBoundaryBehavior.JumpToGroup:
                    var jumpTarget = GetJumpTarget(direction);
                    return jumpTarget?.GetFirstSelectable();

                default:
                    return null;
            }
        }

        private Selectable GetWrapTarget(MoveDirection direction, int row, int col, int totalRows)
        {
            int targetRow = row;
            int targetCol = col;

            switch (direction)
            {
                case MoveDirection.Up:
                    targetRow = totalRows - 1;
                    break;
                case MoveDirection.Down:
                    targetRow = 0;
                    break;
                case MoveDirection.Left:
                    targetCol = mCachedColumns - 1;
                    break;
                case MoveDirection.Right:
                    targetCol = 0;
                    break;
            }

            int targetIndex = targetRow * mCachedColumns + targetCol;
            if (targetIndex >= 0 && targetIndex < mSelectables.Count)
            {
                return mSelectables[targetIndex];
            }
            return null;
        }

        private NavigationBoundaryBehavior GetBoundaryBehavior(MoveDirection direction)
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

        private UINavigationGrid GetJumpTarget(MoveDirection direction)
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

        #endregion
    }

    /// <summary>
    /// 网格导航模式
    /// </summary>
    public enum GridNavigationMode
    {
        /// <summary>
        /// 自动检测布局
        /// </summary>
        Automatic,

        /// <summary>
        /// 手动指定列数
        /// </summary>
        Manual
    }
}
