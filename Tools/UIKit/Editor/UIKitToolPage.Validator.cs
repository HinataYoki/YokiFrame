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
            toolbar.AddToClassList("yoki-ui-validator-toolbar");
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
            targetBar.AddToClassList("yoki-ui-validator-target-bar");
            container.Add(targetBar);

            var targetLabel = new Label("验证目标:");
            targetLabel.AddToClassList("yoki-ui-toolbar__label");
            targetBar.Add(targetLabel);

            var targetField = new ObjectField();
            targetField.objectType = typeof(GameObject);
            targetField.value = mValidatorTargetPanel;
            targetField.AddToClassList("yoki-ui-toolbar__target-field");
            targetField.RegisterValueChangedCallback(evt => SetValidatorTarget(evt.newValue as GameObject));
            targetBar.Add(targetField);

            // 过滤栏
            var filterBar = new VisualElement();
            filterBar.AddToClassList("yoki-ui-validator-filter-bar");
            container.Add(filterBar);

            // 严重程度过滤
            filterBar.Add(CreateValidatorFilterButton("错误", mValidatorShowErrors, Colors.StatusError, v => { mValidatorShowErrors = v; RefreshValidatorContent(); }));
            filterBar.Add(CreateValidatorFilterButton("警告", mValidatorShowWarnings, Colors.StatusWarning, v => { mValidatorShowWarnings = v; RefreshValidatorContent(); }));
            filterBar.Add(CreateValidatorFilterButton("信息", mValidatorShowInfo, Colors.StatusInfo, v => { mValidatorShowInfo = v; RefreshValidatorContent(); }));

            var categoryLabel = new Label("类别:");
            categoryLabel.AddToClassList("yoki-ui-toolbar__label");
            categoryLabel.style.marginLeft = Spacing.MD;
            filterBar.Add(categoryLabel);

            var categoryDropdown = new DropdownField();
            categoryDropdown.choices = new List<string> { "全部", "绑定", "引用", "Canvas", "动画", "焦点", "其他" };
            categoryDropdown.index = mValidatorCategoryFilter.HasValue ? (int)mValidatorCategoryFilter.Value + 1 : 0;
            categoryDropdown.AddToClassList("yoki-ui-validator-category-dropdown");
            categoryDropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = categoryDropdown.choices.IndexOf(evt.newValue);
                mValidatorCategoryFilter = idx == 0 ? null : (UIPanelValidator.IssueCategory?)(idx - 1);
                RefreshValidatorContent();
            });
            filterBar.Add(categoryDropdown);

            // 内容区域
            mValidatorContent = new ScrollView();
            mValidatorContent.AddToClassList("yoki-ui-validator-content");
            container.Add(mValidatorContent);

            RefreshValidatorContent();
        }

        private Button CreateValidatorFilterButton(string text, bool initialValue, Color activeColor, Action<bool> onChanged)
        {
            var btn = new Button();
            btn.text = text;
            btn.AddToClassList("yoki-ui-validator-filter-button");

            void UpdateStyle(bool isActive)
            {
                btn.RemoveFromClassList("yoki-ui-validator-filter-button--error");
                btn.RemoveFromClassList("yoki-ui-validator-filter-button--warning");
                btn.RemoveFromClassList("yoki-ui-validator-filter-button--info");
                btn.RemoveFromClassList("yoki-ui-validator-filter-button--inactive");

                if (isActive)
                {
                    if (activeColor == Colors.StatusError)
                        btn.AddToClassList("yoki-ui-validator-filter-button--error");
                    else if (activeColor == Colors.StatusWarning)
                        btn.AddToClassList("yoki-ui-validator-filter-button--warning");
                    else if (activeColor == Colors.StatusInfo)
                        btn.AddToClassList("yoki-ui-validator-filter-button--info");
                }
                else
                {
                    btn.AddToClassList("yoki-ui-validator-filter-button--inactive");
                }
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
