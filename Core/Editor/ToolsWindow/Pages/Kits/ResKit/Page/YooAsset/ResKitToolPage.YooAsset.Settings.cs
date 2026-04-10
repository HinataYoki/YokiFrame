#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - YooAsset 设置面板。
    /// </summary>
    public partial class ResKitToolPage
    {
        #region 设置面板 UI 引用

        /// <summary>包名输入框</summary>
        private TextField mYooPackageNameField;

        /// <summary>启用可寻址开关</summary>
        private VisualElement mYooEnableAddressableToggle;

        /// <summary>地址转小写开关</summary>
        private VisualElement mYooLocationToLowerToggle;

        /// <summary>包含资源 GUID 开关</summary>
        private VisualElement mYooIncludeAssetGUIDToggle;

        /// <summary>自动收集着色器开关</summary>
        private VisualElement mYooAutoCollectShadersToggle;

        /// <summary>支持无扩展名资源开关</summary>
        private VisualElement mYooSupportExtensionlessToggle;

        /// <summary>唯一 Bundle 名称开关</summary>
        private VisualElement mYooUniqueBundleNameToggle;

        /// <summary>显示包视图开关</summary>
        private VisualElement mYooShowPackageViewToggle;

        /// <summary>显示编辑器别名开关</summary>
        private VisualElement mYooShowEditorAliasToggle;

        #endregion

        /// <summary>
        /// 构建包设置面板。
        /// </summary>
        private VisualElement BuildYooPackageSettingsPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("yoo-package-settings-panel");
            panel.style.backgroundColor = new StyleColor(new Color(0.18f, 0.18f, 0.20f));
            panel.style.paddingLeft = 16;
            panel.style.paddingRight = 16;
            panel.style.paddingTop = 12;
            panel.style.paddingBottom = 12;
            panel.style.borderBottomWidth = 1;
            panel.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));

            var title = CreateSectionHeader("包设置", KitIcons.SETTINGS);
            title.style.marginBottom = 12;
            panel.Add(title);

            var nameRow = new VisualElement();
            nameRow.style.flexDirection = FlexDirection.Row;
            nameRow.style.alignItems = Align.Center;
            nameRow.style.marginBottom = 12;
            panel.Add(nameRow);

            var nameLabel = new Label("包名称");
            nameLabel.style.width = 70;
            nameLabel.style.fontSize = 12;
            nameLabel.style.color = new StyleColor(new Color(0.7f, 0.7f, 0.7f));
            nameRow.Add(nameLabel);

            mYooPackageNameField = new TextField();
            mYooPackageNameField.style.flexGrow = 1;
            mYooPackageNameField.RegisterCallback<FocusOutEvent>(_ => OnYooPackageNameChanged());
            mYooPackageNameField.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                {
                    OnYooPackageNameChanged();
                    evt.StopPropagation();
                }
            });
            nameRow.Add(mYooPackageNameField);

            var settingsContainer = new VisualElement();
            settingsContainer.style.flexDirection = FlexDirection.Row;
            settingsContainer.style.flexWrap = Wrap.Wrap;
            panel.Add(settingsContainer);

            mYooEnableAddressableToggle = CreateModernToggle("启用可寻址", false, OnYooEnableAddressableChanged);
            mYooEnableAddressableToggle.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
            mYooEnableAddressableToggle.style.marginBottom = 8;
            settingsContainer.Add(mYooEnableAddressableToggle);

            mYooLocationToLowerToggle = CreateModernToggle("地址转小写", false, OnYooLocationToLowerChanged);
            mYooLocationToLowerToggle.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
            mYooLocationToLowerToggle.style.marginBottom = 8;
            settingsContainer.Add(mYooLocationToLowerToggle);

            mYooIncludeAssetGUIDToggle = CreateModernToggle("包含资源GUID", false, OnYooIncludeAssetGUIDChanged);
            mYooIncludeAssetGUIDToggle.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
            mYooIncludeAssetGUIDToggle.style.marginBottom = 8;
            settingsContainer.Add(mYooIncludeAssetGUIDToggle);

            mYooAutoCollectShadersToggle = CreateModernToggle("自动收集着色器", true, OnYooAutoCollectShadersChanged);
            mYooAutoCollectShadersToggle.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
            mYooAutoCollectShadersToggle.style.marginBottom = 8;
            settingsContainer.Add(mYooAutoCollectShadersToggle);

            mYooSupportExtensionlessToggle = CreateModernToggle("支持无扩展名资源", true, OnYooSupportExtensionlessChanged);
            mYooSupportExtensionlessToggle.style.width = new StyleLength(new Length(50, LengthUnit.Percent));
            mYooSupportExtensionlessToggle.style.marginBottom = 8;
            settingsContainer.Add(mYooSupportExtensionlessToggle);

            return panel;
        }

        /// <summary>
        /// 构建全局设置面板。
        /// </summary>
        private VisualElement BuildYooGlobalSettingsPanel()
        {
            var panel = new VisualElement();
            panel.AddToClassList("yoo-global-settings-panel");
            panel.style.backgroundColor = new StyleColor(new Color(0.16f, 0.16f, 0.18f));
            panel.style.paddingLeft = 16;
            panel.style.paddingRight = 16;
            panel.style.paddingTop = 12;
            panel.style.paddingBottom = 12;
            panel.style.borderBottomWidth = 1;
            panel.style.borderBottomColor = new StyleColor(new Color(0.22f, 0.22f, 0.24f));

            var title = CreateSectionHeader("全局设置", KitIcons.SETTINGS);
            title.style.marginBottom = 12;
            panel.Add(title);

            var settingsContainer = new VisualElement();
            settingsContainer.style.flexDirection = FlexDirection.Row;
            settingsContainer.style.flexWrap = Wrap.Wrap;
            panel.Add(settingsContainer);

            mYooUniqueBundleNameToggle = CreateModernToggle("唯一 Bundle 名称", YooSetting?.UniqueBundleName ?? false, OnYooUniqueBundleNameChanged);
            mYooUniqueBundleNameToggle.style.width = new StyleLength(new Length(33, LengthUnit.Percent));
            mYooUniqueBundleNameToggle.style.marginBottom = 8;
            settingsContainer.Add(mYooUniqueBundleNameToggle);

            mYooShowPackageViewToggle = CreateModernToggle("显示包视图", YooSetting?.ShowPackageView ?? false, OnYooShowPackageViewChanged);
            mYooShowPackageViewToggle.style.width = new StyleLength(new Length(33, LengthUnit.Percent));
            mYooShowPackageViewToggle.style.marginBottom = 8;
            settingsContainer.Add(mYooShowPackageViewToggle);

            mYooShowEditorAliasToggle = CreateModernToggle("显示编辑器别名", YooSetting?.ShowEditorAlias ?? false, OnYooShowEditorAliasChanged);
            mYooShowEditorAliasToggle.style.width = new StyleLength(new Length(33, LengthUnit.Percent));
            mYooShowEditorAliasToggle.style.marginBottom = 8;
            settingsContainer.Add(mYooShowEditorAliasToggle);

            return panel;
        }

        /// <summary>
        /// 刷新全局设置面板。
        /// </summary>
        private void RefreshYooGlobalSettingsPanel()
        {
            if (YooSetting == default)
            {
                ResetYooGlobalSettingsPanel();
                return;
            }

            bool uniqueBundleName = YooSetting.UniqueBundleName;
            bool showPackageView = YooSetting.ShowPackageView;
            bool showEditorAlias = YooSetting.ShowEditorAlias;

            SetToggleValue(mYooUniqueBundleNameToggle, uniqueBundleName);
            SetToggleValue(mYooShowPackageViewToggle, showPackageView);
            SetToggleValue(mYooShowEditorAliasToggle, showEditorAlias);
        }

        /// <summary>
        /// 刷新包设置面板。
        /// </summary>
        private void RefreshYooPackageSettingsPanelFull()
        {
            var package = YooCurrentPackage;
            if (package == default)
            {
                ResetYooPackageSettingsPanel();
                return;
            }

            if (mYooPackageNameField != default)
            {
                mYooPackageNameField.SetValueWithoutNotify(package.PackageName);
            }

            SetToggleValue(mYooEnableAddressableToggle, package.EnableAddressable);
            SetToggleValue(mYooLocationToLowerToggle, package.LocationToLower);
            SetToggleValue(mYooIncludeAssetGUIDToggle, package.IncludeAssetGUID);
            SetToggleValue(mYooAutoCollectShadersToggle, package.AutoCollectShaders);
            SetToggleValue(mYooSupportExtensionlessToggle, package.SupportExtensionless);
        }

        /// <summary>
        /// 重置全局设置面板，避免配置失效时保留旧状态。
        /// </summary>
        private void ResetYooGlobalSettingsPanel()
        {
            SetToggleValue(mYooUniqueBundleNameToggle, false);
            SetToggleValue(mYooShowPackageViewToggle, false);
            SetToggleValue(mYooShowEditorAliasToggle, false);
        }

        /// <summary>
        /// 重置包设置面板，避免包切换失效时保留旧状态。
        /// </summary>
        private void ResetYooPackageSettingsPanel()
        {
            if (mYooPackageNameField != default)
            {
                mYooPackageNameField.SetValueWithoutNotify(string.Empty);
            }

            SetToggleValue(mYooEnableAddressableToggle, false);
            SetToggleValue(mYooLocationToLowerToggle, false);
            SetToggleValue(mYooIncludeAssetGUIDToggle, false);
            SetToggleValue(mYooAutoCollectShadersToggle, false);
            SetToggleValue(mYooSupportExtensionlessToggle, false);
        }

        /// <summary>
        /// 设置 Toggle 开关的值。
        /// </summary>
        private static void SetToggleValue(VisualElement toggle, bool value)
        {
            if (toggle == default)
            {
                return;
            }

            if (value)
            {
                if (!toggle.ClassListContains("checked"))
                {
                    toggle.AddToClassList("checked");
                }
            }
            else
            {
                toggle.RemoveFromClassList("checked");
            }
        }

        #region 包设置事件处理

        /// <summary>
        /// 包名修改回调。
        /// </summary>
        private void OnYooPackageNameChanged()
        {
            var package = YooCurrentPackage;
            if (package == default || mYooPackageNameField == default)
            {
                return;
            }

            var newName = mYooPackageNameField.value?.Trim();
            if (string.IsNullOrEmpty(newName) || newName == package.PackageName)
            {
                return;
            }

            var existingNames = GetYooPackageNames();
            if (existingNames.Contains(newName))
            {
                mYooPackageNameField.SetValueWithoutNotify(package.PackageName);
                EditorUtility.DisplayDialog("包名重复", $"包名称 \"{newName}\" 已存在，请使用其他名称。", "确定");
                return;
            }

            package.PackageName = newName;
            AssetBundleCollectorSettingData.ModifyPackage(package);
            MarkYooDirty();
            RefreshYooPackageDropdown();
        }

        private void OnYooEnableAddressableChanged(bool value)
        {
            var package = YooCurrentPackage;
            if (package == default)
            {
                return;
            }

            package.EnableAddressable = value;
            AssetBundleCollectorSettingData.ModifyPackage(package);
            MarkYooDirty();
        }

        private void OnYooLocationToLowerChanged(bool value)
        {
            var package = YooCurrentPackage;
            if (package == default)
            {
                return;
            }

            package.LocationToLower = value;
            AssetBundleCollectorSettingData.ModifyPackage(package);
            MarkYooDirty();
        }

        private void OnYooIncludeAssetGUIDChanged(bool value)
        {
            var package = YooCurrentPackage;
            if (package == default)
            {
                return;
            }

            package.IncludeAssetGUID = value;
            AssetBundleCollectorSettingData.ModifyPackage(package);
            MarkYooDirty();
        }

        private void OnYooAutoCollectShadersChanged(bool value)
        {
            var package = YooCurrentPackage;
            if (package == default)
            {
                return;
            }

            package.AutoCollectShaders = value;
            AssetBundleCollectorSettingData.ModifyPackage(package);
            MarkYooDirty();
        }

        private void OnYooSupportExtensionlessChanged(bool value)
        {
            var package = YooCurrentPackage;
            if (package == default)
            {
                return;
            }

            package.SupportExtensionless = value;
            AssetBundleCollectorSettingData.ModifyPackage(package);
            MarkYooDirty();
        }

        #endregion

        #region 全局设置事件处理

        private void OnYooUniqueBundleNameChanged(bool value)
        {
            AssetBundleCollectorSettingData.ModifyUniqueBundleName(value);
            MarkYooDirty();
        }

        private void OnYooShowPackageViewChanged(bool value)
        {
            AssetBundleCollectorSettingData.ModifyShowPackageView(value);
            MarkYooDirty();
        }

        private void OnYooShowEditorAliasChanged(bool value)
        {
            AssetBundleCollectorSettingData.ModifyShowEditorAlias(value);
            MarkYooDirty();
        }

        #endregion
    }
}
#endif
