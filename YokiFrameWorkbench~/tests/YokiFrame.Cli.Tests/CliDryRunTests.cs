using System.Text.Json.Nodes;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 覆盖写入命令的 --dry-run 契约：必须完成与真实执行一致的校验、报告计划写入的文件，并且不落地任何内容。
/// </summary>
public sealed class CliDryRunTests
{
    /// <summary>
    /// 验证 project refresh --dry-run 列出五个模型文件且不创建它们。
    /// </summary>
    [Fact]
    public async Task ProjectRefreshDryRunPlansBundleWithoutWriting()
    {
        using var project = CliTestHelpers.CreateProjectRoot("dry-run-project");

        var result = await CliTestHelpers.RunCliAsync("project", "refresh", "--dry-run", "--project", project.Path);

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(result.StandardOutput)!;
        Assert.True(json["dryRun"]!.GetValue<bool>());
        Assert.Equal("Ready", json["state"]!.GetValue<string>());
        Assert.Equal(5, json["writes"]!.AsArray().Count);
        Assert.All(json["writes"]!.AsArray(), static node =>
            Assert.Equal("create", node!["action"]!.GetValue<string>()));
        Assert.False(Directory.Exists(Path.Combine(project.Path, ".yokiframe", "project")));
        // 计划中的五个模型文件必须完全不存在；Inspector 只允许创建协议目录本身和读锁。
        Assert.All(json["writes"]!.AsArray(), node =>
        {
            var relative = node!["path"]!.GetValue<string>().Replace('/', Path.DirectorySeparatorChar);
            Assert.False(File.Exists(Path.Combine(project.Path, relative)), "dry-run 不得写入 " + relative);
        });
    }

    /// <summary>
    /// 验证 dry-run 目标使用项目相对路径，AI 不需要读取绝对路径即可定位文件。
    /// </summary>
    [Fact]
    public async Task DryRunPathsAreProjectRelative()
    {
        using var project = CliTestHelpers.CreateProjectRoot("dry-run-relative");

        var result = await CliTestHelpers.RunCliAsync("project", "refresh", "--dry-run", "--project", project.Path);

        var json = JsonNode.Parse(result.StandardOutput)!;
        var first = json["writes"]![0]!["path"]!.GetValue<string>();
        Assert.Equal(".yokiframe/project/project-model.json", first);
    }

    /// <summary>
    /// 验证 localization add --dry-run 在已有文本且未 force 时给出与真实执行相同的拒绝，并且不修改源文件。
    /// </summary>
    [Fact]
    public async Task LocalizationAddDryRunRejectsOverwriteAndLeavesFileUntouched()
    {
        using var project = CliTestHelpers.CreateProjectRoot("dry-run-localization");
        var sourcePath = WriteLocalizationSource(project.Path);
        var original = File.ReadAllText(sourcePath);

        var rejected = await CliTestHelpers.RunCliAsync(
            "localization", "add", "--project", project.Path,
            "--text-id", "1", "--language", "ChineseSimplified", "--value", "覆盖", "--dry-run");

        Assert.Equal(1, rejected.ExitCode);
        var rejectedJson = JsonNode.Parse(rejected.StandardError)!;
        Assert.Equal("LocalizationAddRejected", rejectedJson["error"]!["code"]!.GetValue<string>());
        Assert.True(rejectedJson["dryRun"]!.GetValue<bool>());
        Assert.Empty(rejectedJson["writes"]!.AsArray());
        Assert.Equal(original, File.ReadAllText(sourcePath));

        var forced = await CliTestHelpers.RunCliAsync(
            "localization", "add", "--project", project.Path,
            "--text-id", "1", "--language", "ChineseSimplified", "--value", "覆盖", "--force", "--dry-run");

        Assert.Equal(0, forced.ExitCode);
        var forcedJson = JsonNode.Parse(forced.StandardOutput)!;
        Assert.True(forcedJson["requiresForce"]!.GetValue<bool>());
        Assert.Equal("overwrite", forcedJson["writes"]![0]!["action"]!.GetValue<string>());
        Assert.Equal("Assets/Settings/YokiFrame/localization.json", forcedJson["writes"]![0]!["path"]!.GetValue<string>());
        Assert.Equal(original, File.ReadAllText(sourcePath));
    }

