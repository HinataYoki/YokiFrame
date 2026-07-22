using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>验证 LogKit 设置命令的严格 JSON 边界、原子语义和显式文件尾读。</summary>
    public sealed class YokiFrameLogKitSettingsCommandTests
    {
        private string mTempRoot;

        /// <summary>为每个测试建立独立宿主目录并启用设置与文件预览能力。</summary>
        [SetUp]
        public void SetUp()
        {
            KitSettings.Reset();
            LogKit.Reset();
            LogKitSettings.ResetToDefaults();
            mTempRoot = Path.Combine(
                Path.GetTempPath(),
                "YokiFrame.LogKit.Tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(mTempRoot);
            LogKitHostEnvironment.Configure(
                Path.Combine(mTempRoot, "LogFiles"),
                true,
                true,
                false,
                false,
                false);
        }

        /// <summary>测试后释放全局宿主状态并删除本用例创建的临时目录。</summary>
        [TearDown]
        public void TearDown()
        {
            LogKitHostEnvironment.Reset();
            LogKit.Reset();
            KitSettings.Reset();
            if (Directory.Exists(mTempRoot))
            {
                Directory.Delete(mTempRoot, true);
            }
        }

        /// <summary>验证 System.Text.Json 风格 unicode escape 能还原中文及代理对并返回完整 state。</summary>
        [Test]
        public void SetSettingsDecodesUnicodeEscapesAndReturnsFullState()
        {
            string payload = CreateSettingsPayload(
                false,
                "Error",
                "\"\\u65e5\\u5fd7\\uD83D\\uDCC1\"",
                "\"\\u7f16\\u8f91.log\"",
                "\"\\u73a9\\u5bb6.log\"");

            YokiFrameCommandResult result = Execute("set_settings", payload);
            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            WorkbenchState state = JsonUtility.FromJson<WorkbenchState>(result.ResultJson);
            Assert.IsFalse(LogKit.Enabled);
            Assert.AreEqual(LogLevel.Error, LogKit.MinimumLevel);
            Assert.AreEqual("日志📁", LogKitSettings.GetString(LogKitSettings.LOG_DIRECTORY_KEY, string.Empty));
            Assert.AreEqual("编辑.log", LogKitSettings.GetString(LogKitSettings.EDITOR_FILE_NAME_KEY, string.Empty));
            Assert.AreEqual("玩家.log", LogKitSettings.GetString(LogKitSettings.PLAYER_FILE_NAME_KEY, string.Empty));
            Assert.AreEqual("日志📁", state.settings.logDirectory);
            Assert.AreEqual("编辑.log", state.settings.editorFileName);
            Assert.IsTrue(state.capabilities.settingsApply);
            Assert.IsTrue(state.capabilities.filePreview);
            Assert.IsFalse(state.capabilities.fileWriter);
            Assert.IsFalse(state.capabilities.playerImGui);
            Assert.IsFalse(state.capabilities.encryption);
        }

        /// <summary>验证畸形、重复、嵌套、未知或错误 primitive 不会部分修改当前设置。</summary>
        /// <param name="variant">待构造的无效 payload 类型。</param>
        [TestCase("duplicate")]
        [TestCase("nested")]
        [TestCase("quoted-boolean")]
        [TestCase("quoted-integer")]
        [TestCase("unknown")]
        [TestCase("missing")]
        [TestCase("trailing-comma")]
        [TestCase("unpaired-unicode")]
        [TestCase("raw-unpaired-unicode")]
        public void InvalidSettingsPayloadIsRejectedAtomically(string variant)
        {
            long version = LogKitSettings.SettingsVersion;
            string payload = CreateInvalidPayload(variant);

            YokiFrameCommandResult result = Execute("set_settings", payload);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("InvalidPayload", result.ErrorCode);
            Assert.AreEqual(version, LogKitSettings.SettingsVersion);
            Assert.IsTrue(LogKit.Enabled);
            Assert.AreEqual(LogLevel.Debug, LogKit.MinimumLevel);
            Assert.AreEqual(
                string.Empty,
                LogKitSettings.GetString(LogKitSettings.LOG_DIRECTORY_KEY, "changed"));
        }

        /// <summary>验证 reset_settings 与 clear_history 都返回同一完整 state，而非零散确认对象。</summary>
        [Test]
        public void ResetAndClearCommandsReturnCompleteState()
        {
            YokiFrameCommandResult changed = Execute(
                "set_settings",
                CreateSettingsPayload(false, "Error"));
            YokiFrameCommandResult reset = Execute("reset_settings", "{}");
            LogKit.Warning("clear-me");
            YokiFrameCommandResult cleared = Execute("clear_history", "{}");

            Assert.IsTrue(changed.IsSuccess);
            Assert.IsTrue(reset.IsSuccess);
            Assert.IsTrue(cleared.IsSuccess);
            WorkbenchState resetState = JsonUtility.FromJson<WorkbenchState>(reset.ResultJson);
            WorkbenchState clearedState = JsonUtility.FromJson<WorkbenchState>(cleared.ResultJson);
            Assert.IsTrue(resetState.settings.enabled);
            Assert.AreEqual("Debug", resetState.settings.minimumLevel);
            Assert.IsNotNull(resetState.files);
            Assert.AreEqual(0, clearedState.history.count);
            Assert.AreEqual(0, clearedState.history.totalCount);
            Assert.AreEqual(0, clearedState.history.droppedCount);
        }

        /// <summary>验证 editor 文件预览字段完整，末尾换行不会制造额外空行。</summary>
        [Test]
        public void ReadLogFileReturnsCompletePreviewWithoutTrailingEmptyLine()
        {
            string directory = Path.Combine(mTempRoot, "LogFiles");
            string path = Path.Combine(directory, LogKitSettings.DEFAULT_EDITOR_FILE_NAME);
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "first\nsecond\n", new UTF8Encoding(false));

            YokiFrameCommandResult result = Execute("read_log_file", "{\"kind\":\"editor\"}");
            YokiFrameCommandResult invalid = Execute("read_log_file", "{\"kind\":\"runtime\"}");

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            FilePreview preview = JsonUtility.FromJson<FilePreview>(result.ResultJson);
            Assert.AreEqual("editor", preview.kind);
            Assert.AreEqual(path, preview.path);
            Assert.AreEqual(LogKitSettings.DEFAULT_EDITOR_FILE_NAME, preview.fileName);
            Assert.IsTrue(preview.exists);
            Assert.Greater(preview.sizeBytes, 0L);
            Assert.IsNotEmpty(preview.modifiedUtc);
            Assert.AreEqual(2, preview.lineCount);
            Assert.IsFalse(preview.truncated);
            Assert.AreEqual("first\nsecond\n", preview.content);
            Assert.AreEqual(string.Empty, preview.errorMessage);
            Assert.IsFalse(invalid.IsSuccess);
            Assert.AreEqual("InvalidPayload", invalid.ErrorCode);
        }

        /// <summary>验证大文件只读取最后 48 KiB，并从第一条完整日志行开始返回。</summary>
        [Test]
        public void ReadLogFileBoundsTailAndDropsPartialFirstLine()
        {
            string directory = Path.Combine(mTempRoot, "LogFiles");
            string path = Path.Combine(directory, LogKitSettings.DEFAULT_EDITOR_FILE_NAME);
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, CreateLargeLogText(), new UTF8Encoding(false));

            YokiFrameCommandResult result = Execute("read_log_file", "{\"kind\":\"editor\"}");

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            FilePreview preview = JsonUtility.FromJson<FilePreview>(result.ResultJson);
            Assert.IsTrue(preview.truncated);
            Assert.LessOrEqual(Encoding.UTF8.GetByteCount(preview.content), 48 * 1024);
            StringAssert.DoesNotContain("discard-prefix", preview.content);
            StringAssert.Contains("final-marker", preview.content);
            Assert.Greater(preview.lineCount, 0);
        }

        /// <summary>执行当前默认 Registry 中的 LogKit 命令。</summary>
        private static YokiFrameCommandResult Execute(string action, string payloadJson)
        {
            IYokiFrameKitInteractionProvider provider = YokiFrameCoreKitInteractions.CreateDefault()
                .Providers.First(item => string.Equals(item.Kit, "LogKit", StringComparison.Ordinal));
            return provider.Handle(new YokiFrameCommandRequest(
                "logkit-test",
                "LogKit",
                action,
                payloadJson,
                1000,
                64));
        }

        /// <summary>创建全部十四个字段均存在的有效扁平设置对象。</summary>
        private static string CreateSettingsPayload(
            bool enabled,
            string minimumLevel,
            string directoryJson = "\"\"",
            string editorFileJson = "\"yoki_editor.log\"",
            string playerFileJson = "\"yoki_player.log\"")
        {
            return "{\"enabled\":" + (enabled ? "true" : "false")
                + ",\"minimumLevel\":\"" + minimumLevel + "\""
                + ",\"saveLogInEditor\":true,\"saveLogInPlayer\":true"
                + ",\"enableIMGUIInPlayer\":false,\"enableEncryption\":false"
                + ",\"maxQueueSize\":4096,\"maxSameLogCount\":20"
                + ",\"maxRetentionDays\":7,\"maxFileSizeMB\":32"
                + ",\"imguiMaxLogCount\":100,\"logDirectory\":" + directoryJson
                + ",\"editorFileName\":" + editorFileJson
                + ",\"playerFileName\":" + playerFileJson + "}";
        }

        /// <summary>按变体从有效对象构造一个应被严格 parser 拒绝的 payload。</summary>
        private static string CreateInvalidPayload(string variant)
        {
            string valid = CreateSettingsPayload(true, "Debug");
            if (variant == "duplicate") return valid.Substring(0, valid.Length - 1) + ",\"enabled\":false}";
            if (variant == "nested") return valid.Replace("\"enabled\":true", "\"enabled\":{\"value\":true}");
            if (variant == "quoted-boolean") return valid.Replace("\"enabled\":true", "\"enabled\":\"true\"");
            if (variant == "quoted-integer") return valid.Replace("\"maxQueueSize\":4096", "\"maxQueueSize\":\"4096\"");
            if (variant == "unknown") return valid.Substring(0, valid.Length - 1) + ",\"extra\":1}";
            if (variant == "missing") return valid.Replace(",\"playerFileName\":\"yoki_player.log\"", string.Empty);
            if (variant == "trailing-comma") return valid.Substring(0, valid.Length - 1) + ",}";
            if (variant == "raw-unpaired-unicode")
            {
                return valid.Replace("yoki_editor.log", "\ud800.log");
            }

            return valid.Replace("\"yoki_editor.log\"", "\"\\uD800.log\"");
        }

        /// <summary>创建明显超过尾读上限且包含稳定首尾标记的日志文本。</summary>
        private static string CreateLargeLogText()
        {
            var builder = new StringBuilder(80 * 1024);
            builder.Append("discard-prefix:").Append(new string('x', 2048)).Append('\n');
            for (var index = 0; index < 1800; index++)
            {
                builder.Append("line-").Append(index).Append(':')
                    .Append("abcdefghijklmnopqrstuvwxyz0123456789").Append('\n');
            }

            builder.Append("final-marker\n");
            return builder.ToString();
        }

        /// <summary>映射命令返回的完整 state 关键字段。</summary>
        [Serializable]
        private sealed class WorkbenchState
        {
            public SettingsState settings;
            public CapabilitiesState capabilities;
            public FilesState files;
            public HistoryState history;
        }

        /// <summary>映射命令可修改的设置字段。</summary>
        [Serializable]
        private sealed class SettingsState
        {
            public bool enabled;
            public string minimumLevel;
            public string logDirectory;
            public string editorFileName;
            public string playerFileName;
        }

        /// <summary>映射宿主能力，确保 UI 不展示未实现功能。</summary>
        [Serializable]
        private sealed class CapabilitiesState
        {
            public bool settingsApply;
            public bool filePreview;
            public bool fileWriter;
            public bool playerImGui;
            public bool encryption;
        }

        /// <summary>映射日志文件状态容器。</summary>
        [Serializable]
        private sealed class FilesState
        {
            public string directory;
        }

        /// <summary>映射清空后历史统计。</summary>
        [Serializable]
        private sealed class HistoryState
        {
            public int count;
            public int totalCount;
            public int droppedCount;
        }

        /// <summary>映射 read_log_file 的固定响应对象。</summary>
        [Serializable]
        private sealed class FilePreview
        {
            public string kind;
            public string path;
            public string fileName;
            public bool exists;
            public long sizeBytes;
            public string modifiedUtc;
            public int lineCount;
            public bool truncated;
            public string content;
            public string errorMessage;
        }
    }
}
