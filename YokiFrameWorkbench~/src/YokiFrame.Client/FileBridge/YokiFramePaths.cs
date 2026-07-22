using YokiFrame.Client.FileBridge.IO;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Protocol.Validation;

namespace YokiFrame.Client.FileBridge;

/// <summary>
/// 解析 YokiFrame FileBridge 在项目内的标准路径。
/// </summary>
public sealed class YokiFramePaths
{
    /// <summary>
    /// 根据项目根目录创建路径解析器。
    /// </summary>
    /// <param name="projectRoot">Unity/Godot 项目根目录。</param>
    public YokiFramePaths(string projectRoot)
    {
        ProjectRoot = Path.GetFullPath(projectRoot);
        YokiFrameRoot = PathSecurity.CombineInside(ProjectRoot, YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY);
        EnginesRoot = PathSecurity.CombineInside(YokiFrameRoot, YokiFrameFileBridgeLayout.ENGINES_DIRECTORY);
        ProjectModelRoot = PathSecurity.CombineInside(YokiFrameRoot, ProjectModelContract.PROJECT_DIRECTORY);
        ProjectModelLockPath = PathSecurity.CombineInside(YokiFrameRoot, "project-model.lock");
        ProjectModelManifestPath = PathSecurity.CombineInside(ProjectModelRoot, ProjectModelContract.PROJECT_MODEL_FILE_NAME);
        ProjectArchitecturePath = PathSecurity.CombineInside(ProjectModelRoot, ProjectModelContract.ARCHITECTURE_FILE_NAME);
        ProjectCapabilitiesPath = PathSecurity.CombineInside(ProjectModelRoot, ProjectModelContract.CAPABILITIES_FILE_NAME);
        ProjectDependenciesPath = PathSecurity.CombineInside(ProjectModelRoot, ProjectModelContract.DEPENDENCIES_FILE_NAME);
        ProjectValidationProfilePath = PathSecurity.CombineInside(ProjectModelRoot, ProjectModelContract.VALIDATION_PROFILE_FILE_NAME);
    }

    /// <summary>
    /// 获取项目根目录。
    /// </summary>
    public string ProjectRoot { get; }

    /// <summary>
    /// 获取 `.yokiframe` 根目录。
    /// </summary>
    public string YokiFrameRoot { get; }

    /// <summary>
    /// 获取 FileBridge engine registry 根目录。
    /// </summary>
    public string EnginesRoot { get; }

    /// <summary>
    /// 获取 Project Model 五文件所在目录；目录缺失时由写入方按需创建。
    /// </summary>
    public string ProjectModelRoot { get; }

    /// <summary>
    /// 获取 Project Model 的项目级独占锁路径；锁位于 bundle 目录外，避免目录替换时被移动。
    /// </summary>
    public string ProjectModelLockPath { get; }

    /// <summary>获取聚合 project-model.json 固定路径。</summary>
    public string ProjectModelManifestPath { get; }

    /// <summary>获取 architecture.json 固定路径。</summary>
    public string ProjectArchitecturePath { get; }

    /// <summary>获取 capabilities.json 固定路径。</summary>
    public string ProjectCapabilitiesPath { get; }

    /// <summary>获取 dependencies.json 固定路径。</summary>
    public string ProjectDependenciesPath { get; }

    /// <summary>获取 validation-profile.json 固定路径。</summary>
    public string ProjectValidationProfilePath { get; }

    /// <summary>
    /// 获取 harness capability 文件路径。
    /// </summary>
    /// <returns>`.yokiframe/harness/capabilities.json` 的完整路径。</returns>
    public string GetHarnessCapabilitiesPath()
    {
        return PathSecurity.CombineInside(YokiFrameRoot, "harness", "capabilities.json");
    }

    /// <summary>
    /// 获取指定 engine 的根目录。
    /// </summary>
    /// <param name="engineId">安全 engine 标识。</param>
    /// <returns>engine 根目录完整路径。</returns>
    public string GetEngineRoot(string engineId)
    {
        var safeEngineId = SafeIdValidator.EnsureSafeId(engineId, nameof(engineId));
        return PathSecurity.CombineInside(EnginesRoot, safeEngineId);
    }

