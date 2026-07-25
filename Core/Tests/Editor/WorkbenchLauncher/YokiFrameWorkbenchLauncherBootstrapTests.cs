using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 验证 Unity Workbench Runtime bootstrap 的异步边界、进度反馈与 Editor 生命周期所有权。
    /// </summary>
    public sealed partial class YokiFrameWorkbenchLauncherTests
    {
        /// <summary>
        /// 验证 Ctrl+E 后台编译期间会持续显示进度，且无论成功或失败都由 finally 清理 Editor 进度条。
        /// </summary>
        [Test]
        public void RuntimeBootstrapShowsAndClearsEditorProgress()
        {
            string runtimeSource = ReadRuntimeCacheLauncherSource();
            string lifecycleSource = ReadBootstrapLifecycleSource();

            StringAssert.Contains("BootstrapRuntimeCacheWithProgressAsync", runtimeSource);
            StringAssert.Contains("EditorUtility.DisplayProgressBar", runtimeSource);
            StringAssert.Contains("Task.Delay(RUNTIME_BOOTSTRAP_PROGRESS_UPDATE_INTERVAL_MILLISECONDS)", runtimeSource);
            StringAssert.Contains("ClearRuntimeBootstrapProgressSafely", lifecycleSource);
            StringAssert.Contains("EditorUtility.ClearProgressBar();", lifecycleSource);
            Assert.Greater(
                runtimeSource.IndexOf("ClearRuntimeBootstrapProgressSafely();", StringComparison.Ordinal),
                runtimeSource.IndexOf("finally", StringComparison.Ordinal));
        }

        /// <summary>
        /// 验证 bootstrap 已绑定 Domain Reload、Editor 退出和有限进程终止等待，不把外部进程留给失效脚本域。
        /// </summary>
        [Test]
        public void RuntimeBootstrapBindsEditorLifecycleCancellation()
        {
            string lifecycleSource = ReadBootstrapLifecycleSource();

            StringAssert.Contains("AssemblyReloadEvents.beforeAssemblyReload", lifecycleSource);
            StringAssert.Contains("EditorApplication.quitting", lifecycleSource);
            StringAssert.Contains("CancellationTokenSource", lifecycleSource);
            StringAssert.Contains("TryTerminateProcessTree", lifecycleSource);
            StringAssert.Contains("GetRemainingTerminationWaitMilliseconds", lifecycleSource);
            StringAssert.Contains("TryTerminateWindowsProcessTree", lifecycleSource);
            StringAssert.Contains("JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE", ReadWindowsProcessJobSource());
        }

        /// <summary>
        /// 验证进程启动、Job owner 创建和 operation 发布共享同一个状态锁，取消回调不能穿过半发布窗口。
        /// </summary>
        [Test]
        public void RuntimeBootstrapStartsAndPublishesProcessInsideStateLock()
        {
            string lifecycleSource = ReadBootstrapLifecycleSource();
            string methodSource = ExtractSourceSegment(
                lifecycleSource,
                "private static bool TryStartAndTrackRuntimeBootstrapProcess(",
                "private static void UntrackRuntimeBootstrapProcess(");
            string normalizedSource = methodSource.Replace("\r\n", "\n");

            int lockIndex = normalizedSource.IndexOf("lock (sRuntimeBootstrapStateLock)", StringComparison.Ordinal);
            int processStartIndex = normalizedSource.IndexOf("process.Start()", StringComparison.Ordinal);
            int ownerCreateIndex = normalizedSource.IndexOf("CreateRuntimeBootstrapProcessTreeOwner(process)", StringComparison.Ordinal);
            int processPublishIndex = normalizedSource.IndexOf("operation.Process = process;", StringComparison.Ordinal);
            int ownerPublishIndex = normalizedSource.IndexOf("operation.ProcessTreeOwner = processTreeOwner;", StringComparison.Ordinal);
            int lockEndIndex = normalizedSource.LastIndexOf("\n            }", StringComparison.Ordinal);

            Assert.That(lockIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(processStartIndex, Is.GreaterThan(lockIndex));
            Assert.That(ownerCreateIndex, Is.GreaterThan(processStartIndex));
            Assert.That(processPublishIndex, Is.GreaterThan(ownerCreateIndex));
            Assert.That(ownerPublishIndex, Is.GreaterThan(processPublishIndex).And.LessThan(lockEndIndex));
            StringAssert.DoesNotContain("process.Start()", ReadRuntimeCacheLauncherSource());
        }

        /// <summary>
        /// 在 Windows 启动已拥有子进程的受控 bootstrap 树，验证 fallback 能终止登记前已经创建的后代。
        /// </summary>
        [Test]
        public void RuntimeBootstrapTerminatesExistingWindowsChildTree()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("Windows process-tree verification only runs in the Windows Editor.");

            Process child = null;
            object processTreeOwner = null;
            using (Process parent = StartWindowsProcessTree())
            {
                try
                {
                    string childIdText = ReadChildProcessId(parent);
                    child = Process.GetProcessById(int.Parse(childIdText));
                    Type launcherType = GetLauncherType();
                    MethodInfo createOwner = launcherType.GetMethod(
                        "CreateRuntimeBootstrapProcessTreeOwner",
                        BindingFlags.Static | BindingFlags.NonPublic);
                    MethodInfo terminate = launcherType.GetMethod(
                        "TryTerminateProcessTree",
                        BindingFlags.Static | BindingFlags.NonPublic);
                    Assert.IsNotNull(createOwner);
                    Assert.IsNotNull(terminate);
                    processTreeOwner = createOwner.Invoke(null, new object[] { parent });
                    Assert.IsNotNull(processTreeOwner, "当前 Windows Editor 必须建立 Job Object owner。");

                    bool terminated = (bool)terminate.Invoke(null, new[] { parent, processTreeOwner });

                    Assert.IsTrue(terminated);
                    Assert.IsTrue(parent.WaitForExit(1000), "bootstrap 根进程未退出。");
                    Assert.IsTrue(child.WaitForExit(1000), "bootstrap 子进程未随树终止。");
                }
                finally
                {
                    (processTreeOwner as IDisposable)?.Dispose();
                    TryKillProcess(child);
                    TryKillProcess(parent);
                    if (child != null) child.Dispose();
                }
            }
        }

        /// <summary>
        /// 验证父进程加入 Windows Job 后创建的子进程仍归同一 Job 所有，关闭 Job 即可终止完整进程树。
        /// </summary>
        [Test]
        public void WindowsJobTerminatesChildCreatedAfterParentAssignment()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("Windows Job Object verification only runs in the Windows Editor.");

            Process child = null;
            object processTreeOwner = null;
            using (Process parent = StartWindowsProcessTreeAfterSignal())
            {
                try
                {
                    Type jobType = GetLauncherType().GetNestedType(
                        "WindowsRuntimeBootstrapJob",
                        BindingFlags.NonPublic);
                    Assert.IsNotNull(jobType);
                    MethodInfo tryCreate = jobType.GetMethod("TryCreate", BindingFlags.Static | BindingFlags.NonPublic);
                    Assert.IsNotNull(tryCreate);
                    object[] createArguments = { parent, null };
                    processTreeOwner = tryCreate.Invoke(null, createArguments);
                    Assert.IsNotNull(processTreeOwner, createArguments[1] as string);

                    parent.StandardInput.WriteLine("spawn");
                    parent.StandardInput.Flush();
                    child = Process.GetProcessById(int.Parse(ReadChildProcessId(parent)));
                    MethodInfo tryTerminate = jobType.GetMethod("TryTerminate", BindingFlags.Instance | BindingFlags.Public);
                    Assert.IsNotNull(tryTerminate);

                    Assert.IsTrue((bool)tryTerminate.Invoke(processTreeOwner, null));
                    Assert.IsTrue(parent.WaitForExit(2000), "Job 关闭后 bootstrap 根进程未退出。");
                    Assert.IsTrue(child.WaitForExit(2000), "Job 关闭后延迟创建的子进程未退出。");
                }
                finally
                {
                    (processTreeOwner as IDisposable)?.Dispose();
                    TryKillProcess(child);
                    TryKillProcess(parent);
                    if (child != null) child.Dispose();
                }
            }
        }

        /// <summary>
        /// 验证 Runtime bootstrap 队列返回可观察的 Task；菜单入口显式丢弃该 Task，避免新增 async void 异常边界。
        /// </summary>
        [Test]
        public void RuntimeBootstrapQueueUsesTaskBoundaryInsteadOfAsyncVoid()
        {
            MethodInfo method = GetLauncherType().GetMethod(
                "QueueRuntimeBootstrapAndLaunch",
                BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(method);
            Assert.AreEqual(typeof(System.Threading.Tasks.Task), method.ReturnType);

            string runtimeSource = ReadRuntimeCacheLauncherSource();
            string launcherSource = ReadLauncherSource();
            StringAssert.DoesNotContain("async void QueueRuntimeBootstrapAndLaunch", runtimeSource);
            StringAssert.Contains("if (plan == null)", runtimeSource);
            StringAssert.Contains("operation.Token.Register", runtimeSource);
            StringAssert.DoesNotContain("static state => TryTerminateProcessTree((Process)state)", runtimeSource);
            StringAssert.Contains("_ = QueueRuntimeBootstrapAndLaunch(plan);", launcherSource);
        }

        /// <summary>
        /// 验证 Ctrl+E bootstrap 失败能识别 .NET SDK 与 Native AOT 工具链缺失，并提供对应菜单入口。
        /// </summary>
        [Test]
        public void RuntimeBootstrapFailureIdentifiesMissingBuildEnvironment()
        {
            Type launcherType = GetLauncherType();
            MethodInfo createMessage = launcherType.GetMethod(
                "CreateRuntimeBootstrapFailureMessage",
                BindingFlags.Static | BindingFlags.NonPublic);
            string environmentSource = ReadLauncherPartialSource("YokiFrameWorkbenchLauncher.BootstrapEnvironment.cs");

            Assert.IsNotNull(createMessage);
            StringAssert.Contains("YokiFrame/Workbench/打开缺失的编译环境", environmentSource);
            StringAssert.Contains("Application.OpenURL", environmentSource);
            StringAssert.Contains("DOTNET_10_SDK_DOWNLOAD_URL", environmentSource);
            StringAssert.Contains("VISUAL_STUDIO_BUILD_TOOLS_DOWNLOAD_URL", environmentSource);

            string dotnetMessage = (string)createMessage.Invoke(
                null,
                new object[] { "NETSDK1045: The current .NET SDK does not support targeting .NET 10.0." });
            string nativeAotMessage = (string)createMessage.Invoke(
                null,
                new object[] { "Platform linker not found. Ensure that Visual Studio 2022 is installed." });

            StringAssert.Contains("缺少 .NET 10 SDK", dotnetMessage);
            StringAssert.Contains("缺少 Visual Studio 2022 C++ Build Tools", nativeAotMessage);
            StringAssert.Contains("再次按 Ctrl+E", dotnetMessage);
        }

        /// <summary>
        /// 读取 Ctrl+E Runtime bootstrap 实现，验证后台编译不会丢失 Editor 进度反馈。
        /// </summary>
        /// <returns>Runtime 缓存启动器源码文本。</returns>
        private static string ReadRuntimeCacheLauncherSource()
        {
            return ReadLauncherPartialSource("YokiFrameWorkbenchLauncher.RuntimeCache.cs");
        }

        /// <summary>
        /// 读取 bootstrap 生命周期 partial，供进度清理和 Editor 退出契约测试共用。
        /// </summary>
        /// <returns>生命周期管理源码文本。</returns>
        private static string ReadBootstrapLifecycleSource()
        {
            return ReadLauncherPartialSource("YokiFrameWorkbenchLauncher.BootstrapLifecycle.cs");
        }

        /// <summary>
        /// 读取 Windows Job Object partial，验证 Unity Mono 缺少 Kill(bool) 时仍具备完整树终止 owner。
        /// </summary>
        /// <returns>Windows 进程树所有权实现源码。</returns>
        private static string ReadWindowsProcessJobSource()
        {
            return ReadLauncherPartialSource("YokiFrameWorkbenchLauncher.WindowsProcessJob.cs");
        }

        /// <summary>提取两个稳定方法签名之间的源码，供锁与调用顺序契约测试使用。</summary>
        /// <param name="source">完整 partial 源码。</param>
        /// <param name="startMarker">目标片段起始签名。</param>
        /// <param name="endMarker">目标片段之后的下一方法签名。</param>
        /// <returns>包含起始签名且不包含结束签名的源码片段。</returns>
        private static string ExtractSourceSegment(string source, string startMarker, string endMarker)
        {
            int startIndex = source.IndexOf(startMarker, StringComparison.Ordinal);
            Assert.That(startIndex, Is.GreaterThanOrEqualTo(0), startMarker);
            int endIndex = source.IndexOf(endMarker, startIndex, StringComparison.Ordinal);
            Assert.That(endIndex, Is.GreaterThan(startIndex), endMarker);
            return source.Substring(startIndex, endIndex - startIndex);
        }

        /// <summary>启动 PowerShell 父进程，并让它创建可由测试观测的长期 ping 子进程。</summary>
        /// <returns>标准输出首行会报告子进程 PID 的父进程。</returns>
        private static Process StartWindowsProcessTree()
        {
            const string script = "$child = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\\PING.EXE') "
                + "-ArgumentList @('127.0.0.1','-t') -PassThru; "
                + "[Console]::Out.WriteLine($child.Id); [Console]::Out.Flush(); $child.WaitForExit()";
            return StartWindowsPowerShellProcess(script, false);
        }

        /// <summary>启动等待 stdin 信号的 PowerShell 父进程，确保它只在加入 Job 后创建长期子进程。</summary>
        /// <returns>收到任意输入行后创建 ping 子进程并报告 PID 的父进程。</returns>
        private static Process StartWindowsProcessTreeAfterSignal()
        {
            const string script = "$null = [Console]::In.ReadLine(); "
                + "$child = Start-Process -FilePath (Join-Path $env:SystemRoot 'System32\\PING.EXE') "
                + "-ArgumentList @('127.0.0.1','-t') -PassThru; "
                + "[Console]::Out.WriteLine($child.Id); [Console]::Out.Flush(); $child.WaitForExit()";
            return StartWindowsPowerShellProcess(script, true);
        }

        /// <summary>按测试脚本启动隐藏 PowerShell，并统一配置父子进程观测所需的标准流。</summary>
        /// <param name="script">在 PowerShell 中执行的进程树脚本。</param>
        /// <param name="redirectStandardInput">是否重定向 stdin 以控制子进程创建时机。</param>
        /// <returns>已经成功启动的 PowerShell 父进程。</returns>
        private static Process StartWindowsPowerShellProcess(string script, bool redirectStandardInput)
        {
            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = "-NoLogo -NoProfile -NonInteractive -Command \"" + script + "\"",
                    UseShellExecute = false,
                    RedirectStandardInput = redirectStandardInput,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            Assert.IsTrue(process.Start());
            return process;
        }

        /// <summary>在有限时间内读取测试父进程报告的子进程 PID。</summary>
        /// <param name="parent">已启动且重定向标准输出的父进程。</param>
        /// <returns>可解析为正整数的 PID 文本。</returns>
        private static string ReadChildProcessId(Process parent)
        {
            var readTask = parent.StandardOutput.ReadLineAsync();
            Assert.IsTrue(readTask.Wait(5000), "测试父进程未及时报告子进程 PID。");
            Assert.IsFalse(string.IsNullOrWhiteSpace(readTask.Result));
            return readTask.Result;
        }

        /// <summary>清理行为测试异常退出时可能仍活动的受控进程。</summary>
        /// <param name="process">待清理进程；为空、已退出或已释放时忽略。</param>
        private static void TryKillProcess(Process process)
        {
            try
            {
                if (process != null && !process.HasExited) process.Kill();
            }
            catch (InvalidOperationException)
            {
                // 测试清理与进程自然退出竞态时无需重复终止。
            }
        }

        /// <summary>
        /// 从固定 WorkbenchLauncher 目录读取指定 partial，缺失时返回清晰测试失败。
        /// </summary>
        /// <param name="fileName">目标源码文件名。</param>
        /// <returns>源码文本。</returns>
        private static string ReadLauncherPartialSource(string fileName)
        {
            string path = Path.Combine(
                Application.dataPath,
                "YokiFrame",
                "Core",
                "Adapters",
                "Unity",
                "Editor",
                "WorkbenchLauncher",
                fileName);
            Assert.IsTrue(File.Exists(path), path);
            return File.ReadAllText(path);
        }
    }
}
