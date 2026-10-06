using YokiFrame.Json;

namespace YokiFrame.EngineKit.Tests;

/// <summary>
/// 覆盖 Engine Kit 共享 JSON 写出器：嵌套结构、转义、数值与非法用法。
/// </summary>
public sealed class EngineJsonBuilderTests
{
    [Fact]
    public void Builds_nested_document()
    {
        string json = new YokiFrameEngineJsonBuilder()
            .StartObject()
            .Property("operation", "scene_query")
            .Property("truncated", false)
            .Property("nodeCount", 2)
            .Name("nodes")
            .StartArray()
            .StartObject()
            .Property("name", "Root")
            .Property("active", true)
            .EndObject()
            .StartObject()
            .Property("name", "A\"B")
            .Property("active", false)
            .EndObject()
            .EndArray()
            .EndObject()
            .ToString();

        JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal("scene_query", document.RootElement.GetProperty("operation").GetString());
        Assert.False(document.RootElement.GetProperty("truncated").GetBoolean());
        Assert.True(document.RootElement.GetProperty("nodeCount").TryGetInt64(out long count));
        Assert.Equal(2L, count);

        JsonElement nodes = document.RootElement.GetProperty("nodes");
        Assert.Equal(2, nodes.GetArrayLength());
        Assert.Equal("Root", nodes[0].GetProperty("name").GetString());
        Assert.True(nodes[0].GetProperty("active").GetBoolean());
        Assert.Equal("A\"B", nodes[1].GetProperty("name").GetString());
        Assert.False(nodes[1].GetProperty("active").GetBoolean());
    }

    [Fact]
    public void Escapes_control_characters_and_round_trips()
    {
        const string value = "line1\nline2\ttab\\slash\"quote";
        string json = new YokiFrameEngineJsonBuilder()
            .StartObject()
            .Property("note", value)
            .EndObject()
            .ToString();

        JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(value, document.RootElement.GetProperty("note").GetString());
    }

    [Fact]
    public void Empty_containers_are_valid_json()
    {
        string json = new YokiFrameEngineJsonBuilder()
            .StartObject()
            .Name("items")
            .StartArray()
            .EndArray()
            .Name("child")
            .StartObject()
            .EndObject()
            .EndObject()
            .ToString();

        JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(0, document.RootElement.GetProperty("items").GetArrayLength());
        Assert.Equal(JsonValueKind.Object, document.RootElement.GetProperty("child").ValueKind);
    }

    [Fact]
    public void Property_name_requires_an_open_object()
    {
        Assert.Throws<InvalidOperationException>(() => new YokiFrameEngineJsonBuilder().Name("orphan"));
    }

    [Fact]
    public void Object_value_requires_a_property_name()
    {
        var builder = new YokiFrameEngineJsonBuilder().StartObject();
        Assert.Throws<InvalidOperationException>(() => builder.String("value"));
    }

    [Fact]
    public void Second_root_value_is_rejected()
    {
        var builder = new YokiFrameEngineJsonBuilder().String("first");
        Assert.Throws<InvalidOperationException>(() => builder.String("second"));
    }
}
