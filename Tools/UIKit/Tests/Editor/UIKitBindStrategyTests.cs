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

        /// <summary>验证 Member 显式 Target 优先于旧类型文本，组件层级拒绝 Element。</summary>
        [Test]
        public void BuiltInsPreferExplicitTargetAndValidateComponentChildren()
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

                bind.Bind = BindType.Component;
                Assert.IsTrue(UIKitBindStrategyRegistry.TryGet(bind, out IUIKitBindStrategy component, out _));
                Assert.IsFalse(component.TryValidateChild(BindType.Element, out error));
                StringAssert.Contains("Component", error);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>验证同一 owner 的重复 Member 字段形成带完整层级路径的阻断诊断。</summary>
        [Test]
        public void ScannerRejectsDuplicateMemberNamesWithPath()
        {
            GameObject root = new("Panel", typeof(RectTransform));
            try
            {
                CreateChildMember(root.transform, "First", "Shared");
                CreateChildMember(root.transform, "Second", "Shared");
                UIKitBindScanResult scan = UIKitBindScanner.Scan(root);

                Assert.IsTrue(scan.HasErrors);
                Assert.AreEqual(1, scan.Nodes.Count);
                Assert.AreEqual(1, scan.Diagnostics.Count);
                StringAssert.Contains("Panel/Second", scan.Diagnostics[0].Path);
                StringAssert.Contains("字段名重复", scan.Diagnostics[0].Message);
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
