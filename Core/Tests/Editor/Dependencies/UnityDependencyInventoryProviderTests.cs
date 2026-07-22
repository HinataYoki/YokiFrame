#if UNITY_EDITOR
using System;
using NUnit.Framework;
using YokiFrame.Unity;

namespace YokiFrame.Tests
{
    /// <summary>覆盖当前项目可选依赖的真实 Unity inventory 证据。</summary>
    public sealed class UnityDependencyInventoryProviderTests
    {
        /// <summary>验证预编译程序集快照保持稳定排序，且不把具体可选包写成测试前置条件。</summary>
        [Test]
        public void CaptureReturnsStablePrecompiledAssemblyNames()
        {
            DependencyInventorySnapshot snapshot = new UnityDependencyInventoryProvider().Capture();

            string[] sorted = (string[])snapshot.PrecompiledAssemblyNames.Clone();
            Array.Sort(sorted, StringComparer.OrdinalIgnoreCase);
            CollectionAssert.AreEqual(sorted, snapshot.PrecompiledAssemblyNames);
        }

        /// <summary>验证 DOTween 宏严格跟随当前 inventory，缺包时必须自动回退而不是让测试失败。</summary>
        [Test]
        public void CurrentProjectPlanMatchesDotweenInventory()
        {
            DependencyInventorySnapshot snapshot = new UnityDependencyInventoryProvider().Capture();
            string[] currentSymbols = new UnityDependencyDefineStore().ReadSymbols();

            DependencyDefinePlan plan = new DependencyDefinePlanner().CreatePlan(
                currentSymbols,
                snapshot);

            bool detected = Array.IndexOf(snapshot.PrecompiledAssemblyNames, "DOTween.dll") >= 0;
            bool enabled = Array.IndexOf(
                plan.DesiredSymbols,
                DependencyDefineCatalog.DOTWEEN_SUPPORT_DEFINE) >= 0;
            Assert.AreEqual(detected, enabled);
        }
    }
}
#endif
