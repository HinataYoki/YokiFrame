namespace YokiFrame.Tooling.Application.Models.EventKit;

/// <summary>集中生成 EventKit Workbench read model 使用的紧凑显示名称。</summary>
public static class WorkbenchEventKitDisplayName
{
    /// <summary>
    /// 按通道生成事件短名；Type 移除命名空间和外层类型，Enum 额外保留成员名。
    /// </summary>
    /// <param name="channel">Type、Enum 或 String 通道。</param>
    /// <param name="eventKey">协议中的完整事件键。</param>
    /// <returns>适合紧凑界面显示的事件键。</returns>
    public static string CreateEventKey(string channel, string eventKey)
    {
        if (string.IsNullOrWhiteSpace(eventKey)
            || string.Equals(channel, "String", StringComparison.Ordinal))
        {
            return eventKey;
        }

        if (!string.Equals(channel, "Enum", StringComparison.Ordinal))
        {
            return ShortenTypeName(eventKey);
        }

        int memberSeparator = FindLastTopLevelDot(eventKey);
        return memberSeparator < 0
            ? ShortenTypeName(eventKey)
            : ShortenTypeName(eventKey[..memberSeparator]) + eventKey[memberSeparator..];
    }

    /// <summary>
    /// 生成不含命名空间和外层声明类型的负载短名。
    /// </summary>
    /// <param name="payloadType">协议中的完整负载类型。</param>
    /// <param name="emptyText">没有负载时使用的文本。</param>
    /// <returns>适合紧凑界面显示的负载文本。</returns>
    public static string CreatePayload(string payloadType, string emptyText)
    {
        return string.IsNullOrWhiteSpace(payloadType)
            ? emptyText
            : ShortenTypeName(payloadType);
    }

    /// <summary>
    /// 生成时间线详情；处理器只显示方法名，否则显示短负载名。
    /// </summary>
    /// <param name="handler">协议中的完整处理器名称。</param>
    /// <param name="payloadType">协议中的完整负载类型。</param>
    /// <returns>适合时间线列宽的详情文本。</returns>
    public static string CreateActivityDetail(string handler, string payloadType)
    {
        if (!string.IsNullOrWhiteSpace(handler))
        {
            int separator = handler.LastIndexOf('.');
            return separator < 0 ? handler : handler[(separator + 1)..];
        }

        return CreatePayload(payloadType, "无参数");
    }

    /// <summary>递归移除类型命名空间和外层声明类型，并保留泛型参数与数组形态。</summary>
    private static string ShortenTypeName(string typeName)
    {
        string value = typeName.Trim();
        if (value.EndsWith("[]", StringComparison.Ordinal))
        {
            return ShortenTypeName(value[..^2]) + "[]";
        }

        int genericStart = value.IndexOf('<');
        int genericEnd = genericStart < 0 ? -1 : FindMatchingGenericEnd(value, genericStart);
        if (genericStart < 0 || genericEnd < 0)
        {
            return ShortenSimpleTypeName(value);
        }

        string[] arguments = SplitGenericArguments(value[(genericStart + 1)..genericEnd]);
        for (var index = 0; index < arguments.Length; index++)
        {
            arguments[index] = ShortenTypeName(arguments[index]);
        }

        return ShortenSimpleTypeName(value[..genericStart])
            + "<" + string.Join(", ", arguments) + ">"
            + value[(genericEnd + 1)..];
    }

    /// <summary>从普通类型名中移除 global 前缀、命名空间和外层声明类型。</summary>
    private static string ShortenSimpleTypeName(string typeName)
    {
        string value = typeName.StartsWith("global::", StringComparison.Ordinal)
            ? typeName[8..]
            : typeName;
        int separator = Math.Max(value.LastIndexOf('+'), value.LastIndexOf('.'));
        return separator < 0 ? value : value[(separator + 1)..];
    }

    /// <summary>查找不位于泛型参数中的最后一个成员分隔点。</summary>
    private static int FindLastTopLevelDot(string value)
    {
        var depth = 0;
        for (var index = value.Length - 1; index >= 0; index--)
        {
            if (value[index] == '>') depth++;
            else if (value[index] == '<') depth--;
            else if (value[index] == '.' && depth == 0) return index;
        }

        return -1;
    }

    /// <summary>查找指定泛型左尖括号对应的右尖括号。</summary>
    private static int FindMatchingGenericEnd(string value, int genericStart)
    {
        var depth = 0;
        for (var index = genericStart; index < value.Length; index++)
        {
            if (value[index] == '<') depth++;
            else if (value[index] == '>' && --depth == 0) return index;
        }

        return -1;
    }

    /// <summary>按顶层逗号拆分泛型参数，避免破坏嵌套泛型。</summary>
    private static string[] SplitGenericArguments(string arguments)
    {
        List<string> result = new();
        var depth = 0;
        var start = 0;
        for (var index = 0; index < arguments.Length; index++)
        {
            if (arguments[index] == '<') depth++;
            else if (arguments[index] == '>') depth--;
            else if (arguments[index] == ',' && depth == 0)
            {
                result.Add(arguments[start..index].Trim());
                start = index + 1;
            }
        }

        result.Add(arguments[start..].Trim());
        return result.ToArray();
    }
}
