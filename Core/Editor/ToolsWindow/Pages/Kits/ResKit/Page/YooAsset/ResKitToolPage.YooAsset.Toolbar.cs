#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - YooAsset 工具栏。
    /// </summary>
    public partial class ResKitToolPage
    {
        /// <summary>
        /// 构建 YooAsset 工具栏。
        /// </summary>
        private VisualElement BuildYooToolbar()
        {
            var toolbar = CreateToolbar();

            mYooPackageDropdown = new DropdownField();
            mYooPackageDropdown.AddToClassList("yoo-package-dropdown");
            mYooPackageDropdown.style.minWidth = 150;
            mYooPackageDropdown.style.maxWidth = 200;
            mYooPackageDropdown.style.marginRight = 4;
            mYooPackageDropdown.style.flexGrow = 0;
            mYooPackageDropdown.style.flexShrink = 0;
            mYooPackageDropdown.RegisterValueChangedCallback(OnYooPackageChanged);
            toolbar.Add(mYooPackageDropdown);

            var packageBtns = BuildYooPackageManagementButtons();
            toolbar.Add(packageBtns);

            var settingsBtn = CreateToolbarButtonWithIcon(KitIcons.SETTINGS, "包设置", ToggleYooPackageSettingsPanel);
            settingsBtn.style.marginLeft = 8;
            settingsBtn.style.flexGrow = 0;
            settingsBtn.style.flexShrink = 0;
            toolbar.Add(settingsBtn);

            var globalSettingsBtn = CreateToolbarButtonWithIcon(KitIcons.SETTINGS, "全局设置", ToggleYooGlobalSettingsPanel);
            globalSettingsBtn.style.flexGrow = 0;
            globalSettingsBtn.style.flexShrink = 0;
            toolbar.Add(globalSettingsBtn);

            toolbar.Add(CreateToolbarSpacer());

            mYooUnsavedLabel = new Label("有未保存的更改");
            mYooUnsavedLabel.AddToClassList("yoo-unsaved-label");
            mYooUnsavedLabel.style.color = new StyleColor(new Color(1f, 0.6f, 0.2f));
            mYooUnsavedLabel.style.marginRight = 12;
            mYooUnsavedLabel.style.flexGrow = 0;
            mYooUnsavedLabel.style.flexShrink = 0;
            mYooUnsavedLabel.style.display = DisplayStyle.None;
            toolbar.Add(mYooUnsavedLabel);

            var refreshBtn = CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", ManualRefreshYooUI);
            refreshBtn.style.flexGrow = 0;
            refreshBtn.style.flexShrink = 0;
            toolbar.Add(refreshBtn);

            var fixBtn = CreateToolbarButtonWithIcon(KitIcons.SETTINGS, "修复", FixYooSettings);
            fixBtn.style.flexGrow = 0;
            fixBtn.style.flexShrink = 0;
            toolbar.Add(fixBtn);

            var saveBtn = CreateToolbarPrimaryButton("保存", SaveYooSettings);
            saveBtn.style.flexGrow = 0;
            saveBtn.style.flexShrink = 0;
            toolbar.Add(saveBtn);

            return toolbar;
        }

        /// <summary>
        /// 构建资源包管理按钮组。
        /// </summary>
        private VisualElement BuildYooPackageManagementButtons()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.flexGrow = 0;
            container.style.flexShrink = 0;

            mYooAddPackageBtn = new Button(ShowYooCreatePackageDialog) { text = "+" };
            mYooAddPackageBtn.tooltip = "创建新资源包";
            mYooAddPackageBtn.style.width = 22;
            mYooAddPackageBtn.style.height = 22;
            mYooAddPackageBtn.style.marginRight = 2;
            mYooAddPackageBtn.style.paddingLeft = 0;
            mYooAddPackageBtn.style.paddingRight = 0;
            mYooAddPackageBtn.style.flexGrow = 0;
            mYooAddPackageBtn.style.flexShrink = 0;
            container.Add(mYooAddPackageBtn);

            mYooRemovePackageBtn = new Button(ShowYooDeletePackageDialog) { text = "-" };
            mYooRemovePackageBtn.tooltip = "删除当前资源包";
            mYooRemovePackageBtn.style.width = 22;
            mYooRemovePackageBtn.style.height = 22;
            mYooRemovePackageBtn.style.paddingLeft = 0;
            mYooRemovePackageBtn.style.paddingRight = 0;
            mYooRemovePackageBtn.style.flexGrow = 0;
            mYooRemovePackageBtn.style.flexShrink = 0;
            container.Add(mYooRemovePackageBtn);

            return container;
        }

        #region 工具栏事件处理

        /// <summary>
        /// Package 选择变更。
        /// </summary>
        private void OnYooPackageChanged(ChangeEvent<string> evt)
        {
            var names = GetYooPackageNames();
            var index = names.IndexOf(evt.newValue);
            if (index >= 0 && index != mYooSelectedPackageIndex)
            {
                mYooSelectedPackageIndex = index;
                mYooSelectedGroupIndex = 0;
                RefreshYooPackageSettingsPanel();
                RefreshYooGroupNav();
                RefreshYooCollectorCanvas();
            }
        }

        /// <summary>
        /// 切换包设置面板显示。
        /// </summary>
        private void ToggleYooPackageSettingsPanel()
        {
            mYooPackageSettingsExpanded = !mYooPackageSettingsExpanded;
            mYooPackageSettingsPanel.style.display = mYooPackageSettingsExpanded ? DisplayStyle.Flex : DisplayStyle.None;

            if (mYooPackageSettingsExpanded && mYooGlobalSettingsExpanded)
            {
                mYooGlobalSettingsExpanded = false;
                mYooGlobalSettingsPanel.style.display = DisplayStyle.None;
            }

            if (mYooPackageSettingsExpanded)
            {
                RefreshYooPackageSettingsPanel();
            }
        }

        /// <summary>
        /// 切换全局设置面板显示。
        /// </summary>
        private void ToggleYooGlobalSettingsPanel()
        {
            mYooGlobalSettingsExpanded = !mYooGlobalSettingsExpanded;
            mYooGlobalSettingsPanel.style.display = mYooGlobalSettingsExpanded ? DisplayStyle.Flex : DisplayStyle.None;

            if (mYooGlobalSettingsExpanded && mYooPackageSettingsExpanded)
            {
                mYooPackageSettingsExpanded = false;
                mYooPackageSettingsPanel.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// 刷新 Package 下拉选择器。
        /// </summary>
        private void RefreshYooPackageDropdown()
        {
            var names = GetYooPackageNames();
            mYooPackageDropdown.choices = names;

            if (names.Count == 0)
            {
                mYooPackageDropdown.SetValueWithoutNotify(string.Empty);
                return;
            }

            if (mYooSelectedPackageIndex >= names.Count)
            {
                mYooSelectedPackageIndex = 0;
            }

            mYooPackageDropdown.SetValueWithoutNotify(names[mYooSelectedPackageIndex]);
        }

        /// <summary>
        /// 刷新包设置面板。
        /// </summary>
        private void RefreshYooPackageSettingsPanel()
        {
            RefreshYooPackageSettingsPanelFull();
        }

        /// <summary>
        /// 刷新未保存提示标签。
        /// </summary>
        private void RefreshYooUnsavedLabel()
        {
            if (mYooUnsavedLabel == default)
            {
                return;
            }

            mYooUnsavedLabel.style.display = mYooHasUnsavedChanges ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion

        #region 资源包管理

        /// <summary>
        /// 显示创建资源包对话框。
        /// </summary>
        private void ShowYooCreatePackageDialog()
        {
            var defaultName = "NewPackage";
            var existingNames = GetYooPackageNames();
            int suffix = 1;
            while (existingNames.Contains(defaultName))
            {
                defaultName = $"NewPackage{suffix++}";
            }

            var packageName = defaultName;
            CreateYooNewPackage(packageName);
        }

        /// <summary>
        /// 显示删除资源包对话框。
        /// </summary>
        private void ShowYooDeletePackageDialog()
        {
            var package = YooCurrentPackage;
            if (package == default)
            {
                return;
            }

            if (YooSetting.Packages.Count <= 1)
            {
                EditorUtility.DisplayDialog("提示", "至少需要保留一个资源包", "确定");
                return;
            }

            if (EditorUtility.DisplayDialog("确认删除", $"确定要删除资源包 \"{package.PackageName}\" 吗？\n此操作不可撤销。", "删除", "取消"))
            {
                DeleteYooCurrentPackage();
            }
        }

        #endregion
    }
}
#endif
