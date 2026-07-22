#if UNITY_2022_3_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>以行列布局为 Selectable 提供显式导航关系和默认焦点。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Navigation Grid")]
    public sealed class UINavigationGrid : MonoBehaviour
    {
        [SerializeField] private int mColumns = 1;
        [SerializeField] private bool mWrapAround;
        [SerializeField] private Selectable mDefaultSelectable;
        private readonly List<Selectable> mSelectables = new(16);

        /// <summary>获取或设置网格列数。</summary>
        public int Columns
        {
            get { return mColumns; }
            set { mColumns = Mathf.Max(1, value); }
        }

        /// <summary>获取网格中的第一个可交互控件。</summary>
        public Selectable GetFirstSelectable()
        {
            if (mDefaultSelectable != null && mDefaultSelectable.interactable) return mDefaultSelectable;
            Refresh();
            for (var index = 0; index < mSelectables.Count; index++)
                if (mSelectables[index].interactable) return mSelectables[index];
            return null;
        }

        /// <summary>配置网格中所有控件的 Explicit Navigation。</summary>
        public void Configure()
        {
            Refresh();
            int columns = Mathf.Max(1, mColumns);
            for (var index = 0; index < mSelectables.Count; index++)
            {
                int row = index / columns;
                int column = index % columns;
                Navigation navigation = mSelectables[index].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = Resolve(index, row, column - 1, columns, true);
                navigation.selectOnRight = Resolve(index, row, column + 1, columns, true);
                navigation.selectOnUp = Resolve(index, row - 1, column, columns, false);
                navigation.selectOnDown = Resolve(index, row + 1, column, columns, false);
                mSelectables[index].navigation = navigation;
            }
        }

        /// <summary>刷新网格控件缓存。</summary>
        private void Refresh()
        {
            mSelectables.Clear();
            GetComponentsInChildren(false, mSelectables);
        }

        /// <summary>按目标行列查找控件并处理循环边界。</summary>
        private Selectable Resolve(int origin, int row, int column, int columns, bool horizontal)
        {
            int index = row * columns + column;
            if (index >= 0 && index < mSelectables.Count) return mSelectables[index];
            if (!mWrapAround) return null;
            if (horizontal)
            {
                int wrappedColumn = column < 0 ? columns - 1 : 0;
                index = row * columns + wrappedColumn;
            }
            else
            {
                int rowCount = (mSelectables.Count + columns - 1) / columns;
                index = (row < 0 ? rowCount - 1 : 0) * columns + column;
            }
            return index >= 0 && index < mSelectables.Count ? mSelectables[index] : null;
        }
    }
}
#endif
