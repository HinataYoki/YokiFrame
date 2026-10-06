using System.Reflection;
using YokiFrame;
using YokiFrame.Json;

namespace YokiFrame.EngineKit.Tests;

public sealed class LiveFieldValueTests
{
    public enum Mode { Idle, Moving }
    [Serializable] public struct Point { public float X; public int Y; }
    [Serializable] public sealed class Stats
    {
        public int Health;
        public List<Point> Points = new();
        public Mode Mode;
        public string? Label;
        public Stats() => throw new Exception("User constructor must not run");
        public int Getter => throw new Exception("User accessor must not run");
    }
    [Serializable] public sealed class Link { public Link? Next; }
    [Serializable] public sealed class Row { public int[] Values = Array.Empty<int>(); }
    public sealed class Unmarked { public int Value; }

    private static object Decode(Type type, string text)
    {
        using var json = JsonDocument.Parse(text);
        return YokiFrameLiveFieldValues.Decode(type, json.RootElement,
            t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void Arrays_lists_and_nested_data_decode_without_user_code()
    {
        Assert.Equal(new[] { 1, 2, 3 }, (int[])Decode(typeof(int[]), "[1,2,3]"));
        Assert.Equal(new[] { 1.5f, 2f }, (List<float>)Decode(typeof(List<float>), "[1.5,2]"));
        var result = (Stats)Decode(typeof(Stats), """
            {"Health":75,"Points":[{"X":1.5,"Y":2}],"Mode":"Moving","Label":null}
            """);
        Assert.Equal(75, result.Health);
        Assert.Equal(1.5f, Assert.Single(result.Points).X);
        Assert.Equal(Mode.Moving, result.Mode);
        Assert.Null(result.Label);
        Assert.Null(Decode(typeof(Stats), "null"));
        Assert.Empty((int[])Decode(typeof(int[]), "[]"));
        Assert.Null(Decode(typeof(List<int>), "null"));
    }

    [Theory]
    [InlineData("[1,2.5]")]
    [InlineData("[1,2147483648]")]
    [InlineData("[1,\"2\"]")]
    [InlineData("{}")]
    public void Bad_elements_are_rejected(string text) =>
        Assert.ThrowsAny<Exception>(() => Decode(typeof(int[]), text));

    [Theory]
    [InlineData("{\"X\":1}")]
    [InlineData("{\"X\":1,\"Y\":2,\"Typo\":3}")]
    [InlineData("{\"X\":1,\"Other\":2}")]
    [InlineData("null")]
    public void Objects_require_the_complete_exact_field_set(string text) =>
        Assert.ThrowsAny<Exception>(() => Decode(typeof(Point), text));

    [Theory]
    [InlineData(typeof(Dictionary<string, int>), "{}")]
    [InlineData(typeof(int[,]), "[]")]
    [InlineData(typeof(int[][]), "[]")]
    [InlineData(typeof(List<List<int>>), "[]")]
    [InlineData(typeof(Unmarked), "{}")]
    public void Unsupported_shapes_are_rejected(Type type, string text) =>
        Assert.Throws<NotSupportedException>(() => Decode(type, text));

    [Fact]
    public void Depth_element_and_total_node_limits_are_enforced()
    {
        string deep = "null";
        for (int i = 0; i < 10; i++) deep = "{\"Next\":" + deep + "}";
        Assert.Throws<NotSupportedException>(() => Decode(typeof(Link), deep));
        Assert.Throws<ArgumentException>(() => Decode(typeof(int[]), "[" + string.Join(",", new int[257]) + "]"));
        string row = "{\"Values\":[" + string.Join(",", new int[256]) + "]}";
        Assert.Throws<NotSupportedException>(() => Decode(typeof(Row[]), "[" + string.Join(",", Enumerable.Repeat(row, 5)) + "]"));
    }

    [Fact]
    public void Type_identity_excludes_transient_assembly_names()
    {
        string name = YokiFrameLiveFieldValues.PersistedTypeName(typeof(List<Stats>));
        Assert.Equal("System.Collections.Generic.List<" + typeof(Stats).FullName + ">", name);
        Assert.DoesNotContain("Version=", name);
    }
}
