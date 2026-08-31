using System.Security.Cryptography;
using YokiFrame.Protocol.ProjectModel;

namespace YokiFrame.Protocol.Tests.ProjectModel;

/// <summary>
/// 覆盖 Core/Tool Kit 与 Unity 只读诊断 descriptor 的 schema、来源哈希和宿主范围。
/// </summary>
public sealed class ProjectCapabilityDescriptorTests
{
    private static readonly string[] sKnownCommandKinds =
    {
        "ReadOnly",
        "Maintenance",
        "UserAction",
        "Dangerous"
    };

    private static readonly string[] sKnownEngineKinds =
    {
        "Unity",
        "Godot"
    };

    /// <summary>
    /// 验证 System 只声明当前真实命令，并准确区分 Unity/Godot engine scope。
    /// </summary>
    [Fact]
    public void SystemDescriptorMatchesCurrentUnityAndGodotCommands()
    {
        var descriptor = ReadDescriptor("Core/Editor/CommandBridge/Capabilities/System/capability.json");

        ValidateDescriptor(descriptor);
        Assert.Equal("System", descriptor.Kit.Kit);
        Assert.Equal(
            new[]
            {
                "ping",
                "bridge_status",
                "list_commands",
                "refresh_snapshots",
                "get_environment",
                "open_project_folder",
                "open_log",
                "open_code_location"
            },
            descriptor.Kit.Commands.Select(command => command.Action));
        Assert.Equal(
            sKnownEngineKinds,
            FindCommand(descriptor, "ping").EngineKinds);
        Assert.Equal(
            sKnownEngineKinds,
            FindCommand(descriptor, "bridge_status").EngineKinds);
        Assert.Equal(
            sKnownEngineKinds,
            FindCommand(descriptor, "list_commands").EngineKinds);
        Assert.All(
            descriptor.Kit.Commands.Skip(3),
            command => Assert.Equal(new[] { "Unity" }, command.EngineKinds));
        Assert.All(
            descriptor.Kit.Commands.Where(command => command.Kind == "UserAction"),
            command => Assert.NotEmpty(command.SideEffects));
    }

    /// <summary>
    /// 验证 FsmKit descriptor 只包含 Unity/Godot 两端已有的五条只读诊断命令。
    /// </summary>
    [Fact]
    public void FsmKitDescriptorContainsOnlyFiveReadOnlyCommands()
    {
        var descriptor = ReadDescriptor("Core/Editor/FsmKit/Capabilities/capability.json");

        ValidateDescriptor(descriptor);
        Assert.Equal("FsmKit", descriptor.Kit.Kit);
        Assert.Equal(
            new[]
            {
                "list_all",
                "get_state",
                "get_history",
                "get_state_events",
                "get_workbench_snapshot"
            },
            descriptor.Kit.Commands.Select(command => command.Action));
        Assert.All(descriptor.Kit.Commands, command => Assert.Equal("ReadOnly", command.Kind));
        Assert.All(descriptor.Kit.Commands, command => Assert.Equal(sKnownEngineKinds, command.EngineKinds));
    }

    /// <summary>验证 EventKit 只声明 state 和唯一只读 Workbench 快照命令。</summary>
    [Fact]
    public void EventKitDescriptorContainsOnlyWorkbenchSnapshotCommand()
    {
        var descriptor = ReadDescriptor("Core/Editor/EventKit/Capabilities/capability.json");

        ValidateDescriptor(descriptor);
        Assert.Equal("EventKit", descriptor.Kit.Kit);
        Assert.Equal(new[] { "state" }, descriptor.Kit.SnapshotNames);
        Assert.Equal(new[] { "state" }, descriptor.Kit.TelemetryNames);
        ProjectCapabilityCommand command = Assert.Single(descriptor.Kit.Commands);
        Assert.Equal("get_workbench_snapshot", command.Action);
        Assert.Equal("ReadOnly", command.Kind);
        Assert.Equal(sKnownEngineKinds, command.EngineKinds);
        Assert.Equal("eventkit-read-only", command.VerifyRecipe);
    }

