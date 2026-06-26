#if !GODOT
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.ShortcutManagement;
using UnityEngine;

[assembly: InternalsVisibleTo("YokiFrame.Unity.Tests")]

namespace YokiFrame.Unity
{
    /// <summary>
    /// Tauri 进程启动器 — 管理 Tauri App 的生命周期。
    ///
    /// v2（文件 I/O 版本）：通信通过 .yokiframe/ 文件系统完成，无 WebSocket。
    /// 启动时设置 YOKI_YOKIFRAME_DIR env，Tauri 端点通过文件监视器读取 unity 状态。
    ///
        /// 启动策略：
        ///   1. 编辑器开发期优先启动最新二进制。
        ///   2. 二进制缺失或源码更新时直接拉起源码窗口，不在打开窗口时自动 release build。
        ///   3. 发布准备时通过 Build Tauri Binary / Packager 显式生成 release 产物。
    /// </summary>
    [InitializeOnLoad]
    public static partial class TauriLauncher
    {
        internal enum LaunchTarget
        {
            Binary,
            Source,
            Unavailable
        }

        public enum TauriRuntimePlatform
        {
            Windows,
            MacOS,
            Linux
        }

        /// <summary>跨平台编译目标。</summary>
        public enum CrossPlatformTarget
        {
            WinX64,
            MacosArm64,
            MacosX64,
            LinuxX64
        }

        private static System.Diagnostics.Process sTauriProcess;
        private static bool sOutdatedBinaryWarningShown;
        private static bool sPreloadAttempted;
        private static DateTime sEditorLoadUtc;

        private const string PANEL_REQUEST_DIR = "panel";
        private const string PANEL_SHOW_REQUEST_FILE = "show-window.json";
        internal const string AUTO_PRELOAD_PREF_KEY = "YokiFrame.TauriLauncher.AutoPreload";
        private const int ASSET_REFRESH_INTERVAL_SEC = 2;
        private const int AUTO_PRELOAD_DELAY_SEC = 3;
        internal const string CARGO_TARGET_DIR_NAME = "target-codex";
        private const string DETACHED_BINARY_ROOT_DIR = "YokiFrame/TauriRuntime/bin";

        /// <summary>本源文件编译期绝对路径。由 CallerFilePath 烘焙。</summary>
        private static string sSourceFilePath;

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string ResolveSourceFilePath([CallerFilePath] string path = "") => path;

        private static string SourceFilePath =>
            sSourceFilePath ??= ResolveSourceFilePath();

        private static string BridgeDir => Path.GetDirectoryName(SourceFilePath);

        private const string TOOLS_SOURCE_ROOT_DIR = "YokiFrameTools";

        private static string ToolsSourceRootPath =>
            Path.GetFullPath(Path.Combine(ProjectRootDir, TOOLS_SOURCE_ROOT_DIR));

        private static string TauriProjectPath =>
            Path.Combine(ToolsSourceRootPath, "TauriEditor");

        internal static string SrcTauriPath =>
            Path.Combine(TauriProjectPath, "src-tauri");

        private static string PackageRootDir =>
            Path.GetFullPath(Path.Combine(BridgeDir, "..", "..", "..", "..", "..", ".."));

        internal static string RuntimeDir =>
            Path.GetFullPath(Path.Combine(PackageRootDir, "TauriRuntime~"));

        private static string ProjectRootDir =>
            Path.GetFullPath(Path.Combine(PackageRootDir, "..", ".."));

        private static string UnityProjectRootDir
        {
            get
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                return string.IsNullOrEmpty(projectRoot) ? ProjectRootDir : projectRoot;
            }
        }

        /// <summary>安装器 Tauri 项目路径。</summary>
        internal static string InstallerProjectPath =>
            Path.Combine(ToolsSourceRootPath, "Installer", "YokiFramePackageTool", "src-tauri");

        internal static string InstallerPublishedRoot =>
            Path.GetFullPath(Path.Combine(PackageRootDir, "Installer~"));

        internal static bool IsInstallerDevMode => Directory.Exists(InstallerProjectPath);

        internal static TauriRuntimePlatform CurrentRuntimePlatform
        {
            get
            {
#if UNITY_EDITOR_WIN
                return TauriRuntimePlatform.Windows;
#elif UNITY_EDITOR_OSX
                return TauriRuntimePlatform.MacOS;
#else
                return TauriRuntimePlatform.Linux;
#endif
            }
        }

        internal static string PublishedBinaryPath =>
            ResolvePublishedBinaryPath(RuntimeDir, CurrentRuntimePlatform);

        internal static string DevBinaryPath =>
            ResolveDevBinaryPath(SrcTauriPath, CurrentRuntimePlatform);

        internal static bool IsDevMode => Directory.Exists(SrcTauriPath);

        internal static string BinaryPath =>
            IsDevMode ? DevBinaryPath : PublishedBinaryPath;

        internal static string RuntimeDistPath => Path.Combine(RuntimeDir, "dist");

        internal static string DistPath =>
            RuntimeDistPath;

        internal static string ResolvePublishedBinaryPath(string runtimeDir, TauriRuntimePlatform platform)
        {
            if (platform == TauriRuntimePlatform.MacOS)
            {
                return Path.Combine(
                    runtimeDir,
                    "yokiframe-tauri-editor.app",
                    "Contents",
                    "MacOS",
                    "yokiframe-tauri-editor");
            }

            if (platform == TauriRuntimePlatform.Windows)
                return Path.Combine(runtimeDir, "yokiframe-tauri-editor.exe");

            return Path.Combine(runtimeDir, "yokiframe-tauri-editor");
        }

        internal static string ResolveDevBinaryPath(string srcTauriPath, TauriRuntimePlatform platform)
        {
            var releaseDir = Path.Combine(srcTauriPath, CARGO_TARGET_DIR_NAME, "release");
            if (platform == TauriRuntimePlatform.Windows)
                return Path.Combine(releaseDir, "yokiframe-tauri-editor.exe");

            return Path.Combine(releaseDir, "yokiframe-tauri-editor");
        }

        internal static string ResolvePublishedAppBundlePath(string runtimeDir, TauriRuntimePlatform platform)
        {
            if (platform != TauriRuntimePlatform.MacOS)
                return string.Empty;

            return Path.Combine(runtimeDir, "yokiframe-tauri-editor.app");
        }

