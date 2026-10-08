#if UNITY_EDITOR && UNITY_INCLUDE_TESTS && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using YooAsset.Editor;
using YooAsset;

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

        /// <summary>验证默认视觉树组合通用卡片、只读字符串列表、开关和折叠，且不使用 Toggle。</summary>
        [Test]
        public void CreatePropertyGUI_默认选项_组合卡片只读列表开关和折叠()
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

        /// <summary>验证编辑器和 Player 都是本地模式时隐藏联网策略与启动下载参数。</summary>
        [Test]
        public void CreatePropertyGUI_编辑器与Player都本地_隐藏联网设置()
        {
            mHolder.Options.EditorPlayMode = EPlayMode.EditorSimulateMode;
            mHolder.Options.RuntimePlayMode = EPlayMode.OfflinePlayMode;

            VisualElement root = CreateOptionsRoot();

            Assert.That(HasExactLabel(root, "联网初始化策略"), Is.False);
            Assert.That(HasExactLabel(root, "启动下载并发数"), Is.False);
            Assert.That(HasExactLabel(root, "版本请求附加时间戳"), Is.False);
        }

        /// <summary>验证编辑器进入 Host 模式时，即使 Player 保持离线也显示联网配置和下载并发。</summary>
        [Test]
        public void CreatePropertyGUI_编辑器Host且Player离线_显示联网和下载并发()
        {
            mHolder.Options.EditorPlayMode = EPlayMode.HostPlayMode;
            mHolder.Options.RuntimePlayMode = EPlayMode.OfflinePlayMode;

            VisualElement root = CreateOptionsRoot();

            Assert.That(HasExactLabel(root, "联网初始化策略"), Is.True);
            Assert.That(HasExactLabel(root, "启动下载并发数"), Is.True);
        }

        /// <summary>验证 Player 进入 Web 模式时显示联网配置，但不显示 Host 启动下载并发。</summary>
        [Test]
        public void CreatePropertyGUI_Player为Web且编辑器模拟_显示联网但隐藏下载并发()
        {
            mHolder.Options.EditorPlayMode = EPlayMode.EditorSimulateMode;
            mHolder.Options.RuntimePlayMode = EPlayMode.WebPlayMode;

            VisualElement root = CreateOptionsRoot();

            Assert.That(HasExactLabel(root, "联网初始化策略"), Is.True);
            Assert.That(HasExactLabel(root, "清单超时秒数"), Is.True);
            Assert.That(HasExactLabel(root, "版本请求附加时间戳"), Is.True);
            Assert.That(HasExactLabel(root, "启动下载并发数"), Is.False);
        }

        /// <summary>验证四种联网策略各自显示对应的说明标题。</summary>
        [Test]
        public void CreatePropertyGUI_四种联网策略_显示对应说明()
        {
            mHolder.Options.EditorPlayMode = EPlayMode.HostPlayMode;
            YooAssetInitializationStrategy[] strategies =
            {
                YooAssetInitializationStrategy.ManifestOnly,
                YooAssetInitializationStrategy.RemoteOnly,
                YooAssetInitializationStrategy.RemoteThenCached,
                YooAssetInitializationStrategy.RemoteThenOffline
            };
            string[] titles =
            {
                "Manifest Only（仅清单）",
                "Remote Only（仅远端）",
                "Remote Then Cached（远端后缓存）",
                "Remote Then Offline（远端后离线）"
            };

            for (int index = 0; index < strategies.Length; index++)
            {
                mHolder.Options.InitializationStrategy = strategies[index];
                VisualElement root = CreateOptionsRoot();
                Assert.That(HasExactLabel(root, titles[index]), Is.True);
            }
        }

        /// <summary>验证创建视觉树时把手写包名替换为 YooAsset 收集器中的包名。</summary>
        [Test]
        public void CreatePropertyGUI_已有手动包名_同步为收集器包名()
        {
            mHolder.Options.PackageNames = new List<string> { "ManualPackage" };
            SerializedObject serializedObject = new(mHolder);
            SerializedProperty property = serializedObject.FindProperty(
                nameof(YooAssetInitializationOptionsHolder.Options));

            new YooAssetInitializationOptionsDrawer().CreatePropertyGUI(property);

            CollectionAssert.AreEqual(GetExpectedCollectorPackages(), mHolder.Options.PackageNames);
        }

        /// <summary>验证扫描到成对实现后显示构建加密、运行时解密和构建按钮。</summary>
        [Test]
        public void CreatePropertyGUI_已有成对实现_显示加解密类型和构建按钮()
        {
            mHolder.Options.EncryptionMode = YooAssetEncryptionMode.XorStream;
            SerializedObject serializedObject = new(mHolder);
            SerializedProperty property = serializedObject.FindProperty(
                nameof(YooAssetInitializationOptionsHolder.Options));

            VisualElement root = new YooAssetInitializationOptionsDrawer().CreatePropertyGUI(property);

            Assert.That(HasExactLabel(root, "构建加密实现"), Is.True);
            Assert.That(HasExactLabel(root, "运行时解密实现"), Is.True);
            Assert.That(HasExactLabel(root, "加密服务"), Is.False);
            Assert.That(HasExactButton(root, "按当前方案构建"), Is.True);
        }

        /// <summary>验证空集或只有加密侧时不收录，且内置 XOR 以完整对出现。</summary>
        [Test]
        public void CollectPairs_缺侧或内置XOR_只保留完整对()
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

        /// <summary>按完整文本精确匹配用户可见标签，不使用子串包含。</summary>
        /// <param name="root">待检查的视觉树。</param>
        /// <param name="text">期望的完整标签文本。</param>
        /// <returns>存在完全相同的标签时返回 true。</returns>
        private static bool HasExactLabel(VisualElement root, string text)
        {
            List<Label> labels = root.Query<Label>().ToList();
            for (int index = 0; index < labels.Count; index++)
            {
                if (labels[index].text == text)
                    return true;
            }

            return false;
        }

        /// <summary>按完整文本精确匹配用户可见按钮，不使用子串包含。</summary>
        /// <param name="root">待检查的视觉树。</param>
        /// <param name="text">期望的完整按钮文本。</param>
        /// <returns>存在完全相同的按钮时返回 true。</returns>
        private static bool HasExactButton(VisualElement root, string text)
        {
            List<Button> buttons = root.Query<Button>().ToList();
            for (int index = 0; index < buttons.Count; index++)
            {
                if (buttons[index].text == text)
                    return true;
            }

            return false;
        }

        /// <summary>创建当前测试宿主的 YooAsset 选项视觉树。</summary>
        private VisualElement CreateOptionsRoot()
        {
            SerializedObject serializedObject = new(mHolder);
            SerializedProperty property = serializedObject.FindProperty(
                nameof(YooAssetInitializationOptionsHolder.Options));
            return new YooAssetInitializationOptionsDrawer().CreatePropertyGUI(property);
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
