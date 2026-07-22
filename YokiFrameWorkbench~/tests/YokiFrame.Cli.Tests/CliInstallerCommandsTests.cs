using System.Text.Json.Nodes;

namespace YokiFrame.Cli.Tests;

/// <summary>
/// 通过真实 CLI 进程验证 Installer plan/apply 与 Avalonia 共用 Application 会话和 gateway。
/// </summary>
public sealed class CliInstallerCommandsTests
{
    /// <summary>
    /// 验证 Unity local、Unity Git 与 Godot local 三种模式均输出统一 plan actions、warnings 和会话日志。
    /// </summary>
    /// <param name="mode">CLI 安装模式。</param>
    [Theory]
    [InlineData("unity-local")]
    [InlineData("unity-git")]
    [InlineData("godot-local")]
    public async Task InstallerPlanOutputsUnifiedSessionForAllModes(string mode)
    {
        using CliInstallerFixture fixture = CliInstallerFixture.Create();
        var arguments = CreatePlanArguments(fixture, mode);

        var result = await CliInstallerFixture.RunCliAsync(arguments);

        var json = AssertSuccess(result, "installer plan");
        var session = json["session"]!;
        Assert.Equal("PlanReady", session["status"]!.GetValue<string>());
        Assert.NotEmpty(session["logs"]!.AsArray());
        Assert.NotEmpty(session["plan"]!["actions"]!.AsArray());
        Assert.NotNull(session["plan"]!["warnings"]!.AsArray());
        Assert.Empty(session["conflicts"]!.AsArray());
        Assert.Null(session["result"]);
        if (mode == "godot-local")
        {
            var actionKinds = session["plan"]!["actions"]!.AsArray()
                .Select(static action => action!["kind"]!.GetValue<string>())
                .ToArray();
            Assert.DoesNotContain("PatchProjectSettings", actionKinds);
            Assert.Equal(new[] { "InstallPackage", "PatchProjectFile" }, actionKinds);
        }

        Assert.False(Directory.Exists(Path.Combine(fixture.UnityProjectRoot, "Packages", "com.hinatayoki.yokiframe")));
        Assert.False(Directory.Exists(Path.Combine(fixture.GodotProjectRoot, "addons", "yokiframe")));
    }

    /// <summary>
    /// 验证 Godot apply 在临时项目真实提交包、plugin 入口、UID 与成功 evidence。
    /// </summary>
    [Fact]
    public async Task InstallerApplyCommitsGodotFixtureAndReturnsResultEvidence()
    {
        using CliInstallerFixture fixture = CliInstallerFixture.Create();

        var result = await CliInstallerFixture.RunCliAsync(
            "installer", "apply",
            "--mode", "godot-local",
            "--target", fixture.GodotProjectRoot,
            "--source", fixture.SourcePackageRoot,
            "--repair-godot", "true",
            "--enable-godot", "true");

        var json = AssertSuccess(result, "installer apply");
        var session = json["session"]!;
        Assert.Equal("Succeeded", session["status"]!.GetValue<string>());
        Assert.NotNull(session["result"]);
        Assert.NotEmpty(session["evidence"]!.AsArray());
        Assert.True(File.Exists(fixture.GetGodotPackagePath("Core/Runtime/Alpha.cs")));
        Assert.True(File.Exists(fixture.GetGodotPackagePath("Core/Runtime/Alpha.cs.uid")));
        Assert.True(File.Exists(Path.Combine(
            fixture.GodotProjectRoot,
            "addons",
            "yokiframe",
            "YokiFrameGodotEditorPlugin.cs.uid")));
    }