        internal static string ResolveDevAppBundlePath(string srcTauriPath, TauriRuntimePlatform platform)
        {
            if (platform != TauriRuntimePlatform.MacOS)
                return string.Empty;

            return Path.Combine(
                srcTauriPath,
                CARGO_TARGET_DIR_NAME,
                "release",
                "bundle",
                "macos",
                "YokiFrame Editor.app");
        }

        static TauriLauncher()
        {
            sEditorLoadUtc = DateTime.UtcNow;
            EditorApplication.quitting += OnEditorQuitting;
            EditorApplication.update += OnEditorUpdate;
        }

        public static async void BuildCurrentPlatform()
        {
            await BuildForPlatformAsync(CurrentRuntimePlatform);
        }

        public static async void BuildWinX64()
        {
            await BuildCrossPlatformAsync(CrossPlatformTarget.WinX64);
        }

        public static async void BuildMacosArm64()
        {
            await BuildCrossPlatformAsync(CrossPlatformTarget.MacosArm64);
        }

        public static async void BuildMacosX64()
        {
            await BuildCrossPlatformAsync(CrossPlatformTarget.MacosX64);
        }

        public static async void BuildLinuxX64()
        {
            await BuildCrossPlatformAsync(CrossPlatformTarget.LinuxX64);
        }

        public static async void BuildAllPlatforms()
        {
            if (!Directory.Exists(SrcTauriPath))
            {
                EditorUtility.DisplayDialog("编译失败",
                    $"Tauri 项目不存在: {SrcTauriPath}", "确定");
                return;
            }

            var targets = GetBuildableCrossPlatformTargets(CurrentRuntimePlatform);
            if (targets.Length == 0)
            {
                var message = GetUnsupportedCrossPlatformBuildMessage(CrossPlatformTarget.WinX64, CurrentRuntimePlatform);
                EditorUtility.DisplayDialog("编译失败", message, "确定");
                return;
            }

            var total = targets.Length;
            var succeeded = 0;
            var failed = new List<string>();

            for (var i = 0; i < total; i++)
            {
                var target = targets[i];
                var progress = (float)i / total;
                EditorUtility.DisplayProgressBar("YokiFrame",
                    $"正在编译 {GetCrossPlatformTargetName(target)} ({i + 1}/{total})...", progress);

                try
                {
                    var (success, _) = await RunCargoForCrossPlatformAsync(target);
                    if (success)
                    {
                        CopyCrossPlatformArtifact(target);
                        succeeded++;
                    }
                    else
                    {
                        failed.Add(GetCrossPlatformTargetName(target));
                    }
                }
                catch (Exception e)
                {
                    LogKit.Error($"[TauriLauncher] Tauri Editor 发布复制失败 ({GetCrossPlatformTargetName(target)}): {e.Message}");
                    failed.Add(GetCrossPlatformTargetName(target));
                }
            }

            EditorUtility.ClearProgressBar();

            if (failed.Count == 0)
            {
                var skipped = GetSkippedCrossPlatformTargetMessage(CurrentRuntimePlatform);
                EditorUtility.DisplayDialog("编译完成",
                    $"当前宿主可构建目标已完成。\n成功: {succeeded}/{total}{skipped}", "确定");
            }
            else
            {
                var failedList = string.Join("\n", failed);
                EditorUtility.DisplayDialog("编译完成",
                    $"部分平台编译失败。\n成功: {succeeded}/{total}\n失败:\n{failedList}", "确定");
            }
        }

        private static bool ValidateBuildCurrent() => Directory.Exists(SrcTauriPath);

        private static bool ValidateBuildWinX64() => Directory.Exists(SrcTauriPath);

        private static bool ValidateBuildMacosArm64() => Directory.Exists(SrcTauriPath);

        private static bool ValidateBuildMacosX64() => Directory.Exists(SrcTauriPath);

        private static bool ValidateBuildLinuxX64() => Directory.Exists(SrcTauriPath);

        private static bool ValidateBuildAll() => Directory.Exists(SrcTauriPath);

        public static async void BuildInstallerCurrentPlatform()
        {
            await BuildInstallerForPlatformAsync(CurrentRuntimePlatform);
        }

        public static async void BuildInstallerWinX64()
        {
            await BuildInstallerForCrossPlatformAsync(CrossPlatformTarget.WinX64);
        }

        public static async void BuildInstallerMacosArm64()
        {
            await BuildInstallerForCrossPlatformAsync(CrossPlatformTarget.MacosArm64);
        }

        public static async void BuildInstallerMacosX64()
        {
            await BuildInstallerForCrossPlatformAsync(CrossPlatformTarget.MacosX64);
        }

        public static async void BuildInstallerLinuxX64()
        {
            await BuildInstallerForCrossPlatformAsync(CrossPlatformTarget.LinuxX64);
        }

        public static async void BuildInstallerAllPlatforms()
        {
            if (!Directory.Exists(InstallerProjectPath))
            {
                EditorUtility.DisplayDialog("编译失败",
                    $"安装器项目不存在: {InstallerProjectPath}", "确定");
                return;
            }

            var targets = GetBuildableCrossPlatformTargets(CurrentRuntimePlatform);
            if (targets.Length == 0)
            {
                var message = GetUnsupportedCrossPlatformBuildMessage(CrossPlatformTarget.WinX64, CurrentRuntimePlatform);
                EditorUtility.DisplayDialog("编译失败", message, "确定");
                return;
            }

            var total = targets.Length;
            var succeeded = 0;
            var failed = new List<string>();

            for (var i = 0; i < total; i++)
            {
                var target = targets[i];
                var progress = (float)i / total;
                EditorUtility.DisplayProgressBar("YokiFrame",
                    $"正在编译安装器 {GetCrossPlatformTargetName(target)} ({i + 1}/{total})...", progress);

                try
                {
                    var (success, _) = await RunInstallerCargoForCrossPlatformAsync(target);
                    if (success)
                    {
                        CopyInstallerCrossPlatformArtifact(target);
                        succeeded++;
                    }
                    else
                    {
                        failed.Add(GetCrossPlatformTargetName(target));
                    }
                }
                catch (Exception e)
                {
                    LogKit.Error($"[TauriLauncher] 安装器发布复制失败 ({GetCrossPlatformTargetName(target)}): {e.Message}");
                    failed.Add(GetCrossPlatformTargetName(target));
                }
            }

            EditorUtility.ClearProgressBar();

            if (failed.Count == 0)
            {
                var skipped = GetSkippedCrossPlatformTargetMessage(CurrentRuntimePlatform);
                EditorUtility.DisplayDialog("编译完成",
                    $"当前宿主可构建安装器目标已完成。\n成功: {succeeded}/{total}{skipped}", "确定");
            }
            else
            {
                var failedList = string.Join("\n", failed);
                EditorUtility.DisplayDialog("编译完成",
                    $"部分平台安装器编译失败。\n成功: {succeeded}/{total}\n失败:\n{failedList}", "确定");
            }
        }

