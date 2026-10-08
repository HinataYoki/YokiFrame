using System;
using System.Collections;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YokiFrame.Unity;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity LogKit 适配层通过独立程序集安装，并把 Core LogKit 输出转发给 Unity Debug。
    /// </summary>
    public sealed class YokiFrameUnityLogKitAdapterTests
    {
        private const int COMPILER_MESSAGE_MODES = 0x800 | 0x1000;

        /// <summary>
        /// 每个测试前清空 Core LogKit 状态，避免其它测试注入的后端影响 Unity 适配层。
        /// </summary>
        [SetUp]
        public void SetUp()
        {
            KitSettings.Reset();
            LogKit.Reset();
            LogKitSettings.ResetToDefaults();
        }

        /// <summary>
        /// 每个测试后关闭 Unity 适配层，避免后端跨测试保留。
        /// </summary>
        [TearDown]
        public void TearDown()
        {
            UnityLogKitRuntimeInstaller.Shutdown();
            LogKitHostEnvironment.Reset();
        }

        /// <summary>
        /// 验证 Unity Editor 在未写入第一条日志前已配置工具环境，同时仍保持默认 logger 的惰性创建。
        /// </summary>
        [Test]
        public void ConfigureLogKitEnvironment_尚未写日志_已配置工具环境且不创建默认Logger()
        {
            UnityYokiFrameEditorAdapterInstaller.ConfigureLogKitEnvironment();

            LogKitHostEnvironmentSnapshot environment = LogKitHostEnvironment.Capture();
            Assert.IsTrue(environment.SettingsApply);
            Assert.IsTrue(environment.FilePreview);
            Assert.IsTrue(environment.PlayerImGui);
            Assert.IsFalse(LogKit.HasLogger);
        }


        /// <summary>
        /// 验证覆盖层缓冲超过容量时只保留最新日志，且未变更时不再次构建文本。
        /// </summary>
        [Test]
        public void TryBuildText_容量为2_保留最新两条且未变更时不重建()
        {
            var buffer = new Unity.UnityLogKitPlayerLogBuffer(2);
            buffer.Record(LogLevel.Debug, "first");
            buffer.Record(LogLevel.Info, "second");
            buffer.Record(LogLevel.Error, "third");

            var builder = new StringBuilder();
            Assert.IsTrue(buffer.TryBuildText(builder, out string text));
            Assert.AreEqual(
                "[Info] second" + Environment.NewLine + "[Error] third" + Environment.NewLine,
                text);
            Assert.IsFalse(buffer.TryBuildText(new StringBuilder(), out _));
        }

        /// <summary>
        /// 验证 Unity 适配层安装后，LogKit.Warning 会进入 Unity 日志系统。
        /// </summary>
        [Test]
        public void Warning_已安装运行时适配_写入Unity警告日志()
        {
            UnityLogKitRuntimeInstaller.Install();

            LogAssert.Expect(LogType.Warning, "unity-adapter-warning");
            LogKit.Warning("unity-adapter-warning");
        }

        /// <summary>
        /// 验证 LogKit 警告保留宿主原生堆栈，且不进入编译器消息通道。
        /// UnityEditor.LogEntries 与 LogEntry 是 Unity 内部 API，没有公开读取入口。
        /// 只反射这些 Unity 内部成员，不反射本仓库 private。
        /// </summary>
        [UnityTest]
        public IEnumerator UnityEditorLogger_反射读取UnityConsole内部条目_保留原生堆栈且非编译器通道()
        {
            UnityLogKitRuntimeInstaller.Install();

            string capturedStackTrace = null;
            Application.LogCallback callback = (message, stackTrace, type) =>
            {
                if (type == LogType.Warning && message.StartsWith("unity-callsite", StringComparison.Ordinal))
                {
                    capturedStackTrace = stackTrace;
                }
            };

            Application.logMessageReceived += callback;
            try
            {
                LogAssert.Expect(LogType.Warning, "unity-callsite");
                LogKit.Warning("unity-callsite");
                yield return null;
            }
            finally
            {
                Application.logMessageReceived -= callback;
            }

            Assert.IsFalse(string.IsNullOrEmpty(capturedStackTrace), "LogKit 日志必须保留宿主原生调用堆栈。");
            StringAssert.Contains("UnityEngine.Logger.Log", capturedStackTrace);
            StringAssert.Contains("YokiFrameUnityLogKitAdapterTests.cs", capturedStackTrace);
            StringAssert.DoesNotContain("UnityEngineLogger:Log", capturedStackTrace);
            Assert.IsTrue(
                TryFindConsoleEntry("unity-callsite", out ConsoleEntrySnapshot entry),
                "Unity Console 中缺少调用点测试日志条目。");
            Assert.IsTrue(entry.File.EndsWith("YokiFrameUnityLogKitAdapterTests.cs", StringComparison.OrdinalIgnoreCase), entry.File);
            Assert.AreEqual(
                0,
                entry.Mode & COMPILER_MESSAGE_MODES,
                "LogKit 日志不得进入编译器消息通道。");
        }

        /// <summary>
        /// 验证 OnOpenAsset 路由器能跳过 LogKit 包装帧、定位首个业务调用帧。
        /// </summary>
        [Test]
        public void TryResolveCallsiteFromText_请求帧是LogKit包装层_定位业务文件和行号()
        {
            const string WRAPPER_FILE = "Assets/YokiFrame/Core/Runtime/LogKit/Facade/LogKit.Write.cs";
            const int WRAPPER_LINE = 236;
            string activeText = string.Join("\n", new[]
            {
                "unity-callsite",
                "UnityEngine.Debug:LogWarning (object,UnityEngine.Object)",
                "YokiFrame.Unity.UnityEngineLogger:WriteToUnity (YokiFrame.LogLevel,string,UnityEngine.Object)",
                "YokiFrame.Unity.UnityEngineLogger:Log (YokiFrame.LogLevel,string,object)",
                "YokiFrame.LogKit:WriteToLogger () (at " + WRAPPER_FILE + ":" + WRAPPER_LINE + ")",
                "YokiFrame.LogKit:Write () (at " + WRAPPER_FILE + ":137)",
                "YokiFrame.LogKit:Warning () (at Assets/YokiFrame/Core/Runtime/LogKit/Facade/LogKit.cs:223)",
                "MyNamespace.MyClass:MyMethod () (at Assets/Scripts/MyClass.cs:42)"
            });

            bool result = UnityLogKitConsoleCallsiteRouter.TryResolveCallsiteFromText(
                WRAPPER_FILE, WRAPPER_LINE, activeText, out string filePath, out int lineNumber);

            Assert.IsTrue(result, "路由器应当找到业务调用点。");
            Assert.AreEqual("Assets/Scripts/MyClass.cs", filePath);
            Assert.AreEqual(42, lineNumber);
        }

        /// <summary>
        /// 验证 Unity 2022.3 首先暴露 UnityEngineLogger 源码行时，路由器仍能跳过完整包装链。
        /// </summary>
        [Test]
        public void TryResolveCallsiteFromText_请求帧是Unity适配层_定位业务文件和行号()
        {
            const string ADAPTER_FILE = "Assets/YokiFrame/Core/Adapters/Unity/Runtime/LogKit/UnityEngineLogger.cs";
            const int ADAPTER_LINE = 44;
            string activeText = string.Join("\n", new[]
            {
                "unity-callsite",
                "UnityEngine.Debug:LogWarning (object,UnityEngine.Object)",
                "YokiFrame.Unity.UnityEngineLogger:WriteToUnity (YokiFrame.LogLevel,string,UnityEngine.Object) (at " + ADAPTER_FILE + ":44)",
                "YokiFrame.Unity.UnityEngineLogger:Log (YokiFrame.LogLevel,string,object) (at " + ADAPTER_FILE + ":23)",
                "YokiFrame.LogKit:WriteToLogger () (at Assets/YokiFrame/Core/Runtime/LogKit/Facade/LogKit.Write.cs:234)",
                "YokiFrame.LogKit:Write () (at Assets/YokiFrame/Core/Runtime/LogKit/Facade/LogKit.Write.cs:137)",
                "YokiFrame.Samples.LogKitCallsiteTest:WriteCallsiteTestLog () (at Assets/Scripts/LogKitCallsiteTest.cs:34)"
            });

            bool result = UnityLogKitConsoleCallsiteRouter.TryResolveCallsiteFromText(
                ADAPTER_FILE,
                ADAPTER_LINE,
                activeText,
                out string filePath,
                out int lineNumber);

            Assert.IsTrue(result, "Unity 2022.3 的适配层首帧应当被路由器识别为 LogKit 包装层。");
            Assert.AreEqual("Assets/Scripts/LogKitCallsiteTest.cs", filePath);
            Assert.AreEqual(34, lineNumber);
        }

        /// <summary>
        /// 验证路由器对非 LogKit 帧不拦截。
        /// </summary>
        [Test]
        public void TryResolveCallsiteFromText_非LogKit帧_不拦截()
        {
            const string OTHER_FILE = "Assets/Scripts/OtherClass.cs";
            const int OTHER_LINE = 10;
            string activeText = string.Join("\n", new[]
            {
                "some-message",
                "MyNamespace.MyClass:MyMethod () (at " + OTHER_FILE + ":" + OTHER_LINE + ")",
                "MyNamespace.Other:OtherMethod () (at Assets/Scripts/Other.cs:20)"
            });

            bool result = UnityLogKitConsoleCallsiteRouter.TryResolveCallsiteFromText(
                OTHER_FILE, OTHER_LINE, activeText, out _, out _);

            Assert.IsFalse(result, "非 LogKit 帧不应被路由器拦截。");
        }

        /// <summary>
        /// 验证 Unity 适配层注入的后端类型来自独立 Unity Runtime 程序集。
        /// </summary>
        [Test]
        public void Install_已安装运行时适配_注入UnityEngineLogger()
        {
            UnityLogKitRuntimeInstaller.Install();

            IEngineLogger logger = LogKit.GetLogger();
            Assert.IsNotNull(logger);
            Assert.AreEqual("YokiFrame.Unity.UnityEngineLogger", logger.GetType().FullName);
            Assert.AreEqual("YokiFrame.Unity.Runtime", logger.GetType().Assembly.GetName().Name);
        }


        /// <summary>
        /// 从 Unity Console 的原生条目中查找指定消息，读取 Unity 解析后的文件、行号和通道元数据。
        /// </summary>
        /// <param name="message">要匹配的日志正文。</param>
        /// <param name="entry">找到的 Console 条目元数据。</param>
        /// <returns>找到匹配条目时返回 true。</returns>
        private static bool TryFindConsoleEntry(string message, out ConsoleEntrySnapshot entry)
        {
            entry = default;
            ConsoleEntryReflection reflection = ResolveConsoleEntryReflection();
            int count = (int)reflection.GetCount.Invoke(null, Array.Empty<object>());
            reflection.StartGettingEntries.Invoke(null, Array.Empty<object>());
            try
            {
                for (int index = count - 1; index >= 0; index--)
                {
                    if (TryReadConsoleEntry(reflection, index, message, out entry))
                    {
                        return true;
                    }
                }
            }
            finally
            {
                reflection.EndGettingEntries.Invoke(null, Array.Empty<object>());
            }

            return false;
        }

        /// <summary>
        /// 解析当前 Unity 版本读取 Console 条目所需的内部类型、方法和字段。
        /// </summary>
        /// <returns>已完成成员校验的 Console 反射描述。</returns>
        private static ConsoleEntryReflection ResolveConsoleEntryReflection()
        {
            Assembly unityEditorAssembly = Assembly.GetAssembly(typeof(UnityEditor.Editor));
            Assert.IsNotNull(unityEditorAssembly, "UnityEditor 程序集不可用。");
            Type logEntriesType = unityEditorAssembly.GetType("UnityEditor.LogEntries")
                ?? unityEditorAssembly.GetType("UnityEditorInternal.LogEntries");
            Type logEntryType = unityEditorAssembly.GetType("UnityEditor.LogEntry")
                ?? unityEditorAssembly.GetType("UnityEditorInternal.LogEntry");
            Assert.IsNotNull(logEntriesType, "UnityEditor.LogEntries 类型不可用。");
            Assert.IsNotNull(logEntryType, "UnityEditor.LogEntry 类型不可用。");

            ConsoleEntryReflection reflection = new ConsoleEntryReflection
            {
                LogEntryType = logEntryType,
                GetCount = FindStaticMethod(logEntriesType, "GetCount", Type.EmptyTypes),
                GetEntry = FindStaticMethod(logEntriesType, "GetEntryInternal", new[] { typeof(int), logEntryType }),
                StartGettingEntries = FindStaticMethod(logEntriesType, "StartGettingEntries", Type.EmptyTypes),
                EndGettingEntries = FindStaticMethod(logEntriesType, "EndGettingEntries", Type.EmptyTypes),
                Message = FindInstanceField(logEntryType, "message"),
                File = FindInstanceField(logEntryType, "file"),
                Line = FindInstanceField(logEntryType, "line"),
                Mode = FindInstanceField(logEntryType, "mode")
            };
            Assert.IsNotNull(reflection.GetCount, "UnityEditor.LogEntries 缺少 GetCount 方法。");
            Assert.IsNotNull(reflection.GetEntry, "UnityEditor.LogEntries 缺少 GetEntryInternal 方法。");
            Assert.IsNotNull(reflection.StartGettingEntries, "UnityEditor.LogEntries 缺少 StartGettingEntries 方法。");
            Assert.IsNotNull(reflection.EndGettingEntries, "UnityEditor.LogEntries 缺少 EndGettingEntries 方法。");
            Assert.IsNotNull(reflection.Message, "UnityEditor.LogEntry 缺少 message 字段。");
            Assert.IsNotNull(reflection.File, "UnityEditor.LogEntry 缺少 file 字段。");
            Assert.IsNotNull(reflection.Line, "UnityEditor.LogEntry 缺少 line 字段。");
            Assert.IsNotNull(reflection.Mode, "UnityEditor.LogEntry 缺少 mode 字段。");
            return reflection;
        }

        /// <summary>
        /// 按固定绑定标志查找 Console 的静态内部方法。
        /// </summary>
        /// <param name="ownerType">声明方法的 Unity 内部类型。</param>
        /// <param name="methodName">方法名。</param>
        /// <param name="parameterTypes">参数类型列表。</param>
        /// <returns>找到的方法；不存在时返回 null。</returns>
        private static MethodInfo FindStaticMethod(Type ownerType, string methodName, Type[] parameterTypes)
        {
            return ownerType.GetMethod(
                methodName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null,
                parameterTypes,
                null);
        }

        /// <summary>
        /// 按固定绑定标志查找 Console 条目的实例字段。
        /// </summary>
        /// <param name="ownerType">声明字段的 Unity 内部类型。</param>
        /// <param name="fieldName">字段名。</param>
        /// <returns>找到的字段；不存在时返回 null。</returns>
        private static FieldInfo FindInstanceField(Type ownerType, string fieldName)
        {
            return ownerType.GetField(
                fieldName,
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        }

        /// <summary>
        /// 读取并匹配单条 Console 记录，返回 Unity 已解析的文件、行号和通道元数据。
        /// </summary>
        /// <param name="reflection">Console 反射描述。</param>
        /// <param name="index">Console 条目索引。</param>
        /// <param name="message">要匹配的日志正文前缀。</param>
        /// <param name="entry">匹配条目的元数据。</param>
        /// <returns>找到匹配条目时返回 true。</returns>
        private static bool TryReadConsoleEntry(
            ConsoleEntryReflection reflection,
            int index,
            string message,
            out ConsoleEntrySnapshot entry)
        {
            entry = default;
            object candidate = Activator.CreateInstance(reflection.LogEntryType, true);
            bool retrieved = (bool)reflection.GetEntry.Invoke(null, new[] { (object)index, candidate });
            string candidateMessage = reflection.Message.GetValue(candidate) as string;
            string normalizedCandidateMessage = candidateMessage == null
                ? string.Empty
                : candidateMessage.TrimEnd('\r', '\n');
            if (!retrieved || !normalizedCandidateMessage.StartsWith(message, StringComparison.Ordinal))
            {
                return false;
            }

            entry = new ConsoleEntrySnapshot
            {
                File = reflection.File.GetValue(candidate) as string ?? string.Empty,
                Line = (int)reflection.Line.GetValue(candidate),
                Mode = (int)reflection.Mode.GetValue(candidate)
            };
            return true;
        }

        /// <summary>
        /// 承载单条 Unity Console 条目中与调用点定位相关的元数据。
        /// </summary>
        private struct ConsoleEntrySnapshot
        {
            public string File;
            public int Line;
            public int Mode;
        }

        /// <summary>
        /// 保存 Unity Console 反射读取所需的最小成员集合。
        /// </summary>
        private sealed class ConsoleEntryReflection
        {
            public Type LogEntryType;
            public MethodInfo GetCount;
            public MethodInfo GetEntry;
            public MethodInfo StartGettingEntries;
            public MethodInfo EndGettingEntries;
            public FieldInfo Message;
            public FieldInfo File;
            public FieldInfo Line;
            public FieldInfo Mode;
        }
    }
}
