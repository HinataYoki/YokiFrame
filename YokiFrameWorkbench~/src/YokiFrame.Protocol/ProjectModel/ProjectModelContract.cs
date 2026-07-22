namespace YokiFrame.Protocol.ProjectModel;

/// <summary>
/// 定义 Project Model v1 的固定 schema 版本、文件名和文档 kind。
/// </summary>
public static class ProjectModelContract
{
    /// <summary>Project Model 当前 schema 版本。</summary>
    public const int SCHEMA_VERSION = 1;

    /// <summary>Project Model 在 `.yokiframe` 下的目录名。</summary>
    public const string PROJECT_DIRECTORY = "project";

    /// <summary>聚合 manifest 文件名。</summary>
    public const string PROJECT_MODEL_FILE_NAME = "project-model.json";

    /// <summary>架构文档文件名。</summary>
    public const string ARCHITECTURE_FILE_NAME = "architecture.json";

    /// <summary>静态能力文档文件名。</summary>
    public const string CAPABILITIES_FILE_NAME = "capabilities.json";

    /// <summary>依赖文档文件名。</summary>
    public const string DEPENDENCIES_FILE_NAME = "dependencies.json";

    /// <summary>验证策略文档文件名。</summary>
    public const string VALIDATION_PROFILE_FILE_NAME = "validation-profile.json";

    /// <summary>聚合 manifest 的 kind。</summary>
    public const string PROJECT_MODEL_KIND = "project-model";

    /// <summary>架构文档的 kind。</summary>
    public const string ARCHITECTURE_KIND = "architecture";

    /// <summary>静态能力文档的 kind。</summary>
    public const string CAPABILITIES_KIND = "capabilities";

    /// <summary>依赖文档的 kind。</summary>
    public const string DEPENDENCIES_KIND = "dependencies";

    /// <summary>验证策略文档的 kind。</summary>
    public const string VALIDATION_PROFILE_KIND = "validation-profile";

    /// <summary>包内单 Kit capability descriptor 的 kind。</summary>
    public const string CAPABILITY_DESCRIPTOR_KIND = "capability-descriptor";

    /// <summary>按提交顺序列出 Project Model 的五个固定文件名。</summary>
    public static readonly IReadOnlyList<string> FILE_NAMES = Array.AsReadOnly(new[]
    {
        PROJECT_MODEL_FILE_NAME,
        ARCHITECTURE_FILE_NAME,
        CAPABILITIES_FILE_NAME,
        DEPENDENCIES_FILE_NAME,
        VALIDATION_PROFILE_FILE_NAME
    });
}
