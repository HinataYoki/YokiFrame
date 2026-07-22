namespace YokiFrame.Tooling.Application.Models.LogKit;

/// <summary>描述当前项目持久化的 LogKit 设置及并发写入指纹。</summary>
public sealed record WorkbenchLogKitProjectSettings
{
    /// <summary>创建项目设置投影；仅由 Application 设置服务使用。</summary>
    internal WorkbenchLogKitProjectSettings(
        string engineId, string engine, bool canPersist, bool exists,
        string path, string fingerprint, WorkbenchLogKitSettings settings,
        string statusMessage)
    {
        EngineId = engineId;
        Engine = engine;
        CanPersist = canPersist;
        Exists = exists;
        Path = path;
        Fingerprint = fingerprint;
        Settings = settings;
        StatusMessage = statusMessage;
    }

    /// <summary>获取请求的 engine 标识。</summary>
    public string EngineId { get; }
    /// <summary>获取项目宿主类型。</summary>
    public string Engine { get; }
    /// <summary>获取当前项目是否允许持久保存。</summary>
    public bool CanPersist { get; }
    /// <summary>获取配置文件是否存在。</summary>
    public bool Exists { get; }
    /// <summary>获取受控配置文件路径。</summary>
    public string Path { get; }
    /// <summary>获取原文件并发写入指纹。</summary>
    public string Fingerprint { get; }
    /// <summary>获取项目设置或只读有效设置。</summary>
    public WorkbenchLogKitSettings Settings { get; }
    /// <summary>获取加载与持久化能力说明。</summary>
    public string StatusMessage { get; }
}

/// <summary>描述一次项目设置保存和当前 Runtime 应用的独立结果。</summary>
public sealed record WorkbenchLogKitSettingsSaveResult
{
    /// <summary>创建两阶段保存结果；仅由 Application 用例使用。</summary>
    internal WorkbenchLogKitSettingsSaveResult(
        bool projectSaved, bool runtimeApplied, bool conflictDetected,
        WorkbenchLogKitProjectSettings projectSettings,
        WorkbenchLogKitState? appliedState, string errorMessage)
    {
        ProjectSaved = projectSaved;
        RuntimeApplied = runtimeApplied;
        ConflictDetected = conflictDetected;
        ProjectSettings = projectSettings;
        AppliedState = appliedState;
        ErrorMessage = errorMessage;
    }

    /// <summary>获取项目文件是否已保存。</summary>
    public bool ProjectSaved { get; init; }
    /// <summary>获取当前 Runtime 是否已应用同一设置。</summary>
    public bool RuntimeApplied { get; init; }
    /// <summary>获取是否因原文件变化拒绝覆盖。</summary>
    public bool ConflictDetected { get; init; }
    /// <summary>获取保存后或冲突时的项目设置。</summary>
    public WorkbenchLogKitProjectSettings ProjectSettings { get; init; }
    /// <summary>获取 Runtime 应用命令返回的新状态。</summary>
    public WorkbenchLogKitState? AppliedState { get; init; }
    /// <summary>获取持久化或 Runtime 应用错误。</summary>
    public string ErrorMessage { get; init; }
}
