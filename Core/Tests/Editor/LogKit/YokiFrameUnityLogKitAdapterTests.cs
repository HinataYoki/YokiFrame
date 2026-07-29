using System;
using System.Collections;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity LogKit 适配层通过独立程序集安装，并把 Core LogKit 输出转发给 Unity Debug。
    /// </summary>
    public sealed class YokiFrameUnityLogKitAdapterTests
    {
        private const int COMPILER_MESSAGE_MODES = 0x800 | 0x1000;
        private const string LOGKIT_CORE_PATH_FRAGMENT = "LogKit";

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
            InvokeUnityInstaller("Shutdown");
            LogKitHostEnvironment.Reset();
        }

        /// <summary>
        /// 验证 Unity Editor 在未写入第一条日志前已配置工具环境，同时仍保持默认 logger 的惰性创建。
        /// </summary>
        [Test]
        public void UnityEditorEnvironmentIsReadyBeforeDefaultLoggerCreation()
        {
            UnityYokiFrameEditorAdapterInstaller.ConfigureLogKitEnvironment();

            LogKitHostEnvironmentSnapshot environment = LogKitHostEnvironment.Capture();
            Assert.IsTrue(environment.SettingsApply);
            Assert.IsTrue(environment.FilePreview);
            Assert.IsTrue(environment.PlayerImGui);
            Assert.IsFalse(LogKit.HasLogger);
        }

        /// <summary>
        /// 验证 Unity Player 调试覆盖层位于独立 Runtime Adapter，使用 IMGUI 和主线程安全缓冲，不泄漏 UnityEditor API。
        /// </summary>
        [Test]
        public void UnityPlayerOverlayUsesRuntimeImGuiAndThreadSafeBuffer()
        {
            string overlaySource = ReadUnityRuntimeSource("UnityLogKitPlayerOverlay.cs");
            string loggerSource = ReadUnityRuntimeSource("UnityEngineLogger.cs");

            Assert.IsTrue(overlaySource.Contains("#if UNITY_5_3_OR_NEWER"));
            Assert.IsTrue(overlaySource.Contains("MonoBehaviour"));
            Assert.IsTrue(overlaySource.Contains("OnGUI()"));
            Assert.IsTrue(overlaySource.Contains("UnityLogKitPlayerLogBuffer"));
            Assert.IsTrue(overlaySource.Contains("SynchronizationContext"));
            Assert.IsFalse(overlaySource.Contains("UnityEditor"));
            Assert.IsTrue(loggerSource.Contains("UnityLogKitPlayerOverlay.Record"));
            Assert.IsTrue(loggerSource.Contains("[HideInCallstack]"));
            Assert.IsFalse(loggerSource.Contains("GetMethod("));
        }

        /// <summary>
        /// 验证 Unity Runtime Adapter 的实际覆盖层缓冲按容量保留最新日志，并且未变更时不重复构建显示文本。
        /// </summary>
        [Test]
        public void UnityPlayerOverlayBufferCapsEntriesAndBatchesTextRebuild()
        {
            Type bufferType = Type.GetType("YokiFrame.Unity.UnityLogKitPlayerLogBuffer, YokiFrame.Unity.Runtime");
            Assert.IsNotNull(bufferType, "缺少 Unity Player 覆盖层缓冲类型。");
            ConstructorInfo constructor = bufferType.GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(int) },
                null);
            MethodInfo record = bufferType.GetMethod(
                "Record",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(LogLevel), typeof(string) },
                null);
            MethodInfo tryBuildText = bufferType.GetMethod(
                "TryBuildText",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new[] { typeof(StringBuilder), typeof(string).MakeByRefType() },
                null);

            Assert.IsNotNull(constructor, "Unity Player 覆盖层缓冲缺少容量构造方法。");
            Assert.IsNotNull(record, "Unity Player 覆盖层缓冲缺少日志写入方法。");
            Assert.IsNotNull(tryBuildText, "Unity Player 覆盖层缓冲缺少批量文本构建方法。");
            object buffer = constructor.Invoke(new object[] { 2 });
            record.Invoke(buffer, new object[] { LogLevel.Debug, "first" });
            record.Invoke(buffer, new object[] { LogLevel.Info, "second" });
            record.Invoke(buffer, new object[] { LogLevel.Error, "third" });

            object[] buildArguments = { new StringBuilder(), null };
            bool changed = (bool)tryBuildText.Invoke(buffer, buildArguments);
            string text = buildArguments[1] as string;
            object[] unchangedArguments = { new StringBuilder(), null };
            bool unchanged = (bool)tryBuildText.Invoke(buffer, unchangedArguments);

            Assert.IsTrue(changed);
            StringAssert.DoesNotContain("first", text);
            StringAssert.Contains("[Info] second", text);
            StringAssert.Contains("[Error] third", text);
            Assert.IsFalse(unchanged);
        }

        /// <summary>
        /// 验证 Unity 适配层安装后，LogKit.Warning 会进入 Unity 日志系统。
        /// </summary>
        [Test]
        public void UnityRuntimeInstallerRoutesLogKitWarningsToUnityDebug()
        {
            InvokeUnityInstaller("Install");

            LogAssert.Expect(LogType.Warning, "unity-adapter-warning");
            LogKit.Warning("unity-adapter-warning");
        }

        /// <summary>
        /// 验证 LogKit 日志保留完整原生调用堆栈且不进入编译器日志通道。
        /// 双击跳转到业务调用点由 Editor 侧的 OnOpenAsset 路由器负责。
        /// </summary>
        [UnityTest]
        public IEnumerator UnityEditorLoggerKeepsNativeCallstackAndBusinessCallsite()
        {
            InvokeUnityInstaller("Install");

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
            StringAssert.Contains("LogKit", capturedStackTrace);
            Assert.IsTrue(
                TryFindConsoleEntry("unity-callsite", out ConsoleEntrySnapshot entry),
                "Unity Console 中缺少调用点测试日志条目。");
            StringAssert.Contains(LOGKIT_CORE_PATH_FRAGMENT, entry.File);
            Assert.AreEqual(
                0,
                entry.Mode & COMPILER_MESSAGE_MODES,
                "LogKit 日志不得进入编译器消息通道。");
        }

        /// <summary>
        /// 验证 OnOpenAsset 路由器能跳过 LogKit 包装帧、定位首个业务调用帧。
        /// </summary>
        [Test]
        public void ConsoleCallsiteRouterSkipsLogKitFramesToBusinessCallsite()
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
            StringAssert.Contains("Assets/Scripts/MyClass.cs", filePath);
            Assert.AreEqual(42, lineNumber);
        }

        /// <summary>
        /// 验证路由器对非 LogKit 帧不拦截。
        /// </summary>
        [Test]
        public void ConsoleCallsiteRouterIgnoresNonLogKitFrames()
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
        public void UnityRuntimeInstallerUsesUnityEngineLogger()
        {
            InvokeUnityInstaller("Install");

            IEngineLogger logger = LogKit.GetLogger();
            Assert.IsNotNull(logger);
            Assert.AreEqual("YokiFrame.Unity.UnityEngineLogger", logger.GetType().FullName);
            Assert.AreEqual("YokiFrame.Unity.Runtime", logger.GetType().Assembly.GetName().Name);
        }

        /// <summary>
        /// 通过反射调用 Unity 适配层入口，使测试能在 RED 阶段表达”程序集尚未落地”的失败。
        /// </summary>
        /// <param name="methodName">要调用的静态方法名。</param>
        private static void InvokeUnityInstaller(string methodName)
        {
            Type installerType = Type.GetType("YokiFrame.Unity.UnityLogKitRuntimeInstaller, YokiFrame.Unity.Runtime");
            Assert.IsNotNull(installerType, "缺少 Unity LogKit Runtime Adapter 程序集或安装器。");
            MethodInfo method = installerType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static, null, Type.EmptyTypes, null);
            Assert.IsNotNull(method, "Unity LogKit Runtime Adapter 缺少方法: " + methodName);
            method.Invoke(null, Array.Empty<object>());
        }

        /// <summary>
        /// 读取 Unity Runtime LogKit Adapter 的源码，确保测试验证的是实际独立程序集输入。
        /// </summary>
        /// <param name="fileName">要读取的 Runtime Adapter 文件名。</param>
        /// <returns>对应源码全文。</returns>
        private static string ReadUnityRuntimeSource(string fileName)
        {
            string sourcePath = Path.Combine(
                Application.dataPath,
                "YokiFrame",
                "Core",
                "Adapters",
                "Unity",
                "Runtime",
                "LogKit",
                fileName);
            Assert.IsTrue(File.Exists(sourcePath), "缺少 Unity LogKit Runtime Adapter 源码: " + sourcePath);
            return File.ReadAllText(sourcePath);
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
