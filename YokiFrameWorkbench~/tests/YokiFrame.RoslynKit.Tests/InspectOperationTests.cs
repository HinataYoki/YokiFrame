using System.Collections.Generic;
using Xunit;

namespace YokiFrame.RoslynKit.Tests;

/// <summary>
/// inspect：通用只读反射读取（根 + 选择器 + 路径）。零编译、不依赖任何具体框架类型。
/// </summary>
public sealed class InspectOperationTests
{
    [Fact]
    /// <summary>从 service 根按路径读取成员。成功结果包含字段值，不编译。</summary>
    public void Reads_member_by_path_from_a_service_root()
    {
        var model = new FakeModel { Name = "hero", Hp = 20 };
        YokiFrameCommandResult result = Inspect(_ => model, "{\"selector\":\"FakeModel\",\"path\":\"Hp\"}");

        Assert.True(result.IsSuccess, result.ErrorCode + " " + result.ErrorMessage);
        Assert.Contains("\"value\":\"20\"", Compact(result.ResultJson), System.StringComparison.Ordinal);
    }

    [Fact]
    /// <summary>路径可走字典键和列表下标。两次读取都成功，且不修改源对象。</summary>
    public void Walks_dictionary_keys_and_list_indexes()
    {
        var model = new FakeModel { Name = "hero", Hp = 1 };
        model.Items["k1"] = new FakeModel { Name = "boss", Hp = 77 };
        model.Order.Add(new FakeModel { Name = "first", Hp = 1 });
        model.Order.Add(new FakeModel { Name = "second", Hp = 2 });

        YokiFrameCommandResult byKey = Inspect(_ => model, "{\"selector\":\"FakeModel\",\"path\":\"Items[k1].Hp\"}");
        Assert.True(byKey.IsSuccess, byKey.ErrorCode + " " + byKey.ErrorMessage);
        Assert.Contains("\"value\":\"77\"", Compact(byKey.ResultJson), System.StringComparison.Ordinal);

        YokiFrameCommandResult byIndex = Inspect(_ => model, "{\"selector\":\"FakeModel\",\"path\":\"Order[1].Name\"}");
        Assert.True(byIndex.IsSuccess, byIndex.ErrorCode + " " + byIndex.ErrorMessage);
        Assert.Contains("second", Compact(byIndex.ResultJson), System.StringComparison.Ordinal);
    }

    [Fact]
    /// <summary>static 根按类型全名读取静态字段。解析器返回空也不影响静态读取。</summary>
    public void Reads_static_members_through_the_static_root()
    {
        InspectStatics.Counter = 42;
        YokiFrameCommandResult result = Inspect(_ => null, "{\"root\":\"static\",\"selector\":\"YokiFrame.RoslynKit.Tests.InspectStatics\",\"path\":\"Counter\"}");

        Assert.True(result.IsSuccess, result.ErrorCode + " " + result.ErrorMessage);
        Assert.Contains("\"value\":\"42\"", Compact(result.ResultJson), System.StringComparison.Ordinal);
    }

    [Fact]
    /// <summary>缺少成员和未知根返回不同错误码。未知根的说明里仍列出 service。</summary>
    public void Missing_member_and_unknown_root_are_reported_separately()
    {
        var model = new FakeModel();
        YokiFrameCommandResult missing = Inspect(_ => model, "{\"selector\":\"FakeModel\",\"path\":\"Nope\"}");
        Assert.False(missing.IsSuccess);
        Assert.Equal(RoslynErrorCodes.INSPECT_MEMBER_NOT_FOUND, missing.ErrorCode);

        YokiFrameCommandResult unknownRoot = Inspect(_ => model, "{\"root\":\"custom\",\"selector\":\"x\",\"path\":\"Hp\"}");
        Assert.False(unknownRoot.IsSuccess);
        Assert.Equal(RoslynErrorCodes.INSPECT_TARGET_NOT_FOUND, unknownRoot.ErrorCode);
        Assert.Contains("service", unknownRoot.ErrorMessage, System.StringComparison.Ordinal);
    }

