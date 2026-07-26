using System.Text.Json;

namespace YokiFrame.Tooling.Application.Models.Architecture;

/// <summary>
/// 把 Runtime Architecture 工作台 payload 转换为稳定强类型 read model。
/// </summary>
internal static class WorkbenchArchitectureStateParser
{
    /// <summary>
    /// 解析完整 Architecture payload；无效输入转换为空状态并保留 stale 原因。
    /// </summary>
    internal static WorkbenchArchitectureState Parse(WorkbenchArchitectureDataSource dataSource)
    {
        ArgumentNullException.ThrowIfNull(dataSource);
        if (string.IsNullOrWhiteSpace(dataSource.RawPayloadJson))
        {
            return CreateEmpty(dataSource, "Architecture payload is empty.");
        }

        try
        {
            using var document = JsonDocument.Parse(dataSource.RawPayloadJson);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("architectures", out var architecturesElement)
                || architecturesElement.ValueKind != JsonValueKind.Array)
            {
                return CreateEmpty(dataSource, "Architecture payload must contain an architectures array.");
            }

            return ParseRoot(root, architecturesElement, dataSource);
        }
        catch (JsonException exception)
        {
            return CreateEmpty(dataSource, "Architecture payload is invalid JSON: " + exception.Message);
        }
    }

    /// <summary>解析已确认有效的 payload 根和实例数组。</summary>
    private static WorkbenchArchitectureState ParseRoot(
        JsonElement root,
        JsonElement architecturesElement,
        WorkbenchArchitectureDataSource dataSource)
    {
        var architectures = ReadArchitectures(architecturesElement);
        var stats = root.TryGetProperty("stats", out var statsElement)
            && statsElement.ValueKind == JsonValueKind.Object
                ? statsElement
                : default;
        var aliveCount = architectures.Count(static item => item.IsAlive);
        var serviceCount = architectures.Sum(static item => item.Services.Count);
        return new WorkbenchArchitectureState(
            dataSource,
            ReadInt64(stats, "diagnosticVersion"),
            ReadInt32(root, "count", architectures.Count),
            ReadInt32(stats, "aliveCount", aliveCount),
            ReadInt32(stats, "serviceCount", serviceCount),
            architectures);
    }

    /// <summary>读取全部 Architecture 实例并忽略非对象条目。</summary>
    private static IReadOnlyList<WorkbenchArchitectureInstance> ReadArchitectures(JsonElement array)
    {
        List<WorkbenchArchitectureInstance> architectures = new();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                architectures.Add(ReadArchitecture(element));
            }
        }

        return architectures;
    }

    /// <summary>读取一个 Architecture 实例与其注册服务。</summary>
    private static WorkbenchArchitectureInstance ReadArchitecture(JsonElement element)
    {
        var services = ReadServices(element);
        return new WorkbenchArchitectureInstance(
            ReadString(element, "typeName"),
            ReadString(element, "fullName"),
            ReadString(element, "createdAtUtc"),
            ReadInt32(element, "instanceHash"),
            ReadBoolean(element, "isAlive"),
            ReadBoolean(element, "initialized"),
            ReadInt32(element, "serviceCount", services.Count),
            services);
    }

    /// <summary>读取一个 Architecture 的服务列表。</summary>
    private static IReadOnlyList<WorkbenchArchitectureService> ReadServices(JsonElement architecture)
    {
        if (!architecture.TryGetProperty("services", out var array)
            || array.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<WorkbenchArchitectureService>();
        }

        List<WorkbenchArchitectureService> services = new();
        foreach (var element in array.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                services.Add(ReadService(element));
            }
        }

        return services;
    }

    /// <summary>读取一个服务契约与实现。</summary>
    private static WorkbenchArchitectureService ReadService(JsonElement element)
    {
        return new WorkbenchArchitectureService(
            ReadString(element, "typeName"),
            ReadString(element, "fullName"),
            ReadString(element, "implementationTypeName"),
            ReadString(element, "implementationFullName"),
            ReadBoolean(element, "initialized"),
            ReadInt32(element, "instanceHash"));
    }

    /// <summary>创建保留来源证据的安全空状态。</summary>
    private static WorkbenchArchitectureState CreateEmpty(
        WorkbenchArchitectureDataSource dataSource,
        string reason)
    {
        var staleReason = string.IsNullOrWhiteSpace(dataSource.StaleReason)
            ? reason
            : dataSource.StaleReason + " " + reason;
        return new WorkbenchArchitectureState(
            dataSource.WithStaleReason(staleReason),
            0L,
            0,
            0,
            0,
            Array.Empty<WorkbenchArchitectureInstance>());
    }

    /// <summary>安全读取字符串属性。</summary>
    private static string ReadString(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
    }

    /// <summary>安全读取整数属性。</summary>
    private static int ReadInt32(JsonElement parent, string name, int defaultValue = 0)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.TryGetInt32(out var result)
                ? result
                : defaultValue;
    }

    /// <summary>安全读取长整数属性。</summary>
    private static long ReadInt64(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.TryGetInt64(out var result)
                ? result
                : 0L;
    }

    /// <summary>安全读取布尔属性。</summary>
    private static bool ReadBoolean(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out var value)
            && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            && value.GetBoolean();
    }
}
