using System;
using System.Collections;
using System.Text;
using NUnit.Framework;
using YokiFrame.Json;

namespace YokiFrame.Tests
{
    /// <summary>覆盖 ActionKit 诊断预算、Provider 契约和少见复用边界。</summary>
    public sealed class ActionKitDiagnosticsTests
    {
        private static readonly string sEscapedDebugText = new('\u0001', 240);

        /// <summary>每个测试前清空调度和诊断静态状态，避免执行顺序影响断言。</summary>
        [SetUp]
        public void SetUp()
        {
            ActionKitScheduler.Cleanup();
            ActionStackTraceService.Enabled = false;
        }

        /// <summary>每个测试后释放动作树、池回收队列和终态文本引用。</summary>
        [TearDown]
        public void TearDown()
        {
            ActionKitScheduler.Cleanup();
            ActionStackTraceService.Enabled = false;
        }

        /// <summary>验证节点超限时先裁剪节点，仍保留调试文本，并且 payload 不超过共享内存上限。</summary>
        [Test]
        public void WriteWorkbench_节点超限_保留调试文本且不超过共享内存()
        {
            ISequence sequence = ActionKit.Sequence();
            for (var index = 0; index < 256; index++)
            {
                sequence.Append(new LongDebugAction());
            }

            sequence.Start();

            string json = ActionKitSnapshotWriter.WriteWorkbench();
            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                Assert.IsTrue(document.RootElement.GetProperty("nodesTruncated").GetBoolean());
                Assert.IsTrue(
                    ContainsDebugCharacter(document.RootElement, '\u0001'),
                    "分级降载应先缩减节点预算，而不是直接丢弃全部诊断文本。");
            }
        }

        /// <summary>验证同一诊断版本的重复读取复用 Editor 快照，避免命令与 Telemetry 重复序列化。</summary>
        [Test]
        public void WorkbenchSnapshotReusesUnchangedDiagnosticVersion()
        {
            string first = ActionKitSnapshotWriter.WriteWorkbench();
            string second = ActionKitSnapshotWriter.WriteWorkbench();

            Assert.AreSame(first, second);

            ActionKit.Delay(10f).Start();
            string changed = ActionKitSnapshotWriter.WriteWorkbench();

            Assert.AreNotSame(first, changed);
        }

        /// <summary>验证后续浅分支不会覆盖先前深分支已经触发的深度裁剪标记。</summary>
        [Test]
        public void WorkbenchSnapshotPreservesDepthTruncationAcrossBranches()
        {
            ISequence deepBranch = CreateNestedSequence(
                ActionKitSnapshotWriter.MAX_DEPTH,
                ActionKit.Delay(100f));
            ISequence boundaryLeafBranch = CreateNestedSequence(ActionKitSnapshotWriter.MAX_DEPTH, null);
            IParallel parallel = ActionKit.Parallel();
            parallel.Append(deepBranch);
            parallel.Append(boundaryLeafBranch);
            parallel.Start();

            string json = ActionKitSnapshotWriter.WriteWorkbench();

            using (JsonDocument document = JsonDocument.Parse(json))
            {
                Assert.IsTrue(document.RootElement.GetProperty("depthTruncated").GetBoolean());
            }
        }

        /// <summary>验证未自行分配 ID 的 ActionBase 子节点在首次启动时获得唯一非零 ID。</summary>
        [Test]
        public void CustomActionBaseChildrenReceiveUniqueIds()
        {
            LongDebugAction first = new();
            LongDebugAction second = new();
            ActionKit.Sequence().Append(first).Append(second).Start();

            Assert.AreNotEqual(0UL, first.ActionID);
            Assert.AreNotEqual(0UL, second.ActionID);
            Assert.AreNotEqual(first.ActionID, second.ActionID);
        }

        /// <summary>验证直接包装的一次性枚举器在 Repeat 后续轮立即完成，而不是调用空 factory 故障。</summary>
        [Test]
        public void DirectEnumeratorRepeatDoesNotFault()
        {
            IRepeat repeat = ActionKit.Repeat(2);
            repeat.Append(EmptyRoutine().ToAction());
            IActionController controller = repeat.Start();

            ActionKitScheduler.Tick(0f, 0f);

            Assert.IsTrue(controller.IsCompleted);
            Assert.IsFalse(controller.IsFaulted);
            Assert.AreEqual(ActionStatus.Finished, repeat.ActionState);
        }

        /// <summary>验证大异常消息在写入固定终态环前已裁剪，避免历史长期保留大字符串。</summary>
        [Test]
        public void TerminalHistoryBoundsRetainedErrorText()
        {
            string message = new string('x', ActionKitDiagnosticHistory.MAX_ERROR_MESSAGE_LENGTH - 1)
                + char.ConvertFromUtf32(0x1F600)
                + new string('y', 4096);
            ActionKitDiagnosticHistory.Record(
                new DiagnosticProbeAction(),
                ActionKitTerminalOutcome.Faulted,
                new InvalidOperationException(message));

            ActionKitTerminalEvent terminalEvent = ActionKitDiagnosticHistory.CreateLatestSnapshot()[0];

            Assert.LessOrEqual(
                terminalEvent.ErrorMessage.Length,
                ActionKitDiagnosticHistory.MAX_ERROR_MESSAGE_LENGTH);
            Assert.IsFalse(char.IsHighSurrogate(terminalEvent.ErrorMessage[^1]));
        }

