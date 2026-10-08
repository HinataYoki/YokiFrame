#if UNITY_EDITOR

using System;
using System.IO;
using NUnit.Framework;
using YokiFrame.Unity;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证依赖宏 planner 和 refresh coordinator 的纯 C# 行为。
    /// </summary>
    public sealed class DependencyDefinePlannerTests
    {
        /// <summary>
        /// 验证 planner 保留非受管宏、移除未检测依赖与废弃 FMOD 宏，并输出稳定去重排序。
        /// </summary>
        [Test]
        public void CreatePlan_混有外部和废弃宏_保留外部宏并稳定排序()
        {
            var snapshot = new DependencyInventorySnapshot(
                new[] { "com.tuyoogame.yooasset" },
                Array.Empty<string>(),
                Array.Empty<string>());
            var planner = new DependencyDefinePlanner();
            var plan = planner.CreatePlan(
                new[]
                {
                    "Z_USER",
                    "YOKIFRAME_UNITASK_SUPPORT",
                    "YOKIFRAME_INPUTSYSTEM_SUPPORT",
                    "YOKIFRAME_FMOD_SUPPORT",
                    "A_USER",
                    "A_USER"
                },
                snapshot);

            CollectionAssert.AreEqual(
                new[] { "A_USER", "YOKIFRAME_YOOASSET_SUPPORT", "Z_USER" },
                plan.DesiredSymbols);
            CollectionAssert.AreEqual(
                new[] { "YOKIFRAME_YOOASSET_SUPPORT" },
                plan.AddedSymbols);
            CollectionAssert.AreEqual(
                new[]
                {
                    "YOKIFRAME_FMOD_SUPPORT",
                    "YOKIFRAME_INPUTSYSTEM_SUPPORT",
                    "YOKIFRAME_UNITASK_SUPPORT"
                },
                plan.RemovedSymbols);
            Assert.IsTrue(plan.Changed);
        }

        /// <summary>
        /// 验证七组固定依赖分别可由 package、真实 asmdef name 或预编译 DLL 文件名命中。
        /// </summary>
        [Test]
        public void CreatePlan_七组依赖证据_只生成对应支持宏()
        {
            AssertSingleDefine(new[] { "com.cysharp.unitask" }, null, null, "YOKIFRAME_UNITASK_SUPPORT");
            AssertSingleDefine(null, new[] { "YooAsset" }, null, "YOKIFRAME_YOOASSET_SUPPORT");
            AssertSingleDefine(null, null, new[] { "Luban.Runtime.dll" }, "YOKIFRAME_LUBAN_SUPPORT");
            AssertSingleDefine(new[] { "com.cysharp.zstring" }, null, null, "YOKIFRAME_ZSTRING_SUPPORT");
            AssertSingleDefine(null, new[] { "DOTween" }, null, "YOKIFRAME_DOTWEEN_SUPPORT");
            AssertSingleDefine(null, null, new[] { "DOTween.dll" }, "YOKIFRAME_DOTWEEN_SUPPORT");
            AssertSingleDefine(new[] { "com.jasonxudeveloper.nino" }, null, null, "YOKIFRAME_NINO_SUPPORT");
            AssertSingleDefine(new[] { "com.unity.inputsystem" }, null, null, "YOKIFRAME_INPUTSYSTEM_SUPPORT");
        }

        /// <summary>
        /// 验证幂等刷新只采集一次 inventory，且目标宏未变化时不调用写入端。
        /// </summary>
        [Test]
        public void Refresh_目标宏未变化_只采集一次且不写入()
        {
            var snapshot = new DependencyInventorySnapshot(
                new[] { "com.cysharp.unitask" },
                Array.Empty<string>(),
                Array.Empty<string>());
            var collectCount = 0;
            var writeCount = 0;

            var coordinator = new DependencyDefineRefreshCoordinator(
                () =>
                {
                    collectCount++;
                    return snapshot;
                },
                () => new[] { "USER_DEFINE", "YOKIFRAME_UNITASK_SUPPORT" },
                _ => writeCount++);
            var result = coordinator.Refresh();

            Assert.AreEqual(1, collectCount);
            Assert.AreEqual(0, writeCount);
            Assert.IsTrue(result.Succeeded);
            Assert.IsFalse(result.Changed);
        }

        /// <summary>
        /// 验证 inventory 采集异常会返回可诊断失败，且不读取或写入 PlayerSettings 宏。
        /// </summary>
        [Test]
        public void Refresh_库存采集失败_不读写宏并返回错误()
        {
            var readCount = 0;
            var writeCount = 0;

            var coordinator = new DependencyDefineRefreshCoordinator(
                () => throw new InvalidOperationException("inventory snapshot failed"),
                () =>
                {
                    readCount++;
                    return new[] { "USER_DEFINE" };
                },
                _ => writeCount++);
            var result = coordinator.Refresh();

            Assert.AreEqual(0, readCount);
            Assert.AreEqual(0, writeCount);
            Assert.IsFalse(result.Succeeded);
            StringAssert.Contains("inventory snapshot failed", result.ErrorMessage);
        }

        /// <summary>
        /// 验证成功刷新会保留本轮 plan、inventory 和采集诊断，供 Unity Console 输出可审计的宏差异与证据。
        /// </summary>
        [Test]
        public void Refresh_刷新成功_保留规划结果和采集诊断()
        {
            var snapshot = new DependencyInventorySnapshot(
                new[] { "com.cysharp.unitask" },
                Array.Empty<string>(),
                Array.Empty<string>());
            var coordinator = new DependencyDefineRefreshCoordinator(
                () => snapshot,
                () => new[] { "USER_DEFINE" },
                _ => { });

            var result = coordinator.Refresh();

            Assert.IsNotNull(result.Plan, "刷新结果必须保留宏规划结果。");
            Assert.AreSame(snapshot, result.Snapshot);
            Assert.IsTrue(result.Plan.Changed);
            CollectionAssert.IsEmpty(result.InventoryDiagnostics);
        }

        /// <summary>
        /// 验证单个无法读取的 asmdef 只返回带路径的诊断，不会向上抛出并中止其它依赖证据采集。
        /// </summary>
        [Test]
        public void TryReadAssemblyDefinitionName_文件不可读_只返回路径诊断()
        {
            bool succeeded = UnityDependencyInventoryProvider.TryReadAssemblyDefinitionName(
                "Assets/ThirdParty/Broken.asmdef",
                () => throw new IOException("file is locked"),
                out string assemblyName,
                out string diagnostic);

            Assert.IsFalse(succeeded);
            Assert.AreEqual(string.Empty, assemblyName);
            StringAssert.Contains("Assets/ThirdParty/Broken.asmdef", diagnostic);
            StringAssert.Contains("file is locked", diagnostic);
        }

        /// <summary>
        /// 验证真实 asmdef JSON 的 name 字段是检测事实，不依赖文件名猜测。
        /// </summary>
        [Test]
        public void TryReadAssemblyDefinitionName_包含name字段_读取程序集名()
        {
            Assert.IsTrue(UnityDependencyInventoryProvider.TryReadAssemblyDefinitionName(
                "{\"name\":\"Company.Real.Assembly\"}",
                out var assemblyName));
            Assert.AreEqual("Company.Real.Assembly", assemblyName);
            Assert.IsFalse(UnityDependencyInventoryProvider.TryReadAssemblyDefinitionName(
                "{\"references\":[]}",
                out assemblyName));
            Assert.AreEqual(string.Empty, assemblyName);
        }

        /// <summary>
        /// 使用单一证据创建 snapshot，并验证 planner 只生成期望宏。
        /// </summary>
        /// <param name="packages">package 证据。</param>
        /// <param name="asmdefs">asmdef name 证据。</param>
        /// <param name="dlls">预编译 DLL 证据。</param>
        /// <param name="expectedDefine">期望唯一宏。</param>
        private static void AssertSingleDefine(
            string[] packages,
            string[] asmdefs,
            string[] dlls,
            string expectedDefine)
        {
            var snapshot = new DependencyInventorySnapshot(
                packages ?? Array.Empty<string>(),
                asmdefs ?? Array.Empty<string>(),
                dlls ?? Array.Empty<string>());
            var planner = new DependencyDefinePlanner();
            var plan = planner.CreatePlan(Array.Empty<string>(), snapshot);

            CollectionAssert.AreEqual(
                new[] { expectedDefine },
                plan.DesiredSymbols);
        }
    }
}

#endif
