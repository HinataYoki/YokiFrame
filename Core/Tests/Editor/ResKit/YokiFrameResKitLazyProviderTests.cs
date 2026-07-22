using System.Threading;
using System.Threading.Tasks;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>验证宿主默认 Provider 只在首次真实资源调用时创建，并始终让显式 Provider 优先。</summary>
    [TestFixture]
    public sealed class YokiFrameResKitLazyProviderTests
    {
        /// <summary>为每个用例清除 Provider、默认工厂和缓存。</summary>
        [SetUp]
        public void SetUp()
        {
            ResKit.ResetForTests();
        }

        /// <summary>测试结束后撤销全部静态状态，避免默认工厂污染其它测试。</summary>
        [TearDown]
        public void TearDown()
        {
            ResKit.ResetForTests();
        }

        /// <summary>验证查询和未知对象释放不创建默认 Provider，首次同步加载只创建一次。</summary>
        [Test]
        public void DefaultProviderWaitsForFirstSynchronousLoad()
        {
            LazyResourceProvider provider = new();
            var factoryCount = 0;
            ResKit.RegisterDefaultProviderFactory(() =>
            {
                factoryCount++;
                return provider;
            });

            Assert.IsNull(ResKit.GetProvider());
            Assert.AreEqual("None", ResKit.ProviderName);
            ResKit.Release(new object());
            Assert.AreEqual(0, factoryCount);

            var first = ResKit.LoadAsset<LazyAsset>("Lazy/First");
            var second = ResKit.LoadAsset<LazyAsset>("Lazy/Second");

            Assert.AreSame(provider, ResKit.GetProvider());
            Assert.AreEqual(1, factoryCount);
            Assert.AreEqual(2, provider.SyncLoadCount);
            first.Dispose();
            second.Dispose();
        }

        /// <summary>验证用户提前设置 Provider 后，首次资源调用完全跳过宿主默认工厂。</summary>
        [Test]
        public void ExplicitProviderSkipsDefaultFactory()
        {
            LazyResourceProvider explicitProvider = new();
            var factoryCount = 0;
            ResKit.RegisterDefaultProviderFactory(() =>
            {
                factoryCount++;
                return new LazyResourceProvider();
            });
            ResKit.SetProvider(explicitProvider);

            var handle = ResKit.LoadAsset<LazyAsset>("Explicit/First");

            Assert.AreSame(explicitProvider, ResKit.GetProvider());
            Assert.AreEqual(0, factoryCount);
            Assert.AreEqual(1, explicitProvider.SyncLoadCount);
            handle.Dispose();
        }

        /// <summary>验证并发首次异步加载共享同一个默认 Provider 和同一次底层加载。</summary>
        [Test]
        public async Task ConcurrentFirstLoadsCreateDefaultProviderOnce()
        {
            LazyResourceProvider provider = new();
            provider.BeginGatedLoad();
            var factoryCount = 0;
            ResKit.RegisterDefaultProviderFactory(() =>
            {
                factoryCount++;
                return provider;
            });

            var firstTask = ResKit.LoadAssetAsync<LazyAsset>("Lazy/Async");
            var secondTask = ResKit.LoadAssetAsync<LazyAsset>("Lazy/Async");
            Assert.AreEqual(1, factoryCount);
            Assert.AreEqual(1, provider.AsyncLoadCount);

            provider.CompleteGatedLoad();
            var first = await firstTask;
            var second = await secondTask;
            first.Dispose();
            second.Dispose();
        }

        /// <summary>验证 raw API 也复用同一惰性 Provider 入口。</summary>
        [Test]
        public void RawLoadCreatesDefaultProviderOnDemand()
        {
            LazyResourceProvider provider = new();
            var factoryCount = 0;
            ResKit.RegisterDefaultProviderFactory(() =>
            {
                factoryCount++;
                return provider;
            });

            byte[] bytes = ResKit.LoadRaw("Lazy/Raw");

            CollectionAssert.AreEqual(new byte[] { 4, 2 }, bytes);
            Assert.AreSame(provider, ResKit.GetProvider());
            Assert.AreEqual(1, factoryCount);
        }

        /// <summary>用于验证默认 Provider 生命周期的最小资源对象。</summary>
        private sealed class LazyAsset
        {
        }

        /// <summary>提供同步、异步和 raw 计数的可控默认 Provider。</summary>
        private sealed class LazyResourceProvider : IResourceProvider, IRawResourceProvider
        {
            private TaskCompletionSource<LazyAsset> mLoadGate;

            /// <summary>获取稳定测试名称。</summary>
            public string ProviderName => "Lazy";

            internal int SyncLoadCount { get; private set; }
            internal int AsyncLoadCount { get; private set; }

            /// <summary>同步返回新的测试资源。</summary>
            public T Load<T>(string path) where T : class
            {
                SyncLoadCount++;
                return new LazyAsset() as T;
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
                    return new LazyAsset() as T;
                }

                LazyAsset asset = await mLoadGate.Task;
                return asset as T;
            }

            /// <summary>测试资源没有额外释放行为。</summary>
            public void Release(object asset)
            {
            }

            /// <summary>返回固定 raw bytes。</summary>
            public byte[] LoadRaw(string path) => new byte[] { 4, 2 };

            /// <summary>返回固定 raw 文本。</summary>
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
                mLoadGate = new TaskCompletionSource<LazyAsset>();
            }

            /// <summary>完成当前异步加载 gate。</summary>
            internal void CompleteGatedLoad()
            {
                mLoadGate.SetResult(new LazyAsset());
            }
        }
    }
}
