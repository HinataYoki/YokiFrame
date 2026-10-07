#if UNITY_EDITOR || (GODOT && TOOLS) || YOKIFRAME_TOOLING
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using YokiFrame.Json;

namespace YokiFrame
{
    public sealed partial class RoslynRunStore
    {
        /// <summary>把请求索引写成只含 requestId、runId 和创建时间的 JSON。</summary>
        /// <param name="requestId">外部请求标识。</param>
        /// <param name="runId">对应的运行标识。</param>
        /// <returns>索引 JSON。</returns>
        private static string BuildIndexJson(string requestId, string runId)
        {
            return new RoslynJsonBuilder()
                .StartObject()
                .Property("requestId", requestId)
                .Property("runId", runId)
                .Property("createdAtUtc", DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture))
                .EndObject()
                .ToString();
        }

        private static bool TryParseIndex(string json, out string runId)
        {
            runId = string.Empty;
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    if (document.RootElement.TryGetProperty("runId", out JsonElement element)
                        && element.ValueKind == JsonValueKind.String)
                    {
                        runId = element.GetString() ?? string.Empty;
                        return runId.Length > 0;
                    }
                }
            }
            catch (JsonException)
            {
                return false;
            }

            return false;
        }

        private static string BuildRecordJson(RoslynRunRecord record)
        {
            RoslynJsonBuilder builder = new RoslynJsonBuilder()
                .StartObject()
                .Property("runId", record.RunId)
                .Property("requestId", record.RequestId)
                .Property("source", record.Source)
                .Property("entry", record.LegacyEntry)
                .Property("kind", record.Kind)
                .Property("payloadHash", record.PayloadHash)
                .Property("timeoutMs", record.TimeoutMs)
                .Property("graceMs", record.GraceMs)
                .Property("target", record.Target)
                .Property("args", record.ArgsJson)
                .Property("state", record.State.ToString())
                .Property("submittedAtUtc", Format(record.SubmittedAtUtc))
                .Property("startedAtUtc", Format(record.StartedAtUtc))
                .Property("updatedAtUtc", Format(record.UpdatedAtUtc))
                .Property("expiresAtUtc", Format(record.ExpiresAtUtc))
                .Property("ownerSessionId", record.OwnerSessionId)
                .Property("ownerHostId", record.OwnerHostId)
                .Property("generation", record.Generation)
                .Property("attempt", record.Attempt)
                .Property("resultPath", record.ResultPath)
                .Property("errorCode", record.ErrorCode)
                .Property("cancelRequestedAtUtc", Format(record.CancelRequestedAtUtc))
                .Property("timeoutRequestedAtUtc", Format(record.TimeoutRequestedAtUtc))
                .Property("detachedAtUtc", Format(record.DetachedAtUtc))
                .Property("lateCompletionAtUtc", Format(record.LateCompletionAtUtc))
                .Property("lateResultPath", record.LateResultPath)
                .Property("staleContextCalls", record.StaleContextCalls)
                .Property("note", record.Note)
                .Name("steps")
                .StartArray();
            for (var index = 0; index < record.Steps.Count; index++)
            {
                RoslynRunStep step = record.Steps[index];
                builder.StartObject()
                    .Property("name", step.Name)
                    .Property("state", step.State)
                    .Property("atUtc", step.AtUtc)
                    .Property("message", step.Message)
                    .EndObject();
            }

            return builder.EndArray().EndObject().ToString();
        }

        private static bool TryParseRecord(string json, out RoslynRunRecord record)
        {
            record = null;
            try
            {
                using (JsonDocument document = JsonDocument.Parse(json))
                {
                    JsonElement root = document.RootElement;
                    if (root.ValueKind != JsonValueKind.Object)
                    {
                        return false;
                    }

                    var parsed = new RoslynRunRecord
                    {
                        RunId = ReadString(root, "runId"),
                        RequestId = ReadString(root, "requestId"),
                        Source = ReadString(root, "source"),
                LegacyEntry = ReadString(root, "entry"),
                        Kind = ReadString(root, "kind"),
                        PayloadHash = ReadString(root, "payloadHash"),
                        TimeoutMs = (int)ReadLong(root, "timeoutMs"),
                        GraceMs = (int)ReadLong(root, "graceMs"),
                        Target = ReadString(root, "target"),
                        ArgsJson = ReadString(root, "args"),
                        SubmittedAtUtc = ReadDate(root, "submittedAtUtc") ?? DateTime.MinValue,
                        StartedAtUtc = ReadDate(root, "startedAtUtc"),
                        UpdatedAtUtc = ReadDate(root, "updatedAtUtc") ?? DateTime.MinValue,
                        ExpiresAtUtc = ReadDate(root, "expiresAtUtc"),
                        OwnerSessionId = ReadString(root, "ownerSessionId"),
                        OwnerHostId = ReadString(root, "ownerHostId"),
                        Generation = ReadLong(root, "generation"),
                        Attempt = (int)ReadLong(root, "attempt"),
                        ResultPath = ReadString(root, "resultPath"),
                        ErrorCode = ReadString(root, "errorCode"),
                        CancelRequestedAtUtc = ReadDate(root, "cancelRequestedAtUtc"),
                        TimeoutRequestedAtUtc = ReadDate(root, "timeoutRequestedAtUtc"),
                        DetachedAtUtc = ReadDate(root, "detachedAtUtc"),
                        LateCompletionAtUtc = ReadDate(root, "lateCompletionAtUtc"),
                        LateResultPath = ReadString(root, "lateResultPath"),
                        StaleContextCalls = (int)ReadLong(root, "staleContextCalls"),
                        Note = ReadString(root, "note")
                    };

                    if (!Enum.TryParse(ReadString(root, "state"), ignoreCase: false, out RunStatus state))
                    {
                        state = RunStatus.Unknown;
                    }

                    parsed.State = state;
                    if (root.TryGetProperty("steps", out JsonElement steps) && steps.ValueKind == JsonValueKind.Array)
                    {
                        for (var index = 0; index < steps.GetArrayLength(); index++)
                        {
                            JsonElement step = steps[index];
                            parsed.Steps.Add(new RoslynRunStep
                            {
                                Name = ReadString(step, "name"),
                                State = ReadString(step, "state"),
                                AtUtc = ReadString(step, "atUtc"),
                                Message = ReadString(step, "message")
                            });
                        }
                    }

                    record = parsed;
                    return parsed.RunId.Length > 0;
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }

        private static string BuildResultJson(RunResult result)
        {
            RoslynJsonBuilder builder = new RoslynJsonBuilder()
                .StartObject()
                .Property("status", result.Status.ToString())
                .Property("durationMs", result.DurationMs)
                .Property("frames", result.Frames)
                .Property("staleContextCalls", result.StaleContextCalls)
                .Property("exceptionType", result.ExceptionType)
                .Property("exceptionMessage", result.ExceptionMessage)
                .Property("exceptionStack", result.ExceptionStack)
                .Property("note", result.Note)
                .Name("assertions")
                .StartArray();
            IReadOnlyList<RunAssertion> assertions = result.Assertions;
            for (var index = 0; index < assertions.Count; index++)
            {
                builder.StartObject()
                    .Property("name", assertions[index].Name)
                    .Property("passed", assertions[index].Passed)
                    .Property("message", assertions[index].Message)
                    .EndObject();
            }

            builder.EndArray().Name("logs").StartArray();
            IReadOnlyList<string> logs = result.Logs;
            for (var index = 0; index < logs.Count; index++)
            {
                builder.String(logs[index]);
            }

            return builder.EndArray().EndObject().ToString();
        }

        private static string Format(DateTime? value)
        {
            return value.HasValue ? value.Value.ToString("o", CultureInfo.InvariantCulture) : string.Empty;
        }

        private static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : string.Empty;
        }

        private static long ReadLong(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long parsed)
                ? parsed
                : 0L;
        }

        private static DateTime? ReadDate(JsonElement element, string name)
        {
            string text = ReadString(element, name);
            return DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime parsed)
                    ? parsed
                    : (DateTime?)null;
        }

        /// <summary>判断标识是否可安全用作文件名。</summary>
        /// <param name="value">标识。</param>
        /// <returns>安全时返回 true。</returns>
        private static bool IsSafeId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > 128)
            {
                return false;
            }

            for (var index = 0; index < value.Length; index++)
            {
                char c = value[index];
                bool allowed = (c >= '0' && c <= '9')
                    || (c >= 'a' && c <= 'z')
                    || (c >= 'A' && c <= 'Z')
                    || c == '-' || c == '_';
                if (!allowed)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
#endif
