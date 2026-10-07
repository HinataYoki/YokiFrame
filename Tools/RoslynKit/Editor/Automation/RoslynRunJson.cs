#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Globalization;
using YokiFrame.Json;

namespace YokiFrame
{
    internal static class RoslynRunJson
    {
        public static string WriteResult(
            RoslynRunRecord record, string resultJson, string action = "run_result")
        {
            var builder = new RoslynJsonBuilder().StartObject()
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
            if (!string.IsNullOrWhiteSpace(resultJson))
            {
                try
                {
                    using (JsonDocument document = JsonDocument.Parse(resultJson))
                    {
                        JsonElement root = document.RootElement;
                        builder.Property("status", ReadString(root, "status"))
                            .Property("durationMs", ReadLong(root, "durationMs"))
                            .Property("frames", ReadLong(root, "frames"))
                            .Property("exceptionType", ReadString(root, "exceptionType"))
                            .Property("exceptionMessage", ReadString(root, "exceptionMessage"))
                            .Property("exceptionStack", ReadString(root, "exceptionStack"))
                            .Property("detailsJson", ReadString(root, "note"))
                            .Name("assertions").StartArray();
                        if (root.TryGetProperty("assertions", out var assertions) && assertions.ValueKind == JsonValueKind.Array)
                        {
                            for (int i = 0; i < assertions.GetArrayLength(); i++)
                            {
                                var item = assertions[i];
                                builder.StartObject().Property("name", ReadString(item, "name"))
                                    .Property("passed", item.TryGetProperty("passed", out var passed) && passed.ValueKind == JsonValueKind.True)
                                    .Property("message", ReadString(item, "message")).EndObject();
                            }
                        }
                        builder.EndArray().Name("logs").StartArray();
                        if (root.TryGetProperty("logs", out var logs) && logs.ValueKind == JsonValueKind.Array)
                        {
                            for (int i = 0; i < logs.GetArrayLength(); i++) builder.String(logs[i].GetString() ?? string.Empty);
                        }
                        builder.EndArray();
                    }
                }
                catch (JsonException) { builder.Property("resultUnreadable", true); }
            }
            return builder.EndObject().ToString();
        }

        private static string Format(DateTime? value) =>
            value.HasValue ? value.Value.ToString("o", CultureInfo.InvariantCulture) : string.Empty;
        private static string ReadString(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty : string.Empty;
        private static long ReadLong(JsonElement root, string name) =>
            root.TryGetProperty(name, out var value) && value.TryGetInt64(out long number) ? number : 0;
    }
}
#endif
