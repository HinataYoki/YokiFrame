#if UNITY_2022_3_OR_NEWER
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>按横向、纵向或网格规则自动写入 Selectable 导航关系。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Auto Navigation")]
    public sealed class UIAutoNavigation : MonoBehaviour
    {
        [SerializeField] private bool mConfigureOnEnable = true;
        [SerializeField] private AutoNavigationMode mMode = AutoNavigationMode.Vertical;
        [SerializeField] private bool mWrapAround;
        [SerializeField] private int mColumnsPerRow = 1;
        private readonly List<Selectable> mSelectables = new(16);

        /// <summary>启用时按配置自动生成导航。</summary>
        private void OnEnable()
        {
            if (mConfigureOnEnable) Configure();
        }

        /// <summary>扫描子控件并按当前模式配置导航。</summary>
        public void Configure()
        {
            RefreshSelectables();
            for (var index = 0; index < mSelectables.Count; index++)
            {
                Navigation navigation = mSelectables[index].navigation;
                navigation.mode = Navigation.Mode.Explicit;
                navigation.selectOnLeft = FindTarget(index, -1, 0);
                navigation.selectOnRight = FindTarget(index, 1, 0);
                navigation.selectOnUp = FindTarget(index, 0, -1);
                navigation.selectOnDown = FindTarget(index, 0, 1);
                mSelectables[index].navigation = navigation;
            }
        }

        /// <summary>刷新子树内可交互控件列表。</summary>
        public void RefreshSelectables()
        {
            mSelectables.Clear();
            GetComponentsInChildren(false, mSelectables);
        }

        /// <summary>根据模式和方向计算目标控件。</summary>
        private Selectable FindTarget(int index, int horizontal, int vertical)
        {
            int targetIndex = index;
            if (mMode == AutoNavigationMode.Horizontal) targetIndex += horizontal;
            else if (mMode == AutoNavigationMode.Vertical) targetIndex += vertical;
            else
            {
                int columns = Mathf.Max(1, mColumnsPerRow);
                targetIndex += horizontal + vertical * columns;
            }

            if (targetIndex >= 0 && targetIndex < mSelectables.Count) return mSelectables[targetIndex];
            if (!mWrapAround || mSelectables.Count == 0) return null;
            if (horizontal < 0 || vertical < 0) return mSelectables[mSelectables.Count - 1];
            return mSelectables[0];
        }
    }
}
#endif