    /// <summary>
    /// 获取指定 engine 的 heartbeat 文件路径。
    /// </summary>
    /// <param name="engineId">安全 engine 标识。</param>
    /// <returns>heartbeat.json 完整路径。</returns>
    public string GetHeartbeatPath(string engineId)
    {
        return PathSecurity.CombineInside(
            GetEngineRoot(engineId),
            YokiFrameFileBridgeLayout.STATUS_DIRECTORY,
            YokiFrameFileBridgeLayout.HEARTBEAT_FILE_NAME);
    }

    /// <summary>
    /// 获取指定 snapshot 文件路径。
    /// </summary>
    /// <param name="engineId">安全 engine 标识。</param>
    /// <param name="kit">安全 Kit 标识。</param>
    /// <param name="name">安全 snapshot 名称。</param>
    /// <returns>snapshot JSON 完整路径。</returns>
    public string GetSnapshotPath(string engineId, string kit, string name)
    {
        var safeKit = SafeIdValidator.EnsureSafeId(kit, nameof(kit));
        var safeName = SafeIdValidator.EnsureSafeId(name, nameof(name));
        return PathSecurity.CombineInside(
            GetEngineRoot(engineId),
            YokiFrameFileBridgeLayout.SNAPSHOTS_DIRECTORY,
            safeKit,
            safeName + YokiFrameFileBridgeLayout.JSON_EXTENSION);
    }

    /// <summary>
    /// 获取指定 engine 的命令队列目录。
    /// </summary>
    /// <param name="engineId">安全 engine 标识。</param>
    /// <returns>commands 目录完整路径。</returns>
    public string GetCommandsRoot(string engineId)
    {
        return PathSecurity.CombineInside(GetEngineRoot(engineId), YokiFrameFileBridgeLayout.COMMANDS_DIRECTORY);
    }

    /// <summary>
    /// 获取指定 engine 的命令队列目录；这是带 engine 语义别名，保持旧 Client 调用方可读性。
    /// </summary>
    /// <param name="engineId">安全 engine 标识。</param>
    /// <returns>commands 目录完整路径。</returns>
    public string GetEngineCommandsRoot(string engineId)
    {
        return GetCommandsRoot(engineId);
    }

    /// <summary>
    /// 获取待处理命令文件路径。
    /// </summary>
    /// <param name="engineId">安全 engine 标识。</param>
    /// <param name="requestId">安全请求标识。</param>
    /// <returns>命令 JSON 完整路径。</returns>
    public string GetPendingCommandPath(string engineId, string requestId)
    {
        var safeRequestId = SafeIdValidator.EnsureSafeId(requestId, nameof(requestId));
        return PathSecurity.CombineInside(
            GetCommandsRoot(engineId),
            safeRequestId + YokiFrameFileBridgeLayout.JSON_EXTENSION);
    }

    /// <summary>
    /// 获取结果目录路径。
    /// </summary>
    /// <param name="engineId">安全 engine 标识。</param>
    /// <returns>results 目录完整路径。</returns>
    public string GetResultsRoot(string engineId)
    {
        return PathSecurity.CombineInside(GetEngineRoot(engineId), YokiFrameFileBridgeLayout.RESULTS_DIRECTORY);
    }

    /// <summary>
    /// 获取指定 engine 的结果目录；这是带 engine 语义别名，保持旧 Client 调用方可读性。
    /// </summary>
    /// <param name="engineId">安全 engine 标识。</param>
    /// <returns>results 目录完整路径。</returns>
    public string GetEngineResultsRoot(string engineId)
    {
        return GetResultsRoot(engineId);
    }

    /// <summary>
    /// 获取指定请求的响应文件路径。
    /// </summary>
    /// <param name="engineId">安全 engine 标识。</param>
    /// <param name="requestId">安全请求标识。</param>
    /// <returns>response JSON 完整路径。</returns>
    public string GetResponsePath(string engineId, string requestId)
    {
        var safeRequestId = SafeIdValidator.EnsureSafeId(requestId, nameof(requestId));
        return PathSecurity.CombineInside(
            GetResultsRoot(engineId),
            safeRequestId + YokiFrameFileBridgeLayout.RESPONSE_FILE_SUFFIX);
    }
}
