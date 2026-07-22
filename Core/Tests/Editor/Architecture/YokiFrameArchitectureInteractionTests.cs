using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Architecture 通过统一 Kit Interaction 发布真实 Snapshot 与只读命令。
    /// </summary>
    public sealed class YokiFrameArchitectureInteractionTests
    {
        private IArchitecture mArchitecture;

        /// <summary>每个测试前清空注册表并创建一个包含服务的真实 Architecture。</summary>
        [SetUp]
        public void SetUp()
        {
            ArchitectureRegistry.Clear();
            mArchitecture = InteractionArchitecture.Interface;
        }

        /// <summary>每个测试后释放静态 Architecture，避免污染其它测试。</summary>
        [TearDown]
        public void TearDown()
        {
            mArchitecture.Dispose();
            ArchitectureRegistry.Clear();
        }

        /// <summary>验证默认 Core Registry 声明 Architecture Provider 和两个只读命令。</summary>
        [Test]
        public void DefaultRegistryIncludesArchitectureProvider()
        {
            YokiFrameKitInteractionRegistry registry = YokiFrameCoreKitInteractions.CreateDefault();
            IYokiFrameKitInteractionProvider provider = FindArchitectureProvider(registry);

            Assert.IsNotNull(provider);
            Assert.AreEqual(1, provider.SnapshotNames.Count);
            Assert.AreEqual("state", provider.SnapshotNames[0]);
            Assert.AreEqual(2, provider.Commands.Count);
            Assert.IsInstanceOf<IYokiFrameVersionedKitInteractionProvider>(provider);
        }

        /// <summary>验证 Snapshot 包含真实实例、服务实现和新版统计字段。</summary>
        [Test]
        public void SnapshotContainsRegisteredArchitectureAndService()
        {
            YokiFrameKitInteractionRegistry registry = YokiFrameCoreKitInteractions.CreateDefault();

            Assert.IsTrue(registry.TryCreateSnapshot("Architecture", "state", out var payloadJson));
            StringAssert.Contains("\"architectureCount\":1", payloadJson);
            StringAssert.Contains("\"typeName\":\"InteractionArchitecture\"", payloadJson);
            StringAssert.Contains("\"implementationTypeName\":\"InteractionService\"", payloadJson);
            StringAssert.Contains("\"initialized\":true", payloadJson);
        }

        /// <summary>从统一 Registry 中定位 Architecture Provider。</summary>
        private static IYokiFrameKitInteractionProvider FindArchitectureProvider(
            YokiFrameKitInteractionRegistry registry)
        {
            for (var index = 0; index < registry.Providers.Count; index++)
            {
                IYokiFrameKitInteractionProvider provider = registry.Providers[index];
                if (provider.Kit == "Architecture")
                {
                    return provider;
                }
            }

            return null;
        }

        /// <summary>用于验证 Interaction payload 的最小 Architecture。</summary>
        private sealed class InteractionArchitecture : Architecture<InteractionArchitecture>
        {
            /// <summary>注册一个可观察服务。</summary>
            protected override void OnInit()
            {
                Register(new InteractionService());
            }
        }

        /// <summary>用于验证服务契约与实现投影的最小服务。</summary>
        private sealed class InteractionService : AbstractService
        {
            /// <summary>服务初始化无需额外副作用。</summary>
            protected override void OnInit()
            {
            }
        }
    }
}
