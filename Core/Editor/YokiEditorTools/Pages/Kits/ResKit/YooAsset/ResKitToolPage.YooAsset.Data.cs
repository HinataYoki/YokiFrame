#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YooAsset.Editor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit 工具页面 - YooAsset 数据操作
    /// </summary>
    public partial class ResKitToolPage
    {
        #region 规则名称简短映射

        /// <summary>
        /// 规则名称简短映射字典
        /// </summary>
        private static readonly Dictionary<string, string> sYooRuleShortNames = new()
        {
            // CollectorType
            { "MainAssetCollector", "主资源" },
            { "StaticAssetCollector", "静态" },
            { "DependAssetCollector", "依赖" },

            // AddressRule
            { "AddressDisable", "禁用寻址" },
            { "AddressByFileName", "按文件名" },
            { "AddressByFilePath", "按路径" },
            { "AddressByFolderAndFileName", "目录+文件名" },
            { "AddressByGroupAndFileName", "组+文件名" },

            // PackRule
            { "PackDirectory", "按目录" },
            { "PackTopDirectory", "按顶层目录" },
            { "PackSeparately", "单独打包" },
            { "PackCollector", "按收集器" },
            { "PackGroup", "按分组" },
            { "PackRawFile", "原始文件" },
            { "PackShaderVariants", "着色器变体" },

            // FilterRule
            { "CollectAll", "全部" },
            { "CollectScene", "场景" },
            { "CollectPrefab", "预制体" },
            { "CollectSprite", "精灵" },

            // ActiveRule
            { "EnableGroup", "启用" },
            { "DisableGroup", "禁用" },

            // IgnoreRule
            { "NormalIgnoreRule", "常规忽略" },
            { "RawFileIgnoreRule", "原始文件忽略" }
        };

        #endregion

        #region 数据访问属性

        /// <summary>
        /// 获取 YooAsset 配置数据
        /// </summary>
        private static AssetBundleCollectorSetting YooSetting => AssetBundleCollectorSettingData.Setting;

        /// <summary>
        /// 获取当前选中的 Package
        /// </summary>
        private AssetBundleCollectorPackage YooCurrentPackage
        {
            get
            {
                if (YooSetting == default || YooSetting.Packages == default || YooSetting.Packages.Count == 0)
                    return default;

                if (mYooSelectedPackageIndex < 0 || mYooSelectedPackageIndex >= YooSetting.Packages.Count)
                    mYooSelectedPackageIndex = 0;

                return YooSetting.Packages[mYooSelectedPackageIndex];
            }
        }

        /// <summary>
        /// 获取当前选中的 Group
        /// </summary>
        private AssetBundleCollectorGroup YooCurrentGroup
        {
            get
            {
                var package = YooCurrentPackage;
                if (package == default || package.Groups == default || package.Groups.Count == 0)
                    return default;

                if (mYooSelectedGroupIndex < 0 || mYooSelectedGroupIndex >= package.Groups.Count)
                    mYooSelectedGroupIndex = 0;

                return package.Groups[mYooSelectedGroupIndex];
            }
        }

        #endregion

        #region 数据操作方法

        /// <summary>
        /// 获取所有 Package 名称列表
        /// </summary>
        private List<string> GetYooPackageNames()
        {
            var names = new List<string>();
            if (YooSetting == default || YooSetting.Packages == default)
                return names;

            foreach (var package in YooSetting.Packages)
            {
                names.Add(package.PackageName);
            }
            return names;
        }

        /// <summary>
        /// 保存 YooAsset 配置
        /// </summary>
        private void SaveYooSettings()
        {
            AssetBundleCollectorSettingData.SaveFile();
            mYooHasUnsavedChanges = false;
            RefreshYooUnsavedLabel();

            // 保存后强制重新导入 SO 文件，确保内存数据与磁盘完全同步
            if (!string.IsNullOrEmpty(mYooSettingAssetPath))
            {
                AssetDatabase.ImportAsset(mYooSettingAssetPath, ImportAssetOptions.ForceUpdate);
            }

            // 更新文件时间戳，避免自己保存后触发外部变更检测
            CacheYooSettingFileInfo();

            // 更新数据状态缓存
            CacheYooDataState();

            // 刷新所有 UI 以确保显示与保存的数据一致
            RefreshYooPackageDropdown();
            RefreshYooPackageSettingsPanel();
            RefreshYooGlobalSettingsPanel();
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
        }

        /// <summary>
        /// 标记配置已修改
        /// </summary>
        private void MarkYooDirty()
        {
            mYooHasUnsavedChanges = true;
            RefreshYooUnsavedLabel();
        }

        /// <summary>
        /// 修复 YooAsset 配置
        /// </summary>
        private void FixYooSettings()
        {
            AssetBundleCollectorSettingData.FixFile();
            if (AssetBundleCollectorSettingData.IsDirty)
            {
                MarkYooDirty();
            }
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
        }

        /// <summary>
        /// 创建新分组
        /// </summary>
        private void CreateYooNewGroup(string groupName)
        {
            var package = YooCurrentPackage;
            if (package == default)
                return;

            AssetBundleCollectorSettingData.CreateGroup(package, groupName);
            MarkYooDirty();
            RefreshYooGroupNav();

            // 选中新创建的分组
            mYooSelectedGroupIndex = package.Groups.Count - 1;
            RefreshYooCollectorCanvas();
        }

        /// <summary>
        /// 删除分组
        /// </summary>
        private void DeleteYooGroup(AssetBundleCollectorGroup group)
        {
            var package = YooCurrentPackage;
            if (package == default || group == default)
                return;

            AssetBundleCollectorSettingData.RemoveGroup(package, group);
            MarkYooDirty();

            // 调整选中索引
            if (mYooSelectedGroupIndex >= package.Groups.Count)
                mYooSelectedGroupIndex = package.Groups.Count - 1;
            if (mYooSelectedGroupIndex < 0)
                mYooSelectedGroupIndex = 0;

            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
        }

        /// <summary>
        /// 创建新收集器
        /// </summary>
        private void CreateYooNewCollector(string collectPath)
        {
            var group = YooCurrentGroup;
            if (group == default)
                return;

            var collector = new AssetBundleCollector
            {
                CollectPath = collectPath,
                CollectorType = ECollectorType.MainAssetCollector,
                AddressRuleName = nameof(AddressByFileName),
                PackRuleName = nameof(PackDirectory),
                FilterRuleName = nameof(CollectAll)
            };

            AssetBundleCollectorSettingData.CreateCollector(group, collector);
            MarkYooDirty();
            RefreshYooCollectorCanvas();
        }

        /// <summary>
        /// 删除收集器
        /// </summary>
        private void DeleteYooCollector(AssetBundleCollector collector)
        {
            var group = YooCurrentGroup;
            if (group == default || collector == default)
                return;

            AssetBundleCollectorSettingData.RemoveCollector(group, collector);
            MarkYooDirty();
            RefreshYooCollectorCanvas();
        }

        /// <summary>
        /// 创建新资源包
        /// </summary>
        private void CreateYooNewPackage(string packageName)
        {
            AssetBundleCollectorSettingData.CreatePackage(packageName);
            MarkYooDirty();
            RefreshYooPackageDropdown();

            // 选中新创建的包
            mYooSelectedPackageIndex = YooSetting.Packages.Count - 1;
            mYooSelectedGroupIndex = 0;
            mYooPackageDropdown.SetValueWithoutNotify(packageName);
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
            RefreshYooRemovePackageButton();
        }

        /// <summary>
        /// 删除当前资源包
        /// </summary>
        private void DeleteYooCurrentPackage()
        {
            var package = YooCurrentPackage;
            if (package == default)
                return;

            AssetBundleCollectorSettingData.RemovePackage(package);
            MarkYooDirty();

            // 调整选中索引
            if (mYooSelectedPackageIndex >= YooSetting.Packages.Count)
                mYooSelectedPackageIndex = YooSetting.Packages.Count - 1;
            if (mYooSelectedPackageIndex < 0)
                mYooSelectedPackageIndex = 0;

            mYooSelectedGroupIndex = 0;
            RefreshYooPackageDropdown();
            RefreshYooGroupNav();
            RefreshYooCollectorCanvas();
            RefreshYooRemovePackageButton();
        }

        /// <summary>
        /// 刷新删除资源包按钮状态
        /// </summary>
        private void RefreshYooRemovePackageButton()
        {
            if (mYooRemovePackageBtn == default)
                return;

            // 只剩一个包时禁用删除按钮
            mYooRemovePackageBtn.SetEnabled(YooSetting.Packages.Count > 1);
        }

        /// <summary>
        /// 获取规则的简短显示名称
        /// </summary>
        public static string GetYooShortRuleName(string ruleName)
        {
            if (string.IsNullOrEmpty(ruleName))
                return ruleName;

            return sYooRuleShortNames.TryGetValue(ruleName, out var shortName) ? shortName : ruleName;
        }

        /// <summary>
        /// 获取 CollectorType 的简短显示名称
        /// </summary>
        public static string GetYooCollectorTypeShortName(ECollectorType collectorType)
        {
            return collectorType switch
            {
                ECollectorType.MainAssetCollector => "主资源",
                ECollectorType.StaticAssetCollector => "静态",
                ECollectorType.DependAssetCollector => "依赖",
                _ => collectorType.ToString()
            };
        }

        #endregion
    }
}
#endif
