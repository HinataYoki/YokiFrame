#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Collections.Generic;
using UnityEngine.UIElements;
using YooAsset.Editor;

namespace YokiFrame.Unity
{
    public sealed partial class YooAssetWorkbenchWindow
    {
        private void RefreshPackageSettingsPanel()
        {
            var body = GetPanelBody(mPackageSettingsPanel);
            if (body == null)
            {
                return;
            }

            body.Clear();
            var package = CurrentPackage;
            if (package == null)
            {
                body.Add(YokiFrameUIComponents.CreateEmptyState(KitIcons.PACKAGE, "暂无资源包", "创建资源包后可编辑包设置"));
                return;
            }

            body.Add(CreateTextRow("包名称", package.PackageName, value =>
            {
                if (string.IsNullOrWhiteSpace(value) || value == package.PackageName)
                {
                    return;
                }

                package.PackageName = value.Trim();
                ModifyPackage(package);
                RefreshPackageDropdown();
            }));
            body.Add(CreateTextRow("包描述", package.PackageDesc, value =>
            {
                package.PackageDesc = value;
                ModifyPackage(package);
            }));
            body.Add(CreateToggleSetting("启用可寻址", package.EnableAddressable, value =>
            {
                package.EnableAddressable = value;
                ModifyPackage(package);
            }));
            body.Add(CreateToggleSetting("地址转小写", package.LocationToLower, value =>
            {
                package.LocationToLower = value;
                ModifyPackage(package);
            }));
            body.Add(CreateToggleSetting("包含资源 GUID", package.IncludeAssetGUID, value =>
            {
                package.IncludeAssetGUID = value;
                ModifyPackage(package);
            }));
            body.Add(CreateToggleSetting("自动收集着色器", package.AutoCollectShaders, value =>
            {
                package.AutoCollectShaders = value;
                ModifyPackage(package);
            }));
            body.Add(CreateToggleSetting("支持无扩展名资源", package.SupportExtensionless, value =>
            {
                package.SupportExtensionless = value;
                ModifyPackage(package);
            }));
            body.Add(CreateRuleDropdownRow("忽略规则", GetIgnoreRuleNames(), package.IgnoreRuleName, value =>
            {
                package.IgnoreRuleName = value;
                ModifyPackage(package);
            }));
        }

        private void RefreshGlobalSettingsPanel()
        {
            var body = GetPanelBody(mGlobalSettingsPanel);
            if (body == null)
            {
                return;
            }

            body.Clear();
            if (YooSetting == null)
            {
                body.Add(YokiFrameUIComponents.CreateEmptyState(KitIcons.SETTINGS, "Collector Setting 不可用", "请确认 YooAsset 包已正确导入"));
                return;
            }

            body.Add(CreateToggleSetting("唯一 Bundle 名称", YooSetting.UniqueBundleName, ModifyUniqueBundleName));
            body.Add(CreateToggleSetting("显示包视图", YooSetting.ShowPackageView, ModifyShowPackageView));
            body.Add(CreateToggleSetting("显示编辑器别名", YooSetting.ShowEditorAlias, ModifyShowEditorAlias));
        }

        private static VisualElement GetPanelBody(VisualElement panel)
        {
            if (panel == null)
            {
                return null;
            }

            return panel.Q(className: "yoki-kit-panel__body");
        }

        private static VisualElement CreateToggleSetting(string label, bool value, Action<bool> onChanged)
        {
            var toggle = YokiFrameUIComponents.CreateModernToggle(label, value, onChanged);
            toggle.style.marginBottom = 4f;
            return toggle;
        }

        private static List<string> GetAddressRuleNames()
        {
#if YOOASSET_3_0_OR_NEWER
            return ConvertRuleNames(BundleCollectorSettingData.GetAddressRuleNames());
#else
            return ConvertRuleNames(AssetBundleCollectorSettingData.GetAddressRuleNames());
#endif
        }

        private static List<string> GetPackRuleNames()
        {
#if YOOASSET_3_0_OR_NEWER
            return ConvertRuleNames(BundleCollectorSettingData.GetBundlePackRuleNames());
#else
            return ConvertRuleNames(AssetBundleCollectorSettingData.GetPackRuleNames());
#endif
        }

        private static List<string> GetFilterRuleNames()
        {
#if YOOASSET_3_0_OR_NEWER
            return ConvertRuleNames(BundleCollectorSettingData.GetAssetFilterRuleNames());
#else
            return ConvertRuleNames(AssetBundleCollectorSettingData.GetFilterRuleNames());
#endif
        }

        private static List<string> GetIgnoreRuleNames()
        {
#if YOOASSET_3_0_OR_NEWER
            return ConvertRuleNames(BundleCollectorSettingData.GetAssetIgnoreRuleNames());
#else
            return ConvertRuleNames(AssetBundleCollectorSettingData.GetIgnoreRuleNames());
#endif
        }

        private static List<string> ConvertRuleNames(List<RuleDisplayName> rules)
        {
            var result = new List<string>();
            if (rules == null)
            {
                return result;
            }

            for (var i = 0; i < rules.Count; i++)
            {
                result.Add(rules[i].ClassName);
            }

            return result;
        }
    }
}
#endif
#endif