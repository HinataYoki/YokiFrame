#if UNITY_EDITOR

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using UnityEditor;
using Debug = UnityEngine.Debug;

namespace YokiFrame
{
    /// <summary>
    /// 绑定 Workbench Runtime bootstrap 与 Unity Editor 生命周期，确保 reload 或退出不会遗留构建进程。
    /// </summary>
    [InitializeOnLoad]
    internal static partial class YokiFrameWorkbenchLauncher
    {
        private static readonly object sRuntimeBootstrapStateLock = new object();
        private static readonly MethodInfo sKillProcessTreeMethod = typeof(Process).GetMethod(
            "Kill",
            new[] { typeof(bool) });
        private const int RUNTIME_BOOTSTRAP_TERMINATION_WAIT_MILLISECONDS = 2000;

        private static volatile bool sRuntimeBootstrapInFlight;
        private static RuntimeBootstrapOperation sRuntimeBootstrapOperation;

        /// <summary>
        /// 注册 Domain Reload 与 Editor 退出回调；每个脚本域只注册一次。
        /// </summary>
        static YokiFrameWorkbenchLauncher()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            EditorApplication.quitting -= OnEditorQuitting;
            EditorApplication.quitting += OnEditorQuitting;
        }

        /// <summary>
        /// 原子创建一次 bootstrap operation，避免重复 Ctrl+E 启动并行发布事务。
        /// </summary>
        /// <param name="operation">成功时返回本次操作 owner。</param>
        /// <returns>当前没有其它 bootstrap 时返回 true。</returns>
        private static bool TryBeginRuntimeBootstrap(out RuntimeBootstrapOperation operation)
        {
            lock (sRuntimeBootstrapStateLock)
            {
                if (sRuntimeBootstrapOperation != null)
                {
                    operation = null;
                    return false;
                }

                operation = new RuntimeBootstrapOperation();
                sRuntimeBootstrapOperation = operation;
                sRuntimeBootstrapInFlight = true;
                return true;
            }
        }

        /// <summary>
        /// 在状态锁内启动并登记进程，确保生命周期取消无法观察到已启动但尚未发布的进程。
        /// </summary>
        /// <param name="operation">发起 bootstrap 的 operation。</param>
        /// <param name="process">配置完成但尚未启动的 dotnet 进程。</param>
        /// <param name="processTreeOwner">成功时返回平台进程树 owner；平台不支持或进程已退出时为空。</param>
        /// <returns>进程已启动并由当前 Launcher operation 接管时返回 true。</returns>
        private static bool TryStartAndTrackRuntimeBootstrapProcess(
            RuntimeBootstrapOperation operation,
            Process process,
            out IRuntimeBootstrapProcessTreeOwner processTreeOwner)
        {
            lock (sRuntimeBootstrapStateLock)
            {
                processTreeOwner = null;
                if (!ReferenceEquals(sRuntimeBootstrapOperation, operation)
                    || operation.IsCancellationRequested)
                {
                    return false;
                }

                if (!process.Start())
                {
                    return false;
                }

                if (!HasProcessExited(process))
                {
                    processTreeOwner = CreateRuntimeBootstrapProcessTreeOwner(process);
                }

                operation.Process = process;
                operation.ProcessTreeOwner = processTreeOwner;
                return true;
            }
        }

        /// <summary>
        /// 仅在当前 operation 仍登记同一进程时解除跟踪，防止迟到清理覆盖新操作。
        /// </summary>
        /// <param name="operation">进程所属 operation。</param>
        /// <param name="process">已结束或即将释放的进程。</param>
        private static void UntrackRuntimeBootstrapProcess(
            RuntimeBootstrapOperation operation,
            Process process,
            IRuntimeBootstrapProcessTreeOwner processTreeOwner)
        {
            lock (sRuntimeBootstrapStateLock)
            {
                if (ReferenceEquals(sRuntimeBootstrapOperation, operation)
                    && ReferenceEquals(operation.Process, process)
                    && ReferenceEquals(operation.ProcessTreeOwner, processTreeOwner))
                {
                    operation.Process = null;
                    operation.ProcessTreeOwner = null;
                }
            }
        }

