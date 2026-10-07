#if UNITY_EDITOR
using UnityEditor;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// asset_ops：资产刷新、导入、检索与检查；UserAction 级别，只读为主、写操作可回滚。
    /// </summary>
    /// <remarks>
    /// 编辑器正在编译或导入时直接返回 <c>RoslynOperationUnavailable</c>，避免与 Unity 自身的
    /// 资产管线竞争；检索结果有硬上限，超出时返回 truncated 而不是无限增长。
    /// </remarks>
    internal sealed class UnityAssetOpsOperation : IRoslynOperation
    {
        internal const string ACTION = "asset_ops";

        private const string OP_REFRESH = "refresh";
        private const string OP_IMPORT = "import";
        private const string OP_FIND = "find";
        private const string OP_INSPECT = "inspect";
        private const int MAX_FIND_RESULTS = 200;

        /// <summary>创建 asset_ops 操作。</summary>
        internal UnityAssetOpsOperation()
        {
            Descriptor = new RoslynOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.UserAction,
                isDiagnostic: false,
                isCancellation: false,
                targets: RoslynExecutionTarget.Editor);
        }

        /// <summary>获取操作描述。</summary>
        public RoslynOperationDescriptor Descriptor { get; }

        /// <summary>执行一次资产操作。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.UNAVAILABLE,
                    "Unity is compiling or importing; retry asset_ops after the asset pipeline is idle.");
            }

            if (!TryReadPayload(request.PayloadJson, out string op, out JsonDocument document, out string error))
            {
                return YokiFrameCommandResult.Error(RoslynErrorCodes.INVALID_PAYLOAD, error);
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                switch (op)
                {
                    case OP_REFRESH:
                        AssetDatabase.Refresh();
                        return YokiFrameCommandResult.Success(new RoslynJsonBuilder()
                            .StartObject()
                            .Property("operation", ACTION)
                            .Property("op", OP_REFRESH)
                            .Property("completed", true)
                            .EndObject()
                            .ToString());
                    case OP_IMPORT:
                        return Import(root);
                    case OP_FIND:
                        return Find(root);
                    case OP_INSPECT:
                        return Inspect(root);
                    default:
                        return YokiFrameCommandResult.Error(
                            RoslynErrorCodes.INVALID_PAYLOAD,
                            "Unsupported asset_ops op: " + op + ".");
                }
            }
        }

        /// <summary>强制重新导入项目相对路径。路径为空时返回 INVALID_PAYLOAD，不调用导入。</summary>
        /// <param name="root">含 path 的载荷。</param>
        /// <returns>导入后的路径与 guid，或错误结果。</returns>
        private static YokiFrameCommandResult Import(JsonElement root)
        {
            string path = ReadString(root, "path");
            if (path.Length == 0)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "asset_ops import requires a project-relative path.");
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return YokiFrameCommandResult.Success(new RoslynJsonBuilder()
                .StartObject()
                .Property("operation", ACTION)
                .Property("op", OP_IMPORT)
                .Property("path", path)
                .Property("guid", AssetDatabase.AssetPathToGUID(path))
                .EndObject()
                .ToString());
        }

        /// <summary>
        /// 按过滤器检索资产。limit 缺省或超过上限时夹到 200；非正整数直接失败。
        /// 结果只含路径，并用 truncated 表示被截断。
        /// </summary>
        /// <param name="root">含 filter 和可选 limit 的载荷。</param>
        /// <returns>检索结果或错误。</returns>
        private static YokiFrameCommandResult Find(JsonElement root)
        {
            string filter = ReadString(root, "filter");
            if (filter.Length == 0)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "asset_ops find requires a filter, for example 't:Prefab'.");
            }

            int limit = MAX_FIND_RESULTS;
            if (root.TryGetProperty("limit", out JsonElement limitElement)
                && limitElement.ValueKind != JsonValueKind.Null)
            {
                if (!limitElement.TryGetInt32(out int requested) || requested <= 0)
                {
                    return YokiFrameCommandResult.Error(
                        RoslynErrorCodes.INVALID_PAYLOAD,
                        "asset_ops limit must be a positive integer.");
                }

                limit = requested > MAX_FIND_RESULTS ? MAX_FIND_RESULTS : requested;
            }

            string[] guids = AssetDatabase.FindAssets(filter);
            bool truncated = guids.Length > limit;
            int count = truncated ? limit : guids.Length;
            RoslynJsonBuilder builder = new RoslynJsonBuilder()
                .StartObject()
                .Property("operation", ACTION)
                .Property("op", OP_FIND)
                .Property("filter", filter)
                .Property("count", count)
                .Property("total", guids.Length)
                .Property("truncated", truncated)
                .Name("paths")
                .StartArray();
            for (var index = 0; index < count; index++)
            {
                builder.String(AssetDatabase.GUIDToAssetPath(guids[index]));
            }

            return YokiFrameCommandResult.Success(builder.EndArray().EndObject().ToString());
        }

        /// <summary>读取单个资产的 guid、主类型、标签和递归依赖数量。不修改资产。</summary>
        /// <param name="root">含 path 的载荷。</param>
        /// <returns>检查结果；path 为空时返回 INVALID_PAYLOAD。</returns>
        private static YokiFrameCommandResult Inspect(JsonElement root)
        {
            string path = ReadString(root, "path");
            if (path.Length == 0)
            {
                return YokiFrameCommandResult.Error(
                    RoslynErrorCodes.INVALID_PAYLOAD,
                    "asset_ops inspect requires a project-relative path.");
            }

            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            string[] labels = AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(path));
            string[] dependencies = AssetDatabase.GetDependencies(path, recursive: true);
            RoslynJsonBuilder builder = new RoslynJsonBuilder()
                .StartObject()
                .Property("operation", ACTION)
                .Property("op", OP_INSPECT)
                .Property("path", path)
                .Property("guid", AssetDatabase.AssetPathToGUID(path))
                .Property("exists", AssetDatabase.LoadMainAssetAtPath(path) != null)
                .Property("type", type == null ? string.Empty : type.Name)
                .Property("dependencyCount", dependencies.Length)
                .Name("labels")
                .StartArray();
            for (var index = 0; index < labels.Length; index++)
            {
                builder.String(labels[index]);
            }

            return YokiFrameCommandResult.Success(builder.EndArray().EndObject().ToString());
        }

        /// <summary>
        /// 解析 op。空白载荷、非法 JSON、非对象或空 op 都失败；
        /// 失败时释放并清空 document，避免调用方再次释放。
        /// </summary>
        /// <param name="payloadJson">payload JSON。</param>
        /// <param name="op">操作名。</param>
        /// <param name="document">成功时交给调用方释放。</param>
        /// <param name="error">失败说明。</param>
        /// <returns>得到非空 op 时返回 true。</returns>
        private static bool TryReadPayload(
            string payloadJson,
            out string op,
            out JsonDocument document,
            out string error)
        {
            op = string.Empty;
            document = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(payloadJson))
            {
                error = "asset_ops requires an op field.";
                return false;
            }

            try
            {
                document = JsonDocument.Parse(payloadJson);
            }
            catch (JsonException exception)
            {
                error = "asset_ops payload is not valid JSON: " + exception.Message;
                return false;
            }

            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                document.Dispose();
                document = null;
                error = "asset_ops payload must be a JSON object.";
                return false;
            }

            op = ReadString(document.RootElement, "op");
            if (op.Length == 0)
            {
                document.Dispose();
                document = null;
                error = "asset_ops requires a non-empty op field.";
                return false;
            }

            return true;
        }

        /// <summary>读取并修剪字符串字段。缺失、null 或非字符串时返回空串，不抛异常。</summary>
        /// <param name="element">JSON 对象。</param>
        /// <param name="name">字段名。</param>
        /// <returns>修剪后的文本。</returns>
        private static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? (value.GetString() ?? string.Empty).Trim()
                : string.Empty;
        }
    }
}
#endif
