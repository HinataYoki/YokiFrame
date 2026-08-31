#if UNITY_EDITOR

using System;
using System.IO;
using System.Reflection;
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
        public void PlannerPreservesExternalSymbolsAndProducesStableManagedSet()
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
        public void PlannerDetectsExactlySevenSupportedDependencyDefinesFromSnapshotEvidence()
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
        public void CoordinatorCollectsOnceAndSkipsIdempotentWrite()
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
        public void CoordinatorDoesNotModifySymbolsWhenInventoryCollectionFails()
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
        public void CoordinatorRetainsPlanSnapshotAndInventoryDiagnostics()
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

            var resultType = result.GetType();
            var planProperty = resultType.GetProperty("Plan", BindingFlags.Instance | BindingFlags.Public);
            var snapshotProperty = resultType.GetProperty("Snapshot", BindingFlags.Instance | BindingFlags.Public);
            var diagnosticsProperty = resultType.GetProperty(
                "InventoryDiagnostics",
                BindingFlags.Instance | BindingFlags.Public);

            Assert.IsNotNull(planProperty, "刷新结果必须保留宏规划结果。");
            Assert.IsNotNull(snapshotProperty, "刷新结果必须保留本轮 inventory 快照。");
            Assert.IsNotNull(diagnosticsProperty, "刷新结果必须保留 inventory 诊断。");
            Assert.AreSame(snapshot, snapshotProperty.GetValue(result));
            Assert.IsTrue(((DependencyDefinePlan)planProperty.GetValue(result)).Changed);
            CollectionAssert.IsEmpty((string[])diagnosticsProperty.GetValue(result));
        }

        /// <summary>
        /// 验证单个无法读取的 asmdef 只返回带路径的诊断，不会向上抛出并中止其它依赖证据采集。
        /// </summary>
        [Test]
        public void InventoryParserIsolatesUnreadableAssemblyDefinition()
        {
            var method = typeof(UnityDependencyInventoryProvider).GetMethod(
                "TryReadAssemblyDefinitionName",
                BindingFlags.Static | BindingFlags.Public,
                null,
                new[]
                {
                    typeof(string),
                    typeof(Func<string>),
                    typeof(string).MakeByRefType(),
                    typeof(string).MakeByRefType()
                },
                null);
            Assert.IsNotNull(method, "缺少带路径和读取回调的 asmdef 容错解析入口。");
            object[] arguments =
            {
                "Assets/ThirdParty/Broken.asmdef",
                (Func<string>)(() => throw new IOException("file is locked")),
                null,
                null
            };

            var succeeded = (bool)method.Invoke(null, arguments);

            Assert.IsFalse(succeeded);
            Assert.AreEqual(string.Empty, arguments[2]);
            StringAssert.Contains("Assets/ThirdParty/Broken.asmdef", (string)arguments[3]);
            StringAssert.Contains("file is locked", (string)arguments[3]);
        }

        /// <summary>
        /// 验证真实 asmdef JSON 的 name 字段是检测事实，不依赖文件名猜测。
        /// </summary>
        [Test]
        public void InventoryParserReadsAssemblyDefinitionNameFromJson()
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