        /// <summary>
        /// 完成本次 operation；只有当前 owner 可以清除 in-flight 状态，operation 自身始终幂等释放。
        /// </summary>
        /// <param name="operation">待完成的 bootstrap operation。</param>
        private static void CompleteRuntimeBootstrap(RuntimeBootstrapOperation operation)
        {
            lock (sRuntimeBootstrapStateLock)
            {
                if (ReferenceEquals(sRuntimeBootstrapOperation, operation))
                {
                    sRuntimeBootstrapOperation = null;
                    sRuntimeBootstrapInFlight = false;
                }
            }

            operation.Dispose();
        }

        /// <summary>
        /// 取消当前 bootstrap；已登记的唯一取消回调会同步终止完整进程树。
        /// </summary>
        private static void CancelRuntimeBootstrap()
        {
            RuntimeBootstrapOperation operation;
            lock (sRuntimeBootstrapStateLock)
            {
                operation = sRuntimeBootstrapOperation;
            }

            if (operation != null) operation.TryCancel();
        }

        /// <summary>
        /// 终止指定 operation 当前登记的进程树；取消发生在进程登记前时保持无操作。
        /// </summary>
        /// <param name="operation">收到生命周期取消的 bootstrap operation。</param>
        private static void TerminateTrackedRuntimeBootstrapProcess(RuntimeBootstrapOperation operation)
        {
            Process process;
            IRuntimeBootstrapProcessTreeOwner processTreeOwner;
            lock (sRuntimeBootstrapStateLock)
            {
                process = operation.Process;
                processTreeOwner = operation.ProcessTreeOwner;
            }

            if (process != null && !TryTerminateProcessTree(process, processTreeOwner))
            {
                Debug.LogWarning(LOG_PREFIX + "Unable to terminate the Runtime bootstrap process tree after Editor lifecycle cancellation.");
            }
        }

        /// <summary>
        /// 在脚本域卸载前取消 bootstrap，避免后台 dotnet 进程失去 owner。
        /// </summary>
        private static void OnBeforeAssemblyReload()
        {
            CancelRuntimeBootstrap();
            ClearRuntimeBootstrapProgressSafely();
        }

        /// <summary>
        /// 在 Unity Editor 退出前取消 bootstrap，避免退出后继续写项目 Runtime 缓存。
        /// </summary>
        private static void OnEditorQuitting()
        {
            CancelRuntimeBootstrap();
            ClearRuntimeBootstrapProgressSafely();
        }

        /// <summary>
        /// 安全清理 Editor 进度条；reload 或退出阶段的 Unity API 异常只保留诊断。
        /// </summary>
        private static void ClearRuntimeBootstrapProgressSafely()
        {
            try
            {
                EditorUtility.ClearProgressBar();
            }
            catch (Exception exception)
            {
                Debug.LogWarning(LOG_PREFIX + "Unable to clear the Runtime bootstrap progress bar: " + exception.Message);
            }
        }

        /// <summary>
        /// 为已启动进程创建平台级进程树 owner；当前 Windows 使用 Job Object 承担硬生命周期所有权。
        /// </summary>
        /// <param name="process">已经启动但尚未进入等待阶段的 bootstrap 进程。</param>
        /// <returns>成功接管进程树时返回 owner；平台不支持或接管失败时返回 null。</returns>
        private static IRuntimeBootstrapProcessTreeOwner CreateRuntimeBootstrapProcessTreeOwner(Process process)
        {
#if UNITY_EDITOR_WIN
            string error;
            var owner = WindowsRuntimeBootstrapJob.TryCreate(process, out error);
            if (owner == null)
            {
                Debug.LogWarning(LOG_PREFIX + "Unable to attach the Runtime bootstrap process to a Windows Job Object: " + error);
            }

            return owner;
#else
            return null;
#endif
        }

