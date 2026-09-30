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
            Action refreshModeDependent = null;
            Action refreshBasic = null;
            Action refreshRemote = null;
            panel.Add(CreateBasicCard(
                property,
                () => refreshModeDependent?.Invoke(),
                out refreshBasic));
            panel.Add(CreateRemoteCard(property, out refreshRemote));
            refreshModeDependent = () =>
            {
                refreshBasic?.Invoke();
                refreshRemote?.Invoke();
            };
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
            Action onModeChanged,
            out Action refreshNetwork)
        {
            Action refreshNetworkContent = null;
            VisualElement card = InspectorKitUi.CreateCard(
                "基础配置",
                BASIC_CARD_KEY,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    body.Add(CreateEnumRow(
                        property.FindPropertyRelative(nameof(YooAssetInitializationOptions.EditorPlayMode)),
                        "编辑器运行模式",
                        onModeChanged));
                    body.Add(CreateRuntimeModeRow(property, onModeChanged));
                    body.Add(CreatePackageList(property));
                    VisualElement networkContent = new();
                    body.Add(networkContent);
                    refreshNetworkContent = () => InspectorKitUi.Refresh(
                        networkContent,
                        target => BuildNetworkContent(target, property));
                    refreshNetworkContent();
                });
            refreshNetwork = () => refreshNetworkContent?.Invoke();
            return card;
        }

        /// <summary>仅在编辑器或 Player 至少一个运行模式联网时显示联网初始化和下载参数。</summary>
        private static void BuildNetworkContent(
            VisualElement container,
            SerializedProperty property)
        {
            if (!HasNetworkMode(property))
                return;

            SerializedProperty packages = property.FindPropertyRelative(
                nameof(YooAssetInitializationOptions.PackageNames));
            if (packages != null && packages.arraySize > 1)
            {
                container.Add(InspectorKitUi.CreateInfoBox(
                    "多包共用联网策略",
                    "ResKit 只安装一个 YooAsset Provider，但这个 Provider 可以代理多个 package。当前一键初始化让所有 package 共用编辑器/Player 运行模式、远端地址和回退策略；混合热更包与不热更包时，建议统一选择 Remote Then Offline，让不热更包在远端不存在时回退到包体资源。",
                    InspectorInfoBoxType.Info));
            }

            SerializedProperty strategy = property.FindPropertyRelative(
                nameof(YooAssetInitializationOptions.InitializationStrategy));
            VisualElement strategyInfo = new();
            container.Add(CreateEnumRow(
                strategy,
                "联网初始化策略",
                () => RefreshStrategyInfo(strategyInfo, strategy)));
            container.Add(strategyInfo);
            RefreshStrategyInfo(strategyInfo, strategy);
            container.Add(InspectorKitUi.CreateSwitchRow(
                property.FindPropertyRelative(nameof(YooAssetInitializationOptions.LoadManifestAfterInitialization)),
                "初始化后加载清单"));
            container.Add(InspectorKitUi.CreateIntegerRow(
                property.FindPropertyRelative(nameof(YooAssetInitializationOptions.ManifestTimeoutSeconds)),
                "清单超时秒数"));
            if (!HasHostMode(property))
                return;

            container.Add(InspectorKitUi.CreateSwitchRow(
                property.FindPropertyRelative(nameof(YooAssetInitializationOptions.CopyBuiltinPackageManifest)),
                "复制包体内置清单"));
            container.Add(InspectorKitUi.CreateStringRow(
                property.FindPropertyRelative(nameof(YooAssetInitializationOptions.CopyBuiltinPackageManifestDestRoot)),
                "内置清单复制目标目录"));
            container.Add(CreateEnumRow(
                property.FindPropertyRelative(nameof(YooAssetInitializationOptions.InstallCleanupMode)),
                "覆盖安装清理策略",
                null));
            container.Add(InspectorKitUi.CreateIntegerRow(
                property.FindPropertyRelative(nameof(YooAssetInitializationOptions.DownloadMaximumConcurrency)),
                "启动下载并发数"));
            container.Add(InspectorKitUi.CreateIntegerRow(
                property.FindPropertyRelative(nameof(YooAssetInitializationOptions.DownloadRetryCount)),
                "下载失败重试次数"));
            container.Add(InspectorKitUi.CreateIntegerRow(
                property.FindPropertyRelative(nameof(YooAssetInitializationOptions.DownloadMaxRequestPerFrame)),
                "每帧下载请求数"));
            container.Add(InspectorKitUi.CreateIntegerRow(
                property.FindPropertyRelative(nameof(YooAssetInitializationOptions.DownloadNoProgressTimeoutSeconds)),
                "下载无进度超时秒数"));
        }

        /// <summary>按当前策略重建蓝色说明框，让回退范围和 Web 限制在选择后立即可见。</summary>
        private static void RefreshStrategyInfo(
            VisualElement container,
            SerializedProperty strategy)
        {
            InspectorKitUi.Refresh(container, target =>
            {
                YooAssetInitializationStrategy value = strategy == null
                    ? YooAssetInitializationStrategy.ManifestOnly
                    : (YooAssetInitializationStrategy)strategy.enumValueIndex;
                string title;
                string message;
                switch (value)
                {
                    case YooAssetInitializationStrategy.RemoteOnly:
                        title = "Remote Only（仅远端）";
                        message = "必须访问远端；Host 会下载缺失资源，Web 按需请求资源。远端失败直接返回失败，不切换到旧缓存或包体内置版本。";
                        break;
                    case YooAssetInitializationStrategy.RemoteThenCached:
                        title = "Remote Then Cached（远端后缓存）";
                        message = "先执行远端更新；远端失败后优先使用上一次完整下载成功的缓存，缓存不可用时再尝试包体内置资源。仅适用于 HostPlayMode。";
                        break;
                    case YooAssetInitializationStrategy.RemoteThenOffline:
                        title = "Remote Then Offline（远端后离线）";
                        message = "先执行远端更新；失败后销毁联网 package，改用 OfflinePlayMode 加载包体内置资源。仅适用于 HostPlayMode。";
                        break;
                    default:
                        title = "Manifest Only（仅清单）";
                        message = "只初始化 package 并加载当前模式对应的清单，不自动下载整包；适合先检查更新，再由项目自行控制下载。";
                        break;
                }

                target.Add(InspectorKitUi.CreateInfoBox(
                    title,
                    message,
                    InspectorInfoBoxType.Info));
            });
        }

        /// <summary>判断编辑器或 Player 是否至少有一个 Host/Web 联网运行模式。</summary>
        private static bool HasNetworkMode(SerializedProperty property)
        {
            return HasHostMode(property) || HasWebMode(property);
        }

        /// <summary>判断编辑器或 Player 是否至少有一个 Host 运行模式。</summary>
        private static bool HasHostMode(SerializedProperty property)
        {
            return IsPlayMode(property.FindPropertyRelative(
                       nameof(YooAssetInitializationOptions.EditorPlayMode)),
                       EPlayMode.HostPlayMode)
                || IsPlayMode(property.FindPropertyRelative(
                    nameof(YooAssetInitializationOptions.RuntimePlayMode)),
                    EPlayMode.HostPlayMode);
        }

        /// <summary>判断编辑器或 Player 是否至少有一个 Web 运行模式。</summary>
        private static bool HasWebMode(SerializedProperty property)
        {
            return IsPlayMode(property.FindPropertyRelative(
                       nameof(YooAssetInitializationOptions.EditorPlayMode)),
                       EPlayMode.WebPlayMode)
                || IsPlayMode(property.FindPropertyRelative(
                    nameof(YooAssetInitializationOptions.RuntimePlayMode)),
                    EPlayMode.WebPlayMode);
        }

        /// <summary>比较序列化枚举值与 YooAsset 运行模式。</summary>
        private static bool IsPlayMode(SerializedProperty property, EPlayMode mode)
        {
            return property != null && property.enumValueIndex == (int)mode;
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
            if (!HasNetworkMode(property))
            {
                container.Add(InspectorKitUi.CreateInfoBox(
                    "当前模式无需远端地址",
                    "编辑器和 Player 都是 OfflinePlayMode 或 CustomPlayMode，由本地文件系统或项目初始化回调提供资源。",
                    InspectorInfoBoxType.Info));
                return;
            }

            container.Add(InspectorKitUi.CreateInfoBox(
                    "Host / Web",
                    "Host 模式支持远端更新、弱网回退和下载节流；Web 模式仅支持远端更新。自定义文件系统仍可通过初始化回调接管。",
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
