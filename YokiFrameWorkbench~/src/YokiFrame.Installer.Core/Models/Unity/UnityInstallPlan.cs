namespace YokiFrame.Installer.Core.Models;

/// <summary>
/// 描述一次只读 Unity 安装计划及其已验证输入快照。
/// </summary>
public sealed class UnityInstallPlan
{
    /// <summary>
    /// 创建 Unity 安装计划。
    /// </summary>
    /// <param name="request">原始安装请求。</param>
    /// <param name="target">已通过版本门控的 Unity 目标。</param>
    /// <param name="projection">embedded 文件投影；Git 模式为空。</param>
    /// <param name="actions">按执行语义排序的来源互斥动作。</param>
    /// <param name="existingPackageState">计划生成时的现有 embedded 包状态。</param>
    /// <param name="modifiedPaths">现有受管包中检测到的本地修改路径。</param>
    public UnityInstallPlan(
        UnityInstallRequest request,
        InstallerProjectInfo target,
        PackageProjection? projection,
        IReadOnlyList<UnityInstallPlanAction> actions,
        PackageOwnershipState existingPackageState,
        IReadOnlyList<string> modifiedPaths)
    {
        Request = request;
        Target = target;
        Projection = projection;
        Actions = actions;
        ExistingPackageState = existingPackageState;
        ModifiedPaths = modifiedPaths.ToArray();
    }

    /// <summary>
    /// 获取原始安装请求。
    /// </summary>
    public UnityInstallRequest Request { get; }

    /// <summary>
    /// 获取已通过版本门控的 Unity 目标。
    /// </summary>
    public InstallerProjectInfo Target { get; }

    /// <summary>
    /// 获取 embedded 文件投影；Git 模式返回空。
    /// </summary>
    public PackageProjection? Projection { get; }

    /// <summary>
    /// 获取按执行语义排序的来源互斥动作。
    /// </summary>
    public IReadOnlyList<UnityInstallPlanAction> Actions { get; }

    /// <summary>
    /// 获取计划生成时的现有 embedded 包状态，用于入口展示替换影响而不是重复扫描目录。
    /// </summary>
    public PackageOwnershipState ExistingPackageState { get; }

    /// <summary>
    /// 获取现有受管包中稳定排序的本地修改路径快照。
    /// </summary>
    public IReadOnlyList<string> ModifiedPaths { get; }
}