        private static bool ValidateInstallerBuildCurrent() => Directory.Exists(InstallerProjectPath);

        private static bool ValidateInstallerBuildWinX64() => Directory.Exists(InstallerProjectPath);

        private static bool ValidateInstallerBuildMacosArm64() => Directory.Exists(InstallerProjectPath);

        private static bool ValidateInstallerBuildMacosX64() => Directory.Exists(InstallerProjectPath);

        private static bool ValidateInstallerBuildLinuxX64() => Directory.Exists(InstallerProjectPath);

        private static bool ValidateInstallerBuildAll() => Directory.Exists(InstallerProjectPath);

        /// <summary>编译指定运行平台的安装器。</summary>
        internal static async Task<(bool success, string output)> BuildInstallerForPlatformAsync(TauriRuntimePlatform platform)
        {
            if (!Directory.Exists(InstallerProjectPath))
            {
                EditorUtility.DisplayDialog("编译失败",
                    $"安装器项目不存在: {InstallerProjectPath}", "确定");
                return (false, "安装器项目不存在");
            }

            var platformName = GetPlatformDisplayName(platform);
            EditorUtility.DisplayProgressBar("YokiFrame", $"正在编译安装器 {platformName} (release)...", 0.5f);
            try
            {
                var args = ResolveBuildReleaseArguments(platform);
                var (success, output) = await RunInstallerCargoAsync(args);
                EditorUtility.ClearProgressBar();

                if (success)
                {
                    CopyInstallerPlatformArtifact(platform);
                    return (true, output);
                }

                LogKit.Error($"[TauriLauncher] 安装器 cargo build 失败 ({platformName})\n{output}");
                EditorUtility.DisplayDialog("编译失败",
                    $"安装器 cargo build 失败 ({platformName})。详见 Console。", "确定");
                return (false, output);
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                LogKit.Error($"[TauriLauncher] 安装器 cargo build 异常 ({platformName}): {e.Message}");
                return (false, e.Message);
            }
        }

        /// <summary>编译跨平台目标的安装器。</summary>
        internal static async Task<(bool success, string output)> BuildInstallerForCrossPlatformAsync(CrossPlatformTarget target)
        {
            if (!Directory.Exists(InstallerProjectPath))
            {
                EditorUtility.DisplayDialog("编译失败",
                    $"安装器项目不存在: {InstallerProjectPath}", "确定");
                return (false, "安装器项目不存在");
            }

            var targetName = GetCrossPlatformTargetName(target);
            EditorUtility.DisplayProgressBar("YokiFrame", $"正在编译安装器 {targetName} (release)...", 0.5f);
            try
            {
                var (success, output) = await RunInstallerCargoForCrossPlatformAsync(target);
                EditorUtility.ClearProgressBar();

                if (success)
                {
                    CopyInstallerCrossPlatformArtifact(target);
                    return (true, output);
                }

                LogKit.Error($"[TauriLauncher] 安装器 cargo build 失败 ({targetName})\n{output}");
                EditorUtility.DisplayDialog("编译失败",
                    $"安装器 cargo build 失败 ({targetName})。详见 Console。", "确定");
                return (false, output);
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                LogKit.Error($"[TauriLauncher] 安装器 cargo build 异常 ({targetName}): {e.Message}");
                return (false, e.Message);
            }
        }

        /// <summary>执行安装器跨平台编译的 cargo 命令。</summary>
        internal static async Task<(bool success, string output)> RunInstallerCargoForCrossPlatformAsync(CrossPlatformTarget target)
        {
            if (!CanBuildCrossPlatformTarget(target, CurrentRuntimePlatform))
                return (false, GetUnsupportedCrossPlatformBuildMessage(target, CurrentRuntimePlatform));

            var args = ResolveCrossPlatformBuildArguments(target);
            return await RunInstallerCargoAsync(args);
        }

        /// <summary>解析安装器随包发布路径。</summary>
        internal static string ResolvePublishedInstallerBinaryPath(string installerRoot, CrossPlatformTarget target)
        {
            var platformDir = GetInstallerPlatformDirectoryName(target);
            if (target == CrossPlatformTarget.WinX64)
                return Path.Combine(installerRoot, platformDir, "YokiFramePackageTool.exe");

            if (target == CrossPlatformTarget.MacosArm64 || target == CrossPlatformTarget.MacosX64)
                return Path.Combine(
                    installerRoot,
                    platformDir,
                    "YokiFramePackageTool.app",
                    "Contents",
                    "MacOS",
                    "yokiframe-package-tool");

            return Path.Combine(installerRoot, platformDir, "YokiFramePackageTool");
        }

        /// <summary>获取安装器发布平台目录名。</summary>
        internal static string GetInstallerPlatformDirectoryName(CrossPlatformTarget target)
        {
            return target switch
            {
                CrossPlatformTarget.WinX64 => "win-x64",
                CrossPlatformTarget.MacosArm64 => "macos-arm64",
                CrossPlatformTarget.MacosX64 => "macos-x64",
                CrossPlatformTarget.LinuxX64 => "linux-x64",
                _ => "unknown"
            };
        }

        /// <summary>复制当前平台安装器产物到 Installer~/&lt;platform&gt;/。</summary>
        internal static void CopyInstallerPlatformArtifact(TauriRuntimePlatform platform)
        {
            var target = platform switch
            {
                TauriRuntimePlatform.Windows => CrossPlatformTarget.WinX64,
                TauriRuntimePlatform.MacOS => CrossPlatformTarget.MacosArm64,
                _ => CrossPlatformTarget.LinuxX64
            };

            if (platform == TauriRuntimePlatform.MacOS)
            {
                CopyInstallerAppBundle(
                    ResolveDevInstallerAppBundlePath(InstallerProjectPath, platform),
                    ResolvePublishedInstallerAppBundlePath(InstallerPublishedRoot, target));
                return;
            }

            CopyInstallerArtifact(
                ResolveDevInstallerBinaryPath(InstallerProjectPath, platform),
                ResolvePublishedInstallerBinaryPath(InstallerPublishedRoot, target));
        }

