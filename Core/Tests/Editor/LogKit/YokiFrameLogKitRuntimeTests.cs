using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Core LogKit 的运行时门面、Editor 观察能力、过滤规则和 Player 编译边界。
    /// </summary>
    public sealed class YokiFrameLogKitRuntimeTests
    {
        private const string OBSERVATION_DEFINE = "#if UNITY_EDITOR || (GODOT && TOOLS)";

        /// <summary>
        /// 每个测试前清理全局状态，避免日志历史和设置相互污染。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            KitSettings.Reset();
            LogKit.Reset();
            LogKitSettings.ResetToDefaults();
        }

        /// <summary>
        /// 验证 LogKit.Log 会写入注入后端，并把最近日志放到历史列表头部。
        /// </summary>
        [Test]
        public void LogWritesToInjectedLoggerAndHistory()
        {
            var logger = new RecordingLogger();
            LogKit.SetLogger(logger);

            LogKit.Log("hello logkit", "context-a");

            var history = new List<LogKitEntry>();
            LogKit.GetHistory(history);
            Assert.AreEqual(LogLevel.Info, logger.LastLevel);
            Assert.AreEqual("hello logkit", logger.LastMessage);
            Assert.AreEqual("context-a", logger.LastContext);
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual("hello logkit", history[0].Message);
            Assert.AreEqual("context-a", history[0].Context);
        }

        /// <summary>
        /// 验证最小等级过滤会丢弃低等级日志，但仍允许同级和更高等级日志通过。
        /// </summary>
        [Test]
        public void MinimumLevelFiltersLowerPriorityLogs()
        {
            LogKit.MinimumLevel = LogLevel.Warning;
            LogKit.Log("hidden info");
            LogKit.Warning("visible warning");

            var history = new List<LogKitEntry>();
            LogKit.GetHistory(history);
            Assert.AreEqual(1, history.Count);
            Assert.AreEqual(LogLevel.Warning, history[0].Level);
            Assert.AreEqual("visible warning", history[0].Message);
        }

        /// <summary>
        /// 验证新架构不再暴露旧版 KitLogger 入口，避免 Tool 继续依赖旧兼容 API。
        /// </summary>
        [Test]
        public void LegacyKitLoggerEntryIsRemoved()
        {
            Assert.IsNull(typeof(LogKit).Assembly.GetType("YokiFrame.KitLogger"), "新架构不再保留 KitLogger 旧入口。");
        }

        /// <summary>
        /// 验证历史队列保持固定容量，并按最新日志优先返回。
        /// </summary>
        [Test]
        public void HistoryIsCappedAndReturnedNewestFirst()
        {
            for (var index = 0; index < 130; index++)
            {
                LogKit.Warning("entry-" + index);
            }

            var history = new List<LogKitEntry>();
            LogKit.GetHistory(history);
            var stats = LogKit.GetStats();
            Assert.AreEqual(128, history.Count);
            Assert.AreEqual(2, stats.DroppedCount);
            Assert.AreEqual("entry-129", history[0].Message);
            Assert.AreEqual("entry-2", history[127].Message);
        }

        /// <summary>
        /// 验证完整 settings payload 能同步运行时开关、等级和日志文件字段。
        /// </summary>
        [Test]
        public void SettingsPayloadUpdatesRuntimeAndJsonSnapshot()
        {
            LogKitSettings.ApplyPayload(CreateCompleteSettingsPayload());

            string settingsJson = LogKitSettings.BuildJson();
            Assert.IsFalse(LogKit.Enabled);
            Assert.AreEqual(LogLevel.Error, LogKit.MinimumLevel);
            Assert.IsTrue(settingsJson.Contains("\"enabled\":false"));
            Assert.IsTrue(settingsJson.Contains("\"minimumLevel\":\"Error\""));
            Assert.IsTrue(settingsJson.Contains("\"maxQueueSize\":4096"));
            Assert.IsTrue(settingsJson.Contains("\"maxSameLogCount\":12"));
            Assert.IsTrue(settingsJson.Contains("\"editorFileName\":\"editor-custom.log\""));
        }

        /// <summary>
        /// 验证 Runtime Settings 同步完成后通知宿主 Adapter，使 Player 调试覆盖层无需轮询配置存储。
        /// </summary>
        [Test]
        public void ApplyingRuntimeSettingsNotifiesHostAdapters()
        {
            var notificationCount = 0;
            Action callback = () => notificationCount++;
            LogKitSettings.RuntimeSettingsApplied += callback;
            try
            {
                LogKitSettings.ApplyBaseRuntimeSettings();

                Assert.AreEqual(1, notificationCount);
            }
            finally
            {
                LogKitSettings.RuntimeSettingsApplied -= callback;
            }
        }

        /// <summary>验证禁用和低等级过滤在消息格式化前完成，不会调用无效消息的 ToString。</summary>
        [Test]
        public void DisabledAndFilteredLogsDoNotFormatMessages()
        {
            var disabledMessage = new CountingMessage();
            var filteredMessage = new CountingMessage();

            LogKit.Enabled = false;
            LogKit.Log(disabledMessage);
            LogKit.Enabled = true;
            LogKit.MinimumLevel = LogLevel.Error;
            LogKit.Warning(filteredMessage);

            Assert.AreEqual(0, disabledMessage.FormatCount);
            Assert.AreEqual(0, filteredMessage.FormatCount);
            Assert.AreEqual(0, LogKit.GetStats().HistoryCount);
        }

        /// <summary>
        /// 验证 Player 预处理后的 LogKit 源码不再包含 Workbench 历史、堆栈、Provider 或 Editor 配置。
        /// </summary>
        [Test]
        public void PlayerLogKitSourcesExcludeWorkbenchObservationCode()
        {
            string packageRoot = Path.Combine(Application.dataPath, "YokiFrame");
            string[] roots =
            {
                Path.Combine(packageRoot, "Core", "Runtime", "LogKit"),
                Path.Combine(packageRoot, "Core", "Adapters", "Unity", "Runtime", "LogKit"),
                Path.Combine(packageRoot, "Core", "Adapters", "Godot", "Runtime", "LogKit")
            };
            string[] forbiddenTokens =
            {
                "ResolveStackTrace",
                "LogKitEntry",
                "LogKitStats",
                "LogKitInteractionProvider",
                "LogKitHostEnvironment",
                "IEngineLoggerWithStackTrace",
                "saveLogInEditor",
                "editorFileName"
            };

            for (var rootIndex = 0; rootIndex < roots.Length; rootIndex++)
            {
                foreach (string path in Directory.GetFiles(roots[rootIndex], "*.cs", SearchOption.AllDirectories))
                {
                    string playerSource = CreatePlayerSource(File.ReadAllText(path));
                    for (var tokenIndex = 0; tokenIndex < forbiddenTokens.Length; tokenIndex++)
                    {
                        StringAssert.DoesNotContain(forbiddenTokens[tokenIndex], playerSource, path);
                    }
                }
            }
        }

        /// <summary>验证所有纯 Workbench 观察类型均使用统一整文件条件编译边界。</summary>
        [Test]
        public void WorkbenchObservationTypesUseWholeFileEditorGuards()
        {
            string runtimeRoot = Path.Combine(Application.dataPath, "YokiFrame", "Core", "Runtime", "LogKit");
            var sourcePaths = new List<string>();
            sourcePaths.AddRange(Directory.GetFiles(Path.Combine(runtimeRoot, "Diagnostics"), "*.cs"));
            sourcePaths.Add(Path.Combine(runtimeRoot, "Entries", "LogKitEntry.cs"));
            sourcePaths.Add(Path.Combine(runtimeRoot, "Hosting", "LogKitHostEnvironment.cs"));
            sourcePaths.Add(Path.Combine(runtimeRoot, "Settings", "LogKitSettings.Validation.cs"));
            sourcePaths.Add(Path.Combine(runtimeRoot, "Settings", "LogKitSettingsJsonReader.cs"));
            sourcePaths.Add(Path.Combine(runtimeRoot, "Settings", "LogKitSettingsPayloadParser.cs"));
            sourcePaths.Add(Path.Combine(
                Application.dataPath,
                "YokiFrame",
                "Core",
                "Editor",
                "LogKit",
                "Interaction",
                "LogKitInteractionProvider.cs"));

            for (var index = 0; index < sourcePaths.Count; index++)
            {
                string[] lines = File.ReadAllLines(sourcePaths[index]);
                Assert.AreEqual(OBSERVATION_DEFINE, FirstNonEmptyLine(lines), sourcePaths[index]);
                Assert.AreEqual("#endif", LastNonEmptyLine(lines), sourcePaths[index]);
            }
        }

        /// <summary>创建覆盖全部固定字段的有效设置对象，供公开 ApplyPayload 契约验证。</summary>
        private static string CreateCompleteSettingsPayload()
        {
            return "{\"enabled\":false,\"minimumLevel\":\"Error\""
                + ",\"saveLogInEditor\":true,\"saveLogInPlayer\":false"
                + ",\"enableIMGUIInPlayer\":false,\"enableEncryption\":false"
                + ",\"maxQueueSize\":4096,\"maxSameLogCount\":12"
                + ",\"maxRetentionDays\":7,\"maxFileSizeMB\":32"
                + ",\"imguiMaxLogCount\":100,\"logDirectory\":\"\""
                + ",\"editorFileName\":\"editor-custom.log\""
                + ",\"playerFileName\":\"player-custom.log\"}";
        }

        /// <summary>
        /// 记录 LogKit 转发到宿主日志后端的最后一次调用，便于验证门面输出。
        /// </summary>
        private sealed class RecordingLogger : IEngineLogger
        {
            public LogLevel LastLevel;
            public string LastMessage = string.Empty;
            public object LastContext;
            /// <summary>
            /// 记录普通宿主日志请求，证明业务输出不依赖 Workbench 堆栈接口。
            /// </summary>
            /// <param name="level">日志等级。</param>
            /// <param name="message">日志内容。</param>
            /// <param name="context">宿主上下文。</param>
            public void Log(LogLevel level, string message, object context = null)
            {
                LastLevel = level;
                LastMessage = message;
                LastContext = context;
            }
        }

        /// <summary>
        /// 按 Player 条件移除统一 Editor/Tools 代码块；其它宿主宏按已启用处理以同时覆盖 Unity 与 Godot。
        /// </summary>
        /// <param name="source">待预处理的源码。</param>
        /// <returns>模拟 Player 编译可见的源码。</returns>
        private static string CreatePlayerSource(string source)
        {
            var states = new Stack<(bool ParentIncluded, bool Condition)>();
            var builder = new StringBuilder(source.Length);
            bool included = true;
            using StringReader reader = new(source);
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                string trimmed = line.Trim();
                if (trimmed.StartsWith("#if ", StringComparison.Ordinal))
                {
                    bool condition = !string.Equals(trimmed, OBSERVATION_DEFINE, StringComparison.Ordinal);
                    states.Push((included, condition));
                    included = included && condition;
                }
                else if (trimmed == "#else")
                {
                    var state = states.Peek();
                    included = state.ParentIncluded && !state.Condition;
                }
                else if (trimmed == "#endif")
                {
                    included = states.Pop().ParentIncluded;
                }
                else if (included)
                {
                    builder.AppendLine(line);
                }
            }

            return builder.ToString();
        }

        /// <summary>读取第一条非空源码行。</summary>
        private static string FirstNonEmptyLine(string[] lines)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                if (!string.IsNullOrWhiteSpace(lines[index])) return lines[index].Trim();
            }

            return string.Empty;
        }

        /// <summary>读取最后一条非空源码行。</summary>
        private static string LastNonEmptyLine(string[] lines)
        {
            for (var index = lines.Length - 1; index >= 0; index--)
            {
                if (!string.IsNullOrWhiteSpace(lines[index])) return lines[index].Trim();
            }

            return string.Empty;
        }

        /// <summary>记录消息格式化次数，证明过滤路径不会触发用户对象代码。</summary>
        private sealed class CountingMessage
        {
            internal int FormatCount;

            /// <summary>记录格式化调用并返回稳定文本。</summary>
            /// <returns>测试消息。</returns>
            public override string ToString()
            {
                FormatCount++;
                return "counting-message";
            }
        }
    }
}
