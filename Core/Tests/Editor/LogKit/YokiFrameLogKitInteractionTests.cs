using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>验证 LogKit 统一 Interaction、固定 state 和高频快照体积边界。</summary>
    public sealed class YokiFrameLogKitInteractionTests
    {
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

        /// <summary>验证 Provider 只声明两个只读和三个显式用户操作命令。</summary>
        [Test]
        public void ProviderDeclaresFixedCommandsAndRiskKinds()
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

        /// <summary>验证 state 顶层结构、子对象和未实现能力始终明确存在。</summary>
        [Test]
        public void StateSnapshotUsesFixedSchemaAndTruthfulCapabilities()
        {
            IYokiFrameVersionedKitInteractionProvider provider = GetProvider();

            string json = provider.CreateSnapshot("state");
            WorkbenchState state = JsonUtility.FromJson<WorkbenchState>(json);

            StringAssert.StartsWith("{\"schemaVersion\":", json);
            Assert.AreEqual(1, state.schemaVersion);
            Assert.IsNotNull(state.stats);
            Assert.IsNotNull(state.settings);
            Assert.IsNotNull(state.capabilities);
            Assert.IsNotNull(state.files);
            Assert.IsNotNull(state.files.editor);
            Assert.IsNotNull(state.files.player);
            Assert.IsNotNull(state.history);
            Assert.IsNotNull(state.history.entries);
            Assert.IsFalse(state.capabilities.settingsApply);
            Assert.IsFalse(state.capabilities.filePreview);
            Assert.IsFalse(state.capabilities.fileWriter);
            Assert.IsFalse(state.capabilities.playerImGui);
            Assert.IsFalse(state.capabilities.encryption);
        }

        /// <summary>验证快照只复制最新 32 条、保留总量事实并始终低于 Shared Memory 上限。</summary>
        [Test]
        public void SnapshotBoundsRecentHistoryAndUtf8Payload()
        {
            for (var index = 0; index < 130; index++)
            {
                LogKit.Warning("entry-" + index + ":" + new string('中', 500) + "\ud83d\ude80");
            }

            string json = GetProvider().CreateSnapshot("state");
            WorkbenchState state = JsonUtility.FromJson<WorkbenchState>(json);

            Assert.AreEqual(32, state.history.entries.Length);
            Assert.AreEqual(32, state.history.count);
            Assert.AreEqual(130, state.history.totalCount);
            Assert.AreEqual(2, state.history.droppedCount);
            Assert.IsTrue(state.history.truncated);
            StringAssert.StartsWith("entry-129:", state.history.entries[0].message);
            StringAssert.StartsWith("entry-98:", state.history.entries[31].message);
            Assert.LessOrEqual(
                Encoding.UTF8.GetByteCount(json),
                YokiFrameSharedMemoryTelemetryContract.DEFAULT_MAX_PAYLOAD_BYTES);
            Assert.IsTrue(state.history.entries.All(entry =>
                Encoding.UTF8.GetByteCount(entry.message) <= 220
                && !ContainsUnpairedSurrogate(entry.message)));
        }

        /// <summary>验证快照专用入口跳过旧项并返回独立的最新优先副本。</summary>
        [Test]
        public void RecentHistoryCopiesOnlyRequestedNewestEntries()
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

        /// <summary>验证 capability descriptor 同步声明五个命令且不恢复旧 action。</summary>
        [Test]
        public void CapabilityDescriptorMatchesProviderCatalog()
        {
            string path = Path.Combine(
                Application.dataPath,
                "YokiFrame",
                "Core",
                "Editor",
                "LogKit",
                "Capabilities",
                "capability.json");
            string descriptor = File.ReadAllText(path);

            StringAssert.Contains("\"get_workbench_snapshot\"", descriptor);
            StringAssert.Contains("\"read_log_file\"", descriptor);
            StringAssert.Contains("\"set_settings\"", descriptor);
            StringAssert.Contains("\"reset_settings\"", descriptor);
            StringAssert.Contains("\"clear_history\"", descriptor);
            StringAssert.DoesNotContain("\"scan\"", descriptor);
            StringAssert.DoesNotContain("\"write_log_file\"", descriptor);
        }

        /// <summary>从 Core 默认 Registry 获取唯一 LogKit versioned Provider。</summary>
        private static IYokiFrameVersionedKitInteractionProvider GetProvider()
        {
            IYokiFrameKitInteractionProvider provider = YokiFrameCoreKitInteractions.CreateDefault()
                .Providers.First(item => string.Equals(item.Kit, "LogKit", StringComparison.Ordinal));
            var versioned = provider as IYokiFrameVersionedKitInteractionProvider;
            Assert.IsNotNull(versioned);
            return versioned;
        }

        /// <summary>检查文本是否包含破损 UTF-16 代理，避免裁剪输出不可编码文本。</summary>
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

        /// <summary>映射 LogKit 完整 Workbench state，字段名与协议保持一致。</summary>
        [Serializable]
        private sealed class WorkbenchState
        {
            public int schemaVersion;
            public long diagnosticVersion;
            public long settingsVersion;
            public StatsState stats;
            public SettingsState settings;
            public CapabilitiesState capabilities;
            public FilesState files;
            public HistoryState history;
        }

        /// <summary>映射运行统计。</summary>
        [Serializable]
        private sealed class StatsState
        {
            public string loggerName;
            public bool hasLogger;
            public bool enabled;
            public string minimumLevel;
            public int historyCount;
            public int droppedCount;
        }

        /// <summary>映射完整 Runtime Settings；测试只需确认对象存在。</summary>
        [Serializable]
        private sealed class SettingsState
        {
            public bool enabled;
        }

        /// <summary>映射宿主真实能力。</summary>
        [Serializable]
        private sealed class CapabilitiesState
        {
            public bool settingsApply;
            public bool filePreview;
            public bool fileWriter;
            public bool playerImGui;
            public bool encryption;
        }

        /// <summary>映射 Editor/Player 文件状态。</summary>
        [Serializable]
        private sealed class FilesState
        {
            public string directory;
            public FileState editor;
            public FileState player;
        }

        /// <summary>映射单个日志文件元数据。</summary>
        [Serializable]
        private sealed class FileState
        {
            public string kind;
            public string path;
            public string fileName;
            public bool exists;
            public long sizeBytes;
            public string modifiedUtc;
        }

        /// <summary>映射有界历史及总量统计。</summary>
        [Serializable]
        private sealed class HistoryState
        {
            public HistoryEntry[] entries;
            public int count;
            public int totalCount;
            public int droppedCount;
            public bool truncated;
        }

        /// <summary>映射一条历史文本。</summary>
        [Serializable]
        private sealed class HistoryEntry
        {
            public string level;
            public string message;
            public string context;
            public string exceptionType;
            public string exceptionMessage;
            public string stackTrace;
            public string timestampUtc;
        }
    }
}
