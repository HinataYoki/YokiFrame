#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Globalization;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>把一次运行记录和结果文件投影成命令响应。</summary>
    internal static class RoslynRunJson
    {
        /// <summary>
        /// 写出运行结果。结果文件缺失或不是 JSON 时保留记录字段，并标记 resultUnreadable，
        /// 不把解析失败变成命令失败。
        /// </summary>
        /// <param name="record">运行记录。</param>
        /// <param name="resultJson">结果文件文本，允许空白。</param>
        /// <param name="action">响应里的操作名。</param>
        /// <returns>命令响应 JSON。</returns>
        public static string WriteResult(
            RoslynRunRecord record, string resultJson, string action = "run_result")
        {
            RoslynJsonBuilder builder = WriteRecord(record, action);
            AppendResult(builder, resultJson);
            return builder.EndObject().ToString();
        }

        /// <summary>写出记录头。旧入口没有 kind 时继续用 legacy 标记，不改历史响应字段。</summary>
        /// <param name="record">运行记录。</param>
        /// <param name="action">响应里的操作名。</param>
        /// <returns>尚未结束的 JSON 对象。</returns>
        private static RoslynJsonBuilder WriteRecord(RoslynRunRecord record, string action)
        {
            return new RoslynJsonBuilder().StartObject()
                .Property("operation", action)
                .Property("kind", record.Kind.Length == 0 ? "legacy" : record.Kind)
                .Property("recordSchema", record.Kind.Length == 0 ? "legacy-entry-v4" : "script-v1")
                .Property("legacyEntry", record.LegacyEntry)
                .Property("payloadHash", record.PayloadHash)
                .Property("sessionId", record.OwnerSessionId)
                .Property("engineId", record.OwnerHostId)
                .Property("generation", record.Generation)
                .Property("runId", record.RunId)
                .Property("requestId", record.RequestId)
                .Property("target", record.Target)
                .Property("state", record.State.ToString())
                .Property("terminal", record.IsTerminal)
                .Property("resultPath", record.ResultPath)
                .Property("steps", record.Steps.Count)
                .Property("staleContextCalls", record.StaleContextCalls)
                .Property("cancelRequested", record.CancelRequestedAtUtc.HasValue)
                .Property("timeoutRequested", record.TimeoutRequestedAtUtc.HasValue)
                .Property("lateCompletionAtUtc", Format(record.LateCompletionAtUtc))
                .Property("lateResultPath", record.LateResultPath)
                .Property("note", record.Note);
        }

        /// <summary>附加结果文件。空白直接跳过；JSON 损坏时只标记不可读，不抛出。</summary>
        /// <param name="builder">尚未结束的记录对象。</param>
        /// <param name="resultJson">结果文件文本。</param>
        private static void AppendResult(RoslynJsonBuilder builder, string resultJson)
        {
            if (string.IsNullOrWhiteSpace(resultJson))
            {
                return;
            }

            try
            {
                using (JsonDocument document = JsonDocument.Parse(resultJson))
                {
                    WriteParsedResult(builder, document.RootElement);
                }
            }
            catch (JsonException)
            {
                builder.Property("resultUnreadable", true);
            }
        }

        /// <summary>写出结果状态、断言和日志。缺省数组写成空数组，不省略字段。</summary>
        /// <param name="builder">尚未结束的记录对象。</param>
        /// <param name="root">结果文件根对象。</param>
        private static void WriteParsedResult(RoslynJsonBuilder builder, JsonElement root)
        {
            builder.Property("status", ReadString(root, "status"))
                .Property("durationMs", ReadLong(root, "durationMs"))
                .Property("frames", ReadLong(root, "frames"))
                .Property("exceptionType", ReadString(root, "exceptionType"))
                .Property("exceptionMessage", ReadString(root, "exceptionMessage"))
                .Property("exceptionStack", ReadString(root, "exceptionStack"))
                .Property("detailsJson", ReadString(root, "note"))
                .Name("assertions").StartArray();
            WriteAssertions(builder, root);
            builder.EndArray().Name("logs").StartArray();
            WriteLogs(builder, root);
            builder.EndArray();
        }

        /// <summary>写出断言数组。字段不是数组时保持空数组。</summary>
        /// <param name="builder">已打开的断言数组。</param>
        /// <param name="root">结果文件根对象。</param>
        private static void WriteAssertions(RoslynJsonBuilder builder, JsonElement root)
        {
            if (!root.TryGetProperty("assertions", out JsonElement assertions) || assertions.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            for (int index = 0; index < assertions.GetArrayLength(); index++)
            {
                JsonElement item = assertions[index];
                builder.StartObject().Property("name", ReadString(item, "name"))
                    .Property("passed", item.TryGetProperty("passed", out JsonElement passed) && passed.ValueKind == JsonValueKind.True)
                    .Property("message", ReadString(item, "message")).EndObject();
            }
        }

        /// <summary>写出日志数组。非字符串元素写成空字符串，不中断整份结果。</summary>
        /// <param name="builder">已打开的日志数组。</param>
        /// <param name="root">结果文件根对象。</param>
        private static void WriteLogs(RoslynJsonBuilder builder, JsonElement root)
        {
            if (!root.TryGetProperty("logs", out JsonElement logs) || logs.ValueKind != JsonValueKind.Array)
            {
                return;
            }

            for (int index = 0; index < logs.GetArrayLength(); index++)
            {
                builder.String(logs[index].GetString() ?? string.Empty);
            }
        }

        /// <summary>把可空时间写成 round-trip 文本。没有值时写空字符串，不省略字段。</summary>
        /// <param name="value">UTC 时间。</param>
        /// <returns>文本。</returns>
        private static string Format(DateTime? value) =>
            value.HasValue ? value.Value.ToString("o", CultureInfo.InvariantCulture) : string.Empty;

        /// <summary>读取字符串字段。缺失或类型不对时返回空字符串。</summary>
        /// <param name="root">JSON 对象。</param>
        /// <param name="name">字段名。</param>
        /// <returns>字段文本。</returns>
        private static string ReadString(JsonElement root, string name) =>
            root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty : string.Empty;

        /// <summary>读取整数字段。缺失或不能按 64 位整数解析时返回 0。</summary>
        /// <param name="root">JSON 对象。</param>
        /// <param name="name">字段名。</param>
        /// <returns>字段值。</returns>
        private static long ReadLong(JsonElement root, string name) =>
            root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long number) ? number : 0;
    }
}
#endif
