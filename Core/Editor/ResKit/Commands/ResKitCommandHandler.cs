#if UNITY_EDITOR || (GODOT && TOOLS)
using System;

namespace YokiFrame
{
    /// <summary>提供 ResKit 有界状态、分页查询和显式诊断设置命令。</summary>
    public sealed class ResKitCommandHandler : YokiFrameKitCommandHandler
    {
        private const string KIT = "ResKit";
        private const string STATS = "stats";
        private const string GET_WORKBENCH_SNAPSHOT = "get_workbench_snapshot";
        private const string LIST_RESOURCES = "list_resources";
        private const string GET_RESOURCE_DETAIL = "get_resource_detail";
        private const string DIAGNOSE_RESOURCE = "diagnose_resource";
        private const string GET_UNLOAD_HISTORY = "get_unload_history";
        private const string CLEAR_HISTORY = "clear_history";
        private const string SET_TRACKING = "set_tracking";
        private const string TRACKING_FIELD = "loadLocationTrackingEnabled";
        private static readonly string[] sSupportedActions =
        {
            STATS,
            GET_WORKBENCH_SNAPSHOT,
            LIST_RESOURCES,
            GET_RESOURCE_DETAIL,
            DIAGNOSE_RESOURCE,
            GET_UNLOAD_HISTORY,
            CLEAR_HISTORY,
            SET_TRACKING
        };

        /// <summary>创建覆盖 ResKit 八个受控诊断 action 的 handler。</summary>
        public ResKitCommandHandler() : base(KIT, sSupportedActions)
        {
        }

        /// <summary>创建当前唯一 ResKit/state 的有界 JSON。</summary>
        /// <returns>不超过 Shared Memory 默认 payload 上限的状态 JSON。</returns>
        public string CreateWorkbenchSnapshot() => ResKitJsonWriter.WriteState();

        /// <summary>执行匹配 action，并把输入或查询错误转换为 terminal result。</summary>
        protected override YokiFrameCommandResult HandleAction(YokiFrameCommandRequest request)
        {
            try
            {
                if (request.Action == STATS) return WriteStats();
                if (request.Action == GET_WORKBENCH_SNAPSHOT) return Success(CreateWorkbenchSnapshot());
                if (request.Action == LIST_RESOURCES) return ListResources(request.PayloadJson);
                if (request.Action == GET_RESOURCE_DETAIL) return GetResourceDetail(request.PayloadJson);
                if (request.Action == DIAGNOSE_RESOURCE) return DiagnoseResource(request.PayloadJson);
                if (request.Action == GET_UNLOAD_HISTORY) return GetUnloadHistory(request.PayloadJson);
                if (request.Action == CLEAR_HISTORY) return ClearHistory();
                return SetTracking(request.PayloadJson);
            }
            catch (ArgumentException exception)
            {
                return YokiFrameCommandResult.Error("InvalidPayload", exception.Message);
            }
            catch (Exception exception)
            {
                return YokiFrameCommandResult.Error("ResKitCommandFailed", exception.Message);
            }
        }

        /// <summary>返回不含明细的原子聚合状态。</summary>
        private static YokiFrameCommandResult WriteStats()
        {
            ResKitDiagnosticSnapshot snapshot = ResKit.CaptureDiagnosticSnapshot(0, 0);
            return Success(ResKitJsonWriter.WriteStats(snapshot));
        }

        /// <summary>返回已稳定排序的资源页，并拒绝过期 expectedVersion。</summary>
        private static YokiFrameCommandResult ListResources(string payloadJson)
        {
            PageRequest page = ParsePage(payloadJson);
            ResKitDiagnosticSnapshot snapshot = ResKit.CaptureDiagnosticSnapshot(int.MaxValue, 0);
            YokiFrameCommandResult versionError = ValidateVersion(page, snapshot.Version);
            if (versionError != null) return versionError;
            return Success(ResKitJsonWriter.WriteResourcePage(snapshot, page.Offset, page.Limit));
        }

        /// <summary>按路径和可选类型查询唯一资源，类型缺失且存在歧义时明确失败。</summary>
        private static YokiFrameCommandResult GetResourceDetail(string payloadJson)
        {
            ResourceQuery query = ParseResourceQuery(payloadJson);
            ResKitDiagnosticSnapshot snapshot = ResKit.CaptureDiagnosticSnapshot(
                int.MaxValue, 0, ResKitJsonWriter.MAX_DETAIL_SOURCES);
            YokiFrameCommandResult versionError = ValidateVersion(query.ExpectedVersion, snapshot.Version);
            if (versionError != null) return versionError;
            YokiFrameCommandResult findError = FindResource(snapshot, query, out ResDebugInfo resource);
            if (findError != null) return findError;
            return Success(ResKitJsonWriter.WriteResourceDetail(snapshot, resource));
        }

