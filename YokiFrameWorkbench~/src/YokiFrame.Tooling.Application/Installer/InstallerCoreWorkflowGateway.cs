using YokiFrame.Installer.Core.Models;
using YokiFrame.Installer.Core.Services;

namespace YokiFrame.Tooling.Application.Installer;

/// <summary>
/// 把 Application 安装选项映射到 Unity/Godot typed Core 服务，并返回统一预览与结果。
/// </summary>
public sealed partial class InstallerCoreWorkflowGateway : IInstallerWorkflowGateway
{
    private const string UNITY_ROLLBACK_INCOMPLETE_TEXT = "rollback was incomplete";

    private readonly UnityInstallService mUnityInstallService = new();
    private readonly GodotInstallService mGodotInstallService = new();
    private readonly IGodotProjectBuildService mGodotProjectBuildService;

    /// <summary>
    /// 创建使用默认 Godot 主项目构建器的安装 gateway。
    /// </summary>
    public InstallerCoreWorkflowGateway()
        : this(new GodotProjectBuildService())
    {
    }

    /// <summary>
    /// 创建可替换 Godot 主项目构建边界的安装 gateway，供测试隔离外部 dotnet 进程。
    /// </summary>
    /// <param name="godotProjectBuildService">Godot 主项目构建服务。</param>
    internal InstallerCoreWorkflowGateway(IGodotProjectBuildService godotProjectBuildService)
    {
        mGodotProjectBuildService = godotProjectBuildService
            ?? throw new ArgumentNullException(nameof(godotProjectBuildService));
    }

    /// <summary>
    /// 在后台执行 Core 只读计划，并把 typed plan 映射为 Application 预览。
    /// </summary>
    /// <param name="options">安装输入。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>不暴露 Core 类型的安装预览。</returns>
    public async Task<InstallerPlanPreview> CreatePlanAsync(
        InstallerInstallOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        try
        {
            return await Task.Run(() => CreatePlan(options), cancellationToken).ConfigureAwait(false);
        }
        catch (PackageInstallRejectedException exception)
        {
            throw CreateConflict(exception);
        }
        catch (UnsupportedKitReferenceException exception)
        {
            throw CreateConflict(exception);
        }
    }

    /// <summary>
    /// 执行预览内部保存的 typed Core 请求，并统一上报应用、校验和回滚阶段。
    /// </summary>
    /// <param name="options">生成预览时使用的安装输入。</param>
    /// <param name="plan">携带内部执行令牌的安装预览。</param>
    /// <param name="progress">执行进度接收器。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>不暴露 Core 类型的统一执行结果。</returns>
    public async Task<InstallerExecutionResult> ExecuteAsync(
        InstallerInstallOptions options,
        InstallerPlanPreview plan,
        IProgress<InstallerProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(progress);
        ValidateExecutionToken(options, plan);
        progress.Report(new InstallerProgressUpdate(InstallerProgressStage.Applying, 0, 2, "正在执行受控安装事务。"));
        try
        {
            var result = await ExecuteCoreAsync(
                plan.ExecutionToken!,
                progress,
                cancellationToken).ConfigureAwait(false);
            var verifyingMessage = options.Mode == InstallerInstallMode.GodotLocal
                ? "Godot add-on 与主项目已完成提交后校验。"
                : "Core 已完成提交后校验。";
            progress.Report(new InstallerProgressUpdate(InstallerProgressStage.Verifying, 2, 2, verifyingMessage));
            return result;
        }
        catch (PackageInstallRejectedException exception)
        {
            throw CreateConflict(exception);
        }
        catch (UnsupportedKitReferenceException exception)
        {
            throw CreateConflict(exception);
        }
        catch (PackageInstallTransactionException exception)
        {
            ReportRollback(progress, exception.RollbackSucceeded);
            throw CreateExecutionFailure(exception, exception.RollbackSucceeded, exception.DiagnosticEvidencePath);
        }
        catch (GodotInstallException exception)
        {
            ReportRollback(progress, exception.RollbackSucceeded);
            throw CreateExecutionFailure(exception, exception.RollbackSucceeded, exception.DiagnosticEvidencePath);
        }
        catch (IOException exception) when (exception.Message.Contains(
            UNITY_ROLLBACK_INCOMPLETE_TEXT,
            StringComparison.OrdinalIgnoreCase))
        {
            ReportRollback(progress, rollbackSucceeded: false);
            throw CreateExecutionFailure(exception, rollbackSucceeded: false, null);
        }
    }

