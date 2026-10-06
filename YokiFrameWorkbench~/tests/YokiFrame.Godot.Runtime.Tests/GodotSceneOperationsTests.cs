using YokiFrame;
using YokiFrame.Json;

namespace YokiFrame.Godot.Runtime.Tests;

/// <summary>
/// Godot Runtime 场景操作的描述符与载荷校验（§7）。
/// </summary>
/// <remarks>
/// 只覆盖不触碰 Godot 原生单例的路径：非法载荷在解析阶段即返回，
/// 合法载荷在缺少 SceneTree 时返回 Unavailable。真实遍历/修改需要活的 Godot 进程。
/// </remarks>
public sealed class GodotSceneOperationsTests
{
    [Fact]
    public void Runtime_scene_operations_declare_runtime_target()
    {
        var query = new GodotRuntimeSceneQueryOperation(null);
        var mutate = new GodotRuntimeSceneMutateOperation(null);

        Assert.Equal("scene_query", query.Descriptor.Action);
        Assert.Equal(YokiFrameCommandKind.ReadOnly, query.Descriptor.Kind);
        Assert.Equal(YokiFrameEngineExecutionTarget.Runtime, query.Descriptor.Targets);
        Assert.False(query.Descriptor.IsTargetAgnostic);

        Assert.Equal("scene_mutate", mutate.Descriptor.Action);
        Assert.Equal(YokiFrameCommandKind.Dangerous, mutate.Descriptor.Kind);
        Assert.Equal(YokiFrameEngineExecutionTarget.Runtime, mutate.Descriptor.Targets);
    }

    [Theory]
    [InlineData("{\"depth\":\"x\"}")]
    [InlineData("not-json")]
    [InlineData("3")]
    public void Runtime_scene_query_rejects_invalid_payloads(string payload)
    {
        var query = new GodotRuntimeSceneQueryOperation(null);

        YokiFrameCommandResult result = query.Execute(CreateRequest("scene_query", payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
    }

    [Fact]
    public void Runtime_scene_query_reports_unavailable_without_scene_tree()
    {
        var query = new GodotRuntimeSceneQueryOperation(null);

        YokiFrameCommandResult result = query.Execute(CreateRequest("scene_query", "{\"depth\":1}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.UNAVAILABLE, result.ErrorCode);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"op\":\"\"}")]
    public void Runtime_scene_mutate_rejects_payloads_without_op(string payload)
    {
        var mutate = new GodotRuntimeSceneMutateOperation(null);

        YokiFrameCommandResult result = mutate.Execute(CreateRequest("scene_mutate", payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
    }

    [Fact]
    public void Runtime_scene_mutate_reports_unavailable_without_scene_tree()
    {
        var mutate = new GodotRuntimeSceneMutateOperation(null);

        YokiFrameCommandResult result = mutate.Execute(CreateRequest(
            "scene_mutate",
            "{\"op\":\"create\",\"name\":\"Probe\"}"));

        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.UNAVAILABLE, result.ErrorCode);
    }

    [Fact]
    public void Scene_query_support_clamps_depth_and_validates_vectors()
    {
        Assert.True(GodotSceneQuerySupport.TryReadOptions("{\"depth\":99}", out _, out int depth, out _, out _));
        Assert.Equal(GodotSceneQuerySupport.MAX_DEPTH, depth);
        Assert.True(GodotSceneQuerySupport.TryReadOptions("{\"depth\":-5}", out _, out int negative, out _, out _));
        Assert.Equal(0, negative);
        Assert.True(GodotSceneQuerySupport.TryReadOptions("{}", out _, out int fallback, out _, out _));
        Assert.Equal(GodotSceneQuerySupport.DEFAULT_DEPTH, fallback);

        using JsonDocument single = JsonDocument.Parse("{\"position\":[1]}");
        Assert.False(GodotSceneMutateSupport.TryReadVector(single.RootElement, "position", 2, out _));
        using JsonDocument triple = JsonDocument.Parse("{\"position\":[1,2.5,-3]}");
        Assert.True(GodotSceneMutateSupport.TryReadVector(triple.RootElement, "position", 2, out float[] values));
        Assert.Equal(2.5f, values[1]);
    }

    private static YokiFrameCommandRequest CreateRequest(string action, string payload)
    {
        return new YokiFrameCommandRequest("cli", "Engine", action, payload, 5000, 0L, "req-" + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
    }
}