        /// <summary>组合当前资源和相关卸载历史，供显式故障诊断使用。</summary>
        private static YokiFrameCommandResult DiagnoseResource(string payloadJson)
        {
            ResourceQuery query = ParseResourceQuery(payloadJson);
            ResKitDiagnosticSnapshot snapshot = ResKit.CaptureDiagnosticSnapshot(
                int.MaxValue, ResKit.MAX_UNLOAD_HISTORY, ResKitJsonWriter.MAX_DETAIL_SOURCES);
            YokiFrameCommandResult versionError = ValidateVersion(query.ExpectedVersion, snapshot.Version);
            if (versionError != null) return versionError;
            YokiFrameCommandResult findError = FindResource(snapshot, query, out ResDebugInfo resource, true);
            if (findError != null) return findError;
            ResUnloadRecord latest = FindLatestUnload(snapshot, query, out int relatedCount);
            return Success(ResKitJsonWriter.WriteDiagnosis(
                snapshot, query.Path, query.TypeName, resource, latest, relatedCount));
        }

        /// <summary>返回最新优先的卸载历史页和固定环覆盖计数。</summary>
        private static YokiFrameCommandResult GetUnloadHistory(string payloadJson)
        {
            PageRequest page = ParsePage(payloadJson);
            ResKitDiagnosticSnapshot snapshot = ResKit.CaptureDiagnosticSnapshot(
                0, ResKit.MAX_UNLOAD_HISTORY);
            YokiFrameCommandResult versionError = ValidateVersion(page, snapshot.Version);
            if (versionError != null) return versionError;
            return Success(ResKitJsonWriter.WriteHistoryPage(snapshot, page.Offset, page.Limit));
        }

        /// <summary>清空卸载历史并返回更新后的完整 state。</summary>
        private YokiFrameCommandResult ClearHistory()
        {
            ResKit.ClearUnloadHistory();
            return Success(CreateWorkbenchSnapshot());
        }

        /// <summary>严格读取唯一布尔字段后切换加载位置采集。</summary>
        private YokiFrameCommandResult SetTracking(string payloadJson)
        {
            if (!TryParseTrackingPayload(payloadJson, out bool enabled))
            {
                throw new ArgumentException("ResKit set_tracking requires one loadLocationTrackingEnabled boolean.");
            }

            ResKit.EnableLoadLocationTracking = enabled;
            return Success(CreateWorkbenchSnapshot());
        }

        /// <summary>解析分页参数并应用固定页上限。</summary>
        private static PageRequest ParsePage(string payloadJson)
        {
            int offset = 0;
            int limit = ResKitJsonWriter.MAX_PAGE_SIZE;
            long? expectedVersion = null;
            if (JsonHelper.TryExtractInt(payloadJson, "offset", out int parsedOffset)) offset = parsedOffset;
            if (JsonHelper.TryExtractInt(payloadJson, "limit", out int parsedLimit)) limit = parsedLimit;
            if (JsonHelper.TryExtractLong(payloadJson, "expectedVersion", out long parsedVersion))
            {
                expectedVersion = parsedVersion;
            }

            if (offset < 0) throw new ArgumentException("offset must be greater than or equal to zero.");
            if (limit <= 0 || limit > ResKitJsonWriter.MAX_PAGE_SIZE)
            {
                throw new ArgumentException("limit must be between 1 and " + ResKitJsonWriter.MAX_PAGE_SIZE + ".");
            }

            return new PageRequest(offset, limit, expectedVersion);
        }

        /// <summary>解析资源路径、可选类型和可选诊断版本。</summary>
        private static ResourceQuery ParseResourceQuery(string payloadJson)
        {
            string path = JsonHelper.ExtractString(payloadJson, "path");
            string typeName = JsonHelper.ExtractString(payloadJson, "typeName");
            long? expectedVersion = null;
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("ResKit resource query requires path.");
            if (JsonHelper.TryExtractLong(payloadJson, "expectedVersion", out long parsedVersion))
            {
                expectedVersion = parsedVersion;
            }

            return new ResourceQuery(path, typeName ?? string.Empty, expectedVersion);
        }

        /// <summary>查找唯一资源；诊断模式允许资源当前未加载。</summary>
        private static YokiFrameCommandResult FindResource(
            ResKitDiagnosticSnapshot snapshot,
            ResourceQuery query,
            out ResDebugInfo result,
            bool allowMissing = false)
        {
            result = null;
            for (var index = 0; index < snapshot.Resources.Length; index++)
            {
                ResDebugInfo candidate = snapshot.Resources[index];
                if (!Matches(candidate, query)) continue;
                if (result != null) return YokiFrameCommandResult.Error(
                    "AmbiguousResource", "Multiple resource types match the requested path; provide typeName.");
                result = candidate;
            }

            return result != null || allowMissing
                ? null
                : YokiFrameCommandResult.Error("ResourceNotFound", "The requested resource is not loaded.");
        }

