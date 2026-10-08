using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>验证 LogKit 统一 Interaction、固定 state 和高频快照体积边界。</summary>
    public sealed class YokiFrameLogKitInteractionTests
    {
        private const int SNAPSHOT_HISTORY_LIMIT = 32;
        private const int MESSAGE_MAX_BYTES = 220;

        /// <summary>每个测试前恢复 Core 全局状态，避免设置和历史相互污染。</summary>
        [SetUp]
        public void SetUp()
        {
            KitSettings.Reset();
            LogKit.Reset();
            LogKitSettings.ResetToDefaults();
            LogKitHostEnvironment.Reset();
        }

        /// <summary>每个测试后清除宿主环境和日志历史。</summary>
        [TearDown]
        public void TearDown()
        {
            LogKitHostEnvironment.Reset();
            LogKit.Reset();
            KitSettings.Reset();
        }

        /// <summary>验证默认 Provider 只声明固定的只读命令、用户操作和 state 快照。</summary>
        [Test]
        public void Commands_默认Provider_声明固定命令和风险()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetProvider();

            CollectionAssert.AreEqual(
                new[]
                {
                    "get_workbench_snapshot",
                    "read_log_file",
                    "set_settings",
                    "reset_settings",
                    "clear_history"
                },
                provider.Commands.Select(command => command.Action).ToArray());
            CollectionAssert.AreEqual(
                new[]
                {
                    YokiFrameCommandKind.ReadOnly,
                    YokiFrameCommandKind.ReadOnly,
                    YokiFrameCommandKind.UserAction,
                    YokiFrameCommandKind.UserAction,
                    YokiFrameCommandKind.UserAction
                },
                provider.Commands.Select(command => command.Kind).ToArray());
            CollectionAssert.AreEqual(new[] { "state" }, provider.SnapshotNames);
        }

        /// <summary>验证默认宿主的 state 具有固定对象，且未实现能力明确为 false。</summary>
        [Test]
        public void CreateSnapshot_默认宿主_固定结构且未实现能力为假()
        {
            using (JsonDocument document = JsonDocument.Parse(GetProvider().CreateSnapshot("state")))
            {
                JsonElement root = document.RootElement;
                Assert.AreEqual(1, ReadInt32(root, "schemaVersion"));
                Assert.AreEqual(JsonValueKind.Number, root.GetProperty("diagnosticVersion").ValueKind);
                Assert.AreEqual(JsonValueKind.Number, root.GetProperty("settingsVersion").ValueKind);
                AssertSnapshotObjects(root);
                AssertCapabilitiesAreUnavailable(root.GetProperty("capabilities"));
            }
        }

        /// <summary>验证历史超过快照窗口时只输出最新 32 条，并保持 UTF-8 载荷可编码。</summary>
        [Test]
        public void CreateSnapshot_历史超过上限_只保留最新且载荷有界()
        {
            for (var index = 0; index < 130; index++)
            {
                LogKit.Warning("entry-" + index + ":" + new string('中', 500) + "\ud83d\ude80");
            }

            string json = GetProvider().CreateSnapshot("state");
            using (JsonDocument document = JsonDocument.Parse(json))
            {
                JsonElement history = document.RootElement.GetProperty("history");
                AssertHistoryWindow(history);
                AssertBoundedMessages(history.GetProperty("entries"));
            }

            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
        }

        /// <summary>验证请求指定数量时返回独立的最新优先副本，且不改写运行时历史。</summary>
        [Test]
        public void GetRecentHistory_请求三条_返回独立的最新副本()
        {
            for (var index = 0; index < 5; index++)
            {
                LogKit.Warning("entry-" + index);
            }

            LogKitEntry[] first = LogKit.GetRecentHistory(3, out _, out _);
            first[0].Message = "mutated";
            LogKitEntry[] second = LogKit.GetRecentHistory(3, out var stats, out _);

            CollectionAssert.AreEqual(
                new[] { "entry-4", "entry-3", "entry-2" },
                second.Select(entry => entry.Message).ToArray());
            Assert.AreEqual(5, stats.HistoryCount);
        }

        /// <summary>验证 capability 描述中的命令 action 与当前 Provider 目录一致。</summary>
        [Test]
        public void Commands_capability描述_与Provider目录一致()
        {
            string path = Path.Combine(
                Application.dataPath,
                "YokiFrame",
                "Core",
                "Editor",
                "LogKit",
                "Capabilities",
                "capability.json");
            using (JsonDocument document = JsonDocument.Parse(File.ReadAllText(path)))
            {
                CollectionAssert.AreEquivalent(
                    GetProvider().Commands.Select(static command => command.Action).ToArray(),
                    ReadActions(document.RootElement.GetProperty("kit").GetProperty("commands")));
            }
        }

        /// <summary>确认 state 的统计、设置、文件和历史都是固定结构。</summary>
        /// <param name="root">state 根对象。</param>
        private static void AssertSnapshotObjects(JsonElement root)
        {
            Assert.AreEqual(JsonValueKind.Object, root.GetProperty("stats").ValueKind);
            Assert.AreEqual(JsonValueKind.Object, root.GetProperty("settings").ValueKind);
            JsonElement files = root.GetProperty("files");
            Assert.AreEqual(JsonValueKind.Object, files.ValueKind);
            Assert.AreEqual(JsonValueKind.Object, files.GetProperty("editor").ValueKind);
            Assert.AreEqual(JsonValueKind.Object, files.GetProperty("player").ValueKind);
            JsonElement history = root.GetProperty("history");
            Assert.AreEqual(JsonValueKind.Object, history.ValueKind);
            Assert.AreEqual(JsonValueKind.Array, history.GetProperty("entries").ValueKind);
        }

        /// <summary>确认默认宿主没有声明设置应用、文件、界面或加密能力。</summary>
        /// <param name="capabilities">capabilities 对象。</param>
        private static void AssertCapabilitiesAreUnavailable(JsonElement capabilities)
        {
            Assert.AreEqual(JsonValueKind.Object, capabilities.ValueKind);
            Assert.IsFalse(ReadBool(capabilities, "settingsApply"));
            Assert.IsFalse(ReadBool(capabilities, "filePreview"));
            Assert.IsFalse(ReadBool(capabilities, "fileWriter"));
            Assert.IsFalse(ReadBool(capabilities, "playerImGui"));
            Assert.IsFalse(ReadBool(capabilities, "encryption"));
        }

        /// <summary>确认快照历史窗口保留总量、丢弃量和裁剪事实。</summary>
        /// <param name="history">history 对象。</param>
        private static void AssertHistoryWindow(JsonElement history)
        {
            Assert.AreEqual(SNAPSHOT_HISTORY_LIMIT, history.GetProperty("entries").GetArrayLength());
            Assert.AreEqual(SNAPSHOT_HISTORY_LIMIT, ReadInt32(history, "count"));
            Assert.AreEqual(130, ReadInt32(history, "totalCount"));
            Assert.AreEqual(2, ReadInt32(history, "droppedCount"));
            Assert.IsTrue(ReadBool(history, "truncated"));
        }

        /// <summary>确认每条快照消息都可编码，并且首尾仍指向最新窗口。</summary>
        /// <param name="entries">历史条目数组。</param>
        private static void AssertBoundedMessages(JsonElement entries)
        {
            for (var index = 0; index < SNAPSHOT_HISTORY_LIMIT; index++)
            {
                string message = ReadString(entries[index], "message");
                Assert.LessOrEqual(Encoding.UTF8.GetByteCount(message), MESSAGE_MAX_BYTES);
                Assert.IsFalse(ContainsUnpairedSurrogate(message));
            }

            Assert.IsTrue(ReadString(entries[0], "message").StartsWith("entry-129:", StringComparison.Ordinal));
            Assert.IsTrue(ReadString(entries[SNAPSHOT_HISTORY_LIMIT - 1], "message").StartsWith("entry-98:", StringComparison.Ordinal));
        }

        /// <summary>读取 capability 命令 action。</summary>
        /// <param name="commands">命令数组。</param>
        /// <returns>action 列表。</returns>
        private static string[] ReadActions(JsonElement commands)
        {
            var actions = new string[commands.GetArrayLength()];
            for (var index = 0; index < actions.Length; index++)
            {
                actions[index] = commands[index].GetProperty("action").GetString();
            }

            return actions;
        }

        /// <summary>从 Core 默认 Registry 获取唯一 LogKit versioned Provider。</summary>
        /// <returns>LogKit 版本化 Interaction Provider。</returns>
        private static IYokiFrameVersionedKitInteractionProvider GetProvider()
        {
            IYokiFrameKitInteractionProvider provider = YokiFrameCoreKitInteractions.CreateDefault()
                .Providers.First(item => string.Equals(item.Kit, "LogKit", StringComparison.Ordinal));
            var versioned = provider as IYokiFrameVersionedKitInteractionProvider;
            Assert.IsNotNull(versioned);
            return versioned;
        }

        /// <summary>检查文本是否包含破损 UTF-16 代理，避免裁剪输出不可编码文本。</summary>
        /// <param name="value">待检查文本。</param>
        /// <returns>存在不成对代理时返回 true。</returns>
        private static bool ContainsUnpairedSurrogate(string value)
        {
            for (var index = 0; index < value.Length; index++)
            {
                if (char.IsHighSurrogate(value[index]))
                {
                    if (index + 1 >= value.Length || !char.IsLowSurrogate(value[++index])) return true;
                }
                else if (char.IsLowSurrogate(value[index]))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>读取 JSON 布尔字段。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段值。</returns>
        private static bool ReadBool(JsonElement element, string propertyName)
        {
            return element.GetProperty(propertyName).GetBoolean();
        }

        /// <summary>读取 JSON 字符串字段。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段文本。</returns>
        private static string ReadString(JsonElement element, string propertyName)
        {
            return element.GetProperty(propertyName).GetString();
        }

        /// <summary>读取 JSON 整数字段。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段整数。</returns>
        private static int ReadInt32(JsonElement element, string propertyName)
        {
            int value;
            Assert.IsTrue(element.GetProperty(propertyName).TryGetInt32(out value), propertyName);
            return value;
        }
    }
}
