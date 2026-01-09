#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// UIKitToolPage - 验证器功能
    /// </summary>
    public partial class UIKitToolPage
    {
        #region 字段 - 验证器

        private GameObject mValidatorTargetPanel;
        private UIPanelValidator.ValidationResult mValidatorCurrentResult;
        private readonly List<UIPanelValidator.ValidationResult> mValidatorSceneResults = new(16);
        private bool mValidatorShowErrors = true;
        private bool mValidatorShowWarnings = true;
        private bool mValidatorShowInfo = true;
        private UIPanelValidator.IssueCategory? mValidatorCategoryFilter;
        private bool mValidatorAutoValidate = true;
        private VisualElement mValidatorContent;

        #endregion

        #region 验证器 UI

        private void BuildValidatorUI(VisualElement container)
        {
            // 工具栏
            var toolbar = new VisualElement();
            toolbar.style.flexDirection = FlexDirection.Row;
            toolbar.style.paddingLeft = Spacing.SM;
            toolbar.style.paddingRight = Spacing.SM;
            toolbar.style.paddingTop = Spacing.XS;
            toolbar.style.paddingBottom = Spacing.XS;
            toolbar.style.backgroundColor = new StyleColor(Colors.LayerToolbar);
            toolbar.style.borderBottomWidth = 1;
            toolbar.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            container.Add(toolbar);

            var validateSelectedBtn = new Button(() => SetValidatorTarget(Selection.activeGameObject)) { text = "验证选中" };
            validateSelectedBtn.style.height = 24;
            toolbar.Add(validateSelectedBtn);

            var validateSceneBtn = new Button(ValidateScene) { text = "验证场景" };
            validateSceneBtn.style.height = 24;
            validateSceneBtn.style.marginLeft = Spacing.XS;
            toolbar.Add(validateSceneBtn);

            toolbar.Add(new VisualElement { style = { flexGrow = 1 } });

            var autoValidateToggle = CreateModernToggle("自动验证", mValidatorAutoValidate, v => mValidatorAutoValidate = v);
            toolbar.Add(autoValidateToggle);

            var clearBtn = new Button(() =>
            {
                mValidatorCurrentResult = null;
                mValidatorSceneResults.Clear();
                mValidatorTargetPanel = null;
                RefreshValidatorContent();
            }) { text = "清除" };
            clearBtn.style.height = 24;
            clearBtn.style.marginLeft = Spacing.SM;
            toolbar.Add(clearBtn);

            // 目标选择栏
            var targetBar = new VisualElement();
            targetBar.style.flexDirection = FlexDirection.Row;
            targetBar.style.paddingLeft = Spacing.SM;
            targetBar.style.paddingRight = Spacing.SM;
            targetBar.style.paddingTop = Spacing.XS;
            targetBar.style.paddingBottom = Spacing.XS;
            targetBar.style.backgroundColor = new StyleColor(Colors.LayerFilterBar);
            container.Add(targetBar);

            targetBar.Add(new Label("验证目标:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginRight = Spacing.XS } });

            var targetField = new ObjectField();
            targetField.objectType = typeof(GameObject);
            targetField.value = mValidatorTargetPanel;
            targetField.style.width = 200;
            targetField.RegisterValueChangedCallback(evt => SetValidatorTarget(evt.newValue as GameObject));
            targetBar.Add(targetField);

            // 过滤栏
            var filterBar = new VisualElement();
            filterBar.style.flexDirection = FlexDirection.Row;
            filterBar.style.paddingLeft = Spacing.SM;
            filterBar.style.paddingRight = Spacing.SM;
            filterBar.style.paddingTop = Spacing.XS;
            filterBar.style.paddingBottom = Spacing.XS;
            filterBar.style.backgroundColor = new StyleColor(Colors.LayerFilterBar);
            filterBar.style.borderBottomWidth = 1;
            filterBar.style.borderBottomColor = new StyleColor(Colors.BorderLight);
            container.Add(filterBar);

            // 严重程度过滤
            filterBar.Add(CreateValidatorFilterButton("错误", mValidatorShowErrors, Colors.StatusError, v => { mValidatorShowErrors = v; RefreshValidatorContent(); }));
            filterBar.Add(CreateValidatorFilterButton("警告", mValidatorShowWarnings, Colors.StatusWarning, v => { mValidatorShowWarnings = v; RefreshValidatorContent(); }));
            filterBar.Add(CreateValidatorFilterButton("信息", mValidatorShowInfo, Colors.StatusInfo, v => { mValidatorShowInfo = v; RefreshValidatorContent(); }));

            filterBar.Add(new Label("类别:") { style = { unityTextAlign = TextAnchor.MiddleLeft, marginLeft = Spacing.MD, marginRight = Spacing.XS } });

            var categoryDropdown = new DropdownField();
            categoryDropdown.choices = new List<string> { "全部", "绑定", "引用", "Canvas", "动画", "焦点", "其他" };
            categoryDropdown.index = mValidatorCategoryFilter.HasValue ? (int)mValidatorCategoryFilter.Value + 1 : 0;
            categoryDropdown.style.width = 80;
            categoryDropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = categoryDropdown.choices.IndexOf(evt.newValue);
                mValidatorCategoryFilter = idx == 0 ? null : (UIPanelValidator.IssueCategory?)(idx - 1);
                RefreshValidatorContent();
            });
            filterBar.Add(categoryDropdown);

            // 内容区域
            mValidatorContent = new ScrollView();
            mValidatorContent.style.flexGrow = 1;
            mValidatorContent.style.paddingLeft = Spacing.MD;
            mValidatorContent.style.paddingRight = Spacing.MD;
            mValidatorContent.style.paddingTop = Spacing.MD;
            container.Add(mValidatorContent);

            RefreshValidatorContent();
        }

        private Button CreateValidatorFilterButton(string text, bool initialValue, Color activeColor, Action<bool> onChanged)
        {
            var btn = new Button();
            btn.text = text;
            btn.style.height = 24;
            btn.style.marginRight = Spacing.XS;

            void UpdateStyle(bool isActive)
            {
                btn.style.backgroundColor = new StyleColor(isActive ? activeColor : Colors.LayerCard);
                btn.style.color = new StyleColor(isActive ? Color.white : Colors.TextSecondary);
            }

            UpdateStyle(initialValue);
            bool currentValue = initialValue;

            btn.clicked += () =>
            {
                currentValue = !currentValue;
                UpdateStyle(currentValue);
                onChanged?.Invoke(currentValue);
            };

            return btn;
        }

        #endregion
    }
}
#endif