    /// <summary>验证 ResKit 只发布唯一 state、六个只读诊断和两个显式 UserAction。</summary>
    [Fact]
    public void ResKitDescriptorMatchesRuntimeInteractionContract()
    {
        var descriptor = ReadDescriptor("Core/Editor/ResKit/Capabilities/capability.json");

        ValidateDescriptor(descriptor);
        Assert.Equal("ResKit", descriptor.Kit.Kit);
        Assert.Equal(new[] { "state" }, descriptor.Kit.SnapshotNames);
        Assert.Equal(new[] { "state" }, descriptor.Kit.TelemetryNames);
        Assert.Equal(
            new[]
            {
                "stats",
                "get_workbench_snapshot",
                "list_resources",
                "get_resource_detail",
                "diagnose_resource",
                "get_unload_history",
                "clear_history",
                "set_tracking"
            },
            descriptor.Kit.Commands.Select(command => command.Action));
        Assert.All(descriptor.Kit.Commands.Take(6), command => Assert.Equal("ReadOnly", command.Kind));
        Assert.All(descriptor.Kit.Commands.Skip(6), command =>
        {
            Assert.Equal("UserAction", command.Kind);
            Assert.NotEmpty(command.SideEffects);
        });
        Assert.All(descriptor.Kit.Commands, command => Assert.Equal(sKnownEngineKinds, command.EngineKinds));
    }

    /// <summary>验证 ActionKit Tool descriptor 与当前 Interaction Provider 的公开能力一致。</summary>
    [Fact]
    public void ActionKitDescriptorMatchesRuntimeInteractionContract()
    {
        var descriptor = ReadDescriptor("Tools/ActionKit/Editor/Capabilities/capability.json");

        ValidateDescriptor(descriptor, "Tool");
        Assert.Equal("ActionKit", descriptor.Kit.Kit);
        Assert.Equal(new[] { "state" }, descriptor.Kit.SnapshotNames);
        Assert.Equal(new[] { "state" }, descriptor.Kit.TelemetryNames);
        Assert.Equal(
            new[]
            {
                "stats",
                "get_workbench_snapshot",
                "set_stack_trace",
                "clear_stack_trace"
            },
            descriptor.Kit.Commands.Select(command => command.Action));
        Assert.All(descriptor.Kit.Commands.Take(2), command => Assert.Equal("ReadOnly", command.Kind));
        Assert.All(descriptor.Kit.Commands.Skip(2), command =>
        {
            Assert.Equal("UserAction", command.Kind);
            Assert.NotEmpty(command.SideEffects);
        });
        Assert.All(descriptor.Kit.Commands, command => Assert.Equal(sKnownEngineKinds, command.EngineKinds));
    }

    /// <summary>验证 AudioKit descriptor 只发布两个观察 action，不能成为 Runtime 操作入口。</summary>
    [Fact]
    public void AudioKitDescriptorMatchesReadonlyObserverContract()
    {
        var descriptor = ReadDescriptor("Tools/AudioKit/Editor/Capabilities/capability.json");

        ValidateDescriptor(descriptor, "Tool");
        Assert.Equal("AudioKit", descriptor.Kit.Kit);
        Assert.Equal(new[] { "state" }, descriptor.Kit.SnapshotNames);
        Assert.Equal(new[] { "state" }, descriptor.Kit.TelemetryNames);
        Assert.Equal(
            new[] { "stats", "get_workbench_snapshot" },
            descriptor.Kit.Commands.Select(command => command.Action));
        Assert.All(descriptor.Kit.Commands, command =>
        {
            Assert.Equal("ReadOnly", command.Kind);
            Assert.Empty(command.SideEffects);
            Assert.Equal(sKnownEngineKinds, command.EngineKinds);
        });
    }

