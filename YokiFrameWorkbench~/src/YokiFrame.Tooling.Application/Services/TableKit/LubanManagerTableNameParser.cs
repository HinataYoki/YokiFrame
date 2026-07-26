using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using YokiFrame.Tooling.Application.Models.TableKit;

namespace YokiFrame.Tooling.Application.Services.TableKit;

/// <summary>从 Luban 原始 manager 生成源码提取 Loader 实际请求的表资源名。</summary>
internal static class LubanManagerTableNameParser
{
    /// <summary>读取当前生成目标的 manager 构造函数并返回稳定资源名顺序。</summary>
    /// <param name="contract">包含 Luban 输出根、命名空间和 manager 名的生成契约。</param>
    /// <returns>manager 调用 Loader 时使用的字符串资源名。</returns>
    public static IReadOnlyList<string> Parse(TableKitContract contract)
    {
        ArgumentNullException.ThrowIfNull(contract);
        string lubanRoot = LubanProcessService.GetLubanCodeOutputDirectory(contract);
        if (!Directory.Exists(lubanRoot))
        {
            throw new DirectoryNotFoundException("TableKit 找不到 Luban 代码输出目录: " + lubanRoot);
        }

        ClassDeclarationSyntax[] managers = ParseManagerDeclarations(lubanRoot, contract)
            .ToArray();
        if (managers.Length != 1)
        {
            throw new InvalidDataException(
                "TableKit 要求 Luban 输出中存在唯一 manager: " + contract.TablesType);
        }

        ConstructorDeclarationSyntax[] constructors = managers[0].Members
            .OfType<ConstructorDeclarationSyntax>()
            .Where(IsLoaderConstructor)
            .ToArray();
        if (constructors.Length != 1)
        {
            throw new InvalidDataException(
                "TableKit 要求 Luban manager 存在唯一 Func<string, T> Loader 构造函数: " + contract.TablesType);
        }
        return ParseTableNames(constructors[0]);
    }

    /// <summary>解析 Luban 输出中的全部 C# 文件并筛选目标 manager 声明。</summary>
    /// <param name="lubanRoot">Luban 专属代码输出根。</param>
    /// <param name="contract">目标命名空间和 manager 契约。</param>
    /// <returns>匹配完整类型名的类声明。</returns>
    private static IEnumerable<ClassDeclarationSyntax> ParseManagerDeclarations(
        string lubanRoot,
        TableKitContract contract)
    {
        string[] files = Directory.EnumerateFiles(lubanRoot, "*.cs", SearchOption.AllDirectories)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        foreach (string path in files)
        {
            SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
            IEnumerable<ClassDeclarationSyntax> declarations = syntaxTree.GetRoot()
                .DescendantNodes()
                .OfType<ClassDeclarationSyntax>();
            foreach (ClassDeclarationSyntax declaration in declarations)
            {
                if (declaration.Identifier.ValueText != contract.Manager) continue;
                if (string.Equals(GetNamespace(declaration), contract.TopModule, StringComparison.Ordinal))
                {
                    yield return declaration;
                }
            }
        }
    }

    /// <summary>判断构造函数是否接收单个 `Func&lt;string, T&gt;` Loader 参数。</summary>
    /// <param name="constructor">待检查的 manager 构造函数。</param>
    /// <returns>满足 Luban Loader 构造形状时返回 true。</returns>
    private static bool IsLoaderConstructor(ConstructorDeclarationSyntax constructor)
    {
        if (constructor.ParameterList.Parameters.Count != 1) return false;
        TypeSyntax? parameterType = constructor.ParameterList.Parameters[0].Type;
        if (parameterType == null) return false;
        return parameterType.DescendantNodesAndSelf()
            .OfType<GenericNameSyntax>()
            .Any(static generic =>
                generic.Identifier.ValueText == "Func"
                && generic.TypeArgumentList.Arguments.Count == 2
                && generic.TypeArgumentList.Arguments[0] is PredefinedTypeSyntax predefined
                && predefined.Keyword.IsKind(SyntaxKind.StringKeyword));
    }

    /// <summary>按源码顺序读取 Loader 调用中的字符串参数并去重。</summary>
    /// <param name="constructor">已确认形状的 Luban manager 构造函数。</param>
    /// <returns>异步默认 Loader 应预加载的资源名。</returns>
    private static IReadOnlyList<string> ParseTableNames(ConstructorDeclarationSyntax constructor)
    {
        string loaderName = constructor.ParameterList.Parameters[0].Identifier.ValueText;
        InvocationExpressionSyntax[] calls = constructor.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(invocation => invocation.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == loaderName)
            .OrderBy(static invocation => invocation.SpanStart)
            .ToArray();
        List<string> tableNames = new(calls.Length);
        HashSet<string> uniqueNames = new(StringComparer.Ordinal);
        foreach (InvocationExpressionSyntax call in calls)
        {
            string tableName = ReadTableName(call);
            if (uniqueNames.Add(tableName)) tableNames.Add(tableName);
        }
        return tableNames;
    }

    /// <summary>读取一个 Loader 调用的唯一字符串字面量参数。</summary>
    /// <param name="call">manager 构造函数中的 Loader 调用。</param>
    /// <returns>Luban 生成的表资源名。</returns>
    private static string ReadTableName(InvocationExpressionSyntax call)
    {
        SeparatedSyntaxList<ArgumentSyntax> arguments = call.ArgumentList.Arguments;
        if (arguments.Count == 1
            && arguments[0].Expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            string tableName = literal.Token.ValueText;
            if (!string.IsNullOrWhiteSpace(tableName)) return tableName;
        }
        throw new InvalidDataException(
            "TableKit 只支持 Luban manager 中使用非空字符串字面量的 Loader 调用: "
            + call.SyntaxTree.FilePath);
    }

    /// <summary>组合类声明外层的块级或文件级命名空间。</summary>
    /// <param name="declaration">目标 manager 类声明。</param>
    /// <returns>不含 global 前缀的完整命名空间。</returns>
    private static string GetNamespace(ClassDeclarationSyntax declaration)
    {
        List<string> segments = new();
        for (SyntaxNode? current = declaration.Parent; current != null; current = current.Parent)
        {
            if (current is BaseNamespaceDeclarationSyntax namespaceDeclaration)
            {
                segments.Insert(0, namespaceDeclaration.Name.ToString());
            }
        }
        return string.Join(".", segments);
    }
}