        /// <summary>
        /// 优先释放平台进程树 owner，再尝试运行时或系统树终止能力，最后终止直接进程。
        /// </summary>
        /// <param name="process">待终止进程。</param>
        /// <param name="processTreeOwner">启动后接管该进程树的平台 owner；平台不支持时为空。</param>
        /// <returns>进程已结束或在有限等待内确认退出时返回 true。</returns>
        private static bool TryTerminateProcessTree(
            Process process,
            IRuntimeBootstrapProcessTreeOwner processTreeOwner)
        {
            if (process == null || HasProcessExited(process))
            {
                return true;
            }

            var elapsed = Stopwatch.StartNew();
            if (TryTerminateWindowsProcessTree(process, elapsed))
            {
                if (processTreeOwner != null) processTreeOwner.TryTerminate();
                if (WaitForProcessExit(process, elapsed)) return true;
            }

            if (processTreeOwner != null
                && processTreeOwner.TryTerminate()
                && WaitForProcessExit(process, elapsed))
            {
                return true;
            }

            if (TryInvokeProcessTreeKill(process) && WaitForProcessExit(process, elapsed))
            {
                return true;
            }

            try
            {
                process.Kill();
                return WaitForProcessExit(process, elapsed);
            }
            catch (Exception exception) when (IsExpectedProcessLifecycleException(exception))
            {
                return HasProcessExited(process);
            }
        }

        /// <summary>
        /// 在旧 Unity Mono 缺少 `Kill(bool)` 时使用 Windows 系统工具按 PID 终止完整进程树。
        /// </summary>
        /// <param name="process">待终止的 bootstrap 根进程。</param>
        /// <returns>系统树终止命令成功，或目标进程已并发退出时返回 true。</returns>
        private static bool TryTerminateWindowsProcessTree(Process process, Stopwatch elapsed)
        {
#if UNITY_EDITOR_WIN
            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.System),
                        "taskkill.exe"),
                    Arguments = "/PID " + process.Id + " /T /F",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (var terminator = Process.Start(startInfo))
                {
                    if (terminator == null) return false;
                    if (!terminator.WaitForExit(GetRemainingTerminationWaitMilliseconds(elapsed)))
                    {
                        terminator.Kill();
                        return false;
                    }

                    return terminator.ExitCode == 0 || HasProcessExited(process);
                }
            }
            catch (Exception exception) when (IsExpectedProcessLifecycleException(exception))
            {
                return HasProcessExited(process);
            }
#else
            return false;
