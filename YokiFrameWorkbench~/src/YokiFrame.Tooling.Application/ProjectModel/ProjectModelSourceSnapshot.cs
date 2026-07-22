namespace YokiFrame.Tooling.Application.ProjectModel;

/// <summary>
/// 保存一次 Project Model 刷新使用的已规范化项目、包和依赖事实。
/// </summary>
internal sealed class ProjectModelSourceSnapshot
{
    /// <summary>
    /// 创建不可变的项目事实快照；绝对路径仅供本次生成使用，不会直接写入持久模型。
    /// </summary>
    public ProjectModelSourceSnapshot(
        string projectRoot,
        string projectKind,
        string engineVersion,
        string packageRoot,
        string packageRelativeRoot,
        string packageName,
        string packageVersion,
        string packageSource,
        IReadOnlyList<ProjectModelSourceFile> sourceFiles,
        IReadOnlyList<ProjectDependencyFact> dependencies,
        IReadOnlyList<string> capabilityDescriptorPaths,
        ProjectHarnessDeclarations harnessDeclarations)
    {
        ProjectRoot = projectRoot;
        ProjectKind = projectKind;
        EngineVersion = engineVersion;
        PackageRoot = packageRoot;
        PackageRelativeRoot = packageRelativeRoot;
        PackageName = packageName;
        PackageVersion = packageVersion;
        PackageSource = packageSource;
        SourceFiles = sourceFiles.ToArray();
        Dependencies = dependencies.ToArray();
        CapabilityDescriptorPaths = capabilityDescriptorPaths.ToArray();
        HarnessDeclarations = harnessDeclarations;
    }

    /// <summary>获取当前项目绝对根，仅供本次安全读取使用。</summary>
    public string ProjectRoot { get; }

    /// <summary>获取 Unity 或 Godot 项目类型。</summary>
    public string ProjectKind { get; }

    /// <summary>获取项目声明的引擎版本。</summary>
    public string EngineVersion { get; }

    /// <summary>获取本次解析到的 YokiFrame 包绝对根。</summary>
    public string PackageRoot { get; }

    /// <summary>获取项目相对的 YokiFrame 包根。</summary>
    public string PackageRelativeRoot { get; }

    /// <summary>获取包逻辑名称。</summary>
    public string PackageName { get; }

    /// <summary>获取包版本。</summary>
    public string PackageVersion { get; }

    /// <summary>获取 Development、Embedded、GitCache 或 GodotProjection 来源。</summary>
    public string PackageSource { get; }

    /// <summary>获取参与 input hash 的确定性源文件。</summary>
    public IReadOnlyList<ProjectModelSourceFile> SourceFiles { get; }

    /// <summary>获取结构化解析出的项目依赖。</summary>
    public IReadOnlyList<ProjectDependencyFact> Dependencies { get; }

    /// <summary>获取项目相对的 package capability descriptor 路径。</summary>
    public IReadOnlyList<string> CapabilityDescriptorPaths { get; }

    /// <summary>获取旧 bootstrap 中尚未被正式 descriptor 覆盖的声明事实。</summary>
    public ProjectHarnessDeclarations HarnessDeclarations { get; }
}

/// <summary>
/// 描述参与 Project Model input hash 的单个源文件。
/// </summary>
internal sealed class ProjectModelSourceFile
{
    /// <summary>
    /// 创建源文件证据；相对路径用于模型，绝对路径只用于当前进程读取。
    /// </summary>
    public ProjectModelSourceFile(string absolutePath, string relativePath, string sha256)
    {
        AbsolutePath = absolutePath;
        RelativePath = relativePath;
        Sha256 = sha256;
    }

    /// <summary>获取源文件绝对路径。</summary>
    public string AbsolutePath { get; }

    /// <summary>获取项目相对正斜杠路径。</summary>
    public string RelativePath { get; }

    /// <summary>获取源文件 SHA-256。</summary>
    public string Sha256 { get; }
}

/// <summary>
/// 描述从 Unity manifest 或 Godot csproj 读取的单条依赖事实。
/// </summary>
internal sealed class ProjectDependencyFact
{
    /// <summary>创建结构化依赖事实。</summary>
    public ProjectDependencyFact(string name, string reference, string kind, string sourcePath)
    {
        Name = name;
        Reference = reference;
        Kind = kind;
        SourcePath = sourcePath;
    }

    /// <summary>获取依赖名称。</summary>
    public string Name { get; }

    /// <summary>获取版本、URI 或项目引用。</summary>
    public string Reference { get; }

    /// <summary>获取 Package、Project 或 EngineSdk 类型。</summary>
    public string Kind { get; }

    /// <summary>获取依赖来源的项目相对路径。</summary>
    public string SourcePath { get; }
}

/// <summary>
/// 保存 bootstrap harness 中可作为兼容声明来源的 engine 与 Kit 集合。
/// </summary>
internal sealed class ProjectHarnessDeclarations
{
    /// <summary>创建已排序去重的 bootstrap 声明。</summary>
    public ProjectHarnessDeclarations(
        IReadOnlyList<string> engineKinds,
        IReadOnlyList<string> snapshotKits,
        IReadOnlyList<string> commandKits)
    {
        EngineKinds = engineKinds.ToArray();
        SnapshotKits = snapshotKits.ToArray();
        CommandKits = commandKits.ToArray();
    }

    /// <summary>获取 bootstrap 声明的宿主类型。</summary>
    public IReadOnlyList<string> EngineKinds { get; }

    /// <summary>获取 bootstrap 声明的 snapshot Kit。</summary>
    public IReadOnlyList<string> SnapshotKits { get; }

    /// <summary>获取 bootstrap 声明的 command Kit。</summary>
    public IReadOnlyList<string> CommandKits { get; }
}
