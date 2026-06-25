#if !GODOT
#if YOKIFRAME_YOOASSET_SUPPORT
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace YokiFrame.Unity
{
    public sealed partial class YooAssetWorkbenchWindow
    {
        private void AddPackage()
        {
            var names = GetPackageNames();
            var packageName = "NewPackage";
            var suffix = 1;
            while (names.Contains(packageName))
            {
                packageName = "NewPackage" + suffix;
                suffix++;
            }

#if YOOASSET_3_0_OR_NEWER
            BundleCollectorSettingData.CreatePackage(packageName);
#else
            AssetBundleCollectorSettingData.CreatePackage(packageName);
#endif
            mSelectedPackageIndex = GetPackageNames().Count - 1;
            mSelectedGroupIndex = 0;
            MarkDirty();
            RefreshAll();
        }

        private void RemoveCurrentPackage()
        {
            var package = CurrentPackage;
            if (package == null || YooSetting == null || YooSetting.Packages == null)
            {
                return;
            }

            if (YooSetting.Packages.Count <= 1)
            {
                EditorUtility.DisplayDialog("YooAsset", "至少需要保留一个资源包。", "确定");
                return;
            }

            if (!EditorUtility.DisplayDialog("确认删除", "确定要删除资源包 \"" + package.PackageName + "\" 吗？", "删除", "取消"))
            {
                return;
            }

#if YOOASSET_3_0_OR_NEWER
            BundleCollectorSettingData.RemovePackage(package);
#else
            AssetBundleCollectorSettingData.RemovePackage(package);
#endif
            if (mSelectedPackageIndex >= YooSetting.Packages.Count)
            {
                mSelectedPackageIndex = YooSetting.Packages.Count - 1;
            }

            mSelectedGroupIndex = 0;
            MarkDirty();
            RefreshAll();
        }

        private void AddGroup()
        {
            var package = CurrentPackage;
            if (package == null)
            {
                return;
            }

            var groupName = "NewGroup";
            var suffix = 1;
            while (ContainsGroupName(package, groupName))
            {
                groupName = "NewGroup" + suffix;
                suffix++;
            }

#if YOOASSET_3_0_OR_NEWER
            BundleCollectorSettingData.CreateGroup(package, groupName);
#else
            AssetBundleCollectorSettingData.CreateGroup(package, groupName);
#endif
            mSelectedGroupIndex = package.Groups.Count - 1;
            MarkDirty();
            RefreshGroupNav();
            RefreshCollectorCanvas();
        }

#if YOOASSET_3_0_OR_NEWER
        private static bool ContainsGroupName(BundleCollectorPackage package, string groupName)
#else
        private static bool ContainsGroupName(AssetBundleCollectorPackage package, string groupName)
#endif
        {
            if (package == null || package.Groups == null)
            {
                return false;
            }

            for (var i = 0; i < package.Groups.Count; i++)
            {
                if (package.Groups[i].GroupName == groupName)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddCollector()
        {
            var group = CurrentGroup;
            if (group == null)
            {
                EditorUtility.DisplayDialog("YooAsset", "请先选择或创建一个分组。", "确定");
                return;
            }

            var path = EditorUtility.OpenFolderPanel("选择收集路径", "Assets", string.Empty);
            path = NormalizeAssetPath(path);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

#if YOOASSET_3_0_OR_NEWER
            var collector = new BundleCollector
            {
                CollectPath = path,
                CollectorType = ECollectorType.MainAssetCollector,
                AddressRuleName = "AddressByFileName",
                PackRuleName = "PackDirectory",
                FilterRuleName = "CollectAll"
            };
            BundleCollectorSettingData.CreateCollector(group, collector);
#else
            var collector = new AssetBundleCollector
            {
                CollectPath = path,
                CollectorType = ECollectorType.MainAssetCollector,
                AddressRuleName = "AddressByFileName",
                PackRuleName = "PackDirectory",
                FilterRuleName = "CollectAll"
            };
            AssetBundleCollectorSettingData.CreateCollector(group, collector);
#endif
            MarkDirty();
            RefreshCollectorCanvas();
        }

#if YOOASSET_3_0_OR_NEWER
        private void ShowGroupContextMenu(BundleCollectorGroup group)
#else
        private void ShowGroupContextMenu(AssetBundleCollectorGroup group)
#endif
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(group.ActiveRuleName == DISABLE_GROUP_RULE ? "启用分组" : "禁用分组"), false, () =>
            {
                group.ActiveRuleName = group.ActiveRuleName == DISABLE_GROUP_RULE ? ENABLE_GROUP_RULE : DISABLE_GROUP_RULE;
                ModifyGroup(group);
            });
            menu.AddSeparator(string.Empty);
            menu.AddItem(new GUIContent("删除分组"), false, () =>
            {
                if (EditorUtility.DisplayDialog("确认删除", "确定要删除分组 \"" + group.GroupName + "\" 吗？", "删除", "取消"))
                {
                    RemoveGroup(group);
                }
            });
            menu.ShowAsContext();
        }

#if YOOASSET_3_0_OR_NEWER
        private void RemoveGroup(BundleCollectorGroup group)
#else
        private void RemoveGroup(AssetBundleCollectorGroup group)
#endif
        {
            var package = CurrentPackage;
            if (package == null || group == null)
            {
                return;
            }

#if YOOASSET_3_0_OR_NEWER
            BundleCollectorSettingData.RemoveGroup(package, group);
#else
            AssetBundleCollectorSettingData.RemoveGroup(package, group);
#endif
            if (mSelectedGroupIndex >= package.Groups.Count)
            {
                mSelectedGroupIndex = package.Groups.Count - 1;
            }

            if (mSelectedGroupIndex < 0)
            {
                mSelectedGroupIndex = 0;
            }

            MarkDirty();
            RefreshGroupNav();
            RefreshCollectorCanvas();
        }

#if YOOASSET_3_0_OR_NEWER
        private void RemoveCollector(BundleCollector collector)
#else
        private void RemoveCollector(AssetBundleCollector collector)
#endif
        {
            var group = CurrentGroup;
            if (group == null || collector == null)
            {
                return;
            }

            if (!EditorUtility.DisplayDialog("确认删除", "确定要删除收集器 \"" + collector.CollectPath + "\" 吗？", "删除", "取消"))
            {
                return;
            }

#if YOOASSET_3_0_OR_NEWER
            BundleCollectorSettingData.RemoveCollector(group, collector);
#else
            AssetBundleCollectorSettingData.RemoveCollector(group, collector);
#endif
            MarkDirty();
            RefreshCollectorCanvas();
        }

#if YOOASSET_3_0_OR_NEWER
        private void ModifyPackage(BundleCollectorPackage package)
        {
            BundleCollectorSettingData.ModifyPackage(package);
            MarkDirty();
        }

        private void ModifyGroup(BundleCollectorGroup group)
        {
            BundleCollectorSettingData.ModifyGroup(CurrentPackage, group);
            MarkDirty();
            RefreshGroupNav();
            RefreshCollectorCanvas();
        }

        private void ModifyCollector(BundleCollector collector)
        {
            BundleCollectorSettingData.ModifyCollector(CurrentGroup, collector);
            MarkDirty();
        }
#else
        private void ModifyPackage(AssetBundleCollectorPackage package)
        {
            AssetBundleCollectorSettingData.ModifyPackage(package);
            MarkDirty();
        }

        private void ModifyGroup(AssetBundleCollectorGroup group)
        {
            AssetBundleCollectorSettingData.ModifyGroup(CurrentPackage, group);
            MarkDirty();
            RefreshGroupNav();
            RefreshCollectorCanvas();
        }

        private void ModifyCollector(AssetBundleCollector collector)
        {
            AssetBundleCollectorSettingData.ModifyCollector(CurrentGroup, collector);
            MarkDirty();
        }
#endif

        private void SaveSettings()
        {
#if YOOASSET_3_0_OR_NEWER
            BundleCollectorSettingData.SaveFile();
#else
            AssetBundleCollectorSettingData.SaveFile();
#endif
            mHasUnsavedChanges = false;
            RefreshDirtyState();
        }

        private void FixSettings()
        {
#if YOOASSET_3_0_OR_NEWER
            BundleCollectorSettingData.FixFile();
            if (BundleCollectorSettingData.IsDirty)
#else
            AssetBundleCollectorSettingData.FixFile();
            if (AssetBundleCollectorSettingData.IsDirty)
#endif
            {
                MarkDirty();
            }

            RefreshAll();
        }

        private void ModifyUniqueBundleName(bool value)
        {
#if YOOASSET_3_0_OR_NEWER
            BundleCollectorSettingData.ModifyUniqueBundleName(value);
#else
            AssetBundleCollectorSettingData.ModifyUniqueBundleName(value);
#endif
            MarkDirty();
        }

        private void ModifyShowPackageView(bool value)
        {
#if YOOASSET_3_0_OR_NEWER
            BundleCollectorSettingData.ModifyShowPackageView(value);
#else
            AssetBundleCollectorSettingData.ModifyShowPackageView(value);
#endif
            MarkDirty();
        }

        private void ModifyShowEditorAlias(bool value)
        {
#if YOOASSET_3_0_OR_NEWER
            BundleCollectorSettingData.ModifyShowEditorAlias(value);
#else
            AssetBundleCollectorSettingData.ModifyShowEditorAlias(value);
#endif
            MarkDirty();
        }
    }
}
#endif
#endif