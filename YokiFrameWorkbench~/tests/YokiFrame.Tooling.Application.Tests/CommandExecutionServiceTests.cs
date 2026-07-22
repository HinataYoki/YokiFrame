using System.Reflection;

namespace YokiFrame.Tooling.Application.Tests;

/// <summary>
/// 覆盖 Workbench 与 CLI 共用的命令执行用例，确保入口层不会各自实现传输选择。
/// </summary>
public sealed class CommandExecutionServiceTests
{
    private const string SERVICE_TYPE_NAME = "YokiFrame.Tooling.Application.Services.CommandExecutionService";

    /// <summary>
    /// 验证无 payload 的 System/ping 优先使用 FastChannel，并向 CLI 暴露明确传输类型。
    /// </summary>
    [Fact]
    public async Task EmptySystemPingUsesFastChannel()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();

        var result = await ExecuteAsync(
            recorder,
            "System",
            "ping",
            "{}",
            "cli",
            2500);

        Assert.Equal(1, recorder.FastChannelCallCount);
        Assert.Equal(0, recorder.FileBridgeCallCount);
        Assert.Equal("fast-channel", ReadProperty<string>(result, "Transport"));
        Assert.Equal(string.Empty, ReadProperty<string>(result, "CommandPath"));
        Assert.Equal(string.Empty, ReadProperty<string>(result, "ResponsePath"));
    }

    /// <summary>
    /// 验证 Host 声明为只读的 System 命令即使携带查询 payload 也走 FastChannel，并保留 payload。
    /// </summary>
    [Fact]
    public async Task SystemPingWithPayloadUsesFastChannel()
    {
        var recorder = WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy.Create();

        var result = await ExecuteAsync(
            recorder,
            "System",
            "ping",
            "{\"detail\":true}",
            "cli",
            2500);

        Assert.Equal(1, recorder.FastChannelCallCount);
        Assert.Equal(0, recorder.FileBridgeCallCount);
        Assert.Equal("{\"detail\":true}", recorder.LastFastChannelPayloadJson);
        Assert.Equal("fast-channel", ReadProperty<string>(result, "Transport"));
        Assert.Equal(string.Empty, ReadProperty<string>(result, "CommandPath"));
        Assert.Equal(string.Empty, ReadProperty<string>(result, "ResponsePath"));
    }

    /// <summary>
    /// 通过反射调用尚未存在的共享 Application 用例，使首轮测试以缺失行为失败，而不是依赖生产类型编译通过。
    /// </summary>
    /// <param name="recorder">记录 Client 通道调用的测试代理。</param>
    /// <param name="kit">目标 Kit。</param>
    /// <param name="action">目标 action。</param>
    /// <param name="payloadJson">命令 payload。</param>
    /// <param name="source">审计来源。</param>
    /// <param name="timeoutMs">命令超时。</param>
    /// <returns>共享命令执行结果实例。</returns>
    private static async Task<object> ExecuteAsync(
        WorkbenchFastChannelCommandTests.RecordingYokiFrameClientProxy recorder,
        string kit,
        string action,
        string payloadJson,
        string source,
        int timeoutMs)
    {
        var assembly = typeof(global::YokiFrame.Tooling.Application.Services.WorkbenchDashboardService).Assembly;
        var serviceType = assembly.GetType(SERVICE_TYPE_NAME);
        Assert.NotNull(serviceType);
        var service = Activator.CreateInstance(serviceType, recorder.Client);
        Assert.NotNull(service);
        var method = serviceType.GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.Public);
        Assert.NotNull(method);
        var invocation = method.Invoke(service, new object?[]
        {
            "unity-editor",
            kit,
            action,
            payloadJson,
            source,
            timeoutMs,
            CancellationToken.None
        });
        var task = Assert.IsAssignableFrom<Task>(invocation);
        await task;
        var result = task.GetType().GetProperty("Result")?.GetValue(task);
        return Assert.IsAssignableFrom<object>(result);
    }

    /// <summary>
    /// 从共享结果对象读取公开属性，并保留清晰的断言失败信息。
    /// </summary>
    /// <typeparam name="T">期望属性类型。</typeparam>
    /// <param name="instance">命令执行结果。</param>
    /// <param name="propertyName">公开属性名。</param>
    /// <returns>属性值。</returns>
    private static T ReadProperty<T>(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        Assert.NotNull(property);
        return Assert.IsType<T>(property.GetValue(instance));
    }
}
