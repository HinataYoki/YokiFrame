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
    /// <summary>自动化清单让脚本和热更动作覆盖 Unity 与 Godot，播放、资源和导出动作只覆盖 Unity。</summary>
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

    /// <summary>核对 capability.json 的动作集合、sourceHash 和校验配方。不改清单或实现文件。</summary>
    [Fact]
    public void Capability_manifest_matches_registered_commands_and_source_hash()
    {
        string packageRoot = FindPackageRoot();
        using JsonDocument manifest = ParseCapabilityManifest(packageRoot);
        JsonElement kit = manifest.RootElement.GetProperty("kit");
        JsonElement commands = kit.GetProperty("commands");
        AssertDeclaredActionsMatchRegistration(commands);
        AssertSourceHashMatchesImplementation(packageRoot, kit);
        AssertCommandsReferenceExistingRecipes(kit, commands);
    }

    /// <summary>读取包内 Roslyn capability.json。不注册命令，不改文件。</summary>
    /// <param name="packageRoot">YokiFrame 包根。</param>
    /// <returns>调用方负责释放的清单文档。</returns>
    private static JsonDocument ParseCapabilityManifest(string packageRoot)
    {
        string manifestPath = Path.Combine(
            packageRoot, "Core", "Editor", "CommandBridge", "Capabilities", "Roslyn", "capability.json");
        return JsonDocument.Parse(File.ReadAllText(manifestPath));
    }

    /// <summary>断言清单动作与注册命令面一致。会创建临时运行存储，不写清单。</summary>
    /// <param name="commands">清单中的 commands 数组。</param>
    private static void AssertDeclaredActionsMatchRegistration(JsonElement commands)
    {
        // ① 动作集合一致
        var declared = new SortedSet<string>(StringComparer.Ordinal);
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
    }

    /// <summary>断言 sourceHash 等于实现文件 LF 归一化后的 SHA-256。只读文件。</summary>
    /// <param name="packageRoot">YokiFrame 包根。</param>
    /// <param name="kit">清单中的 kit 对象。</param>
    private static void AssertSourceHashMatchesImplementation(string packageRoot, JsonElement kit)
    {
        // ② sourceHash 与实现文件一致（LF 归一化，和 Workbench 的校验口径相同）
        string sourcePath = Path.Combine(
            packageRoot, "Tools", "RoslynKit", "Editor", "RoslynKitProvider.cs");
        string expected = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(
                File.ReadAllText(sourcePath).Replace("\r\n", "\n")))).ToLowerInvariant();
        string declaredHash = kit.GetProperty("sourceHash").GetString()!;
        Assert.Equal(expected, declaredHash);
    }

    /// <summary>断言每个动作的 verifyRecipe 都存在于配方表。不改清单。</summary>
    /// <param name="kit">清单中的 kit 对象。</param>
    /// <param name="commands">清单中的 commands 数组。</param>
    private static void AssertCommandsReferenceExistingRecipes(JsonElement kit, JsonElement commands)
    {
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

    /// <summary>装配带临时运行调度器的 RoslynKit 提供者。不发送命令。</summary>
    /// <returns>启用状态下的提供者。</returns>
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

    /// <summary>从测试输出目录向上查找包根。找不到时抛出 DirectoryNotFoundException。</summary>
    /// <returns>包含 Core/Editor/Skills 的包根绝对路径。</returns>
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
