#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;
using YooAsset.Editor;

namespace YokiFrame.Unity
{
    public sealed partial class YooAssetInitializationOptionsDrawer
    {
        private const string BUILD_CARD_KEY = "YooAssetInitialization.Build";
        private const string BUILD_ADVANCED_KEY = "YooAssetInitialization.Build.Advanced";

        private static readonly List<string> sCompressNames = new(
            Enum.GetNames(typeof(ECompressOption)));

        private static readonly List<string> sCopyOptionNames = new()
        {
            "不拷贝",
            "清空后拷贝全部",
            "清空后按标签拷贝",
            "直接拷贝全部",
            "直接按标签拷贝"
        };

        /// <summary>创建映射 YooAsset EditorPrefs 构建设置的打包配置卡片。</summary>
        private static VisualElement CreateBuildCard(SerializedProperty property)
        {
            return InspectorKitUi.CreateCard(
                "打包配置",
                BUILD_CARD_KEY,
                InspectorCardInitialState.Collapsed,
                body =>
                {
                    VisualElement content = new();
                    body.Add(content);
                    BuildBuildContent(content, property);
                });
        }

        /// <summary>读取当前 package 与管线后重建完整打包配置。</summary>
        private static void BuildBuildContent(
            VisualElement container,
            SerializedProperty property)
        {
            YooAssetBuildSettingsAdapter.SynchronizePackageNames(property);
            List<string> packages = YooAssetBuildSettingsAdapter.GetCollectorPackageNames();
            string packageName = packages[0];
            IReadOnlyList<string> pipelines = YooAssetBuildSettingsAdapter.GetBuildPipelineNames();
            string pipelineName = YooAssetBuildSettingsAdapter.GetBuildPipeline(packageName);
            if (!Contains(pipelines, pipelineName))
                pipelineName = pipelines[0];

            RebuildBuildRows(container, property, packages, packageName, pipelines, pipelineName);
        }

        /// <summary>向打包卡片添加 package、压缩、拷贝、高级选项和工具入口。</summary>
        private static void RebuildBuildRows(
            VisualElement container,
            SerializedProperty property,
            List<string> packages,
            string packageName,
            IReadOnlyList<string> pipelines,
            string pipelineName)
        {
            InspectorKitUi.Refresh(container, body =>
            {
                body.Add(CreateBuildPackageRow(
                    container,
                    property,
                    packages,
                    packageName,
                    pipelines));
                body.Add(CreateBuildPipelineRow(
                    container,
                    property,
                    packages,
                    packageName,
                    pipelines,
                    pipelineName));
                body.Add(CreateCompressRow(packageName, pipelineName));
                body.Add(CreateCopyOptionRow(packageName, pipelineName));
                body.Add(InspectorKitUi.CreateStringRow(
                    "拷贝标签",
                    YooAssetBuildSettingsAdapter.GetCopyParams(packageName, pipelineName),
                    value => YooAssetBuildSettingsAdapter.SetCopyParams(
                        packageName,
                        pipelineName,
                        value)));
                body.Add(CreateBuildAdvancedOptions(property, packageName, pipelineName));
                body.Add(InspectorKitUi.CreateSeparator());
                body.Add(CreateBuildButtons(property, packageName, pipelineName));
            });
        }

        /// <summary>创建 package 选择行，并在变化时刷新后续设置。</summary>
        private static VisualElement CreateBuildPackageRow(
            VisualElement container,
            SerializedProperty property,
            List<string> packages,
            string packageName,
            IReadOnlyList<string> pipelines)
        {
            return InspectorKitUi.CreateDropdownRow(
                "资源包",
                packages,
                Math.Max(0, packages.IndexOf(packageName)),
                index =>
                {
                    string nextPackage = packages[index];
                    string nextPipeline = YooAssetBuildSettingsAdapter.GetBuildPipeline(nextPackage);
                    if (!Contains(pipelines, nextPipeline))
                        nextPipeline = pipelines[0];
                    RebuildBuildRows(
                        container,
                        property,
                        packages,
                        nextPackage,
                        pipelines,
                        nextPipeline);
                });
        }