        /// <summary>验证 Editor Installer 是 ActionKit Provider 进入通用 Tool catalog 的唯一安装入口。</summary>
        [Test]
        public void EditorInstallerPublishesActionKitProvider()
        {
            ActionKitEditorInstaller.EnsureInstalled();
            YokiFrameKitInteractionRegistry registry = YokiFrameCoreKitInteractions.CreateDefault();

            Assert.IsTrue(registry.TryCreateSnapshot("ActionKit", "state", out string json));
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                Assert.AreEqual(1, ReadInt(document.RootElement, "schemaVersion"));
            }
            Assert.AreEqual(4, CountActionKitCommands(registry.GetCommandDescriptors()));
        }

        /// <summary>验证唯一标准 JSON bool 可以切换堆栈状态并返回新 state。</summary>
        [Test]
        public void SetStackTraceAcceptsSingleJsonBoolean()
        {
            ActionKitCommandHandler handler = new();
            YokiFrameCommandResult result = handler.Handle(
                CreateRequest("set_stack_trace", "{\"enabled\":true}"));

            Assert.IsTrue(result.IsSuccess);
            Assert.IsTrue(ActionStackTraceService.Enabled);
            using (JsonDocument document = JsonDocument.Parse(result.ResultJson))
            {
                Assert.IsTrue(document.RootElement.GetProperty("stats").GetProperty("stackTraceEnabled").GetBoolean());
            }
        }

        /// <summary>验证缺失、字符串、重复、额外和嵌套字段都不能绕过唯一 bool 契约。</summary>
        [TestCase("{}")]
        [TestCase("{\"enabled\":\"true\"}")]
        [TestCase("{\"enabled\":true,\"enabled\":false}")]
        [TestCase("{\"enabled\":true,\"extra\":false}")]
        [TestCase("{\"enabled\":{\"value\":true}}")]
        public void SetStackTraceRejectsNonCanonicalPayload(string payload)
        {
            ActionKitCommandHandler handler = new();
            YokiFrameCommandResult result = handler.Handle(CreateRequest("set_stack_trace", payload));

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("InvalidPayload", result.ErrorCode);
            Assert.IsFalse(ActionStackTraceService.Enabled);
        }

        /// <summary>统计当前目录中由 ActionKit Provider 声明的命令数量。</summary>
        /// <param name="commands">通用 Registry 聚合出的命令描述。</param>
        /// <returns>Kit 精确匹配 ActionKit 的数量。</returns>
        private static int CountActionKitCommands(YokiFrameCommandDescriptor[] commands)
        {
            var count = 0;
            for (var index = 0; index < commands.Length; index++)
                if (commands[index].Kit == "ActionKit") count++;
            return count;
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

        /// <summary>创建携带真实 UTF-8 payload 字节数的 ActionKit 测试请求。</summary>
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

        /// <summary>创建首次推进即结束的一次性枚举器。</summary>
        private static IEnumerator EmptyRoutine()
        {
            yield break;
        }

        /// <summary>创建指定容器层数的 Sequence 链，并可在最深层追加叶节点。</summary>
        /// <param name="containerCount">Sequence 容器数量。</param>
        /// <param name="leaf">可选最深层叶节点。</param>
        /// <returns>链顶部 Sequence。</returns>
        private static ISequence CreateNestedSequence(int containerCount, IAction leaf)
        {
            ISequence root = ActionKit.Sequence();
            ISequence current = root;
            for (var index = 1; index < containerCount; index++)
            {
                ISequence child = ActionKit.Sequence();
                current.Append(child);
                current = child;
            }

            if (leaf != null) current.Append(leaf);
            return root;
        }

        /// <summary>
        /// 在已解析的 roots 与 children 中查找仍保留指定字符的 debugInfo。
        /// </summary>
        /// <param name="element">state 根或动作节点。</param>
        /// <param name="character">应被保留的诊断字符。</param>
        /// <returns>任一 debugInfo 包含该字符时返回 true。</returns>
        private static bool ContainsDebugCharacter(JsonElement element, char character)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            JsonElement debugInfo;
            if (element.TryGetProperty("debugInfo", out debugInfo)
                && debugInfo.ValueKind == JsonValueKind.String)
            {
                string debugText = debugInfo.GetString();
                if (debugText != null && debugText.IndexOf(character) >= 0)
                {
                    return true;
                }
            }

            if (ContainsDebugCharacterInArray(element, "roots", character)
                || ContainsDebugCharacterInArray(element, "children", character))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 检查对象上的数组属性，任一元素保留诊断字符即成立。
        /// </summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">数组属性名。</param>
        /// <param name="character">应被保留的诊断字符。</param>
        /// <returns>数组中存在匹配节点时返回 true。</returns>
        private static bool ContainsDebugCharacterInArray(JsonElement element, string propertyName, char character)
        {
            JsonElement items;
            if (!element.TryGetProperty(propertyName, out items) || items.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            int length = items.GetArrayLength();
            for (int index = 0; index < length; index++)
            {
                if (ContainsDebugCharacter(items[index], character))
                {
                    return true;
                }
            }

            return false;
        }
        /// <summary>提供极端转义诊断文本且保持运行的自定义子 Action。</summary>
        private sealed class LongDebugAction : ActionBase
        {
            /// <summary>返回共享长文本，不在每次诊断读取时重新分配字符串。</summary>
            public override string GetDebugInfo() => sEscapedDebugText;
        }

        /// <summary>提供固定非零 ID 的终态历史探针。</summary>
        private sealed class DiagnosticProbeAction : ActionBase
        {
            /// <summary>创建只用于历史记录的探针，不进入 Scheduler。</summary>
            internal DiagnosticProbeAction()
            {
                ActionID = ulong.MaxValue;
            }
        }
    }
}
