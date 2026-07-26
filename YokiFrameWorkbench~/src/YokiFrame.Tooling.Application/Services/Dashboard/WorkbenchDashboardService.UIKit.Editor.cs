using System.Text.Json;
using System.Text.Json.Nodes;
using YokiFrame.Protocol.Common;
using YokiFrame.Protocol.Validation;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.UIKit;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>承载 Unity UIKit Editor Tools 的强类型 Application 用例。</summary>
public sealed partial class WorkbenchDashboardService
{
    /// <summary>
    /// 执行一个显式 UIKit Editor action，并在成功变更后回读最新 Unity 选择上下文。
    /// </summary>
    public async Task<WorkbenchUIKitEditorResult> ExecuteUIKitEditorActionAsync(
        string engineId,
        WorkbenchUIKitEditorAction action,
        WorkbenchUIKitPanelGenerationRequest? request,
        CancellationToken cancellationToken)
    {
        if (!IsUnityEditorEngine(engineId))
        {
            return CreateUIKitEditorError(action, "UIKit Editor Tools 只支持 unity-editor。");
        }

        string commandAction = GetUIKitEditorCommandAction(action);
        string payload = CreateUIKitEditorPayload(action, request);
        WorkbenchCommandState command = await SendCommandAsync(
            engineId,
            UI_KIT,
            commandAction,
            payload,
            cancellationToken).ConfigureAwait(false);
        if (!command.Ok)
            return CreateUIKitEditorError(action, command.ErrorMessage);

        WorkbenchUIKitEditorResult parsed = ParseUIKitEditorResult(action, command.ResultJson);
        if (action == WorkbenchUIKitEditorAction.RefreshContext) return parsed;
        WorkbenchUIKitEditorContext? context = await ReadUIKitEditorContextAsync(
            engineId,
            cancellationToken).ConfigureAwait(false);
        return new WorkbenchUIKitEditorResult
        {
            Succeeded = parsed.Succeeded,
            Action = parsed.Action,
            Message = parsed.Message,
            AffectedCount = parsed.AffectedCount,
            PrefabPath = parsed.PrefabPath,
            PanelScriptPath = parsed.PanelScriptPath,
            DesignerScriptPath = parsed.DesignerScriptPath,
            Context = context,
        };
    }

    /// <summary>发送只读 editor context 查询并转换为强类型模型。</summary>
    private async Task<WorkbenchUIKitEditorContext?> ReadUIKitEditorContextAsync(
        string engineId,
        CancellationToken cancellationToken)
    {
        WorkbenchCommandState command = await SendCommandAsync(
            engineId,
            UI_KIT,
            "get_editor_context",
            "{}",
            cancellationToken).ConfigureAwait(false);
        return command.Ok ? ParseUIKitEditorContext(command.ResultJson) : null;
    }

