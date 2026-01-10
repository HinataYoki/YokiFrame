#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && UNITY_2022_1_OR_NEWER
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - 打包配置 UI
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        #region 打包配置

        /// <summary>
        /// 创建打包配置卡片
        /// </summary>
        private static VisualElement CreateBuildConfigCard(SerializedProperty property)
        {
            var (card, body) = CreateConfigCard("打包配置", KitIcons.SETTINGS, true, false);

            var packageNames = GetBuildPackageNames();
            if (packageNames.Count == 0)
            {
                CreateEmptyPackageHint(body);
                return card;
            }

            string selectedPackage = packageNames[0];
            string selectedPipeline = AssetBundleBuilderSetting.GetPackageBuildPipeline(selectedPackage);

            // 基础配置
            CreateBasicBuildOptions(body, packageNames, ref selectedPackage, ref selectedPipeline);

            // 高级选项
            CreateAdvancedBuildOptions(body, selectedPackage, selectedPipeline);

            // 分隔线
            CreateSeparator(body);

            // 操作按钮
            CreateBuildButtons(body, selectedPackage, selectedPipeline);

            return card;
        }

        /// <summary>
        /// 创建空包提示
        /// </summary>
        private static void CreateEmptyPackageHint(VisualElement body)
        {
            var hint = new Label("未找到资源包配置，请先在 YooAsset Collector 中配置资源包");
            hint.style.color = new StyleColor(Colors.StatusWarning);
            hint.style.fontSize = 11;
            hint.style.whiteSpace = WhiteSpace.Normal;
            body.Add(hint);

            var emptyOpenCollectorBtn = CreateActionButton("打开资源收集器", Colors.BrandPrimary, () =>
            {
                EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector");
            });
            emptyOpenCollectorBtn.style.marginTop = Spacing.SM;
            body.Add(emptyOpenCollectorBtn);
        }

        /// <summary>
        /// 创建基础构建选项
        /// </summary>
        private static void CreateBasicBuildOptions(VisualElement body, List<string> packageNames, ref string selectedPackage, ref string selectedPipeline)
        {
            string package = selectedPackage;
            string pipeline = selectedPipeline;

            body.Add(CreateLabeledDropdown("资源包", packageNames, package, newValue =>
            {
                package = newValue;
                pipeline = AssetBundleBuilderSetting.GetPackageBuildPipeline(package);
            }));

            int pipelineIndex = sBuildPipelineChoices.IndexOf(pipeline);
            if (pipelineIndex < 0) pipelineIndex = 0;

            body.Add(CreateLabeledDropdown("构建管线", sBuildPipelineChoices, sBuildPipelineChoices[pipelineIndex], newValue =>
            {
                pipeline = newValue;
                AssetBundleBuilderSetting.SetPackageBuildPipeline(package, newValue);
            }));

            var compressOption = AssetBundleBuilderSetting.GetPackageCompressOption(package, pipeline);
            body.Add(CreateLabeledDropdown("压缩方式", sCompressChoices, sCompressChoices[(int)compressOption], newValue =>
            {
                int idx = sCompressChoices.IndexOf(newValue);
                AssetBundleBuilderSetting.SetPackageCompressOption(package, pipeline, (ECompressOption)idx);
            }));

            var copyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(package, pipeline);
            body.Add(CreateLabeledDropdown("首包拷贝", sCopyOptionDisplayNames, sCopyOptionDisplayNames[(int)copyOption], newValue =>
            {
                int idx = sCopyOptionDisplayNames.IndexOf(newValue);
                AssetBundleBuilderSetting.SetPackageBuildinFileCopyOption(package, pipeline, (EBuildinFileCopyOption)idx);
            }));

            var copyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(package, pipeline);
            body.Add(CreateLabeledTextField("拷贝标签", copyParams, "多个标签用分号分隔", newValue =>
            {
                AssetBundleBuilderSetting.SetPackageBuildinFileCopyParams(package, pipeline, newValue);
            }));

            selectedPackage = package;
            selectedPipeline = pipeline;
        }

        /// <summary>
        /// 创建高级构建选项
        /// </summary>
        private static void CreateAdvancedBuildOptions(VisualElement body, string selectedPackage, string selectedPipeline)
        {
            var advancedFoldout = new Foldout { text = "高级选项", value = false };
            advancedFoldout.style.marginTop = Spacing.SM;
            body.Add(advancedFoldout);

            var clearCache = AssetBundleBuilderSetting.GetPackageClearBuildCache(selectedPackage, selectedPipeline);
            advancedFoldout.Add(CreateModernToggle("清空构建缓存", clearCache, newValue =>
            {
                AssetBundleBuilderSetting.SetPackageClearBuildCache(selectedPackage, selectedPipeline, newValue);
            }));

            var useDepDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(selectedPackage, selectedPipeline);
            advancedFoldout.Add(CreateModernToggle("使用依赖缓存", useDepDB, newValue =>
            {
                AssetBundleBuilderSetting.SetPackageUseAssetDependencyDB(selectedPackage, selectedPipeline, newValue);
            }));

            var encryptClass = AssetBundleBuilderSetting.GetPackageEncyptionServicesClassName(selectedPackage, selectedPipeline);
            var encryptionClasses = GetEncryptionServiceClassNames();
            int encryptIndex = encryptionClasses.IndexOf(encryptClass);
            if (encryptIndex < 0) encryptIndex = 0;
            advancedFoldout.Add(CreateLabeledDropdown("加密服务", encryptionClasses, encryptionClasses[encryptIndex], newValue =>
            {
                AssetBundleBuilderSetting.SetPackageEncyptionServicesClassName(selectedPackage, selectedPipeline, newValue);
            }));
        }

        /// <summary>
        /// 创建分隔线
        /// </summary>
        private static void CreateSeparator(VisualElement body)
        {
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new StyleColor(Colors.BorderDefault);
            separator.style.marginTop = Spacing.MD;
            separator.style.marginBottom = Spacing.SM;
            body.Add(separator);
        }

        /// <summary>
        /// 创建构建操作按钮
        /// </summary>
        private static void CreateBuildButtons(VisualElement body, string selectedPackage, string selectedPipeline)
        {
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.SpaceBetween;
            body.Add(buttonRow);

            var openCollectorBtn = CreateActionButton("打开收集器", Colors.TextSecondary, OpenYooAssetCollector);
            openCollectorBtn.style.flexGrow = 1;
            openCollectorBtn.style.marginRight = Spacing.SM;
            buttonRow.Add(openCollectorBtn);

            var openOriginalCollectorBtn = CreateActionButton("打开原始收集器", Colors.TextSecondary, () =>
            {
                EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector");
            });
            openOriginalCollectorBtn.style.flexGrow = 1;
            buttonRow.Add(openOriginalCollectorBtn);

            var buildBtn = CreateActionButton("构建资源包", Colors.BrandSuccess, () =>
            {
                if (EditorUtility.DisplayDialog("确认构建",
                    $"确定要构建资源包 [{selectedPackage}] 吗？\n\n" +
                    $"管线: {selectedPipeline}\n" +
                    $"平台: {EditorUserBuildSettings.activeBuildTarget}",
                    "确定", "取消"))
                {
                    ExecuteBuild(selectedPackage, selectedPipeline);
                }
            });
            buildBtn.style.marginTop = Spacing.SM;
            buildBtn.style.height = 32;
            body.Add(buildBtn);
        }

        #endregion
    }
}
#endif
