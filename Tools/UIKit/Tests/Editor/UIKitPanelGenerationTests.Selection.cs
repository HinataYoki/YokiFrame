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
        public void GeneratedOwnerSourcesUseImportedYokiFrameTypes()
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
                AssertGeneratedOwnerSource(
                    sources[layout.GetElementPath("InventoryElement", false)],
                    "UIElement");
                AssertGeneratedOwnerSource(
                    sources[layout.GetComponentPath("InventoryComponent", false)],
                    "UIComponent");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>创建一个配置了生成类型名的测试 owner Bind。</summary>
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

        /// <summary>断言 owner 用户脚本使用短基类名且没有框架全局限定前缀。</summary>
        private static void AssertGeneratedOwnerSource(string source, string baseTypeName)
        {
            StringAssert.Contains(": " + baseTypeName, source);
            StringAssert.DoesNotContain("global::YokiFrame.", source);
        }

        /// <summary>验证通用 selection action 拒绝把 Element/Component Prefab 生成成 Panel。</summary>
        [TestCase(typeof(UIKitStandaloneElementTest), "UIElement")]
        [TestCase(typeof(UIKitStandaloneComponentTest), "UIComponent")]
        public void GenericSelectionGenerationRejectsStandaloneOwnerPrefab(
            Type ownerType,
            string ownerLabel)
        {
            GameObject root = new(ownerType.Name, typeof(RectTransform), ownerType);
            try
            {
                InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
                    () => UIKitPanelPrefabService.RequirePanelPrefab(root));
                StringAssert.Contains(ownerLabel, exception.Message);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        /// <summary>向测试 Bind 追加一个有稳定字段名的 Member 目标。</summary>
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

        /// <summary>测试统一 Editor Settings JSON 的最小解析结构。</summary>
        [Serializable]
        private sealed class EditorSettingsDocument
        {
            public EditorSettingsEntry[] settings;
        }

        /// <summary>测试统一 Editor Settings 的单条键值。</summary>
        [Serializable]
        private sealed class EditorSettingsEntry
        {
            public string kit;
            public string key;
            public string value;
        }
    }
}
