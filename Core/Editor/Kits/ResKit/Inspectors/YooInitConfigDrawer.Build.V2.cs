#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && !YOOASSET_3_0_OR_NEWER && UNITY_2022_1_OR_NEWER
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - 2.x 打包配置 UI
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        /// <summary>
        /// 打包配置面板的当前选择状态（2.x）。
        /// </summary>
        private sealed class BuildSelectionStateV2
        {
            public string Package;
            public string Pipeline;
        }

        #region 打包配置 UI（2.x）

        /// <summary>
        /// 创建 2.x 打包配置卡片
        /// </summary>
        public static VisualElement CreateBuildConfigCardV2(SerializedProperty property)
        {
            var (card, body) = CreateConfigCard("打包配置", KitIcons.SETTINGS, "BuildConfig", true, false);

            var packageNames = GetBuildPackageNamesV2();
            if (packageNames.Count == 0)
            {
                CreateEmptyPackageHintV2(body);
                return card;
            }

            var state = new BuildSelectionStateV2
            {
                Package = packageNames[0],
                Pipeline = AssetBundleBuilderSetting.GetPackageBuildPipeline(packageNames[0])
            };

            // 2.x 管线名称适配（BuiltinBuildPipeline 替代 ArchiveFileBuildPipeline）
            if (!sBuildPipelineChoicesV2.Contains(state.Pipeline))
                state.Pipeline = nameof(EBuildPipeline.ScriptableBuildPipeline);

            var dynamicContainer = new VisualElement();
            dynamicContainer.style.flexDirection = FlexDirection.Column;
            dynamicContainer.style.marginTop = Spacing.SM;

            CreateBasicBuildOptionsV2(body, packageNames, state, dynamicContainer);
            body.Add(dynamicContainer);
            RefreshBuildDynamicSectionV2(dynamicContainer, state);

            return card;
        }

        /// <summary>
        /// 创建空包提示（2.x）
        /// </summary>
        private static void CreateEmptyPackageHintV2(VisualElement body)
        {
            var hint = new Label("未找到资源包配置，请先在 YooAsset AssetBundle Collector 中配置资源包");
            hint.style.color = new StyleColor(Colors.StatusWarning);
            hint.style.fontSize = 11;
            hint.style.whiteSpace = WhiteSpace.Normal;
            body.Add(hint);

            var openCollectorBtn = CreateActionButton("打开资源收集器", Colors.BrandPrimary, () =>
            {
                EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector");
            });
            openCollectorBtn.style.marginTop = Spacing.SM;
            body.Add(openCollectorBtn);
        }

        /// <summary>
        /// 创建基础构建选项（2.x）
        /// </summary>
        private static void CreateBasicBuildOptionsV2(
            VisualElement body,
            List<string> packageNames,
            BuildSelectionStateV2 state,
            VisualElement dynamicContainer)
        {
            body.Add(CreateLabeledDropdown("资源包", packageNames, state.Package, newValue =>
            {
                state.Package = newValue;
                state.Pipeline = AssetBundleBuilderSetting.GetPackageBuildPipeline(state.Package);
                if (!sBuildPipelineChoicesV2.Contains(state.Pipeline))
                    state.Pipeline = sBuildPipelineChoicesV2[0];
                RefreshBuildDynamicSectionV2(dynamicContainer, state);
            }));

            int pipelineIndex = sBuildPipelineChoicesV2.IndexOf(state.Pipeline);
            if (pipelineIndex < 0) pipelineIndex = 0;

            body.Add(CreateLabeledDropdown("构建管线", sBuildPipelineChoicesV2, sBuildPipelineChoicesV2[pipelineIndex], newValue =>
            {
                state.Pipeline = newValue;
                AssetBundleBuilderSetting.SetPackageBuildPipeline(state.Package, newValue);
                RefreshBuildDynamicSectionV2(dynamicContainer, state);
            }));

            var compressOption = AssetBundleBuilderSetting.GetPackageCompressOption(state.Package, state.Pipeline);
            body.Add(CreateLabeledDropdown("压缩方式", sCompressChoices, sCompressChoices[(int)compressOption], newValue =>
            {
                int idx = sCompressChoices.IndexOf(newValue);
                AssetBundleBuilderSetting.SetPackageCompressOption(state.Package, state.Pipeline, (ECompressOption)idx);
            }));

            var copyOption = AssetBundleBuilderSetting.GetPackageBuildinFileCopyOption(state.Package, state.Pipeline);
            body.Add(CreateLabeledDropdown("首包拷贝", sCopyOptionDisplayNames, sCopyOptionDisplayNames[(int)copyOption], newValue =>
            {
                int idx = sCopyOptionDisplayNames.IndexOf(newValue);
                AssetBundleBuilderSetting.SetPackageBuildinFileCopyOption(state.Package, state.Pipeline, (EBuildinFileCopyOption)idx);
            }));

            var copyParams = AssetBundleBuilderSetting.GetPackageBuildinFileCopyParams(state.Package, state.Pipeline);
            body.Add(CreateLabeledTextField("拷贝标签", copyParams, "多个标签用分号分隔", newValue =>
            {
                AssetBundleBuilderSetting.SetPackageBuildinFileCopyParams(state.Package, state.Pipeline, newValue);
            }));
        }

        /// <summary>
        /// 刷新依赖当前选择状态的动态区域（2.x）。
        /// </summary>
        private static void RefreshBuildDynamicSectionV2(VisualElement dynamicContainer, BuildSelectionStateV2 state)
        {
            dynamicContainer.Clear();
            CreateAdvancedBuildOptionsV2(dynamicContainer, state);
            CreateSeparatorV2(dynamicContainer);
            CreateBuildButtonsV2(dynamicContainer, state);
        }

        /// <summary>
        /// 创建高级构建选项（2.x）
        /// </summary>
        private static void CreateAdvancedBuildOptionsV2(VisualElement body, BuildSelectionStateV2 state)
        {
            var advancedFoldout = new Foldout { text = "高级选项", value = false };
            advancedFoldout.style.marginTop = Spacing.SM;
            body.Add(advancedFoldout);

            var clearCache = AssetBundleBuilderSetting.GetPackageClearBuildCache(state.Package, state.Pipeline);
            advancedFoldout.Add(CreateModernToggle("清空构建缓存", clearCache, newValue =>
            {
                AssetBundleBuilderSetting.SetPackageClearBuildCache(state.Package, state.Pipeline, newValue);
            }));

            var useDepDB = AssetBundleBuilderSetting.GetPackageUseAssetDependencyDB(state.Package, state.Pipeline);
            advancedFoldout.Add(CreateModernToggle("使用依赖缓存", useDepDB, newValue =>
            {
                AssetBundleBuilderSetting.SetPackageUseAssetDependencyDB(state.Package, state.Pipeline, newValue);
            }));

            var encryptClass = AssetBundleBuilderSetting.GetPackageEncyptionServicesClassName(state.Package, state.Pipeline);
            var encryptionClasses = GetEncryptionServiceClassNamesV2();
            int encryptIndex = encryptionClasses.IndexOf(encryptClass);
            if (encryptIndex < 0) encryptIndex = 0;
            advancedFoldout.Add(CreateLabeledDropdown("加密服务", encryptionClasses, encryptionClasses[encryptIndex], newValue =>
            {
                AssetBundleBuilderSetting.SetPackageEncyptionServicesClassName(state.Package, state.Pipeline, newValue);
            }));
        }

        /// <summary>
        /// 创建分隔线（2.x）
        /// </summary>
        private static void CreateSeparatorV2(VisualElement body)
        {
            var separator = new VisualElement();
            separator.style.height = 1;
            separator.style.backgroundColor = new StyleColor(Colors.BorderDefault);
            separator.style.marginTop = Spacing.MD;
            separator.style.marginBottom = Spacing.SM;
            body.Add(separator);
        }

        /// <summary>
        /// 创建构建操作按钮（2.x）
        /// </summary>
        private static void CreateBuildButtonsV2(VisualElement body, BuildSelectionStateV2 state)
        {
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.SpaceBetween;
            body.Add(buttonRow);

            var openCollectorBtn = CreateActionButton("打开收集器", Colors.TextSecondary, () =>
            {
                EditorApplication.ExecuteMenuItem("YooAsset/AssetBundle Collector");
            });
            openCollectorBtn.style.flexGrow = 1;
            openCollectorBtn.style.marginRight = Spacing.SM;
            buttonRow.Add(openCollectorBtn);

            var openBuilderBtn = CreateActionButton("构建窗口", Colors.BrandPrimary, () =>
            {
                ExecuteBuildV2(state.Package, state.Pipeline);
            });
            openBuilderBtn.style.flexGrow = 1;
            openBuilderBtn.style.marginLeft = Spacing.SM;
            buttonRow.Add(openBuilderBtn);
        }

        #endregion
    }
}
#endif
