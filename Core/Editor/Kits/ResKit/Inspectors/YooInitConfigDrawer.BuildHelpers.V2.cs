#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_2_3_OR_NEWER && !YOOASSET_3_0_OR_NEWER
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using YooAsset;
using YooAsset.Editor;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - 2.x 构建辅助方法
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        /// <summary>2.x 构建管线选项列表</summary>
        private static readonly List<string> sBuildPipelineChoicesV2 = new()
        {
            nameof(EBuildPipeline.ScriptableBuildPipeline),
            nameof(EBuildPipeline.BuiltinBuildPipeline),
            nameof(EBuildPipeline.RawFileBuildPipeline)
        };

        #region 获取资源包名称

        /// <summary>
        /// 获取所有资源包名称（2.x 收集器数据）
        /// </summary>
        private static List<string> GetBuildPackageNamesV2()
        {
            var result = new List<string>();
            try
            {
                foreach (var package in AssetBundleCollectorSettingData.Setting.Packages)
                    result.Add(package.PackageName);
            }
            catch { }
            return result;
        }

        #endregion

        #region 获取加密服务类名

        /// <summary>
        /// 获取所有 IEncryptionServices 实现类名（2.x 加密接口）
        /// </summary>
        private static List<string> GetEncryptionServiceClassNamesV2()
        {
            var result = new List<string> { typeof(EncryptionNone).FullName };
            try
            {
                var types = TypeCache.GetTypesDerivedFrom<IEncryptionServices>();
                foreach (var type in types)
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!result.Contains(type.FullName))
                        result.Add(type.FullName);
                }
            }
            catch { }
            return result;
        }

        #endregion

        #region 执行构建

        /// <summary>
        /// 执行资源包构建 — 打开 YooAsset 2.x 内置构建窗口
        /// </summary>
        private static void ExecuteBuildV2(string packageName, string pipelineName)
        {
            // 将 package 和 pipeline 写入 EditorPrefs 以便构建窗口预选
            AssetBundleBuilderSetting.SetPackageBuildPipeline(packageName, pipelineName);

            if (EditorUtility.DisplayDialog("确认构建",
                $"即将打开 YooAsset 构建窗口。\n\n" +
                $"包名: {packageName}\n" +
                $"管线: {pipelineName}\n" +
                $"平台: {EditorUserBuildSettings.activeBuildTarget}\n\n" +
                $"请在 YooAsset 构建窗口中点击「构建」按钮。",
                "打开构建窗口", "取消"))
            {
                EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Builder");
            }
        }

        #endregion
    }
}
#endif
