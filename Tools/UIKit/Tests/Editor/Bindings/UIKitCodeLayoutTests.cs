#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame.Tests
{
    /// <summary>固定统一目录规则、实际脚本反解和首次 Element 的明确归属要求。</summary>
    public sealed class UIKitCodeLayoutTests
    {
        private const string TEST_ROOT = "Assets/__UIKitCodeLayoutTests__";

        /// <summary>清理测试专属 Prefab 资源，不触及用户生成根。</summary>
        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(TEST_ROOT)) AssetDatabase.DeleteAsset(TEST_ROOT);
        }

        /// <summary>四种输出归属均能由实际 MonoScript 身份反解，命名空间不随物理目录变化。</summary>
        [TestCase(typeof(LayoutFixtures.LayoutPanel), "Panel/LayoutPanel/LayoutPanel.cs", "")]
        [TestCase(typeof(LayoutFixtures.LayoutView), "Component/LayoutView/LayoutView.cs", "LayoutView")]
        [TestCase(typeof(LayoutFixtures.LayoutPanelUIElement.Item), "Panel/LayoutPanel/Element/Item.cs", "")]
        [TestCase(typeof(LayoutFixtures.LayoutViewUIElement.Item), "Component/LayoutView/Element/Item.cs", "LayoutView")]
        public void ScriptLayoutRoundTrips(Type type, string relative, string component)
        {
            string path = TEST_ROOT + "/" + relative;
            var layout = UIKitPanelCodeLayout.FromScript(type, path, TEST_ROOT + "/Independent.prefab");
            Assert.AreEqual(TEST_ROOT, layout.ScriptFolder);
            Assert.AreEqual("LayoutFixtures", layout.ScriptNamespace);
            Assert.AreEqual(type.Assembly.GetName().Name, layout.AssemblyName);
            Assert.AreEqual(component, layout.ElementComponentName);
            string actual = typeof(UIPanel).IsAssignableFrom(type) ? layout.PanelScriptPath
                : typeof(UIComponent).IsAssignableFrom(type) ? layout.GetComponentPath(type.Name, false)
                : layout.GetElementPath(type.Name, false);
            Assert.AreEqual(path, actual);
        }

        /// <summary>旧目录、错误 owner 或类型文件名不一致必须拒绝，不能猜测命名空间后继续生成。</summary>
        [TestCase("UIComponent/LayoutView/UIElement/Item.cs")]
        [TestCase("Component/Other/Element/Item.cs")]
        [TestCase("Component/LayoutView/Element/Other.cs")]
        public void InvalidElementIdentityIsRejected(string relative)
        {
            Assert.Throws<InvalidOperationException>(() => UIKitPanelCodeLayout.FromScript(
                typeof(LayoutFixtures.LayoutViewUIElement.Item), TEST_ROOT + "/" + relative, TEST_ROOT + "/Item.prefab"));
        }

        /// <summary>独立首次 Element 没有 owner 时失败，放入未生成 Component 后可明确建立局部作用域。</summary>
        [TestCase(false)]
        [TestCase(true)]
        public void FirstElementRequiresExplicitOwner(bool withComponent)
        {
            GameObject root = new("MisleadingPrefabName", typeof(Bind));
            try
            {
                Bind rootBind = root.GetComponent<Bind>();
                rootBind.Bind = withComponent ? BindType.Component : BindType.Element;
                rootBind.CustomType = "InventoryView";
                GameObject child = new("Item", typeof(Bind));
                child.transform.SetParent(root.transform, false);
                Bind element = child.GetComponent<Bind>();
                element.Bind = BindType.Element;
                element.CustomType = "Item";
                Directory.CreateDirectory(TEST_ROOT);
                AssetDatabase.Refresh();
                GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, TEST_ROOT + "/Unrelated.prefab");
                Bind target = prefab.transform.GetChild(0).GetComponent<Bind>();
                if (!withComponent)
                {
                    Assert.That(Assert.Throws<InvalidOperationException>(() => UIKitBindCodeService.ResolveLayout(target)).Message,
                        Does.Contain("Element 缺少所属"));
                    return;
                }
                UIKitPanelCodeLayout layout = UIKitBindCodeService.ResolveLayout(target);
                Assert.AreEqual("InventoryView", layout.ElementComponentName);
                Assert.That(layout.GetElementNamespace(), Does.EndWith(".InventoryViewUIElement"));
                Assert.That(layout.GetElementPath("Item", false), Does.EndWith("/Component/InventoryView/Element/Item.cs"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>旧目录文件会登记到同一次生成事务；需要整体迁移共同脚本根时，仍可由用户主动执行目录迁移菜单。</summary>
        [Test]
        public void GenerationPlansLegacySourceMigration()
        {
            Directory.CreateDirectory(TEST_ROOT + "/OldPanel");
            File.WriteAllText(TEST_ROOT + "/OldPanel/OldPanel.cs", "// user code");
            AssetDatabase.Refresh();
            var layout = new UIKitPanelCodeLayout(new UIKitPanelGenerationRequest
            { panelName = "OldPanel", scriptFolder = TEST_ROOT, scriptNamespace = "LayoutLegacyTests" });
            Dictionary<string, string> relocations = new();
            Dictionary<string, string> sources = UIKitPanelCodeGenerator.BuildSources(layout,
                new UIKitBindScanResult("OldPanel"), relocations, new List<string>());
            Assert.IsTrue(relocations.ContainsKey(TEST_ROOT + "/OldPanel/OldPanel.cs"));
            Assert.IsTrue(relocations.ContainsValue(layout.PanelScriptPath));
            Assert.IsTrue(sources.ContainsKey(layout.PanelDesignerPath));
            Assert.IsFalse(Directory.Exists(TEST_ROOT + "/Panel"));
        }
    }
}

namespace LayoutFixtures
{
    internal sealed class LayoutPanel : YokiFrame.UIPanel { }
    internal sealed class LayoutView : YokiFrame.UIComponent { }
}
namespace LayoutFixtures.LayoutPanelUIElement { internal sealed class Item : YokiFrame.UIElement { } }
namespace LayoutFixtures.LayoutViewUIElement { internal sealed class Item : YokiFrame.UIElement { } }
#endif
