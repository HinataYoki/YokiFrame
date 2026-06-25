#if !GODOT
using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Tauri 启动器的外部进程管理逻辑。
    /// </summary>
    public static partial class TauriLauncher
    {
        internal static async Task<(bool success, string output)> RunCargoAsync(string args)
        {
            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cargo",
                    Arguments = args,
                    WorkingDirectory = SrcTauriPath,
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
        /// 启动 Tauri 二进制，并注入文件桥、前端资源和 Unity owner 窗口环境变量。
        /// </summary>
        private static bool StartBinaryProcess(string binaryPath)
        {
            if (!File.Exists(binaryPath))
            {
                LogKit.Error($"[TauriLauncher] 二进制不存在: {binaryPath}");
                return false;
            }

            try
            {
                var workingDir = Path.GetDirectoryName(binaryPath);
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = binaryPath,
                    WorkingDirectory = workingDir,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true,
                };

                AddTauriEnvironment(startInfo);

                sTauriProcess = new System.Diagnostics.Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true,
                };

                sTauriProcess.OutputDataReceived += (_, _) => { };
                sTauriProcess.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        LogKit.Warning($"[Tauri:err] {e.Data}");
                };

                sTauriProcess.Exited += OnTauriProcessExited;
                sTauriProcess.Start();
                sTauriProcess.BeginOutputReadLine();
                sTauriProcess.BeginErrorReadLine();

                return true;
            }
            catch (Exception e)
            {
                LogKit.Error($"[TauriLauncher] 启动二进制失败: {e.Message}");
                return false;
            }
        }

        private static bool StartSourceProcess()
        {
            if (!Directory.Exists(SrcTauriPath))
            {
                LogKit.Error($"[TauriLauncher] Tauri 源码目录不存在: {SrcTauriPath}");
                return false;
            }

            try
            {
                var startInfo = new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "cargo",
                    Arguments = ResolveSourceRunArguments(),
                    WorkingDirectory = SrcTauriPath,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8,
                    StandardErrorEncoding = Encoding.UTF8,
                    CreateNoWindow = true,
                };

                AddTauriEnvironment(startInfo);

                sTauriProcess = new System.Diagnostics.Process
                {
                    StartInfo = startInfo,
                    EnableRaisingEvents = true,
                };

                sTauriProcess.OutputDataReceived += (_, _) => { };
                sTauriProcess.ErrorDataReceived += (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        LogKit.Warning($"[Tauri:dev] {e.Data}");
                };

                sTauriProcess.Exited += OnTauriProcessExited;
                sTauriProcess.Start();
                sTauriProcess.BeginOutputReadLine();
                sTauriProcess.BeginErrorReadLine();
                return true;
            }
            catch (Exception e)
            {
                LogKit.Error($"[TauriLauncher] 启动源码窗口失败: {e.Message}");
                return false;
            }
        }

        internal static string ResolveSourceRunArguments()
        {
            return "run --target-dir " + CARGO_TARGET_DIR_NAME;
        }

        private static void AddTauriEnvironment(System.Diagnostics.ProcessStartInfo startInfo)
        {
            var yokiframeDir = ResolveYokiframeDir();
            startInfo.EnvironmentVariables["YOKI_YOKIFRAME_DIR"] = yokiframeDir;
            startInfo.EnvironmentVariables["YOKI_DIST_PATH"] = DistPath;

            var ownerHwnd = TauriInstanceBridge.GetUnityMainWindowHwnd();
            startInfo.EnvironmentVariables["YOKI_OWNER_HWND"] = ownerHwnd.ToString();
        }

        private static System.Diagnostics.Process GetRunningTauriProcess()
        {
            if (sTauriProcess is { HasExited: false })
                return sTauriProcess;

            DisposeProcessReference();
            sTauriProcess = FindExistingTauriProcess();
            return sTauriProcess;
        }

        private static System.Diagnostics.Process FindExistingTauriProcess()
        {
            var processName = Path.GetFileNameWithoutExtension(BinaryPath);
            var expectedPath = Path.GetFullPath(BinaryPath);
            var processes = System.Diagnostics.Process.GetProcessesByName(processName);
            foreach (var process in processes)
            {
                try
                {
                    if (!process.HasExited &&
                        string.Equals(Path.GetFullPath(process.MainModule.FileName), expectedPath, StringComparison.OrdinalIgnoreCase))
                    {
                        process.EnableRaisingEvents = true;
                        process.Exited -= OnTauriProcessExited;
                        process.Exited += OnTauriProcessExited;
                        return process;
                    }
                }
                catch
                {
                    // 进程可能刚退出或 MainModule 不可读，忽略后继续扫描。
                }

                process.Dispose();
            }

            return null;
        }

        private static void OnTauriProcessExited(object sender, EventArgs e)
        {
        }

        private static void DisposeProcessReference()
        {
            if (sTauriProcess == null)
                return;

            try { sTauriProcess.Dispose(); }
            catch { }
            sTauriProcess = null;
        }
    }
}
#endif
