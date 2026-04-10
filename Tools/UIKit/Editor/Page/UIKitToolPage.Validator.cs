#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.EditorTools;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame
{
    /// <summary>
    /// UIKit 工具页中的批量验证中心。
    /// 用于对当前场景中的全部 UIPanel 执行结构与约束检查，并集中查看问题清单。
    /// </summary>
    public partial class UIKitToolPage
    {
        #region 字段 - 验证器

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
            var toolbar = CreateToolbar();
            toolbar.AddToClassList("yoki-ui-validator-toolbar");
            container.Add(toolbar);

            var validateSceneBtn = new Button(ValidateScene) { text = "扫描当前场景" };
            validateSceneBtn.style.height = 24;
            toolbar.Add(validateSceneBtn);

            var locateSelectionBtn = new Button(() =>
            {
                if (Selection.activeGameObject != null)
                {
                    EditorGUIUtility.PingObject(Selection.activeGameObject);
                }
            })
            {
                text = "定位当前选中"
            };
            locateSelectionBtn.style.height = 24;
            locateSelectionBtn.style.marginLeft = Spacing.XS;
            toolbar.Add(locateSelectionBtn);

            toolbar.Add(CreateToolbarSpacer());

            var autoValidateToggle = CreateModernToggle("自动扫描", mValidatorAutoValidate, value => mValidatorAutoValidate = value);
            toolbar.Add(autoValidateToggle);

            var clearBtn = new Button(() =>
            {
                mValidatorSceneResults.Clear();
                RefreshValidatorContent();
            })
            {
                text = "清除结果"
            };
            clearBtn.style.height = 24;
            clearBtn.style.marginLeft = Spacing.SM;
            toolbar.Add(clearBtn);

            var filterBar = new VisualElement();
            filterBar.AddToClassList("yoki-ui-validator-filter-bar");
            container.Add(filterBar);

            filterBar.Add(CreateValidatorFilterButton("错误", mValidatorShowErrors, Colors.StatusError, value =>
            {
                mValidatorShowErrors = value;
                RefreshValidatorContent();
            }));
            filterBar.Add(CreateValidatorFilterButton("警告", mValidatorShowWarnings, Colors.StatusWarning, value =>
            {
                mValidatorShowWarnings = value;
                RefreshValidatorContent();
            }));
            filterBar.Add(CreateValidatorFilterButton("信息", mValidatorShowInfo, Colors.StatusInfo, value =>
            {
                mValidatorShowInfo = value;
                RefreshValidatorContent();
            }));

            var categoryLabel = new Label("类别:");
            categoryLabel.AddToClassList("yoki-ui-toolbar__label");
            categoryLabel.style.marginLeft = Spacing.MD;
            filterBar.Add(categoryLabel);

            var categoryDropdown = new DropdownField
            {
                choices = new List<string> { "全部", "绑定", "引用", "Canvas", "动画", "焦点", "其他" },
                index = mValidatorCategoryFilter.HasValue ? (int)mValidatorCategoryFilter.Value + 1 : 0
            };
            categoryDropdown.AddToClassList("yoki-ui-validator-category-dropdown");
            categoryDropdown.RegisterValueChangedCallback(evt =>
            {
                var idx = categoryDropdown.choices.IndexOf(evt.newValue);
                mValidatorCategoryFilter = idx == 0 ? null : (UIPanelValidator.IssueCategory?)(idx - 1);
                RefreshValidatorContent();
            });
            filterBar.Add(categoryDropdown);

            var scrollView = new ScrollView();
            scrollView.AddToClassList("yoki-ui-validator-content");
            scrollView.style.flexGrow = 1;
            container.Add(scrollView);

            var (section, body) = CreateKitSectionPanel("场景批量验证", "扫描当前场景中的 UIPanel，并给出统一的问题摘要和跳转入口。", KitIcons.CHECK);
            scrollView.Add(section);
            mValidatorContent = body;

            RefreshValidatorContent();
        }

        private Button CreateValidatorFilterButton(string text, bool initialValue, Color activeColor, Action<bool> onChanged)
        {
            var btn = new Button { text = text };
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
                    {
                        btn.AddToClassList("yoki-ui-validator-filter-button--error");
                    }
                    else if (activeColor == Colors.StatusWarning)
                    {
                        btn.AddToClassList("yoki-ui-validator-filter-button--warning");
                    }
                    else if (activeColor == Colors.StatusInfo)
                    {
                        btn.AddToClassList("yoki-ui-validator-filter-button--info");
                    }
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