        /// <summary>复制跨平台安装器产物到 Installer~/&lt;platform&gt;/。</summary>
        internal static void CopyInstallerCrossPlatformArtifact(CrossPlatformTarget target)
        {
            if (target == CrossPlatformTarget.MacosArm64 || target == CrossPlatformTarget.MacosX64)
            {
                CopyInstallerAppBundle(
                    ResolveCrossPlatformInstallerAppBundlePath(InstallerProjectPath, target),
                    ResolvePublishedInstallerAppBundlePath(InstallerPublishedRoot, target));
                return;
            }

            CopyInstallerArtifact(
                ResolveCrossPlatformInstallerBinaryPath(InstallerProjectPath, target),
                ResolvePublishedInstallerBinaryPath(InstallerPublishedRoot, target));
        }

        internal static string ResolvePublishedInstallerAppBundlePath(string installerRoot, CrossPlatformTarget target)
        {
            if (target != CrossPlatformTarget.MacosArm64 && target != CrossPlatformTarget.MacosX64)
                return string.Empty;

            return Path.Combine(
                installerRoot,
                GetInstallerPlatformDirectoryName(target),
                "YokiFramePackageTool.app");
        }

        internal static string ResolveDevInstallerAppBundlePath(string installerProjectPath, TauriRuntimePlatform platform)
        {
            if (platform != TauriRuntimePlatform.MacOS)
                return string.Empty;

            return Path.Combine(
                installerProjectPath,
                CARGO_TARGET_DIR_NAME,
                "release",
                "bundle",
                "macos",
                "YokiFrame Package Tool.app");
        }

        internal static string ResolveDevInstallerBinaryPath(string installerProjectPath, TauriRuntimePlatform platform)
        {
            var releaseDir = Path.Combine(installerProjectPath, CARGO_TARGET_DIR_NAME, "release");
            if (platform == TauriRuntimePlatform.Windows)
                return Path.Combine(releaseDir, "yokiframe-package-tool.exe");

            return Path.Combine(releaseDir, "yokiframe-package-tool");
        }

        internal static string ResolveCrossPlatformInstallerBinaryPath(string installerProjectPath, CrossPlatformTarget target)
        {
            var targetDir = target switch
            {
                CrossPlatformTarget.WinX64 => "x86_64-pc-windows-msvc",
                CrossPlatformTarget.MacosArm64 => "aarch64-apple-darwin",
                CrossPlatformTarget.MacosX64 => "x86_64-apple-darwin",
                CrossPlatformTarget.LinuxX64 => "x86_64-unknown-linux-gnu",
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };

            var releaseDir = Path.Combine(installerProjectPath, CARGO_TARGET_DIR_NAME, targetDir, "release");
            if (target == CrossPlatformTarget.WinX64)
                return Path.Combine(releaseDir, "yokiframe-package-tool.exe");

            return Path.Combine(releaseDir, "yokiframe-package-tool");
        }

        internal static string ResolveCrossPlatformInstallerAppBundlePath(string installerProjectPath, CrossPlatformTarget target)
        {
            var targetDir = target switch
            {
                CrossPlatformTarget.MacosArm64 => "aarch64-apple-darwin",
                CrossPlatformTarget.MacosX64 => "x86_64-apple-darwin",
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };

            return Path.Combine(
                installerProjectPath,
                CARGO_TARGET_DIR_NAME,
                targetDir,
                "release",
                "bundle",
                "macos",
                "YokiFrame Package Tool.app");
        }

