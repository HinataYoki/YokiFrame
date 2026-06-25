#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;

namespace YokiFrame.Unity
{
    public sealed partial class YooAssetWorkbenchWindow
    {
#if YOOASSET_3_0_OR_NEWER
        private static BundleCollectorSetting YooSetting
        {
            get { return BundleCollectorSettingData.Setting; }
        }

        private BundleCollectorPackage CurrentPackage
        {
            get
            {
                if (YooSetting == null || YooSetting.Packages == null || YooSetting.Packages.Count == 0)
                {
                    return null;
                }

                if (mSelectedPackageIndex < 0 || mSelectedPackageIndex >= YooSetting.Packages.Count)
                {
                    mSelectedPackageIndex = 0;
                }

                return YooSetting.Packages[mSelectedPackageIndex];
            }
        }

        private BundleCollectorGroup CurrentGroup
        {
            get
            {
                var package = CurrentPackage;
                if (package == null || package.Groups == null || package.Groups.Count == 0)
                {
                    return null;
                }

                if (mSelectedGroupIndex < 0 || mSelectedGroupIndex >= package.Groups.Count)
                {
                    mSelectedGroupIndex = 0;
                }

                return package.Groups[mSelectedGroupIndex];
            }
        }
#else
        private static AssetBundleCollectorSetting YooSetting
        {
            get { return AssetBundleCollectorSettingData.Setting; }
        }

        private AssetBundleCollectorPackage CurrentPackage
        {
            get
            {
                if (YooSetting == null || YooSetting.Packages == null || YooSetting.Packages.Count == 0)
                {
                    return null;
                }

                if (mSelectedPackageIndex < 0 || mSelectedPackageIndex >= YooSetting.Packages.Count)
                {
                    mSelectedPackageIndex = 0;
                }

                return YooSetting.Packages[mSelectedPackageIndex];
            }
        }

        private AssetBundleCollectorGroup CurrentGroup
        {
            get
            {
                var package = CurrentPackage;
                if (package == null || package.Groups == null || package.Groups.Count == 0)
                {
                    return null;
                }

                if (mSelectedGroupIndex < 0 || mSelectedGroupIndex >= package.Groups.Count)
                {
                    mSelectedGroupIndex = 0;
                }

                return package.Groups[mSelectedGroupIndex];
            }
        }
#endif

        private void EnsureDefaultPackage()
        {
            if (YooSetting == null)
            {
                return;
            }

            if (YooSetting.Packages != null && YooSetting.Packages.Count > 0)
            {
                return;
            }

#if YOOASSET_3_0_OR_NEWER
            BundleCollectorSettingData.CreatePackage(DEFAULT_PACKAGE_NAME);
            BundleCollectorSettingData.SaveFile();
#else
            AssetBundleCollectorSettingData.CreatePackage(DEFAULT_PACKAGE_NAME);
            AssetBundleCollectorSettingData.SaveFile();
#endif
        }

        private List<string> GetPackageNames()
        {
            var names = new List<string>();
            if (YooSetting == null || YooSetting.Packages == null)
            {
                return names;
            }

            for (var i = 0; i < YooSetting.Packages.Count; i++)
            {
                names.Add(YooSetting.Packages[i].PackageName);
            }

            return names;
        }

        private void RefreshPackageDropdown()
        {
            if (mPackageDropdown == null)
            {
                return;
            }

            var names = GetPackageNames();
            mPackageDropdown.choices = names;

            if (names.Count == 0)
            {
                mPackageDropdown.SetValueWithoutNotify(string.Empty);
                return;
            }

            if (mSelectedPackageIndex < 0 || mSelectedPackageIndex >= names.Count)
            {
                mSelectedPackageIndex = 0;
            }

            mPackageDropdown.SetValueWithoutNotify(names[mSelectedPackageIndex]);
        }

        private void RefreshGroupNav()
        {
            if (mGroupList == null)
            {
                return;
            }

            mGroupList.Clear();
            var package = CurrentPackage;
            if (package == null || package.Groups == null || package.Groups.Count == 0)
            {
                UpdateCountBadge(mGroupCountBadge, 0);
                mGroupList.Add(YokiFrameUIComponents.CreateEmptyState(KitIcons.FOLDER, "暂无分组", "点击下方按钮创建分组"));
                return;
            }

            UpdateCountBadge(mGroupCountBadge, package.Groups.Count);
            if (mSelectedGroupIndex >= package.Groups.Count)
            {
                mSelectedGroupIndex = 0;
            }

            for (var i = 0; i < package.Groups.Count; i++)
            {
#if YOOASSET_3_0_OR_NEWER
                mGroupList.Add(CreateGroupItem(package.Groups[i], i));
#else
                mGroupList.Add(CreateGroupItem(package.Groups[i], i));
#endif
            }
        }

#if YOOASSET_3_0_OR_NEWER
        private VisualElement CreateGroupItem(BundleCollectorGroup group, int index)
#else
        private VisualElement CreateGroupItem(AssetBundleCollectorGroup group, int index)
#endif
        {
            var item = new VisualElement();
            item.AddToClassList("list-item");
            item.style.flexDirection = FlexDirection.Row;
            item.style.alignItems = Align.Center;
            item.style.minHeight = 34f;
            item.style.paddingLeft = 8f;
            item.style.paddingRight = 8f;
            item.style.paddingTop = 6f;
            item.style.paddingBottom = 6f;
            item.style.marginBottom = 5f;
            item.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.LayerElevated);
            SetRadius(item, 8f);
            if (index == mSelectedGroupIndex)
            {
                item.AddToClassList("selected");
                item.style.backgroundColor = new StyleColor(new Color(0.25f, 0.45f, 0.70f, 1f));
            }

            var isActive = group.ActiveRuleName != DISABLE_GROUP_RULE;
            var indicator = YokiFrameUIComponents.CreateStatusDot(isActive ? YokiFrameUIComponents.Colors.BrandSuccess : YokiFrameUIComponents.Colors.TextTertiary, 8f);
            indicator.style.flexShrink = 0f;
            indicator.style.marginRight = 8f;
            item.Add(indicator);

            var label = new Label(group.GroupName);
            label.AddToClassList("list-item-label");
            label.style.flexGrow = 1f;
            label.style.minWidth = 0f;
            label.style.overflow = Overflow.Hidden;
            label.style.textOverflow = TextOverflow.Ellipsis;
            label.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            label.style.opacity = isActive ? 1f : 0.5f;
            item.Add(label);

            var count = group.Collectors != null ? group.Collectors.Count : 0;
            var countLabel = new Label(count.ToString());
            countLabel.AddToClassList("list-item-count");
            countLabel.style.minWidth = 24f;
            countLabel.style.height = 18f;
            countLabel.style.marginLeft = 8f;
            countLabel.style.paddingLeft = 6f;
            countLabel.style.paddingRight = 6f;
            countLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            countLabel.style.backgroundColor = new StyleColor(YokiFrameUIComponents.Colors.BadgeDefault);
            countLabel.style.color = new StyleColor(YokiFrameUIComponents.Colors.TextPrimary);
            countLabel.style.fontSize = 10f;
            SetRadius(countLabel, 9f);
            item.Add(countLabel);

            item.RegisterCallback<ClickEvent>(_ =>
            {
                mSelectedGroupIndex = index;
                mExpandedCollectorIndex = -1;
                RefreshGroupNav();
                RefreshCollectorCanvas();
            });

            item.RegisterCallback<ContextClickEvent>(evt =>
            {
                ShowGroupContextMenu(group);
                evt.StopPropagation();
            });

            return item;
        }
    }
}
#endif
#endif