        /// <summary>创建构建管线选择行，并写回 YooAsset EditorPrefs。</summary>
        private static VisualElement CreateBuildPipelineRow(
            VisualElement container,
            SerializedProperty property,
            List<string> packages,
            string packageName,
            IReadOnlyList<string> pipelines,
            string pipelineName)
        {
            return InspectorKitUi.CreateDropdownRow(
                "构建管线",
                pipelines,
                IndexOf(pipelines, pipelineName),
                index =>
                {
                    string nextPipeline = pipelines[index];
                    YooAssetBuildSettingsAdapter.SetBuildPipeline(packageName, nextPipeline);
                    RebuildBuildRows(
                        container,
                        property,
                        packages,
                        packageName,
                        pipelines,
                        nextPipeline);
                });
        }

        /// <summary>创建构建压缩方式选择行。</summary>
        private static VisualElement CreateCompressRow(string packageName, string pipelineName)
        {
            int selected = (int)YooAssetBuildSettingsAdapter.GetCompressOption(
                packageName,
                pipelineName);
            return InspectorKitUi.CreateDropdownRow(
                "压缩方式",
                sCompressNames,
                selected,
                index => YooAssetBuildSettingsAdapter.SetCompressOption(
                    packageName,
                    pipelineName,
                    (ECompressOption)index));
        }

        /// <summary>创建首包拷贝策略选择行。</summary>
        private static VisualElement CreateCopyOptionRow(string packageName, string pipelineName)
        {
            int selected = YooAssetBuildSettingsAdapter.GetCopyOption(packageName, pipelineName);
            return InspectorKitUi.CreateDropdownRow(
                "首包拷贝",
                sCopyOptionNames,
                selected,
                index => YooAssetBuildSettingsAdapter.SetCopyOption(
                    packageName,
                    pipelineName,
                    index));
        }

        /// <summary>创建清缓存、依赖缓存和当前构建加密方案摘要。</summary>
        private static VisualElement CreateBuildAdvancedOptions(
            SerializedProperty property,
            string packageName,
            string pipelineName)
        {
            return InspectorKitUi.CreateFoldoutSection(
                "高级选项",
                BUILD_ADVANCED_KEY,
                InspectorCardInitialState.Collapsed,
                body =>
                {
                    body.Add(InspectorKitUi.CreateSwitchRow(
                        "清空构建缓存",
                        YooAssetBuildSettingsAdapter.GetClearBuildCache(packageName, pipelineName),
                        value => YooAssetBuildSettingsAdapter.SetClearBuildCache(
                            packageName,
                            pipelineName,
                            value)));
                    body.Add(InspectorKitUi.CreateSwitchRow(
                        "使用依赖缓存",
                        YooAssetBuildSettingsAdapter.GetUseAssetDependencyDatabase(
                            packageName,
                            pipelineName),
                        value => YooAssetBuildSettingsAdapter.SetUseAssetDependencyDatabase(
                            packageName,
                            pipelineName,
                            value)));
                    AddBuildEncryptionRows(body, property);
                });
        }

        /// <summary>仅在当前方案扫描到成对实现时显示构建加密与运行时解密类型。</summary>
        private static void AddBuildEncryptionRows(
            VisualElement body,
            SerializedProperty property)
        {
            SerializedProperty mode = property.FindPropertyRelative(
                nameof(YooAssetInitializationOptions.EncryptionMode));
            if (mode == null || mode.enumValueIndex <= (int)YooAssetEncryptionMode.None)
                return;
            if (!YooAssetEncryptionImplementationCatalog.TryGetPair(
                    (YooAssetEncryptionMode)mode.enumValueIndex,
                    out var pair))
            {
                return;
            }

            body.Add(InspectorKitUi.CreateReadOnlyStringRow(
                "构建加密实现",
                pair.EncryptionType.FullName));
            body.Add(InspectorKitUi.CreateReadOnlyStringRow(
                "运行时解密实现",
                pair.DecryptionType.FullName));
        }