        private static void CopyInstallerArtifact(string sourcePath, string targetPath)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"安装器 release 产物缺失: {sourcePath}");

            var targetParent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetParent))
                Directory.CreateDirectory(targetParent);
            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        private static void CopyInstallerAppBundle(string sourceAppPath, string targetAppPath)
        {
            if (!Directory.Exists(sourceAppPath))
                throw new DirectoryNotFoundException($"安装器 app bundle 缺失: {sourceAppPath}");

            if (Directory.Exists(targetAppPath))
                Directory.Delete(targetAppPath, recursive: true);

            CopyDirectory(sourceAppPath, targetAppPath);
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);

            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), overwrite: true);

            foreach (var dir in Directory.GetDirectories(sourceDir))
                CopyDirectory(dir, Path.Combine(targetDir, Path.GetFileName(dir)));
        }

        /// <summary>执行安装器的 cargo 命令。</summary>
        internal static async Task<(bool success, string output)> RunInstallerCargoAsync(string args)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cargo",
                    Arguments = args,
                    WorkingDirectory = InstallerProjectPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                };

                using var process = System.Diagnostics.Process.Start(startInfo);
                if (process == default)
                    return (false, "无法启动 cargo 进程");

                var stdout = process.StandardOutput.ReadToEndAsync();
                var stderr = process.StandardError.ReadToEndAsync();

                await Task.WhenAll(stdout, stderr);
                process.WaitForExit();

                var output = stdout.Result;
                if (!string.IsNullOrEmpty(stderr.Result))
                    output += "\n" + stderr.Result;

                return (process.ExitCode == 0, output);
            }
            catch (Exception e)
            {
                return (false, e.Message);
            }
        }

        /// <summary>
        /// 启动或唤起 Tauri 编辑器窗口。
        /// </summary>
        public static void Launch()
        {
            if (IsRunning)
            {
                if (ShouldRequestPanelShow(
                        EditorApplication.isCompiling,
                        EditorApplication.isUpdating,
                        EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    WritePanelShowRequest(
                        ResolveYokiframeDir(),
                        activate: true);
                }
                return;
            }

            var launchTarget = ResolveCurrentLaunchTarget();

            if (launchTarget == LaunchTarget.Source)
            {
                WarnIfLaunchingOutdatedDevBinary(launchingSourceWindow: true);
                var processStarted = StartSourceProcess();
                if (ShouldRequestPanelShowAfterLaunchStart(
                        processStarted,
                        EditorApplication.isCompiling,
                        EditorApplication.isUpdating,
                        EditorApplication.isPlayingOrWillChangePlaymode))
                {
                    WritePanelShowRequest(
                        ResolveYokiframeDir(),
                        activate: true);
                }

                if (!processStarted)
                {
                    EditorUtility.DisplayDialog("Tauri 源码窗口启动失败",
                        $"无法通过源码启动 Tauri。\n路径: {SrcTauriPath}", "确定");
                }
                return;
            }

            if (launchTarget == LaunchTarget.Unavailable)
            {
                ReportMissingPublishedBinary();
                return;
            }

            WarnIfLaunchingOutdatedDevBinary(launchingSourceWindow: false);
            var binaryStarted = StartBinaryProcess(BinaryPath);
            if (ShouldRequestPanelShowAfterLaunchStart(
                    binaryStarted,
                    EditorApplication.isCompiling,
                    EditorApplication.isUpdating,
                    EditorApplication.isPlayingOrWillChangePlaymode))
            {
                WritePanelShowRequest(
                    ResolveYokiframeDir(),
                    activate: true);
            }

            if (!binaryStarted)
            {
                EditorUtility.DisplayDialog("Tauri 启动失败",
                    $"无法启动 Tauri 进程。\n路径: {BinaryPath}", "确定");
            }
        }

        private static bool ValidateLaunch() => true;

        private static void Preload()
        {
            if (IsRunning)
                return;

            var launchTarget = ResolveCurrentLaunchTarget();
            if (launchTarget != LaunchTarget.Binary)
                return;

            if (!StartBinaryProcess(BinaryPath))
                LogKit.Warning($"[TauriLauncher] 预热 Tauri 二进制失败: {BinaryPath}");
        }

        /// <summary>
        /// 关闭当前 Tauri 编辑器进程。
        /// </summary>
        public static void Close()
        {
            var process = GetRunningTauriProcess();
            if (process != null)
            {
                process.Kill();
                process.WaitForExit(3000);
                if (!process.HasExited)
                    process.Kill();
                process.Dispose();
                sTauriProcess = null;
            }
        }

        private static bool ValidateClose() => IsRunning;

        /// <summary>
        /// 重启 Tauri 编辑器窗口。
        /// </summary>
        public static async void Restart()
        {
            Close();
            await Task.Delay(500);
            Launch();
        }

        private static bool ValidateRestart() => IsRunning;

        [MenuItem("YokiFrame/编辑器窗口/启动窗口", false, 99)]
        [Shortcut("YokiFrame/Editor UI/Launch", KeyCode.E, ShortcutModifiers.Action)]
        private static void MenuLaunchWindow()
        {
            Launch();
        }

        [MenuItem("YokiFrame/编辑器窗口/关闭窗口", false, 100)]
        private static void MenuCloseWindow()
        {
            Close();
        }

        [MenuItem("YokiFrame/编辑器窗口/重启窗口", false, 101)]
        private static void MenuRestartWindow()
        {
            Restart();
        }

        [MenuItem("YokiFrame/编辑器窗口/启动时预热（可选）", false, 104)]
        private static void MenuToggleAutoPreload()
        {
            AutoPreloadEnabled = !AutoPreloadEnabled;
            Menu.SetChecked("YokiFrame/编辑器窗口/启动时预热（可选）", AutoPreloadEnabled);
        }

        [MenuItem("YokiFrame/编辑器窗口/启动时预热（可选）", true)]
        private static bool ValidateMenuToggleAutoPreload()
        {
            Menu.SetChecked("YokiFrame/编辑器窗口/启动时预热（可选）", AutoPreloadEnabled);
            return true;
        }

        #region Cross-Platform Build

        /// <summary>解析运行平台的 cargo 编译参数。</summary>
        internal static string ResolveBuildReleaseArguments(TauriRuntimePlatform platform)
        {
            if (platform == TauriRuntimePlatform.MacOS)
                return "tauri build --bundles app --target-dir " + CARGO_TARGET_DIR_NAME;

            return "build --release --target-dir " + CARGO_TARGET_DIR_NAME;
        }

        /// <summary>编译指定运行平台的 Tauri 二进制。</summary>
        internal static async Task<(bool success, string output)> BuildForPlatformAsync(TauriRuntimePlatform platform)
        {
            if (!Directory.Exists(SrcTauriPath))
            {
                EditorUtility.DisplayDialog("编译失败",
                    $"Tauri 项目不存在: {SrcTauriPath}", "确定");
                return (false, "Tauri 项目不存在");
            }

            var platformName = GetPlatformDisplayName(platform);
            EditorUtility.DisplayProgressBar("YokiFrame", $"正在编译 {platformName} (release)...", 0.5f);
            try
            {
                var args = ResolveBuildReleaseArguments(platform);
                var (success, output) = await RunCargoAsync(args);
                EditorUtility.ClearProgressBar();

                if (success)
                {
                    CopyPlatformArtifact(platform);
                    return (true, output);
                }

                LogKit.Error($"[TauriLauncher] cargo build 失败 ({platformName})\n{output}");
                EditorUtility.DisplayDialog("编译失败",
                    $"cargo build 失败 ({platformName})。详见 Console。", "确定");
                return (false, output);
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                LogKit.Error($"[TauriLauncher] cargo build 异常 ({platformName}): {e.Message}");
                return (false, e.Message);
            }
        }

        /// <summary>编译跨平台目标的 Tauri 二进制。</summary>
        internal static async Task<(bool success, string output)> BuildCrossPlatformAsync(CrossPlatformTarget target)
        {
            if (!Directory.Exists(SrcTauriPath))
            {
                EditorUtility.DisplayDialog("编译失败",
                    $"Tauri 项目不存在: {SrcTauriPath}", "确定");
                return (false, "Tauri 项目不存在");
            }

            var targetName = GetCrossPlatformTargetName(target);
            if (!CanBuildCrossPlatformTarget(target, CurrentRuntimePlatform))
            {
                var message = GetUnsupportedCrossPlatformBuildMessage(target, CurrentRuntimePlatform);
                LogKit.Warning($"[TauriLauncher] 跳过不支持的本机构建目标 ({targetName})\n{message}");
                EditorUtility.DisplayDialog("无法在当前系统编译", message, "确定");
                return (false, message);
            }

            EditorUtility.DisplayProgressBar("YokiFrame", $"正在编译 {targetName} (release)...", 0.5f);
            try
            {
                var (success, output) = await RunCargoForCrossPlatformAsync(target);
                EditorUtility.ClearProgressBar();

                if (success)
                {
                    CopyCrossPlatformArtifact(target);
                    return (true, output);
                }

                LogKit.Error($"[TauriLauncher] cargo build 失败 ({targetName})\n{output}");
                EditorUtility.DisplayDialog("编译失败",
                    $"cargo build 失败 ({targetName})。详见 Console。", "确定");
                return (false, output);
            }
            catch (Exception e)
            {
                EditorUtility.ClearProgressBar();
                LogKit.Error($"[TauriLauncher] cargo build 异常 ({targetName}): {e.Message}");
                return (false, e.Message);
            }
        }

        /// <summary>执行跨平台编译的 cargo 命令。</summary>
        internal static async Task<(bool success, string output)> RunCargoForCrossPlatformAsync(CrossPlatformTarget target)
        {
            if (!CanBuildCrossPlatformTarget(target, CurrentRuntimePlatform))
                return (false, GetUnsupportedCrossPlatformBuildMessage(target, CurrentRuntimePlatform));

            var args = ResolveCrossPlatformBuildArguments(target);
            return await RunCargoAsync(args);
        }

        /// <summary>解析跨平台编译的 cargo 参数。</summary>
        internal static string ResolveCrossPlatformBuildArguments(CrossPlatformTarget target)
        {
            return target switch
            {
                CrossPlatformTarget.WinX64 => "build --release --target x86_64-pc-windows-msvc --target-dir " + CARGO_TARGET_DIR_NAME,
                CrossPlatformTarget.MacosArm64 => "tauri build --target aarch64-apple-darwin --bundles app --target-dir " + CARGO_TARGET_DIR_NAME,
                CrossPlatformTarget.MacosX64 => "tauri build --target x86_64-apple-darwin --bundles app --target-dir " + CARGO_TARGET_DIR_NAME,
                CrossPlatformTarget.LinuxX64 => "build --release --target x86_64-unknown-linux-gnu --target-dir " + CARGO_TARGET_DIR_NAME,
                _ => "build --release --target-dir " + CARGO_TARGET_DIR_NAME
            };
        }

        /// <summary>复制当前平台 Tauri Editor release 产物到 TauriRuntime~/。</summary>
        internal static void CopyPlatformArtifact(TauriRuntimePlatform platform)
        {
            CopyPlatformArtifact(SrcTauriPath, RuntimeDir, platform);
        }

        internal static void CopyPlatformArtifact(
            string srcTauriPath,
            string runtimeDir,
            TauriRuntimePlatform platform)
        {
            if (platform == TauriRuntimePlatform.MacOS)
            {
                CopyAppBundle(
                    ResolveDevAppBundlePath(srcTauriPath, platform),
                    ResolvePublishedAppBundlePath(runtimeDir, platform));
                return;
            }

            CopyArtifact(
                ResolveDevBinaryPath(srcTauriPath, platform),
                ResolvePublishedBinaryPath(runtimeDir, platform));
        }

        /// <summary>复制跨平台 Tauri Editor release 产物到 TauriRuntime~/。</summary>
        internal static void CopyCrossPlatformArtifact(CrossPlatformTarget target)
        {
            CopyCrossPlatformArtifact(SrcTauriPath, RuntimeDir, target);
        }

        internal static void CopyCrossPlatformArtifact(
            string srcTauriPath,
            string runtimeDir,
            CrossPlatformTarget target)
        {
            if (target == CrossPlatformTarget.MacosArm64 || target == CrossPlatformTarget.MacosX64)
            {
                CopyAppBundle(
                    ResolveCrossPlatformAppBundlePath(srcTauriPath, target),
                    ResolvePublishedAppBundlePath(runtimeDir, TauriRuntimePlatform.MacOS));
                return;
            }

            var platform = target == CrossPlatformTarget.WinX64
                ? TauriRuntimePlatform.Windows
                : TauriRuntimePlatform.Linux;
            CopyArtifact(
                ResolveCrossPlatformBinaryPath(srcTauriPath, target),
                ResolvePublishedBinaryPath(runtimeDir, platform));
        }

        internal static string ResolveCrossPlatformBinaryPath(string srcTauriPath, CrossPlatformTarget target)
        {
            var targetDir = target switch
            {
                CrossPlatformTarget.WinX64 => "x86_64-pc-windows-msvc",
                CrossPlatformTarget.MacosArm64 => "aarch64-apple-darwin",
                CrossPlatformTarget.MacosX64 => "x86_64-apple-darwin",
                CrossPlatformTarget.LinuxX64 => "x86_64-unknown-linux-gnu",
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };

            var releaseDir = Path.Combine(srcTauriPath, CARGO_TARGET_DIR_NAME, targetDir, "release");
            if (target == CrossPlatformTarget.WinX64)
                return Path.Combine(releaseDir, "yokiframe-tauri-editor.exe");

            return Path.Combine(releaseDir, "yokiframe-tauri-editor");
        }

        internal static string ResolveCrossPlatformAppBundlePath(string srcTauriPath, CrossPlatformTarget target)
        {
            var targetDir = target switch
            {
                CrossPlatformTarget.MacosArm64 => "aarch64-apple-darwin",
                CrossPlatformTarget.MacosX64 => "x86_64-apple-darwin",
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };

            return Path.Combine(
                srcTauriPath,
                CARGO_TARGET_DIR_NAME,
                targetDir,
                "release",
                "bundle",
                "macos",
                "YokiFrame Editor.app");
        }

        private static void CopyArtifact(string sourcePath, string targetPath)
        {
            if (!File.Exists(sourcePath))
                throw new FileNotFoundException($"Tauri Editor release 产物缺失: {sourcePath}");

            var targetParent = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetParent))
                Directory.CreateDirectory(targetParent);
            File.Copy(sourcePath, targetPath, overwrite: true);
        }

        private static void CopyAppBundle(string sourceAppPath, string targetAppPath)
        {
            if (!Directory.Exists(sourceAppPath))
                throw new DirectoryNotFoundException($"Tauri Editor app bundle 缺失: {sourceAppPath}");

            if (Directory.Exists(targetAppPath))
                Directory.Delete(targetAppPath, recursive: true);

            CopyDirectory(sourceAppPath, targetAppPath);
        }

        /// <summary>判断当前宿主系统是否能本机生成指定 Tauri 目标。</summary>
        internal static bool CanBuildCrossPlatformTarget(
            CrossPlatformTarget target,
            TauriRuntimePlatform hostPlatform)
        {
            switch (hostPlatform)
            {
                case TauriRuntimePlatform.Windows:
                    return target == CrossPlatformTarget.WinX64;
                case TauriRuntimePlatform.MacOS:
                    return target == CrossPlatformTarget.MacosArm64 ||
                           target == CrossPlatformTarget.MacosX64;
                case TauriRuntimePlatform.Linux:
                    return target == CrossPlatformTarget.LinuxX64;
                default:
                    return false;
            }
        }

        /// <summary>列出当前宿主系统可直接构建的 Tauri 目标。</summary>
        internal static CrossPlatformTarget[] GetBuildableCrossPlatformTargets(TauriRuntimePlatform hostPlatform)
        {
            switch (hostPlatform)
            {
                case TauriRuntimePlatform.Windows:
                    return new[] { CrossPlatformTarget.WinX64 };
                case TauriRuntimePlatform.MacOS:
                    return new[] { CrossPlatformTarget.MacosArm64, CrossPlatformTarget.MacosX64 };
                case TauriRuntimePlatform.Linux:
                    return new[] { CrossPlatformTarget.LinuxX64 };
                default:
                    return Array.Empty<CrossPlatformTarget>();
            }
        }

        private static string GetUnsupportedCrossPlatformBuildMessage(
            CrossPlatformTarget target,
            TauriRuntimePlatform hostPlatform)
        {
            return
                $"当前系统是 {GetPlatformDisplayName(hostPlatform)}，不能直接生成 {GetCrossPlatformTargetName(target)} 的 Tauri 原生壳。\n\n" +
                "Tauri 的 macOS / Linux / Windows 壳依赖各自平台的系统 WebView、链接器和打包工具。" +
                "请在对应操作系统或 CI runner 上构建该平台产物，然后拷贝到 Assets/YokiFrame/TauriRuntime~；dist 目录仍然共用同一份。";
        }

        private static string GetSkippedCrossPlatformTargetMessage(TauriRuntimePlatform hostPlatform)
        {
            var skipped = new List<string>();
            foreach (CrossPlatformTarget target in Enum.GetValues(typeof(CrossPlatformTarget)))
            {
                if (!CanBuildCrossPlatformTarget(target, hostPlatform))
                    skipped.Add(GetCrossPlatformTargetName(target));
            }

            if (skipped.Count == 0)
                return string.Empty;

            return "\n\n已跳过需在对应系统或 CI runner 构建的目标:\n" + string.Join("\n", skipped);
        }

        /// <summary>获取跨平台目标的显示名称。</summary>
        internal static string GetCrossPlatformTargetName(CrossPlatformTarget target)
        {
            return target switch
            {
                CrossPlatformTarget.WinX64 => "Windows (win-x64)",
                CrossPlatformTarget.MacosArm64 => "macOS (arm64)",
                CrossPlatformTarget.MacosX64 => "macOS (x64)",
                CrossPlatformTarget.LinuxX64 => "Linux (x64)",
                _ => target.ToString()
            };
        }

        /// <summary>获取平台显示名称。</summary>
        internal static string GetPlatformDisplayName(TauriRuntimePlatform platform)
        {
            return platform switch
            {
                TauriRuntimePlatform.Windows => "Windows",
                TauriRuntimePlatform.MacOS => "macOS",
                _ => "Linux"
            };
        }

        #endregion

        #region Binary Management

        internal static LaunchTarget ResolveLaunchTarget(
            bool isDevMode,
            bool binaryExists,
            bool binaryOutdated)
        {
            if (isDevMode && (!binaryExists || binaryOutdated))
                return LaunchTarget.Source;
            if (binaryExists)
                return LaunchTarget.Binary;

            return LaunchTarget.Unavailable;
        }

        private static LaunchTarget ResolveCurrentLaunchTarget()
        {
            return ResolveLaunchTarget(
                IsDevMode,
                IsBinaryAvailableForLaunch(BinaryPath),
                IsDevMode && IsBinaryOutdated());
        }

        private static void ReportMissingPublishedBinary()
        {
            LogKit.Error($"[TauriLauncher] 发布产物缺失: {PublishedBinaryPath}");
            EditorUtility.DisplayDialog("Tauri 发布产物缺失",
                "未找到随包发布的 Tauri 二进制。请先通过打包流程生成 TauriRuntime~ 产物。", "确定");
        }

        internal static bool IsBinaryAvailableForLaunch(string binaryPath)
        {
            return File.Exists(binaryPath);
        }

        internal static bool ShouldWarnOutdatedBinary(
            bool isDevMode,
            bool binaryOutdated,
            bool launchingSourceWindow,
            bool warningAlreadyShown)
        {
            return isDevMode && binaryOutdated && launchingSourceWindow && !warningAlreadyShown;
        }

        private static void WarnIfLaunchingOutdatedDevBinary(bool launchingSourceWindow)
        {
            if (!ShouldWarnOutdatedBinary(
                    IsDevMode,
                    IsDevMode && IsBinaryOutdated(),
                    launchingSourceWindow,
                    sOutdatedBinaryWarningShown))
                return;

            sOutdatedBinaryWarningShown = true;
            LogKit.Warning("[TauriLauncher] Tauri 源码比当前 release exe 新，本次直接拉起源码窗口；准备发布前请手动执行 YokiFrame/编辑器窗口/编译/编译当前平台。");
        }

        internal static bool IsBinaryOutdated()
        {
            if (!File.Exists(DevBinaryPath))
                return true;

            var binaryTime = File.GetLastWriteTimeUtc(DevBinaryPath);
            var srcDir = Path.Combine(SrcTauriPath, "src");

            if (!Directory.Exists(srcDir))
                return false;

            if (Directory.GetFiles(srcDir, "*.rs", SearchOption.AllDirectories)
                .Any(f => File.GetLastWriteTimeUtc(f) > binaryTime))
                return true;

            if (File.GetLastWriteTimeUtc(Path.Combine(SrcTauriPath, "Cargo.toml")) > binaryTime)
                return true;

            if (File.GetLastWriteTimeUtc(Path.Combine(SrcTauriPath, "tauri.conf.json")) > binaryTime)
                return true;

            return false;
        }

        private static string ResolveYokiframeDir()
        {
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, ".yokiframe");
        }

        internal static bool WritePanelShowRequest(
            string yokiframeDir,
            bool activate = true)
        {
            try
            {
                var panelDir = Path.Combine(yokiframeDir, PANEL_REQUEST_DIR);
                Directory.CreateDirectory(panelDir);
                var requestPath = Path.Combine(panelDir, PANEL_SHOW_REQUEST_FILE);
                var tempPath = requestPath + ".tmp";
                File.WriteAllText(tempPath, BuildPanelShowRequestJson(activate), Encoding.UTF8);

                if (File.Exists(requestPath))
                    File.Replace(tempPath, requestPath, null);
                else
                    File.Move(tempPath, requestPath);

                return true;
            }
            catch (Exception e)
            {
                LogKit.Warning($"[TauriLauncher] 写入窗口显示请求失败: {e.Message}");
                return false;
            }
        }

        private static string BuildPanelShowRequestJson(bool activate)
        {
            return "{\"protocolVersion\":2" +
                   ",\"type\":\"show_window\"" +
                   ",\"activate\":" + (activate ? "true" : "false") +
                   ",\"source\":\"unity-editor\"" +
                   ",\"createdAtUtc\":\"" + DateTime.UtcNow.ToString("O") + "\"}";
        }

        private static bool IsRunning => GetRunningTauriProcess() != null;

        internal static bool ShouldAutoRestartExitedProcess(bool processExited) => false;

        internal static bool AutoPreloadEnabled
        {
            get => EditorPrefs.GetBool(AUTO_PRELOAD_PREF_KEY, false);
            set => EditorPrefs.SetBool(AUTO_PRELOAD_PREF_KEY, value);
        }

        internal static bool ShouldUseDetachedPackageBinary(
            string binaryPath,
            string runtimeDir,
            TauriRuntimePlatform platform)
        {
            if (platform != TauriRuntimePlatform.Windows)
                return false;
            if (string.IsNullOrEmpty(binaryPath) || string.IsNullOrEmpty(runtimeDir))
                return false;

            var binaryFullPath = Path.GetFullPath(binaryPath);
            var runtimeFullPath = Path.GetFullPath(runtimeDir);
            return string.Equals(
                       Path.GetFileName(binaryFullPath),
                       "yokiframe-tauri-editor.exe",
                       StringComparison.OrdinalIgnoreCase) &&
                   IsPathInsideDirectory(runtimeFullPath, binaryFullPath);
        }

        internal static string ResolveDetachedPackageBinaryRoot(string projectRootDir)
        {
            return Path.Combine(projectRootDir, "Temp", DETACHED_BINARY_ROOT_DIR);
        }

        internal static string ResolveDetachedPackageBinaryPath(
            string binaryPath,
            string projectRootDir)
        {
            return Path.Combine(
                ResolveDetachedPackageBinaryRoot(projectRootDir),
                Path.GetFileName(binaryPath));
        }

        internal static string ResolveBinaryLaunchPath(
            string binaryPath,
            string runtimeDir,
            TauriRuntimePlatform platform,
            string projectRootDir)
        {
            if (!ShouldUseDetachedPackageBinary(binaryPath, runtimeDir, platform))
                return binaryPath;

            return ResolveDetachedPackageBinaryPath(binaryPath, projectRootDir);
        }

        internal static bool IsDetachedPackageBinaryPath(
            string processPath,
            string projectRootDir,
            string processName)
        {
            if (string.IsNullOrEmpty(processPath) ||
                string.IsNullOrEmpty(projectRootDir) ||
                string.IsNullOrEmpty(processName))
                return false;

            var detachedRoot = ResolveDetachedPackageBinaryRoot(projectRootDir);
            return string.Equals(
                       Path.GetFileNameWithoutExtension(processPath),
                       processName,
                       StringComparison.OrdinalIgnoreCase) &&
                   IsPathInsideDirectory(detachedRoot, processPath);
        }

        private static bool IsPathInsideDirectory(string rootDir, string candidatePath)
        {
            if (string.IsNullOrEmpty(rootDir) || string.IsNullOrEmpty(candidatePath))
                return false;

            var rootFullPath = Path.GetFullPath(rootDir)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var candidateFullPath = Path.GetFullPath(candidatePath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            if (string.Equals(rootFullPath, candidateFullPath, StringComparison.OrdinalIgnoreCase))
                return true;

            return candidateFullPath.StartsWith(
                rootFullPath + Path.DirectorySeparatorChar,
                StringComparison.OrdinalIgnoreCase);
        }

        internal static bool ShouldRequestPanelShow(
            bool isCompiling,
            bool isUpdating,
            bool isPlayingOrWillChangePlaymode)
        {
            return !isCompiling && !isUpdating && !isPlayingOrWillChangePlaymode;
        }

        internal static bool ShouldRequestPanelShowAfterLaunchStart(
            bool processStarted,
            bool isCompiling,
            bool isUpdating,
            bool isPlayingOrWillChangePlaymode)
        {
            return processStarted && ShouldRequestPanelShow(
                isCompiling,
                isUpdating,
                isPlayingOrWillChangePlaymode);
        }

        internal static bool ShouldAutoPreload(
            bool preloadEnabled,
            bool preloadAttempted,
            bool processRunning,
            LaunchTarget launchTarget,
            bool isCompiling,
            bool isUpdating,
            bool isPlayingOrWillChangePlaymode,
            bool isPlaying,
            DateTime nowUtc,
            DateTime editorLoadUtc)
        {
            if (!preloadEnabled || preloadAttempted || processRunning)
                return false;
            if (launchTarget != LaunchTarget.Binary)
                return false;
            if (isCompiling || isUpdating || isPlayingOrWillChangePlaymode || isPlaying)
                return false;

            return nowUtc - editorLoadUtc >= TimeSpan.FromSeconds(AUTO_PRELOAD_DELAY_SEC);
        }

        internal static bool ShouldRefreshAssetsForToolWindow(
            bool processRunning,
            bool isCompiling,
            bool isUpdating,
            bool isPlayingOrWillChangePlaymode,
            bool isPlaying,
            bool isApplicationFocused,
            DateTime nowUtc,
            DateTime lastRefreshUtc)
        {
            if (!processRunning)
                return false;
            if (isApplicationFocused)
                return false;
            if (isCompiling || isUpdating || isPlayingOrWillChangePlaymode || isPlaying)
                return false;

            return nowUtc - lastRefreshUtc >= TimeSpan.FromSeconds(ASSET_REFRESH_INTERVAL_SEC);
        }

        #endregion
    }
}
#endif
