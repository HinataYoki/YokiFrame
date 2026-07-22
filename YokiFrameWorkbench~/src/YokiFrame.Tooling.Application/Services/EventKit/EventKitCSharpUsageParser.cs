using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using YokiFrame.Tooling.Application.Models.EventKit.Scan;

namespace YokiFrame.Tooling.Application.Services.EventKit;

/// <summary>使用 Roslyn 语法树和语义模型识别 EventKit 直接调用点。</summary>
internal static class EventKitCSharpUsageParser
{
    internal const string UNKNOWN_PAYLOAD = "Unknown";
    private static readonly HashSet<string> sChannels = new(StringComparer.Ordinal)
        { "Type", "Enum", "String" };
    private static readonly HashSet<string> sActions = new(StringComparer.Ordinal)
        { "Send", "Register", "UnRegister" };

    /// <summary>解析全部语法树并按事件身份聚合源码关系。</summary>
    internal static IReadOnlyList<WorkbenchEventKitCodeRelation> Parse(
        IReadOnlyList<EventKitCodeSourceFile> files,
        IReadOnlyList<MetadataReference> projectReferences,
        ISet<string> matchedFiles,
        CancellationToken cancellationToken)
    {
        CSharpSyntaxTree[] trees = files.Select(static file => file.SyntaxTree).ToArray();
        CSharpCompilation compilation = CreateCompilation(trees, projectReferences);
        Dictionary<string, EventKitCodeScanAggregate> aggregates = new(StringComparer.Ordinal);
        for (var index = 0; index < files.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ParseFile(compilation, files[index], aggregates, matchedFiles, cancellationToken);
        }

        MergeUniquelyInferredPayloads(aggregates);
        return aggregates.Values
            .Select(static aggregate => aggregate.Build())
            .OrderBy(static relation => GetChannelRank(relation.Channel))
            .ThenBy(static relation => relation.EventKey, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static relation => relation.PayloadType, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    /// <summary>创建只用于源码符号解析的轻量编译，不执行 emit 或项目编译。</summary>
    private static CSharpCompilation CreateCompilation(
        IReadOnlyList<CSharpSyntaxTree> trees,
        IReadOnlyList<MetadataReference> projectReferences)
    {
        return CSharpCompilation.Create(
            "YokiFrame.EventKit.CodeScan",
            trees,
            CreatePlatformReferences().Concat(projectReferences),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>从当前 .NET Runtime 可信平台程序集创建基础元数据引用。</summary>
    private static IEnumerable<MetadataReference> CreatePlatformReferences()
    {
        string? trustedAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (string.IsNullOrWhiteSpace(trustedAssemblies))
        {
            yield break;
        }

        foreach (string path in trustedAssemblies.Split(Path.PathSeparator))
        {
            yield return MetadataReference.CreateFromFile(path);
        }
    }

    /// <summary>解析单个文件内的直接 EventKit 调用并记录匹配文件。</summary>
    private static void ParseFile(
        CSharpCompilation compilation,
        EventKitCodeSourceFile file,
        Dictionary<string, EventKitCodeScanAggregate> aggregates,
        ISet<string> matchedFiles,
        CancellationToken cancellationToken)
    {
        SemanticModel semanticModel = compilation.GetSemanticModel(file.SyntaxTree, true);
        SyntaxNode root = file.SyntaxTree.GetRoot(cancellationToken);
        bool matched = false;
        foreach (InvocationExpressionSyntax invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryParseUsage(semanticModel, invocation, out EventKitCodeUsage usage))
            {
                continue;
            }

            matched = true;
            AddUsage(aggregates, file.RelativePath, invocation, usage);
        }

        if (matched)
        {
            matchedFiles.Add(file.RelativePath);
        }
    }

    /// <summary>把调用点转换为 EventKit 通道、动作、事件键和负载类型。</summary>
    private static bool TryParseUsage(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        out EventKitCodeUsage usage)
    {
        usage = default;
        if (!TryGetInvocationParts(
                semanticModel,
                invocation,
                out string channel,
                out string action,
                out TypeArgumentListSyntax? typeArguments))
        {
            return false;
        }

        SeparatedSyntaxList<ArgumentSyntax> arguments = invocation.ArgumentList.Arguments;
        string eventKey;
        string payloadType;
        if (channel == "Type")
        {
            payloadType = ResolveTypePayload(semanticModel, typeArguments, arguments);
            eventKey = payloadType;
        }
        else
        {
            if (arguments.Count == 0)
            {
                return false;
            }

            eventKey = ResolveEventKey(
                semanticModel,
                channel,
                arguments[0].Expression,
                typeArguments);
            payloadType = ResolveKeyedPayload(semanticModel, channel, action, typeArguments, arguments);
        }

        if (string.IsNullOrWhiteSpace(eventKey))
        {
            return false;
        }

        usage = new EventKitCodeUsage(channel, action, eventKey, payloadType);
        return true;
    }

    /// <summary>验证调用表达式严格属于 EventKit.Type/Enum/String 的目标动作。</summary>
    private static bool TryGetInvocationParts(
        SemanticModel semanticModel,
        InvocationExpressionSyntax invocation,
        out string channel,
        out string action,
        out TypeArgumentListSyntax? typeArguments)
    {
        channel = string.Empty;
        action = string.Empty;
        typeArguments = null;
        if (invocation.Expression is not MemberAccessExpressionSyntax actionAccess
            || actionAccess.Expression is not MemberAccessExpressionSyntax channelAccess
            || !IsEventKitRoot(channelAccess.Expression))
        {
            return false;
        }

        channel = channelAccess.Name.Identifier.ValueText;
        action = actionAccess.Name.Identifier.ValueText;
        typeArguments = (actionAccess.Name as GenericNameSyntax)?.TypeArgumentList;
        return sChannels.Contains(channel)
            && sActions.Contains(action)
            && IsYokiFrameEventKitSymbol(semanticModel, channelAccess.Expression);
    }

    /// <summary>拒绝能够解析为其它命名空间同名类型的伪 EventKit；未解析符号保持兼容。</summary>
    private static bool IsYokiFrameEventKitSymbol(SemanticModel semanticModel, ExpressionSyntax expression)
    {
        ISymbol? symbol = semanticModel.GetSymbolInfo(expression).Symbol;
        if (symbol is not INamedTypeSymbol type)
        {
            return true;
        }

        return type.Name == "EventKit"
            && type.ContainingNamespace?.ToDisplayString() == "YokiFrame";
    }

    /// <summary>判断表达式是否为 EventKit 或命名空间限定的 EventKit。</summary>
    private static bool IsEventKitRoot(ExpressionSyntax expression)
    {
        return expression switch
        {
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText == "EventKit",
            MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText == "EventKit",
            _ => false
        };
    }

    /// <summary>解析 Type 通道的泛型或发送表达式负载类型。</summary>
    private static string ResolveTypePayload(
        SemanticModel semanticModel,
        TypeArgumentListSyntax? typeArguments,
        SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        if (typeArguments?.Arguments.Count > 0)
        {
            return ResolveTypeSyntax(semanticModel, typeArguments.Arguments[0]);
        }

        return arguments.Count == 0
            ? UNKNOWN_PAYLOAD
            : ResolveExpressionType(semanticModel, arguments[0].Expression);
    }

    /// <summary>解析 Enum/String 通道在不同重载下的负载类型。</summary>
    private static string ResolveKeyedPayload(
        SemanticModel semanticModel,
        string channel,
        string action,
        TypeArgumentListSyntax? typeArguments,
        SeparatedSyntaxList<ArgumentSyntax> arguments)
    {
        int typeArgumentCount = typeArguments?.Arguments.Count ?? 0;
        if (channel == "Enum" && typeArgumentCount >= 2)
        {
            return ResolveTypeSyntax(semanticModel, typeArguments!.Arguments[1]);
        }

        if (channel == "String" && typeArgumentCount >= 1)
        {
            return ResolveTypeSyntax(semanticModel, typeArguments!.Arguments[0]);
        }

        if (arguments.Count <= 1)
        {
            return string.Empty;
        }

        if (arguments.Count > 2)
        {
            return "System.Object[]";
        }

        ExpressionSyntax value = arguments[1].Expression;
        if (action != "Send")
        {
            string handlerPayload = ResolveHandlerPayload(semanticModel, value);
            if (handlerPayload != UNKNOWN_PAYLOAD)
            {
                return handlerPayload;
            }
        }

        return ResolveExpressionType(semanticModel, value);
    }

    /// <summary>从方法组、Lambda 或委托类型解析监听器参数类型。</summary>
    private static string ResolveHandlerPayload(SemanticModel semanticModel, ExpressionSyntax expression)
    {
        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(expression);
        if (symbolInfo.Symbol is IMethodSymbol method)
        {
            return method.Parameters.Length == 0
                ? string.Empty
                : CanonicalTypeName(method.Parameters[0].Type);
        }

        ITypeSymbol? convertedType = semanticModel.GetTypeInfo(expression).ConvertedType;
        if (convertedType is INamedTypeSymbol { DelegateInvokeMethod: { } invokeMethod })
        {
            return invokeMethod.Parameters.Length == 0
                ? string.Empty
                : CanonicalTypeName(invokeMethod.Parameters[0].Type);
        }

        return UNKNOWN_PAYLOAD;
    }

    /// <summary>解析 Enum 常量或 String 常量的稳定事件键。</summary>
    private static string ResolveEventKey(
        SemanticModel semanticModel,
        string channel,
        ExpressionSyntax expression,
        TypeArgumentListSyntax? typeArguments)
    {
        Optional<object?> constant = semanticModel.GetConstantValue(expression);
        if (channel == "String" && constant.HasValue && constant.Value is string text)
        {
            return text;
        }

        SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(expression);
        if (channel == "Enum" && symbolInfo.Symbol is IFieldSymbol field && field.ContainingType != null)
        {
            return CanonicalTypeName(field.ContainingType) + "." + field.Name;
        }

        if (channel == "Enum")
        {
            string enumType = ResolveEnumType(semanticModel, expression, typeArguments);
            if (!string.IsNullOrWhiteSpace(enumType))
            {
                return enumType;
            }
        }

        return expression.ToString().Trim();
    }

    /// <summary>解析动态枚举表达式的实际类型；显式泛型实参可作为语义模型不完整时的回退。</summary>
    private static string ResolveEnumType(
        SemanticModel semanticModel,
        ExpressionSyntax expression,
        TypeArgumentListSyntax? typeArguments)
    {
        ITypeSymbol? expressionType = semanticModel.GetTypeInfo(expression).Type;
        if (expressionType is { TypeKind: TypeKind.Enum })
        {
            return CanonicalTypeName(expressionType);
        }

        if (typeArguments?.Arguments.Count > 0)
        {
            ITypeSymbol? explicitType = semanticModel.GetTypeInfo(typeArguments.Arguments[0]).Type;
            if (explicitType is { TypeKind: TypeKind.Enum })
            {
                return CanonicalTypeName(explicitType);
            }
        }

        return string.Empty;
    }

    /// <summary>把类型语法解析为与 Runtime FullName 接近的规范名称。</summary>
    private static string ResolveTypeSyntax(SemanticModel semanticModel, TypeSyntax syntax)
    {
        ITypeSymbol? type = semanticModel.GetTypeInfo(syntax).Type;
        return type == null ? syntax.ToString().Trim() : CanonicalTypeName(type);
    }

    /// <summary>把表达式类型解析为规范名称，无法推断时返回 Unknown。</summary>
    private static string ResolveExpressionType(SemanticModel semanticModel, ExpressionSyntax expression)
    {
        ITypeSymbol? type = semanticModel.GetTypeInfo(expression).Type;
        if (type != null && type.TypeKind != TypeKind.Error)
        {
            return CanonicalTypeName(type);
        }

        return expression is ObjectCreationExpressionSyntax creation
            ? creation.Type.ToString().Trim()
            : UNKNOWN_PAYLOAD;
    }

    /// <summary>生成命名空间与嵌套类型均稳定的 Runtime 风格类型名。</summary>
    private static string CanonicalTypeName(ITypeSymbol type)
    {
        if (type is IArrayTypeSymbol array)
        {
            return CanonicalTypeName(array.ElementType) + "[]";
        }

        if (type is not INamedTypeSymbol named)
        {
            return type.ToDisplayString(SymbolDisplayFormat.CSharpErrorMessageFormat);
        }

        Stack<string> names = new();
        for (INamedTypeSymbol? current = named; current != null; current = current.ContainingType)
        {
            names.Push(FormatNamedTypeSegment(current));
        }

        string nestedName = string.Join("+", names);
        string namespaceName = named.ContainingNamespace is { IsGlobalNamespace: false } containingNamespace
            ? containingNamespace.ToDisplayString()
            : string.Empty;
        return string.IsNullOrWhiteSpace(namespaceName)
            ? nestedName
            : namespaceName + "." + nestedName;
    }

    /// <summary>格式化单个命名类型段，并保留闭合泛型实参避免身份碰撞。</summary>
    private static string FormatNamedTypeSegment(INamedTypeSymbol type)
    {
        if (!type.IsGenericType || type.TypeArguments.Length == 0)
        {
            return type.Name;
        }

        string arguments = string.Join(",", type.TypeArguments.Select(CanonicalTypeName));
        return type.Name + "<" + arguments + ">";
    }

    /// <summary>把解析结果追加到稳定身份聚合器。</summary>
    private static void AddUsage(
        Dictionary<string, EventKitCodeScanAggregate> aggregates,
        string relativePath,
        InvocationExpressionSyntax invocation,
        EventKitCodeUsage usage)
    {
        string identity = WorkbenchEventKitCodeRelation.CreateIdentity(
            usage.Channel,
            usage.EventKey,
            usage.PayloadType);
        if (!aggregates.TryGetValue(identity, out EventKitCodeScanAggregate? aggregate))
        {
            aggregate = new EventKitCodeScanAggregate(usage.Channel, usage.EventKey, usage.PayloadType);
            aggregates.Add(identity, aggregate);
        }

        int line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
        aggregate.Add(usage.Kind, new WorkbenchEventKitCodeLocation(relativePath, line));
    }

    /// <summary>把同 channel/key 的唯一未知发送负载合并到唯一已注册负载。</summary>
    private static void MergeUniquelyInferredPayloads(Dictionary<string, EventKitCodeScanAggregate> aggregates)
    {
        EventKitCodeScanAggregate[] unknowns = aggregates.Values
            .Where(static aggregate => aggregate.PayloadType == UNKNOWN_PAYLOAD && aggregate.SendCount > 0)
            .ToArray();
        for (var index = 0; index < unknowns.Length; index++)
        {
            EventKitCodeScanAggregate unknown = unknowns[index];
            EventKitCodeScanAggregate[] candidates = aggregates.Values.Where(candidate =>
                    candidate.Channel == unknown.Channel
                    && candidate.EventKey == unknown.EventKey
                    && candidate.PayloadType != UNKNOWN_PAYLOAD
                    && candidate.RegisterCount > 0)
                .ToArray();
            if (candidates.Length != 1)
            {
                continue;
            }

            candidates[0].Merge(unknown);
            aggregates.Remove(WorkbenchEventKitCodeRelation.CreateIdentity(
                unknown.Channel,
                unknown.EventKey,
                unknown.PayloadType));
        }
    }

    /// <summary>返回通道的稳定显示顺序。</summary>
    private static int GetChannelRank(string channel)
    {
        return channel == "Type" ? 0 : channel == "Enum" ? 1 : channel == "String" ? 2 : 3;
    }

    /// <summary>保存单个调用点解析出的静态事实。</summary>
    private readonly record struct EventKitCodeUsage(
        string Channel,
        string Action,
        string EventKey,
        string PayloadType)
    {
        internal EventKitCodeUsageKind Kind => Action == "Send"
            ? EventKitCodeUsageKind.Send
            : Action == "Register"
                ? EventKitCodeUsageKind.Register
                : EventKitCodeUsageKind.Unregister;
    }
}

/// <summary>保存扫描文件的相对路径和已解析语法树。</summary>
internal sealed class EventKitCodeSourceFile
{
    /// <summary>创建单个已解析源码文件。</summary>
    internal EventKitCodeSourceFile(string relativePath, CSharpSyntaxTree syntaxTree)
    {
        RelativePath = relativePath;
        SyntaxTree = syntaxTree;
    }

    internal string RelativePath { get; }
    internal CSharpSyntaxTree SyntaxTree { get; }
}
