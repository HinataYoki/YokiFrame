using System.Text.Json.Nodes;
using YokiFrame.Client.FileBridge.Diagnostics;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Protocol.Results;
using YokiFrame.Protocol.Validation;

namespace YokiFrame.Tooling.Application.Capabilities;

/// <summary>
/// 提供能力目录构建器的 JSON 读取、标识累加和证据处理辅助方法。
/// </summary>
internal sealed partial class CapabilityCatalogBuilder
{
    /// <summary>
    /// 解析静态 harness 中的字符串数组，并过滤不安全标识。
    /// </summary>
    /// <param name="node">父 JSON 对象。</param>
    /// <param name="name">数组字段名。</param>
    /// <returns>合法且去重的标识。</returns>
    private IReadOnlyList<string> ReadStringArray(JsonObject? node, string name)
    {
        if (node?[name] is not JsonArray array)
        {
            return Array.Empty<string>();
        }

        List<string> values = new();
        foreach (var item in array)
        {
            var value = ReadString(item);
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            try
            {
                SafeIdValidator.EnsureSafeId(value, name);
                if (!values.Contains(value, StringComparer.Ordinal))
                {
                    values.Add(value);
                }
            }
            catch (YokiFrameProtocolException exception)
            {
                AddIssue(exception.Error.Code, "Warning", "harness", exception.Error.Message, exception.Error.Suggestion, new[] { mHarnessPath });
            }
        }

        return values;
    }

    /// <summary>获取或创建 Kit 累加器。</summary>
    /// <param name="kit">Kit 标识。</param>
    /// <returns>Kit 累加器。</returns>
    private KitBuilder GetKit(string kit)
    {
        if (!mKits.TryGetValue(kit, out var value))
        {
            value = new KitBuilder(kit);
            mKits.Add(kit, value);
        }

        return value;
    }

    /// <summary>加入非空证据路径。</summary>
    /// <param name="path">证据路径。</param>
    private void AddEvidence(string path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            mEvidencePaths.Add(path);
        }
    }

    /// <summary>计算 registry 与 heartbeat 的身份关系。</summary>
    /// <param name="entry">registry 条目。</param>
    /// <param name="heartbeat">heartbeat。</param>
    /// <returns>Match、Missing、Mismatch 或 Invalid。</returns>
    private static string ResolveIdentityState(EngineRegistryEntry entry, HeartbeatInfo? heartbeat)
    {
        if (heartbeat == null)
        {
            return "Missing";
        }

        if (string.IsNullOrWhiteSpace(entry.SessionId)
            || string.IsNullOrWhiteSpace(heartbeat.SessionId)
            || entry.Generation <= 0L
            || heartbeat.Generation <= 0L)
        {
            return "Invalid";
        }

        return string.Equals(entry.SessionId, heartbeat.SessionId, StringComparison.Ordinal)
            && entry.Generation == heartbeat.Generation
            ? "Match"
            : "Mismatch";
    }

    /// <summary>创建带证据路径的能力目录异常。</summary>
    /// <param name="code">错误码。</param>
    /// <param name="message">错误说明。</param>
    /// <param name="suggestion">恢复建议。</param>
    /// <param name="evidencePaths">证据路径。</param>
    /// <returns>协议异常。</returns>
    private static YokiFrameProtocolException CreateCatalogException(
        string code,
        string message,
        string suggestion,
        IReadOnlyList<string> evidencePaths)
    {
        return new YokiFrameProtocolException(new YokiFrameError(code, message, suggestion, evidencePaths));
    }

    /// <summary>读取 JSON 对象字段，不匹配时返回 null。</summary>
    /// <param name="node">父节点。</param>
    /// <param name="name">字段名。</param>
    /// <returns>对象字段。</returns>
    private static JsonObject? ReadObject(JsonObject? node, string name)
    {
        return node?[name] as JsonObject;
    }

    /// <summary>读取字符串字段，不匹配时返回空字符串。</summary>
    /// <param name="node">父节点。</param>
    /// <param name="name">字段名。</param>
    /// <returns>字段文本。</returns>
    private static string ReadString(JsonObject? node, string name)
    {
        return ReadString(node?[name]);
    }

    /// <summary>读取任意 JSON 节点中的字符串。</summary>
    /// <param name="node">待读取节点。</param>
    /// <returns>字段文本。</returns>
    private static string ReadString(JsonNode? node)
    {
        try
        {
            return node?.GetValue<string>() ?? string.Empty;
        }
        catch (InvalidOperationException)
        {
            return string.Empty;
        }
        catch (FormatException)
        {
            return string.Empty;
        }
    }

    /// <summary>读取 Int32 字段，不匹配时返回 0。</summary>
    /// <param name="node">父节点。</param>
    /// <param name="name">字段名。</param>
    /// <returns>整数值。</returns>
    private static int ReadInt32(JsonObject? node, string name)
    {
        try
        {
            return node?[name]?.GetValue<int>() ?? 0;
        }
        catch (InvalidOperationException)
        {
            return 0;
        }
        catch (FormatException)
        {
            return 0;
        }
    }

    /// <summary>读取 Int64 字段，不匹配时返回 0。</summary>
    /// <param name="node">父节点。</param>
    /// <param name="name">字段名。</param>
    /// <returns>长整数值。</returns>
    private static long ReadInt64(JsonObject? node, string name)
    {
        try
        {
            return node?[name]?.GetValue<long>() ?? 0L;
        }
        catch (InvalidOperationException)
        {
            return 0L;
        }
        catch (FormatException)
        {
            return 0L;
        }
    }
}
