using System.Linq;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>验证 AudioKit Provider、严格命令和有界 Workbench payload。</summary>
    public sealed class AudioKitInteractionTests
    {
        private FakeAudioBackend mBackend;

        /// <summary>每个用例安装独立后端并清理门面状态。</summary>
        [SetUp]
        public void SetUp()
        {
            AudioKit.ResetRuntimeDefaults();
            mBackend = new FakeAudioBackend();
            AudioKit.SetBackend(mBackend);
        }

        /// <summary>每个用例后释放测试后端和静态诊断。</summary>
        [TearDown]
        public void TearDown()
        {
            AudioKit.ResetRuntimeDefaults();
        }

        /// <summary>验证 Provider 只声明两个只读观察命令。</summary>
        [Test]
        public void ProviderDeclaresExpectedCommandRiskKinds()
        {
            var provider = new AudioKitInteractionProvider();

            CollectionAssert.AreEqual(
                new[] { "stats", "get_workbench_snapshot" },
                provider.Commands.Select(static command => command.Action).ToArray());
            Assert.IsTrue(provider.Commands.All(static command =>
                command.Kind == YokiFrameCommandKind.ReadOnly));
        }

        /// <summary>验证全部已移除 Runtime 操作无法通过 AudioKit Interaction 重新进入。</summary>
        [Test]
        public void RemovedRuntimeActionsAreRejected()
        {
            var provider = new AudioKitInteractionProvider();
            string[] removedActions =
            {
                "stop_voice", "stop_all", "stop_bus", "set_master_volume",
                "set_bus_volume", "mute_master", "mute_bus", "clear_history"
            };
            for (var index = 0; index < removedActions.Length; index++)
            {
                string action = removedActions[index];
                YokiFrameCommandResult result = provider.Handle(CreateRequest(action, "{}"));

                Assert.IsFalse(result.IsSuccess, action);
                Assert.AreEqual("HandlerMismatch", result.ErrorCode, action);
            }
        }

        /// <summary>验证显式注册的空闲自定义 Bus 可见、大小写去重且可注销。</summary>
        [Test]
        public void CustomBusRegistryPublishesIdleBusAndSupportsRemoval()
        {
            Assert.IsTrue(AudioKit.RegisterBus("DialogueNpc"));
            Assert.IsFalse(AudioKit.RegisterBus("dialoguenpc"));
            List<AudioBusSnapshot> buses = new();

            AudioKit.GetBuses(buses);

            AudioBusSnapshot custom = buses.Single(bus => bus.Name == "DialogueNpc");
            Assert.IsTrue(custom.IsRegistered);
            Assert.IsFalse(custom.IsBuiltIn);
            Assert.AreEqual(0, custom.ActiveVoiceCount);
            Assert.IsTrue(AudioKit.UnregisterBus("DIALOGUENPC"));
            AudioKit.GetBuses(buses);
            Assert.IsFalse(buses.Any(bus => bus.Name == "DialogueNpc"));
        }

        /// <summary>验证注册 API 拒绝空白、控制字符和超过协议预算的名称。</summary>
        [Test]
        public void CustomBusRegistryRejectsInvalidNames()
        {
            Assert.Throws<System.ArgumentException>(() => AudioKit.RegisterBus(" "));
            Assert.Throws<System.ArgumentException>(() => AudioKit.RegisterBus("Dialogue\nNpc"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                AudioKit.RegisterBus(new string('a', 129)));
        }

        /// <summary>验证 Master 聚合全部 active voice，普通 Bus 保留自己的活动数量。</summary>
        [Test]
        public void MasterBusAggregatesAllActiveVoices()
        {
            AudioKit.RegisterBus("DialogueNpc");
            AudioKit.PlayMusic("audio/music");
            AudioPlayOptions options = AudioPlayOptions.Default;
            options.Bus = "DialogueNpc";
            AudioKit.Play("audio/dialogue", options);
            List<AudioBusSnapshot> buses = new();

            AudioKit.GetBuses(buses);

            Assert.AreEqual(2, buses.Single(static bus => bus.IsMaster).ActiveVoiceCount);
            Assert.AreEqual(1, buses.Single(static bus => bus.Name == "Music").ActiveVoiceCount);
            Assert.AreEqual(1, buses.Single(static bus => bus.Name == "DialogueNpc").ActiveVoiceCount);
        }

        /// <summary>验证大量注册 Bus 保留总数与截断证据，不静默冒充完整列表。</summary>
        [Test]
        public void ManyRegisteredBusesExposePayloadCoverage()
        {
            for (var index = 0; index < 80; index++) AudioKit.RegisterBus("Custom" + index);

            string json = new AudioKitInteractionProvider().CreateSnapshot("state");

            StringAssert.Contains("\"busTotal\":86", json);
            StringAssert.Contains("\"busesTruncated\":true", json);
            StringAssert.Contains("\"isRegistered\":true", json);
        }

        /// <summary>验证大量长路径历史下 payload 仍有裁剪证据且不超过共享内存上限。</summary>
        [Test]
        public void WorkbenchSnapshotStaysWithinSharedMemoryLimit()
        {
            string longPath = "audio/" + new string('界', 1024);
            for (var index = 0; index < 160; index++) AudioKit.Play(longPath + index);

            string json = new AudioKitInteractionProvider().CreateSnapshot("state");

            StringAssert.StartsWith("{\"schemaVersion\":1", json);
            StringAssert.EndsWith("}", json);
            StringAssert.Contains("\"historyTruncated\":true", json);
            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
        }

        /// <summary>创建使用准确 UTF-8 payload 长度的 AudioKit 命令请求。</summary>
        private static YokiFrameCommandRequest CreateRequest(string action, string payload)
        {
            return new YokiFrameCommandRequest(
                "audiokit-test", "AudioKit", action, payload, 1000,
                Encoding.UTF8.GetByteCount(payload));
        }
    }
}
