using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Godot LogKit 适配层只在 Godot 编译上下文中启用，并提供与 Unity 适配层一致的安装入口。
    /// </summary>
    public sealed class YokiFrameGodotLogKitAdapterTests
    {
        private const string GODOT_DEFINE = "#if GODOT";

        /// <summary>
        /// 验证 Godot LogKit 适配源码位于 Godot Runtime Adapter 目录，避免把 Godot API 泄漏到 Core Runtime。
        /// </summary>
        [Test]
        public void GodotLogKitAdapterSourcesLiveUnderGodotRuntimeAdapter()
        {
            string adapterRoot = GetGodotLogKitAdapterRoot();

            Assert.IsTrue(Directory.Exists(adapterRoot), "缺少 Godot LogKit Runtime Adapter 目录。");
            AssertGodotAdapterFile(Path.Combine(adapterRoot, "GodotEngineLogger.cs"));
            AssertGodotAdapterFile(Path.Combine(adapterRoot, "GodotLogKitRuntimeInstaller.cs"));
            AssertGodotAdapterFile(Path.Combine(adapterRoot, "GodotLogKitPlayerOverlay.cs"));
        }

        /// <summary>
        /// 验证 Godot 日志后端按 LogKit 等级转发到 Godot GD 日志 API。
        /// </summary>
        [Test]
        public void GodotEngineLoggerRoutesLogLevelsToGdMethods()
        {
            string source = ReadRequiredSource("GodotEngineLogger.cs");

            Assert.IsTrue(source.Contains("GodotEngineLogger : IEngineLogger"), "Godot 日志后端必须只实现业务日志接口。");
            Assert.IsFalse(source.Contains("IEngineLoggerWithStackTrace"), "Godot Player 不应携带 Workbench 堆栈接口调用。");
            Assert.IsTrue(source.Contains("GD.Print("), "Godot Debug / Info 日志应转发到 GD.Print。");
            Assert.IsTrue(source.Contains("GD.PushWarning("), "Godot Warning 日志应转发到 GD.PushWarning。");
            Assert.IsTrue(source.Contains("GD.PushError("), "Godot Error 日志应转发到 GD.PushError。");
        }

        /// <summary>
        /// 验证 Godot 安装器在模块加载时只注册惰性 logger 工厂，并保留显式覆盖和关闭能力。
        /// </summary>
        [Test]
        public void GodotRuntimeInstallerRegistersLazyLoggerFactory()
        {
            string source = ReadRequiredSource("GodotLogKitRuntimeInstaller.cs");

            Assert.IsTrue(source.Contains("GodotLogKitRuntimeInstaller"), "Godot LogKit 适配层缺少安装器。");
            Assert.IsTrue(source.Contains("ModuleInitializer"), "Godot LogKit 必须在 Adapter 程序集加载时注册默认工厂。");
            Assert.IsTrue(source.Contains("LogKit.RegisterDefaultLoggerFactory"), "Godot 安装器必须只注册惰性 logger 工厂。");
            Assert.IsTrue(source.Contains("LogKit.SetLogger(finalLogger)"), "Godot 安装器必须把 Godot 后端注入 Core LogKit。");
            Assert.IsTrue(source.Contains("LogKitSettings.ApplyBaseRuntimeSettings()"), "Godot 安装器必须同步 LogKit 基础设置。");
            Assert.IsTrue(source.Contains("LogKitSettings.RuntimeSettingsApplied"), "Godot 安装器必须响应 Runtime Settings 以更新 Player 调试覆盖层。");
            Assert.IsTrue(source.Contains("AttachPlayerOverlay"), "Godot 安装器必须提供由 Bootstrap 挂载 Player 覆盖层的入口。");
            Assert.IsTrue(source.Contains("Shutdown()"), "Godot 安装器必须提供关闭入口，方便测试或用户替换后端。");
            Assert.IsFalse(source.Contains("LogKitHostEnvironment"), "Godot Runtime logger 安装器不应持有 Tools 文件预览环境。");
        }

        /// <summary>
        /// 验证 Godot Player 覆盖层使用原生 CanvasLayer/Control，而不是把 Unity IMGUI 或 Tools 代码带入导出运行时。
        /// </summary>
        [Test]
        public void GodotPlayerOverlayUsesRuntimeControlAndBoundedBuffer()
        {
            string source = ReadRequiredSource("GodotLogKitPlayerOverlay.cs");
            string loggerSource = ReadRequiredSource("GodotEngineLogger.cs");

            Assert.IsTrue(source.Contains("CanvasLayer"), "Godot Player 覆盖层必须挂在 CanvasLayer 下。");
            Assert.IsTrue(source.Contains("RichTextLabel"), "Godot Player 覆盖层必须使用原生 Control 输出日志。");
            Assert.IsTrue(source.Contains("GodotLogKitPlayerLogBuffer"), "Godot Player 覆盖层必须使用有界纯 C# 缓冲。");
            Assert.IsTrue(source.Contains("ProcessPendingSettings"), "后台设置变更必须延迟到 Godot 主线程应用。");
            Assert.IsFalse(source.Contains("TOOLS"), "Godot Player 覆盖层不得依赖 Tools 编译条件。");
            Assert.IsTrue(loggerSource.Contains("GodotLogKitPlayerOverlay.Record"), "Godot LogKit logger 必须把已过滤日志送入覆盖层缓冲。");
        }

        /// <summary>
        /// 验证 Godot Tools 仅在 Bootstrap 的 Tools 分支配置 LogKit 工具环境，Player 不携带该职责。
        /// </summary>
        [Test]
        public void GodotBootstrapOwnsToolsOnlyLogKitEnvironment()
        {
            string bootstrapSource = ReadRequiredSource("../GodotBootstrap.cs");

            Assert.IsTrue(bootstrapSource.Contains("#if GODOT && TOOLS"), "Godot Tools 环境必须使用完整工具宏包裹。");
            Assert.IsTrue(bootstrapSource.Contains("ConfigureToolingLogKitEnvironment();"), "Godot Bootstrap 必须在 Tools 运行时配置 LogKit 环境。");
            Assert.IsTrue(bootstrapSource.Contains("LogKitHostEnvironment.Configure("), "Godot Tools 环境必须提供文件位置和真实 capability。");
            Assert.IsTrue(bootstrapSource.Contains("LogKitHostEnvironment.Reset();"), "Godot Tools 退出时必须清理 LogKit 环境。");
            Assert.IsTrue(bootstrapSource.Contains("GodotLogKitPlayerOverlay.ProcessPendingSettings();"), "Godot Bootstrap 必须在主线程应用待处理 Player 覆盖层设置。");
        }

        /// <summary>
        /// 验证 Godot Bootstrap 只注册各 Kit 默认工厂，Runtime Settings 由 Adapter 从当前项目读取。
        /// </summary>
        [Test]
        public void GodotBootstrapRegistersLazyProjectSettingsBackend()
        {
            string bootstrapSource = ReadRequiredSource("../GodotBootstrap.cs");
            string loaderSource = ReadRequiredSource("../Settings/GodotYokiFrameRuntimeSettingsLoader.cs");

            Assert.IsTrue(
                bootstrapSource.Contains("GodotYokiFrameRuntimeSettingsInstaller.EnsureInstalled()"),
                "Godot Bootstrap 必须注册 ProjectSettings Store 工厂。");
            Assert.IsFalse(bootstrapSource.Contains("YokiFrameKit.Initialize"), "Godot Bootstrap 不应要求全局初始化入口。");
            Assert.IsTrue(loaderSource.Contains("ProjectSettings.HasSetting"), "Godot 配置必须复用项目自身 ProjectSettings。");
            Assert.IsTrue(loaderSource.Contains("yokiframe/runtime/"), "Godot 配置必须使用隔离的 yokiframe/runtime 命名空间。");
        }

        /// <summary>
        /// 获取 Godot LogKit Runtime Adapter 的绝对目录路径。
        /// </summary>
        /// <returns>Godot LogKit Runtime Adapter 目录。</returns>
        private static string GetGodotLogKitAdapterRoot()
        {
            return Path.Combine(Application.dataPath, "YokiFrame", "Core", "Adapters", "Godot", "Runtime", "LogKit");
        }

        /// <summary>
        /// 读取指定 Godot LogKit 适配源码，并在文件缺失时给出清晰断言。
        /// </summary>
        /// <param name="fileName">源码文件名。</param>
        /// <returns>源码文本。</returns>
        private static string ReadRequiredSource(string fileName)
        {
            string sourcePath = Path.GetFullPath(Path.Combine(GetGodotLogKitAdapterRoot(), fileName));
            Assert.IsTrue(File.Exists(sourcePath), "缺少 Godot LogKit 适配源码: " + sourcePath);
            return File.ReadAllText(sourcePath);
        }

        /// <summary>
        /// 验证 Godot 适配源码文件存在、整文件由 GODOT 宏包裹，并且 Godot API 引用只出现在该宏内部。
        /// </summary>
        /// <param name="sourcePath">待验证的源码文件路径。</param>
        private static void AssertGodotAdapterFile(string sourcePath)
        {
            Assert.IsTrue(File.Exists(sourcePath), "缺少 Godot LogKit 适配源码: " + sourcePath);

            string[] lines = File.ReadAllLines(sourcePath);
            Assert.AreEqual(GODOT_DEFINE, FirstNonEmptyLine(lines), "Godot 适配源码必须从 GODOT 宏开始: " + sourcePath);
            Assert.AreEqual("#endif", LastNonEmptyLine(lines), "Godot 适配源码必须用 #endif 结束: " + sourcePath);

            string source = File.ReadAllText(sourcePath);
            if (source.Contains("using Godot;"))
            {
                Assert.AreEqual(GODOT_DEFINE, FirstNonEmptyLine(lines), "Godot API 引用必须只出现在 GODOT 宏包裹的文件中: " + sourcePath);
            }
        }

        /// <summary>
        /// 从行数组中获取第一条非空行。
        /// </summary>
        /// <param name="lines">源码行数组。</param>
        /// <returns>第一条非空行；不存在时返回空字符串。</returns>
        private static string FirstNonEmptyLine(string[] lines)
        {
            for (var index = 0; index < lines.Length; index++)
            {
                string line = lines[index].Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 从行数组中获取最后一条非空行。
        /// </summary>
        /// <param name="lines">源码行数组。</param>
        /// <returns>最后一条非空行；不存在时返回空字符串。</returns>
        private static string LastNonEmptyLine(string[] lines)
        {
            for (var index = lines.Length - 1; index >= 0; index--)
            {
                string line = lines[index].Trim();
                if (line.Length > 0)
                {
                    return line;
                }
            }

            return string.Empty;
        }
    }
}
