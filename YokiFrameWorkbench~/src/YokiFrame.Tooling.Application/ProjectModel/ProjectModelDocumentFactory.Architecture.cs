using YokiFrame.Protocol.ProjectModel;

namespace YokiFrame.Tooling.Application.ProjectModel;

/// <summary>
/// 生成 Project Model 的架构边界和写入 owner 投影。
/// </summary>
internal static partial class ProjectModelDocumentFactory
{
    /// <summary>创建架构文档，并只声明当前包内真实存在的边界。</summary>
    private static ProjectArchitectureDocument CreateArchitecture(
        ProjectModelSourceSnapshot snapshot,
        string generation,
        string modelId,
        string generatedAtUtc)
    {
        var boundaries = new List<ProjectArchitectureBoundary>();
        AddBoundary(boundaries, snapshot, "core", "Core", "Core/Runtime", "YokiFrame.asmdef", string.Empty, Array.Empty<string>());
        AddBoundary(boundaries, snapshot, "core-editor", "Editor", "Core/Editor", "YokiFrame.Editor.asmdef", string.Empty, new[] { "core" });
        AddBoundary(boundaries, snapshot, "unity-runtime-adapter", "Adapter", "Core/Adapters/Unity/Runtime", "YokiFrame.Unity.Runtime.asmdef", "Unity", new[] { "core" });
        AddBoundary(boundaries, snapshot, "unity-editor-adapter", "Adapter", "Core/Adapters/Unity/Editor", "YokiFrame.Unity.Editor.asmdef", "Unity", new[] { "core", "core-editor" });
        AddBoundary(boundaries, snapshot, "godot-runtime-adapter", "Adapter", "Core/Adapters/Godot/Runtime", "YokiFrame.Godot.Runtime.csproj", "Godot", new[] { "core" });
        AddBoundary(boundaries, snapshot, "godot-editor-adapter", "Adapter", "Core/Adapters/Godot/Editor", "YokiFrame.Godot.Editor.csproj", "Godot", new[] { "core", "core-editor" });
        AddBoundary(boundaries, snapshot, "tooling", "Tooling", "YokiFrameWorkbench~", "YokiFrameWorkbench~/*.csproj", string.Empty, new[] { "core", "protocol", "client", "installer-core" });
        AddBoundary(boundaries, snapshot, "tools", "Tools", "Tools", "Tools/*/Runtime", string.Empty, new[] { "core" });
        return new ProjectArchitectureDocument
        {
            ModelGeneration = generation,
            ModelId = modelId,
            GeneratedAtUtc = generatedAtUtc,
            Profile = "core-adapter-tooling-v1",
            Boundaries = boundaries,
            Ownership = CreateOwnership(snapshot),
            Invariants = CreateInvariants(snapshot)
        };
    }

    /// <summary>只为当前包内存在的目录加入架构边界。</summary>
    private static void AddBoundary(
        ICollection<ProjectArchitectureBoundary> boundaries,
        ProjectModelSourceSnapshot snapshot,
        string id,
        string role,
        string relativeRoot,
        string compilationBoundary,
        string engineKind,
        IReadOnlyList<string> dependencies)
    {
        var absoluteRoot = Path.Combine(snapshot.PackageRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(absoluteRoot))
        {
            return;
        }

        boundaries.Add(new ProjectArchitectureBoundary
        {
            Id = id,
            Role = role,
            Root = CombinePackageRelative(snapshot.PackageRelativeRoot, relativeRoot),
            CompilationBoundary = compilationBoundary,
            EngineKind = engineKind,
            Dependencies = dependencies.ToList()
        });
    }

    /// <summary>创建项目级路径 owner，明确 AI 需要经过控制面才能变更的边界。</summary>
    private static List<ProjectPathOwnership> CreateOwnership(ProjectModelSourceSnapshot snapshot)
    {
        var packageRoot = snapshot.PackageRelativeRoot;
        return new List<ProjectPathOwnership>
        {
            new() { Path = ".yokiframe/project", Owner = "YokiFrame.Tooling.Application", Access = "GeneratedCache" },
            new() { Path = ".yokiframe/harness", Owner = "YokiFrame.Tooling.Application", Access = "GeneratedBootstrap" },
            new() { Path = ".yokiframe/workflows", Owner = "YokiFrame.Tooling.Application", Access = "ControlPlaneState" },
            new() { Path = ".yokiframe/engines", Owner = "EngineAdapter", Access = "RuntimeState" },
            new() { Path = CombinePackageRelative(packageRoot, "Core/Runtime"), Owner = "YokiFrame", Access = "ManagedPackage" },
            new() { Path = CombinePackageRelative(packageRoot, "Core/Editor"), Owner = "YokiFrame", Access = "ManagedPackage" },
            new() { Path = CombinePackageRelative(packageRoot, "Core/Adapters"), Owner = "YokiFrame", Access = "ManagedPackage" },
            new() { Path = CombinePackageRelative(packageRoot, "Core/Integrations"), Owner = "YokiFrame", Access = "ManagedPackage" },
            new() { Path = CombinePackageRelative(packageRoot, "Tools"), Owner = "YokiFrame", Access = "PlanRequired" },
            new() { Path = CombinePackageRelative(packageRoot, "YokiFrameWorkbench~"), Owner = "YokiFrame.Tooling", Access = "ManagedPackage" },
            new() { Path = "Assets/**", Owner = "User", Access = "PlanRequired" },
            new() { Path = "Packages/**", Owner = "User", Access = "PlanRequired" }
        };
    }

    /// <summary>创建可由路径与编译边界直接证明的架构约束。</summary>
    private static List<ProjectArchitectureInvariant> CreateInvariants(ProjectModelSourceSnapshot snapshot)
    {
        var packageRoot = snapshot.PackageRelativeRoot;
        return new List<ProjectArchitectureInvariant>
        {
            CreateInvariant(
                "CoreAssemblyBoundary",
                "Core Runtime has an explicit assembly/project boundary.",
                Path.Combine(packageRoot, "Core/Runtime/YokiFrame.asmdef"),
                Path.Combine(snapshot.PackageRoot, "Core", "Runtime", "YokiFrame.asmdef")),
            new()
            {
                Code = "GeneratedModelOwner",
                Description = "Project Model is owned by Tooling.Application and stored under .yokiframe/project.",
                Severity = "Error",
                Satisfied = true,
                EvidencePaths = new List<string> { ".yokiframe/project" }
            },
            CreateInvariant(
                "AdapterAssemblyBoundary",
                "Engine adapters are represented as separate host boundaries.",
                Path.Combine(packageRoot, "Core/Adapters"),
                Path.Combine(snapshot.PackageRoot, "Core", "Adapters"))
        };
    }

    /// <summary>根据证据路径创建满足或不满足的架构约束。</summary>
    private static ProjectArchitectureInvariant CreateInvariant(
        string code,
        string description,
        string evidencePath,
        string absoluteEvidencePath)
    {
        var normalized = evidencePath.Replace(Path.DirectorySeparatorChar, '/');
        return new ProjectArchitectureInvariant
        {
            Code = code,
            Description = description,
            Severity = "Error",
            Satisfied = File.Exists(absoluteEvidencePath) || Directory.Exists(absoluteEvidencePath),
            EvidencePaths = new[] { normalized }.ToList()
        };
    }

    /// <summary>拼接包根与包内相对路径，并统一使用正斜杠。</summary>
    private static string CombinePackageRelative(string packageRoot, string relativePath)
    {
        return (packageRoot.TrimEnd('/') + "/" + relativePath.TrimStart('/')).Replace('\\', '/');
    }
}
