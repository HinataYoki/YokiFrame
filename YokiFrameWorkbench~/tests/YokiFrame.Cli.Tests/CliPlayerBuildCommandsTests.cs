using System.Diagnostics;
using System.Text.Json.Nodes;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 覆盖 Player build CLI 的输入与进程启动边界；真实 Godot 导出由宿主集成验证承担。
/// </summary>
public sealed class CliPlayerBuildCommandsTests
{
    /// <summary>验证缺失 project.godot 时返回稳定项目错误。</summary>
    [Fact]
    public async Task PlayerBuildRejectsMissingGodotProject()
    {
        using TestProjectRoot project = new(writeProjectFiles: false);
        var result = await RunCliAsync(CreateArguments(project, project.GetOutputPath(), "missing-godot.exe"));

        AssertError(result, "GodotProjectMissing");
    }

    /// <summary>验证导出目标越过项目根时在启动 Godot 前被拒绝。</summary>
    [Fact]
    public async Task PlayerBuildRejectsOutputOutsideProject()
    {
        using TestProjectRoot project = new(writeProjectFiles: true);
        var outsidePath = Path.Combine(Path.GetDirectoryName(project.Path)!, "outside.exe");
        var result = await RunCliAsync(CreateArguments(project, outsidePath, "missing-godot.exe"));

        AssertError(result, "PlayerOutputOutsideProject");
    }

    /// <summary>验证不可启动的 Godot 路径返回标准错误并保留日志证据。</summary>
    [Fact]
    public async Task PlayerBuildReportsMissingGodotExecutable()
    {
        using TestProjectRoot project = new(writeProjectFiles: true);
        var result = await RunCliAsync(CreateArguments(project, project.GetOutputPath(), "missing-godot.exe"));
        JsonNode json = AssertError(result, "GodotExecutableNotFound");

        var evidencePaths = json["error"]!["evidencePaths"]!.AsArray()
            .Select(static node => node!.GetValue<string>())
            .ToArray();
        Assert.Contains("missing-godot.exe", evidencePaths);
        Assert.Contains(evidencePaths, static path => path.EndsWith(".log", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>验证 Unity Player 构建返回 YokiFrame 自有的中性能力边界，不耦合具体外部插件。</summary>
    [Fact]
    public async Task PlayerBuildRejectsUnityWithNeutralGuidance()
    {
        using TestProjectRoot project = new(writeProjectFiles: true);
        var arguments = CreateArguments(project, project.GetOutputPath(), "unused-godot.exe");
        arguments[3] = "unity";

        var result = await RunCliAsync(arguments);
        JsonNode json = AssertError(result, "UnsupportedPlayerEngine");
        var suggestion = json["error"]!["suggestion"]!.GetValue<string>();

        Assert.Equal(
            "Use --engine godot; the YokiFrame CLI does not currently build Unity Players. "
            + "Use the Unity Editor or an external automation tool.",
            suggestion);
    }

    /// <summary>创建一次完整 player build 参数列表。</summary>
    /// <param name="project">测试项目。</param>
    /// <param name="outputPath">目标产物。</param>
    /// <param name="godotExecutable">Godot 可执行文件。</param>
    /// <returns>CLI 参数。</returns>
    private static string[] CreateArguments(
        TestProjectRoot project,
        string outputPath,
        string godotExecutable)
    {
        return new[]
        {
            "player", "build",
            "--engine", "godot",
            "--project", project.Path,
            "--godot", godotExecutable,
            "--preset", "Windows Desktop",
            "--output", outputPath,
            "--configuration", "debug"
        };
    }

    /// <summary>启动真实 CLI 程序并捕获 JSON 输出。</summary>
    /// <param name="arguments">CLI 参数。</param>
    /// <returns>进程结果。</returns>
    private static async Task<CliProcessResult> RunCliAsync(params string[] arguments)
    {
        ProcessStartInfo startInfo = new("dotnet")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        startInfo.ArgumentList.Add(GetCliAssemblyPath());
        foreach (var argument in arguments) startInfo.ArgumentList.Add(argument);

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start YokiFrame.Cli process.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();
        return new CliProcessResult(process.ExitCode, await standardOutputTask, await standardErrorTask);
    }

    /// <summary>断言失败输出符合统一 compact JSON 契约。</summary>
    /// <param name="result">CLI 进程结果。</param>
    /// <param name="expectedCode">预期错误码。</param>
    /// <returns>解析后的错误 JSON。</returns>
    private static JsonNode AssertError(CliProcessResult result, string expectedCode)
    {
        Assert.Equal(1, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput.Trim());
        var json = JsonNode.Parse(result.StandardError)
            ?? throw new InvalidOperationException("CLI stderr is not JSON.");
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal(expectedCode, json["error"]!["code"]!.GetValue<string>());
        return json;
    }

    /// <summary>定位同一构建配置生成的 CLI 程序集。</summary>
    /// <returns>CLI DLL 路径。</returns>
    private static string GetCliAssemblyPath()
    {
        var outputDirectory = new DirectoryInfo(AppContext.BaseDirectory);
        var configurationDirectory = outputDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test configuration directory is missing.");
        var projectDirectory = configurationDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test project directory is missing.");
        var binDirectory = projectDirectory.Parent
            ?? throw new DirectoryNotFoundException("CLI test bin directory is missing.");
        var cliAssemblyPath = Path.Combine(
            binDirectory.FullName,
            "YokiFrame.Cli",
            configurationDirectory.Name,
            outputDirectory.Name,
            "YokiFrame.Cli.dll");
        Assert.True(File.Exists(cliAssemblyPath), "CLI assembly was not built: " + cliAssemblyPath);
        return cliAssemblyPath;
    }

    /// <summary>表示 CLI 子进程终态。</summary>
    /// <param name="ExitCode">退出码。</param>
    /// <param name="StandardOutput">标准输出。</param>
    /// <param name="StandardError">标准错误。</param>
    private sealed record CliProcessResult(int ExitCode, string StandardOutput, string StandardError);

    /// <summary>创建并清理最小 Godot Player build 测试工程。</summary>
    private sealed class TestProjectRoot : IDisposable
    {
        /// <summary>创建临时工程，并按需写入 Godot 项目与导出 preset 文件。</summary>
        /// <param name="writeProjectFiles">是否写入必需项目文件。</param>
        public TestProjectRoot(bool writeProjectFiles)
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "yokiframe-cli-player-build-tests",
                Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
            if (!writeProjectFiles) return;
            File.WriteAllText(System.IO.Path.Combine(Path, "project.godot"), "config_version=5\n");
            File.WriteAllText(System.IO.Path.Combine(Path, "export_presets.cfg"), "[preset.0]\nname=\"Windows Desktop\"\n");
        }

        /// <summary>获取临时项目根。</summary>
        public string Path { get; }

        /// <summary>获取项目内测试导出路径。</summary>
        /// <returns>完整 exe 路径。</returns>
        public string GetOutputPath()
        {
            return System.IO.Path.Combine(Path, "Builds", "Validation.exe");
        }

        /// <summary>递归删除当前测试创建的临时工程。</summary>
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