        /// <summary>返回相关最新卸载记录并统计历史命中总量。</summary>
        private static ResUnloadRecord FindLatestUnload(
            ResKitDiagnosticSnapshot snapshot,
            ResourceQuery query,
            out int relatedCount)
        {
            relatedCount = 0;
            ResUnloadRecord latest = null;
            for (var index = 0; index < snapshot.History.Length; index++)
            {
                ResUnloadRecord item = snapshot.History[index];
                if (!Matches(item.Path, item.TypeName, query)) continue;
                relatedCount++;
                if (latest == null) latest = item;
            }

            return latest;
        }

        /// <summary>判断资源路径和完整/简单类型名是否匹配查询。</summary>
        private static bool Matches(ResDebugInfo resource, ResourceQuery query)
        {
            return Matches(resource.Path, resource.TypeName, query);
        }

        /// <summary>判断给定路径和类型是否匹配查询，兼容旧版简单类型名。</summary>
        private static bool Matches(string path, string typeName, ResourceQuery query)
        {
            if (!string.Equals(path, query.Path, StringComparison.Ordinal)) return false;
            if (string.IsNullOrEmpty(query.TypeName)) return true;
            if (string.Equals(typeName, query.TypeName, StringComparison.Ordinal)) return true;
            return typeName != null && typeName.EndsWith("." + query.TypeName, StringComparison.Ordinal);
        }

        /// <summary>校验分页请求是否仍对应同一诊断版本。</summary>
        private static YokiFrameCommandResult ValidateVersion(PageRequest request, long actualVersion)
        {
            return ValidateVersion(request.ExpectedVersion, actualVersion);
        }

        /// <summary>诊断版本变化时返回显式 StateChanged，避免拼接不同状态的分页结果。</summary>
        private static YokiFrameCommandResult ValidateVersion(long? expectedVersion, long actualVersion)
        {
            return expectedVersion.HasValue && expectedVersion.Value != actualVersion
                ? StateChanged()
                : null;
        }

        /// <summary>创建统一状态变化错误，要求调用方重新开始详情或分页查询。</summary>
        private static YokiFrameCommandResult StateChanged()
        {
            return YokiFrameCommandResult.Error("StateChanged", "ResKit state changed; restart the query.");
        }

        /// <summary>严格解析只含唯一顶层布尔字段的 set_tracking payload。</summary>
        private static bool TryParseTrackingPayload(string json, out bool enabled)
        {
            enabled = false;
            if (string.IsNullOrWhiteSpace(json)) return false;
            var index = 0;
            SkipJsonWhitespace(json, ref index);
            if (!TryConsumeJsonToken(json, ref index, "{")) return false;
            SkipJsonWhitespace(json, ref index);
            if (!TryConsumeJsonToken(json, ref index, "\"" + TRACKING_FIELD + "\"")) return false;
            SkipJsonWhitespace(json, ref index);
            if (!TryConsumeJsonToken(json, ref index, ":")) return false;
            SkipJsonWhitespace(json, ref index);
            if (TryConsumeJsonToken(json, ref index, "true")) enabled = true;
            else if (!TryConsumeJsonToken(json, ref index, "false")) return false;
            SkipJsonWhitespace(json, ref index);
            if (!TryConsumeJsonToken(json, ref index, "}")) return false;
            SkipJsonWhitespace(json, ref index);
            return index == json.Length;
        }

        /// <summary>从当前位置消费固定 JSON token，失败时不移动索引。</summary>
        private static bool TryConsumeJsonToken(string json, ref int index, string token)
        {
            if (index < 0 || token.Length > json.Length - index) return false;
            for (var tokenIndex = 0; tokenIndex < token.Length; tokenIndex++)
            {
                if (json[index + tokenIndex] != token[tokenIndex]) return false;
            }

            index += token.Length;
            return true;
        }

        /// <summary>跳过 JSON 标准允许的四类空白字符。</summary>
        private static void SkipJsonWhitespace(string json, ref int index)
        {
            while (index < json.Length)
            {
                char value = json[index];
                if (value != ' ' && value != '\t' && value != '\r' && value != '\n') return;
                index++;
            }
        }

        /// <summary>创建成功 terminal result。</summary>
        private static YokiFrameCommandResult Success(string json) => YokiFrameCommandResult.Success(json);

        /// <summary>保存分页边界和可选一致性版本。</summary>
        private readonly struct PageRequest
        {
            /// <summary>创建分页请求。</summary>
            internal PageRequest(int offset, int limit, long? expectedVersion)
            {
                Offset = offset; Limit = limit; ExpectedVersion = expectedVersion;
            }

            internal int Offset { get; }
            internal int Limit { get; }
            internal long? ExpectedVersion { get; }
        }

        /// <summary>保存资源唯一键查询与可选一致性版本。</summary>
        private readonly struct ResourceQuery
        {
            /// <summary>创建资源查询。</summary>
            internal ResourceQuery(string path, string typeName, long? expectedVersion)
            {
                Path = path; TypeName = typeName; ExpectedVersion = expectedVersion;
            }

            internal string Path { get; }
            internal string TypeName { get; }
            internal long? ExpectedVersion { get; }
        }
    }
}
#endif
