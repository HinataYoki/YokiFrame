using System.Security.Cryptography;
using System.Text.Json;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Validation;

namespace YokiFrame.Tooling.Application.ProjectModel;

/// <summary>
/// 读取 package-owned capability descriptor，生成唯一静态能力事实。
/// </summary>
internal static partial class ProjectModelDocumentFactory
{
    private static readonly HashSet<string> sValidCommandKinds =
        new(StringComparer.Ordinal) { "ReadOnly", "Maintenance", "UserAction", "Dangerous" };
    /// <summary>创建当前项目适用的静态能力文档。</summary>
    private static ProjectCapabilitiesDocument CreateCapabilities(
        ProjectModelSourceSnapshot snapshot,
        string generation,
        string modelId,
        string generatedAtUtc)
    {
        List<ProjectCapabilityKit> kits = new();
        HashSet<string> seenDescriptorKits = new(StringComparer.Ordinal);
        foreach (var descriptorPath in snapshot.CapabilityDescriptorPaths)
        {
            var descriptor = ReadDescriptor(snapshot, descriptorPath);
            ValidateDescriptor(snapshot, descriptor, descriptorPath);
            if (!seenDescriptorKits.Add(descriptor.Kit.Kit))
            {
                throw CreateDescriptorError(
                    "CapabilityDescriptorDuplicateKit",
                    "Capability descriptors contain a duplicate Kit: " + descriptor.Kit.Kit,
                    descriptorPath);
            }

            if (descriptor.Kit.Commands.Count > 0
                && !descriptor.Kit.Commands.SelectMany(command => command.EngineKinds)
                    .Contains(snapshot.ProjectKind, StringComparer.Ordinal))
            {
                continue;
            }

            kits.Add(descriptor.Kit);
        }

        return new ProjectCapabilitiesDocument
        {
            ModelGeneration = generation,
            ModelId = modelId,
            GeneratedAtUtc = generatedAtUtc,
            SourcePaths = kits
                .Select(static kit => kit.SourcePath)
                .Where(static path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static path => path, StringComparer.Ordinal)
                .ToList(),
            EngineKinds = new[] { snapshot.ProjectKind }.ToList(),
            Kits = kits.OrderBy(static kit => kit.Kit, StringComparer.Ordinal).ToList()
        };
    }

