using System.Text.Json;
using YokiFrame.Tooling.Application.Models.UnityEditor;
using YokiFrame.Tooling.Application.Models.UIKit;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>验证 Workbench UIKit Editor Tools 的宿主限制、payload 和强类型解析。</summary>
public sealed class WorkbenchUIKitEditorCommandTests
{
    private const string CONTEXT_JSON = "{\"available\":true,\"contextRevision\":12,\"activeGlobalObjectId\":\"GlobalObjectId_V1-test\",\"selectedAssetPath\":\"Assets/UI/Inventory.prefab\",\"selectedObjectName\":\"Inventory\",\"selectedGameObjectCount\":2,\"selectedBindCount\":1,\"canGenerateCode\":true,\"canAddBind\":false,\"canRemoveBind\":true,\"scenePath\":\"Assets/Scenes/Main.unity\",\"prefabStageActive\":true,\"editorMode\":\"EditMode\",\"prefabFolder\":\"Assets/UI\",\"scriptFolder\":\"Assets/Scripts/UI\",\"scriptNamespace\":\"Game.UI\",\"assemblyName\":\"Game.UI\",\"codeTemplate\":\"Minimal\",\"codeTemplateOptions\":[\"Default\",\"Minimal\",\"TeamTemplate\"],\"assemblyNames\":[\"Assembly-CSharp\",\"Game.UI\"]}";

    /// <summary>验证只读 context 使用 FileBridge 并在 Application 层转换为强类型模型。</summary>
    [Fact]
    public async Task RefreshContextParsesStronglyTypedSelection()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        recorder.FileBridgeResultJson = CONTEXT_JSON;
        var service = new WorkbenchDashboardService(recorder.Client);

        WorkbenchUIKitEditorResult result = await service.ExecuteUIKitEditorActionAsync(
            "unity-editor", WorkbenchUIKitEditorAction.RefreshContext, null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(1, recorder.FileBridgeCallCount);
        Assert.Equal("UIKit", recorder.LastFileBridgeKit);
        Assert.Equal("get_editor_context", recorder.LastFileBridgeAction);
        Assert.NotNull(result.Context);
        Assert.Equal("Inventory", result.Context.SelectedObjectName);
        Assert.Equal(2, result.Context.SelectedGameObjectCount);
        Assert.Equal("Minimal", result.Context.Defaults.CodeTemplate);
        Assert.Equal(new[] { "Default", "Minimal", "TeamTemplate" }, result.Context.CodeTemplateOptions);
        Assert.Equal(new[] { "Assembly-CSharp", "Game.UI" }, result.Context.AssemblyNames);
        Assert.Equal(12L, result.Context.ContextRevision);
        Assert.Equal("GlobalObjectId_V1-test", result.Context.ActiveGlobalObjectId);
    }

    /// <summary>验证旧 Provider 缺少模板目录或默认字段时不会把页面配置清空。</summary>
    [Fact]
    public async Task RefreshContextUsesStableDefaultsWhenOptionalFieldsAreMissing()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        recorder.FileBridgeResultJson = "{\"available\":true}";
        var service = new WorkbenchDashboardService(recorder.Client);

