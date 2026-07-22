using System;
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
        /// 通过反射调用 Unity 适配层入口，使测试能在 RED 阶段表达“程序集尚未落地”的失败。
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
    }
}
