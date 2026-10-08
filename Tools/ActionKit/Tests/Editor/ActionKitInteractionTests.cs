using System;
using System.Linq;
using System.Text;
using NUnit.Framework;
using YokiFrame.Json;

namespace YokiFrame.Tests
{
    /// <summary>验证 ActionKit 动态 Interaction、命令契约和诊断 payload 边界。</summary>
    public sealed class ActionKitInteractionTests
    {
        private readonly ActionKitTestLogger mLogger = new();

        /// <summary>每个用例前清理调度、历史与堆栈开关，隔离静态状态。</summary>
        [SetUp]
        public void SetUp()
        {
            ActionKitScheduler.Cleanup();
            ActionStackTraceService.Enabled = false;
            mLogger.Clear();
            LogKit.SetLogger(mLogger);
        }

        /// <summary>每个用例后释放活动动作及诊断引用。</summary>
        [TearDown]
        public void TearDown()
        {
            try
            {
                ActionStackTraceService.Enabled = false;
                ActionKitScheduler.Cleanup();
                mLogger.AssertNoErrors();
            }
            finally { LogKit.ClearLogger(); }
        }

        /// <summary>验证 Tool Provider 在首次初始化后进入 Core 组合，且 catalog 版本与快照一致。</summary>
        [Test]
        public void Init_调度器初始化_注册动态工具提供者()
        {
            ActionKitScheduler.Initialize();

            YokiFrameKitInteractionRegistry registry =
                YokiFrameCoreKitInteractions.CreateDefault(out long capturedRevision);
            IYokiFrameKitInteractionProvider[] providers = registry.Providers
                .Where(static provider => provider.Kit == "ActionKit")
                .ToArray();

            Assert.AreEqual(YokiFrameToolKitInteractionCatalog.Revision, capturedRevision);
            Assert.AreEqual(1, providers.Length);
            CollectionAssert.AreEqual(new[] { "state" }, providers[0].SnapshotNames);
        }

        /// <summary>验证命令风险类型、严格 payload 与清理堆栈的 terminal response。</summary>
        [Test]
        public void Execute_严格命令_声明并执行()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetProvider();
            CollectionAssert.AreEqual(
                new[] { "stats", "get_workbench_snapshot", "set_stack_trace", "clear_stack_trace" },
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

            YokiFrameCommandResult invalid = provider.Handle(CreateRequest(
                "set_stack_trace",
                "{\"enabled\":true,\"extra\":false}"));
            YokiFrameCommandResult enabled = provider.Handle(CreateRequest(
                "set_stack_trace",
                "{\"enabled\":true}"));
            new LongDebugAction().Start();
            YokiFrameCommandResult cleared = provider.Handle(CreateRequest("clear_stack_trace", "{}"));

            Assert.IsFalse(invalid.IsSuccess);
            Assert.AreEqual("InvalidPayload", invalid.ErrorCode);
            Assert.IsTrue(enabled.IsSuccess);
            Assert.IsTrue(cleared.IsSuccess);
            Assert.IsTrue(ActionStackTraceService.Enabled);
            Assert.AreEqual(0, ActionStackTraceService.Count);
        }

        /// <summary>验证超过 JavaScript 安全整数的 Action ID 始终以 JSON 字符串输出。</summary>
        [Test]
        public void Snapshot_大动作ID_序列化为字符串()
        {
            const ulong LARGE_ACTION_ID = ulong.MaxValue - 7UL;
            new LongDebugAction(LARGE_ACTION_ID).Start();

            string json = GetProvider().CreateSnapshot("state");
            string invariantId = LARGE_ACTION_ID.ToString(System.Globalization.CultureInfo.InvariantCulture);
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement actionId = document.RootElement.GetProperty("roots")[0].GetProperty("actionId");
                Assert.AreEqual(JsonValueKind.String, actionId.ValueKind);
                Assert.AreEqual(invariantId, actionId.GetString());
            }
        }

