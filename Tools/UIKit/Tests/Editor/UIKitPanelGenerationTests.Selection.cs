using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame.Tests
{
    public sealed partial class UIKitPanelGenerationTests
    {
        private const string OWNER_TYPES_PANEL = "OwnerTypesPanel";

        /// <summary>验证自动生成的 Element 和 Component 用户脚本使用已导入的框架短类型名。</summary>
        [Test]
        public void BuildSources_生成Element和Component_使用短类型名()
        {
            UIKitPanelCodeLayout layout = CreateLayout(OWNER_TYPES_PANEL);
            GameObject root = new(OWNER_TYPES_PANEL, typeof(RectTransform));
            CreateGeneratedOwner(root, "InventoryElement", BindType.Element);
            CreateGeneratedOwner(root, "InventoryComponent", BindType.Component);
            try
            {
                UIKitBindScanResult scan = UIKitBindScanner.Scan(root);
                Assert.IsFalse(scan.HasErrors);
                Dictionary<string, string> sources = UIKitPanelCodeGenerator.BuildSources(layout, scan);
                AssertDeclaration(
                    sources[layout.GetElementPath("InventoryElement", false)],
                    "public partial class InventoryElement : UIElement");
                AssertDeclaration(
                    sources[layout.GetComponentPath("InventoryComponent", false)],
                    "public partial class InventoryComponent : UIComponent");
                string panelDesigner = sources[layout.PanelDesignerPath];
                AssertDeclaration(
                    panelDesigner,
                    "public YokiFrame.GeneratedTests.OwnerTypesPanelUIElement.InventoryElement InventoryElement;");
                AssertDeclaration(
                    panelDesigner,
                    "public YokiFrame.GeneratedTests.InventoryComponent InventoryComponent;");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>创建一个配置了生成类型名的测试 owner Bind。</summary>
        /// <param name="root">面板根对象。</param>
        /// <param name="typeName">生成类型名。</param>
        /// <param name="bindType">Bind 输出类型。</param>
        private static void CreateGeneratedOwner(
            GameObject root,
            string typeName,
            BindType bindType)
        {
            GameObject owner = new(typeName, typeof(RectTransform), typeof(Bind));
            owner.transform.SetParent(root.transform, false);
            Bind bind = owner.GetComponent<Bind>();
            bind.Bind = bindType;
            bind.Name = typeName;
            bind.CustomType = typeName;
            bind.Type = typeName;
        }

        /// <summary>判断生成源码是否包含去掉 Tab 缩进后完全相等的声明行。</summary>
        /// <param name="source">生成源码。</param>
        /// <param name="declaration">不含缩进的完整声明。</param>
        private static void AssertDeclaration(string source, string declaration)
        {
            string[] lines = source.Split('\n');
            for (int index = 0; index < lines.Length; index++)
            {
                if (lines[index].TrimStart('\t') == declaration) return;
            }

            Assert.Fail("缺少声明: " + declaration);
        }

        /// <summary>验证通用 selection action 拒绝把 Element/Component Prefab 生成成 Panel。</summary>
        /// <param name="ownerType">已编译的独立 owner 类型。</param>
        /// <param name="ownerLabel">拒绝消息中的 owner 名称。</param>
        [TestCase(typeof(UIKitStandaloneElementTest), "UIElement")]
        [TestCase(typeof(UIKitStandaloneComponentTest), "UIComponent")]
        public void RequirePanelPrefab_独立Owner_拒绝当Panel生成(
            Type ownerType,
            string ownerLabel)
        {
            GameObject root = new(ownerType.Name, typeof(RectTransform), ownerType);
            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => UIKitPanelPrefabService.RequirePanelPrefab(root));
                Assert.AreEqual(ownerLabel + " 请从组件 Inspector 单独生成代码。", exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>向测试 Bind 追加一个有稳定字段名的 Member 目标。</summary>
        /// <param name="bind">待追加目标的 Bind。</param>
        /// <param name="target">成员组件。</param>
        /// <param name="fieldName">生成字段名。</param>
        private static void AddMemberTarget(Bind bind, Component target, string fieldName)
        {
            bind.MemberTargets.Add(new BindMemberTarget
            {
                Target = target,
                Name = fieldName,
            });
            if (bind.MemberTargets.Count != 1)
                return;
            bind.Target = target;
            bind.Name = fieldName;
            bind.Type = target.GetType().FullName;
            bind.AutoType = bind.Type;
        }
    }
}