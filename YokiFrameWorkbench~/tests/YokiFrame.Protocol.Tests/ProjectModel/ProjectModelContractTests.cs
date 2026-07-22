using YokiFrame.Protocol.ProjectModel;

namespace YokiFrame.Protocol.Tests.ProjectModel;

/// <summary>
/// 覆盖 Project Model v1 的固定目录文件名，防止生成端和读取端产生第二套命名。
/// </summary>
public sealed class ProjectModelContractTests
{
    /// <summary>
    /// 验证 schema 版本和五个公开文件名保持稳定。
    /// </summary>
    [Fact]
    public void ContractUsesSchemaVersionOneAndFixedFileNames()
    {
        Assert.Equal(1, ProjectModelContract.SCHEMA_VERSION);
        Assert.Equal("project-model.json", ProjectModelContract.PROJECT_MODEL_FILE_NAME);
        Assert.Equal("architecture.json", ProjectModelContract.ARCHITECTURE_FILE_NAME);
        Assert.Equal("capabilities.json", ProjectModelContract.CAPABILITIES_FILE_NAME);
        Assert.Equal("dependencies.json", ProjectModelContract.DEPENDENCIES_FILE_NAME);
        Assert.Equal("validation-profile.json", ProjectModelContract.VALIDATION_PROFILE_FILE_NAME);
        Assert.Equal(
            new[]
            {
                "project-model.json",
                "architecture.json",
                "capabilities.json",
                "dependencies.json",
                "validation-profile.json"
            },
            ProjectModelContract.FILE_NAMES);
    }
}