        /// <summary>创建 YooAsset 官方窗口入口和使用当前方案的构建入口。</summary>
        private static VisualElement CreateBuildButtons(
            SerializedProperty property,
            string packageName,
            string pipelineName)
        {
            VisualElement container = new();
            Button collector = InspectorKitUi.CreateActionButton(
                "打开资源收集器",
                YooAssetEditorWindows.OpenCollector);
            Button builder = InspectorKitUi.CreateActionButton(
                "打开资源构建器",
                YooAssetEditorWindows.OpenBuilder);
            Button build = InspectorKitUi.CreateActionButton(
                "按当前方案构建",
                () => ConfirmBuild(property, packageName, pipelineName),
                InspectorActionStyle.Success);
            container.Add(InspectorKitUi.CreateButtonRow(collector, builder));
            container.Add(InspectorKitUi.CreateButtonRow(build));
            return container;
        }

        /// <summary>确认构建目标，并捕获不会受后续 Inspector 刷新影响的配置快照。</summary>
        private static void ConfirmBuild(
            SerializedProperty property,
            string packageName,
            string pipelineName)
        {
            YooAssetInitializationOptions options = CreateBuildOptionsSnapshot(property);
            string message = "资源包：" + packageName
                + "\n构建管线：" + pipelineName
                + "\n加密方案：" + options.EncryptionMode;
            if (!EditorUtility.DisplayDialog("构建 YooAsset 资源包", message, "构建", "取消"))
                return;

            EditorApplication.delayCall += () => ExecuteBuild(packageName, pipelineName, options);
        }

        /// <summary>执行 YooAsset 构建，并向用户显示成功目录或完整失败原因。</summary>
        private static void ExecuteBuild(
            string packageName,
            string pipelineName,
            YooAssetInitializationOptions options)
        {
            try
            {
                BuildResult result = YooAssetPackageBuilder.Build(packageName, pipelineName, options);
                if (result.Success)
                {
                    EditorUtility.RevealInFinder(result.OutputPackageDirectory);
                    EditorUtility.DisplayDialog("构建成功", result.OutputPackageDirectory, "确定");
                    return;
                }

                EditorUtility.DisplayDialog("构建失败", result.ErrorInfo, "确定");
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorUtility.DisplayDialog("构建异常", exception.Message, "确定");
            }
        }

        /// <summary>从 SerializedProperty 复制构建加密所需字段，避免保存行为对象。</summary>
        private static YooAssetInitializationOptions CreateBuildOptionsSnapshot(
            SerializedProperty property)
        {
            property.serializedObject.ApplyModifiedProperties();
            return new YooAssetInitializationOptions
            {
                EncryptionMode = (YooAssetEncryptionMode)property.FindPropertyRelative(
                    nameof(YooAssetInitializationOptions.EncryptionMode)).enumValueIndex,
                XorKeySeed = property.FindPropertyRelative(
                    nameof(YooAssetInitializationOptions.XorKeySeed)).stringValue,
                FileOffset = property.FindPropertyRelative(
                    nameof(YooAssetInitializationOptions.FileOffset)).intValue,
                AesPassword = property.FindPropertyRelative(
                    nameof(YooAssetInitializationOptions.AesPassword)).stringValue,
                AesSalt = property.FindPropertyRelative(
                    nameof(YooAssetInitializationOptions.AesSalt)).stringValue
            };
        }

        /// <summary>判断只读列表是否包含指定字符串。</summary>
        private static bool Contains(IReadOnlyList<string> values, string value)
        {
            return IndexOf(values, value) >= 0;
        }

        /// <summary>在只读列表中查找指定字符串索引，未找到时返回零。</summary>
        private static int IndexOf(IReadOnlyList<string> values, string value)
        {
            for (int index = 0; index < values.Count; index++)
            {
                if (string.Equals(values[index], value, StringComparison.Ordinal))
                    return index;
            }

            return 0;
        }
    }
}
#endif
