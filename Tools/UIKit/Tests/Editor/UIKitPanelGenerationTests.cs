using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace YokiFrame.Tests
{
    /// <summary>验证 UIKit Panel 源码生成、文件集事务和编译后 Prefab 回填。</summary>
    public sealed partial class UIKitPanelGenerationTests
    {
        private const string TEST_ROOT = "Assets/__YokiFrameUIKitGenerationTests__";

        /// <summary>每条测试后移除临时代码、目录和 Prefab。</summary>
        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TEST_ROOT)) AssetDatabase.DeleteAsset(TEST_ROOT);
            string absoluteRoot = UIKitPanelCodeLayout.ToAbsolutePath(TEST_ROOT);
            if (Directory.Exists(absoluteRoot)) Directory.Delete(absoluteRoot, true);
            AssetDatabase.Refresh();
        }

        /// <summary>验证用户 partial 不覆盖，Designer 重复生成保持内容和时间戳稳定。</summary>
        [Test]
        public void GenerationPreservesUserPartialAndIsIdempotent()
        {
            UIKitPanelCodeLayout layout = CreateLayout("GeneratedPanel");
            UIKitBindScanResult scan = new("GeneratedPanel");
            Dictionary<string, string> firstSources = UIKitPanelCodeGenerator.BuildSources(layout, scan);
            string designerSource = firstSources[layout.PanelDesignerPath];
            StringAssert.Contains("public GeneratedPanelData Data => mData;", designerSource);
            StringAssert.DoesNotContain("public new GeneratedPanelData Data", designerSource);
            Assert.IsTrue(UIKitPanelCodeGenerator.CommitSources(firstSources));
            string designerPath = UIKitPanelCodeLayout.ToAbsolutePath(layout.PanelDesignerPath);
            long firstTimestamp = File.GetLastWriteTimeUtc(designerPath).Ticks;
            string userPath = UIKitPanelCodeLayout.ToAbsolutePath(layout.PanelScriptPath);
            File.WriteAllText(userPath, "// user-owned\n");

            Dictionary<string, string> secondSources = UIKitPanelCodeGenerator.BuildSources(layout, scan);
            Assert.IsFalse(secondSources.ContainsKey(layout.PanelScriptPath));
            Assert.IsFalse(UIKitPanelCodeGenerator.CommitSources(secondSources));
            Assert.AreEqual("// user-owned\n", File.ReadAllText(userPath));
            Assert.AreEqual(firstTimestamp, File.GetLastWriteTimeUtc(designerPath).Ticks);
        }

        /// <summary>验证 Inspector 生成默认值会读取当前项目保存的 UIKit 命名空间。</summary>
        [Test]
        public void DefaultGenerationRequestUsesSavedProjectNamespace()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string settingsPath = Path.Combine(
                projectRoot,
                "ProjectSettings",
                "Packages",
                "com.hinatayoki.yokiframe",
                "editor-settings.json");
            if (!File.Exists(settingsPath)) Assert.Ignore("当前项目没有统一 Editor Settings 文件。");

            EditorSettingsDocument document = JsonUtility.FromJson<EditorSettingsDocument>(
                File.ReadAllText(settingsPath));
            string expectedNamespace = string.Empty;
            EditorSettingsEntry[] entries = document == null ? null : document.settings;
            if (entries != null)
            {
                for (var index = 0; index < entries.Length; index++)
                {
                    EditorSettingsEntry entry = entries[index];
                    if (entry != null
                        && entry.kit == "UIKit"
                        && entry.key == "editor.scriptNamespace")
                    {
                        expectedNamespace = entry.value;
                        break;
                    }
                }
            }

            if (string.IsNullOrWhiteSpace(expectedNamespace))
                Assert.Ignore("当前项目没有保存 UIKit 命名空间覆盖值。");
            UIKitPanelGenerationRequest request = UIKitPanelGenerationRequest.CreateDefault("SavedSettingsPanel");
            Assert.AreEqual(expectedNamespace, request.scriptNamespace);
        }

        /// <summary>验证已有用户 Panel 与新配置不一致时拒绝拆分 partial 命名空间。</summary>
        [Test]
        public void GenerationRejectsNamespaceMigrationOfExistingUserPanel()
        {
            UIKitPanelCodeLayout layout = CreateLayout(
                nameof(UIKitMultiMemberTestPanel),
                "Project.Renamed.UI",
                "Assembly-CSharp");
            GameObject root = new(
                nameof(UIKitMultiMemberTestPanel),
                typeof(RectTransform),
                typeof(UIKitMultiMemberTestPanel));
            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => UIKitPanelPrefabService.GenerateForPrefab(layout, root));
                StringAssert.Contains("不会自动迁移或覆盖用户 partial", exception.Message);
                StringAssert.Contains("Project.Renamed.UI", exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证 Panel 用户脚本和 Designer 都导入 Unity UI 与 YokiFrame 常用命名空间。</summary>
        [Test]
        public void GeneratedPanelSourcesIncludeRequiredNamespaces()
        {
            UIKitPanelCodeLayout layout = CreateLayout("NamespacePanel");
            UIKitBindScanResult scan = new("NamespacePanel");
            Dictionary<string, string> sources = UIKitPanelCodeGenerator.BuildSources(layout, scan);

            Assert.AreEqual(2, sources.Count);
            Assert.That(sources[layout.PanelScriptPath], Does.StartWith(
                "using UnityEngine;\nusing UnityEngine.UI;\nusing YokiFrame;\n\n"));
            Assert.That(sources[layout.PanelDesignerPath], Does.StartWith(
                "//------------------------------------------------------------------------------\n"
                + "// <auto-generated>\n"
                + "//     This code was generated by YokiFrame UIKit.\n"
                + "// </auto-generated>\n"
                + "//------------------------------------------------------------------------------\n\n"
                + "using UnityEngine;\nusing UnityEngine.UI;\nusing YokiFrame;\n\n"));
            foreach (string source in sources.Values)
                Assert.That(source, Does.Not.Contain("global::YokiFrame."));
        }

        /// <summary>验证后续文件提交失败时，先前更新和新建文件都恢复原状态。</summary>
        [Test]
        public void CommitFailureRollsBackEarlierFiles()
        {
            string firstAssetPath = TEST_ROOT + "/A.cs";
            string blockedAssetPath = TEST_ROOT + "/Z.cs";
            string firstPath = UIKitPanelCodeLayout.ToAbsolutePath(firstAssetPath);
            string blockedPath = UIKitPanelCodeLayout.ToAbsolutePath(blockedAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(firstPath));
            File.WriteAllText(firstPath, "before");
            Directory.CreateDirectory(blockedPath);
            Dictionary<string, string> sources = new()
            {
                [firstAssetPath] = "after",
                [blockedAssetPath] = "cannot replace a directory",
            };

            Assert.Throws<IOException>(() => UIKitPanelCodeGenerator.CommitSources(sources));
            Assert.AreEqual("before", File.ReadAllText(firstPath));
            Assert.IsTrue(Directory.Exists(blockedPath));
        }

        /// <summary>验证已编译 Panel 类型会挂载到 Prefab，回填后资产没有 Missing Script。</summary>
        [Test]
        public void BindingProcessorAttachesCompiledPanelType()
        {
            UIKitPanelCodeLayout layout = CreateLayout(
                nameof(UIKitLifecycleTestPanel),
                "YokiFrame.Tests",
                "YokiFrame.UIKit.PlayMode.Tests");
            UIKitPanelCodeLayout.EnsureAssetFolder(TEST_ROOT);
            AssetDatabase.Refresh();
            GameObject root = new(nameof(UIKitLifecycleTestPanel), typeof(RectTransform));
            try
            {
                PrefabUtility.SaveAsPrefabAsset(root, layout.PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            UIKitPrefabBindingStatus status = UIKitPrefabBindingProcessor.Bind(layout, out string error);
            Assert.AreEqual(UIKitPrefabBindingStatus.Success, status, error);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(layout.PrefabPath);
            Assert.IsNotNull(prefab.GetComponent<UIKitLifecycleTestPanel>());
            Assert.AreEqual(0, GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab));
        }

        /// <summary>验证一个内置 Member Bind 会按目标顺序展开并生成多个 Designer 字段。</summary>
        [Test]
        public void MultiMemberBindGeneratesAllSelectedComponentFields()
        {
            GameObject root = new("GeneratedPanel", typeof(RectTransform));
            GameObject child = new("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Bind));
            child.transform.SetParent(root.transform, false);
            try
            {
                Bind bind = child.GetComponent<Bind>();
                AddMemberTarget(bind, child.GetComponent<Button>(), "ConfirmButton");
                AddMemberTarget(bind, child.GetComponent<Image>(), "ConfirmButtonImage");

                UIKitBindScanResult scan = UIKitBindScanner.Scan(root);
                Assert.IsFalse(scan.HasErrors);
                Assert.AreEqual(2, scan.Nodes.Count);
                Assert.AreEqual("ConfirmButton", scan.Nodes[0].FieldName);
                Assert.AreSame(child.GetComponent<Button>(), scan.Nodes[0].Target);
                Assert.AreEqual("ConfirmButtonImage", scan.Nodes[1].FieldName);
                Assert.AreSame(child.GetComponent<Image>(), scan.Nodes[1].Target);

                UIKitPanelCodeLayout layout = CreateLayout("GeneratedPanel");
                string designer = UIKitPanelCodeGenerator.BuildSources(layout, scan)[layout.PanelDesignerPath];
                StringAssert.Contains("ConfirmButton", designer);
                StringAssert.Contains("ConfirmButtonImage", designer);
                StringAssert.Contains(typeof(Button).FullName, designer);
                StringAssert.Contains(typeof(Image).FullName, designer);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证编译后回填会把同一 Bind 的多个字段分别指向对应组件。</summary>
        [Test]
        public void BindingProcessorAssignsAllMultiMemberReferences()
        {
            UIKitPanelCodeLayout layout = CreateLayout(
                nameof(UIKitMultiMemberTestPanel),
                "YokiFrame.Tests",
                "YokiFrame.UIKit.Tests");
            GameObject root = new(nameof(UIKitMultiMemberTestPanel), typeof(RectTransform));
            GameObject child = new("ConfirmButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(Bind));
            child.transform.SetParent(root.transform, false);
            try
            {
                Bind bind = child.GetComponent<Bind>();
                AddMemberTarget(bind, child.GetComponent<Button>(), "ConfirmButton");
                AddMemberTarget(bind, child.GetComponent<Image>(), "ConfirmButtonImage");

                UIKitPrefabBindingStatus status = UIKitPrefabBindingProcessor.BindContents(layout, root, out string error);
                Assert.AreEqual(UIKitPrefabBindingStatus.Success, status, error);
                UIKitMultiMemberTestPanel panel = root.GetComponent<UIKitMultiMemberTestPanel>();
                Assert.AreSame(child.GetComponent<Button>(), panel.ConfirmButton);
                Assert.AreSame(child.GetComponent<Image>(), panel.ConfirmButtonImage);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证选择工具添加 Bind 时默认使用组件列表中最后一个非 Bind 组件。</summary>
        [Test]
        public void AddBindSelectsLastNonBindComponentByDefault()
        {
            GameObject target = new("DefaultTarget", typeof(RectTransform), typeof(Button), typeof(CanvasGroup));
            try
            {
                Selection.activeGameObject = target;
                UIKitPanelPrefabService.AddBindToSelection();
                Assert.AreSame(target.GetComponent<CanvasGroup>(), target.GetComponent<Bind>().Target);
            }
            finally
            {
                Selection.activeObject = default;
                Object.DestroyImmediate(target);
            }
        }

        /// <summary>验证独立 UIElement 只生成当前 owner Designer，不产生 Panel 文件。</summary>
        [Test]
        public void StandaloneElementGenerationBuildsOnlyOwnerDesigner()
        {
            AssertStandaloneOwnerSources(
                UIKitGeneratedOwnerKind.Element,
                typeof(UIKitStandaloneElementTest));
        }

        /// <summary>验证独立 UIComponent 只生成当前 owner Designer，不产生 Panel 文件。</summary>
        [Test]
        public void StandaloneComponentGenerationBuildsOnlyOwnerDesigner()
        {
            AssertStandaloneOwnerSources(
                UIKitGeneratedOwnerKind.Component,
                typeof(UIKitStandaloneComponentTest));
        }

        /// <summary>验证独立 owner 编译后回填会写入当前 Element 的字段。</summary>
        [Test]
        public void BindingProcessorAssignsStandaloneOwnerReferences()
        {
            UIKitPanelCodeLayout layout = CreateLayout("StandaloneHost");
            GameObject root = new(nameof(UIKitStandaloneElementTest), typeof(RectTransform), typeof(UIKitStandaloneElementTest));
            GameObject child = new("ConfirmButton", typeof(RectTransform), typeof(Button), typeof(Bind));
            child.transform.SetParent(root.transform, false);
            try
            {
                Bind bind = child.GetComponent<Bind>();
                bind.Name = "ConfirmButton";
                bind.Target = child.GetComponent<Button>();
                UIKitPrefabBindingStatus status = UIKitPrefabBindingProcessor.BindOwnerContents(
                    layout,
                    root,
                    typeof(UIKitStandaloneElementTest),
                    UIKitGeneratedOwnerKind.Element,
                    out string error);
                Assert.AreEqual(UIKitPrefabBindingStatus.Success, status, error);
                Assert.AreSame(
                    child.GetComponent<Button>(),
                    root.GetComponent<UIKitStandaloneElementTest>().ConfirmButton);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证 Panel Prefab 层级内的嵌套 owner 可按相对路径回填自身字段。</summary>
        [Test]
        public void BindingProcessorAssignsNestedOwnerReferences()
        {
            UIKitPanelCodeLayout layout = CreateLayout("NestedHost");
            GameObject root = new("Panel", typeof(RectTransform));
            GameObject ownerObject = new(
                "NestedElement",
                typeof(RectTransform),
                typeof(UIKitStandaloneElementTest));
            ownerObject.transform.SetParent(root.transform, false);
            GameObject child = new("ConfirmButton", typeof(RectTransform), typeof(Button), typeof(Bind));
            child.transform.SetParent(ownerObject.transform, false);
            try
            {
                Bind bind = child.GetComponent<Bind>();
                bind.Name = "ConfirmButton";
                bind.Target = child.GetComponent<Button>();
                UIKitPrefabBindingStatus status = UIKitPrefabBindingProcessor.BindOwnerContents(
                    layout,
                    root,
                    "0",
                    typeof(UIKitStandaloneElementTest),
                    UIKitGeneratedOwnerKind.Element,
                    out string error);
                Assert.AreEqual(UIKitPrefabBindingStatus.Success, status, error);
                Assert.AreSame(
                    child.GetComponent<Button>(),
                    ownerObject.GetComponent<UIKitStandaloneElementTest>().ConfirmButton);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证嵌套 owner 生成上下文只扫描自身子树，并记录相对 Prefab 根路径。</summary>
        [Test]
        public void NestedOwnerGenerationContextUsesOwnerSubtree()
        {
            GameObject root = new("Panel", typeof(RectTransform));
            GameObject container = new("Container", typeof(RectTransform));
            container.transform.SetParent(root.transform, false);
            GameObject ownerObject = new(
                "NestedElement",
                typeof(RectTransform),
                typeof(UIKitStandaloneElementTest));
            ownerObject.transform.SetParent(container.transform, false);
            try
            {
                UIKitGeneratedOwnerCodeService.ResolvePrefabContext(
                    ownerObject.GetComponent<UIKitStandaloneElementTest>(),
                    root,
                    TEST_ROOT + "/NestedHost.prefab",
                    out GameObject scanRoot,
                    out string prefabPath,
                    out string ownerPath);
                Assert.AreSame(ownerObject, scanRoot);
                Assert.AreEqual(TEST_ROOT + "/NestedHost.prefab", prefabPath);
                Assert.AreEqual("0/0", ownerPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证独立 UIComponent 根继续拒绝直接 Element 子绑定。</summary>
        [Test]
        public void StandaloneComponentScanRejectsElementChild()
        {
            GameObject root = new("ComponentRoot", typeof(RectTransform), typeof(UIKitStandaloneComponentTest));
            GameObject child = new("NestedElement", typeof(RectTransform), typeof(Bind));
            child.transform.SetParent(root.transform, false);
            try
            {
                Bind bind = child.GetComponent<Bind>();
                bind.Bind = BindType.Element;
                bind.Name = "NestedElement";
                bind.CustomType = "NestedElement";
                bind.Type = "NestedElement";
                UIKitBindScanResult scan = UIKitBindScanner.ScanOwner(
                    root,
                    UIKitGeneratedOwnerKind.Component);
                Assert.IsTrue(scan.HasErrors);
                StringAssert.Contains("Component 下不能定义 Element", scan.Diagnostics[0].Message);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>构建一个只有 Member 的 owner 扫描，并断言只输出指定 Designer。</summary>
        private static void AssertStandaloneOwnerSources(
            UIKitGeneratedOwnerKind ownerKind,
            System.Type ownerType)
        {
            UIKitPanelCodeLayout layout = CreateLayout("StandaloneHost");
            GameObject root = new(ownerType.Name, typeof(RectTransform), ownerType);
            GameObject child = new("ConfirmButton", typeof(RectTransform), typeof(Button), typeof(Bind));
            child.transform.SetParent(root.transform, false);
            try
            {
                Bind bind = child.GetComponent<Bind>();
                bind.Name = "ConfirmButton";
                bind.Target = child.GetComponent<Button>();
                UIKitBindScanResult scan = UIKitBindScanner.Scan(root);
                string designerPath = TEST_ROOT + "/" + ownerType.Name + ".Designer.cs";
                Dictionary<string, string> sources = UIKitPanelCodeGenerator.BuildOwnerSources(
                    layout,
                    scan,
                    ownerKind,
                    ownerType,
                    designerPath);
                Assert.AreEqual(1, sources.Count);
                Assert.IsTrue(sources.ContainsKey(designerPath));
                Assert.IsFalse(sources.ContainsKey(layout.PanelScriptPath));
                Assert.IsFalse(sources.ContainsKey(layout.PanelDesignerPath));
                StringAssert.Contains(ownerType.Name, sources[designerPath]);
                StringAssert.Contains("ConfirmButton", sources[designerPath]);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>创建使用测试专属 Assets 路径的生成布局。</summary>
        private static UIKitPanelCodeLayout CreateLayout(
            string panelName,
            string scriptNamespace = "YokiFrame.GeneratedTests",
            string assemblyName = "Assembly-CSharp")
        {
            return new UIKitPanelCodeLayout(new UIKitPanelGenerationRequest
            {
                panelName = panelName,
                prefabFolder = TEST_ROOT,
                scriptFolder = TEST_ROOT + "/Scripts",
                scriptNamespace = scriptNamespace,
                assemblyName = assemblyName,
                codeTemplate = UIKitPanelGenerationRequest.DEFAULT_TEMPLATE,
                prefabPath = TEST_ROOT + "/" + panelName + ".prefab",
            });
        }

    }

    /// <summary>提供多 Member Prefab 回填所需的已编译 Panel 字段。</summary>
    internal sealed class UIKitMultiMemberTestPanel : UIPanel
    {
        /// <summary>第一个 Member 的按钮引用。</summary>
        public Button ConfirmButton;

        /// <summary>第二个 Member 的图片引用。</summary>
        public Image ConfirmButtonImage;
    }

    /// <summary>提供独立 UIElement Designer 生成与回填目标。</summary>
    internal sealed partial class UIKitStandaloneElementTest : UIElement
    {
        /// <summary>由测试生成器回填的按钮引用。</summary>
        public Button ConfirmButton;
    }

    /// <summary>提供独立 UIComponent Designer 生成目标。</summary>
    internal sealed partial class UIKitStandaloneComponentTest : UIComponent
    {
    }
}
