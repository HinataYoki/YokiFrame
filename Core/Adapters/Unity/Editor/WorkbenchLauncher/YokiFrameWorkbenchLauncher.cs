#if UNITY_EDITOR

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 提供 Unity Editor 菜单入口，从项目级 Runtime 缓存启动 Avalonia Workbench。
    /// </summary>
    internal static partial class YokiFrameWorkbenchLauncher
    {
        private const string MENU_PATH = "YokiFrame/Workbench/Open %e";
        private const string LAUNCHER_SCRIPT_NAME = "YokiFrameWorkbenchLauncher";
        private const string LAUNCHER_SCRIPT_GUID = "99ad1cafdf154af19d97a5864ac6e097";
        private const string LAUNCHER_SCRIPT_RELATIVE_PATH = "Core/Adapters/Unity/Editor/WorkbenchLauncher/YokiFrameWorkbenchLauncher.cs";
        private const string LOG_PREFIX = "[YokiFrame Workbench] ";
        private const string RUNTIME_MANIFEST_NAME = "tool-manifest.json";
        private const string WORKBENCH_PROJECT_ARGUMENT = "--project";
        private const string WORKBENCH_SOURCE_ARGUMENT = "--source";
        private const string WORKBENCH_PARENT_WINDOW_ARGUMENT = "--parent-hwnd";
        private const string ACTIVATION_MESSAGE = "activate";
        private const string ACTIVATION_ACKNOWLEDGED = "ack";
        private const string ACTIVATION_PIPE_NAME_PREFIX = "yokiframe-workbench-";
        private const int ACTIVATION_CONNECT_TIMEOUT_MS = 250;
        private const int ACTIVATION_RESPONSE_TIMEOUT_MS = 750;
        /// <summary>
        /// 从 Unity 菜单启动 Workbench；无法启动时展示可操作的错误和证据路径。
        /// </summary>
        [MenuItem(MENU_PATH)]
        private static void OpenWorkbenchFromMenu()
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            var projectRoot = GetProjectRoot();
            if (TryActivateExistingWorkbench(projectRoot))
            {
                UnityEngine.Debug.Log(LOG_PREFIX + "Activated existing Workbench instance. total=" + stopwatch.ElapsedMilliseconds + "ms");
                return;
            }

            if (sRuntimeBootstrapInFlight)
            {
                UnityEngine.Debug.Log(LOG_PREFIX + "Project Runtime bootstrap is already running.");
                return;
            }

            var packageRoot = GetPackageRoot(projectRoot);
            var resolveElapsed = stopwatch.ElapsedMilliseconds;
            var plan = string.IsNullOrWhiteSpace(packageRoot)
                ? CreateFailedPlan(
                    projectRoot,
                    "YokiFrame package root could not be resolved from the launcher script path.",
                    LAUNCHER_SCRIPT_RELATIVE_PATH)
                : CreateLaunchPlan(projectRoot, packageRoot, GetEditorMainWindowHandle());
            var planElapsed = stopwatch.ElapsedMilliseconds - resolveElapsed;
            if (!plan.CanLaunch)
            {
                UnityEngine.Debug.LogWarning(LOG_PREFIX + plan.ErrorMessage + " Evidence: " + plan.EvidencePath + " Resolve/plan timings: resolve=" + resolveElapsed + "ms plan=" + planElapsed + "ms");
                EditorUtility.DisplayDialog(
                    "YokiFrame Workbench",
                    plan.ErrorMessage + Environment.NewLine + plan.EvidencePath,
                    "OK");
                return;
            }

            if (plan.RequiresBootstrap)
            {
                _ = QueueRuntimeBootstrapAndLaunch(plan);
                UnityEngine.Debug.Log(LOG_PREFIX + "Open command queued project Runtime bootstrap. resolve=" + resolveElapsed + "ms plan=" + planElapsed + "ms total=" + stopwatch.ElapsedMilliseconds + "ms");
                return;
            }

            Launch(plan, static startInfo => Process.Start(startInfo));
            UnityEngine.Debug.Log(LOG_PREFIX + "Open command timings: resolve=" + resolveElapsed + "ms plan=" + planElapsed + "ms total=" + stopwatch.ElapsedMilliseconds + "ms");
        }

        /// <summary>
        /// 根据项目根和包根创建 Workbench 启动计划；该方法不启动进程，便于测试和诊断。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根目录。</param>
        /// <param name="packageRoot">YokiFrame 包根目录。</param>
        /// <returns>Workbench 启动计划。</returns>
        internal static YokiFrameWorkbenchLaunchPlan CreateLaunchPlan(string projectRoot, string packageRoot)
        {
            return CreateLaunchPlan(projectRoot, packageRoot, 0L);
        }

        /// <summary>
        /// 根据项目根、包根和父窗口句柄创建 Workbench 启动计划；该方法不启动进程，便于测试和诊断。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根目录。</param>
        /// <param name="packageRoot">YokiFrame 包根目录。</param>
        /// <param name="parentWindowHandle">Unity Editor 主窗口 HWND；为 0 时 Workbench 不尝试嵌入。</param>
        /// <returns>Workbench 启动计划。</returns>
        internal static YokiFrameWorkbenchLaunchPlan CreateLaunchPlan(string projectRoot, string packageRoot, long parentWindowHandle)
        {
            var normalizedProjectRoot = Path.GetFullPath(projectRoot);
            var normalizedPackageRoot = Path.GetFullPath(packageRoot);
            try
            {
                var runtimeRoot = ResolveCurrentRuntimeRoot(normalizedProjectRoot, out var sourceFingerprint);
                if (string.IsNullOrWhiteSpace(runtimeRoot))
                {
                    sourceFingerprint = YokiFrameWorkbenchSourceFingerprint.Compute(normalizedPackageRoot);
                    return CreateBootstrapPlan(
                        normalizedProjectRoot,
                        normalizedPackageRoot,
                        sourceFingerprint,
                        parentWindowHandle,
                        "Workbench Runtime cache is missing or was built from older sources.");
                }

                var manifestPath = Path.Combine(runtimeRoot, RUNTIME_MANIFEST_NAME);
                if (!File.Exists(manifestPath))
                {
                    return CreateBootstrapPlan(
                        normalizedProjectRoot,
                        normalizedPackageRoot,
                        sourceFingerprint,
                        parentWindowHandle,
                        "Workbench Runtime cache manifest is missing.");
                }

                var runtimePlatforms = GetPreferredRuntimePlatforms();
                if (!TryValidateRuntimeManifest(
                        manifestPath,
                        runtimeRoot,
                        runtimePlatforms,
                        out var executablePath,
                        out var validationError))
                {
                    return CreateBootstrapPlan(
                        normalizedProjectRoot,
                        normalizedPackageRoot,
                        sourceFingerprint,
                        parentWindowHandle,
                        "Workbench Runtime cache integrity validation failed: " + validationError);
                }

                return new YokiFrameWorkbenchLaunchPlan(
                    true,
                    executablePath,
                    CreateWorkbenchArguments(normalizedProjectRoot, normalizedPackageRoot, parentWindowHandle),
                    normalizedProjectRoot,
                    string.Empty,
                    manifestPath,
                    false,
                    normalizedPackageRoot,
                    runtimeRoot);
            }
            catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException || exception is ArgumentException)
            {
                return CreateFailedPlan(normalizedProjectRoot, "Unable to inspect the Workbench Runtime cache: " + exception.Message, normalizedPackageRoot);
            }
        }

        /// <summary>
        /// 使用注入的进程启动动作执行启动计划；注入点用于测试中避免真实打开窗口。
        /// </summary>
        /// <param name="plan">已创建的 Workbench 启动计划。</param>
        /// <param name="startProcess">进程启动动作。</param>
        /// <returns>启动动作已执行时返回 true。</returns>
        internal static bool Launch(YokiFrameWorkbenchLaunchPlan plan, Action<ProcessStartInfo> startProcess)
        {
            return Launch(plan, startProcess, UnityEngine.Debug.Log);
        }

        /// <summary>
        /// 使用注入的日志动作执行启动计划；项目级实例复用统一由 Workbench 进程负责。
        /// </summary>
        /// <param name="plan">已创建的 Workbench 启动计划。</param>
        /// <param name="startProcess">进程启动动作。</param>
        /// <param name="logMessage">日志输出动作。</param>
        /// <returns>实际启动候选进程时返回 true；计划不可启动时返回 false。</returns>
        internal static bool Launch(
            YokiFrameWorkbenchLaunchPlan plan,
            Action<ProcessStartInfo> startProcess,
            Action<string> logMessage)
        {
            if (plan == null || !plan.CanLaunch || plan.RequiresBootstrap)
            {
                return false;
            }

            Stopwatch stopwatch = Stopwatch.StartNew();
            var preparationElapsed = stopwatch.ElapsedMilliseconds;
            ProcessStartInfo startInfo = new ProcessStartInfo
            {
                FileName = plan.ExecutablePath,
                Arguments = plan.Arguments,
                WorkingDirectory = plan.WorkingDirectory,
                UseShellExecute = false
            };
            logMessage?.Invoke(LOG_PREFIX + "Starting YokiFrame Workbench. executable: " + plan.ExecutablePath + " arguments: " + plan.Arguments + " workingDirectory: " + plan.WorkingDirectory);
            startProcess(startInfo);
            logMessage?.Invoke(LOG_PREFIX + "Launch timings: preparation=" + preparationElapsed + "ms startProcess=" + (stopwatch.ElapsedMilliseconds - preparationElapsed) + "ms total=" + stopwatch.ElapsedMilliseconds + "ms");
            return true;
        }

        /// <summary>
        /// 获取当前 Unity Editor 对应的 Workbench runtime 平台标识。
        /// </summary>
        /// <returns>平台标识。</returns>
        private static string GetCurrentPlatform()
        {
            return GetRuntimeIdentifier(Application.platform, RuntimeInformation.ProcessArchitecture);
        }

        /// <summary>
        /// 获取当前 Editor 的项目缓存 profile；Windows 只接受由 Ctrl+E 生成的 Native AOT 入口。
        /// </summary>
        /// <returns>按优先级排列的平台标识。</returns>
        private static string[] GetPreferredRuntimePlatforms()
        {
            var platform = GetCurrentPlatform();
            if (string.IsNullOrWhiteSpace(platform))
            {
                return Array.Empty<string>();
            }

            if (string.Equals(platform, "win-x64", StringComparison.Ordinal))
            {
                return new[] { "win-x64-aot" };
            }

            return new[] { platform };
        }

        /// <summary>
        /// 根据 Unity Editor 平台和进程架构选择基础 Runtime profile；Windows 会在候选阶段映射为 AOT profile。
        /// </summary>
        /// <param name="platform">Unity Editor 当前平台。</param>
        /// <param name="architecture">当前进程架构。</param>
        /// <returns>基础 runtime identifier；不支持时返回空字符串。</returns>
        internal static string GetRuntimeIdentifier(RuntimePlatform platform, Architecture architecture)
        {
            if (platform == RuntimePlatform.WindowsEditor && architecture == Architecture.X64)
            {
                return "win-x64";
            }

            if (platform == RuntimePlatform.LinuxEditor && architecture == Architecture.X64)
            {
                return "linux-x64";
            }

            if (platform == RuntimePlatform.OSXEditor && architecture == Architecture.Arm64)
            {
                return "osx-arm64";
            }

            if (platform == RuntimePlatform.OSXEditor && architecture == Architecture.X64)
            {
                return "osx-x64";
            }

            return string.Empty;
        }

        /// <summary>
        /// 获取 Unity Editor 主窗口 HWND；非 Windows 或句柄不可用时返回 0。
        /// </summary>
        /// <returns>Unity Editor 主窗口 HWND。</returns>
        private static long GetEditorMainWindowHandle()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
            {
                return 0L;
            }

            using (Process process = Process.GetCurrentProcess())
            {
                return process.MainWindowHandle.ToInt64();
            }
        }

        /// <summary>
        /// 创建失败启动计划，并保留证据路径供用户修复源码包或项目缓存。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根目录。</param>
        /// <param name="message">错误说明。</param>
        /// <param name="evidencePath">证据路径。</param>
        /// <returns>失败启动计划。</returns>
        private static YokiFrameWorkbenchLaunchPlan CreateFailedPlan(string projectRoot, string message, string evidencePath)
        {
            return new YokiFrameWorkbenchLaunchPlan(
                false,
                string.Empty,
                string.Empty,
                projectRoot,
                message,
                evidencePath);
        }

        /// <summary>
        /// 获取 Unity 项目根目录。
        /// </summary>
        /// <returns>Unity 项目根目录。</returns>
        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        /// <summary>
        /// 从 Unity 资源数据库定位当前 launcher 脚本所在包根；包安装在 Packages 时优先使用 PackageInfo 的真实磁盘路径。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根目录。</param>
        /// <returns>YokiFrame 包根绝对路径；定位失败时返回空字符串。</returns>
        private static string GetPackageRoot(string projectRoot)
        {
            var launcherAssetPath = FindLauncherAssetPath();
            if (string.IsNullOrWhiteSpace(launcherAssetPath))
            {
                return string.Empty;
            }

            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(launcherAssetPath);
            if (packageInfo != null && !string.IsNullOrWhiteSpace(packageInfo.resolvedPath))
            {
                return Path.GetFullPath(packageInfo.resolvedPath);
            }

            return ResolvePackageRootFromAssetPath(projectRoot, launcherAssetPath);
        }

        /// <summary>
        /// 查找当前 launcher 脚本的 Unity 资源路径，用于让发布包根随脚本位置移动。
        /// </summary>
        /// <returns>launcher 脚本资源路径；找不到时返回空字符串。</returns>
        private static string FindLauncherAssetPath()
        {
            var fastPath = AssetDatabase.GUIDToAssetPath(LAUNCHER_SCRIPT_GUID).Replace('\\', '/');
            if (IsLauncherAssetPath(fastPath))
            {
                return fastPath;
            }

            var guids = AssetDatabase.FindAssets(LAUNCHER_SCRIPT_NAME + " t:Script");
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
                if (IsLauncherAssetPath(assetPath))
                {
                    return assetPath;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// 判断候选资源路径是否为当前 Workbench launcher 脚本；用于 GUID 快速路径和全局搜索回退共用同一规则。
        /// </summary>
        /// <param name="assetPath">Unity 资源路径。</param>
        /// <returns>候选路径指向 launcher 脚本时返回 true。</returns>
        private static bool IsLauncherAssetPath(string assetPath)
        {
            return !string.IsNullOrWhiteSpace(assetPath)
                && assetPath.EndsWith(LAUNCHER_SCRIPT_RELATIVE_PATH, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// 从 launcher 脚本资源路径反推出 YokiFrame 包根，避免写死 `Assets/YokiFrame`。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根目录。</param>
        /// <param name="launcherAssetPath">launcher 脚本资源路径。</param>
        /// <returns>YokiFrame 包根绝对路径；路径不匹配时返回空字符串。</returns>
        internal static string ResolvePackageRootFromAssetPath(string projectRoot, string launcherAssetPath)
        {
            if (string.IsNullOrWhiteSpace(projectRoot) || string.IsNullOrWhiteSpace(launcherAssetPath))
            {
                return string.Empty;
            }

            var normalizedAssetPath = launcherAssetPath.Replace('\\', '/').Trim('/');
            if (!normalizedAssetPath.EndsWith(LAUNCHER_SCRIPT_RELATIVE_PATH, StringComparison.OrdinalIgnoreCase))
            {
                return string.Empty;
            }

            var packageRelativePath = normalizedAssetPath
                .Substring(0, normalizedAssetPath.Length - LAUNCHER_SCRIPT_RELATIVE_PATH.Length)
                .TrimEnd('/');
            if (string.IsNullOrWhiteSpace(packageRelativePath))
            {
                return string.Empty;
            }

            return Path.GetFullPath(Path.Combine(projectRoot, NormalizeRelativePath(packageRelativePath)));
        }

    }
}

#endif
