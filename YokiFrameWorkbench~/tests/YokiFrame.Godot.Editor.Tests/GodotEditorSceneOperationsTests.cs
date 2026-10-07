using System.Text.Json;
using YokiFrame;

namespace YokiFrame.Godot.Editor.Tests;

/// <summary>
/// Godot Editor 场景操作的描述符、载荷校验与能力矩阵（§7）。
/// </summary>
/// <remarks>
/// 合法载荷会触碰 EditorInterface 原生单例，因此这里只断言"解析阶段就失败"的路径；
/// 真实创建/删除/Undo 需要活的 Godot 编辑器进程。
/// </remarks>
public sealed class GodotEditorSceneOperationsTests
{
    [Fact]
    public void Editor_scene_operations_declare_editor_target()
    {
        var query = new GodotEditorSceneQueryOperation();
        var mutate = new GodotEditorSceneMutateOperation();

        Assert.Equal("scene_query", query.Descriptor.Action);
        Assert.Equal(YokiFrameCommandKind.ReadOnly, query.Descriptor.Kind);
        Assert.Equal(RoslynExecutionTarget.Editor, query.Descriptor.Targets);

        Assert.Equal("scene_mutate", mutate.Descriptor.Action);
        Assert.Equal(YokiFrameCommandKind.Dangerous, mutate.Descriptor.Kind);
        Assert.Equal(RoslynExecutionTarget.Editor, mutate.Descriptor.Targets);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{\"depth\":\"x\"}")]
    public void Editor_scene_query_rejects_invalid_payloads(string payload)
    {
        YokiFrameCommandResult result = new GodotEditorSceneQueryOperation().Execute(CreateRequest("scene_query", payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
    }

    [Theory]
    [InlineData("not-json")]
    [InlineData("[]")]
    [InlineData("{\"op\":\"\"}")]
    public void Editor_scene_mutate_rejects_payloads_without_op(string payload)
    {
        YokiFrameCommandResult result = new GodotEditorSceneMutateOperation().Execute(CreateRequest("scene_mutate", payload));

        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.INVALID_PAYLOAD, result.ErrorCode);
    }

    [Fact]
    public void Editor_capabilities_matrix_includes_scene_operations_for_editor_target()
    {
        var settingsSource = new StubEditorSettingsSource(RoslynSettingsSnapshot.Enabled());
        var engineProvider = new GodotEditorEngineOperationProvider
        {
            EngineVersionAccessor = () => "4.7.0-test",
            IsPlayingAccessor = () => false,
            SessionIdAccessor = () => "session-editor"
        };
        var gate = RoslynGate.CreateDefault(settingsSource);
        var provider = new RoslynKitProvider(gate, engineProvider, settingsSource);

        YokiFrameCommandResult result = provider.Handle(CreateRequest("engine_capabilities", "{}"));
        Assert.True(result.IsSuccess, result.ErrorCode + " " + result.ErrorMessage);

        JsonElement capabilities = JsonDocument.Parse(result.ResultJson).RootElement;
        Assert.Equal("editor", capabilities.GetProperty("hostTargets").GetString());

        JsonElement operations = capabilities.GetProperty("operations");
        bool foundQuery = false;
        bool foundMutate = false;
        for (var index = 0; index < operations.GetArrayLength(); index++)
        {
            string action = operations[index].GetProperty("action").GetString()!;
            if (action == "scene_query")
            {
                foundQuery = true;
                Assert.Equal("editor", operations[index].GetProperty("targets").GetString());
                Assert.Equal("Enabled", Availability(operations[index], "editor"));
            }
            else if (action == "scene_mutate")
            {
                foundMutate = true;
                Assert.Equal("editor", operations[index].GetProperty("targets").GetString());
                Assert.Equal("Enabled", Availability(operations[index], "editor"));
            }
        }

        Assert.True(foundQuery, "capabilities must list scene_query");
        Assert.True(foundMutate, "capabilities must list scene_mutate");
    }

    private static string Availability(JsonElement operation, string target)
    {
        JsonElement entries = operation.GetProperty("targetAvailability");
        for (var index = 0; index < entries.GetArrayLength(); index++)
        {
            if (entries[index].GetProperty("target").GetString() == target)
            {
                return entries[index].GetProperty("availability").GetString()!;
            }
        }

        throw new InvalidOperationException("target not declared: " + target);
    }

    private static YokiFrameCommandRequest CreateRequest(string action, string payload)
    {
        return new YokiFrameCommandRequest("cli", "RoslynKit", action, payload, 5000, 0L, "req-" + Guid.NewGuid().ToString("N"), DateTimeOffset.UtcNow);
    }
}