    [Fact]
    /// <summary>选择器无法解析时返回目标不存在，不抛异常。</summary>
    public void Unresolved_selector_is_a_target_error_not_a_crash()
    {
        YokiFrameCommandResult result = Inspect(_ => null, "{\"selector\":\"NoSuchModel\",\"path\":\"Hp\"}");
        Assert.False(result.IsSuccess);
        Assert.Equal(RoslynErrorCodes.INSPECT_TARGET_NOT_FOUND, result.ErrorCode);
    }

    [Fact]
    /// <summary>自定义根可以扩展寻址并读到字段。不替换默认根以外的注册项。</summary>
    public void Custom_root_provider_extends_addressing()
    {
        var provider = new FakeRootProvider { Instance = new FakeModel { Hp = 9 } };
        var operation = new RoslynInspectOperation(
            InspectRootRegistry.CreateDefault().Add(provider));

        YokiFrameCommandResult result = operation.Execute(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            RoslynInspectOperation.ACTION,
            "{\"root\":\"fake\",\"selector\":\"anything\",\"path\":\"Hp\"}"));

        Assert.True(result.IsSuccess, result.ErrorCode + " " + result.ErrorMessage);
        Assert.Contains("\"value\":\"9\"", Compact(result.ResultJson), System.StringComparison.Ordinal);
    }

    [Fact]
    /// <summary>不给路径时列出成员名。结果同时包含 Hp 和 Name。</summary>
    public void Without_path_lists_members_with_values()
    {
        YokiFrameCommandResult result = Inspect(_ => new FakeModel { Name = "hero", Hp = 5 }, "{\"selector\":\"FakeModel\"}");

        Assert.True(result.IsSuccess, result.ErrorCode + " " + result.ErrorMessage);
        string json = Compact(result.ResultJson);
        Assert.Contains("\"name\":\"Hp\"", json, System.StringComparison.Ordinal);
        Assert.Contains("\"name\":\"Name\"", json, System.StringComparison.Ordinal);
    }

    /// <summary>用给定解析器替换 service 根并执行 inspect。不写文件。</summary>
    /// <param name="resolver">按选择器返回实例的函数。</param>
    /// <param name="payload">inspect 请求 JSON。</param>
    /// <returns>操作执行结果。</returns>
    private static YokiFrameCommandResult Inspect(System.Func<string, object> resolver, string payload)
    {
        // 用访问器覆盖内置的 service 根：这本身也是"根可替换"这一设计的用法示例。
        InspectRootRegistry roots = InspectRootRegistry.CreateDefault()
            .Add(new AccessorInspectRoot(ArchitectureInspectRoot.ROOT_NAME, resolver));
        var operation = new RoslynInspectOperation(roots);
        return operation.Execute(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            RoslynInspectOperation.ACTION,
            payload));
    }

    /// <summary>去掉空白，便于断言压缩后的 JSON 片段。不解析 JSON。</summary>
    /// <param name="json">原始结果 JSON。</param>
    /// <returns>去掉空格和换行后的文本。</returns>
    private static string Compact(string json)
    {
        return json.Replace(" ", string.Empty).Replace("\n", string.Empty).Replace("\r", string.Empty);
    }

    private sealed class FakeModel
    {
        public string Name { get; set; } = string.Empty;

        public int Hp;

        public Dictionary<string, FakeModel> Items { get; } = new Dictionary<string, FakeModel>();

        public List<FakeModel> Order { get; } = new List<FakeModel>();
    }

    private sealed class FakeRootProvider : IInspectRoot
    {
        public string Name => "fake";

        public object Instance { get; set; }

        /// <summary>忽略选择器并返回预设实例。实例为空时失败，错误信息为空。</summary>
        /// <param name="selector">未使用的选择器。</param>
        /// <param name="instance">解析到的实例。</param>
        /// <param name="error">失败原因；本实现始终为空。</param>
        /// <returns>实例非空时为 true。</returns>
        public bool TryResolve(string selector, out object instance, out string error)
        {
            instance = Instance;
            error = string.Empty;
            return instance != null;
        }
    }
}

/// <summary>inspect 的静态根测试夹具（顶层类型，按全名可解析）。</summary>
internal static class InspectStatics
{
    public static int Counter;
}
