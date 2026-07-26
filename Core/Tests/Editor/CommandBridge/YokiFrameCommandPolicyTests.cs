using NUnit.Framework;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Runtime CommandPolicy 的危险命令二次确认规则。
    /// </summary>
    public sealed class YokiFrameCommandPolicyTests
    {
        /// <summary>
        /// 验证默认策略允许用户显式触发的打开项目目录命令。
        /// </summary>
        [Test]
        public void EvaluateAllowsOpenProjectFolderAsUserAction()
        {
            YokiFrameCommandPolicyDecision decision = YokiFrameCommandPolicy.CreateDefault().Evaluate(CreateSystemRequest("open_project_folder"));

            Assert.IsTrue(decision.IsAllowed);
            Assert.AreEqual(YokiFrameCommandKind.UserAction, decision.Kind);
        }

        /// <summary>
        /// 验证默认策略允许用户显式触发的打开 Editor 日志命令。
        /// </summary>
        [Test]
        public void EvaluateAllowsOpenLogAsUserAction()
        {
            YokiFrameCommandPolicyDecision decision = YokiFrameCommandPolicy.CreateDefault().Evaluate(CreateSystemRequest("open_log"));

            Assert.IsTrue(decision.IsAllowed);
            Assert.AreEqual(YokiFrameCommandKind.UserAction, decision.Kind);
        }

        /// <summary>
        /// 验证默认策略允许 Workbench 读取当前宿主命令目录，用于动态生成快捷命令面板。
        /// </summary>
        [Test]
        public void EvaluateAllowsListCommandsAsReadOnly()
        {
            YokiFrameCommandPolicyDecision decision = YokiFrameCommandPolicy.CreateDefault().Evaluate(CreateSystemRequest("list_commands"));

            Assert.IsTrue(decision.IsAllowed);
            Assert.AreEqual(YokiFrameCommandKind.ReadOnly, decision.Kind);
        }

        /// <summary>
        /// 验证产品中立的外部自动化来源可以访问已登记的只读命令。
        /// </summary>
        [Test]
        public void EvaluateAllowsExternalAutomationSource()
        {
            YokiFrameCommandPolicyDecision decision = YokiFrameCommandPolicy.CreateDefault().Evaluate(
                CreateSystemRequest(
                    "list_commands",
                    YokiFrameCommandSourceContract.EXTERNAL_AUTOMATION));

            Assert.IsTrue(decision.IsAllowed);
            Assert.AreEqual(YokiFrameCommandKind.ReadOnly, decision.Kind);
        }

        /// <summary>
        /// 验证旧的供应商专属来源标识不再被默认策略接受，避免协议继续耦合历史工具名称。
        /// </summary>
        [Test]
        public void EvaluateRejectsUnregisteredAutomationSource()
        {
            YokiFrameCommandPolicyDecision decision = YokiFrameCommandPolicy.CreateDefault().Evaluate(
                CreateSystemRequest("list_commands", "legacy-automation"));

            Assert.IsFalse(decision.IsAllowed);
            Assert.AreEqual("PolicyRejected", decision.ErrorCode);
        }

        /// <summary>
        /// 验证任意未登记的 internal-like 来源仍被拒绝，避免 allowlist 退化为前缀匹配。
        /// </summary>
        [Test]
        public void EvaluateRejectsUnregisteredInternalSource()
        {
            YokiFrameCommandPolicyDecision decision = YokiFrameCommandPolicy.CreateDefault().Evaluate(
                new YokiFrameCommandPolicyRequest(
                    "workflow-unregistered",
                    "System",
                    "list_commands",
                    "{}",
                    YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                    0L));

            Assert.IsFalse(decision.IsAllowed);
            Assert.AreEqual("PolicyRejected", decision.ErrorCode);
        }

        /// <summary>
        /// 验证 Registry 追加后完整允许 FsmKit 的五个只读诊断 action，默认策略不再认识具体 Kit。
        /// </summary>
        /// <param name="action">待验证的 FsmKit action。</param>
        [TestCase("list_all")]
        [TestCase("get_state")]
        [TestCase("get_history")]
        [TestCase("get_state_events")]
        [TestCase("get_workbench_snapshot")]
        public void EvaluateAllowsRegisteredFsmKitDiagnosticActionsAsReadOnly(string action)
        {
            YokiFrameKitInteractionRegistry registry = YokiFrameCoreKitInteractions.CreateDefault();
            YokiFrameCommandPolicy policy = YokiFrameCommandPolicy.CreateDefault(registry.GetCommandDescriptors());
            YokiFrameCommandPolicyDecision decision = policy.Evaluate(CreateFsmKitRequest(action));

            Assert.IsTrue(decision.IsAllowed);
            Assert.AreEqual(YokiFrameCommandKind.ReadOnly, decision.Kind);
        }

        /// <summary>
        /// 验证跨宿主默认策略不声明 Unity-only Validation 命令，避免 Godot catalog 出现无 handler action。
        /// </summary>
        [Test]
        public void EvaluateDefaultPolicyDoesNotAdvertiseUnityValidationCommands()
        {
            YokiFrameCommandPolicyDecision decision = YokiFrameCommandPolicy.CreateDefault().Evaluate(
                CreateHarnessRequest("Validation", "inspect_status"));

            Assert.IsFalse(decision.IsAllowed);
            Assert.AreEqual("UnknownCommand", decision.ErrorCode);
        }

        /// <summary>
        /// 验证 Unity Editor 追加的两个诊断命令全部按 ReadOnly 风险等级放行。
        /// </summary>
        /// <param name="kit">目标 Kit。</param>
        /// <param name="action">目标 action。</param>
        [TestCase("Validation", "inspect_status")]
        [TestCase("Validation", "get_console_errors")]
        public void EvaluateUnityExtendedPolicyAllowsValidationCommandsAsReadOnly(string kit, string action)
        {
            YokiFrameCommandPolicy policy = YokiFrameCommandPolicy.CreateDefault(
                YokiFrameUnityHarnessObservationCommandHandler.CreateCommandDescriptors());
            YokiFrameCommandPolicyDecision decision = policy.Evaluate(CreateHarnessRequest(kit, action));

            Assert.IsTrue(decision.IsAllowed);
            Assert.AreEqual(YokiFrameCommandKind.ReadOnly, decision.Kind);
        }

        /// <summary>
        /// 验证危险命令缺少 confirmed 布尔值时会被拒绝，避免误触发破坏性操作。
        /// </summary>
        [Test]
        public void EvaluateRejectsDangerousCommandWithoutConfirmedPayload()
        {
            YokiFrameCommandPolicyDecision decision = CreateDangerousPolicy().Evaluate(CreateRequest("{}"));

            Assert.IsFalse(decision.IsAllowed);
            Assert.AreEqual("ConfirmationRequired", decision.ErrorCode);
        }

        /// <summary>
        /// 验证 confirmed 必须是 JSON 布尔 true，字符串形式不能绕过确认。
        /// </summary>
        [Test]
        public void EvaluateRejectsDangerousCommandWithStringConfirmedPayload()
        {
            YokiFrameCommandPolicyDecision decision = CreateDangerousPolicy().Evaluate(CreateRequest("{\"confirmed\":\"true\"}"));

            Assert.IsFalse(decision.IsAllowed);
            Assert.AreEqual("ConfirmationRequired", decision.ErrorCode);
        }

        /// <summary>
        /// 验证危险命令显式携带 payload.confirmed=true 时允许进入后续 handler。
        /// </summary>
        [Test]
        public void EvaluateAllowsDangerousCommandWithBooleanConfirmedPayload()
        {
            YokiFrameCommandPolicyDecision decision = CreateDangerousPolicy().Evaluate(CreateRequest("{\"confirmed\":true}"));

            Assert.IsTrue(decision.IsAllowed);
            Assert.AreEqual(YokiFrameCommandKind.Dangerous, decision.Kind);
        }

        /// <summary>
        /// 验证嵌套对象与字符串值中的 confirmed 文本不能替代顶层确认，避免共享扫描器回退为全文匹配。
        /// </summary>
        [TestCase("{\"filter\":{\"confirmed\":true}}")]
        [TestCase("{\"note\":\"confirmed\":true}")]
        [TestCase("{\"items\":[{\"confirmed\":true}]}")]
        public void EvaluateRejectsDangerousCommandWithNonTopLevelConfirmedPayload(string payloadJson)
        {
            YokiFrameCommandPolicyDecision decision = CreateDangerousPolicy().Evaluate(CreateRequest(payloadJson));

            Assert.IsFalse(decision.IsAllowed);
            Assert.AreEqual("ConfirmationRequired", decision.ErrorCode);
        }

        /// <summary>
        /// 验证顶层 confirmed 出现在其它字段之后仍能被正确识别。
        /// </summary>
        [Test]
        public void EvaluateAllowsDangerousCommandWithTrailingConfirmedField()
        {
            YokiFrameCommandPolicyDecision decision = CreateDangerousPolicy().Evaluate(
                CreateRequest("{\"filter\":{\"path\":\"a\"},\"confirmed\":true}"));

            Assert.IsTrue(decision.IsAllowed);
            Assert.AreEqual(YokiFrameCommandKind.Dangerous, decision.Kind);
        }

        /// <summary>
        /// 创建只允许一个危险命令的测试策略。
        /// </summary>
        /// <returns>危险命令测试策略。</returns>
        private static YokiFrameCommandPolicy CreateDangerousPolicy()
        {
            return new YokiFrameCommandPolicy(
                new[] { "cli" },
                new[] { new YokiFrameCommandDescriptor("System", "delete_cache", YokiFrameCommandKind.Dangerous) });
        }

        /// <summary>
        /// 创建危险命令测试请求。
        /// </summary>
        /// <param name="payloadJson">payload JSON 文本。</param>
        /// <returns>策略评估请求。</returns>
        private static YokiFrameCommandPolicyRequest CreateRequest(string payloadJson)
        {
            return new YokiFrameCommandPolicyRequest(
                "cli",
                "System",
                "delete_cache",
                payloadJson,
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L);
        }

        /// <summary>
        /// 创建默认 System 命令策略请求。
        /// </summary>
        /// <param name="action">System action 标识。</param>
        /// <returns>策略评估请求。</returns>
        private static YokiFrameCommandPolicyRequest CreateSystemRequest(
            string action,
            string source = "cli")
        {
            return new YokiFrameCommandPolicyRequest(
                source,
                "System",
                action,
                "{}",
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L);
        }

        /// <summary>
        /// 创建默认 FsmKit 命令策略请求。
        /// </summary>
        /// <param name="action">FsmKit action 标识。</param>
        /// <returns>策略评估请求。</returns>
        private static YokiFrameCommandPolicyRequest CreateFsmKitRequest(string action)
        {
            return new YokiFrameCommandPolicyRequest(
                "cli",
                "FsmKit",
                action,
                "{}",
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L);
        }

        /// <summary>创建 Harness 或 Scene 的默认只读策略请求。</summary>
        /// <param name="kit">目标 Kit。</param>
        /// <param name="action">目标 action。</param>
        /// <returns>策略评估请求。</returns>
        private static YokiFrameCommandPolicyRequest CreateHarnessRequest(string kit, string action)
        {
            return new YokiFrameCommandPolicyRequest(
                "cli",
                kit,
                action,
                "{}",
                YokiFrameCommandPolicy.COMMAND_TIMEOUT_MIN_MS,
                0L);
        }
    }
}