    /// <summary>
    /// 验证无 owner 的旧 Godot 包按当前 Installer 契约直接完整替换，不保留文件级 legacy 内容。
    /// </summary>
    [Fact]
    public async Task InstallerApplyReplacesUnmanagedGodotLegacyPackage()
    {
        using CliInstallerFixture fixture = CliInstallerFixture.Create();
        fixture.WriteGodotLegacyPackage();
        var result = await CliInstallerFixture.RunCliAsync(
            "installer", "apply",
            "--mode", "godot-local",
            "--target", fixture.GodotProjectRoot,
            "--source", fixture.SourcePackageRoot);

        var json = AssertSuccess(result, "installer apply");
        Assert.Equal("Succeeded", json["session"]!["status"]!.GetValue<string>());
        Assert.False(File.Exists(Path.Combine(fixture.GodotProjectRoot, "addons", "yokiframe", "legacy.marker")));
        Assert.True(File.Exists(fixture.GetGodotPackagePath("Core/Runtime/Alpha.cs")));
    }

    /// <summary>
    /// 验证无效安装模式使用标准 error JSON 和非零退出码。
    /// </summary>
    [Fact]
    public async Task InstallerRejectsInvalidModeWithStandardErrorJson()
    {
        using CliInstallerFixture fixture = CliInstallerFixture.Create();

        var result = await CliInstallerFixture.RunCliAsync(
            "installer", "plan",
            "--mode", "unsupported",
            "--target", fixture.GodotProjectRoot);

        _ = AssertError(result, "InvalidInstallerMode");
    }

    /// <summary>
    /// 为指定模式创建完整 plan 参数，并在 Godot 关闭 repair/enable 验证可选动作不会被误加。
    /// </summary>
    /// <param name="fixture">Installer CLI fixture。</param>
    /// <param name="mode">安装模式。</param>
    /// <returns>真实 CLI 参数数组。</returns>
    private static string[] CreatePlanArguments(CliInstallerFixture fixture, string mode)
    {
        List<string> arguments = new() { "installer", "plan", "--mode", mode };
        switch (mode)
        {
            case "unity-local":
                arguments.AddRange(new[] { "--target", fixture.UnityProjectRoot, "--source", fixture.SourcePackageRoot });
                break;
            case "unity-git":
                arguments.AddRange(new[]
                {
                    "--target", fixture.UnityProjectRoot,
                    "--git-url", "https://github.com/HinataYoki/YokiFrame.git#main"
                });
                break;
            case "godot-local":
                arguments.AddRange(new[]
                {
                    "--target", fixture.GodotProjectRoot,
                    "--source", fixture.SourcePackageRoot,
                    "--repair-godot", "false",
                    "--enable-godot", "false"
                });
                break;
        }

        return arguments.ToArray();
    }

    /// <summary>
    /// 断言成功进程只写 stdout compact JSON。
    /// </summary>
    /// <param name="result">CLI 子进程结果。</param>
    /// <param name="command">预期命令名称。</param>
    /// <returns>解析后的成功 JSON。</returns>
    private static JsonNode AssertSuccess(CliInstallerProcessResult result, string command)
    {
        Assert.True(result.ExitCode == 0, result.StandardError);
        Assert.Equal(string.Empty, result.StandardError.Trim());
        var json = JsonNode.Parse(result.StandardOutput)
            ?? throw new InvalidOperationException("CLI stdout is not JSON.");
        Assert.True(json["ok"]!.GetValue<bool>());
        Assert.Equal(command, json["command"]!.GetValue<string>());
        return json;
    }

    /// <summary>
    /// 断言失败进程使用标准 error JSON，并同时携带 Installer 会话快照。
    /// </summary>
    /// <param name="result">CLI 子进程结果。</param>
    /// <param name="expectedCode">预期标准错误码。</param>
    /// <returns>解析后的失败 JSON。</returns>
    private static JsonNode AssertError(CliInstallerProcessResult result, string expectedCode)
    {
        Assert.NotEqual(0, result.ExitCode);
        Assert.Equal(string.Empty, result.StandardOutput.Trim());
        var json = JsonNode.Parse(result.StandardError)
            ?? throw new InvalidOperationException("CLI stderr is not JSON.");
        Assert.False(json["ok"]!.GetValue<bool>());
        Assert.Equal(expectedCode, json["error"]!["code"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(json["error"]!["message"]!.GetValue<string>()));
        Assert.NotNull(json["error"]!["evidencePaths"]!.AsArray());
        return json;
    }
}
