using System;
using System.IO;
using System.Linq;
using System.Text;
using NUnit.Framework;
using JsonDocument = YokiFrame.Json.JsonDocument;
using JsonElement = YokiFrame.Json.JsonElement;
using JsonValueKind = YokiFrame.Json.JsonValueKind;

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

        /// <summary>验证 unicode escape 能还原中文及代理对，并在返回 state 中保留同一路径。</summary>
        [Test]
        public void SetSettings_Unicode转义_运行时与返回字段一致()
        {
            string payload = CreateSettingsPayload(
                false,
                "Error",
                "\"\\u65e5\\u5fd7\\uD83D\\uDCC1\"",
                "\"\\u7f16\\u8f91.log\"",
                "\"\\u73a9\\u5bb6.log\"");

            YokiFrameCommandResult result = Execute("set_settings", payload);
            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            Assert.IsFalse(LogKit.Enabled);
            Assert.AreEqual(LogLevel.Error, LogKit.MinimumLevel);
            Assert.AreEqual("日志📁", LogKitSettings.GetString(LogKitSettings.LOG_DIRECTORY_KEY, string.Empty));
            Assert.AreEqual("编辑.log", LogKitSettings.GetString(LogKitSettings.EDITOR_FILE_NAME_KEY, string.Empty));
            Assert.AreEqual("玩家.log", LogKitSettings.GetString(LogKitSettings.PLAYER_FILE_NAME_KEY, string.Empty));
            using (JsonDocument document = JsonDocument.Parse(result.ResultJson))
            {
                JsonElement settings = document.RootElement.GetProperty("settings");
                JsonElement capabilities = document.RootElement.GetProperty("capabilities");
                Assert.AreEqual("日志📁", ReadString(settings, "logDirectory"));
                Assert.AreEqual("编辑.log", ReadString(settings, "editorFileName"));
                Assert.IsTrue(ReadBool(capabilities, "settingsApply"));
                Assert.IsTrue(ReadBool(capabilities, "filePreview"));
                Assert.IsFalse(ReadBool(capabilities, "fileWriter"));
                Assert.IsFalse(ReadBool(capabilities, "playerImGui"));
                Assert.IsFalse(ReadBool(capabilities, "encryption"));
            }
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

        /// <summary>验证 reset_settings 与 clear_history 都返回完整 state，而不是零散确认对象。</summary>
        [Test]
        public void ResetSettings_清空历史_返回完整状态()
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
            using (JsonDocument resetDocument = JsonDocument.Parse(reset.ResultJson))
            using (JsonDocument clearedDocument = JsonDocument.Parse(cleared.ResultJson))
            {
                JsonElement resetSettings = resetDocument.RootElement.GetProperty("settings");
                Assert.IsTrue(ReadBool(resetSettings, "enabled"));
                Assert.AreEqual("Debug", ReadString(resetSettings, "minimumLevel"));
                Assert.AreEqual(JsonValueKind.Object, resetDocument.RootElement.GetProperty("files").ValueKind);
                JsonElement history = clearedDocument.RootElement.GetProperty("history");
                Assert.AreEqual(0, ReadInt32(history, "count"));
                Assert.AreEqual(0, ReadInt32(history, "totalCount"));
                Assert.AreEqual(0, ReadInt32(history, "droppedCount"));
            }
        }

        /// <summary>验证 editor 文件预览字段完整，末尾换行不会制造额外空行。</summary>
        [Test]
        public void ReadLogFile_编辑器文件_预览不增加空行()
        {
            string directory = Path.Combine(mTempRoot, "LogFiles");
            string path = Path.Combine(directory, LogKitSettings.DEFAULT_EDITOR_FILE_NAME);
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, "first\nsecond\n", new UTF8Encoding(false));

            YokiFrameCommandResult result = Execute("read_log_file", "{\"kind\":\"editor\"}");
            YokiFrameCommandResult invalid = Execute("read_log_file", "{\"kind\":\"runtime\"}");

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            using (JsonDocument document = JsonDocument.Parse(result.ResultJson))
            {
                JsonElement preview = document.RootElement;
                Assert.AreEqual("editor", ReadString(preview, "kind"));
                Assert.AreEqual(path, ReadString(preview, "path"));
                Assert.AreEqual(LogKitSettings.DEFAULT_EDITOR_FILE_NAME, ReadString(preview, "fileName"));
                Assert.IsTrue(ReadBool(preview, "exists"));
                Assert.Greater(ReadInt64(preview, "sizeBytes"), 0L);
                Assert.IsNotEmpty(ReadString(preview, "modifiedUtc"));
                Assert.AreEqual(2, ReadInt32(preview, "lineCount"));
                Assert.IsFalse(ReadBool(preview, "truncated"));
                Assert.AreEqual("first\nsecond\n", ReadString(preview, "content"));
                Assert.AreEqual(string.Empty, ReadString(preview, "errorMessage"));
            }
            Assert.IsFalse(invalid.IsSuccess);
            Assert.AreEqual("InvalidPayload", invalid.ErrorCode);
        }

        /// <summary>验证大文件只读取最后 48 KiB，并从第一条完整日志行开始返回。</summary>
        [Test]
        public void ReadLogFile_超过尾部上限_丢掉半行并保留末尾()
        {
            string directory = Path.Combine(mTempRoot, "LogFiles");
            string path = Path.Combine(directory, LogKitSettings.DEFAULT_EDITOR_FILE_NAME);
            Directory.CreateDirectory(directory);
            File.WriteAllText(path, CreateLargeLogText(), new UTF8Encoding(false));

            YokiFrameCommandResult result = Execute("read_log_file", "{\"kind\":\"editor\"}");

            Assert.IsTrue(result.IsSuccess, result.ErrorMessage);
            using (JsonDocument document = JsonDocument.Parse(result.ResultJson))
            {
                JsonElement preview = document.RootElement;
                string content = ReadString(preview, "content");
                Assert.IsTrue(ReadBool(preview, "truncated"));
                Assert.LessOrEqual(Encoding.UTF8.GetByteCount(content), 48 * 1024);
                Assert.IsTrue(content.IndexOf("discard-prefix", StringComparison.Ordinal) < 0);
                Assert.IsTrue(content.EndsWith("final-marker\n", StringComparison.Ordinal));
                Assert.Greater(ReadInt32(preview, "lineCount"), 0);
            }
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

        /// <summary>读取 JSON 64 位整数字段。</summary>
        /// <param name="element">所属对象。</param>
        /// <param name="propertyName">字段名。</param>
        /// <returns>字段整数。</returns>
        private static long ReadInt64(JsonElement element, string propertyName)
        {
            long value;
            Assert.IsTrue(element.GetProperty(propertyName).TryGetInt64(out value), propertyName);
            return value;
        }
    }
}