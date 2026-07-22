using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.ProjectModel;
using YokiFrame.Tooling.Application.Models.Capabilities;
using YokiFrame.Tooling.Application.Models.ProjectModel;

namespace YokiFrame.Tooling.Application.Capabilities;

/// <summary>
/// 负责把经过 Client 校验的 Project Model 投影为能力目录的正式静态事实。
/// </summary>
internal sealed partial class CapabilityCatalogBuilder
{
    private const string PROJECT_MODEL_STATE_MISSING = "Missing";
    private const string PROJECT_MODEL_STATE_READY = "Ready";
    private const string PROJECT_MODEL_STATE_STALE = "Stale";
    private const string PROJECT_MODEL_STATE_PARTIAL = "Partial";
    private const string PROJECT_MODEL_STATE_BLOCKED = "Blocked";

    private string mProjectModelId = string.Empty;
    private string mProjectModelGeneration = string.Empty;
    private string mProjectModelPath = string.Join(
        "/",
        YokiFrameFileBridgeLayout.YOKIFRAME_DIRECTORY,
        ProjectModelContract.PROJECT_DIRECTORY,
        ProjectModelContract.PROJECT_MODEL_FILE_NAME);
    private string mProjectModelInputHash = string.Empty;
    private bool mProjectModelApplied;
    private bool mProjectModelBundleAvailable;
    private bool mProjectModelTrusted;
    private bool mProjectModelBlocked;
    private bool mProjectModelDrifted;
    private readonly HashSet<string> mProjectModelCommandKits = new(StringComparer.Ordinal);
    private readonly HashSet<string> mProjectModelDeclaredEngineKinds = new(StringComparer.Ordinal);
    private readonly HashSet<string> mProjectModelDeclaredKitIds = new(StringComparer.Ordinal);

    /// <summary>
    /// 应用 Project Model 读取结果；只有 Ready bundle 才能提升静态 Kit 为正式可用。
    /// </summary>
    /// <param name="result">Project Model 检查结果。</param>
    public void ApplyProjectModel(ProjectModelResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        mProjectModelApplied = true;
        mModelState = NormalizeProjectModelState(result);
        mProjectModelTrusted = string.Equals(mModelState, PROJECT_MODEL_STATE_READY, StringComparison.Ordinal);
        mProjectModelBlocked = string.Equals(mModelState, PROJECT_MODEL_STATE_BLOCKED, StringComparison.Ordinal);
        mProjectModelDrifted = string.Equals(mModelState, PROJECT_MODEL_STATE_STALE, StringComparison.Ordinal);

        AddProjectModelSource(result);
        foreach (var issue in result.Issues)
        {
            var severity = mProjectModelBlocked ? "Error" : "Warning";
            AddIssue(issue.Code, severity, "project-model", issue.Message, issue.Suggestion, issue.EvidencePaths);
        }

        var bundle = result.Bundle;
        if (bundle == null)
        {
            return;
        }

        mProjectModelBundleAvailable = true;
        mProjectModelId = bundle.Manifest.ModelId;
        mProjectModelGeneration = bundle.Manifest.ModelGeneration;
        mProjectModelInputHash = bundle.Manifest.InputHash;
        mPackageName = bundle.Manifest.Package.Name;
        mPackageVersion = bundle.Manifest.Package.Version;
        mPackageRoot = bundle.Manifest.Package.Root;
        foreach (var engineKind in bundle.Capabilities.EngineKinds)
        {
            mProjectModelDeclaredEngineKinds.Add(engineKind);
        }

        if (mProjectModelDeclaredEngineKinds.Count == 0)
        {
            foreach (var engineKind in bundle.Manifest.Project.EngineKinds)
            {
                mProjectModelDeclaredEngineKinds.Add(engineKind);
            }
        }

        foreach (var kit in bundle.Capabilities.Kits)
        {
            mProjectModelDeclaredKitIds.Add(kit.Kit);
            var kitBuilder = GetKit(kit.Kit);
            kitBuilder.ApplyProjectCapability(kit, mProjectModelTrusted);
            if (mProjectModelTrusted && kit.CommandCatalogDeclared)
            {
                mProjectModelCommandKits.Add(kit.Kit);
            }
        }
    }

    /// <summary>
    /// 将 Project Model 结果的异常代码归一为 Catalog 的正式门禁状态。
    /// </summary>
    /// <param name="result">Project Model 检查结果。</param>
    /// <returns>Ready、Missing、Stale、Partial 或 Blocked。</returns>
    private static string NormalizeProjectModelState(ProjectModelResult result)
    {
        if (string.Equals(result.State, PROJECT_MODEL_STATE_MISSING, StringComparison.Ordinal))
        {
            return PROJECT_MODEL_STATE_MISSING;
        }

        if (string.Equals(result.State, PROJECT_MODEL_STATE_STALE, StringComparison.Ordinal))
        {
            return PROJECT_MODEL_STATE_STALE;
        }

        if (string.Equals(result.State, PROJECT_MODEL_STATE_READY, StringComparison.Ordinal))
        {
            return PROJECT_MODEL_STATE_READY;
        }

        if (string.Equals(result.State, PROJECT_MODEL_STATE_BLOCKED, StringComparison.Ordinal)
            || result.Issues.Any(issue => IsBlockingProjectModelCode(issue.Code)))
        {
            return PROJECT_MODEL_STATE_BLOCKED;
        }

        return PROJECT_MODEL_STATE_PARTIAL;
    }

    /// <summary>
    /// 判断 Project Model 问题是否表示结构、哈希、代次或路径完整性失效。
    /// </summary>
    /// <param name="code">稳定问题码。</param>
    /// <returns>需要阻断正式能力消费时返回 true。</returns>
    private static bool IsBlockingProjectModelCode(string code)
    {
        return code.Contains("Invalid", StringComparison.OrdinalIgnoreCase)
            || code.Contains("Hash", StringComparison.OrdinalIgnoreCase)
            || code.Contains("Mismatch", StringComparison.OrdinalIgnoreCase)
            || code.Contains("Conflict", StringComparison.OrdinalIgnoreCase)
            || code.Contains("Blocked", StringComparison.OrdinalIgnoreCase)
            || code.Contains("Escapes", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 记录五文件 Project Model 证据，并保留 manifest 的稳定逻辑路径。
    /// </summary>
    /// <param name="result">Project Model 检查结果。</param>
    private void AddProjectModelSource(ProjectModelResult result)
    {
        foreach (var path in result.EvidencePaths)
        {
            AddEvidence(path);
        }

        var manifestPath = Path.Combine(ProjectRoot, ".yokiframe", "project", ProjectModelContract.PROJECT_MODEL_FILE_NAME);
        mSources.Add(new CapabilityCatalogSource("project-model", manifestPath, mModelState));
        AddEvidence(manifestPath);
    }
}