        /// <summary>验证终态历史固定保留最新 64 条，并按最新优先输出。</summary>
        [Test]
        public void History_超过上限_只保留最近六十四个()
        {
            const int EVENT_TOTAL = 70;
            IActionController[] controllers = new IActionController[EVENT_TOTAL];
            for (var index = 0; index < controllers.Length; index++)
            {
                controllers[index] = ActionKit.Callback(static () => { }).Start();
            }

            ActionKitTerminalEvent[] events = ActionKitDiagnosticHistory.CreateLatestSnapshot();

            Assert.AreEqual(ActionKitDiagnosticHistory.MAX_EVENTS, events.Length);
            Assert.AreEqual(EVENT_TOTAL, ActionKitDiagnosticHistory.TotalCount);
            Assert.AreEqual(controllers[EVENT_TOTAL - 1].CurExecuteActionID, events[0].ActionId);
            Assert.AreEqual(controllers[EVENT_TOTAL - events.Length].CurExecuteActionID, events[events.Length - 1].ActionId);
        }

        /// <summary>验证大量多字节诊断文本下的 state 仍不超过 Shared Memory 64 KiB 上限。</summary>
        [Test]
        public void Snapshot_超大树_截断并守住载荷上限()
        {
            const int SAMPLE_TOTAL = 70;
            for (var index = 0; index < SAMPLE_TOTAL; index++)
            {
                new LongDebugAction().Start();
                new LongFaultAction().Start();
            }

            string json = GetProvider().CreateSnapshot("state");
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                Assert.AreEqual(1, ReadInt(root, "schemaVersion"));
                Assert.IsTrue(root.GetProperty("rootsTruncated").GetBoolean());
                Assert.IsTrue(root.GetProperty("eventsTruncated").GetBoolean());
            }
            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
            mLogger.AssertRepeatedErrors(SAMPLE_TOTAL, "[ActionKit] Action ");
        }

        /// <summary>从动态组合后的 Core Registry 取得 ActionKit Provider。</summary>
        private static IYokiFrameVersionedKitInteractionProvider GetProvider()
        {
            ActionKitScheduler.Initialize();
            IYokiFrameKitInteractionProvider provider = YokiFrameCoreKitInteractions.CreateDefault()
                .Providers.First(item => string.Equals(item.Kit, "ActionKit", StringComparison.Ordinal));
            var versioned = provider as IYokiFrameVersionedKitInteractionProvider;
            Assert.IsNotNull(versioned);
            return versioned;
        }

        /// <summary>
        /// 读取必需整数属性。
        /// </summary>
        /// <param name="owner">所属对象。</param>
        /// <param name="propertyName">属性名。</param>
        /// <returns>整数值。</returns>
        private static int ReadInt(JsonElement owner, string propertyName)
        {
            Assert.IsTrue(owner.GetProperty(propertyName).TryGetInt32(out int value), propertyName);
            return value;
        }

        /// <summary>创建使用 UTF-8 payload 长度的 ActionKit 命令请求。</summary>
        private static YokiFrameCommandRequest CreateRequest(string action, string payload)
        {
            return new YokiFrameCommandRequest(
                "actionkit-test",
                "ActionKit",
                action,
                payload,
                1000,
                Encoding.UTF8.GetByteCount(payload));
        }

        /// <summary>保持运行并返回长中文诊断文本，用于覆盖根节点 UTF-8 预算。</summary>
        private sealed class LongDebugAction : ActionBase
        {
            /// <summary>创建自动分配 ID 的长诊断 Action。</summary>
            internal LongDebugAction() { }

            /// <summary>创建使用指定稳定 ID 的长诊断 Action。</summary>
            internal LongDebugAction(ulong actionId)
            {
                ActionID = actionId;
            }

            /// <summary>返回超过运行时单节点上限的多字节诊断文本。</summary>
            public override string GetDebugInfo() => new string('界', 1024);
        }

        /// <summary>在启动阶段抛出长中文异常，用于覆盖终态历史 UTF-8 预算。</summary>
        private sealed class LongFaultAction : ActionBase
        {
            /// <summary>抛出可由 Scheduler 转换为 Faulted 终态的长消息。</summary>
            public override void OnStart()
            {
                throw new InvalidOperationException(new string('错', 1024));
            }
        }
    }
}
