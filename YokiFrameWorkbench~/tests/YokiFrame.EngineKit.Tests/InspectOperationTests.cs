using System.Collections.Generic;
using Xunit;

namespace YokiFrame.EngineKit.Tests;

/// <summary>
/// inspect：通用只读反射读取（根 + 选择器 + 路径）。零编译、不依赖任何具体框架类型。
/// </summary>
public sealed class InspectOperationTests
{
    [Fact]
    public void Reads_member_by_path_from_a_service_root()
    {
        var model = new FakeModel { Name = "hero", Hp = 20 };
        YokiFrameCommandResult result = Inspect(_ => model, "{\"selector\":\"FakeModel\",\"path\":\"Hp\"}");

        Assert.True(result.IsSuccess, result.ErrorCode + " " + result.ErrorMessage);
        Assert.Contains("\"value\":\"20\"", Compact(result.ResultJson), System.StringComparison.Ordinal);
    }

    [Fact]
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
    public void Reads_static_members_through_the_static_root()
    {
        InspectStatics.Counter = 42;
        YokiFrameCommandResult result = Inspect(_ => null, "{\"root\":\"static\",\"selector\":\"YokiFrame.EngineKit.Tests.InspectStatics\",\"path\":\"Counter\"}");

        Assert.True(result.IsSuccess, result.ErrorCode + " " + result.ErrorMessage);
        Assert.Contains("\"value\":\"42\"", Compact(result.ResultJson), System.StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_member_and_unknown_root_are_reported_separately()
    {
        var model = new FakeModel();
        YokiFrameCommandResult missing = Inspect(_ => model, "{\"selector\":\"FakeModel\",\"path\":\"Nope\"}");
        Assert.False(missing.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.INSPECT_MEMBER_NOT_FOUND, missing.ErrorCode);

        YokiFrameCommandResult unknownRoot = Inspect(_ => model, "{\"root\":\"custom\",\"selector\":\"x\",\"path\":\"Hp\"}");
        Assert.False(unknownRoot.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.INSPECT_TARGET_NOT_FOUND, unknownRoot.ErrorCode);
        Assert.Contains("service", unknownRoot.ErrorMessage, System.StringComparison.Ordinal);
    }

    [Fact]
    public void Unresolved_selector_is_a_target_error_not_a_crash()
    {
        YokiFrameCommandResult result = Inspect(_ => null, "{\"selector\":\"NoSuchModel\",\"path\":\"Hp\"}");
        Assert.False(result.IsSuccess);
        Assert.Equal(YokiFrameEngineErrorCodes.INSPECT_TARGET_NOT_FOUND, result.ErrorCode);
    }

    [Fact]
    public void Custom_root_provider_extends_addressing()
    {
        var provider = new FakeRootProvider { Instance = new FakeModel { Hp = 9 } };
        var operation = new YokiFrameEngineInspectOperation(
            YokiFrameInspectRootRegistry.CreateDefault().Add(provider));

        YokiFrameCommandResult result = operation.Execute(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            YokiFrameEngineInspectOperation.ACTION,
            "{\"root\":\"fake\",\"selector\":\"anything\",\"path\":\"Hp\"}"));

        Assert.True(result.IsSuccess, result.ErrorCode + " " + result.ErrorMessage);
        Assert.Contains("\"value\":\"9\"", Compact(result.ResultJson), System.StringComparison.Ordinal);
    }

    [Fact]
    public void Without_path_lists_members_with_values()
    {
        YokiFrameCommandResult result = Inspect(_ => new FakeModel { Name = "hero", Hp = 5 }, "{\"selector\":\"FakeModel\"}");

        Assert.True(result.IsSuccess, result.ErrorCode + " " + result.ErrorMessage);
        string json = Compact(result.ResultJson);
        Assert.Contains("\"name\":\"Hp\"", json, System.StringComparison.Ordinal);
        Assert.Contains("\"name\":\"Name\"", json, System.StringComparison.Ordinal);
    }

    private static YokiFrameCommandResult Inspect(System.Func<string, object> resolver, string payload)
    {
        // 用访问器覆盖内置的 service 根：这本身也是"根可替换"这一设计的用法示例。
        YokiFrameInspectRootRegistry roots = YokiFrameInspectRootRegistry.CreateDefault()
            .Add(new AccessorInspectRoot(ArchitectureInspectRoot.ROOT_NAME, resolver));
        var operation = new YokiFrameEngineInspectOperation(roots);
        return operation.Execute(EnginePipeline.CreateRequest(
            EnginePipeline.CliSource,
            YokiFrameEngineInspectOperation.ACTION,
            payload));
    }

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

    private sealed class FakeRootProvider : IYokiFrameInspectRoot
    {
        public string Name => "fake";

        public object Instance { get; set; }

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
