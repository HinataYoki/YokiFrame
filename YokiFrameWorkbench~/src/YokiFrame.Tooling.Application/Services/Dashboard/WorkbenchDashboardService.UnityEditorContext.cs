using System.Text.Json;
using YokiFrame.Tooling.Application.Models;
using YokiFrame.Tooling.Application.Models.UnityEditor;

namespace YokiFrame.Tooling.Application.Services;

/// <summary>承载 Unity Editor 公共只读上下文的 Application 用例。</summary>
public sealed partial class WorkbenchDashboardService
{
    /// <summary>通过统一 Client 读取并解析 UnityEditor/get_context。</summary>
    /// <param name="engineId">必须为 unity-editor。</param>
    /// <param name="cancellationToken">调用取消令牌。</param>
    /// <returns>强类型上下文；失败时保留 ErrorMessage。</returns>
    public async Task<WorkbenchUnityEditorContext> ReadUnityEditorContextAsync(
        string engineId,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(engineId, "unity-editor", StringComparison.OrdinalIgnoreCase))
        {
            return CreateUnityEditorContextError(
                "Unity Editor context 只支持 unity-editor。");
        }

        WorkbenchCommandState command = await SendCommandAsync(
            engineId,
            "UnityEditor",
            "get_context",
            "{}",
            cancellationToken).ConfigureAwait(false);
        if (!command.Ok)
        {
            return CreateUnityEditorContextError(command.ErrorMessage);
        }

        try
        {
            return ParseUnityEditorContext(command.ResultJson);
        }
        catch (Exception exception)
        {
            return CreateUnityEditorContextError(exception.Message);
        }
    }

    /// <summary>把 Unity Editor context wire JSON 转换为 Application DTO。</summary>
    /// <param name="json">Provider 返回的上下文 JSON。</param>
    /// <returns>强类型上下文。</returns>
    private static WorkbenchUnityEditorContext ParseUnityEditorContext(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        JsonElement root = document.RootElement;
        JsonElement selection = GetContextObject(root, "selection");
        JsonElement scene = GetContextObject(root, "scene");
        JsonElement prefabStage = GetContextObject(root, "prefabStage");
        JsonElement editor = GetContextObject(root, "editor");
        return new WorkbenchUnityEditorContext
        {
            SchemaVersion = ReadContextInt32(root, "schemaVersion"),
            Available = ReadContextBoolean(root, "available"),
            Revision = ReadContextInt64(root, "revision"),
            Selection = ParseUnityEditorSelection(selection),
            Scene = new WorkbenchUnityEditorScene
            {
                Path = ReadContextString(scene, "path"),
                Name = ReadContextString(scene, "name"),
                Dirty = ReadContextBoolean(scene, "dirty"),
                BuildIndex = ReadContextInt32(scene, "buildIndex", -1),
            },
            PrefabStage = new WorkbenchUnityEditorPrefabStage
            {
                Active = ReadContextBoolean(prefabStage, "active"),
                AssetPath = ReadContextString(prefabStage, "assetPath"),
                ScenePath = ReadContextString(prefabStage, "scenePath"),
                RootName = ReadContextString(prefabStage, "rootName"),
            },
            Editor = new WorkbenchUnityEditorState
            {
                Mode = ReadContextString(editor, "mode"),
                IsPlaying = ReadContextBoolean(editor, "isPlaying"),
                IsPaused = ReadContextBoolean(editor, "isPaused"),
                IsCompiling = ReadContextBoolean(editor, "isCompiling"),
                IsUpdating = ReadContextBoolean(editor, "isUpdating"),
                IsBatchMode = ReadContextBoolean(editor, "isBatchMode"),
            },
        };
    }

    /// <summary>解析 Selection 及其有界对象列表。</summary>
    /// <param name="selection">Selection JSON 对象。</param>
    /// <returns>Selection 强类型模型。</returns>
    private static WorkbenchUnityEditorSelection ParseUnityEditorSelection(JsonElement selection)
    {
        List<WorkbenchUnityEditorObject> objects = new();
        if (selection.ValueKind == JsonValueKind.Object
            && selection.TryGetProperty("objects", out JsonElement items)
            && items.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in items.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                    objects.Add(ParseUnityEditorObject(item));
            }
        }

        WorkbenchUnityEditorObject? active = null;
        if (selection.ValueKind == JsonValueKind.Object
            && selection.TryGetProperty("activeObject", out JsonElement activeElement)
            && activeElement.ValueKind == JsonValueKind.Object)
        {
            active = ParseUnityEditorObject(activeElement);
        }

        return new WorkbenchUnityEditorSelection
        {
            Count = ReadContextInt32(selection, "count"),
            TotalCount = ReadContextInt32(selection, "totalCount"),
            Truncated = ReadContextBoolean(selection, "truncated"),
            ActiveGlobalObjectId = ReadContextString(selection, "activeGlobalObjectId"),
            ActiveObject = active,
            Objects = objects,
        };
    }

    /// <summary>解析一个稳定 Unity 对象摘要。</summary>
    /// <param name="item">对象 JSON。</param>
    /// <returns>强类型对象摘要。</returns>
    private static WorkbenchUnityEditorObject ParseUnityEditorObject(JsonElement item)
    {
        return new WorkbenchUnityEditorObject
        {
            GlobalObjectId = ReadContextString(item, "globalObjectId"),
            AssetGuid = ReadContextString(item, "assetGuid"),
            AssetPath = ReadContextString(item, "assetPath"),
            Name = ReadContextString(item, "name"),
            Type = ReadContextString(item, "type"),
            HierarchyPath = ReadContextString(item, "hierarchyPath"),
            IsAsset = ReadContextBoolean(item, "isAsset"),
            IsGameObject = ReadContextBoolean(item, "isGameObject"),
        };
    }

    /// <summary>读取可选子对象；缺失时返回 Undefined。</summary>
    private static JsonElement GetContextObject(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.Object
            ? value
            : default;
    }

    /// <summary>读取上下文字符串字段。</summary>
    private static string ReadContextString(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
    }

    /// <summary>读取上下文布尔字段。</summary>
    private static bool ReadContextBoolean(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.ValueKind == JsonValueKind.True;
    }

    /// <summary>读取上下文 Int32 字段。</summary>
    private static int ReadContextInt32(JsonElement parent, string name, int fallback = 0)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetInt32(out int result)
            ? result
            : fallback;
    }

    /// <summary>读取上下文 Int64 字段。</summary>
    private static long ReadContextInt64(JsonElement parent, string name)
    {
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement value)
            && value.TryGetInt64(out long result)
            ? result
            : 0L;
    }

    /// <summary>创建不抛出到页面的稳定失败模型。</summary>
    private static WorkbenchUnityEditorContext CreateUnityEditorContextError(string message)
    {
        return new WorkbenchUnityEditorContext
        {
            Available = false,
            ErrorMessage = string.IsNullOrWhiteSpace(message)
                ? "Unity Editor context unavailable."
                : message,
        };
    }
}
