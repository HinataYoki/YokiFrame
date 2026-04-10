using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// 自动导航配置器 - 自动为子元素配置导航关系
    /// </summary>
    [DisallowMultipleComponent]
    public class UIAutoNavigation : MonoBehaviour
    {
        #region 配置

        [Header("自动配置")]
        [Tooltip("启用时自动配置")]
        [SerializeField] private bool mConfigureOnEnable = true;

        [Tooltip("导航模式")]
        [SerializeField] private AutoNavigationMode mMode = AutoNavigationMode.Vertical;

        [Tooltip("是否循环")]
        [SerializeField] private bool mWrapAround;

        [Header("网格设置（仅 Grid 模式）")]
        [Tooltip("每行列数")]
        [SerializeField] private int mColumnsPerRow = 1;

        #endregion

        #region 缓存

        private readonly List<Selectable> mSelectables = new(16);

        #endregion

        #region 生命周期

        private void OnEnable()
        {
            if (mConfigureOnEnable)
            {
                Configure();
            }
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 配置导航
        /// </summary>
        public void Configure()
        {
            RefreshSelectables();

            if (mSelectables.Count == 0) return;

            switch (mMode)
            {
                case AutoNavigationMode.Horizontal:
                    ConfigureHorizontal();
                    break;
                case AutoNavigationMode.Vertical:
                    ConfigureVertical();
                    break;
                case AutoNavigationMode.Grid:
                    ConfigureGrid();
                    break;
            }
        }

        /// <summary>
        /// 刷新可选元素列表
        /// </summary>
        public void RefreshSelectables()
        {
            mSelectables.Clear();
            GetComponentsInChildren(false, mSelectables);
        }

        #endregion

        #region 配置方法

        private void ConfigureHorizontal()
        {
            for (int i = 0; i < mSelectables.Count; i++)
            {
                var selectable = mSelectables[i];
                var nav = selectable.navigation;
                nav.mode = Navigation.Mode.Explicit;

                // 左
                if (i > 0)
                {
                    nav.selectOnLeft = mSelectables[i - 1];
                }
                else if (mWrapAround)
                {
                    nav.selectOnLeft = mSelectables[^1];
                }

                // 右
                if (i < mSelectables.Count - 1)
                {
                    nav.selectOnRight = mSelectables[i + 1];
                }
                else if (mWrapAround)
                {
                    nav.selectOnRight = mSelectables[0];
                }

                selectable.navigation = nav;
            }
        }

        private void ConfigureVertical()
        {
            for (int i = 0; i < mSelectables.Count; i++)
            {
                var selectable = mSelectables[i];
                var nav = selectable.navigation;
                nav.mode = Navigation.Mode.Explicit;

                // 上
                if (i > 0)
                {
                    nav.selectOnUp = mSelectables[i - 1];
                }
                else if (mWrapAround)
                {
                    nav.selectOnUp = mSelectables[^1];
                }

                // 下
                if (i < mSelectables.Count - 1)
                {
                    nav.selectOnDown = mSelectables[i + 1];
                }
                else if (mWrapAround)
                {
                    nav.selectOnDown = mSelectables[0];
                }

                selectable.navigation = nav;
            }
        }

        private void ConfigureGrid()
        {
            int columns = Mathf.Max(1, mColumnsPerRow);
            int rows = Mathf.CeilToInt((float)mSelectables.Count / columns);

            for (int i = 0; i < mSelectables.Count; i++)
            {
                var selectable = mSelectables[i];
                var nav = selectable.navigation;
                nav.mode = Navigation.Mode.Explicit;

                int row = i / columns;
                int col = i % columns;

                // 上
                int upIndex = (row - 1) * columns + col;
                if (upIndex >= 0)
                {
                    nav.selectOnUp = mSelectables[upIndex];
                }
                else if (mWrapAround)
                {
                    int wrapIndex = (rows - 1) * columns + col;
                    if (wrapIndex < mSelectables.Count)
                    {
                        nav.selectOnUp = mSelectables[wrapIndex];
                    }
                }

                // 下
                int downIndex = (row + 1) * columns + col;
                if (downIndex < mSelectables.Count)
                {
                    nav.selectOnDown = mSelectables[downIndex];
                }
                else if (mWrapAround)
                {
                    nav.selectOnDown = mSelectables[col];
                }

                // 左
                if (col > 0)
                {
                    nav.selectOnLeft = mSelectables[i - 1];
                }
                else if (mWrapAround)
                {
                    int wrapIndex = row * columns + columns - 1;
                    if (wrapIndex < mSelectables.Count)
                    {
                        nav.selectOnLeft = mSelectables[wrapIndex];
                    }
                }

                // 右
                if (col < columns - 1 && i + 1 < mSelectables.Count)
                {
                    nav.selectOnRight = mSelectables[i + 1];
                }
                else if (mWrapAround)
                {
                    nav.selectOnRight = mSelectables[row * columns];
                }

                selectable.navigation = nav;
            }
        }

        #endregion
    }

    /// <summary>
    /// 自动导航模式
    /// </summary>
    public enum AutoNavigationMode
    {
        /// <summary>
        /// 水平导航（左右）
        /// </summary>
        Horizontal,

        /// <summary>
        /// 垂直导航（上下）
        /// </summary>
        Vertical,

        /// <summary>
        /// 网格导航（上下左右）
        /// </summary>
        Grid
    }
}
