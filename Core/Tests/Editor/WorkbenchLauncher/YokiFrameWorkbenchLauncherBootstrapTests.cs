using System;
using System.Diagnostics;
using System.Threading.Tasks;
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
        /// 在 Windows 启动已拥有子进程的受控 bootstrap 树，验证 fallback 能终止登记前已经创建的后代。
        /// </summary>
        [Test]
        public void RuntimeBootstrapTerminatesExistingWindowsChildTree()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("Windows process-tree verification only runs in the Windows Editor.");

            Process child = null;
            IDisposable processTreeOwner = null;
            using (Process parent = StartWindowsProcessTree())
            {
                try
                {
                    string childIdText = ReadChildProcessId(parent);
                    child = Process.GetProcessById(int.Parse(childIdText));
                    processTreeOwner = YokiFrameWorkbenchLauncher.CreateRuntimeBootstrapProcessTreeOwnerForTests(parent);
                    Assert.IsNotNull(processTreeOwner, "当前 Windows Editor 必须建立 Job Object owner。");

                    bool terminated = YokiFrameWorkbenchLauncher.TryTerminateProcessTreeForTests(parent, processTreeOwner);

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
            IDisposable processTreeOwner = null;
            using (Process parent = StartWindowsProcessTreeAfterSignal())
            {
                try
                {
                    string error;
                    processTreeOwner = YokiFrameWorkbenchLauncher.WindowsRuntimeBootstrapJob.TryCreateForTests(parent, out error);
                    Assert.IsNotNull(processTreeOwner, error);

                    parent.StandardInput.WriteLine("spawn");
                    parent.StandardInput.Flush();
                    child = Process.GetProcessById(int.Parse(ReadChildProcessId(parent)));

                    Assert.IsTrue(((YokiFrameWorkbenchLauncher.IRuntimeBootstrapProcessTreeOwner)processTreeOwner).TryTerminate());
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
        /// 验证后台 bootstrap 队列返回 Task。签名由 internal 委托在编译期绑定，拒绝 async void。
        /// </summary>
        [Test]
        public void QueueRuntimeBootstrapAndLaunch_后台队列_返回Task()
        {
            Func<YokiFrameWorkbenchLaunchPlan, Task> queue =
                YokiFrameWorkbenchLauncher.GetRuntimeBootstrapQueueForTests();

            Assert.AreEqual(typeof(Task), queue.Method.ReturnType);
        }

        /// <summary>
        /// 验证 Ctrl+E bootstrap 失败能识别 .NET SDK 与 Native AOT 工具链缺失，并提供对应菜单入口。
        /// </summary>
        [Test]
        public void RuntimeBootstrapFailureIdentifiesMissingBuildEnvironment()
        {
            string dotnetMessage = YokiFrameWorkbenchLauncher.CreateRuntimeBootstrapFailureMessageForTests(
                "NETSDK1045: The current .NET SDK does not support targeting .NET 10.0.");
            YokiFrameWorkbenchLauncher.RuntimeBootstrapEnvironmentMenuState dotnetMenu =
                YokiFrameWorkbenchLauncher.GetRuntimeBootstrapEnvironmentMenuStateForTests();
            string nativeAotMessage = YokiFrameWorkbenchLauncher.CreateRuntimeBootstrapFailureMessageForTests(
                "Platform linker not found. Ensure that Visual Studio 2022 is installed.");
            YokiFrameWorkbenchLauncher.RuntimeBootstrapEnvironmentMenuState nativeAotMenu =
                YokiFrameWorkbenchLauncher.GetRuntimeBootstrapEnvironmentMenuStateForTests();

            StringAssert.Contains("缺少 .NET 10 SDK", dotnetMessage);
            StringAssert.Contains("再次按 Ctrl+E", dotnetMessage);
            Assert.IsTrue(dotnetMenu.Enabled);
            Assert.AreEqual("https://dotnet.microsoft.com/download/dotnet/10.0", dotnetMenu.Url);
            StringAssert.Contains("缺少 Visual Studio 2022 C++ Build Tools", nativeAotMessage);
            Assert.IsTrue(nativeAotMenu.Enabled);
            Assert.AreEqual("https://visualstudio.microsoft.com/visual-cpp-build-tools/", nativeAotMenu.Url);
        }

        /// <summary>启动 PowerShell 父进程，并让它创建可由测试观测的长期 ping 子进程。</summary>
        /// <returns>标准输出首行会报告子进程 PID 的父进程。</returns>
        private static Process StartWindowsProcessTree()
        {
            return StartWindowsPowerShellProcess(BuildPingChildScript(false), false);
        }

        /// <summary>启动等待 stdin 信号的 PowerShell 父进程，确保它只在加入 Job 后创建长期子进程。</summary>
        /// <returns>收到任意输入行后创建 ping 子进程并报告 PID 的父进程。</returns>
        private static Process StartWindowsProcessTreeAfterSignal()
        {
            return StartWindowsPowerShellProcess(BuildPingChildScript(true), true);
        }

        /// <summary>
        /// 组装拉起长期 ping 子进程的脚本。子进程必须由 PowerShell 直接创建并保持句柄，
        /// 不能使用 Start-Process：后者走 UseShellExecute，会无视父进程的 CreateNoWindow 弹出控制台窗口。
        /// </summary>
        /// <param name="waitForSignal">为 true 时先阻塞读取一行 stdin，让调用方控制子进程的创建时机。</param>
        /// <returns>可交给 powershell.exe -Command 执行的脚本。</returns>
        private static string BuildPingChildScript(bool waitForSignal)
        {
            string signal = waitForSignal ? "$null = [Console]::In.ReadLine(); " : string.Empty;
            return signal
                + "$psi = New-Object System.Diagnostics.ProcessStartInfo; "
                + "$psi.FileName = Join-Path $env:SystemRoot 'System32\\PING.EXE'; "
                + "$psi.Arguments = '127.0.0.1 -t'; "
                + "$psi.UseShellExecute = $false; "
                + "$psi.CreateNoWindow = $true; "
                + "$psi.RedirectStandardOutput = $true; "
                + "$child = [System.Diagnostics.Process]::Start($psi); "
                + "[Console]::Out.WriteLine($child.Id); [Console]::Out.Flush(); $child.WaitForExit()";
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

    }
}