    /// <summary>
    /// 验证 Validation descriptor 只声明编译状态和 Console Error 两项只读诊断。
    /// </summary>
    [Fact]
    public void ValidationDescriptorContainsOnlyMinimalUnityDiagnostics()
    {
        var descriptor = ReadDescriptor("Core/Editor/CommandBridge/Capabilities/Validation/capability.json");

        Assert.Equal(ProjectModelContract.SCHEMA_VERSION, descriptor.SchemaVersion);
        Assert.Equal(ProjectModelContract.CAPABILITY_DESCRIPTOR_KIND, descriptor.Kind);
        Assert.Equal("Validation", descriptor.Kit.Kit);
        Assert.Equal("Available", descriptor.Kit.State);
        Assert.Equal("Diagnostics", descriptor.Kit.Role);
        Assert.True(descriptor.Kit.CommandCatalogDeclared);
        Assert.Empty(descriptor.Kit.SnapshotNames);
        Assert.Empty(descriptor.Kit.TelemetryNames);
        Assert.Equal(new[] { "inspect_status", "get_console_errors" }, descriptor.Kit.Commands.Select(command => command.Action));
        Assert.All(descriptor.Kit.Commands, command =>
        {
            Assert.Equal("ReadOnly", command.Kind);
            Assert.Equal(new[] { "Unity" }, command.EngineKinds);
        });
        AssertSourceHash(descriptor.Kit);
    }

    /// <summary>
    /// 验证包内全部 capability descriptor 都绑定当前实现源码，避免未被逐项语义测试覆盖的 Kit 静默漂移。
    /// </summary>
    [Fact]
    public void AllPackageCapabilityDescriptorsHaveCurrentSourceHashes()
    {
        var packageRoot = FindPackageRoot();
        var descriptorPaths = Directory
            .EnumerateFiles(Path.Combine(packageRoot, "Core"), "capability.json", SearchOption.AllDirectories)
            .Concat(Directory.EnumerateFiles(Path.Combine(packageRoot, "Tools"), "capability.json", SearchOption.AllDirectories))
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();

        Assert.NotEmpty(descriptorPaths);
        Assert.All(
            descriptorPaths,
            path => AssertSourceHash(ProjectCapabilityDescriptor.FromJson(File.ReadAllText(path)).Kit));
    }

    /// <summary>
    /// 校验通用 descriptor schema、唯一 action、验证配方引用和实现来源哈希。
    /// </summary>
    /// <param name="descriptor">待检查 descriptor。</param>
    /// <param name="expectedRole">预期 Core 或 Tool 角色。</param>
    private static void ValidateDescriptor(ProjectCapabilityDescriptor descriptor, string expectedRole = "Core")
    {
        Assert.Equal(ProjectModelContract.SCHEMA_VERSION, descriptor.SchemaVersion);
        Assert.Equal(ProjectModelContract.CAPABILITY_DESCRIPTOR_KIND, descriptor.Kind);
        Assert.Equal("Available", descriptor.Kit.State);
        Assert.Equal(expectedRole, descriptor.Kit.Role);
        Assert.True(descriptor.Kit.CommandCatalogDeclared);
        Assert.NotEmpty(descriptor.Kit.SnapshotNames);
        Assert.NotEmpty(descriptor.Kit.TelemetryNames);
        Assert.NotEmpty(descriptor.Kit.Commands);
        Assert.Equal(
            descriptor.Kit.Commands.Count,
            descriptor.Kit.Commands.Select(command => command.Action).Distinct(StringComparer.Ordinal).Count());
        var recipeIds = descriptor.Kit.VerifyRecipes
            .Select(recipe => recipe.Id)
            .ToHashSet(StringComparer.Ordinal);
        Assert.All(descriptor.Kit.Commands, command =>
        {
            Assert.Contains(command.Kind, sKnownCommandKinds);
            Assert.NotEmpty(command.EngineKinds);
            Assert.All(command.EngineKinds, engine => Assert.Contains(engine, sKnownEngineKinds));
            Assert.NotEmpty(command.Preconditions);
            Assert.Contains(command.VerifyRecipe, recipeIds);
        });
        AssertSourceHash(descriptor.Kit);
        Assert.Equal(descriptor, ProjectCapabilityDescriptor.FromJson(descriptor.ToJson()), DescriptorComparer.Instance);
    }

