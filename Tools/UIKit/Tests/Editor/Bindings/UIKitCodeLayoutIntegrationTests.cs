#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace YokiFrame.Tests
{
    /// <summary>跨真实编译和 Domain Reload 验证目录迁移、Prefab 引用和新布局的双向类型转换。</summary>
    public sealed class UIKitCodeLayoutIntegrationTests
    {
        private const string ROOT = "Assets/__UIKitLayoutIntegrationTests__";
        private const string PREFAB = ROOT + "/Smoke.prefab";
        private const string ROW = ROOT + "/Component/SmokeView/Element/SmokeRow.cs";
        [SerializeField] private string mQueue;
        [SerializeField] private string mRowGuid;
        [SerializeField] private string mBadgeGuid;
        [SerializeField] private bool mOwnsAssets;

        /// <summary>只创建本测试拥有的隔离旧布局源码，编译后再挂载到 Prefab。</summary>
        [UnitySetUp]
        public IEnumerator SetUp()
        {
            Assert.IsFalse(Directory.Exists(ROOT), "隔离资源根已存在，拒绝覆盖。");
            Assert.IsFalse(UIKitConversionTransaction.IsPending || UIKitCodeLayoutMigrationTransaction.IsPending);
            mQueue = UIKitPendingBindingService.CaptureQueue();
            mOwnsAssets = true;
            Write("SmokePanel/SmokePanel.cs", "namespace UIKitLayoutSmoke { public partial class SmokePanel : YokiFrame.UIPanel {} }");
            Write("UIComponent/SmokeView.cs", "namespace UIKitLayoutSmoke { public partial class SmokeView : YokiFrame.UIComponent {} }");
            Write("UIComponent/CustomBehaviour.cs", "namespace UIKitLayoutSmoke { public partial class SmokeView { public int Value() => 42; } }");
            Write("UIComponent/SmokeView/UIElement/SmokeRow.cs", "namespace UIKitLayoutSmoke.SmokeViewUIElement { public partial class SmokeRow : YokiFrame.UIElement { public int Count = 7; public int Value() => Count * 2; } }");
            Write("UIComponent/SmokeView/UIElement/SmokeBadge.cs", "namespace UIKitLayoutSmoke.SmokeViewUIElement { public partial class SmokeBadge : YokiFrame.UIElement { public int Code = 3; } }");
            yield return new RecompileScripts();
            CreatePrefab();
        }

        /// <summary>完成真实迁移及 Element/Component 双向转换，检查业务方法、序列化值、GUID 和子元素归属。</summary>
        [UnityTest]
        public IEnumerator MigrationAndBidirectionalConversionPreserveIdentity()
        {
            UIKitCodeLayoutMigrationPlan plan = UIKitCodeLayoutMigration.Build(ROOT);
            Assert.Greater(plan.Moves.Count, 0);
            UIKitCodeLayoutMigrationTransaction.Execute(plan);
            yield return new WaitForDomainReload();
            yield return WaitForTransactions();
            Assert.AreEqual(0, UIKitCodeLayoutMigration.Build(ROOT).Moves.Count);
            Assert.IsTrue(File.Exists(ROOT + "/Component/SmokeView/CustomBehaviour.cs"));
            AssertOwner("UIKitLayoutSmoke.SmokeViewUIElement.SmokeRow", ROW);
            Assert.AreEqual(mBadgeGuid, AssetDatabase.AssetPathToGUID(ROOT + "/Component/SmokeView/Element/SmokeBadge.cs"));
            UIKitBindConversion.Convert(RowBind(), BindType.Component);
            yield return new WaitForDomainReload();
            yield return WaitForTransactions();
            AssertOwner("UIKitLayoutSmoke.SmokeRow", ROOT + "/Component/SmokeRow/SmokeRow.cs");
            Assert.AreEqual(mBadgeGuid, AssetDatabase.AssetPathToGUID(ROOT + "/Component/SmokeRow/Element/SmokeBadge.cs"));
            UIKitBindConversion.Convert(RowBind(), BindType.Element);
            yield return new WaitForDomainReload();
            yield return WaitForTransactions();
            AssertOwner("UIKitLayoutSmoke.SmokeViewUIElement.SmokeRow", ROW);
            Assert.AreEqual(mBadgeGuid, AssetDatabase.AssetPathToGUID(ROOT + "/Component/SmokeView/Element/SmokeBadge.cs"));
            UIKitPanelCodeLayout layout = UIKitBindCodeService.ResolveLayout(RowBind());
            Assert.AreEqual(ROOT, layout.ScriptFolder);
            Assert.AreEqual("SmokeView", layout.ElementComponentName);
            var sources = UIKitPanelCodeGenerator.BuildBindSources(layout, RowBind());
            UIKitPanelCodeGenerator.CommitSources(sources);
            Assert.IsFalse(UIKitPanelCodeGenerator.CommitSources(UIKitPanelCodeGenerator.BuildBindSources(layout, RowBind())));
        }

        /// <summary>删除本测试创建的整个隔离资源根，并编译恢复；不清空用户的待回填队列。</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (!mOwnsAssets) yield break;
            UIKitPendingBindingService.RestoreQueue(mQueue);
            UIKitCodeLayoutMigrationTransaction.Save(null);
            Assert.IsFalse(UIKitConversionTransaction.IsPending, "转换仍在等待，保留隔离资源以便诊断。");
            if (AssetDatabase.IsValidFolder(ROOT)) AssetDatabase.DeleteAsset(ROOT);
            mOwnsAssets = false;
            yield return new RecompileScripts();
        }

        /// <summary>为测试源码创建精确目录，所有产物只能位于本测试的隔离根。</summary>
        private static void Write(string relative, string source)
        {
            string path = ROOT + "/" + relative;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, source);
        }

        /// <summary>编译后的真实类型挂到测试 Prefab，设置不同于代码默认值的序列化字段。</summary>
        private void CreatePrefab()
        {
            GameObject root = new("Smoke", RequireType("UIKitLayoutSmoke.SmokePanel"));
            try
            {
                GameObject view = AddOwner(root, "SmokeView", BindType.Component, "UIKitLayoutSmoke.SmokeView");
                GameObject row = AddOwner(view, "SmokeRow", BindType.Element, "UIKitLayoutSmoke.SmokeViewUIElement.SmokeRow");
                AddOwner(row, "SmokeBadge", BindType.Element, "UIKitLayoutSmoke.SmokeViewUIElement.SmokeBadge");
                UIElement owner = row.GetComponent<UIElement>();
                owner.GetType().GetField("Count").SetValue(owner, 19);
                PrefabUtility.SaveAsPrefabAsset(root, PREFAB);
                mRowGuid = AssetDatabase.AssetPathToGUID(ROOT + "/UIComponent/SmokeView/UIElement/SmokeRow.cs");
                mBadgeGuid = AssetDatabase.AssetPathToGUID(ROOT + "/UIComponent/SmokeView/UIElement/SmokeBadge.cs");
            }
            finally { Object.DestroyImmediate(root); }
        }

        /// <summary>建立有准确 Bind 类型信息和具体脚本的子节点，模拟用户已使用的旧 Prefab。</summary>
        private static GameObject AddOwner(GameObject parent, string name, BindType kind, string fullName)
        {
            GameObject child = new(name, typeof(Bind), RequireType(fullName));
            child.transform.SetParent(parent.transform, false);
            Bind bind = child.GetComponent<Bind>();
            bind.Bind = kind;
            bind.Name = name;
            bind.Type = name;
            bind.CustomType = name;
            return child;
        }

        /// <summary>通过业务程序集中的准确名称查找临时生成类，不使测试程序集静态依赖动态源码。</summary>
        private static Type RequireType(string name) => Type.GetType(name + ", Assembly-CSharp", true);

        /// <summary>每次重载后重新读取 Prefab Bind，避免持有失效的 Unity 对象引用。</summary>
        private static Bind RowBind() => AssetDatabase.LoadAssetAtPath<GameObject>(PREFAB)
            .transform.GetChild(0).GetChild(0).GetComponent<Bind>();

        /// <summary>断言真实挂载类型、业务逻辑和序列化字段在移动与重载后均保持有效。</summary>
        private void AssertOwner(string fullName, string path)
        {
            UIElement owner = RowBind().GetComponent<UIElement>();
            Assert.IsNotNull(owner);
            Assert.AreEqual(fullName, owner.GetType().FullName);
            Assert.AreEqual(19, owner.GetType().GetField("Count").GetValue(owner));
            Assert.AreEqual(38, owner.GetType().GetMethod("Value").Invoke(owner, null));
            Assert.AreEqual(mRowGuid, AssetDatabase.AssetPathToGUID(path));
        }

        /// <summary>给编译后校验与回填有限完成窗口，超时直接失败而非宣称迁移成功。</summary>
        private static IEnumerator WaitForTransactions()
        {
            double deadline = EditorApplication.timeSinceStartup + 15;
            while (UIKitCodeLayoutMigrationTransaction.IsPending || UIKitConversionTransaction.IsPending)
            {
                Assert.Less(EditorApplication.timeSinceStartup, deadline, "事务超时未结束。");
                yield return null;
            }
        }
    }
}
#endif
