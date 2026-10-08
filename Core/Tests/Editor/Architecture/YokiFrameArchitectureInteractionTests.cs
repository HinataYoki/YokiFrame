using System.Collections.Generic;
using YokiFrame.Json;
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
        public void CreateDefault_默认注册表_声明Architecture快照和只读命令()
        {
            YokiFrameKitInteractionRegistry registry = YokiFrameCoreKitInteractions.CreateDefault();
            IYokiFrameKitInteractionProvider provider = FindArchitectureProvider(registry);

            Assert.IsNotNull(provider);
            Assert.AreEqual(1, provider.SnapshotNames.Count);
            Assert.AreEqual("state", provider.SnapshotNames[0]);
            Assert.AreEqual(2, provider.Commands.Count);
            Assert.IsInstanceOf<IYokiFrameVersionedKitInteractionProvider>(provider);
        }

        /// <summary>验证 state 快照投影真实实例、服务实现和新版统计字段。</summary>
        [Test]
        public void TryCreateSnapshot_已注册架构_state包含实例和服务()
        {
            YokiFrameKitInteractionRegistry registry = YokiFrameCoreKitInteractions.CreateDefault();
            var architectures = new List<ArchitectureDebugInfo>();
            ArchitectureRegistry.GetAll(architectures);
            Assert.AreEqual(1, architectures.Count);
            ArchitectureDebugInfo info = architectures[0];
            Assert.AreEqual(nameof(InteractionArchitecture), info.TypeName);
            Assert.IsTrue(info.Initialized);
            Assert.AreEqual(1, info.ServiceCount);
            Assert.AreEqual(nameof(InteractionService), info.Services[0].ImplementationTypeName);

            Assert.IsTrue(registry.TryCreateSnapshot("Architecture", "state", out string payloadJson));
            using (JsonDocument document = JsonDocument.Parse(payloadJson))
            {
                JsonElement root = document.RootElement;
                JsonElement stats = root.GetProperty("stats");
                JsonElement architecture = root.GetProperty("architectures")[0];
                JsonElement service = architecture.GetProperty("services")[0];
                Assert.AreEqual(1, ReadJsonInt(root, "count"));
                Assert.AreEqual(1, ReadJsonInt(stats, "architectureCount"));
                Assert.AreEqual(info.ServiceCount, ReadJsonInt(stats, "serviceCount"));
                Assert.AreEqual(info.TypeName, architecture.GetProperty("typeName").GetString());
                Assert.AreEqual(info.InstanceHash, ReadJsonInt(architecture, "instanceHash"));
                Assert.AreEqual(info.Initialized, architecture.GetProperty("initialized").GetBoolean());
                Assert.AreEqual(info.ServiceCount, ReadJsonInt(architecture, "serviceCount"));
                Assert.AreEqual(
                    info.Services[0].ImplementationTypeName,
                    service.GetProperty("implementationTypeName").GetString());
            }
        }

        /// <summary>读取 JSON 对象上的 32 位整数属性。</summary>
        /// <param name="element">对象元素。</param>
        /// <param name="propertyName">属性名。</param>
        /// <returns>属性值。</returns>
        private static int ReadJsonInt(JsonElement element, string propertyName)
        {
            Assert.IsTrue(element.GetProperty(propertyName).TryGetInt32(out int value), propertyName);
            return value;
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
