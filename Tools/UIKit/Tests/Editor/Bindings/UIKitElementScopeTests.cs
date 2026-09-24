#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace YokiFrame.Tests
{
    /// <summary>验证局部元素的生成身份、独立入口与同名绑定隔离。</summary>
    public sealed class UIKitElementScopeTests
    {
        /// <summary>同名 Element 在两个公共组件下生成不同类型；离开组件子树后恢复 Panel 作用域。</summary>
        [Test]
        public void ComponentElementsAreIsolatedAndPanelLayoutIsUnchanged()
        {
            GameObject root = new("ScopePanel");
            try
            {
                Bind inventory = Child(root, "InventoryView", BindType.Component);
                Child(inventory.gameObject, "ItemRow", BindType.Element);
                Bind shop = Child(root, "ShopView", BindType.Component);
                Child(shop.gameObject, "ItemRow", BindType.Element);
                Child(root, "ItemRow", BindType.Element);
                UIKitPanelCodeLayout layout = Layout("ScopePanel");
                Dictionary<string, string> sources = UIKitPanelCodeGenerator.BuildSources(layout, UIKitBindScanner.Scan(root));
                Assert.That(sources[layout.GetComponentPath("InventoryView", true)], Does.Contain("ScopeTests.InventoryViewUIElement.ItemRow"));
                Assert.That(sources[layout.GetComponentPath("ShopView", true)], Does.Contain("ScopeTests.ShopViewUIElement.ItemRow"));
                Assert.That(sources[layout.PanelDesignerPath], Does.Contain("ScopeTests.ScopePanelUIElement.ItemRow"));
                Assert.IsTrue(sources.ContainsKey("Assets/__ScopeTests__/Scripts/Component/InventoryView/Element/ItemRow.cs"));
                Assert.IsTrue(sources.ContainsKey("Assets/__ScopeTests__/Scripts/Panel/ScopePanel/Element/ItemRow.cs"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>Element 不开启新类型作用域，嵌套公共 Component 则只影响自己的子树。</summary>
        [Test]
        public void NestedElementsShareScopeAndNestedComponentsReplaceScope()
        {
            GameObject root = new("ScopePanel");
            try
            {
                Bind inventory = Child(root, "InventoryView", BindType.Component);
                Bind row = Child(inventory.gameObject, "ItemRow", BindType.Element);
                Child(row.gameObject, "Label", BindType.Element);
                Bind price = Child(row.gameObject, "PriceView", BindType.Component);
                Child(price.gameObject, "Label", BindType.Element);
                UIKitPanelCodeLayout layout = Layout("ScopePanel");
                Dictionary<string, string> sources = UIKitPanelCodeGenerator.BuildSources(layout, UIKitBindScanner.Scan(root));
                Assert.That(sources[layout.ForComponent("InventoryView").GetElementPath("ItemRow", true)],
                    Does.Contain("ScopeTests.InventoryViewUIElement.Label"));
                Assert.That(sources[layout.GetComponentPath("PriceView", true)], Does.Contain("ScopeTests.PriceViewUIElement.Label"));
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>同一个 Component 在不同 Panel 以及从 Bind 独立生成时，其局部类型源码相同。</summary>
        [Test]
        public void ComponentSourcesDoNotDependOnHostingPanelOrEntryPoint()
        {
            GameObject root = new("FirstPanel");
            try
            {
                Bind component = Child(root, "InventoryView", BindType.Component);
                Child(component.gameObject, "ItemRow", BindType.Element);
                UIKitPanelCodeLayout first = Layout("FirstPanel");
                Dictionary<string, string> panelSources = UIKitPanelCodeGenerator.BuildSources(first, UIKitBindScanner.Scan(root));
                Dictionary<string, string> bindSources = UIKitPanelCodeGenerator.BuildBindSources(Layout("OtherPanel"), component);
                foreach (var source in bindSources) Assert.AreEqual(panelSources[source.Key], source.Value, source.Key);
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>尚未挂载用户脚本的 Element/Component Bind 也有明确的一键生成入口。</summary>
        [TestCase(BindType.Element, "生成 UIElement 代码")]
        [TestCase(BindType.Component, "生成 UIComponent 代码")]
        public void UngeneratedBindInspectorProvidesGenerateButton(BindType kind, string label)
        {
            GameObject root = new("ScopePanel");
            Bind bind = Child(root, "Item", kind);
            UnityEditor.Editor editor = UnityEditor.Editor.CreateEditor(bind);
            try
            {
                VisualElement visual = editor.CreateInspectorGUI();
                Button found = null;
                visual.Query<Button>().ForEach(button => { if (button.text == label) found = button; });
                Assert.IsNotNull(found);
                Assert.AreNotEqual(DisplayStyle.None, found.style.display.value);
            }
            finally { Object.DestroyImmediate(editor); Object.DestroyImmediate(root); }
        }

        /// <summary>标准 Component 局部脚本可以脱离 Panel 层级恢复原代码布局。</summary>
        [Test]
        public void StandaloneElementRestoresComponentScopeFromScriptLayout()
        {
            UIKitPanelCodeLayout layout = UIKitGeneratedOwnerCodeService.CreateLayout(
                typeof(ScopeTests.InventoryViewUIElement.ItemRow), UIKitGeneratedOwnerKind.Element,
                "Assets/__ScopeTests__/Scripts/Component/InventoryView/Element/ItemRow.cs", "Assets/ItemRow.prefab");
            Assert.AreEqual("InventoryView", layout.ElementComponentName);
            Assert.AreEqual("ScopeTests.InventoryViewUIElement", layout.GetElementNamespace());
            Assert.AreEqual("Assets/__ScopeTests__/Scripts", layout.ScriptFolder);
        }

        /// <summary>编译期间层级目标改变后，回填不得把待生成类型挂到没有对应 Bind 的节点。</summary>
        [Test]
        public void OwnerBackfillRejectsChangedUnboundTarget()
        {
            GameObject root = new("ChangedTarget");
            try
            {
                UIKitPrefabBindingStatus status = UIKitPrefabBindingProcessor.BindOwnerContents(Layout("ScopePanel"), root,
                    typeof(UIKitStandaloneComponentTest), UIKitGeneratedOwnerKind.Component, out string error);
                Assert.AreEqual(UIKitPrefabBindingStatus.Failed, status);
                Assert.That(error, Does.Contain("待回填节点已改变"));
                Assert.IsNull(root.GetComponent<UIComponent>());
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>构造只在内存中生成代码的测试布局，不写入项目业务目录。</summary>
        private static UIKitPanelCodeLayout Layout(string panelName)
        {
            return new UIKitPanelCodeLayout(new UIKitPanelGenerationRequest
            {
                panelName = panelName, scriptNamespace = "ScopeTests", scriptFolder = "Assets/__ScopeTests__/Scripts",
                prefabFolder = "Assets/__ScopeTests__", assemblyName = "Assembly-CSharp",
            });
        }

        /// <summary>构造有明确类型名和字段名的子绑定；生命周期由测试根 GameObject 管理。</summary>
        private static Bind Child(GameObject parent, string name, BindType kind)
        {
            GameObject child = new(name, typeof(Bind));
            child.transform.SetParent(parent.transform, false);
            Bind bind = child.GetComponent<Bind>();
            bind.Bind = kind;
            bind.Name = name;
            bind.Type = name;
            bind.CustomType = name;
            return bind;
        }
    }
}

namespace ScopeTests.InventoryViewUIElement
{
    /// <summary>为独立作用域解析提供准确的类型命名空间，不写入业务程序集。</summary>
    internal sealed class ItemRow : YokiFrame.UIElement { }
}
#endif