    /// <summary>
    /// 根据安装模式调用对应 typed Core 计划服务。
    /// </summary>
    /// <param name="options">安装输入。</param>
    /// <returns>携带内部执行令牌的统一预览。</returns>
    private InstallerPlanPreview CreatePlan(InstallerInstallOptions options)
    {
        return options.Mode switch
        {
            InstallerInstallMode.UnityLocal or InstallerInstallMode.UnityGit => CreateUnityPlan(options),
            InstallerInstallMode.GodotLocal => CreateGodotPlan(options),
            _ => throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Unsupported installer mode.")
        };
    }

    /// <summary>
    /// 创建 Unity typed request/plan 并映射来源互斥动作。
    /// </summary>
    /// <param name="options">Unity 本地或 Git 安装输入。</param>
    /// <returns>Unity 安装预览。</returns>
    private InstallerPlanPreview CreateUnityPlan(InstallerInstallOptions options)
    {
        var request = CreateUnityRequest(options);
        var typedPlan = mUnityInstallService.CreatePlan(request);
        var actions = typedPlan.Actions.Select(MapUnityAction).ToArray();
        UnityExecutionToken token = new(request, typedPlan);
        return new InstallerPlanPreview(
            InstallerTargetKind.Unity,
            options.Mode,
            options.Mode == InstallerInstallMode.UnityGit ? options.GitUrl! : typedPlan.Request.SourcePackageRoot,
            typedPlan.Target.ProjectRoot,
            typedPlan.Target.PackageRoot,
            actions,
            CreateUnityWarnings(typedPlan),
            token);
    }

    /// <summary>
    /// 把 Unity 受管包修改转换为非阻断替换提示，避免将既定整包事务误报为冲突。
    /// </summary>
    /// <param name="plan">Core Unity 计划。</param>
    /// <returns>供 UI 与 CLI 审阅的替换警告。</returns>
    private static IReadOnlyList<string> CreateUnityWarnings(UnityInstallPlan plan)
    {
        if (plan.ExistingPackageState != PackageOwnershipState.Modified)
        {
            return Array.Empty<string>();
        }

        return new[]
        {
            "检测到现有受管包有 " + plan.ModifiedPaths.Count
            + " 项本地修改。安装会完整替换该目录；执行前自动备份，失败时恢复原包。"
        };
    }

    /// <summary>
    /// 创建 Godot typed request/plan 并映射包和外层 owner 动作。
    /// </summary>
    /// <param name="options">Godot 本地安装输入。</param>
    /// <returns>Godot 安装预览。</returns>
    private InstallerPlanPreview CreateGodotPlan(InstallerInstallOptions options)
    {
        var request = CreateGodotRequest(options);
        var typedPlan = mGodotInstallService.CreatePlan(request);
        GodotExecutionToken token = new(request, typedPlan);
        return new InstallerPlanPreview(
            InstallerTargetKind.Godot,
            options.Mode,
            typedPlan.SourcePackageRoot,
            typedPlan.ProjectRoot,
            typedPlan.AddonRoot,
            CreateGodotActions(typedPlan),
            CreateGodotWarnings(typedPlan),
            token);
    }

    /// <summary>
    /// 把 Application Unity 选项集中映射为 Core typed request。
    /// </summary>
    /// <param name="options">Unity 安装输入。</param>
    /// <returns>Core Unity 请求。</returns>
    private static UnityInstallRequest CreateUnityRequest(InstallerInstallOptions options)
    {
        var mode = options.Mode == InstallerInstallMode.UnityGit
            ? UnityInstallMode.GitUrl
            : UnityInstallMode.Embedded;
        return new UnityInstallRequest(
            options.SourcePackageRoot ?? string.Empty,
            options.TargetProjectRoot,
            options.RuntimeProfile,
            mode,
            options.GitUrl,
            MapLegacyPolicy(options.LegacyPackagePolicy));
    }

    /// <summary>
    /// 把 Application Godot 选项集中映射为 Core typed request，隔离并行扩展的参数变化。
    /// </summary>
    /// <param name="options">Godot 安装输入。</param>
    /// <returns>Core Godot 请求。</returns>
    private static GodotInstallRequest CreateGodotRequest(InstallerInstallOptions options)
    {
        var godotOptions = options.GodotOptions
            ?? throw new InvalidOperationException("Godot install options are missing.");
        return new GodotInstallRequest(
            options.SourcePackageRoot ?? throw new InvalidOperationException("Godot source package root is missing."),
            options.TargetProjectRoot,
            options.RuntimeProfile,
            godotOptions.RepairProjectSettings,
            godotOptions.EnablePlugin,
            MapLegacyPolicy(options.LegacyPackagePolicy));
    }

