using System.Linq;
using System.Text;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证 UIKit 只读 Provider 目录、有界 payload 与无 Root 查询行为。
    /// </summary>
    public sealed class UIKitInteractionTests
    {
        /// <summary>
        /// 每条测试前确保 Provider 已幂等注册，并移除可能存在的运行时 Root。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            UIRoot.Dispose();
            UIKitEditorInstaller.EnsureInstalled();
        }

        /// <summary>
        /// 每条测试后释放可能由行为断言创建的 Root。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UIRoot.Dispose();
        }

        /// <summary>
        /// 验证 Catalog 中 UIKit 只有一个 Provider，并区分 Runtime 查询与 Editor UserAction。
        /// </summary>
        [Test]
        public void ProviderDeclaresOnlyConfirmedReadOnlyActions()
        {
            IYokiFrameKitInteractionProvider provider = FindProvider();

            CollectionAssert.AreEqual(new[] { "state" }, provider.SnapshotNames);
            CollectionAssert.AreEqual(
                new[]
                {
                    "stats", "get_workbench_snapshot", "get_editor_context",
                    "create_panel_prefab", "generate_code_for_selection",
                    "add_bind_to_selection", "remove_bind_from_selection"
                },
                provider.Commands.Select(static command => command.Action).ToArray());
            Assert.AreEqual(3, provider.Commands.Count(static command => command.Kind == YokiFrameCommandKind.ReadOnly));
            Assert.AreEqual(4, provider.Commands.Count(static command => command.Kind == YokiFrameCommandKind.UserAction));
        }

        /// <summary>
        /// 验证离线 state 查询返回合法空状态且不会为观察创建 UIRoot。
        /// </summary>
        [Test]
        public void SnapshotWithoutRootDoesNotCreateRoot()
        {
            Assert.IsNull(UIKit.Root);
            string json = FindProvider().CreateSnapshot("state");

            Assert.IsNull(UIKit.Root);
            StringAssert.StartsWith("{\"schemaVersion\":1", json);
            StringAssert.Contains("\"root\":{\"exists\":false}", json);
            StringAssert.Contains("\"panels\":{\"items\":[]", json);
            StringAssert.Contains("\"stacks\":{\"items\":[]", json);
            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
        }

        /// <summary>
        /// 验证只读命令接受空对象并拒绝额外字段，不能借 payload 执行变更。
        /// </summary>
        [Test]
        public void CommandsRequireEmptyPayload()
        {
            IYokiFrameKitInteractionProvider provider = FindProvider();
            YokiFrameCommandResult valid = provider.Handle(CreateRequest("stats", "{}"));
            YokiFrameCommandResult invalid = provider.Handle(CreateRequest("stats", "{\"open\":true}"));

            Assert.IsTrue(valid.IsSuccess);
            Assert.AreEqual("InvalidPayload", invalid.ErrorCode);
            Assert.IsNull(UIKit.Root);
        }

        /// <summary>验证 Editor context 会发布代码模板与可选择的目标程序集目录。</summary>
        [Test]
        public void EditorContextPublishesCodeTemplateAndAssemblyOptions()
        {
            YokiFrameCommandResult result = FindProvider().Handle(
                CreateRequest("get_editor_context", "{}"));

            Assert.IsTrue(result.IsSuccess);
            StringAssert.Contains(
                "\"codeTemplateOptions\":[\"Default\",\"Minimal\"",
                result.ResultJson);
            StringAssert.Contains(
                "\"assemblyNames\":[\"Assembly-CSharp\"",
                result.ResultJson);
            Assert.IsNull(UIKit.Root);
        }

        /// <summary>
        /// 验证 Panel 生成命令只接受精确六字段字符串对象，并在执行资产操作前拒绝协议漂移。
        /// </summary>
        [Test]
        public void PanelGenerationPayloadRequiresExactStringSchema()
        {
            const string valid = "{\"panelName\":\"InventoryPanel\",\"prefabFolder\":\"Assets/UI\","
                + "\"scriptFolder\":\"Assets/Scripts/UI\",\"scriptNamespace\":\"Game.UI\","
                + "\"assemblyName\":\"Game.UI\",\"codeTemplate\":\"Default\"}";
            string[] invalidPayloads =
            {
                valid.Substring(0, valid.Length - 1) + ",\"unexpected\":\"value\"}",
                "{\"panelName\":\"InventoryPanel\",\"prefabFolder\":\"Assets/UI\","
                    + "\"scriptFolder\":\"Assets/Scripts/UI\",\"scriptNamespace\":\"Game.UI\","
                    + "\"assemblyName\":\"Game.UI\"}",
                valid.Substring(0, valid.Length - 1) + ",\"codeTemplate\":\"Minimal\"}",
                valid.Replace("\"InventoryPanel\"", "1"),
                valid.Substring(0, valid.Length - 1) + ",}",
            };

            Assert.DoesNotThrow(() => UIKitPayloadValidator.RequirePanelGenerationRequest(valid));
            for (var index = 0; index < invalidPayloads.Length; index++)
            {
                Assert.Throws<System.ArgumentException>(
                    () => UIKitPayloadValidator.RequirePanelGenerationRequest(invalidPayloads[index]));
            }

            YokiFrameCommandResult result = FindProvider().Handle(
                CreateRequest(UIKitCommandHandler.CREATE_PANEL_PREFAB, invalidPayloads[0]));
            Assert.AreEqual("InvalidPayload", result.ErrorCode);
        }

        /// <summary>
        /// 验证同一 Editor 会话内 Root 创建、销毁和重建持续推进版本，避免状态采样出现 ABA。
        /// </summary>
        [Test]
        public void DiagnosticVersionRemainsMonotonicAcrossRootRecreation()
        {
            IYokiFrameKitInteractionProvider provider = FindProvider();
            long initial = ((IYokiFrameVersionedKitInteractionProvider)provider).StateVersion;

            UIRoot first = UIRoot.Instance;
            long created = ((IYokiFrameVersionedKitInteractionProvider)provider).StateVersion;
            UIRoot.Dispose();
            long disposed = ((IYokiFrameVersionedKitInteractionProvider)provider).StateVersion;
            UIRoot second = UIRoot.Instance;
            long recreated = ((IYokiFrameVersionedKitInteractionProvider)provider).StateVersion;

            Assert.IsNotNull(first);
            Assert.IsNotNull(second);
            Assert.AreNotSame(first, second);
            Assert.Greater(created, initial);
            Assert.Greater(disposed, created,
                "Root 销毁必须推进版本。initial=" + initial + ", created=" + created
                + ", disposed=" + disposed + ", recreated=" + recreated);
            Assert.Greater(recreated, disposed);
        }

        /// <summary>
        /// 从默认 Registry 中定位唯一 UIKit Provider，验证真实 Tool catalog 接入路径。
        /// </summary>
        private static IYokiFrameKitInteractionProvider FindProvider()
        {
            YokiFrameKitInteractionRegistry registry = YokiFrameCoreKitInteractions.CreateDefault();
            IYokiFrameKitInteractionProvider[] providers = registry.Providers
                .Where(static provider => provider.Kit == "UIKit")
                .ToArray();
            Assert.AreEqual(1, providers.Length, "Tool catalog 必须只注册一个 UIKit Provider。");
            return providers[0];
        }

        /// <summary>
        /// 创建带精确 UTF-8 长度的 UIKit 命令请求。
        /// </summary>
        private static YokiFrameCommandRequest CreateRequest(string action, string payload)
        {
            return new YokiFrameCommandRequest(
                "uikit-test",
                "UIKit",
                action,
                payload,
                1000,
                Encoding.UTF8.GetByteCount(payload));
        }
    }
}
