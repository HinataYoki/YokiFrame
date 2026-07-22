using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 验证新版 ResKit 的独立租约、并发加载、Provider 代际和释放所有权。
    /// </summary>
    [TestFixture]
    public sealed class YokiFrameResKitRuntimeTests
    {
        private TestResourceProvider mProvider;

        /// <summary>为每个测试安装隔离 Provider 并清空静态状态。</summary>
        [SetUp]
        public void SetUp()
        {
            ResKit.ResetForTests();
            mProvider = new TestResourceProvider("Primary");
            ResKit.SetProvider(mProvider);
        }

        /// <summary>测试结束后强制清理全部租约，避免静态缓存污染其它测试。</summary>
        [TearDown]
        public void TearDown()
        {
            ResKit.ResetForTests();
        }

        /// <summary>验证同 key 共享底层资源，但每次获取返回独立且幂等释放的句柄。</summary>
        [Test]
        public void LoadAssetCreatesIndependentIdempotentLeases()
        {
            var first = ResKit.LoadAsset<TestAsset>("Configs/Main");
            var second = ResKit.LoadAsset<TestAsset>("Configs/Main");

            Assert.AreNotSame(first, second);
            Assert.AreSame(first.Asset, second.Asset);
            Assert.AreEqual(1, mProvider.SyncLoadCount);
            Assert.AreEqual(2, ResKit.TotalRefCount);

            first.Dispose();
            first.Dispose();

            Assert.IsNull(first.Asset);
            Assert.IsNotNull(second.Asset);
            Assert.AreEqual(1, ResKit.TotalRefCount);
            Assert.AreEqual(0, mProvider.ReleaseCount);

            second.Dispose();
            Assert.AreEqual(0, ResKit.LoadedCount);
            Assert.AreEqual(1, mProvider.ReleaseCount);
        }

        /// <summary>验证对象式 API 每次只释放一个已登记匿名租约，未知对象不会交给 Provider。</summary>
        [Test]
        public void ReleaseObjectConsumesOnlyRegisteredAnonymousLeases()
        {
            var first = ResKit.Load<TestAsset>("Configs/Main");
            var second = ResKit.Load<TestAsset>("Configs/Main");

            ResKit.Release(first);
            Assert.AreEqual(1, ResKit.TotalRefCount);
            Assert.AreEqual(0, mProvider.ReleaseCount);

            ResKit.Release(second);
            ResKit.Release(second);

            Assert.AreEqual(0, ResKit.TotalRefCount);
            Assert.AreEqual(1, mProvider.ReleaseCount);
        }

        /// <summary>验证相同 key 的并发异步获取共享一次 Provider 加载并产生独立租约。</summary>
        [Test]
        public async Task ConcurrentAsyncLoadsUseSingleFlight()
        {
            mProvider.BeginGatedLoad();
            var firstTask = ResKit.LoadAssetAsync<TestAsset>("Configs/Async");
            var secondTask = ResKit.LoadAssetAsync<TestAsset>("Configs/Async");

            Assert.AreEqual(1, mProvider.AsyncLoadCount);
            mProvider.CompleteGatedLoad();

            var first = await firstTask;
            var second = await secondTask;
            Assert.AreNotSame(first, second);
            Assert.AreSame(first.Asset, second.Asset);
            Assert.AreEqual(2, ResKit.TotalRefCount);

            first.Dispose();
            second.Dispose();
            Assert.AreEqual(1, mProvider.ReleaseCount);
        }

        /// <summary>验证单个等待者取消不会取消同 key 的共享底层加载或其它等待者。</summary>
        [Test]
        public async Task CancellingOneWaiterDoesNotCancelSharedLoad()
        {
            mProvider.BeginGatedLoad();
            using CancellationTokenSource cancellation = new();
            var cancelledTask = ResKit.LoadAssetAsync<TestAsset>("Configs/Async", cancellation.Token);
            var survivingTask = ResKit.LoadAssetAsync<TestAsset>("Configs/Async");

            cancellation.Cancel();
            bool cancelled = false;
            try
            {
                await cancelledTask;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled);
            mProvider.CompleteGatedLoad();

            var surviving = await survivingTask;
            Assert.IsNotNull(surviving.Asset);
            Assert.AreEqual(1, mProvider.AsyncLoadCount);
            Assert.AreEqual(1, ResKit.TotalRefCount);

            surviving.Dispose();
        }

        /// <summary>验证 Provider 切换后旧异步结果不会回写，并由创建它的旧 Provider 释放。</summary>
        [Test]
        public async Task ProviderSwitchRejectsStaleAsyncCompletion()
        {
            mProvider.BeginGatedLoad();
            var staleTask = ResKit.LoadAssetAsync<TestAsset>("Configs/Stale");
            TestResourceProvider replacement = new("Replacement");

            ResKit.SetProvider(replacement);
#if YOKIFRAME_UNITASK_SUPPORT
            await ObserveStaleWithinTimeout(staleTask.AsTask());
#else
            await ObserveStaleWithinTimeout(staleTask);
#endif
            Assert.AreEqual(0, mProvider.ReleaseCount);
            mProvider.CompleteGatedLoad();
            await WaitForReleaseCount(mProvider, 1);
            Assert.AreEqual(1, mProvider.ReleaseCount);
            Assert.AreEqual(0, replacement.ReleaseCount);
            Assert.AreEqual(0, ResKit.LoadedCount);
            Assert.AreSame(replacement, ResKit.GetProvider());
        }

        /// <summary>验证 ClearAll 撤销全部句柄，并确保旧句柄后续释放不会触发二次释放。</summary>
        [Test]
        public void ClearAllInvalidatesAllLeasesWithoutDoubleRelease()
        {
            var first = ResKit.LoadAsset<TestAsset>("Configs/First");
            var second = ResKit.LoadAsset<TestAsset>("Configs/Second");

            ResKit.ClearAll();

            Assert.IsNull(first.Asset);
            Assert.IsNull(second.Asset);
            Assert.AreEqual(0, ResKit.LoadedCount);
            Assert.AreEqual(2, mProvider.ReleaseCount);

            first.Dispose();
            second.Dispose();
            Assert.AreEqual(2, mProvider.ReleaseCount);
        }

        /// <summary>验证 raw 能力委托到 Provider，缺少能力时明确拒绝。</summary>
        [Test]
        public async Task RawResourcesUseOptionalProviderCapability()
        {
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, ResKit.LoadRaw("Tables/Main"));
            Assert.AreEqual("raw:Tables/Main", ResKit.LoadRawText("Tables/Main"));
            CollectionAssert.AreEqual(new byte[] { 1, 2, 3 }, await ResKit.LoadRawAsync("Tables/Main"));
            Assert.AreEqual("raw:Tables/Main", await ResKit.LoadRawTextAsync("Tables/Main"));

            ResKit.SetProvider(new AssetOnlyProvider());
            Assert.Throws<NotSupportedException>(() => ResKit.LoadRaw("Tables/Main"));
        }

        /// <summary>验证 Task 契约消费者可复用 ResKit single-flight，而不直接引用可选 UniTask 类型。</summary>
        [Test]
        public async Task TaskBridgeLoadsAndRegistersAnonymousLease()
        {
            TestAsset asset = await ResKit.LoadTaskAsync<TestAsset>("Configs/TaskBridge");

            Assert.IsNotNull(asset);
            Assert.AreEqual(1, mProvider.AsyncLoadCount);
            Assert.AreEqual(1, ResKit.TotalRefCount);

            ResKit.Release(asset);
            Assert.AreEqual(1, mProvider.ReleaseCount);
        }

        /// <summary>验证全部等待者取消后，忽略取消的旧 Provider 结果仍会被精确释放一次。</summary>
        [Test]
        public async Task CancellingAllWaitersReleasesLateProviderResult()
        {
            mProvider.BeginGatedLoad();
            using CancellationTokenSource firstCancellation = new();
            using CancellationTokenSource secondCancellation = new();
            var firstTask = ResKit.LoadAssetAsync<TestAsset>("Configs/Cancelled", firstCancellation.Token);
            var secondTask = ResKit.LoadAssetAsync<TestAsset>("Configs/Cancelled", secondCancellation.Token);

            firstCancellation.Cancel();
            secondCancellation.Cancel();
            await ObserveCancellation(firstTask);
            await ObserveCancellation(secondTask);
            mProvider.CompleteGatedLoad();

            for (var index = 0; index < 8 && mProvider.ReleaseCount == 0; index++) await Task.Yield();
            Assert.AreEqual(1, mProvider.ReleaseCount);
            Assert.AreEqual(0, ResKit.LoadedCount);
        }

        /// <summary>验证 ClearAll 遇到 Provider 异常仍尝试释放全部条目并清空 Core 状态。</summary>
        [Test]
        public void ClearAllContinuesAfterEveryReleaseFailure()
        {
            ResKit.LoadAsset<TestAsset>("Configs/First");
            ResKit.LoadAsset<TestAsset>("Configs/Second");
            ResKit.LoadAsset<TestAsset>("Configs/Third");
            mProvider.ThrowOnRelease = true;

            AggregateException exception = Assert.Throws<AggregateException>(() => ResKit.ClearAll());

            Assert.AreEqual(3, exception.InnerExceptions.Count);
            Assert.AreEqual(3, mProvider.ReleaseCount);
            Assert.AreEqual(0, ResKit.LoadedCount);
            Assert.AreEqual(0, ResKit.TotalRefCount);
        }

        /// <summary>验证卸载历史使用固定容量并按最新记录优先返回隔离副本。</summary>
        [Test]
        public void UnloadHistoryIsBoundedAndNewestFirst()
        {
            for (var index = 0; index < 105; index++)
            {
                var handle = ResKit.LoadAsset<TestAsset>("Configs/" + index);
                handle.Dispose();
            }

            List<ResUnloadRecord> history = new();
            ResKit.GetUnloadHistory(history);

            Assert.AreEqual(ResKit.MAX_UNLOAD_HISTORY, history.Count);
            Assert.AreEqual("Configs/104", history[0].Path);
            Assert.AreEqual("Configs/5", history[history.Count - 1].Path);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>等待 UniTask 进入取消终态，避免同步 ThrowsAsync 阻塞 Unity 上下文。</summary>
        private static async UniTask ObserveCancellation(UniTask<ResHandle<TestAsset>> task)
#else
        /// <summary>等待 Task 进入取消终态，避免同步 ThrowsAsync 阻塞 Unity 上下文。</summary>
        private static async Task ObserveCancellation(Task<ResHandle<TestAsset>> task)
#endif
        {
            bool cancelled = false;
            try
            {
                await task;
            }
            catch (OperationCanceledException)
            {
                cancelled = true;
            }

            Assert.IsTrue(cancelled);
        }

        /// <summary>要求旧 pending 在 Provider 返回前及时进入 stale 终态，避免回归为无限等待。</summary>
        private static async Task ObserveStaleWithinTimeout(Task<ResHandle<TestAsset>> task)
        {
            Task completed = await Task.WhenAny(task, Task.Delay(1000));
            Assert.AreSame(task, completed, "Provider 切换后旧 ResKit 等待者必须立即收到 stale。");
            Assert.Throws<InvalidOperationException>(() => task.GetAwaiter().GetResult());
        }

        /// <summary>等待忽略取消的 Provider 返回晚到结果并由原 Provider 完成释放。</summary>
        private static async Task WaitForReleaseCount(TestResourceProvider provider, int expectedCount)
        {
            for (var index = 0; index < 16 && provider.ReleaseCount < expectedCount; index++)
            {
                await Task.Yield();
            }
        }

        /// <summary>用于验证资源引用相等和释放所有权的测试资源。</summary>
        private sealed class TestAsset
        {
        }

        /// <summary>提供可控同步、异步、raw 和释放计数的测试 Provider。</summary>
        private sealed class TestResourceProvider : IResourceProvider, IRawResourceProvider
        {
            private TaskCompletionSource<TestAsset> mLoadGate;

            /// <summary>创建指定名称的测试 Provider。</summary>
            /// <param name="providerName">诊断使用的 Provider 名称。</param>
            internal TestResourceProvider(string providerName)
            {
                ProviderName = providerName;
            }

            /// <summary>获取测试 Provider 名称。</summary>
            public string ProviderName { get; }

            internal int SyncLoadCount { get; private set; }
            internal int AsyncLoadCount { get; private set; }
            internal int ReleaseCount { get; private set; }
            internal bool ThrowOnRelease { get; set; }

            /// <summary>同步创建一个测试资源。</summary>
            public T Load<T>(string path) where T : class
            {
                SyncLoadCount++;
                return new TestAsset() as T;
            }

#if YOKIFRAME_UNITASK_SUPPORT
            /// <summary>等待可控 gate 后异步返回测试资源。</summary>
            public async UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#else
            /// <summary>等待可控 gate 后异步返回测试资源。</summary>
            public async Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
#endif
            {
                AsyncLoadCount++;
                if (mLoadGate == null)
                {
                    return new TestAsset() as T;
                }

                var asset = await mLoadGate.Task;
                return asset as T;
            }

            /// <summary>记录底层资源最终释放次数。</summary>
            public void Release(object asset)
            {
                ReleaseCount++;
                if (ThrowOnRelease) throw new InvalidOperationException("Expected release failure.");
            }

            /// <summary>返回固定 raw bytes。</summary>
            public byte[] LoadRaw(string path) => new byte[] { 1, 2, 3 };

            /// <summary>返回包含路径的固定 raw 文本。</summary>
            public string LoadRawText(string path) => "raw:" + path;

#if YOKIFRAME_UNITASK_SUPPORT
            /// <summary>异步返回固定 raw bytes。</summary>
            public UniTask<byte[]> LoadRawAsync(string path, CancellationToken token = default)
                => UniTask.FromResult(LoadRaw(path));

            /// <summary>异步返回固定 raw 文本。</summary>
            public UniTask<string> LoadRawTextAsync(string path, CancellationToken token = default)
                => UniTask.FromResult(LoadRawText(path));
#else
            /// <summary>异步返回固定 raw bytes。</summary>
            public Task<byte[]> LoadRawAsync(string path, CancellationToken token = default)
                => Task.FromResult(LoadRaw(path));

            /// <summary>异步返回固定 raw 文本。</summary>
            public Task<string> LoadRawTextAsync(string path, CancellationToken token = default)
                => Task.FromResult(LoadRawText(path));
#endif

            /// <summary>让后续异步加载等待显式完成。</summary>
            internal void BeginGatedLoad()
            {
                mLoadGate = new TaskCompletionSource<TestAsset>();
            }

            /// <summary>完成当前异步加载 gate。</summary>
            internal void CompleteGatedLoad()
            {
                mLoadGate.SetResult(new TestAsset());
            }
        }

        /// <summary>验证 raw capability 缺失路径的最小资源 Provider。</summary>
        private sealed class AssetOnlyProvider : IResourceProvider
        {
            /// <summary>获取测试 Provider 名称。</summary>
            public string ProviderName => "AssetOnly";

            /// <summary>该 Provider 不返回普通资源。</summary>
            public T Load<T>(string path) where T : class => null;

#if YOKIFRAME_UNITASK_SUPPORT
            /// <summary>该 Provider 异步不返回普通资源。</summary>
            public UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
                => UniTask.FromResult<T>(null);
#else
            /// <summary>该 Provider 异步不返回普通资源。</summary>
            public Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
                => Task.FromResult<T>(null);
#endif

            /// <summary>空资源无需释放。</summary>
            public void Release(object asset)
            {
            }
        }
    }
}
