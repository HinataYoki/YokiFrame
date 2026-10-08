using System.Linq;
using System.Text;
using System.Collections.Generic;
using NUnit.Framework;
using YokiFrame.Json;

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
        public void Commands_声明观察命令_全部只读()
        {
            var provider = new AudioKitInteractionProvider();

            CollectionAssert.AreEqual(
                new[] { "stats", "get_workbench_snapshot" },
                provider.Commands.Select(static command => command.Action).ToArray());
            Assert.IsTrue(provider.Commands.All(static command =>
                command.Kind == YokiFrameCommandKind.ReadOnly));
        }

        /// <summary>验证显式注册的空闲自定义 Bus 可见、大小写去重且可注销。</summary>
        [Test]
        public void RegisterBus_自定义总线_可见去重且可注销()
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
        public void RegisterBus_非法名称_抛出参数异常()
        {
            Assert.Throws<System.ArgumentException>(() => AudioKit.RegisterBus(" "));
            Assert.Throws<System.ArgumentException>(() => AudioKit.RegisterBus("Dialogue\nNpc"));
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                AudioKit.RegisterBus(new string('a', 129)));
        }

        /// <summary>验证 Master 聚合全部 active voice，普通 Bus 保留自己的活动数量。</summary>
        [Test]
        public void GetBuses_存在活动voice_Master聚合全部()
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
        public void CreateSnapshot_总线超过预算_保留截断证据()
        {
            for (var index = 0; index < 80; index++) AudioKit.RegisterBus("Custom" + index);

            string json = new AudioKitInteractionProvider().CreateSnapshot("state");
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                Assert.AreEqual(86, ReadInt(root, "busTotal"));
                Assert.IsTrue(root.GetProperty("busesTruncated").GetBoolean());
                Assert.IsTrue(HasRegisteredBus(root.GetProperty("buses")));
            }
        }

        /// <summary>验证后端上报的非法总线名称不进入诊断列表，且公开读取不抛异常。</summary>
        [Test]
        public void GetBuses_后端上报非法名称_不进入诊断()
        {
            AudioKit.SetBackend(new InvalidBusReportingBackend());
            List<AudioBusSnapshot> buses = new();

            Assert.DoesNotThrow(() => AudioKit.GetBuses(buses));

            Assert.IsFalse(buses.Any(static bus => bus.Name == null));
            Assert.IsFalse(buses.Any(static bus => bus.Name.Length > 128));
            Assert.AreEqual(2, buses.Single(static bus => bus.IsMaster).ActiveVoiceCount);
        }

        /// <summary>验证 StopBus 传入 Master 停止全部总线，而不是静默停掉 Sfx。</summary>
        [Test]
        public void StopBus_传入Master_停止全部voice()
        {
            AudioKit.PlaySfx("sfx/click");
            AudioKit.PlayMusic("audio/music");

            AudioKit.StopBus(AudioBus.Master);

            List<AudioVoiceSnapshot> voices = new();
            AudioKit.GetActiveVoices(voices);
            Assert.IsEmpty(voices);
        }

        /// <summary>验证主动清空历史后不再把会话累计数误报为队列裁剪。</summary>
        [Test]
        public void ClearHistory_主动清空_不再报告历史截断()
        {
            for (var index = 0; index < 8; index++) AudioKit.Play("audio/click" + index);

            AudioKit.ClearHistory();
            string json = new AudioKitInteractionProvider().CreateSnapshot("state");
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                Assert.IsFalse(document.RootElement.GetProperty("historyTruncated").GetBoolean());
            }
        }

        /// <summary>验证大量长路径历史下 payload 仍有裁剪证据且不超过共享内存上限。</summary>
        [Test]
        public void CreateSnapshot_超长历史_裁剪且不超过共享内存()
        {
            string longPath = "audio/" + new string('界', 1024);
            for (var index = 0; index < 160; index++) AudioKit.Play(longPath + index);

            string json = new AudioKitInteractionProvider().CreateSnapshot("state");
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement root = document.RootElement;
                Assert.AreEqual(1, ReadInt(root, "schemaVersion"));
                Assert.IsTrue(root.GetProperty("historyTruncated").GetBoolean());
            }
            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
        }

        /// <summary>
        /// 判断可见总线中是否存在已注册项。
        /// </summary>
        /// <param name="buses">总线数组。</param>
        /// <returns>存在已注册总线时返回 true。</returns>
        private static bool HasRegisteredBus(JsonElement buses)
        {
            for (var index = 0; index < buses.GetArrayLength(); index++)
            {
                if (buses[index].GetProperty("isRegistered").GetBoolean())
                {
                    return true;
                }
            }

            return false;
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

        /// <summary>回填非法 Bus 名称的第三方后端替身，用于固定诊断入列净化。</summary>
        private sealed class InvalidBusReportingBackend : FakeAudioBackend
        {
            /// <summary>上报一个空 Bus 与一个超长 Bus，模拟未遵守约定的自定义后端。</summary>
            public override void GetActiveVoices(List<AudioVoiceSnapshot> result)
            {
                result.Clear();
                result.Add(new AudioVoiceSnapshot { VoiceId = 1, Path = "audio/null-bus", Bus = null });
                result.Add(new AudioVoiceSnapshot
                {
                    VoiceId = 2,
                    Path = "audio/long-bus",
                    Bus = new string('a', 200)
                });
            }
        }
    }
}
