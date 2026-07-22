#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;
using YokiFrame.Unity.Inspector;

namespace YokiFrame
{
    /// <summary>
    /// 使用 InspectorKit 展示 UIRoot 的层级约束、面板加载与缓存配置。
    /// </summary>
    [CustomEditor(typeof(UIRoot))]
    [CanEditMultipleObjects]
    internal sealed class UIRootInspector : UnityEditor.Editor
    {
        private const string OVERVIEW_CARD_KEY = "UIKit.Root.Overview";
        private const string LOADING_CARD_KEY = "UIKit.Root.Loading";
        private const string CACHE_CARD_KEY = "UIKit.Root.Cache";

        private SerializedProperty mPrefabPathPrefix;
        private SerializedProperty mUseAddressableLocation;
        private SerializedProperty mReusableCacheCapacity;

        /// <summary>缓存 UIRoot 的序列化配置，供 UI Toolkit 字段稳定绑定。</summary>
        private void OnEnable()
        {
            mPrefabPathPrefix = serializedObject.FindProperty("mPrefabPathPrefix");
            mUseAddressableLocation = serializedObject.FindProperty("mUseAddressableLocation");
            mReusableCacheCapacity = serializedObject.FindProperty("mReusableCacheCapacity");
        }

        /// <summary>创建由 InspectorKit 卡片、说明和序列化字段组成的 Root Inspector。</summary>
        public override VisualElement CreateInspectorGUI()
        {
            VisualElement root = InspectorKitUi.CreateRoot();
            VisualElement panel = InspectorKitUi.CreatePanel("UIKit Root");
            panel.Add(InspectorKitUi.CreateInfoBox(
                "唯一运行时根",
                "UIRoot 承载 Canvas、EventSystem 接入和面板生命周期。项目定制请使用默认模板的 Prefab Variant。",
                InspectorInfoBoxType.Info));
            panel.Add(CreateOverviewCard());
            panel.Add(CreateLoadingCard());
            panel.Add(CreateCacheCard());
            root.Add(panel);
            return root;
        }

        /// <summary>展示默认模板与程序化兜底共享的稳定层级。</summary>
        private static VisualElement CreateOverviewCard()
        {
            return InspectorKitUi.CreateCard(
                "Root 概览",
                OVERVIEW_CARD_KEY,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    body.Add(InspectorKitUi.CreateReadOnlyStringRow("运行时层级", "UIKit/UIRoot"));
                    body.Add(InspectorKitUi.CreateReadOnlyStringRow("默认模板", "Resources/UIKit.prefab"));
                });
        }

        /// <summary>绘制默认 ResKit Panel loader 的路径与 Addressable location 配置。</summary>
        private VisualElement CreateLoadingCard()
        {
            return InspectorKitUi.CreateCard(
                "面板加载",
                LOADING_CARD_KEY,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    body.Add(InspectorKitUi.CreateStringRow(mPrefabPathPrefix, "预制体路径前缀"));
                    body.Add(InspectorKitUi.CreateSwitchRow(
                        mUseAddressableLocation,
                        "使用 Addressable Location"));
                    body.Add(InspectorKitUi.CreateInfoBox(
                        "启用后默认 loader 直接使用 Panel 类型名作为资源 location。",
                        InspectorInfoBoxType.Info));
                });
        }

        /// <summary>绘制 Reusable 面板关闭后的有界 LRU 容量。</summary>
        private VisualElement CreateCacheCard()
        {
            return InspectorKitUi.CreateCard(
                "缓存策略",
                CACHE_CARD_KEY,
                InspectorCardInitialState.Expanded,
                body =>
                {
                    body.Add(InspectorKitUi.CreateIntegerRow(
                        mReusableCacheCapacity,
                        "Reusable 缓存容量"));
                    body.Add(InspectorKitUi.CreateInfoBox(
                        "容量为 0 时，关闭后的 Reusable 面板会立即释放。",
                        InspectorInfoBoxType.Info));
                });
        }
    }
}
#endif
