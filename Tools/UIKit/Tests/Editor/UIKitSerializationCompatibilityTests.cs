using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.Serialization;

namespace YokiFrame.Tests
{
    /// <summary>验证 UIKit 当前同名脚本 GUID、枚举数值、字段别名和包内 GUID 唯一性。</summary>
    public sealed class UIKitSerializationCompatibilityTests
    {
        private const string FIXTURE_FOLDER = "Assets/__YokiFrameUIKitCompatibilityTests__";
        private const string FIXTURE_PATH = FIXTURE_FOLDER + "/LegacyBind.prefab";
        private const string UIKIT_PREFAB_PATH = "Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Resources/UIKit.prefab";
        private const string UIKIT_PREFAB_GUID = "2bbfeae984a55604eb3f4966c2af956b";
        private const string BIND_GUID = "e813e8658b359f74b979c9b90f21244e";

        /// <summary>每条测试后移除临时 Prefab，避免污染用户 Assets。</summary>
        [TearDown]
        public void TearDown()
        {
            UIRoot.Dispose();
            if (AssetDatabase.IsValidFolder(FIXTURE_FOLDER)) AssetDatabase.DeleteAsset(FIXTURE_FOLDER);
        }

        /// <summary>验证当前实现仍存在的同名 Unity 脚本保持旧版 GUID。</summary>
        [Test]
        public void SameNamedUnityScriptsKeepPre20Guids()
        {
            IReadOnlyDictionary<string, string> expected = new Dictionary<string, string>
            {
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Bindings/Bind.cs"] = BIND_GUID,
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Bindings/AbstractBind.cs"] = "8c47c49db00f525468e499f8713c551c",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Root/UIRoot.cs"] = "2fa32fc0b28c5aa4a9b832b94151960d",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Panels/UIPanel.cs"] = "7a7e54ebd60cf7349a699f2fd3ddb22b",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Bindings/UIElement.cs"] = "8fc55626bfd84c84a974c1e555001611",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Bindings/UIComponent.cs"] = "6fa1b4fddf987014fa4d7b51a4e78721",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Layout/UIDynamicElement.cs"] = "74ca0f5971f5b17499292fa5fd1e47e1",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Layout/CanvasBatchHint.cs"] = "750318d057e02734ca59585fe718c648",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Layout/SafeAreaAdapter.cs"] = "acd183f7870ce2844b2ae98556691313",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Focus/SelectableGroup.cs"] = "12e419ed36a25e04884a249cd4856a43",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Focus/UIAutoNavigation.cs"] = "acb2a20e61bca2841978aaf0f16aa4ae",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Focus/UINavigationGrid.cs"] = "fd64005e666fdf14ab3c223236ea2c1d",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Focus/GamepadConfig.cs"] = "86cbc7a83eb90b641b3f9bdc881af307",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Focus/UIBackHandler.cs"] = "f467af05adc13a54eaea375b18fae484",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Focus/UIFocusHighlight.cs"] = "d0b5cde31d7df2e488b9ab24596f1aec",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Focus/UISelectableExtension.cs"] = "93e63e3d46aaf1e4b942610c4017bff3",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Focus/UITabGroup.cs"] = "da9cf7eadb78ab74e942806d95e75b35",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Dialog/UIDialogPanel.cs"] = "18501d0eebea5774392d506745675f8c",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Diagnostics/UIDebugOverlay.cs"] = "5a7247b0604e4874f9b2b4eef756e33b",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Animation/UIAnimationConfig.cs"] = "30ac7733d1da2f24fb8841449d673e56",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Animation/UIAnimationFactory.cs"] = "80f186d6218da514e9c4d432ddc6e7ac",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Animation/CompositeAnimation.cs"] = "0fde82729a70c414e9aebf73d1ddd567",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Focus/IGamepadInput.cs"] = "0817db6f8827296419bc437086924e12",
                ["Assets/YokiFrame/Tools/UIKit/Adapters/Unity/Runtime/Focus/GamepadNavigator.cs"] = "ed6340dcc6a4a3f40a9199b186dee95b",
            };

            foreach (KeyValuePair<string, string> item in expected)
            {
                Assert.AreEqual(item.Value, AssetDatabase.AssetPathToGUID(item.Key), item.Key);
                Assert.AreEqual(item.Key, AssetDatabase.GUIDToAssetPath(item.Value), item.Value);
            }
        }

        /// <summary>验证包内 `.meta` 的主 GUID 全部唯一，防止迁移旧 meta 时覆盖当前资产。</summary>
        [Test]
        public void PackageMetaGuidsRemainUnique()
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            var owners = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string metaPath in Directory.EnumerateFiles(packageRoot, "*.meta", SearchOption.AllDirectories))
            {
                Match match = Regex.Match(File.ReadAllText(metaPath), "^guid:\\s*([0-9a-f]{32})$", RegexOptions.Multiline);
                if (!match.Success) continue;
                string guid = match.Groups[1].Value;
                Assert.IsFalse(
                    owners.TryGetValue(guid, out string existing),
                    "重复 GUID " + guid + ": " + existing + " / " + metaPath);
                owners.Add(guid, metaPath);
            }
        }

        /// <summary>验证 UIKit 根 Prefab 保留资产 GUID、旧版层级和当前 UIRoot 脚本。</summary>
        [Test]
        public void UIKitRootPrefabKeepsLegacyHierarchyAndAssetGuid()
        {
            Assert.AreEqual(UIKIT_PREFAB_GUID, AssetDatabase.AssetPathToGUID(UIKIT_PREFAB_PATH));
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(UIKIT_PREFAB_PATH);
            Assert.IsNotNull(prefab, "UIKit 根 Prefab 未导入。");
            Assert.IsNull(prefab.GetComponent<UIRoot>(), "UIRoot 必须继续位于旧版同名子节点。");

            GameObject contents = PrefabUtility.LoadPrefabContents(UIKIT_PREFAB_PATH);
            try
            {
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(contents));
                Transform uiRoot = contents.transform.Find("UIRoot");
                Transform eventSystem = contents.transform.Find("EventSystem");
                Transform uiCamera = contents.transform.Find("UICamera");
                Assert.AreEqual(3, contents.transform.childCount, "UIKit Prefab 必须保持旧版三个直属子节点。");
                Assert.IsNotNull(uiRoot, "UIKit Prefab 缺少旧版 UIRoot 子节点。");
                Assert.IsNotNull(eventSystem, "UIKit Prefab 缺少旧版 EventSystem 子节点。");
                Assert.IsNotNull(uiCamera, "UIKit Prefab 缺少旧版 UICamera 子节点。");
                UIRoot root = uiRoot.GetComponent<UIRoot>();
                Canvas canvas = uiRoot.GetComponent<Canvas>();
                Assert.IsNotNull(root);
                Assert.IsNotNull(canvas);
                Assert.IsNotNull(uiRoot.GetComponent<CanvasScaler>());
                Assert.IsNotNull(uiRoot.GetComponent<GraphicRaycaster>());
                Assert.AreEqual(RenderMode.ScreenSpaceOverlay, canvas.renderMode);
                Assert.IsNull(canvas.worldCamera);
                Assert.IsNotNull(eventSystem.GetComponent<EventSystem>());
                Assert.IsFalse(eventSystem.gameObject.activeSelf, "内置 EventSystem 默认禁用，避免和场景输入系统重复。");
                Assert.IsNotNull(uiCamera.GetComponent<Camera>());
                Assert.IsFalse(uiCamera.gameObject.activeSelf, "UICamera 必须保持默认禁用。");
                Assert.IsFalse(
                    uiCamera.GetComponents<Component>().Any(static component =>
                        component != default && string.Equals(
                            component.GetType().FullName,
                            "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData",
                            StringComparison.Ordinal)),
                    "UIKit 默认模板不得携带 URP 专属组件。");
                SerializedObject rootSettings = new(root);
                Assert.AreEqual("Art/UIPrefab", rootSettings.FindProperty("mPrefabPathPrefix").stringValue);
                Assert.IsFalse(rootSettings.FindProperty("mUseAddressableLocation").boolValue);
                Assert.AreEqual(8, rootSettings.FindProperty("mReusableCacheCapacity").intValue);
                Assert.IsNull(contents.transform.Find("Canvas"), "Canvas 不应继续作为 UIKit 直属子节点。");
                Assert.IsNull(contents.transform.Find("Storage"), "Storage 只允许在运行时创建。");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>验证当前 UIRoot 可直接初始化旧版 Prefab 内容并按需启用内置输入节点。</summary>
        [Test]
        public void LegacyRootPrefabInitializesCurrentRuntimeShape()
        {
            GameObject contents = PrefabUtility.LoadPrefabContents(UIKIT_PREFAB_PATH);
            try
            {
                UIRoot root = contents.transform.Find("UIRoot").GetComponent<UIRoot>();
                if (root.Canvas == default) root.OnSingletonInit();

                Assert.AreSame(root.GetComponent<Canvas>(), root.Canvas);
                Transform storage = root.transform.Find("Storage");
                Assert.IsNotNull(storage);
                Assert.IsFalse(storage.gameObject.activeSelf);
                EventSystem eventSystem = root.EnsureEventSystem();
                Assert.AreSame(contents.transform.Find("EventSystem"), eventSystem.transform);
                Assert.IsTrue(eventSystem.gameObject.activeSelf);
                Assert.IsNotNull(eventSystem.GetComponent<BaseInputModule>());
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>验证项目显式传入的 Root Prefab 会在首次使用前成为实例化来源。</summary>
        [Test]
        public void ExplicitRootPrefabIsUsedBeforeFirstRootCreation()
        {
            EnsureFixtureFolder();
            const string prefabPath = FIXTURE_FOLDER + "/CustomUIKit.prefab";
            GameObject source = new("ProjectUIKitRoot");
            GameObject child = new("UIRoot", typeof(RectTransform));
            child.transform.SetParent(source.transform, false);
            child.AddComponent<UIRoot>();
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(source, prefabPath);
            UnityEngine.Object.DestroyImmediate(source);
            Assert.IsNotNull(prefab);

            UIKit.SetRootPrefab(prefab);
            UIRoot root = UIRoot.Instance;

            Assert.IsNotNull(root);
            Assert.AreEqual("UIRoot", root.name);
            Assert.IsNotNull(root.transform.parent);
            Assert.AreEqual(prefab.name, root.transform.parent.name);
            Assert.Throws<InvalidOperationException>(() => UIKit.SetRootPrefab(prefab));

            UIRoot.Dispose();
            UIRoot recreated = UIRoot.Instance;
            Assert.IsNotNull(recreated.transform.parent);
            Assert.AreEqual(prefab.name, recreated.transform.parent.name);
        }

        /// <summary>验证未显式传入时仍从包内 Resources/UIKit 模板创建 Root。</summary>
        [Test]
        public void DefaultRootUsesPackagePrefabWhenNoOverrideIsConfigured()
        {
            GameObject prefab = Resources.Load<GameObject>("UIKit");
            Assert.IsNotNull(prefab, "UIKit 根 Prefab 未进入 Resources。");

            UIRoot root = UIRoot.Instance;

            Assert.IsNotNull(root);
            Assert.AreEqual("UIRoot", root.name);
            Assert.IsNotNull(root.transform.parent);
            Assert.AreEqual(prefab.name, root.transform.parent.name);
        }

        /// <summary>验证程序化兜底与默认 Prefab 使用相同的 UIKit/UIRoot 层级和 owner 清理语义。</summary>
        [Test]
        public void ProceduralFallbackUsesUIKitRootHierarchy()
        {
            var attribute = Attribute.GetCustomAttribute(
                typeof(UIRoot),
                typeof(MonoSingletonPathAttribute)) as MonoSingletonPathAttribute;
            Assert.IsNotNull(attribute);
            Assert.AreEqual("UIKit/UIRoot", attribute.PathInHierarchy);

            UIRoot root = UIRoot.CreateProceduralFallback();
            Assert.IsNotNull(root);
            Assert.AreEqual("UIRoot", root.name);
            Transform owner = root.transform.parent;
            Assert.IsNotNull(owner);
            Assert.AreEqual("UIKit", owner.name);
            Assert.IsNull(owner.parent);
            Assert.AreSame(root.transform, owner.Find("UIRoot"));

            UIRoot.Dispose();
            Assert.IsTrue(owner == default, "释放 UIRoot 时应同步清理程序化 UIKit owner。");
        }

        /// <summary>验证旧 Prefab 中保存的 BindType 整数仍映射到相同语义。</summary>
        [Test]
        public void BindTypeValuesRemainStable()
        {
            Assert.AreEqual(0, (int)BindType.Member);
            Assert.AreEqual(1, (int)BindType.Element);
            Assert.AreEqual(2, (int)BindType.Component);
            Assert.AreEqual(3, (int)BindType.Leaf);
        }

        /// <summary>验证旧字段别名全部保留，字段重命名不会丢失 Prefab 数据。</summary>
        [Test]
        public void AbstractBindKeepsLegacyFieldAliases()
        {
            AssertFormerName(nameof(AbstractBind.Bind), "bind");
            AssertFormerName(nameof(AbstractBind.Name), "mName");
            AssertFormerName(nameof(AbstractBind.AutoType), "autoType");
            AssertFormerName(nameof(AbstractBind.CustomType), "customType");
            AssertFormerName(nameof(AbstractBind.Type), "type");
            AssertFormerName(nameof(AbstractBind.Comment), "comment");
        }

        /// <summary>由 Unity 创建旧 GUID Bind Prefab，并验证无 Missing Script 且字段可回读。</summary>
        [Test]
        public void BindPrefabRoundTripUsesLegacyGuidWithoutMissingScript()
        {
            EnsureFixtureFolder();
            GameObject root = new("LegacyBind", typeof(RectTransform), typeof(Bind));
            try
            {
                Bind bind = root.GetComponent<Bind>();
                bind.Bind = BindType.Member;
                bind.Name = "ConfirmButton";
                bind.Type = typeof(RectTransform).FullName;
                bind.Target = root.GetComponent<RectTransform>();
                PrefabUtility.SaveAsPrefabAsset(root, FIXTURE_PATH);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            string yaml = File.ReadAllText(ToAbsolutePath(FIXTURE_PATH));
            StringAssert.Contains("guid: " + BIND_GUID, yaml);
            GameObject contents = PrefabUtility.LoadPrefabContents(FIXTURE_PATH);
            try
            {
                Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(contents));
                Bind restored = contents.GetComponent<Bind>();
                Assert.IsNotNull(restored);
                Assert.AreEqual("ConfirmButton", restored.Name);
                Assert.AreEqual(typeof(RectTransform).FullName, restored.Type);
                Assert.AreSame(restored.GetComponent<RectTransform>(), restored.Target);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>验证一个 Bind 的多个 Member 目标和独立字段名可经过 Prefab 序列化完整回读。</summary>
        [Test]
        public void BindPrefabRoundTripPreservesMultipleMemberTargets()
        {
            EnsureFixtureFolder();
            GameObject root = new("MultiMemberBind", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Bind));
            try
            {
                Bind bind = root.GetComponent<Bind>();
                bind.Name = "ConfirmButton";
                bind.Target = root.GetComponent<Button>();
                bind.MemberTargets.Add(new BindMemberTarget
                {
                    Target = root.GetComponent<Button>(),
                    Name = "ConfirmButton",
                });
                bind.MemberTargets.Add(new BindMemberTarget
                {
                    Target = root.GetComponent<Image>(),
                    Name = "ConfirmButtonImage",
                });
                PrefabUtility.SaveAsPrefabAsset(root, FIXTURE_PATH);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(FIXTURE_PATH);
            try
            {
                Bind restored = contents.GetComponent<Bind>();
                Assert.AreEqual(2, restored.MemberTargets.Count);
                Assert.AreEqual("ConfirmButton", restored.MemberTargets[0].Name);
                Assert.AreSame(restored.GetComponent<Button>(), restored.MemberTargets[0].Target);
                Assert.AreEqual("ConfirmButtonImage", restored.MemberTargets[1].Name);
                Assert.AreSame(restored.GetComponent<Image>(), restored.MemberTargets[1].Target);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>验证 UIPanel 的多态动画配置经过 Prefab 序列化后仍可完整回读。</summary>
        [Test]
        public void PanelAnimationConfigRoundTripPreservesManagedReference()
        {
            EnsureFixtureFolder();
            GameObject root = new("ConfiguredPanel", typeof(RectTransform), typeof(UIKitNavigationFirstTestPanel));
            try
            {
                var serializedPanel = new SerializedObject(root.GetComponent<UIKitNavigationFirstTestPanel>());
                SerializedProperty configProperty = serializedPanel.FindProperty("mShowAnimationConfig");
                Assert.IsNotNull(configProperty);
                configProperty.managedReferenceValue = new FadeAnimationConfig
                {
                    Duration = 0.45f,
                    FromAlpha = 0.2f,
                    ToAlpha = 0.8f
                };
                serializedPanel.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, FIXTURE_PATH);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }

            GameObject contents = PrefabUtility.LoadPrefabContents(FIXTURE_PATH);
            try
            {
                var serializedPanel = new SerializedObject(contents.GetComponent<UIKitNavigationFirstTestPanel>());
                var restored = serializedPanel.FindProperty("mShowAnimationConfig").managedReferenceValue as FadeAnimationConfig;
                Assert.IsNotNull(restored);
                Assert.AreEqual(0.45f, restored.Duration);
                Assert.AreEqual(0.2f, restored.FromAlpha);
                Assert.AreEqual(0.8f, restored.ToAlpha);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }

        /// <summary>断言指定字段包含预期 FormerlySerializedAs 名称。</summary>
        private static void AssertFormerName(string fieldName, string oldName)
        {
            FieldInfo field = typeof(AbstractBind).GetField(fieldName, BindingFlags.Instance | BindingFlags.Public);
            Assert.IsNotNull(field, fieldName);
            string[] names = field.GetCustomAttributes<FormerlySerializedAsAttribute>()
                .Select(static attribute => attribute.oldName)
                .ToArray();
            CollectionAssert.Contains(names, oldName);
        }

        /// <summary>创建测试 Prefab 使用的临时 Assets 目录。</summary>
        private static void EnsureFixtureFolder()
        {
            if (!AssetDatabase.IsValidFolder(FIXTURE_FOLDER))
                AssetDatabase.CreateFolder("Assets", "__YokiFrameUIKitCompatibilityTests__");
        }

        /// <summary>把 Assets 相对路径转换为当前 Unity 项目的绝对路径。</summary>
        private static string ToAbsolutePath(string assetPath)
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, assetPath);
        }
    }
}
