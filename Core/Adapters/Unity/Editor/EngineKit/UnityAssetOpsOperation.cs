#if UNITY_EDITOR
using UnityEditor;
using YokiFrame.Json;

namespace YokiFrame
{
    /// <summary>
    /// asset_ops：资产刷新、导入、检索与检查；UserAction 级别，只读为主、写操作可回滚。
    /// </summary>
    /// <remarks>
    /// 编辑器正在编译或导入时直接返回 <c>EngineOperationUnavailable</c>，避免与 Unity 自身的
    /// 资产管线竞争；检索结果有硬上限，超出时返回 truncated 而不是无限增长。
    /// </remarks>
    internal sealed class UnityAssetOpsOperation : IYokiFrameEngineOperation
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
            Descriptor = new YokiFrameEngineOperationDescriptor(
                ACTION,
                YokiFrameCommandKind.UserAction,
                isDiagnostic: false,
                isCancellation: false,
                targets: YokiFrameEngineExecutionTarget.Editor);
        }

        /// <summary>获取操作描述。</summary>
        public YokiFrameEngineOperationDescriptor Descriptor { get; }

        /// <summary>执行一次资产操作。</summary>
        /// <param name="request">命令请求。</param>
        /// <returns>命令结果。</returns>
        public YokiFrameCommandResult Execute(YokiFrameCommandRequest request)
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.UNAVAILABLE,
                    "Unity is compiling or importing; retry asset_ops after the asset pipeline is idle.");
            }

            if (!TryReadPayload(request.PayloadJson, out string op, out JsonDocument document, out string error))
            {
                return YokiFrameCommandResult.Error(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, error);
            }

            using (document)
            {
                JsonElement root = document.RootElement;
                switch (op)
                {
                    case OP_REFRESH:
                        AssetDatabase.Refresh();
                        return YokiFrameCommandResult.Success(new YokiFrameEngineJsonBuilder()
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
                            YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                            "Unsupported asset_ops op: " + op + ".");
                }
            }
        }

        private static YokiFrameCommandResult Import(JsonElement root)
        {
            string path = ReadString(root, "path");
            if (path.Length == 0)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                    "asset_ops import requires a project-relative path.");
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            return YokiFrameCommandResult.Success(new YokiFrameEngineJsonBuilder()
                .StartObject()
                .Property("operation", ACTION)
                .Property("op", OP_IMPORT)
                .Property("path", path)
                .Property("guid", AssetDatabase.AssetPathToGUID(path))
                .EndObject()
                .ToString());
        }

        private static YokiFrameCommandResult Find(JsonElement root)
        {
            string filter = ReadString(root, "filter");
            if (filter.Length == 0)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                    "asset_ops find requires a filter, for example 't:Prefab'.");
            }

            int limit = MAX_FIND_RESULTS;
            if (root.TryGetProperty("limit", out JsonElement limitElement)
                && limitElement.ValueKind != JsonValueKind.Null)
            {
                if (!limitElement.TryGetInt32(out int requested) || requested <= 0)
                {
                    return YokiFrameCommandResult.Error(
                        YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                        "asset_ops limit must be a positive integer.");
                }

                limit = requested > MAX_FIND_RESULTS ? MAX_FIND_RESULTS : requested;
            }

            string[] guids = AssetDatabase.FindAssets(filter);
            bool truncated = guids.Length > limit;
            int count = truncated ? limit : guids.Length;
            YokiFrameEngineJsonBuilder builder = new YokiFrameEngineJsonBuilder()
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

        private static YokiFrameCommandResult Inspect(JsonElement root)
        {
            string path = ReadString(root, "path");
            if (path.Length == 0)
            {
                return YokiFrameCommandResult.Error(
                    YokiFrameEngineErrorCodes.INVALID_PAYLOAD,
                    "asset_ops inspect requires a project-relative path.");
            }

            var type = AssetDatabase.GetMainAssetTypeAtPath(path);
            string[] labels = AssetDatabase.GetLabels(AssetDatabase.LoadMainAssetAtPath(path));
            string[] dependencies = AssetDatabase.GetDependencies(path, recursive: true);
            YokiFrameEngineJsonBuilder builder = new YokiFrameEngineJsonBuilder()
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

        private static string ReadString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
                ? (value.GetString() ?? string.Empty).Trim()
                : string.Empty;
        }
    }
}
#endif
