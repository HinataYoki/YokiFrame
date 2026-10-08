using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace YokiFrame.Tests
{
    /// <summary>验证 Editor 固定 BindType 策略的解析和层级诊断。</summary>
    public sealed class UIKitBindStrategyTests
    {
        /// <summary>每条测试前恢复内置策略，消除静态状态干扰。</summary>
        [SetUp]
        public void SetUp()
        {
            UIKitBindStrategyRegistry.ResetForTests();
        }

        /// <summary>验证 Member 显式 Target 优先于旧类型文本。</summary>
        [Test]
        public void TryResolve_显式Target_优先于类型文本()
        {
            GameObject root = CreateMemberNode("Item", out Bind bind, out Image image);
            try
            {
                bind.Type = typeof(RectTransform).FullName;
                bind.Target = image;
                Assert.IsTrue(UIKitBindStrategyRegistry.TryGet(bind, out IUIKitBindStrategy member, out _));
                Assert.IsTrue(member.TryResolve(bind, out string typeName, out UnityEngine.Object target, out string error), error);
                Assert.AreEqual(typeof(Image).FullName, typeName);
                Assert.AreSame(image, target);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证 Component 策略允许局部 Element 子节点。</summary>
        [Test]
        public void TryValidateChild_Component包含Element_通过校验()
        {
            GameObject root = CreateMemberNode("Item", out Bind bind, out _);
            try
            {
                bind.Bind = BindType.Component;
                Assert.IsTrue(UIKitBindStrategyRegistry.TryGet(bind, out IUIKitBindStrategy component, out _));
                Assert.IsTrue(component.TryValidateChild(BindType.Element, out string error));
                Assert.IsEmpty(error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证同一 owner 的重复 Member 字段会阻断第二个节点，并保留首个节点。</summary>
        [Test]
        public void Scan_重复Member字段_阻断并保留首个节点()
        {
            GameObject root = new("Panel", typeof(RectTransform));
            try
            {
                CreateChildMember(root.transform, "First", "Shared");
                CreateChildMember(root.transform, "Second", "Shared");
                UIKitBindScanResult scan = UIKitBindScanner.Scan(root);

                Assert.IsTrue(scan.HasErrors);
                Assert.AreEqual(1, scan.Nodes.Count);
                Assert.AreEqual("Shared", scan.Nodes[0].FieldName);
                Assert.AreEqual("Panel/First", scan.Nodes[0].Path);
                Assert.AreEqual(1, scan.Diagnostics.Count);
                UIKitBindDiagnostic diagnostic = scan.Diagnostics[0];
                Assert.AreEqual(UIKitBindDiagnosticSeverity.Error, diagnostic.Severity);
                Assert.AreEqual("Panel/Second", diagnostic.Path);
                Assert.AreEqual("字段名重复: Shared，首次出现于 Panel/First", diagnostic.Message);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>创建带 Image 显式目标的 Member Bind 节点。</summary>
        private static GameObject CreateMemberNode(string name, out Bind bind, out Image image)
        {
            GameObject gameObject = new(name, typeof(RectTransform), typeof(Image), typeof(Bind));
            image = gameObject.GetComponent<Image>();
            bind = gameObject.GetComponent<Bind>();
            bind.Bind = BindType.Member;
            bind.Name = name;
            bind.Target = image;
            return gameObject;
        }

        /// <summary>在指定父节点下创建一个具名 Member Bind。</summary>
        private static void CreateChildMember(Transform parent, string objectName, string fieldName)
        {
            GameObject child = CreateMemberNode(objectName, out Bind bind, out _);
            child.transform.SetParent(parent, false);
            bind.Name = fieldName;
        }
    }
}
