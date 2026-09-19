#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using TypeMove = YokiFrame.UIKitConversionSource.TypeMove;

namespace YokiFrame.Tests
{
    /// <summary>验证类型转换保留业务源码，正确更新声明和引用，并拒绝不明确的声明结构。</summary>
    public sealed class UIKitConversionSourceTests
    {
        /// <summary>提升 Element 时仅改变归属和基类，业务字段、方法、字符串与注释原样保留。</summary>
        [Test]
        public void PromotionPreservesBusinessImplementationAndLiteralText()
        {
            TypeMove move = Promotion();
            const string source = "using YokiFrame;\nnamespace Game.InventoryPanelUIElement\n{\n"
                + "public partial class Row : UIElement\n{\n"
                + "// Game.InventoryPanelUIElement.Row should remain in comments\n"
                + "public string Label = \"Game.InventoryPanelUIElement.Row\";\n"
                + "public int Count = 7;\npublic int Value() => Count * 2;\n}\n}";
            string result = UIKitConversionSource.Rewrite(source, new List<TypeMove> { move }, move, _ => false);
            Assert.That(result, Does.Contain("namespace Game"));
            Assert.That(result, Does.Contain("class Row : YokiFrame.UIComponent"));
            Assert.That(result, Does.Contain("public int Value() => Count * 2;"));
            Assert.That(result, Does.Contain("\"Game.InventoryPanelUIElement.Row\""));
            Assert.That(result, Does.Contain("// Game.InventoryPanelUIElement.Row should remain"));
        }

        /// <summary>外部完整类型引用更新，普通 using 的短类型引用通过精确别名保持有效。</summary>
        [Test]
        public void ReferencesUseNewTypeWithoutReplacingVariableNames()
        {
            const string source = "using Game.InventoryPanelUIElement;\nnamespace Game.Client\n{\n"
                + "class Client { Game.InventoryPanelUIElement.Row First; Row Second; int RowCount; }\n}";
            string result = UIKitConversionSource.Rewrite(source, new List<TypeMove> { Promotion() }, null, _ => false);
            Assert.That(result, Does.Contain("Game.Row First"));
            Assert.That(result, Does.Contain("using Row = global::Game.Row;"));
            Assert.That(result, Does.Contain("Row Second; int RowCount;"));
            Assert.That(result, Does.Not.Contain("using Game.InventoryPanelUIElement;"));
        }

        /// <summary>用户自行拆分的 partial 可以没有基类，但仍迁移到同一个新 namespace。</summary>
        [Test]
        public void AdditionalPartialRetainsMethodsAndRequiresSingleClass()
        {
            TypeMove move = Promotion();
            string result = UIKitConversionSource.Rewrite(
                "namespace Game.InventoryPanelUIElement { public partial class Row { public void Refresh() {} } }",
                new List<TypeMove> { move }, move, _ => false);
            Assert.That(result, Does.Contain("namespace Game { public partial class Row { public void Refresh() {} } }"));
            Assert.Throws<InvalidOperationException>(() => UIKitConversionSource.Rewrite(
                "namespace Game.InventoryPanelUIElement { public partial class Row {} class Other {} }",
                new List<TypeMove> { move }, move, _ => false));
        }

        /// <summary>同名类型属于不同作用域时，只改写明确匹配的完整类型引用。</summary>
        [Test]
        public void SameShortNameInOtherScopeIsUnchanged()
        {
            const string source = "namespace Game.Client { class Client { Game.ShopViewUIElement.Row Other; Game.InventoryPanelUIElement.Row Target; } }";
            string result = UIKitConversionSource.Rewrite(source, new List<TypeMove> { Promotion() }, null, _ => true);
            Assert.That(result, Does.Contain("Game.ShopViewUIElement.Row Other"));
            Assert.That(result, Does.Contain("Game.Row Target"));
        }

        /// <summary>已存在的类型别名通过更新目标复用，反向转换不会追加重复别名。</summary>
        [Test]
        public void ReverseConversionReusesExistingAlias()
        {
            TypeMove reverse = new() { OldName = "Game.Row", NewName = "Game.InventoryPanelUIElement.Row", NewBase = "UIElement" };
            string result = UIKitConversionSource.Rewrite(
                "namespace Game { using Row = global::Game.Row; class Client { Row Value; } }",
                new List<TypeMove> { reverse }, null, _ => true);
            Assert.AreEqual(1, result.Split(new[] { "using Row =" }, StringSplitOptions.None).Length - 1);
            Assert.That(result, Does.Contain("global::Game.InventoryPanelUIElement.Row"));
        }

        /// <summary>创建符合现有生成布局的 Element 提升案例。</summary>
        private static TypeMove Promotion()
        {
            return new TypeMove { OldName = "Game.InventoryPanelUIElement.Row", NewName = "Game.Row",
                NewBase = "UIComponent", OldPath = "Assets/UI/InventoryPanel/UIElement/Row.cs" };
        }
    }
}
#endif
