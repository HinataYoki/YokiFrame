using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text;
using YokiFrame.Protocol.Results;

namespace YokiFrame.Cli;

/// <summary>
/// 统一 CLI compact JSON 输出。
/// </summary>
internal static class CliJsonOutput
{
    /// <summary>
    /// 输出成功 JSON 并返回进程退出码。
    /// </summary>
    /// <param name="payload">成功 payload。</param>
    /// <returns>成功退出码。</returns>
    public static int WriteSuccess(JsonObject payload)
    {
        payload["ok"] = true;
        WriteJsonNode(Console.Out, payload);
        return 0;
    }

    /// <summary>
    /// 输出标准错误 JSON 并返回失败退出码。
    /// </summary>
    /// <param name="error">标准错误对象。</param>
    /// <returns>失败退出码。</returns>
    public static int WriteError(YokiFrameError error)
    {
        return WriteError(error, null);
    }

    /// <summary>
    /// 输出标准错误 JSON，并附加命令专属上下文字段。
    /// </summary>
    /// <param name="error">标准错误对象。</param>
    /// <param name="context">可选命令与状态上下文。</param>
    /// <returns>失败退出码。</returns>
    public static int WriteError(YokiFrameError error, JsonObject? context)
    {
        JsonObject payload = new()
        {
            ["ok"] = false,
            ["error"] = error.ToJson()
        };
        if (context != null)
        {
            foreach (var entry in context)
            {
                payload[entry.Key] = entry.Value?.DeepClone();
            }
        }

        WriteJsonNode(Console.Error, payload);
        return 1;
    }

    /// <summary>
    /// 直接写出已构造的 JSON 节点，避免 Native AOT 为节点中的字符串再次查找反射元数据。
    /// </summary>
    /// <param name="output">标准输出或标准错误写入器。</param>
    /// <param name="payload">已完成结构化组装的 JSON 节点。</param>
    private static void WriteJsonNode(TextWriter output, JsonNode payload)
    {
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions { Indented = false }))
        {
            payload.WriteTo(writer, CliJson.CompactOptions);
            writer.Flush();
        }

        output.WriteLine(Encoding.UTF8.GetString(stream.ToArray()));
    }

    /// <summary>
    /// 把任意对象转换成 JSON 节点，便于组合 compact 输出。
    /// </summary>
    /// <param name="value">待转换对象。</param>
    /// <returns>JSON 节点。</returns>
    public static JsonNode ToJsonNode<T>(T value)
    {
        return JsonSerializer.SerializeToNode(value, CliJson.CompactOptions) ?? new JsonObject();
    }
}
