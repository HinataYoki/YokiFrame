#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - YooAsset 工具栏
    /// </summary>
    public partial class ResKitToolPage
    {
        /// <summary>
        /// 构建 YooAsset 工具栏
        /// </summary>
        private VisualElement BuildYooToolbar()
        {
            var toolbar = CreateToolbar();

            // Package 下拉选择器
            mYooPackageDropdown = new DropdownField();
            mYooPackageDropdown.AddToClassList("yoo-package-dropdown");
            mYooPackageDropdown.style.minWidth = 150;
            mYooPackageDropdown.style.maxWidth = 200;
            mYooPackageDropdown.style.marginRight = 4;
            mYooPackageDropdown.style.flexGrow = 0;
            mYooPackageDropdown.style.flexShrink = 0;
            mYooPackageDropdown.RegisterValueChangedCallback(OnYooPackageChanged);
            toolbar.Add(mYooPackageDropdown);

            // 资源包管理按钮组
            var packageBtns = BuildYooPackageManagementButtons();
            toolbar.Add(packageBtns);

            // 包设置按钮
            var settingsBtn = CreateToolbarButtonWithIcon(KitIcons.SETTINGS, "包设置", ToggleYooPackageSettingsPanel);
            settingsBtn.style.marginLeft = 8;
            settingsBtn.style.flexGrow = 0;
            settingsBtn.style.flexShrink = 0;
            toolbar.Add(settingsBtn);

            // 全局设置按钮
            var globalSettingsBtn = CreateToolbarButtonWithIcon(KitIcons.SETTINGS, "全局设置", ToggleYooGlobalSettingsPanel);
            globalSettingsBtn.style.flexGrow = 0;
            globalSettingsBtn.style.flexShrink = 0;
            toolbar.Add(globalSettingsBtn);

            // 弹性空间
            toolbar.Add(CreateToolbarSpacer());

            // 未保存提示标签
            mYooUnsavedLabel = new Label("有未保存的更改");
            mYooUnsavedLabel.AddToClassList("yoo-unsaved-label");
            mYooUnsavedLabel.style.color = new StyleColor(new Color(1f, 0.6f, 0.2f));
            mYooUnsavedLabel.style.marginRight = 12;
            mYooUnsavedLabel.style.flexGrow = 0;
            mYooUnsavedLabel.style.flexShrink = 0;
            mYooUnsavedLabel.style.display = DisplayStyle.None;
            toolbar.Add(mYooUnsavedLabel);

            // 刷新按钮
            var refreshBtn = CreateToolbarButtonWithIcon(KitIcons.REFRESH, "刷新", ManualRefreshYooUI);
            refreshBtn.style.flexGrow = 0;
            refreshBtn.style.flexShrink = 0;
            toolbar.Add(refreshBtn);

            // 修复按钮
            var fixBtn = CreateToolbarButtonWithIcon(KitIcons.SETTINGS, "修复", FixYooSettings);
            fixBtn.style.flexGrow = 0;
            fixBtn.style.flexShrink = 0;
            toolbar.Add(fixBtn);

            // 保存按钮
            var saveBtn = CreateToolbarPrimaryButton("保存", SaveYooSettings);
            saveBtn.style.flexGrow = 0;
            saveBtn.style.flexShrink = 0;
            toolbar.Add(saveBtn);

            return toolbar;
        }

        /// <summary>
        /// 构建资源包管理按钮组
        /// </summary>
        private VisualElement BuildYooPackageManagementButtons()
        {
            var container = new VisualElement();
            container.style.flexDirection = FlexDirection.Row;
            container.style.alignItems = Align.Center;
            container.style.flexGrow = 0;
            container.style.flexShrink = 0;

            // 添加资源包按钮
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

            // 删除资源包按钮
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
        /// Package 选择变更
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
        /// 切换包设置面板显示
        /// </summary>
        private void ToggleYooPackageSettingsPanel()
        {
            mYooPackageSettingsExpanded = !mYooPackageSettingsExpanded;
            mYooPackageSettingsPanel.style.display = mYooPackageSettingsExpanded ? DisplayStyle.Flex : DisplayStyle.None;

            // 展开包设置时折叠全局设置
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
        /// 切换全局设置面板显示
        /// </summary>
        private void ToggleYooGlobalSettingsPanel()
        {
            mYooGlobalSettingsExpanded = !mYooGlobalSettingsExpanded;
            mYooGlobalSettingsPanel.style.display = mYooGlobalSettingsExpanded ? DisplayStyle.Flex : DisplayStyle.None;

            // 展开全局设置时折叠包设置
            if (mYooGlobalSettingsExpanded && mYooPackageSettingsExpanded)
            {
                mYooPackageSettingsExpanded = false;
                mYooPackageSettingsPanel.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// 刷新 Package 下拉选择器
        /// </summary>
        private void RefreshYooPackageDropdown()
        {
            var names = GetYooPackageNames();
            mYooPackageDropdown.choices = names;

            if (names.Count > 0)
            {
                if (mYooSelectedPackageIndex >= names.Count)
                    mYooSelectedPackageIndex = 0;
                mYooPackageDropdown.SetValueWithoutNotify(names[mYooSelectedPackageIndex]);
            }
        }

        /// <summary>
        /// 刷新包设置面板
        /// </summary>
        private void RefreshYooPackageSettingsPanel()
        {
            // 调用完整刷新方法（包含所有开关状态）
            RefreshYooPackageSettingsPanelFull();
        }

        /// <summary>
        /// 刷新未保存提示标签
        /// </summary>
        private void RefreshYooUnsavedLabel()
        {
            if (mYooUnsavedLabel == default)
                return;

            mYooUnsavedLabel.style.display = mYooHasUnsavedChanges ? DisplayStyle.Flex : DisplayStyle.None;
        }

        #endregion

        #region 资源包管理

        /// <summary>
        /// 显示创建资源包对话框
        /// </summary>
        private void ShowYooCreatePackageDialog()
        {
            // 生成默认名称
            var defaultName = "NewPackage";
            var existingNames = GetYooPackageNames();
            int suffix = 1;
            while (existingNames.Contains(defaultName))
            {
                defaultName = $"NewPackage{suffix++}";
            }

            // 使用简单输入对话框
            var packageName = defaultName;
            // TODO: 可以使用更好的输入对话框
            CreateYooNewPackage(packageName);
        }

        /// <summary>
        /// 显示删除资源包对话框
        /// </summary>
        private void ShowYooDeletePackageDialog()
        {
            var package = YooCurrentPackage;
            if (package == default)
                return;

            // 检查是否只剩一个包
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
