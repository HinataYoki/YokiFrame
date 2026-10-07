using System.Reflection;
using YokiFrame;
using YokiFrame.Json;

namespace YokiFrame.RoslynKit.Tests;

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

        /// <summary>用户构造必须不可达。解码器不得调用它。</summary>
        public Stats() => throw new Exception("User constructor must not run");

        public int Getter => throw new Exception("User accessor must not run");
    }
    [Serializable] public sealed class Link { public Link? Next; }
    [Serializable] public sealed class Row { public int[] Values = Array.Empty<int>(); }
    public sealed class Unmarked { public int Value; }

    /// <summary>按公开实例字段解码一段 JSON。不执行用户构造或属性访问器。</summary>
    /// <param name="type">目标类型。</param>
    /// <param name="text">待解码 JSON 文本。</param>
    /// <returns>解码结果；JSON null 时由解码器决定是否返回空引用。</returns>
    private static object Decode(Type type, string text)
    {
        using var json = JsonDocument.Parse(text);
        return LiveFieldValues.Decode(type, json.RootElement,
            t => t.GetFields(BindingFlags.Public | BindingFlags.Instance));
    }

    /// <summary>数组、列表和嵌套数据可解码，且不运行用户构造。空数组与 null 保持原语义。</summary>
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

    /// <summary>元素类型、范围或形状不合法时拒绝解码。不产生部分数组。</summary>
    /// <param name="text">非法整数数组 JSON。</param>
    [Theory]
    [InlineData("[1,2.5]")]
    [InlineData("[1,2147483648]")]
    [InlineData("[1,\"2\"]")]
    [InlineData("{}")]
    public void Bad_elements_are_rejected(string text) =>
        Assert.ThrowsAny<Exception>(() => Decode(typeof(int[]), text));

    /// <summary>对象必须正好包含全部公开字段。缺字段、多字段或 null 都拒绝，不调用构造。</summary>
    /// <param name="text">待解码的对象 JSON。</param>
    [Theory]
    [InlineData("{\"X\":1}")]
    [InlineData("{\"X\":1,\"Y\":2,\"Typo\":3}")]
    [InlineData("{\"X\":1,\"Other\":2}")]
    [InlineData("null")]
    public void Objects_require_the_complete_exact_field_set(string text) =>
        Assert.ThrowsAny<Exception>(() => Decode(typeof(Point), text));

    /// <summary>字典、多维数组、交错数组、嵌套列表和未标记类型不被支持。</summary>
    /// <param name="type">被拒绝的目标类型。</param>
    /// <param name="text">形状上看似合法的 JSON。</param>
    [Theory]
    [InlineData(typeof(Dictionary<string, int>), "{}")]
    [InlineData(typeof(int[,]), "[]")]
    [InlineData(typeof(int[][]), "[]")]
    [InlineData(typeof(List<List<int>>), "[]")]
    [InlineData(typeof(Unmarked), "{}")]
    public void Unsupported_shapes_are_rejected(Type type, string text) =>
        Assert.Throws<NotSupportedException>(() => Decode(type, text));

    /// <summary>深度、单层元素数和总节点数超限时拒绝。不改调用方数据。</summary>
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

    /// <summary>持久化类型名使用完整名且不含程序集版本。不读取临时程序集显示名。</summary>
    [Fact]
    public void Type_identity_excludes_transient_assembly_names()
    {
        string name = LiveFieldValues.PersistedTypeName(typeof(List<Stats>));
        Assert.Equal("System.Collections.Generic.List<" + typeof(Stats).FullName + ">", name);
        Assert.DoesNotContain("Version=", name);
    }
}