    /// <summary>读取并解析单个 package-owned descriptor。</summary>
    private static ProjectCapabilityDescriptor ReadDescriptor(ProjectModelSourceSnapshot snapshot, string relativePath)
    {
        if (ProjectModelPathPolicy.IsPortableRooted(relativePath))
        {
            throw CreateDescriptorError(
                "CapabilityDescriptorPathInvalid",
                "Capability descriptor path must be project-relative.",
                relativePath);
        }

        var absolutePath = Path.GetFullPath(Path.Combine(snapshot.ProjectRoot, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!ProjectModelPathPolicy.IsInsideOrSame(snapshot.PackageRoot, absolutePath))
        {
            throw CreateDescriptorError(
                "CapabilityDescriptorEscapesPackage",
                "Capability descriptor path escapes the YokiFrame package.",
                relativePath,
                absolutePath);
        }

        if (ProjectModelPathPolicy.ContainsReparsePoint(snapshot.PackageRoot, absolutePath))
        {
            throw CreateDescriptorError(
                "CapabilityDescriptorReparsePoint",
                "Capability descriptor path contains a symbolic link or junction.",
                relativePath,
                absolutePath);
        }

        try
        {
            return ProjectCapabilityDescriptor.FromJson(File.ReadAllText(absolutePath));
        }
        catch (JsonException exception)
        {
            throw CreateDescriptorError(
                "CapabilityDescriptorInvalid",
                "Capability descriptor JSON is invalid: " + exception.Message,
                relativePath);
        }
    }

    /// <summary>校验 Kit/action SafeId、kind、唯一性和必要描述，拒绝猜测式能力。</summary>
    private static void ValidateDescriptor(ProjectModelSourceSnapshot snapshot, ProjectCapabilityDescriptor descriptor, string path)
    {
        var kit = descriptor.Kit.Kit;
        SafeIdValidator.EnsureSafeId(kit, nameof(kit));
        if (descriptor.SchemaVersion != ProjectModelContract.SCHEMA_VERSION
            || !string.Equals(descriptor.Kind, ProjectModelContract.CAPABILITY_DESCRIPTOR_KIND, StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(kit)
            || string.IsNullOrWhiteSpace(descriptor.Kit.State))
        {
            throw CreateDescriptorError("CapabilityDescriptorInvalid", "Capability descriptor has invalid required fields.", path);
        }

        ValidateDescriptorSource(snapshot, descriptor.Kit, path);

        HashSet<string> actions = new(StringComparer.Ordinal);
        foreach (var command in descriptor.Kit.Commands)
        {
            SafeIdValidator.EnsureSafeId(command.Action, nameof(command.Action));
            if (!actions.Add(command.Action))
            {
                throw CreateDescriptorError("CapabilityDescriptorDuplicateAction", "Capability descriptor contains duplicate action: " + command.Action, path);
            }

            if (!sValidCommandKinds.Contains(command.Kind))
            {
                throw CreateDescriptorError("CapabilityDescriptorInvalid", "Capability descriptor contains unsupported command kind: " + command.Kind, path);
            }
        }
    }

    /// <summary>校验 descriptor 实现来源位于包内、存在且内容 hash 与声明一致。</summary>
    private static void ValidateDescriptorSource(
        ProjectModelSourceSnapshot snapshot,
        ProjectCapabilityKit kit,
        string descriptorPath)
    {
        if (string.IsNullOrWhiteSpace(kit.SourcePath)
            || ProjectModelPathPolicy.IsPortableRooted(kit.SourcePath))
        {
            throw CreateDescriptorError("CapabilitySourceInvalid", "Capability sourcePath must be a package-relative file path.", descriptorPath);
        }

        var packageRoot = Path.GetFullPath(snapshot.PackageRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var sourcePath = Path.GetFullPath(Path.Combine(packageRoot, kit.SourcePath.Replace('/', Path.DirectorySeparatorChar)));
        if (!ProjectModelPathPolicy.IsInsideOrSame(packageRoot, sourcePath))
        {
            throw CreateDescriptorError("CapabilitySourceEscapesPackage", "Capability sourcePath escapes the YokiFrame package.", descriptorPath, sourcePath);
        }

        if (ProjectModelPathPolicy.ContainsReparsePoint(packageRoot, sourcePath))
        {
            throw CreateDescriptorError(
                "CapabilitySourceReparsePoint",
                "Capability sourcePath contains a symbolic link or junction.",
                descriptorPath,
                sourcePath);
        }

        if (!File.Exists(sourcePath))
        {
            throw CreateDescriptorError("CapabilitySourceMissing", "Capability implementation source is missing.", descriptorPath, sourcePath);
        }

        using var stream = File.OpenRead(sourcePath);
        var actualHash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(actualHash, kit.SourceHash, StringComparison.OrdinalIgnoreCase))
        {
            throw CreateDescriptorError("CapabilitySourceHashMismatch", "Capability implementation hash does not match sourceHash.", descriptorPath, sourcePath);
        }
    }

    /// <summary>创建可由 CLI 转换为稳定失败结果的 descriptor 协议异常。</summary>
    private static YokiFrameProtocolException CreateDescriptorError(
        string code,
        string message,
        string descriptorPath,
        string? sourcePath = null)
    {
        var evidence = string.IsNullOrWhiteSpace(sourcePath)
            ? new[] { descriptorPath }
            : new[] { descriptorPath, sourcePath };
        return new YokiFrameProtocolException(new YokiFrameError(
            code,
            message,
            "Repair the package capability descriptor or its implementation source, then refresh the Project Model.",
            evidence));
    }

}
