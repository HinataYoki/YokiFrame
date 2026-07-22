using System.Reflection;
using YokiFrame.Client;
using YokiFrame.Client.FileBridge;
using YokiFrame.Protocol.FileBridge;
using YokiFrame.Tooling.Application.Services;

namespace YokiFrame.Tooling.Application.Tests.Lifecycle;

/// <summary>
/// 覆盖宿主生命周期身份失效的异常重试与订阅者隔离。
/// </summary>
public sealed class EngineLifecycleMonitorTests
{
    /// <summary>
    /// 验证连接失效失败时不提交新身份，下一次检查会重试；失败订阅者也不会阻断后续订阅者。
    /// </summary>
    [Fact]
    public async Task IdentityChangeRetriesAfterInvalidationFailureAndIsolatesSubscribers()
    {
        var projectRoot = Path.Combine(Path.GetTempPath(), "yokiframe-lifecycle", Guid.NewGuid().ToString("N"));
        var (client, proxy) = LifecycleClientProxy.Create(projectRoot);
        using EngineLifecycleMonitor monitor = new(client);
        proxy.Generation = 1;
        monitor.SetEngine("unity-editor");
        proxy.Generation = 2;
        proxy.FailInvalidation = true;

        await monitor.CheckNowAsync();

        Assert.Equal(1, proxy.InvalidationCount);
        var successfulSubscriberCalls = 0;
        monitor.Changed += static (_, _) => throw new InvalidOperationException("subscriber failure");
        monitor.Changed += (_, _) => successfulSubscriberCalls++;
        monitor.Changed += (_, _) => monitor.Dispose();
        proxy.FailInvalidation = false;

        await monitor.CheckNowAsync();

        Assert.Equal(2, proxy.InvalidationCount);
        Assert.Equal(1, successfulSubscriberCalls);
    }

    /// <summary>
    /// 提供只实现生命周期监视器所需成员的 IYokiFrameClient 动态代理。
    /// </summary>
    public class LifecycleClientProxy : DispatchProxy
    {
        private YokiFramePaths mPaths = null!;

        /// <summary>获取或设置当前 registry generation。</summary>
        internal long Generation { get; set; }

        /// <summary>获取或设置连接失效是否返回失败任务。</summary>
        internal bool FailInvalidation { get; set; }

        /// <summary>获取连接失效调用次数。</summary>
        internal int InvalidationCount { get; private set; }

        /// <summary>创建代理和可配置记录器。</summary>
        /// <param name="projectRoot">用于构造协议路径的测试项目根。</param>
        /// <returns>接口实例与底层记录器。</returns>
        internal static (IYokiFrameClient Client, LifecycleClientProxy Proxy) Create(string projectRoot)
        {
            var client = DispatchProxy.Create<IYokiFrameClient, LifecycleClientProxy>();
            var proxy = (LifecycleClientProxy)(object)client;
            proxy.mPaths = new YokiFramePaths(projectRoot);
            return (client, proxy);
        }

        /// <summary>路由生命周期监视器使用的 Client 成员，其余成员明确拒绝。</summary>
        /// <param name="targetMethod">被调用接口方法。</param>
        /// <param name="args">调用参数。</param>
        /// <returns>模拟返回值。</returns>
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            return targetMethod?.Name switch
            {
                "get_Paths" => mPaths,
                nameof(IYokiFrameClient.ReadEngineEntries) => CreateRegistryEntries(),
                nameof(IYokiFrameClient.ReadHeartbeat) => null,
                nameof(IYokiFrameClient.InvalidateFastChannelConnectionsAsync) => InvalidateConnections(),
                _ => throw new NotSupportedException("Unexpected lifecycle client call: " + targetMethod?.Name)
            };
        }

        /// <summary>创建包含当前 generation 的单个 registry 条目。</summary>
        /// <returns>监视器可消费的 registry 列表。</returns>
        private IReadOnlyList<EngineRegistryEntry> CreateRegistryEntries()
        {
            return new[]
            {
                new EngineRegistryEntry
                {
                    EngineId = "unity-editor",
                    SessionId = "session",
                    Generation = Generation
                }
            };
        }

        /// <summary>记录连接失效，并按测试配置返回成功或失败任务。</summary>
        /// <returns>模拟异步失效结果。</returns>
        private Task InvalidateConnections()
        {
            InvalidationCount++;
            return FailInvalidation
                ? Task.FromException(new InvalidOperationException("invalidation failure"))
                : Task.CompletedTask;
        }
    }
}