    /// <summary>
    /// 验证 descriptor 的 sourceHash 对应当前包内实现文件，防止静态声明与源码静默漂移。
    /// </summary>
    /// <param name="kit">待检查 Kit 声明。</param>
    private static void AssertSourceHash(ProjectCapabilityKit kit)
    {
        var sourcePath = Path.Combine(FindPackageRoot(), kit.SourcePath.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(File.Exists(sourcePath), "Capability source was not found: " + sourcePath);
        var actualHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant();
        Assert.Equal(actualHash, kit.SourceHash);
    }

    /// <summary>
    /// 从 descriptor 中读取唯一 action。
    /// </summary>
    /// <param name="descriptor">能力 descriptor。</param>
    /// <param name="action">目标 action。</param>
    /// <returns>唯一命令描述。</returns>
    private static ProjectCapabilityCommand FindCommand(ProjectCapabilityDescriptor descriptor, string action)
    {
        return Assert.Single(descriptor.Kit.Commands, command => command.Action == action);
    }

    /// <summary>
    /// 从包内相对路径读取并反序列化 capability descriptor。
    /// </summary>
    /// <param name="relativePath">YokiFrame 包内相对路径。</param>
    /// <returns>解析后的 descriptor。</returns>
    private static ProjectCapabilityDescriptor ReadDescriptor(string relativePath)
    {
        var path = Path.Combine(FindPackageRoot(), relativePath.Replace('/', Path.DirectorySeparatorChar));
        return ProjectCapabilityDescriptor.FromJson(File.ReadAllText(path));
    }

    /// <summary>
    /// 从测试输出向上定位同时包含 package.json 与 Core 的 YokiFrame 包根。
    /// </summary>
    /// <returns>YokiFrame 包根绝对路径。</returns>
    private static string FindPackageRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null)
        {
            var directCandidate = directory.FullName;
            if (IsPackageRoot(directCandidate))
            {
                return directCandidate;
            }

            var workspaceCandidate = Path.Combine(directory.FullName, "Assets", "YokiFrame");
            if (IsPackageRoot(workspaceCandidate))
            {
                return workspaceCandidate;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("无法定位 YokiFrame 包根。");
    }

    /// <summary>
    /// 判断候选目录是否为当前 YokiFrame 包根。
    /// </summary>
    /// <param name="path">候选绝对路径。</param>
    /// <returns>同时存在 package.json 与 Core 时返回 true。</returns>
    private static bool IsPackageRoot(string path)
    {
        return File.Exists(Path.Combine(path, "package.json"))
            && Directory.Exists(Path.Combine(path, "Core"));
    }

    /// <summary>
    /// 只比较 descriptor roundtrip 后的稳定业务字段，避免测试依赖对象引用相等。
    /// </summary>
    private sealed class DescriptorComparer : IEqualityComparer<ProjectCapabilityDescriptor>
    {
        /// <summary>获取共享 comparer 实例。</summary>
        public static DescriptorComparer Instance { get; } = new();

        /// <summary>
        /// 比较 descriptor 的 schema、kind、Kit 和 action 顺序。
        /// </summary>
        /// <param name="left">左侧 descriptor。</param>
        /// <param name="right">右侧 descriptor。</param>
        /// <returns>稳定字段相同时返回 true。</returns>
        public bool Equals(ProjectCapabilityDescriptor? left, ProjectCapabilityDescriptor? right)
        {
            return left != null
                && right != null
                && left.SchemaVersion == right.SchemaVersion
                && left.Kind == right.Kind
                && left.Kit.Kit == right.Kit.Kit
                && left.Kit.Commands.Select(command => command.Action).SequenceEqual(
                    right.Kit.Commands.Select(command => command.Action),
                    StringComparer.Ordinal);
        }

        /// <summary>
        /// 返回 descriptor schema、kind 和 Kit 的组合哈希。
        /// </summary>
        /// <param name="value">descriptor。</param>
        /// <returns>稳定字段组合哈希。</returns>
        public int GetHashCode(ProjectCapabilityDescriptor value)
        {
            return HashCode.Combine(value.SchemaVersion, value.Kind, value.Kit.Kit);
        }
    }
}
