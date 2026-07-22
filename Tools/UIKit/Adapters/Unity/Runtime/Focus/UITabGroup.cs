#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>管理成对的 Tab 按钮与内容，并提供前后切换入口。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("YokiFrame/UIKit/Tab Group")]
    public sealed class UITabGroup : MonoBehaviour
    {
        [SerializeField] private List<Selectable> mTabButtons = new();
        [SerializeField] private List<GameObject> mTabContents = new();
        [SerializeField] private bool mWrapAround = true;
        [SerializeField] private int mDefaultIndex;
        [SerializeField] private Color mSelectedColor = Color.white;
        [SerializeField] private Color mNormalColor = new(0.8f, 0.8f, 0.8f, 1f);
        private int mCurrentIndex = -1;
        private bool mInitialized;

        /// <summary>Tab 切换事件。</summary>
        public event Action<int> OnTabChanged;

        /// <summary>当前 Tab 索引。</summary>
        public int CurrentIndex => mCurrentIndex;

        /// <summary>当前 Tab 数量。</summary>
        public int TabCount => mTabButtons.Count;

        /// <summary>当前 Tab 按钮。</summary>
        public Selectable CurrentTabButton => IsValidIndex(mCurrentIndex) ? mTabButtons[mCurrentIndex] : null;

        /// <summary>当前 Tab 内容。</summary>
        public GameObject CurrentContent => mCurrentIndex >= 0 && mCurrentIndex < mTabContents.Count ? mTabContents[mCurrentIndex] : null;

        /// <summary>初始化按钮回调和默认 Tab。</summary>
        private void Awake()
        {
            Initialize();
        }

        /// <summary>选择指定 Tab 并同步视觉与内容显隐。</summary>
        public void SelectTab(int index, bool notify = true)
        {
            if (!IsValidIndex(index) || index == mCurrentIndex) return;
            mCurrentIndex = index;
            UpdateVisuals();
            if (notify && OnTabChanged != null) OnTabChanged(index);
        }

        /// <summary>切换到下一个 Tab。</summary>
        public void NextTab()
        {
            if (mTabButtons.Count == 0) return;
            int index = mCurrentIndex + 1;
            if (index >= mTabButtons.Count) index = mWrapAround ? 0 : mTabButtons.Count - 1;
            SelectTab(index);
        }

        /// <summary>切换到上一个 Tab。</summary>
        public void PreviousTab()
        {
            if (mTabButtons.Count == 0) return;
            int index = mCurrentIndex - 1;
            if (index < 0) index = mWrapAround ? mTabButtons.Count - 1 : 0;
            SelectTab(index);
        }

        /// <summary>按正负方向切换 Tab。</summary>
        public void SwitchTab(int direction)
        {
            if (direction > 0) NextTab();
            else if (direction < 0) PreviousTab();
        }

        /// <summary>追加一组 Tab 按钮和内容。</summary>
        public void AddTab(Selectable button, GameObject content)
        {
            if (button == null) return;
            int index = mTabButtons.Count;
            mTabButtons.Add(button);
            mTabContents.Add(content);
            Button clickable = button as Button;
            if (clickable != null) clickable.onClick.AddListener(() => SelectTab(index));
            if (mCurrentIndex < 0) SelectTab(0, false);
            else UpdateVisuals();
        }

        /// <summary>移除指定索引的 Tab。</summary>
        public void RemoveTab(int index)
        {
            if (!IsValidIndex(index)) return;
            mTabButtons.RemoveAt(index);
            if (index < mTabContents.Count) mTabContents.RemoveAt(index);
            mCurrentIndex = mTabButtons.Count == 0
                ? -1
                : Mathf.Clamp(mCurrentIndex, 0, mTabButtons.Count - 1);
            UpdateVisuals();
        }

        /// <summary>获取当前内容中的第一个可交互控件。</summary>
        public Selectable GetFirstSelectableInCurrentTab()
        {
            return CurrentContent != null ? UIRoot.FindFirstSelectable(CurrentContent.transform) : null;
        }

        /// <summary>绑定初始 Button 点击并应用默认索引。</summary>
        private void Initialize()
        {
            if (mInitialized) return;
            mInitialized = true;
            for (var index = 0; index < mTabButtons.Count; index++)
            {
                Button button = mTabButtons[index] as Button;
                if (button == null) continue;
                int capturedIndex = index;
                button.onClick.AddListener(() => SelectTab(capturedIndex));
            }
            if (mTabButtons.Count > 0) SelectTab(Mathf.Clamp(mDefaultIndex, 0, mTabButtons.Count - 1), false);
        }

        /// <summary>同步按钮颜色和内容显隐。</summary>
        private void UpdateVisuals()
        {
            for (var index = 0; index < mTabButtons.Count; index++)
            {
                Selectable button = mTabButtons[index];
                if (button == null) continue;
                ColorBlock colors = button.colors;
                colors.normalColor = index == mCurrentIndex ? mSelectedColor : mNormalColor;
                button.colors = colors;
            }
            for (var index = 0; index < mTabContents.Count; index++)
                if (mTabContents[index] != null) mTabContents[index].SetActive(index == mCurrentIndex);
        }

        /// <summary>判断按钮索引是否有效。</summary>
        private bool IsValidIndex(int index)
        {
            return index >= 0 && index < mTabButtons.Count;
        }
    }
}
#endif
