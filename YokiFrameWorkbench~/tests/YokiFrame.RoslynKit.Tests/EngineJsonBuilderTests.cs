using YokiFrame.Json;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// 覆盖 RoslynKit 共享 JSON 写出器：嵌套结构、转义、数值与非法用法。
/// </summary>
public sealed class EngineJsonBuilderTests
{
    /// <summary>写出嵌套对象和数组，并按原字段值往返。不写文件。</summary>
    [Fact]
    public void Builds_nested_document()
    {
        string json = new RoslynJsonBuilder()
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

    /// <summary>控制字符、反斜杠和引号被转义后仍能还原。不改变源字符串。</summary>
    [Fact]
    public void Escapes_control_characters_and_round_trips()
    {
        const string value = "line1\nline2\ttab\\slash\"quote";
        string json = new RoslynJsonBuilder()
            .StartObject()
            .Property("note", value)
            .EndObject()
            .ToString();

        JsonDocument document = JsonDocument.Parse(json);
        Assert.Equal(value, document.RootElement.GetProperty("note").GetString());
    }

    /// <summary>空数组和空对象是合法 JSON。不写入占位元素。</summary>
    [Fact]
    public void Empty_containers_are_valid_json()
    {
        string json = new RoslynJsonBuilder()
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

    /// <summary>没有打开对象时写属性名抛出 InvalidOperationException。不产生 JSON 文本。</summary>
    [Fact]
    public void Property_name_requires_an_open_object()
    {
        Assert.Throws<InvalidOperationException>(() => new RoslynJsonBuilder().Name("orphan"));
    }

    /// <summary>对象值之前必须先有属性名，否则抛出 InvalidOperationException。</summary>
    [Fact]
    public void Object_value_requires_a_property_name()
    {
        var builder = new RoslynJsonBuilder().StartObject();
        Assert.Throws<InvalidOperationException>(() => builder.String("value"));
    }

    /// <summary>第二个根值被拒绝。已写出的第一个根值不被替换。</summary>
    [Fact]
    public void Second_root_value_is_rejected()
    {
        var builder = new RoslynJsonBuilder().String("first");
        Assert.Throws<InvalidOperationException>(() => builder.String("second"));
    }
}
