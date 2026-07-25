#if UNITY_EDITOR

using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace YokiFrame
{
    /// <summary>
    /// 承载 Unity Ctrl+E 触发的项目级 Runtime 缓存检查与后台 bootstrap。
    /// </summary>
    internal static partial class YokiFrameWorkbenchLauncher
    {
        private const int RUNTIME_CACHE_LAYOUT_VERSION = 1;
        private const string PACKAGING_PROJECT_RELATIVE_PATH =
            "YokiFrameWorkbench~/src/YokiFrame.Packaging/YokiFrame.Packaging.csproj";
        private const string RUNTIME_BOOTSTRAP_PROGRESS_TITLE = "YokiFrame Workbench";
        private const string RUNTIME_BOOTSTRAP_PROGRESS_BUILDING_MESSAGE = "正在编译 Workbench Runtime，已耗时 {0} 秒...";
        private const string RUNTIME_BOOTSTRAP_PROGRESS_FINALIZING_MESSAGE = "正在验证编译产物...";
        private const int RUNTIME_BOOTSTRAP_PROGRESS_UPDATE_INTERVAL_MILLISECONDS = 150;
        private const float RUNTIME_BOOTSTRAP_PROGRESS_MINIMUM = 0.08f;
        private const float RUNTIME_BOOTSTRAP_PROGRESS_MAXIMUM = 0.88f;
        private const double RUNTIME_BOOTSTRAP_PROGRESS_RAMP_SECONDS = 18.0;

        /// <summary>
        /// 创建需要后台 bootstrap 的启动计划；此时尚未执行外部进程，也不会写入包根。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根。</param>
        /// <param name="packageRoot">只读 YokiFrame 源码包根。</param>
        /// <param name="sourceFingerprint">当前 Workbench 源码指纹。</param>
        /// <param name="parentWindowHandle">Unity Editor 主窗口句柄。</param>
        /// <param name="reason">缓存不可直接启动的原因。</param>
        /// <returns>要求 Runtime bootstrap 的启动计划。</returns>
        private static YokiFrameWorkbenchLaunchPlan CreateBootstrapPlan(
            string projectRoot,
            string packageRoot,
            string sourceFingerprint,
            long parentWindowHandle,
            string reason)
        {
            var runtimeRoot = YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, sourceFingerprint);
            return new YokiFrameWorkbenchLaunchPlan(
                true,
                string.Empty,
                CreateWorkbenchArguments(projectRoot, packageRoot, parentWindowHandle),
                projectRoot,
                reason,
                YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(projectRoot),
                true,
                packageRoot,
                runtimeRoot);
        }

        /// <summary>
        /// 读取项目当前 Runtime 指针；源码是否有新版由 Workbench 启动后的后台任务检查。
        /// </summary>
        /// <param name="projectRoot">Unity 项目根。</param>
        /// <param name="sourceFingerprint">指针记录的当前 Runtime 源码指纹。</param>
        /// <returns>当前指纹 Runtime 根；指针缺失或损坏时返回空文本。</returns>
        private static string ResolveCurrentRuntimeRoot(string projectRoot, out string sourceFingerprint)
        {
            sourceFingerprint = string.Empty;
            var pointerPath = YokiFrameWorkbenchRuntimeCacheLayout.GetCurrentFilePath(projectRoot);
            if (!File.Exists(pointerPath))
            {
                return string.Empty;
            }

            var pointer = JsonUtility.FromJson<YokiFrameWorkbenchRuntimePointer>(File.ReadAllText(pointerPath));
            if (pointer == null
                || pointer.layoutVersion != RUNTIME_CACHE_LAYOUT_VERSION
                || string.IsNullOrWhiteSpace(pointer.sourceFingerprint))
            {
                return string.Empty;
            }

            sourceFingerprint = pointer.sourceFingerprint;
            return YokiFrameWorkbenchRuntimeCacheLayout.GetRuntimeRoot(projectRoot, pointer.sourceFingerprint);
        }

        /// <summary>
        /// 在后台执行 Packaging bootstrap；重复 Ctrl+E 只复用正在进行的同一构建，避免两个 AOT 发布事务争用缓存。
        /// </summary>
        /// <param name="plan">要求 Runtime bootstrap 的启动计划。</param>
        private static async Task QueueRuntimeBootstrapAndLaunch(YokiFrameWorkbenchLaunchPlan plan)
        {
            if (plan == null)
            {
                Debug.LogError(LOG_PREFIX + "Project Runtime bootstrap was requested without a launch plan.");
                return;
            }

            RuntimeBootstrapOperation operation;
            if (!TryBeginRuntimeBootstrap(out operation))
            {
                Debug.Log(LOG_PREFIX + "Project Runtime bootstrap is already running.");
                return;
            }

            try
            {
                Debug.Log(LOG_PREFIX + "Bootstrapping project Runtime cache. package: " + plan.PackageRoot + " runtime: " + plan.RuntimeRoot);
                var bootstrapResult = await BootstrapRuntimeCacheWithProgressAsync(
                    plan.PackageRoot,
                    plan.WorkingDirectory,
                    operation);
                if (!bootstrapResult.Succeeded)
                {
                    Debug.LogError(LOG_PREFIX + CreateRuntimeBootstrapFailureMessage(bootstrapResult.Output));
                    return;
                }

                var launchPlan = CreateLaunchPlan(plan.WorkingDirectory, plan.PackageRoot, GetEditorMainWindowHandle());
                if (!launchPlan.CanLaunch || launchPlan.RequiresBootstrap)
                {
                    Debug.LogError(LOG_PREFIX + "Project Runtime bootstrap completed without a launchable Workbench. Evidence: " + launchPlan.EvidencePath);
                    return;
                }

                Launch(launchPlan, static startInfo => Process.Start(startInfo));
            }
            catch (OperationCanceledException) when (operation.IsCancellationRequested)
            {
                Debug.Log(LOG_PREFIX + "Project Runtime bootstrap was canceled by the Unity Editor lifecycle.");
            }
            catch (Exception exception)
            {
                Debug.LogError(LOG_PREFIX + "Project Runtime bootstrap failed: " + exception);
            }
            finally
            {
                ClearRuntimeBootstrapProgressSafely();
                CompleteRuntimeBootstrap(operation);
            }
        }

        /// <summary>
        /// 在后台执行 Runtime bootstrap，并在 Unity 主线程持续刷新进度条；进度只表示任务仍在运行，不伪造 Packaging 的实际完成比例。
        /// </summary>
        /// <param name="packageRoot">只读 YokiFrame 源码包根。</param>
        /// <param name="projectRoot">Unity 项目根。</param>
        /// <returns>后台 Packaging 命令结束后的进程结果。</returns>
        private static async Task<RuntimeBootstrapProcessResult> BootstrapRuntimeCacheWithProgressAsync(
            string packageRoot,
            string projectRoot,
            RuntimeBootstrapOperation operation)
        {
            var startedAtUtc = DateTime.UtcNow;
            var bootstrapTask = Task.Run(
                () => BootstrapRuntimeCache(packageRoot, projectRoot, operation),
                operation.Token);
            ShowRuntimeBootstrapProgress(startedAtUtc);
            while (!bootstrapTask.IsCompleted)
            {
                await Task.Delay(RUNTIME_BOOTSTRAP_PROGRESS_UPDATE_INTERVAL_MILLISECONDS);
                if (!bootstrapTask.IsCompleted && !operation.IsCancellationRequested)
                {
                    ShowRuntimeBootstrapProgress(startedAtUtc);
                }
            }

            var result = await bootstrapTask;
            operation.Token.ThrowIfCancellationRequested();
            EditorUtility.DisplayProgressBar(
                RUNTIME_BOOTSTRAP_PROGRESS_TITLE,
                RUNTIME_BOOTSTRAP_PROGRESS_FINALIZING_MESSAGE,
                0.95f);
            return result;
        }

        /// <summary>
        /// 根据后台任务已运行时间更新 Editor 进度条，避免 Native AOT 等长任务在界面上看起来没有响应。
        /// </summary>
        /// <param name="startedAtUtc">后台 bootstrap 的 UTC 开始时间。</param>
        private static void ShowRuntimeBootstrapProgress(DateTime startedAtUtc)
        {
            var elapsed = DateTime.UtcNow - startedAtUtc;
            var elapsedSeconds = Math.Max(0, (int)elapsed.TotalSeconds);
            EditorUtility.DisplayProgressBar(
                RUNTIME_BOOTSTRAP_PROGRESS_TITLE,
                string.Format(RUNTIME_BOOTSTRAP_PROGRESS_BUILDING_MESSAGE, elapsedSeconds),
                GetRuntimeBootstrapProgress(elapsed.TotalSeconds));
        }

        /// <summary>
        /// 将未知总耗时的后台编译映射为渐近进度，表达持续工作状态而不宣称精确完成比例。
        /// </summary>
        /// <param name="elapsedSeconds">自任务启动以来经过的秒数。</param>
        /// <returns>限制在初始与验证阶段之间的进度值。</returns>
        private static float GetRuntimeBootstrapProgress(double elapsedSeconds)
        {
            var normalizedElapsed = Math.Max(0.0, elapsedSeconds);
            var ramp = 1.0 - Math.Exp(-normalizedElapsed / RUNTIME_BOOTSTRAP_PROGRESS_RAMP_SECONDS);
            return RUNTIME_BOOTSTRAP_PROGRESS_MINIMUM
                + (float)ramp * (RUNTIME_BOOTSTRAP_PROGRESS_MAXIMUM - RUNTIME_BOOTSTRAP_PROGRESS_MINIMUM);
        }

        /// <summary>
        /// 调用 Packaging CLI 计算源码指纹并按需发布当前平台；Windows 会由 profile resolver 生成 Native AOT。
        /// </summary>
        /// <param name="packageRoot">只读 YokiFrame 源码包根。</param>
        /// <param name="projectRoot">Unity 项目根。</param>
        /// <returns>进程成功状态和用于 Console 诊断的输出。</returns>
        private static RuntimeBootstrapProcessResult BootstrapRuntimeCache(
            string packageRoot,
            string projectRoot,
            RuntimeBootstrapOperation operation)
        {
            var packagingProjectPath = Path.Combine(packageRoot, NormalizeRelativePath(PACKAGING_PROJECT_RELATIVE_PATH));
            if (!File.Exists(packagingProjectPath))
            {
                return new RuntimeBootstrapProcessResult(false, "Packaging project is missing: " + packagingProjectPath);
            }

            try
            {
                if (operation.IsCancellationRequested)
                {
                    return new RuntimeBootstrapProcessResult(false, "Runtime bootstrap was canceled before process start.");
                }

                ProcessStartInfo startInfo = CreateRuntimeBootstrapStartInfo(
                    packagingProjectPath,
                    packageRoot,
                    projectRoot);
                return RunRuntimeBootstrapProcess(startInfo, operation);
            }
            catch (Exception exception) when (
                exception is InvalidOperationException
                || exception is System.ComponentModel.Win32Exception
                || exception is IOException
                || exception is UnauthorizedAccessException
                || exception is AggregateException)
            {
                return new RuntimeBootstrapProcessResult(false, exception.Message);
            }
        }

        /// <summary>
        /// 创建不经过 shell 二次解释的 Packaging bootstrap 启动参数。
        /// </summary>
        /// <param name="packagingProjectPath">Packaging 项目路径。</param>
        /// <param name="packageRoot">YokiFrame 包根。</param>
        /// <param name="projectRoot">目标 Unity 项目根。</param>
        /// <returns>后台 dotnet 进程启动配置。</returns>
        private static ProcessStartInfo CreateRuntimeBootstrapStartInfo(
            string packagingProjectPath,
            string packageRoot,
            string projectRoot)
        {
            var arguments = "run --project " + QuoteArgument(packagingProjectPath)
                + " -- runtime bootstrap --package-root " + QuoteArgument(packageRoot)
                + " --project-root " + QuoteArgument(projectRoot)
                + " --configuration Release";
            return new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = arguments,
                WorkingDirectory = Path.GetDirectoryName(packagingProjectPath),
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
        }

        /// <summary>
        /// 启动并登记 bootstrap 子进程，统一绑定取消回调和进程所有权。
        /// </summary>
        /// <param name="startInfo">已验证的进程启动配置。</param>
        /// <param name="operation">当前 bootstrap 生命周期 owner。</param>
        /// <returns>子进程终态和合并后的输出。</returns>
        private static RuntimeBootstrapProcessResult RunRuntimeBootstrapProcess(
            ProcessStartInfo startInfo,
            RuntimeBootstrapOperation operation)
        {
            using (operation.Token.Register(
                       static state => TerminateTrackedRuntimeBootstrapProcess((RuntimeBootstrapOperation)state),
                       operation))
            using (var process = new Process { StartInfo = startInfo })
            {
                if (operation.IsCancellationRequested)
                {
                    return new RuntimeBootstrapProcessResult(false, "Runtime bootstrap was canceled before process start.");
                }

                IRuntimeBootstrapProcessTreeOwner processTreeOwner;
                if (!TryStartAndTrackRuntimeBootstrapProcess(operation, process, out processTreeOwner))
                {
                    return new RuntimeBootstrapProcessResult(
                        false,
                        operation.IsCancellationRequested
                            ? "Runtime bootstrap was canceled before process start."
                            : "Unable to start dotnet.");
                }

                try
                {
                    return WaitForRuntimeBootstrapProcess(process, operation);
                }
                finally
                {
                    UntrackRuntimeBootstrapProcess(operation, process, processTreeOwner);
                    if (processTreeOwner != null) processTreeOwner.Dispose();
                }
            }
        }

        /// <summary>
        /// 等待 bootstrap 进程退出并读取 stdout/stderr，避免丢失构建诊断。
        /// </summary>
        /// <param name="process">已登记的 bootstrap 进程。</param>
        /// <param name="operation">当前 bootstrap 生命周期 owner。</param>
        /// <returns>进程成功状态与合并输出。</returns>
        private static RuntimeBootstrapProcessResult WaitForRuntimeBootstrapProcess(
            Process process,
            RuntimeBootstrapOperation operation)
        {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();
            process.WaitForExit();
            Task.WaitAll(outputTask, errorTask);
            var output = outputTask.Result + errorTask.Result;
            return new RuntimeBootstrapProcessResult(
                !operation.IsCancellationRequested && process.ExitCode == 0,
                operation.IsCancellationRequested
                    ? "Runtime bootstrap was canceled."
                    : output.Trim());
        }

        /// <summary>
        /// 保存后台 Packaging 进程的成功状态与合并标准输出，避免 Unity Editor 直接依赖 Packaging 程序集。
        /// </summary>
        private sealed class RuntimeBootstrapProcessResult
        {
            /// <summary>
            /// 创建 bootstrap 进程结果。
            /// </summary>
            /// <param name="succeeded">dotnet 命令是否以 0 退出。</param>
            /// <param name="output">标准输出和错误输出的合并文本。</param>
            public RuntimeBootstrapProcessResult(bool succeeded, string output)
            {
                Succeeded = succeeded;
                Output = output;
            }

            /// <summary>获取 dotnet 命令是否成功。</summary>
            public bool Succeeded { get; }

            /// <summary>获取可用于 Unity Console 诊断的命令输出。</summary>
            public string Output { get; }
        }
    }
}

#endif