#endif
        }

        /// <summary>
        /// 在 reload/quit 取消路径上等待子进程退出，但不允许外部进程拖住 Unity 生命周期无限期等待。
        /// </summary>
        /// <param name="process">已提交终止请求的进程。</param>
        /// <returns>在限定时间内确认退出时返回 true。</returns>
        private static bool WaitForProcessExit(Process process, Stopwatch elapsed)
        {
            try
            {
                if (process.WaitForExit(GetRemainingTerminationWaitMilliseconds(elapsed)))
                {
                    return true;
                }
            }
            catch (Exception exception) when (IsExpectedProcessLifecycleException(exception))
            {
                return HasProcessExited(process);
            }

            return HasProcessExited(process);
        }

        /// <summary>
        /// 计算本次完整终止流程剩余等待预算，确保多个 fallback 共用同一个两秒 deadline。
        /// </summary>
        /// <param name="elapsed">从第一次终止请求开始计时的秒表。</param>
        /// <returns>剩余毫秒数；预算耗尽时返回 0。</returns>
        private static int GetRemainingTerminationWaitMilliseconds(Stopwatch elapsed)
        {
            long remaining = RUNTIME_BOOTSTRAP_TERMINATION_WAIT_MILLISECONDS - elapsed.ElapsedMilliseconds;
            return remaining > 0L ? (int)remaining : 0;
        }

        /// <summary>
        /// 通过反射调用较新运行时的 `Kill(bool)`，保持 Unity netstandard2.1 编译边界。
        /// </summary>
        /// <param name="process">待终止进程。</param>
        /// <returns>完整进程树终止请求已成功提交时返回 true。</returns>
        private static bool TryInvokeProcessTreeKill(Process process)
        {
            if (sKillProcessTreeMethod == null)
            {
                return false;
            }

            try
            {
                sKillProcessTreeMethod.Invoke(process, new object[] { true });
                return true;
            }
            catch (Exception exception) when (IsExpectedProcessLifecycleException(exception))
            {
                return false;
            }
        }

        /// <summary>
        /// 安全读取进程终态；尚未启动、已释放或平台查询失败时按未确认结束处理。
        /// </summary>
        /// <param name="process">待检查进程。</param>
        /// <returns>能够确认进程已退出时返回 true。</returns>
        private static bool HasProcessExited(Process process)
        {
            try
            {
                return process.HasExited;
            }
            catch (Exception exception) when (IsExpectedProcessLifecycleException(exception))
            {
                return false;
            }
        }

        /// <summary>
        /// 识别取消、退出和跨平台进程 API 在正常生命周期竞态中允许降级的异常。
        /// </summary>
        /// <param name="exception">待分类异常。</param>
        /// <returns>异常属于可恢复进程生命周期边界时返回 true。</returns>
        private static bool IsExpectedProcessLifecycleException(Exception exception)
        {
            return exception is InvalidOperationException
                || exception is ObjectDisposedException
                || exception is Win32Exception
                || exception is IOException
                || exception is NotSupportedException
                || exception is PlatformNotSupportedException
                || exception is TargetInvocationException;
        }

        /// <summary>
        /// 表示可在脚本域卸载时终止所拥有完整进程树的平台资源。
        /// </summary>
        private interface IRuntimeBootstrapProcessTreeOwner : IDisposable
        {
            /// <summary>幂等终止当前 owner 接管的完整进程树。</summary>
            /// <returns>终止请求已提交时返回 true。</returns>
            bool TryTerminate();
        }

        /// <summary>
        /// 保存一次 Runtime bootstrap 的取消源、当前子进程与平台进程树 owner；所有引用读写均由状态锁保护。
        /// </summary>
        private sealed class RuntimeBootstrapOperation : IDisposable
        {
            private readonly CancellationTokenSource mCancellationSource = new CancellationTokenSource();
            private int mDisposed;

            /// <summary>获取本次 bootstrap 的取消令牌。</summary>
            public CancellationToken Token => mCancellationSource.Token;

            /// <summary>获取当前 operation 是否已经收到取消请求。</summary>
            public bool IsCancellationRequested => mCancellationSource.IsCancellationRequested;

            /// <summary>获取或设置当前已登记的 Packaging 子进程；仅允许在 Launcher 状态锁内访问。</summary>
            public Process Process { get; set; }

            /// <summary>获取或设置当前进程的平台树 owner；仅允许在 Launcher 状态锁内访问。</summary>
            public IRuntimeBootstrapProcessTreeOwner ProcessTreeOwner { get; set; }

            /// <summary>幂等提交取消请求，operation 已释放时保持无异常。</summary>
            public void TryCancel()
            {
                try
                {
                    mCancellationSource.Cancel();
                }
                catch (ObjectDisposedException)
                {
                    // operation 已完成时不再存在需要取消的进程。
                }
            }

            /// <summary>幂等释放取消源；Process 生命周期由执行 bootstrap 的线程负责。</summary>
            public void Dispose()
            {
                if (Interlocked.Exchange(ref mDisposed, 1) == 0)
                {
                    mCancellationSource.Dispose();
                }
            }
        }
    }
}

#endif
