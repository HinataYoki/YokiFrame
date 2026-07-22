#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>
    /// YooAssetInitializationOptions 的 UI Toolkit Drawer。
    /// 本类型只描述 YooAsset 字段语义，所有视觉组件均由 InspectorKit 提供。
    /// </summary>
    [CustomPropertyDrawer(typeof(YooAssetInitializationOptions))]
    public sealed partial class YooAssetInitializationOptionsDrawer : PropertyDrawer
    {
        private const string BASIC_CARD_KEY = "YooAssetInitialization.Basic";
        private const string REMOTE_CARD_KEY = "YooAssetInitialization.Remote";
        private const long PACKAGE_SYNC_INTERVAL_MS = 1000;

        /// <summary>创建由 InspectorKit 卡片、字段和列表组件组成的初始化参数界面。</summary>
        /// <param name="property">初始化参数序列化属性。</param>
        /// <returns>完整 UI Toolkit 视觉树。</returns>
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            VisualElement root = InspectorKitUi.CreateRoot();
            VisualElement panel = InspectorKitUi.CreatePanel(property.displayName);
            Action refreshRemote = null;
            panel.Add(CreateBasicCard(property, () => refreshRemote?.Invoke()));
            panel.Add(CreateRemoteCard(property, out refreshRemote));
            VisualElement encryptionCard = CreateEncryptionCard(property);
            if (encryptionCard != null)
                panel.Add(encryptionCard);
            panel.Add(CreateBuildCard(property));
            root.Add(panel);
            return root;
        }

        /// <summary>创建运行模式、package 列表和 manifest 设置卡片。</summary>
        private static VisualElement CreateBasicCard(
            SerializedProperty property,
            Action refreshRemote)
        {
            return InspectorKitUi.CreateCard(
                "基础配置",
                BASIC_CARD_KEY,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    body.Add(CreateEnumRow(
                        property.FindPropertyRelative(nameof(YooAssetInitializationOptions.EditorPlayMode)),
                        "编辑器运行模式",
                        null));
                    body.Add(CreateRuntimeModeRow(property, refreshRemote));
                    body.Add(CreatePackageList(property));
                    body.Add(InspectorKitUi.CreateSwitchRow(
                        property.FindPropertyRelative(nameof(YooAssetInitializationOptions.LoadManifestAfterInitialization)),
                        "初始化后加载清单"));
                    body.Add(InspectorKitUi.CreateIntegerRow(
                        property.FindPropertyRelative(nameof(YooAssetInitializationOptions.ManifestTimeoutSeconds)),
                        "清单超时秒数"));
                });
        }

        /// <summary>创建只在 Host/Web 模式有实际输入的远端地址卡片。</summary>
        private static VisualElement CreateRemoteCard(
            SerializedProperty property,
            out Action refresh)
        {
            Action refreshContent = null;
            VisualElement card = InspectorKitUi.CreateCard(
                "远端资源",
                REMOTE_CARD_KEY,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    VisualElement content = new();
                    body.Add(content);
                    refreshContent = () => InspectorKitUi.Refresh(
                        content,
                        target => BuildRemoteContent(target, property));
                    refreshContent();
                });
            refresh = refreshContent;
            return card;
        }

        /// <summary>创建排除 EditorSimulateMode 的 Player 运行模式下拉行。</summary>
        private static VisualElement CreateRuntimeModeRow(
            SerializedProperty property,
            Action onChanged)
        {
            SerializedProperty mode = property.FindPropertyRelative(
                nameof(YooAssetInitializationOptions.RuntimePlayMode));
            List<string> choices = new();
            List<int> values = new();
            AddEnumChoice(mode, choices, values, nameof(EPlayMode.OfflinePlayMode));
            AddEnumChoice(mode, choices, values, nameof(EPlayMode.HostPlayMode));
            AddEnumChoice(mode, choices, values, nameof(EPlayMode.WebPlayMode));
            AddEnumChoice(mode, choices, values, nameof(EPlayMode.CustomPlayMode));
            return CreateMappedDropdown(mode, "Player 运行模式", choices, values, onChanged);
        }

        /// <summary>按当前 Player 模式刷新远端字段或显示本地模式说明。</summary>
        private static void BuildRemoteContent(
            VisualElement container,
            SerializedProperty property)
        {
            SerializedProperty mode = property.FindPropertyRelative(
                nameof(YooAssetInitializationOptions.RuntimePlayMode));
            bool usesRemote = mode != null
                && (mode.enumValueIndex == (int)EPlayMode.HostPlayMode
                    || mode.enumValueIndex == (int)EPlayMode.WebPlayMode);
            if (!usesRemote)
            {
                container.Add(InspectorKitUi.CreateInfoBox(
                    "当前模式无需远端地址",
                    "OfflinePlayMode 和 CustomPlayMode 由本地文件系统或项目初始化回调提供资源。",
                    InspectorInfoBoxType.Info));
                return;
            }

            container.Add(InspectorKitUi.CreateInfoBox(
                "Host / Web",
                "YooAsset V2 可直接使用主备地址；V3 由 HostInitializationHandler 或 WebInitializationHandler 创建文件系统。",
                InspectorInfoBoxType.Info));
            container.Add(InspectorKitUi.CreateStringRow(
                property.FindPropertyRelative(nameof(YooAssetInitializationOptions.DefaultHostServer)),
                "主资源服务器"));
            container.Add(InspectorKitUi.CreateStringRow(
                property.FindPropertyRelative(nameof(YooAssetInitializationOptions.FallbackHostServer)),
                "备用资源服务器"));
        }

        /// <summary>创建从 YooAsset 收集器自动同步的只读 package 列表。</summary>
        private static VisualElement CreatePackageList(SerializedProperty property)
        {
            YooAssetBuildSettingsAdapter.SynchronizePackageNames(property);
            VisualElement container = new();
            container.Add(CreateReadOnlyPackageList(property));
            container.schedule.Execute(
                () => RefreshPackageListFromCollector(container, property))
                .Every(PACKAGE_SYNC_INTERVAL_MS);
            return container;
        }

        /// <summary>创建只读 package 列表视觉树，并标记首项为默认包。</summary>
        private static VisualElement CreateReadOnlyPackageList(SerializedProperty property)
        {
            SerializedProperty packages = property.FindPropertyRelative(
                nameof(YooAssetInitializationOptions.PackageNames));
            InspectorStringListOptions options = new()
            {
                Title = "资源包列表（YooAsset 收集器）",
                MarkerFactory = index => index == 0 ? "默认" : "#" + (index + 1),
                IsReadOnly = true
            };
            return InspectorKitUi.CreateStringList(packages, options);
        }

        /// <summary>收集器 package 发生变化时同步数据并局部重建列表。</summary>
        private static void RefreshPackageListFromCollector(
            VisualElement container,
            SerializedProperty property)
        {
            if (!YooAssetBuildSettingsAdapter.SynchronizePackageNames(property))
                return;

            InspectorKitUi.Refresh(
                container,
                target => target.Add(CreateReadOnlyPackageList(property)));
        }

        /// <summary>创建包含完整枚举值的下拉字段，并写回 SerializedProperty。</summary>
        private static VisualElement CreateEnumRow(
            SerializedProperty property,
            string label,
            Action onChanged)
        {
            if (property == null)
                return InspectorKitUi.CreateInfoBox("未找到枚举序列化字段。", InspectorInfoBoxType.Error);

            List<string> choices = new(property.enumDisplayNames);
            List<int> values = new();
            for (int index = 0; index < choices.Count; index++)
                values.Add(index);
            return CreateMappedDropdown(property, label, choices, values, onChanged);
        }

        /// <summary>创建显示文本与真实枚举索引分离的下拉字段。</summary>
        private static VisualElement CreateMappedDropdown(
            SerializedProperty property,
            string label,
            List<string> choices,
            List<int> values,
            Action onChanged = null)
        {
            int selectedIndex = values.IndexOf(property.enumValueIndex);
            if (selectedIndex < 0)
                selectedIndex = 0;

            return InspectorKitUi.CreateDropdownRow(label, choices, selectedIndex, index =>
            {
                if (index < 0 || index >= values.Count)
                    return;
                property.enumValueIndex = values[index];
                property.serializedObject.ApplyModifiedProperties();
                onChanged?.Invoke();
            });
        }

        /// <summary>按枚举成员名向过滤列表追加显示文本和真实索引。</summary>
        private static void AddEnumChoice(
            SerializedProperty property,
            List<string> choices,
            List<int> values,
            string enumName)
        {
            if (property == null)
                return;

            for (int index = 0; index < property.enumNames.Length; index++)
            {
                if (!string.Equals(property.enumNames[index], enumName, StringComparison.Ordinal))
                    continue;

                choices.Add(property.enumDisplayNames[index]);
                values.Add(index);
                return;
            }
        }

    }
}
#endif
