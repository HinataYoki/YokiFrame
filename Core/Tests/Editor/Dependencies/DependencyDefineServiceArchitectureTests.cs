#if UNITY_EDITOR

using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证 Unity Editor DependencyDefineService 的目录、事件调度和单次 snapshot 采集约束。
    /// </summary>
    public sealed class DependencyDefineServiceArchitectureTests
    {
        private const string DEPENDENCIES_RELATIVE_ROOT =
            "YokiFrame/Core/Adapters/Unity/Editor/Dependencies";

        /// <summary>
        /// 验证服务使用 package/asset/compilation 事件和 delayCall 延后，不包含 busy-wait。
        /// </summary>
        [Test]
        public void ServiceUsesEventsAndDelayCallWithoutBusyWait()
        {
            var servicePath = Path.Combine(GetDependenciesRoot(), "DependencyDefineService.cs");
            Assert.IsTrue(File.Exists(servicePath), "缺少 DependencyDefineService: " + servicePath);
            var source = File.ReadAllText(servicePath);

            StringAssert.Contains("Events.registeredPackages", source);
            StringAssert.Contains("CompilationPipeline.compilationFinished", source);
            StringAssert.Contains("EditorApplication.delayCall", source);
            StringAssert.Contains("EditorApplication.isCompiling", source);
            StringAssert.Contains("EditorApplication.isUpdating", source);
            StringAssert.DoesNotContain("while (", source);
            StringAssert.DoesNotContain("Client.List", source);
            StringAssert.DoesNotContain("Thread.Sleep", source);
        }

        /// <summary>
        /// 验证 inventory provider 每次采集只调用一次 package、asmdef 和预编译 DLL 快照入口。
        /// </summary>
        [Test]
        public void InventoryProviderCapturesEachUnityEvidenceSourceOnce()
        {
            var providerPath = Path.Combine(
                GetDependenciesRoot(),
                "Inventory",
                "UnityDependencyInventoryProvider.cs");
            Assert.IsTrue(File.Exists(providerPath), "缺少 Unity inventory provider: " + providerPath);
            var source = File.ReadAllText(providerPath);

            Assert.AreEqual(1, CountOccurrences(source, "PackageInfo.GetAllRegisteredPackages"));
            Assert.AreEqual(1, CountOccurrences(source, "AssetDatabase.FindAssets"));
            Assert.AreEqual(1, CountOccurrences(source, "CompilationPipeline.GetPrecompiledAssemblyPaths"));
        }

        /// <summary>
        /// 获取 Dependencies 生产目录绝对路径。
        /// </summary>
        /// <returns>生产目录。</returns>
        private static string GetDependenciesRoot()
        {
            return Path.Combine(
                Application.dataPath,
                DEPENDENCIES_RELATIVE_ROOT.Replace('/', Path.DirectorySeparatorChar));
        }

        /// <summary>
        /// 统计稳定 API 片段在源码中的出现次数，锁定一次刷新只采集一次快照。
        /// </summary>
        /// <param name="source">生产源码。</param>
        /// <param name="value">待统计片段。</param>
        /// <returns>出现次数。</returns>
        private static int CountOccurrences(string source, string value)
        {
            var count = 0;
            var startIndex = 0;
            while (startIndex < source.Length)
            {
                var index = source.IndexOf(value, startIndex, StringComparison.Ordinal);
                if (index < 0)
                {
                    break;
                }

                count++;
                startIndex = index + value.Length;
            }

            return count;
        }
    }
}

#endif
