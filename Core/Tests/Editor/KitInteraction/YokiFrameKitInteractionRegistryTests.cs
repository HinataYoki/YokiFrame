using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证所有 Kit 共用的交互 Registry 能组合 Provider、Snapshot 和 Command，而不依赖 FsmKit 专用分支。
    /// </summary>
    public sealed class YokiFrameKitInteractionRegistryTests
    {
        /// <summary>验证 Registry 可以路由任意 Kit 的 Snapshot 与只读 Command。</summary>
        [Test]
        public void RegistryRoutesProviderSnapshotAndCommand()
        {
            YokiFrameKitInteractionRegistry registry = new YokiFrameKitInteractionRegistry();
            registry.Register(new TestKitInteractionProvider("ExampleKit"));

            bool snapshotCreated = registry.TryCreateSnapshot("ExampleKit", "state", out string payloadJson);
            YokiFrameCommandResult result = registry.Handle(CreateRequest("ExampleKit", "inspect"));

            Assert.IsTrue(snapshotCreated);
            Assert.AreEqual("{\"kit\":\"ExampleKit\"}", payloadJson);
            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual("{\"action\":\"inspect\"}", result.ResultJson);
        }

        /// <summary>验证重复 Kit 会在组合阶段被拒绝，避免不同 Provider 竞争相同命令和状态事实。</summary>
        [Test]
        public void RegistryRejectsDuplicateKit()
        {
            YokiFrameKitInteractionRegistry registry = new YokiFrameKitInteractionRegistry();
            registry.Register(new TestKitInteractionProvider("ExampleKit"));

            ArgumentException exception = Assert.Throws<ArgumentException>(() =>
                registry.Register(new TestKitInteractionProvider("ExampleKit")));

            StringAssert.Contains("ExampleKit", exception.Message);
        }

        /// <summary>验证 Core 六个 Provider 顺序固定，已注册 Tool Provider 只追加在其后。</summary>
        [Test]
        public void DefaultCompositionStartsWithCoreProvidersAndAppendsTools()
        {
            YokiFrameKitInteractionRegistry registry =
                YokiFrameCoreKitInteractions.CreateDefault(out long toolProviderRevision);

            Assert.GreaterOrEqual(registry.Providers.Count, 6);
            Assert.AreEqual(YokiFrameToolKitInteractionCatalog.Revision, toolProviderRevision);
            CollectionAssert.AreEqual(
                new[] { "Architecture", "EventKit", "FsmKit", "LogKit", "PoolKit", "ResKit" },
                new[]
                {
                    registry.Providers[0].Kit,
                    registry.Providers[1].Kit,
                    registry.Providers[2].Kit,
                    registry.Providers[3].Kit,
                    registry.Providers[4].Kit,
                    registry.Providers[5].Kit
                });
            CollectionAssert.AreEqual(new[] { "state" }, registry.Providers[1].SnapshotNames);
            CollectionAssert.AreEqual(new[] { "state" }, registry.Providers[2].SnapshotNames);
            CollectionAssert.AreEqual(new[] { "state" }, registry.Providers[3].SnapshotNames);
            CollectionAssert.AreEqual(new[] { "state" }, registry.Providers[4].SnapshotNames);
            CollectionAssert.AreEqual(new[] { "state" }, registry.Providers[5].SnapshotNames);
        }

        /// <summary>验证默认策略不硬编码业务 Kit，命令必须由当前 Registry 显式追加。</summary>
        [Test]
        public void CommandPolicyReceivesKitCommandsFromRegistry()
        {
            YokiFrameCommandPolicy defaultPolicy = YokiFrameCommandPolicy.CreateDefault();
            YokiFrameKitInteractionRegistry registry = YokiFrameCoreKitInteractions.CreateDefault();
            YokiFrameCommandPolicy composedPolicy = YokiFrameCommandPolicy.CreateDefault(
                registry.GetCommandDescriptors());

            Assert.IsFalse(defaultPolicy.Evaluate(CreatePolicyRequest("FsmKit", "list_all")).IsAllowed);
            Assert.IsFalse(defaultPolicy.Evaluate(CreatePolicyRequest("EventKit", "get_workbench_snapshot")).IsAllowed);
            Assert.IsTrue(composedPolicy.Evaluate(CreatePolicyRequest("FsmKit", "list_all")).IsAllowed);
            Assert.IsTrue(composedPolicy.Evaluate(CreatePolicyRequest("EventKit", "get_workbench_snapshot")).IsAllowed);
            Assert.IsTrue(composedPolicy.Evaluate(CreatePolicyRequest("LogKit", "set_settings")).IsAllowed);
            Assert.IsTrue(composedPolicy.Evaluate(CreatePolicyRequest("PoolKit", "set_tracking")).IsAllowed);
            Assert.IsTrue(composedPolicy.Evaluate(CreatePolicyRequest("ResKit", "diagnose_resource")).IsAllowed);
        }

        /// <summary>创建 Runtime dispatcher 使用的最小命令请求。</summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="action">action 标识。</param>
        /// <returns>满足协议边界的只读命令请求。</returns>
        private static YokiFrameCommandRequest CreateRequest(string kit, string action)
        {
            return new YokiFrameCommandRequest("cli", kit, action, "{}", 1000, 64);
        }

        /// <summary>创建 CommandPolicy 使用的最小命令摘要。</summary>
        /// <param name="kit">Kit 标识。</param>
        /// <param name="action">action 标识。</param>
        /// <returns>满足大小与超时约束的策略请求。</returns>
        private static YokiFrameCommandPolicyRequest CreatePolicyRequest(string kit, string action)
        {
            return new YokiFrameCommandPolicyRequest("cli", kit, action, "{}", 1000, 64);
        }

        /// <summary>提供与具体业务无关的测试 Kit，用于证明 Registry 不依赖 FsmKit 类型。</summary>
        private sealed class TestKitInteractionProvider : IYokiFrameKitInteractionProvider
        {
            private static readonly IReadOnlyList<string> sSnapshotNames = Array.AsReadOnly(new[] { "state" });
            private static readonly IReadOnlyList<YokiFrameCommandDescriptor> sCommands = Array.AsReadOnly(
                new[] { new YokiFrameCommandDescriptor("ExampleKit", "inspect", YokiFrameCommandKind.ReadOnly) });

            /// <summary>创建指定名称的测试 Provider。</summary>
            /// <param name="kit">Provider 负责的 Kit 标识。</param>
            public TestKitInteractionProvider(string kit)
            {
                Kit = kit;
            }

            /// <summary>获取测试 Kit 标识。</summary>
            public string Kit { get; }

            /// <summary>获取测试 Snapshot 清单。</summary>
            public IReadOnlyList<string> SnapshotNames => sSnapshotNames;

            /// <summary>获取测试 Command 清单。</summary>
            public IReadOnlyList<YokiFrameCommandDescriptor> Commands => sCommands;

            /// <summary>判断当前测试 Provider 是否处理指定命令。</summary>
            /// <param name="request">命令请求。</param>
            /// <returns>Kit/action 同时匹配时返回 true。</returns>
            public bool CanHandle(YokiFrameCommandRequest request)
            {
                return request != null
                    && string.Equals(request.Kit, Kit, StringComparison.Ordinal)
                    && string.Equals(request.Action, "inspect", StringComparison.Ordinal);
            }

            /// <summary>返回稳定测试命令结果。</summary>
            /// <param name="request">已匹配命令请求。</param>
            /// <returns>包含 action 的成功结果。</returns>
            public YokiFrameCommandResult Handle(YokiFrameCommandRequest request)
            {
                return CanHandle(request)
                    ? YokiFrameCommandResult.Success("{\"action\":\"inspect\"}")
                    : YokiFrameCommandResult.Error("HandlerMismatch", "Unsupported test command.");
            }

            /// <summary>创建稳定测试 Snapshot。</summary>
            /// <param name="snapshotName">Snapshot 名称。</param>
            /// <returns>包含 Kit 标识的 JSON。</returns>
            public string CreateSnapshot(string snapshotName)
            {
                if (!string.Equals(snapshotName, "state", StringComparison.Ordinal))
                {
                    throw new ArgumentException("Unsupported snapshot: " + snapshotName, nameof(snapshotName));
                }

                return "{\"kit\":\"" + Kit + "\"}";
            }
        }
    }
}