    /// <summary>把 action 映射为 Provider 稳定命令名。</summary>
    private static string GetUIKitEditorCommandAction(WorkbenchUIKitEditorAction action)
    {
        return action switch
        {
            WorkbenchUIKitEditorAction.RefreshContext => "get_editor_context",
            WorkbenchUIKitEditorAction.CreatePanelPrefab => "create_panel_prefab",
            WorkbenchUIKitEditorAction.GenerateCodeForSelection => "generate_code_for_selection",
            WorkbenchUIKitEditorAction.AddBindToSelection => "add_bind_to_selection",
            WorkbenchUIKitEditorAction.RemoveBindFromSelection => "remove_bind_from_selection",
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, "未知 UIKit Editor action。")
        };
    }

    /// <summary>从强类型请求构造精确扁平 JSON payload。</summary>
    private static string CreateUIKitEditorPayload(
        WorkbenchUIKitEditorAction action,
        WorkbenchUIKitPanelGenerationRequest? request)
    {
        if (action == WorkbenchUIKitEditorAction.RefreshContext)
            return "{}";

        if (action == WorkbenchUIKitEditorAction.AddBindToSelection
            || action == WorkbenchUIKitEditorAction.RemoveBindFromSelection)
        {
            JsonObject selectionPayload = new();
            AddSelectionContext(selectionPayload, request);
            return selectionPayload.ToJsonString(YokiFrameJson.CompactOptions);
        }

        if (request == null) throw new ArgumentNullException(nameof(request));
        JsonObject payload = new()
        {
            ["panelName"] = request.PanelName,
            ["prefabFolder"] = request.PrefabFolder,
            ["scriptFolder"] = request.ScriptFolder,
            ["scriptNamespace"] = request.ScriptNamespace,
            ["assemblyName"] = request.AssemblyName,
            ["codeTemplate"] = request.CodeTemplate,
        };
        if (action == WorkbenchUIKitEditorAction.GenerateCodeForSelection)
            AddSelectionContext(payload, request);
        return payload.ToJsonString(YokiFrameJson.CompactOptions);
    }

    /// <summary>只在调用方持有有效上下文时追加 revision 与稳定目标 ID。</summary>
    private static void AddSelectionContext(
        JsonObject payload,
        WorkbenchUIKitPanelGenerationRequest? request)
    {
        if (request == null) return;
        if (request.ExpectedContextRevision > 0L)
            payload["expectedContextRevision"] = request.ExpectedContextRevision;
        if (!string.IsNullOrWhiteSpace(request.TargetGlobalObjectId))
            payload["targetGlobalObjectId"] = request.TargetGlobalObjectId;
    }

    /// <summary>解析 context 或普通操作结果，wire JSON 不进入 Avalonia。</summary>
    private static WorkbenchUIKitEditorResult ParseUIKitEditorResult(
        WorkbenchUIKitEditorAction action,
        string json)
    {
        if (action == WorkbenchUIKitEditorAction.RefreshContext)
        {
            return new WorkbenchUIKitEditorResult
            {
                Succeeded = true,
                Action = action,
                Message = "Unity Editor context 已刷新。",
                Context = ParseUIKitEditorContext(json),
            };
        }

        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        return new WorkbenchUIKitEditorResult
        {
            Succeeded = true,
            Action = action,
            Message = ReadString(root, "message"),
            AffectedCount = ReadInt32(root, "affectedCount"),
            PrefabPath = ReadString(root, "prefabPath"),
            PanelScriptPath = ReadString(root, "panelScriptPath"),
            DesignerScriptPath = ReadString(root, "designerScriptPath"),
        };
    }

    /// <summary>解析 Unity Editor 选择上下文和 Provider 默认值。</summary>
    private static WorkbenchUIKitEditorContext ParseUIKitEditorContext(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        IReadOnlyList<string> codeTemplateOptions = ReadStringArray(
            root,
            "codeTemplateOptions",
            "Default",
            "Minimal");
        IReadOnlyList<string> assemblyNames = ReadStringArray(
            root,
            "assemblyNames",
            "Assembly-CSharp");
        WorkbenchUIKitPanelGenerationRequest defaultRequest = new();
        return new WorkbenchUIKitEditorContext
        {
            Available = ReadBoolean(root, "available"),
            ContextRevision = ReadInt64(root, "contextRevision"),
            ActiveGlobalObjectId = ReadString(root, "activeGlobalObjectId"),
            SelectedAssetPath = ReadString(root, "selectedAssetPath"),
            SelectedObjectName = ReadString(root, "selectedObjectName"),
            SelectedGameObjectCount = ReadInt32(root, "selectedGameObjectCount"),
            SelectedBindCount = ReadInt32(root, "selectedBindCount"),
            CanGenerateCode = ReadBoolean(root, "canGenerateCode"),
            CanAddBind = ReadBoolean(root, "canAddBind"),
            CanRemoveBind = ReadBoolean(root, "canRemoveBind"),
            ScenePath = ReadString(root, "scenePath"),
            PrefabStageActive = ReadBoolean(root, "prefabStageActive"),
            EditorMode = ReadString(root, "editorMode"),
            Defaults = new WorkbenchUIKitPanelGenerationRequest
            {
                PrefabFolder = ReadString(root, "prefabFolder", defaultRequest.PrefabFolder),
                ScriptFolder = ReadString(root, "scriptFolder", defaultRequest.ScriptFolder),
                ScriptNamespace = ReadString(root, "scriptNamespace", defaultRequest.ScriptNamespace),
                AssemblyName = ReadString(root, "assemblyName", defaultRequest.AssemblyName),
                CodeTemplate = ReadString(root, "codeTemplate", defaultRequest.CodeTemplate),
            },
            CodeTemplateOptions = codeTemplateOptions,
            AssemblyNames = assemblyNames,
        };
    }

    /// <summary>创建不抛出到 UI 的稳定失败结果。</summary>
    private static WorkbenchUIKitEditorResult CreateUIKitEditorError(
        WorkbenchUIKitEditorAction action,
        string message)
    {
        return new WorkbenchUIKitEditorResult
        {
            Succeeded = false,
            Action = action,
            Message = string.IsNullOrWhiteSpace(message) ? "UIKit Editor action failed." : message,
        };
    }

    /// <summary>判断目标是否为 Unity Editor Host。</summary>
    private static bool IsUnityEditorEngine(string engineId)
    {
        return string.Equals(engineId, "unity-editor", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>读取可选字符串字段。</summary>
    private static string ReadString(JsonElement root, string name, string fallback = "")
    {
        return root.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : fallback;
    }

    /// <summary>读取可选整数数字字段。</summary>
    private static int ReadInt32(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement value) && value.TryGetInt32(out int result)
            ? result
            : 0;
    }

    /// <summary>读取可选长整数数字字段。</summary>
    private static long ReadInt64(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement value) && value.TryGetInt64(out long result)
            ? result
            : 0L;
    }

    /// <summary>读取可选布尔字段。</summary>
    private static bool ReadBoolean(JsonElement root, string name)
    {
        return root.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.True;
    }

    /// <summary>
    /// 读取 Provider 暴露的模板名数组；旧 Provider 或损坏数组回退到两个内置模板。
    /// </summary>
    private static IReadOnlyList<string> ReadStringArray(
        JsonElement root,
        string name,
        params string[] fallback)
    {
        List<string> values = new();
        if (root.TryGetProperty(name, out JsonElement array)
            && array.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in array.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String) continue;
                string value = item.GetString() ?? string.Empty;
                if (!SafeIdValidator.IsSafeId(value) || values.Contains(value, StringComparer.Ordinal))
                    continue;
                values.Add(value);
            }
        }

        if (values.Count == 0) values.AddRange(fallback);
        return values;
    }
}