        WorkbenchUIKitEditorResult result = await service.ExecuteUIKitEditorActionAsync(
            "unity-editor", WorkbenchUIKitEditorAction.RefreshContext, null, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.Context);
        Assert.Equal("Assets/Resources/Art/UIPrefab", result.Context!.Defaults.PrefabFolder);
        Assert.Equal("Default", result.Context.Defaults.CodeTemplate);
        Assert.Equal(new[] { "Default", "Minimal" }, result.Context.CodeTemplateOptions);
        Assert.Equal(new[] { "Assembly-CSharp" }, result.Context.AssemblyNames);
    }

    /// <summary>验证创建请求只发送受支持的六个扁平字段，并在变更后回读 context。</summary>
    [Fact]
    public async Task CreatePanelSendsExactFlatPayloadAndRefreshesContext()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        recorder.FileBridgeResultJson = CONTEXT_JSON;
        var service = new WorkbenchDashboardService(recorder.Client);
        WorkbenchUIKitPanelGenerationRequest request = new()
        {
            PanelName = "InventoryPanel",
            PrefabFolder = "Assets/UI",
            ScriptFolder = "Assets/Scripts/UI",
            ScriptNamespace = "Game.UI",
            AssemblyName = "Game.UI",
            CodeTemplate = "Minimal",
        };

        WorkbenchUIKitEditorResult result = await service.ExecuteUIKitEditorActionAsync(
            "unity-editor", WorkbenchUIKitEditorAction.CreatePanelPrefab, request, CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(2, recorder.FileBridgeCallCount);
        Assert.Equal("create_panel_prefab", recorder.FirstFileBridgeAction);
        using JsonDocument payload = JsonDocument.Parse(recorder.FirstFileBridgePayloadJson);
        Assert.Equal(6, payload.RootElement.EnumerateObject().Count());
        Assert.Equal("InventoryPanel", payload.RootElement.GetProperty("panelName").GetString());
        Assert.Equal("Game.UI", payload.RootElement.GetProperty("scriptNamespace").GetString());
        Assert.Equal("get_editor_context", recorder.LastFileBridgeAction);
        Assert.NotNull(result.Context);
    }

    /// <summary>验证 Godot engine 在进入 transport 前被稳定拒绝。</summary>
    [Fact]
    public async Task NonUnityEngineIsRejectedWithoutTransport()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        var service = new WorkbenchDashboardService(recorder.Client);

        WorkbenchUIKitEditorResult result = await service.ExecuteUIKitEditorActionAsync(
            "godot-editor", WorkbenchUIKitEditorAction.RefreshContext, null, CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Contains("unity-editor", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, recorder.FileBridgeCallCount);
    }

    /// <summary>验证生成操作缺少强类型请求时不发送不完整命令。</summary>
    [Fact]
    public async Task GenerationRequestIsRequiredBeforeTransport()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        var service = new WorkbenchDashboardService(recorder.Client);

        await Assert.ThrowsAsync<ArgumentNullException>(() => service.ExecuteUIKitEditorActionAsync(
            "unity-editor", WorkbenchUIKitEditorAction.GenerateCodeForSelection, null, CancellationToken.None));

        Assert.Equal(0, recorder.FileBridgeCallCount);
    }

    /// <summary>验证通用 UnityEditor Context 用例使用独立 Kit/action，不依赖 UIKit 私有字段。</summary>
    [Fact]
    public async Task GenericUnityEditorContextParsesStableSelectionModel()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();
        recorder.FileBridgeResultJson = "{\"schemaVersion\":1,\"available\":true,\"revision\":42,"
            + "\"selection\":{\"count\":1,\"totalCount\":1,\"truncated\":false,"
            + "\"activeGlobalObjectId\":\"GlobalObjectId_V1-object\",\"objects\":[{"
            + "\"globalObjectId\":\"GlobalObjectId_V1-object\",\"assetGuid\":\"guid\","
            + "\"assetPath\":\"Assets/UI/Inventory.prefab\",\"name\":\"Inventory\","
            + "\"type\":\"UnityEngine.GameObject\",\"hierarchyPath\":\"Inventory\","
            + "\"isAsset\":true,\"isGameObject\":true}]},"
            + "\"scene\":{\"path\":\"Assets/Scenes/Main.unity\",\"name\":\"Main\","
            + "\"dirty\":false,\"buildIndex\":0},"
            + "\"prefabStage\":{\"active\":true,\"assetPath\":\"Assets/UI/Inventory.prefab\","
            + "\"scenePath\":\"\",\"rootName\":\"Inventory\"},"
            + "\"editor\":{\"mode\":\"EditMode\",\"isPlaying\":false,\"isPaused\":false,"
            + "\"isCompiling\":false,\"isUpdating\":false,\"isBatchMode\":false}}";
        var service = new WorkbenchDashboardService(recorder.Client);

        WorkbenchUnityEditorContext context = await service.ReadUnityEditorContextAsync(
            "unity-editor",
            CancellationToken.None);

        Assert.True(context.Available);
        Assert.Equal(42L, context.Revision);
        Assert.Equal("GlobalObjectId_V1-object", context.Selection.ActiveGlobalObjectId);
        Assert.Single(context.Selection.Objects);
        Assert.Equal("Assets/Scenes/Main.unity", context.Scene.Path);
        Assert.True(context.PrefabStage.Active);
        Assert.Equal("UnityEditor", recorder.LastFileBridgeKit);
        Assert.Equal("get_context", recorder.LastFileBridgeAction);
    }
}
