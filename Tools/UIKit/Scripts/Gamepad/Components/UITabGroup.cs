using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame
{
    /// <summary>
    /// Tab 组件 - 支持 LB/RB 切换的标签页组
    /// </summary>
    public class UITabGroup : MonoBehaviour
    {
        #region 配置

        [Header("Tab 配置")]
        [Tooltip("Tab 按钮列表")]
        [SerializeField] private List<Selectable> mTabButtons = new();

        [Tooltip("Tab 内容列表（与按钮一一对应）")]
        [SerializeField] private List<GameObject> mTabContents = new();

        [Tooltip("是否循环切换")]
        [SerializeField] private bool mWrapAround = true;

        [Tooltip("默认选中的 Tab 索引")]
        [SerializeField] private int mDefaultIndex;

        [Header("视觉反馈")]
        [Tooltip("选中状态的颜色")]
        [SerializeField] private Color mSelectedColor = Color.white;

        [Tooltip("未选中状态的颜色")]
        [SerializeField] private Color mNormalColor = new(0.8f, 0.8f, 0.8f, 1f);

        #endregion

        #region 事件

        /// <summary>
        /// Tab 切换事件（参数：新索引）
        /// </summary>
        public event Action<int> OnTabChanged;

        #endregion

        #region 状态

        private int mCurrentIndex;
        private bool mIsInitialized;

        #endregion

        #region 属性

        /// <summary>
        /// 当前选中的 Tab 索引
        /// </summary>
        public int CurrentIndex => mCurrentIndex;

        /// <summary>
        /// Tab 数量
        /// </summary>
        public int TabCount => mTabButtons.Count;

        /// <summary>
        /// 当前选中的 Tab 按钮
        /// </summary>
        public Selectable CurrentTabButton => 
            mCurrentIndex >= 0 && mCurrentIndex < mTabButtons.Count ? mTabButtons[mCurrentIndex] : null;

        /// <summary>
        /// 当前显示的内容
        /// </summary>
        public GameObject CurrentContent =>
            mCurrentIndex >= 0 && mCurrentIndex < mTabContents.Count ? mTabContents[mCurrentIndex] : null;

        #endregion

        #region 生命周期

        private void Awake()
        {
            Initialize();
        }

        private void OnEnable()
        {
            // 订阅手柄 Tab 切换事件
            EventKit.Type.Register<GamepadTabSwitchEvent>(OnGamepadTabSwitch).UnRegisterWhenDisabled(this);
        }

        #endregion

        #region 初始化

        private void Initialize()
        {
            if (mIsInitialized) return;
            mIsInitialized = true;

            // 绑定按钮点击事件
            for (int i = 0; i < mTabButtons.Count; i++)
            {
                int index = i; // 闭包捕获
                var button = mTabButtons[i] as Button;
                if (button != null)
                {
                    button.onClick.AddListener(() => SelectTab(index));
                }
            }

            // 设置默认选中
            SelectTab(mDefaultIndex, false);
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 选中指定索引的 Tab
        /// </summary>
        public void SelectTab(int index, bool notify = true)
        {
            if (index < 0 || index >= mTabButtons.Count) return;
            if (index == mCurrentIndex && mIsInitialized) return;

            int previousIndex = mCurrentIndex;
            mCurrentIndex = index;

            UpdateVisuals();

            if (notify)
            {
                OnTabChanged?.Invoke(index);
                EventKit.Type.Send(new GamepadTabSwitchEvent
                {
                    Direction = index > previousIndex ? 1 : -1,
                    PreviousIndex = previousIndex,
                    CurrentIndex = index
                });
            }
        }

        /// <summary>
        /// 切换到下一个 Tab
        /// </summary>
        public void NextTab()
        {
            int nextIndex = mCurrentIndex + 1;
            if (nextIndex >= mTabButtons.Count)
            {
                nextIndex = mWrapAround ? 0 : mTabButtons.Count - 1;
            }
            SelectTab(nextIndex);
        }

        /// <summary>
        /// 切换到上一个 Tab
        /// </summary>
        public void PreviousTab()
        {
            int prevIndex = mCurrentIndex - 1;
            if (prevIndex < 0)
            {
                prevIndex = mWrapAround ? mTabButtons.Count - 1 : 0;
            }
            SelectTab(prevIndex);
        }

        /// <summary>
        /// 根据方向切换 Tab
        /// </summary>
        public void SwitchTab(int direction)
        {
            if (direction > 0)
            {
                NextTab();
            }
            else if (direction < 0)
            {
                PreviousTab();
            }
        }

        /// <summary>
        /// 添加 Tab
        /// </summary>
        public void AddTab(Selectable button, GameObject content)
        {
            if (button == null) return;

            mTabButtons.Add(button);
            if (content != null)
            {
                mTabContents.Add(content);
            }

            // 绑定点击事件
            int index = mTabButtons.Count - 1;
            var btn = button as Button;
            if (btn != null)
            {
                btn.onClick.AddListener(() => SelectTab(index));
            }

            UpdateVisuals();
        }

        /// <summary>
        /// 移除 Tab
        /// </summary>
        public void RemoveTab(int index)
        {
            if (index < 0 || index >= mTabButtons.Count) return;

            mTabButtons.RemoveAt(index);
            if (index < mTabContents.Count)
            {
                mTabContents.RemoveAt(index);
            }

            // 调整当前索引
            if (mCurrentIndex >= mTabButtons.Count)
            {
                mCurrentIndex = Mathf.Max(0, mTabButtons.Count - 1);
            }

            UpdateVisuals();
        }

        /// <summary>
        /// 获取 Tab 内容中的第一个可选元素
        /// </summary>
        public Selectable GetFirstSelectableInCurrentTab()
        {
            var content = CurrentContent;
            if (content == null) return null;

            return content.GetComponentInChildren<Selectable>();
        }

        #endregion

        #region 私有方法

        private void OnGamepadTabSwitch(GamepadTabSwitchEvent evt)
        {
            // 只处理来自手柄的切换（非本组件发出的）
            if (evt.CurrentIndex == mCurrentIndex) return;
            SwitchTab(evt.Direction);
        }

        private void UpdateVisuals()
        {
            // 更新按钮颜色
            for (int i = 0; i < mTabButtons.Count; i++)
            {
                var button = mTabButtons[i];
                if (button == null) continue;

                bool isSelected = i == mCurrentIndex;
                var colors = button.colors;
                colors.normalColor = isSelected ? mSelectedColor : mNormalColor;
                button.colors = colors;
            }

            // 更新内容显示
            for (int i = 0; i < mTabContents.Count; i++)
            {
                var content = mTabContents[i];
                if (content != null)
                {
                    content.SetActive(i == mCurrentIndex);
                }
            }
        }

        #endregion
    }
}
