using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using YokiFrame;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// RoslynKit 命令面与 capability.json 的一致性（§14 P0 / 风险 R5）。
/// </summary>
/// <remarks>
/// 两条守卫：① capability.json 声明的动作集合必须与 Provider 实际注册的命令面一致；
/// ② 清单里的 sourceHash 必须等于实现文件（LF 归一化后）的 SHA-256，避免"改了实现忘了改清单"。
/// </remarks>
public sealed class EngineCapabilityManifestTests
{
    [Fact]
    public void Automation_manifest_declares_both_engines_but_not_unimplemented_player_actions()
    {
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(FindPackageRoot(),
            "Core/Editor/CommandBridge/Capabilities/Roslyn/capability.json")));
        foreach (var command in manifest.RootElement.GetProperty("kit").GetProperty("commands").EnumerateArray())
        {
            string action = command.GetProperty("action").GetString()!;
            var engines = command.GetProperty("engineKinds").EnumerateArray().Select(value => value.GetString()).ToArray();
            if (action.StartsWith("script_", StringComparison.Ordinal)
                || (action.StartsWith("live_", StringComparison.Ordinal) && !action.StartsWith("live_export_", StringComparison.Ordinal)))
                Assert.Equal(new[] { "Unity", "Godot" }, engines);
            if (action == "play_control" || action == "asset_ops" || action.StartsWith("live_export_", StringComparison.Ordinal))
                Assert.Equal(new[] { "Unity" }, engines);
        }
    }

    [Fact]
    public void Capability_manifest_matches_registered_commands_and_source_hash()
    {
        string packageRoot = FindPackageRoot();
        string manifestPath = Path.Combine(
            packageRoot, "Core", "Editor", "CommandBridge", "Capabilities", "Roslyn", "capability.json");
        string sourcePath = Path.Combine(
            packageRoot, "Tools", "RoslynKit", "Editor", "RoslynKitProvider.cs");

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
        JsonElement kit = manifest.RootElement.GetProperty("kit");

        // ① 动作集合一致
        var declared = new SortedSet<string>(StringComparer.Ordinal);
        JsonElement commands = kit.GetProperty("commands");
        for (var index = 0; index < commands.GetArrayLength(); index++)
        {
            declared.Add(commands[index].GetProperty("action").GetString()!);
        }

        // 引擎适配器提供的动作（Unity: play_control / scene_query）在本测试程序集外，
        // 由适配器编译 + 真机 engine_capabilities 复核；这里显式声明，保证清单不会漏掉它们。
        var registered = new SortedSet<string>(StringComparer.Ordinal)
        {
            // 由引擎适配器提供的操作（Unity 侧）
            "play_control",
            "scene_query",
            "scene_mutate",
            "asset_ops",
            "live_export_status",
            "live_export_commit",
            "script_run",
            "script_status",
            "run_result",
            "run_lookup",
            "run_cancel",
            // eval 子系统由适配器接线后才注册（§11，可选能力）
            "eval",
            "eval_result",
            "eval_prune"
        };
        RoslynKitProvider provider = CreateProvider();
        foreach (YokiFrameCommandDescriptor descriptor in provider.Commands)
        {
            registered.Add(descriptor.Action);
        }
        registered.Add(new LiveCodeOperation(null!, "live_status").Descriptor.Action);
        registered.Add(new LiveCodeOperation(null!, "live_remove").Descriptor.Action);
        registered.Add(new LiveCodeOperation(null!, "live_snapshot").Descriptor.Action);
        registered.Add(new LiveCodeOperation(null!, "live_set_fields").Descriptor.Action);
        foreach (string action in new[] { "live_tuning_bind", "live_tuning_refresh", "live_tuning_status", "live_tuning_unbind" })
            registered.Add(new LiveTuningOperation(null!, action).Descriptor.Action);

        Assert.Equal(declared, registered);

        // ② sourceHash 与实现文件一致（LF 归一化，和 Workbench 的校验口径相同）
        string expected = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(
                File.ReadAllText(sourcePath).Replace("\r\n", "\n")))).ToLowerInvariant();
        string declaredHash = kit.GetProperty("sourceHash").GetString()!;
        Assert.Equal(expected, declaredHash);

        // ③ 每个动作都指向存在的校验配方
        var recipeIds = new HashSet<string>(StringComparer.Ordinal);
        JsonElement recipes = kit.GetProperty("verifyRecipes");
        for (var index = 0; index < recipes.GetArrayLength(); index++)
        {
            recipeIds.Add(recipes[index].GetProperty("id").GetString()!);
        }

        for (var index = 0; index < commands.GetArrayLength(); index++)
        {
            string recipe = commands[index].GetProperty("verifyRecipe").GetString()!;
            Assert.Contains(recipe, recipeIds);
        }
    }

    private static RoslynKitProvider CreateProvider()
    {
        var settingsSource = new StubEngineSettingsSource(RoslynSettingsSnapshot.Enabled());
        var engineProvider = new StubEngineOperationProvider(
            "Unity",
            RoslynExecutionTarget.Editor | RoslynExecutionTarget.Play);
        engineProvider.Runs = new RoslynRunScheduler(
            new RoslynRunStore(Path.Combine(Path.GetTempPath(), "yokiframe-manifest-tests", Guid.NewGuid().ToString("N"))),
            new TestRunHost());
        var gate = RoslynGate.CreateDefault(settingsSource);
        return new RoslynKitProvider(gate, engineProvider, settingsSource);
    }

    private static string FindPackageRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Core", "Editor", "Skills")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("YokiFrame package root was not found above " + AppContext.BaseDirectory);
    }
}