    /// <summary>
    /// 把 Unity typed plan action 映射为 Application 统一动作。
    /// </summary>
    /// <param name="action">Core Unity 动作。</param>
    /// <returns>Application 动作预览。</returns>
    private static InstallerPlanActionPreview MapUnityAction(UnityInstallPlanAction action)
    {
        var kind = action.Kind switch
        {
            UnityInstallPlanActionKind.InstallEmbeddedPackage => InstallerPlanActionKind.InstallPackage,
            UnityInstallPlanActionKind.RemoveEmbeddedPackage => InstallerPlanActionKind.RemovePackage,
            UnityInstallPlanActionKind.SetEmbeddedDependency => InstallerPlanActionKind.SetEmbeddedDependency,
            UnityInstallPlanActionKind.SetGitDependency => InstallerPlanActionKind.SetGitDependency,
            _ => throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "Unsupported Unity action.")
        };
        return new InstallerPlanActionPreview(kind, action.TargetPath, action.Value, action.Reason);
    }

    /// <summary>
    /// 根据 Godot typed plan 创建完整 add-on 替换和外部项目 owner 动作。
    /// </summary>
    /// <param name="plan">Core Godot 计划。</param>
    /// <returns>按提交语义排序的统一动作。</returns>
    private static IReadOnlyList<InstallerPlanActionPreview> CreateGodotActions(GodotInstallPlan plan)
    {
        List<InstallerPlanActionPreview> actions = new()
        {
            CreateAction(InstallerPlanActionKind.InstallPackage, plan.AddonRoot, "完整替换受控 Godot add-on。"),
            CreateAction(
                InstallerPlanActionKind.PatchProjectFile,
                plan.ProjectFilePath,
                plan.ProjectFileWasGenerated
                    ? "生成并维护 Godot 主项目及 YokiFrame ProjectReference。"
                    : "维护现有 Godot 主项目及 YokiFrame ProjectReference。")
        };
        if (plan.RepairProjectSettings)
        {
            actions.Add(CreateAction(InstallerPlanActionKind.PatchProjectSettings, plan.ProjectSettingsPath, "维护 YokiFrame project.godot owner 项。"));
        }

        return actions;
    }

    /// <summary>
    /// 根据 Godot 开关生成不会阻止执行的预览警告。
    /// </summary>
    /// <param name="plan">Core Godot 计划。</param>
    /// <returns>预览警告。</returns>
    private static IReadOnlyList<string> CreateGodotWarnings(GodotInstallPlan plan)
    {
        List<string> warnings = new();
        if (plan.ProjectFileWasGenerated)
        {
            warnings.Add("目标是空的 Godot .NET 项目；安装事务会根据 project.godot 生成主 .csproj，并在失败时删除该新文件。");
        }

        if (!plan.RepairProjectSettings)
        {
            warnings.Add("project.godot repair is disabled; existing project settings will remain unchanged.");
        }
        else if (!plan.EnablePlugin)
        {
            warnings.Add("Godot editor plugin registration is disabled; runtime owner settings remain managed.");
        }

        return warnings;
    }

    /// <summary>
    /// 创建不携带目标值的统一动作。
    /// </summary>
    /// <param name="kind">动作类型。</param>
    /// <param name="targetPath">目标路径。</param>
    /// <param name="description">动作说明。</param>
    /// <returns>动作预览。</returns>
    private static InstallerPlanActionPreview CreateAction(
        InstallerPlanActionKind kind,
        string targetPath,
        string description)
    {
        return new InstallerPlanActionPreview(kind, targetPath, null, description);
    }

    /// <summary>
    /// 在后台执行 token 对应的 typed Core 服务并映射成功结果。
    /// </summary>
    /// <param name="executionToken">Unity 或 Godot typed token。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>Application 执行结果。</returns>
    private async Task<InstallerExecutionResult> ExecuteCoreAsync(
        object executionToken,
        IProgress<InstallerProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        return executionToken switch
        {
            UnityExecutionToken unity => await Task.Run(
                () => MapUnityResult(mUnityInstallService.Execute(unity.Request, cancellationToken)),
                CancellationToken.None).ConfigureAwait(false),
            GodotExecutionToken godot => await ExecuteGodotAsync(
                godot,
                progress,
                cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException("Installer plan does not contain a supported execution token.")
        };
    }

    /// <summary>
    /// 执行 Godot Core 事务，并在提交后确保主项目程序集已经可被 Editor 加载。
    /// </summary>
    /// <param name="executionToken">Godot typed 请求和计划。</param>
    /// <param name="progress">安装进度接收器。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>包含 Godot owner 证据的统一结果。</returns>
    private async Task<InstallerExecutionResult> ExecuteGodotAsync(
        GodotExecutionToken executionToken,
        IProgress<InstallerProgressUpdate> progress,
        CancellationToken cancellationToken)
    {
        using var projectLock = InstallerProjectLock.Acquire(executionToken.Request.ProjectRoot);
        var result = await Task.Run(
            () => MapGodotResult(
                executionToken.Plan,
                mGodotInstallService.Execute(executionToken.Request, projectLock, cancellationToken)),
            CancellationToken.None).ConfigureAwait(false);
        if (GodotProjectBuildService.NeedsBuild(executionToken.Plan))
        {
            progress.Report(new InstallerProgressUpdate(
                InstallerProgressStage.Applying,
                1,
                2,
                "正在构建 Godot 主项目程序集。"));
            try
            {
                await mGodotProjectBuildService.BuildIfRequiredAsync(
                    executionToken.Plan,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return MarkCommittedNeedsVerification(result, exception);
            }
        }

        // Godot 在重新扫描托管程序集时可能暂时移除失败插件的 enabled 项；构建成功后以最新文件内容重做 owner patch。
        try
        {
            mGodotInstallService.EnsurePluginEnabled(executionToken.Plan, projectLock);
        }
        catch (Exception exception)
        {
            return MarkCommittedNeedsVerification(result, exception);
        }

        return result;
    }

    /// <summary>
    /// 保留 Core 已提交结果，同时明确宿主 post-verify 仍需人工或重试确认。
    /// </summary>
    /// <param name="result">已经提交的 Godot 结果。</param>
    /// <param name="exception">构建或 owner patch 异常。</param>
    /// <returns>带待验证状态的统一结果。</returns>
    private static InstallerExecutionResult MarkCommittedNeedsVerification(
        InstallerExecutionResult result,
        Exception exception)
    {
        return new InstallerExecutionResult(
            result.TargetPath,
            result.Changed,
            result.ReplacedExistingPackage,
            result.EvidencePaths,
            committedNeedsVerification: true,
            verificationError: exception.Message);
    }

    /// <summary>
    /// 把 Unity typed result 映射为包根或 manifest 语义，不伪造 Git 包事务。
    /// </summary>
    /// <param name="result">Core Unity 结果。</param>
    /// <returns>Application 执行结果。</returns>
    private static InstallerExecutionResult MapUnityResult(UnityInstallResult result)
    {
        if (result.Plan.Request.Mode == UnityInstallMode.Embedded)
        {
            var transaction = result.PackageTransaction
                ?? throw new InvalidOperationException("Unity embedded result is missing its package transaction.");
            List<string> evidence = new() { transaction.OwnerManifestPath };
            AddManifestEvidence(result, evidence);
            return new InstallerExecutionResult(
                transaction.TargetPackageRoot,
                changed: true,
                transaction.ReplacedExistingPackage,
                evidence);
        }

        var manifestPath = FindUnityManifestPath(result.Plan);
        var removedPackage = result.Plan.Actions.Any(static action => action.Kind == UnityInstallPlanActionKind.RemoveEmbeddedPackage);
        return new InstallerExecutionResult(
            manifestPath,
            result.ManifestChanged || removedPackage,
            removedPackage,
            new[] { manifestPath });
    }

    /// <summary>
    /// 把 Godot 包事务和外层 owner 文件映射为统一执行证据。
    /// </summary>
    /// <param name="plan">实际执行的 Godot typed plan。</param>
    /// <param name="result">Core Godot 结果。</param>
    /// <returns>Application 执行结果。</returns>
    private static InstallerExecutionResult MapGodotResult(GodotInstallPlan plan, GodotInstallResult result)
    {
        List<string> evidence = new()
        {
            result.PackageResult.OwnerManifestPath,
            result.ProjectFilePath,
            result.PluginConfigPath,
            result.PluginScriptPath,
            result.PluginScriptUidPath,
            result.RuntimeBootstrapPath,
            result.RuntimeBootstrapUidPath
        };
        if (plan.RepairProjectSettings)
        {
            evidence.Add(result.ProjectSettingsPath);
        }

        return new InstallerExecutionResult(
            result.PackageResult.TargetPackageRoot,
            changed: true,
            result.PackageResult.ReplacedExistingPackage,
            evidence);
    }

    /// <summary>
    /// 在 embedded 模式确实改写 manifest 时追加成功证据。
    /// </summary>
    /// <param name="result">Core Unity 结果。</param>
    /// <param name="evidence">待补充证据列表。</param>
    private static void AddManifestEvidence(UnityInstallResult result, ICollection<string> evidence)
    {
        if (result.ManifestChanged)
        {
            evidence.Add(FindUnityManifestPath(result.Plan));
        }
    }

    /// <summary>
    /// 从 typed Unity plan 动作或项目根解析 manifest 路径。
    /// </summary>
    /// <param name="plan">Core Unity 计划。</param>
    /// <returns>Packages/manifest.json 路径。</returns>
    private static string FindUnityManifestPath(UnityInstallPlan plan)
    {
        var actionPath = plan.Actions
            .FirstOrDefault(static action => action.Kind is UnityInstallPlanActionKind.SetEmbeddedDependency
                or UnityInstallPlanActionKind.SetGitDependency)
            ?.TargetPath;
        return actionPath ?? Path.Combine(plan.Target.ProjectRoot, "Packages", "manifest.json");
    }

    /// <summary>
    /// 把 Core 所有权拒绝转换为 Application 冲突。
    /// </summary>
    /// <param name="exception">Core 拒绝异常。</param>
    /// <returns>Application 冲突异常。</returns>
    private static InstallerConflictException CreateConflict(PackageInstallRejectedException exception)
    {
        return new InstallerConflictException(exception.Message, exception.ConflictPaths);
    }

    /// <summary>
    /// 把 Core 的未迁移 Kit 引用拒绝转换为 Application 冲突，并保留脚本位置。
    /// </summary>
    /// <param name="exception">Core Kit 引用拒绝异常。</param>
    /// <returns>Application 冲突异常。</returns>
    private static InstallerConflictException CreateConflict(UnsupportedKitReferenceException exception)
    {
        return new InstallerConflictException(exception.Message, exception.ConflictPaths);
    }

    /// <summary>
    /// 把 Core 事务失败转换为 Application 执行异常。
    /// </summary>
    /// <param name="exception">原始 Core 异常。</param>
    /// <param name="rollbackSucceeded">回滚结果。</param>
    /// <param name="evidencePath">可选诊断证据。</param>
    /// <returns>Application 执行异常。</returns>
    private static InstallerExecutionException CreateExecutionFailure(
        Exception exception,
        bool rollbackSucceeded,
        string? evidencePath)
    {
        var evidence = evidencePath == null ? Array.Empty<string>() : new[] { evidencePath };
        return new InstallerExecutionException(exception.Message, rollbackSucceeded, evidence, exception);
    }

    /// <summary>
    /// 向 Application 状态机上报 Core 已执行的回滚结果。
    /// </summary>
    /// <param name="progress">进度接收器。</param>
    /// <param name="rollbackSucceeded">回滚结果。</param>
    private static void ReportRollback(IProgress<InstallerProgressUpdate> progress, bool rollbackSucceeded)
    {
        var message = rollbackSucceeded ? "安装失败，Core 已完成回滚。" : "安装失败，Core 回滚未完整完成。";
        progress.Report(new InstallerProgressUpdate(InstallerProgressStage.RollingBack, 1, 1, message));
    }

    /// <summary>
    /// 保存 Unity typed 请求与只读计划，避免执行时依赖公开 DTO 反向拼装。
    /// </summary>
    /// <param name="Request">Core Unity 请求。</param>
    /// <param name="Plan">Core Unity 计划。</param>
    private sealed record UnityExecutionToken(UnityInstallRequest Request, UnityInstallPlan Plan);

    /// <summary>
    /// 保存 Godot typed 请求与只读计划，集中隔离 Core API 变化。
    /// </summary>
    /// <param name="Request">Core Godot 请求。</param>
    /// <param name="Plan">Core Godot 计划。</param>
    private sealed record GodotExecutionToken(GodotInstallRequest Request, GodotInstallPlan Plan);
}