    /// <summary>
    /// 验证 localization add --dry-run 对新文本报告 create 且不需要 force。
    /// </summary>
    [Fact]
    public async Task LocalizationAddDryRunPlansNewValueWithoutForce()
    {
        using var project = CliTestHelpers.CreateProjectRoot("dry-run-localization-new");
        var sourcePath = WriteLocalizationSource(project.Path);
        var original = File.ReadAllText(sourcePath);

        var result = await CliTestHelpers.RunCliAsync(
            "localization", "add", "--project", project.Path,
            "--text-id", "100", "--language", "English", "--value", "Start", "--dry-run");

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(result.StandardOutput)!;
        Assert.False(json["requiresForce"]!.GetValue<bool>());
        Assert.Equal("create", json["writes"]![0]!["action"]!.GetValue<string>());
        Assert.Equal(original, File.ReadAllText(sourcePath));
    }

    /// <summary>
    /// 验证 player build --dry-run 在规划前完成项目校验，失败信息与真实执行一致。
    /// </summary>
    [Fact]
    public async Task PlayerBuildDryRunStillValidatesProjectFiles()
    {
        using var project = CliTestHelpers.CreateProjectRoot("dry-run-player");

        var result = await CliTestHelpers.RunCliAsync(
            "player", "build", "--project", project.Path,
            "--engine", "godot", "--godot", "godot", "--preset", "Windows Desktop",
            "--output", "Builds/Game.exe", "--dry-run");

        Assert.Equal(1, result.ExitCode);
        var json = JsonNode.Parse(result.StandardError)!;
        Assert.Equal("GodotProjectMissing", json["error"]!["code"]!.GetValue<string>());
    }

    /// <summary>
    /// 验证 player build --dry-run 在项目文件齐全时列出产物和日志目标，且不启动外部进程。
    /// </summary>
    [Fact]
    public async Task PlayerBuildDryRunPlansOutputAndLog()
    {
        using var project = CliTestHelpers.CreateProjectRoot("dry-run-player-ok");
        File.WriteAllText(Path.Combine(project.Path, "project.godot"), "config_version=5");
        File.WriteAllText(Path.Combine(project.Path, "export_presets.cfg"), string.Empty);

        var result = await CliTestHelpers.RunCliAsync(
            "player", "build", "--project", project.Path,
            "--engine", "godot", "--godot", "godot", "--preset", "Windows Desktop",
            "--output", "Builds/Game.exe", "--dry-run");

        Assert.Equal(0, result.ExitCode);
        var json = JsonNode.Parse(result.StandardOutput)!;
        Assert.True(json["dryRun"]!.GetValue<bool>());
        Assert.Equal("Builds/Game.exe", json["writes"]![0]!["path"]!.GetValue<string>());
        Assert.Equal("create", json["writes"]![0]!["action"]!.GetValue<string>());
        Assert.False(Directory.Exists(Path.Combine(project.Path, "Builds")), "dry-run 不得创建产物目录");
        Assert.False(Directory.Exists(Path.Combine(project.Path, ".yokiframe")), "dry-run 不得创建日志目录");
    }

    /// <summary>
    /// 验证只读命令不接受 --dry-run，避免 AI 误以为只读查询存在计划阶段。
    /// </summary>
    [Theory]
    [InlineData("engine")]
    [InlineData("telemetry")]
    [InlineData("snapshot")]
    public async Task ReadOnlyCommandsRejectDryRun(string verb)
    {
        using var project = CliTestHelpers.CreateProjectRoot("dry-run-readonly");
        var action = verb == "engine" ? "list" : "read";

        var result = await CliTestHelpers.RunCliAsync(verb, action, "--dry-run", "--project", project.Path);

        Assert.Equal(1, result.ExitCode);
        var json = JsonNode.Parse(result.StandardError)!;
        Assert.Equal("UnknownOption", json["error"]!["code"]!.GetValue<string>());
        Assert.Contains("--dry-run", json["error"]!["message"]!.GetValue<string>(), StringComparison.Ordinal);
    }

    /// <summary>
    /// 写入项目内的最小 LocalizationKit JSON 源文件，并返回其绝对路径。
    /// </summary>
    /// <param name="projectRoot">临时项目根。</param>
    /// <returns>源文件绝对路径。</returns>
    private static string WriteLocalizationSource(string projectRoot)
    {
        var relativePath = Path.Combine("Assets", "Settings", "YokiFrame", "localization.json");
        var sourcePath = Path.Combine(projectRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(sourcePath)!);
        File.WriteAllText(sourcePath, """
            {"formatVersion":1,"languages":[{"id":"ChineseSimplified"},{"id":"English"}],"texts":[{"id":1,"key":"start","values":{"ChineseSimplified":"开始"}}]}
            """);
        return sourcePath;
    }
}