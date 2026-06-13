#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER && UNITY_2022_1_OR_NEWER
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - 3.x 打包配置 UI
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        /// <summary>
        /// 打包配置面板的当前选择状态。
        /// </summary>
        private sealed class BuildSelectionStateV3
        {
            public string Package;
            public string Pipeline;
        }

        #region 打开收集器

        /// <summary>
        /// 打开 YokiFrame 工具窗口并切换到 YooAsset 资源收集标签页
        /// </summary>
        private static void OpenYooAssetCollectorV3()
        {
            YokiToolsWindow.OpenAndSelectPage<ResKitToolPage>(page =>
            {
                page.SwitchToYooAssetCollector();
            });
        }

        #endregion

        #region 打包配置 UI（3.x）

        /// <summary>
        /// 创建 3.x 打包配置卡片
        /// </summary>
        public static VisualElement CreateBuildConfigCardV3(SerializedProperty property)
        {
            var (card, body) = CreateConfigCard("打包配置", KitIcons.SETTINGS, true, false);

            var packageNames = GetBuildPackageNamesV3();
            if (packageNames.Count == 0)
            {
                CreateEmptyPackageHintV3(body);
                return card;
            }

            var state = new BuildSelectionStateV3
            {
                Package = packageNames[0],
                Pipeline = BundleBuilderSetting.GetPackageBuildPipeline(packageNames[0])
            };

            var dynamicContainer = new VisualElement();
            dynamicContainer.style.flexDirection = FlexDirection.Column;
            dynamicContainer.style.marginTop = Spacing.SM;

            CreateBasicBuildOptionsV3(body, packageNames, state, dynamicContainer);
            body.Add(dynamicContainer);
            RefreshBuildDynamicSectionV3(dynamicContainer, state);

            return card;
        }

        /// <summary>
        /// 创建空包提示
        /// </summary>
        private static void CreateEmptyPackageHintV3(VisualElement body)
        {
            var hint = new Label("未找到资源包配置，请先在 YooAsset Collector 中配置资源包");
            hint.style.color = new StyleColor(Colors.StatusWarning);
            hint.style.fontSize = 11;
            hint.style.whiteSpace = WhiteSpace.Normal;
            body.Add(hint);

            var emptyOpenCollectorBtn = CreateActionButton("打开资源收集器", Colors.BrandPrimary, () =>
            {
                EditorApplication.ExecuteMenuItem("YooAsset/Bundle Collector");
            });
            emptyOpenCollectorBtn.style.marginTop = Spacing.SM;
            body.Add(emptyOpenCollectorBtn);
        }

        /// <summary>
        /// 创建基础构建选项
        /// </summary>
        private static void CreateBasicBuildOptionsV3(
            VisualElement body,
            List<string> packageNames,
            BuildSelectionStateV3 state,
            VisualElement dynamicContainer)
        {
            body.Add(CreateLabeledDropdown("资源包", packageNames, state.Package, newValue =>
            {
                state.Package = newValue;
                state.Pipeline = BundleBuilderSetting.GetPackageBuildPipeline(state.Package);
                RefreshBuildDynamicSectionV3(dynamicContainer, state);
            }));

            int pipelineIndex = sBuildPipelineChoicesV3.IndexOf(state.Pipeline);
            if (pipelineIndex < 0) pipelineIndex = 0;

            body.Add(CreateLabeledDropdown("构建管线", sBuildPipelineChoicesV3, sBuildPipelineChoicesV3[pipelineIndex], newValue =>
            {
                state.Pipeline = newValue;
                BundleBuilderSetting.SetPackageBuildPipeline(state.Package, newValue);
                RefreshBuildDynamicSectionV3(dynamicContainer, state);
            }));

            var compressOption = BundleBuilderSetting.GetPackageCompressOption(state.Package, state.Pipeline);
            body.Add(CreateLabeledDropdown("压缩方式", sCompressChoices, sCompressChoices[(int)compressOption], newValue =>
            {
                int idx = sCompressChoices.IndexOf(newValue);
                BundleBuilderSetting.SetPackageCompressOption(state.Package, state.Pipeline, (ECompressOption)idx);
            }));

            var copyOption = BundleBuilderSetting.GetPackageBundledCopyOption(state.Package, state.Pipeline);
            body.Add(CreateLabeledDropdown("首包拷贝", sCopyOptionDisplayNames, sCopyOptionDisplayNames[(int)copyOption], newValue =>
            {
                int idx = sCopyOptionDisplayNames.IndexOf(newValue);
                BundleBuilderSetting.SetPackageBundledCopyOption(state.Package, state.Pipeline, (EBundledCopyOption)idx);
            }));

            var copyParams = BundleBuilderSetting.GetPackageBundledCopyParams(state.Package, state.Pipeline);
            body.Add(CreateLabeledTextField("拷贝标签", copyParams, "多个标签用分号分隔", newValue =>
            {
                BundleBuilderSetting.SetPackageBundledCopyParams(state.Package, state.Pipeline, newValue);
            }));
        }

        /// <summary>
        /// 刷新依赖当前选择状态的动态区域。
        /// </summary>
        private static void RefreshBuildDynamicSectionV3(VisualElement dynamicContainer, BuildSelectionStateV3 state)
        {
            dynamicContainer.Clear();
            CreateAdvancedBuildOptionsV3(dynamicContainer, state);
            CreateSeparatorV3(dynamicContainer);
            CreateBuildButtonsV3(dynamicContainer, state);
        }

        /// <summary>
        /// 创建高级构建选项
        /// </summary>
        private static void CreateAdvancedBuildOptionsV3(VisualElement body, BuildSelectionStateV3 state)
        {
            var advancedFoldout = new Foldout { text = "高级选项", value = false };
            advancedFoldout.style.marginTop = Spacing.SM;
            body.Add(advancedFoldout);

            var clearCache = BundleBuilderSetting.GetPackageClearBuildCache(state.Package, state.Pipeline);
            advancedFoldout.Add(CreateModernToggle("清空构建缓存", clearCache, newValue =>
            {
                BundleBuilderSetting.SetPackageClearBuildCache(state.Package, state.Pipeline, newValue);
            }));

            var useDepDB = BundleBuilderSetting.GetPackageUseAssetDependencyDB(state.Package, state.Pipeline);
            advancedFoldout.Add(CreateModernToggle("使用依赖缓存", useDepDB, newValue =>
            {
                BundleBuilderSetting.SetPackageUseAssetDependencyDB(state.Package, state.Pipeline, newValue);
            }));

            var encryptClass = BundleBuilderSetting.GetPackageBundleEncryptorClassName(state.Package, state.Pipeline);
            var encryptionClasses = GetEncryptionServiceClassNamesV3();
            int encryptIndex = encryptionClasses.IndexOf(encryptClass);
            if (encryptIndex < 0) encryptIndex = 0;
            advancedFoldout.Add(CreateLabeledDropdown("加密服务", encryptionClasses, encryptionClasses[encryptIndex], newValue =>
            {
                BundleBuilderSetting.SetPackageBundleEncryptorClassName(state.Package, state.Pipeline, newValue);
            }));
        }

        /// <summary>
        /// 创建分隔线
        /// </summary>
        private static void CreateSeparatorV3(VisualElement body)
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
        private static void CreateBuildButtonsV3(VisualElement body, BuildSelectionStateV3 state)
        {
            var buttonRow = new VisualElement();
            buttonRow.style.flexDirection = FlexDirection.Row;
            buttonRow.style.justifyContent = Justify.SpaceBetween;
            body.Add(buttonRow);

            var openCollectorBtn = CreateActionButton("打开收集器", Colors.TextSecondary, OpenYooAssetCollectorV3);
            openCollectorBtn.style.flexGrow = 1;
            openCollectorBtn.style.marginRight = Spacing.SM;
            buttonRow.Add(openCollectorBtn);

            var openOriginalCollectorBtn = CreateActionButton("打开原始收集器", Colors.TextSecondary, () =>
            {
                EditorApplication.ExecuteMenuItem("YooAsset/Bundle Collector");
            });
            openOriginalCollectorBtn.style.flexGrow = 1;
            buttonRow.Add(openOriginalCollectorBtn);

            var buildBtn = CreateActionButton("构建资源包", Colors.BrandSuccess, () =>
            {
                if (EditorUtility.DisplayDialog("确认构建",
                    $"确定要构建资源包 [{state.Package}] 吗？\n\n" +
                    $"管线: {state.Pipeline}\n" +
                    $"平台: {EditorUserBuildSettings.activeBuildTarget}",
                    "确定", "取消"))
                {
                    ExecuteBuildV3(state.Package, state.Pipeline);
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
