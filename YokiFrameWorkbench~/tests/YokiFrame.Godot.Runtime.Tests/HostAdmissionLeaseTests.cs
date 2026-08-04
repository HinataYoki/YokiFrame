using YokiFrame;
using System.Diagnostics;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// 验证同一 projectRoot 与 engineId 的 Host admission lease 具备排他性和可恢复性。
/// </summary>
public sealed class HostAdmissionLeaseTests
{
    /// <summary>
    /// 验证第二个进程尝试打开同一锁文件时得到 AlreadyOwned，释放后可以重新取得。
    /// </summary>
    [Fact]
    public void SecondLeaseIsAlreadyOwnedAndReleaseAllowsReacquire()
    {
        var root = CreateTemporaryRoot();
        var lockPath = Path.Combine(root, "engines", "unity-editor", "host.lock");
        try
        {
            var firstResult = YokiFrameHostAdmissionLease.TryAcquire(lockPath, out var firstLease, out var firstError);
            Assert.Equal(YokiFrameHostAdmissionResult.Acquired, firstResult);
            Assert.Null(firstError);
            Assert.NotNull(firstLease);

            var secondResult = YokiFrameHostAdmissionLease.TryAcquire(lockPath, out var secondLease, out var secondError);
            Assert.Equal(YokiFrameHostAdmissionResult.AlreadyOwned, secondResult);
            Assert.Null(secondLease);
            Assert.Null(secondError);

            firstLease!.Dispose();
            var reacquireResult = YokiFrameHostAdmissionLease.TryAcquire(lockPath, out var reacquiredLease, out var reacquireError);
            Assert.Equal(YokiFrameHostAdmissionResult.Acquired, reacquireResult);
            Assert.Null(reacquireError);
            reacquiredLease!.Dispose();
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    /// <summary>
    /// 验证锁路径是目录时返回 StorageError，而不是误报 AlreadyOwned。
    /// </summary>
    [Fact]
    public void DirectoryLockPathIsStorageError()
    {
        var root = CreateTemporaryRoot();
        var lockPath = Path.Combine(root, "engines", "godot-runtime", "host.lock");
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(lockPath)!);
            Directory.CreateDirectory(lockPath);

            var result = YokiFrameHostAdmissionLease.TryAcquire(lockPath, out var lease, out var error);

            Assert.Equal(YokiFrameHostAdmissionResult.StorageError, result);
            Assert.Null(lease);
            Assert.NotNull(error);
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    /// <summary>
    /// 验证不同 engineId 的锁互不阻塞，支持 godot-editor 与 godot-runtime 并存。
    /// </summary>
    [Fact]
    public void DifferentLockPathsCanBeHeldTogether()
    {
        var root = CreateTemporaryRoot();
        try
        {
            var editorPath = Path.Combine(root, "engines", "godot-editor", "host.lock");
            var runtimePath = Path.Combine(root, "engines", "godot-runtime", "host.lock");
            var editorResult = YokiFrameHostAdmissionLease.TryAcquire(editorPath, out var editorLease, out var editorError);
            var runtimeResult = YokiFrameHostAdmissionLease.TryAcquire(runtimePath, out var runtimeLease, out var runtimeError);

            Assert.Equal(YokiFrameHostAdmissionResult.Acquired, editorResult);
            Assert.Equal(YokiFrameHostAdmissionResult.Acquired, runtimeResult);
            Assert.Null(editorError);
            Assert.Null(runtimeError);
            editorLease!.Dispose();
            runtimeLease!.Dispose();
        }
        finally
        {
            DeleteTemporaryRoot(root);
        }
    }

    /// <summary>
    /// 验证 admission lock 的空路径输入被识别为存储配置错误。
    /// </summary>
    [Fact]
    public void EmptyLockPathIsStorageError()
    {
        var result = YokiFrameHostAdmissionLease.TryAcquire("", out var lease, out var error);

        Assert.Equal(YokiFrameHostAdmissionResult.StorageError, result);
        Assert.Null(lease);
        Assert.IsType<ArgumentException>(error);
    }

    /// <summary>
    /// 通过独立 dotnet 进程验证文件句柄锁是真正的跨进程排他，而非测试进程内状态。
    /// </summary>
    [Fact]
    public void SeparateDotnetProcessCannotAcquireHeldLease()
    {
        var root = CreateTemporaryRoot();
        var lockPath = Path.Combine(root, "engines", "unity-editor", "host.lock");
        YokiFrameHostAdmissionLease? lease = null;
        try
        {
            var result = YokiFrameHostAdmissionLease.TryAcquire(lockPath, out lease, out var error);
            Assert.Equal(YokiFrameHostAdmissionResult.Acquired, result);
            Assert.Null(error);

            var scriptPath = Path.Combine(root, "lock-probe.fsx");
            File.WriteAllText(scriptPath, BuildLockProbeScript(lockPath));
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            startInfo.ArgumentList.Add("fsi");
            startInfo.ArgumentList.Add("--exec");
            startInfo.ArgumentList.Add(scriptPath);
            using var process = Process.Start(startInfo);
            Assert.NotNull(process);
            Assert.True(process.WaitForExit(10000), "The cross-process lock probe did not exit in time.");
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();

            Assert.True(process.ExitCode == 0, output);
            Assert.Contains("AlreadyOwned", output, StringComparison.Ordinal);
        }
        finally
        {
            lease?.Dispose();
            DeleteTemporaryRoot(root);
        }
    }

    /// <summary>
    /// 生成只依赖 BCL 的 F# interactive 锁探针，避免把测试辅助项目引入生产 solution。
    /// </summary>
    private static string BuildLockProbeScript(string lockPath)
    {
        var escapedPath = lockPath.Replace("\\", "\\\\").Replace("\"", "\\\"");
        return string.Join("\n", new[]
        {
            "open System",
            "open System.IO",
            $"let path = \"{escapedPath}\"",
            "try",
            "    use _stream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)",
            "    printfn \"Acquired\"",
            "with",
            "| :? IOException -> printfn \"AlreadyOwned\"",
            "| ex -> printfn \"StorageError:%s\" ex.Message"
        }) + "\n";
    }

    /// <summary>
    /// 创建本测试独占的临时目录。
    /// </summary>
    private static string CreateTemporaryRoot()
    {
        var path = Path.Combine(Path.GetTempPath(), "YokiFrame-HostAdmission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// 清理本测试创建的临时目录；清理失败不影响测试主体结果。
    /// </summary>
    private static void DeleteTemporaryRoot(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

}
