using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Tooling.Application.Services.TableKit;

internal sealed partial class TableKitCodeGenerationService
{
    /// <summary>委托 TableKit Editor 生成器构建统一 Loader 契约。</summary>
    /// <returns>由 CodeGenKit 渲染的 C# 9 源码。</returns>
    private static string BuildLoaderSource()
    {
        return global::YokiFrame.TableKitSourceCodeGenerator.BuildLoaderSource();
    }

    /// <summary>把 Workbench 契约映射给 TableKit Editor 生成器构建门面。</summary>
    /// <param name="options">包含 Editor 数据路径的项目选项。</param>
    /// <param name="contract">包含实际 Luban manager 类型的生成契约。</param>
    /// <returns>由 CodeGenKit 渲染的 C# 9 门面源码。</returns>
    private static string BuildFacadeSource(TableKitOptions options, TableKitContract contract)
    {
        IReadOnlyList<string> tableNames = LubanManagerTableNameParser.Parse(contract);
        return global::YokiFrame.TableKitSourceCodeGenerator.BuildFacadeSource(
            contract.TablesType,
            options.EditorDataPath,
            tableNames);
    }

    /// <summary>把 Luban mapper 契约映射给 TableKit Editor 生成器构建 helper。</summary>
    /// <param name="helperNamespace">constructor 所属命名空间。</param>
    /// <param name="helperTypeName">constructor 所属静态类型名。</param>
    /// <param name="mappings">同一 helper 类型下的 mapper 列表。</param>
    /// <returns>由 CodeGenKit 渲染的 C# 9 helper 源码。</returns>
    private static string BuildExternalTypeHelperSource(
        string helperNamespace,
        string helperTypeName,
        IReadOnlyList<TableKitExternalTypeMapping> mappings)
    {
        global::YokiFrame.TableKitExternalTypeCodeMapping[] codeMappings = mappings
            .Select(static mapping => new global::YokiFrame.TableKitExternalTypeCodeMapping(
                mapping.SourceTypeName,
                mapping.TargetTypeName,
                mapping.HelperMethodName,
                mapping.MemberNames))
            .ToArray();
        return global::YokiFrame.TableKitSourceCodeGenerator.BuildExternalTypeHelperSource(
            helperNamespace,
            helperTypeName,
            codeMappings);
    }
}
