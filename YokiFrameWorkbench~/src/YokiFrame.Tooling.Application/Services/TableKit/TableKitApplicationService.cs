using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Tooling.Application.Services.TableKit;

/// <summary>提供 Workbench 使用的 TableKit 验证、生成和契约预览用例。</summary>
public sealed class TableKitApplicationService
{
    private readonly LubanConfigParser mConfigParser = new();
    private readonly LubanProcessService mProcessService = new();
    private readonly TableKitCodeGenerationService mCodeGenerationService = new();
    private readonly TableKitResourceLocationResolver mResourceLocationResolver = new();
    /// <summary>创建 TableKit Workbench 用例；Runtime 门面使用统一的表名路径模板 Loader。</summary>
    public TableKitApplicationService()
    {
    }

    /// <summary>只解析当前 Luban 配置，不启动外部进程。</summary>
    /// <param name="options">Workbench TableKit 选项。</param>
    /// <returns>验证结果和动态契约。</returns>
    public TableKitOperationResult Validate(TableKitOptions options)
    {
        try
        {
            TableKitContract contract = mConfigParser.Parse(options);
            TableKitRuntimeLocation location = mResourceLocationResolver.Resolve(options);
            contract = contract with
            {
                IsAddressable = location.IsAddressable,
                RuntimePathPattern = location.PathPattern
            };
            return new TableKitOperationResult { Succeeded = true, Contract = contract, Log = "Luban 配置验证通过。" };
        }
        catch (Exception exception)
        {
            return new TableKitOperationResult { Succeeded = false, Diagnostics = new[] { exception.Message }, Log = exception.ToString() };
        }
    }

    /// <summary>执行 Luban 验证并返回临时 JSON 预览，不修改正式生成目录。</summary>
    /// <param name="options">Workbench TableKit 选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>验证退出码、日志、动态 contract 和预览表。</returns>
    public async Task<TableKitOperationResult> ValidateAsync(TableKitOptions options, CancellationToken cancellationToken = default)
    {
        TableKitOperationResult validation = Validate(options);
        if (!validation.Succeeded || validation.Contract == null) return validation;
        try
        {
            (int exitCode, string log, IReadOnlyList<TableKitPreviewTable> previewTables, string previewDirectory) =
                await mProcessService.ValidateAsync(options, validation.Contract, cancellationToken).ConfigureAwait(false);
            return validation with
            {
                Succeeded = exitCode == 0,
                ExitCode = exitCode,
                Log = log + global::System.Environment.NewLine + "已读取 " + previewTables.Count + " 个临时 JSON 预览。",
                Diagnostics = exitCode == 0 ? Array.Empty<string>() : new[] { "Luban 验证失败，退出码: " + exitCode },
                PreviewTables = previewTables,
                PreviewDirectory = previewDirectory
            };
        }
        catch (Exception exception)
        {
            return validation with { Succeeded = false, Log = exception.ToString(), Diagnostics = new[] { exception.Message } };
        }
    }

    /// <summary>执行 Luban 并直接生成项目侧 TableKit 代码；不写中转 manifest 或 Runtime Settings。</summary>
    /// <param name="options">Workbench TableKit 选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    /// <returns>生成退出码、日志、契约和文件清单。</returns>
    public async Task<TableKitOperationResult> GenerateAsync(TableKitOptions options, CancellationToken cancellationToken = default)
    {
        TableKitOperationResult validation = Validate(options);
        if (!validation.Succeeded || validation.Contract == null) return validation;
        int exitCode;
        string log;
        try
        {
            var processResult = await mProcessService.GenerateAsync(options, validation.Contract, cancellationToken).ConfigureAwait(false);
            exitCode = processResult.ExitCode;
            log = processResult.Log;
        }
        catch (Exception exception)
        {
            return validation with { Succeeded = false, Log = exception.ToString(), Diagnostics = new[] { exception.Message } };
        }
        if (exitCode != 0)
        {
            return validation with { Succeeded = false, ExitCode = exitCode, Log = log, Diagnostics = new[] { "Luban 生成失败，退出码: " + exitCode } };
        }

        try
        {
            TableKitContract contract = validation.Contract;
            IReadOnlyList<string> files = mCodeGenerationService.Generate(options, contract);
            return new TableKitOperationResult
            {
                Succeeded = true,
                ExitCode = exitCode,
                Contract = contract,
                Log = log + global::System.Environment.NewLine + "TableKit 项目代码已直接生成。",
                Files = files
            };
        }
        catch (Exception exception)
        {
            return validation with
            {
                Succeeded = false,
                ExitCode = exitCode,
                Log = log + global::System.Environment.NewLine + exception,
                Diagnostics = new[] { exception.Message }
            };
        }
    }
}
