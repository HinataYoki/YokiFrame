#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;

namespace YokiFrame.Unity.Tests
{
    /// <summary>验证 YooAsset Drawer 通过 InspectorKit 通用组件组合视觉树。</summary>
    public sealed class YooAssetInitializationOptionsDrawerTests
    {
        private YooAssetInitializationOptionsHolder mHolder;

        /// <summary>为每个用例创建独立可序列化宿主。</summary>
        [SetUp]
        public void SetUp()
        {
            mHolder = ScriptableObject.CreateInstance<YooAssetInitializationOptionsHolder>();
        }

        /// <summary>销毁测试宿主，避免把隐藏 ScriptableObject 留在 Editor 内存中。</summary>
        [TearDown]
        public void TearDown()
        {
            if (mHolder != null)
                Object.DestroyImmediate(mHolder);
        }

        /// <summary>Drawer 应包含四张通用卡片、滑块开关、嵌套折叠和字符串列表。</summary>
        [Test]
        public void DrawerUsesInspectorKitCardsAndStringList()
        {
            SerializedObject serializedObject = new(mHolder);
            SerializedProperty property = serializedObject.FindProperty(
                nameof(YooAssetInitializationOptionsHolder.Options));
            YooAssetInitializationOptionsDrawer drawer = new();

            VisualElement root = drawer.CreatePropertyGUI(property);

            Assert.That(
                root.Query<VisualElement>(className: "yoki-editor-inspector__card").ToList().Count,
                Is.EqualTo(YooAssetEncryptionImplementationCatalog.GetAvailablePairs().Count > 0 ? 4 : 3));
            Assert.That(
                root.Q<VisualElement>(className: "yoki-editor-inspector__list"),
                Is.Not.Null);
            Assert.That(
                root.Q<TextField>(className: "yoki-editor-inspector__list-field").isReadOnly,
                Is.True);
            Assert.That(
                root.Q<Button>(className: "yoki-editor-inspector__list-add"),
                Is.Null);
            Assert.That(
                root.Q<Button>(className: "yoki-editor-inspector__list-remove"),
                Is.Null);
            Assert.That(
                root.Q<VisualElement>(className: "yoki-editor-inspector__switch"),
                Is.Not.Null);
            Assert.That(
                root.Q<VisualElement>(className: "yoki-editor-inspector__foldout"),
                Is.Not.Null);
            Assert.That(root.Query<Toggle>().ToList(), Is.Empty);
        }

        /// <summary>Drawer 创建时应以 YooAsset 收集器为唯一 package 数据源。</summary>
        [Test]
        public void DrawerSynchronizesPackagesFromCollector()
        {
            mHolder.Options.PackageNames = new List<string> { "ManualPackage" };
            SerializedObject serializedObject = new(mHolder);
            SerializedProperty property = serializedObject.FindProperty(
                nameof(YooAssetInitializationOptionsHolder.Options));

            new YooAssetInitializationOptionsDrawer().CreatePropertyGUI(property);

            CollectionAssert.AreEqual(GetExpectedCollectorPackages(), mHolder.Options.PackageNames);
        }

        /// <summary>打包卡片只在扫描到成对实现后显示构建加密和运行时解密类型。</summary>
        [Test]
        public void DrawerShowsOnlyScannedEncryptionImplementationPairs()
        {
            mHolder.Options.EncryptionMode = YooAssetEncryptionMode.XorStream;
            SerializedObject serializedObject = new(mHolder);
            SerializedProperty property = serializedObject.FindProperty(
                nameof(YooAssetInitializationOptionsHolder.Options));

            VisualElement root = new YooAssetInitializationOptionsDrawer().CreatePropertyGUI(property);

            Assert.That(ContainsLabel(root, "构建加密实现"), Is.True);
            Assert.That(ContainsLabel(root, "运行时解密实现"), Is.True);
            Assert.That(ContainsLabel(root, "加密服务"), Is.False);
            Assert.That(ContainsButton(root, "按当前方案构建"), Is.True);
        }

        /// <summary>扫描目录只接受同一方案中同时存在的构建加密和运行时解密实现。</summary>
        [Test]
        public void EncryptionCatalogRequiresCompleteImplementationPair()
        {
            List<YooAssetEncryptionImplementationPair> empty =
                YooAssetEncryptionImplementationCatalog.CollectPairs(new System.Type[0]);
            Assert.That(empty, Is.Empty);

#if YOKIFRAME_YOOASSET_3
            System.Type encryptionType = typeof(YooAssetXorBundleEncryptor);
            System.Type decryptionType = typeof(YooAssetXorStreamDecryptor);
#else
            System.Type encryptionType = typeof(YooAssetXorStreamEncryptionService);
            System.Type decryptionType = typeof(YooAssetXorStreamDecryptionService);
#endif
            List<YooAssetEncryptionImplementationPair> incomplete =
                YooAssetEncryptionImplementationCatalog.CollectPairs(new[] { encryptionType });
            Assert.That(incomplete, Is.Empty);

            List<YooAssetEncryptionImplementationPair> pairs =
                YooAssetEncryptionImplementationCatalog.GetAvailablePairs();
            YooAssetEncryptionImplementationPair xorPair = GetPair(
                pairs,
                YooAssetEncryptionMode.XorStream);

            Assert.That(xorPair.EncryptionType, Is.EqualTo(encryptionType));
            Assert.That(xorPair.DecryptionType, Is.EqualTo(decryptionType));
        }

        /// <summary>按当前 YooAsset 主版本读取测试期望的收集器 package 名称。</summary>
        private static List<string> GetExpectedCollectorPackages()
        {
            List<string> names = new();
#if YOKIFRAME_YOOASSET_3
            foreach (var package in BundleCollectorSettingData.Setting.Packages)
#else
            foreach (var package in AssetBundleCollectorSettingData.Setting.Packages)
#endif
            {
                if (!string.IsNullOrEmpty(package.PackageName) && !names.Contains(package.PackageName))
                    names.Add(package.PackageName);
            }

            if (names.Count == 0)
                names.Add(YooAssetInitializationOptions.DEFAULT_PACKAGE_NAME);
            return names;
        }

        /// <summary>检查视觉树是否包含指定文本的标签。</summary>
        private static bool ContainsLabel(VisualElement root, string text)
        {
            List<Label> labels = root.Query<Label>().ToList();
            for (int index = 0; index < labels.Count; index++)
            {
                if (labels[index].text == text)
                    return true;
            }

            return false;
        }

        /// <summary>检查视觉树是否包含指定文本的按钮。</summary>
        private static bool ContainsButton(VisualElement root, string text)
        {
            List<Button> buttons = root.Query<Button>().ToList();
            for (int index = 0; index < buttons.Count; index++)
            {
                if (buttons[index].text == text)
                    return true;
            }

            return false;
        }

        /// <summary>从扫描结果中获取指定方案的实现对，缺失时使测试明确失败。</summary>
        private static YooAssetEncryptionImplementationPair GetPair(
            List<YooAssetEncryptionImplementationPair> pairs,
            YooAssetEncryptionMode mode)
        {
            for (int index = 0; index < pairs.Count; index++)
            {
                if (pairs[index].Mode == mode)
                    return pairs[index];
            }

            Assert.Fail("未扫描到 " + mode + " 的完整加密解密实现对。");
            return default;
        }

        /// <summary>提供 SerializedObject 可识别的 YooAsset 初始化参数字段。</summary>
        private sealed class YooAssetInitializationOptionsHolder : ScriptableObject
        {
            public YooAssetInitializationOptions Options = new();
        }
    }
}
#endif
