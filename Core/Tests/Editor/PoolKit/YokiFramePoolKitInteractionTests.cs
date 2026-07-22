using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>验证 PoolKit 统一 Interaction、命令风险和有界状态 payload。</summary>
    public sealed class YokiFramePoolKitInteractionTests
    {
        /// <summary>每个测试前清空对象池诊断全局状态。</summary>
        [SetUp]
        public void SetUp()
        {
            PoolKit.Shared.Clear();
            PoolDebugger.Clear();
            PoolDebugger.EnableTracking = false;
            PoolDebugger.EnableEventHistory = false;
            PoolDebugger.EnableStackTrace = false;
        }

        /// <summary>每个测试后恢复诊断开关和状态。</summary>
        [TearDown]
        public void TearDown()
        {
            PoolDebugger.Clear();
            PoolDebugger.EnableTracking = false;
            PoolDebugger.EnableEventHistory = false;
            PoolDebugger.EnableStackTrace = false;
        }

        /// <summary>验证 Provider 声明两个只读与两个显式用户操作命令。</summary>
        [Test]
        public void ProviderDeclaresFixedCommandsAndRiskKinds()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetProvider();

            CollectionAssert.AreEqual(
                new[] { "get_workbench_snapshot", "check_leak", "set_tracking", "clear_history" },
                provider.Commands.Select(static command => command.Action).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    YokiFrameCommandKind.ReadOnly,
                    YokiFrameCommandKind.ReadOnly,
                    YokiFrameCommandKind.UserAction,
                    YokiFrameCommandKind.UserAction
                },
                provider.Commands.Select(static command => command.Kind).ToArray());
            CollectionAssert.AreEqual(new[] { "state" }, provider.SnapshotNames);
        }

        /// <summary>验证 state 包含真实对象池、对象明细、事件和疑似未归还摘要。</summary>
        [Test]
        public void StateSnapshotContainsPoolDetailsEventsAndLeaks()
        {
            PoolDebugger.EnableTracking = true;
            PoolDebugger.EnableEventHistory = true;
            var pool = PoolKit.Create(static () => new PoolToken(), options: new PoolOptions(initialCount: 1, maxRetained: 4));

            PoolToken token = pool.Allocate();
            string json = GetProvider().CreateSnapshot("state");

            StringAssert.StartsWith("{\"schemaVersion\":1", json);
            StringAssert.Contains("\"name\":\"PoolToken\"", json);
            StringAssert.Contains("\"activeCount\":1", json);
            StringAssert.Contains("\"eventType\":\"Spawn\"", json);
            StringAssert.Contains("\"suspectedLeaks\":[{", json);
            Assert.IsTrue(pool.Recycle(token));
        }

        /// <summary>验证定位开关自动启用依赖跟踪，并拒绝字段不完整 payload。</summary>
        [Test]
        public void SetTrackingValidatesCompletePayloadAndEnforcesStackDependencies()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetProvider();

            YokiFrameCommandResult invalid = provider.Handle(CreateRequest("set_tracking", "{\"trackingEnabled\":true}"));
            YokiFrameCommandResult valid = provider.Handle(CreateRequest(
                "set_tracking",
                "{\"trackingEnabled\":false,\"eventHistoryEnabled\":false,\"stackTraceEnabled\":true}"));

            Assert.IsFalse(invalid.IsSuccess);
            Assert.AreEqual("InvalidPayload", invalid.ErrorCode);
            Assert.IsTrue(valid.IsSuccess);
            Assert.IsTrue(PoolDebugger.EnableTracking);
            Assert.IsTrue(PoolDebugger.EnableEventHistory);
            Assert.IsTrue(PoolDebugger.EnableStackTrace);
        }

        /// <summary>验证事件历史不能在关闭跟踪时形成看似开启但永不记录的无效组合。</summary>
        [Test]
        public void SetTrackingDisablesDependentDiagnosticsWhenTrackingIsOff()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetProvider();

            YokiFrameCommandResult result = provider.Handle(CreateRequest(
                "set_tracking",
                "{\"trackingEnabled\":false,\"eventHistoryEnabled\":true,\"stackTraceEnabled\":false}"));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsFalse(PoolDebugger.EnableTracking);
            Assert.IsFalse(PoolDebugger.EnableEventHistory);
            Assert.IsFalse(PoolDebugger.EnableStackTrace);
        }

        /// <summary>验证极端长文本和大量池/对象仍不超过 Shared Memory 默认 payload 上限。</summary>
        [Test]
        public void SnapshotBoundsPoolsObjectsEventsAndUtf8Payload()
        {
            PoolDebugger.EnableTracking = true;
            PoolDebugger.EnableEventHistory = true;
            var pools = new ObjectPool<PoolToken>[30];
            for (var poolIndex = 0; poolIndex < pools.Length; poolIndex++)
            {
                pools[poolIndex] = PoolKit.Create(static () => new PoolToken(), options: new PoolOptions(maxRetained: 16));
                for (var itemIndex = 0; itemIndex < 10; itemIndex++) pools[poolIndex].Allocate();
            }

            string json = GetProvider().CreateSnapshot("state");

            StringAssert.Contains("\"poolTotal\":30", json);
            StringAssert.Contains("\"poolsTruncated\":true", json);
            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
        }

        /// <summary>验证被池列表预算裁剪的活跃池仍会进入全量泄漏摘要并携带稳定池标识。</summary>
        [Test]
        public void LeakSnapshotIncludesActivePoolOutsideVisiblePoolBudget()
        {
            PoolDebugger.EnableTracking = true;
            PoolDebugger.EnableEventHistory = true;
            for (var index = 0; index < 24; index++)
            {
                PoolKit.Create(static () => new AInactivePoolToken());
            }

            var leakPool = PoolKit.Create(static () => new ZLeakingPoolToken());
            leakPool.Allocate();

            string json = GetProvider().CreateSnapshot("state");

            StringAssert.Contains("\"poolsTruncated\":true", json);
            StringAssert.Contains("\"leaks\":{\"suspectedLeaks\":[{\"poolId\":", json);
            StringAssert.Contains("\"total\":1", json);
        }

        /// <summary>验证 capability descriptor 与 Provider 命令面一致。</summary>
        [Test]
        public void CapabilityDescriptorMatchesProviderCatalog()
        {
            string path = Path.Combine(
                Application.dataPath,
                "YokiFrame", "Core", "Editor", "PoolKit", "Capabilities", "capability.json");
            string descriptor = File.ReadAllText(path);

            StringAssert.Contains("\"get_workbench_snapshot\"", descriptor);
            StringAssert.Contains("\"check_leak\"", descriptor);
            StringAssert.Contains("\"set_tracking\"", descriptor);
            StringAssert.Contains("\"clear_history\"", descriptor);
            StringAssert.DoesNotContain("force_return", descriptor);
        }

        /// <summary>从 Core 默认 Registry 获取唯一 PoolKit Provider。</summary>
        private static IYokiFrameVersionedKitInteractionProvider GetProvider()
        {
            IYokiFrameKitInteractionProvider provider = YokiFrameCoreKitInteractions.CreateDefault()
                .Providers.First(item => string.Equals(item.Kit, "PoolKit", StringComparison.Ordinal));
            var versioned = provider as IYokiFrameVersionedKitInteractionProvider;
            Assert.IsNotNull(versioned);
            return versioned;
        }

        /// <summary>创建 PoolKit 命令请求。</summary>
        private static YokiFrameCommandRequest CreateRequest(string action, string payload)
        {
            return new YokiFrameCommandRequest("poolkit-test", "PoolKit", action, payload, 1000, payload.Length);
        }

        /// <summary>提供带长中文显示名的测试对象。</summary>
        private sealed class PoolToken
        {
            /// <summary>返回长文本以验证 UTF-8 payload 裁剪。</summary>
            public override string ToString() => new string('池', 300);
        }

        /// <summary>提供按名称排在前列的无活跃对象池类型。</summary>
        private sealed class AInactivePoolToken
        {
        }

        /// <summary>提供会被旧池列表预算裁剪的活跃对象池类型。</summary>
        private sealed class ZLeakingPoolToken
        {
        }
    }
}
