using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>验证 SceneKit 默认跟随 ResKit Provider、显式覆盖和场景生命周期。</summary>
    [TestFixture]
    public sealed partial class SceneKitRuntimeTests
    {
        private TestResourceProvider mProvider;

        /// <summary>为每个测试安装隔离 ResKit Provider。</summary>
        [SetUp]
        public void SetUp()
        {
            SceneKit.Reset();
            ResKit.ResetForTests();
            mProvider = new TestResourceProvider("Primary");
            ResKit.SetProvider(mProvider);
        }

        /// <summary>清理 SceneKit 静态状态，避免 Handler 跨测试残留。</summary>
        [TearDown]
        public void TearDown()
        {
            SceneKit.Reset();
            ResKit.ResetForTests();
        }

        /// <summary>验证首次真实场景加载会创建 ResKit 宿主默认 Provider，查询本身不会创建。</summary>
        [Test]
        public void FirstSceneLoadCreatesDefaultResKitProvider()
        {
            SceneKit.Reset();
            ResKit.ResetForTests();
            var factoryCount = 0;
            ResKit.RegisterDefaultProviderFactory(() =>
            {
                factoryCount++;
                return mProvider;
            });

            Assert.IsNull(SceneKit.GetBackend());
            Assert.AreEqual(0, factoryCount);

            SceneHandler handler = SceneKit.LoadSceneAsync("Lazy", SceneLoadMode.Additive);

            Assert.AreEqual(SceneState.Loaded, handler.State);
            Assert.AreEqual(1, factoryCount);
            Assert.AreSame(mProvider, ResKit.GetProvider());
        }

        /// <summary>验证未显式设置后端时 SceneKit 自动使用当前 ResKit Provider。</summary>
        [Test]
        public void LoadSceneUsesCurrentResKitSceneProvider()
        {
            SceneHandler handler = SceneKit.LoadSceneAsync("Gameplay", SceneLoadMode.Additive);

            Assert.AreEqual(1, mProvider.SceneRequests.Count);
            Assert.AreEqual("Gameplay", mProvider.SceneRequests[0].SceneName);
            Assert.AreEqual(SceneState.Loaded, handler.State);
            Assert.AreEqual("ResKit:Primary.Scene", SceneKit.GetBackend().BackendName);
        }

        /// <summary>验证显式 SceneKit Backend 优先于 ResKit Provider。</summary>
        [Test]
        public void ExplicitBackendOverridesResKitProvider()
        {
            var backend = new TestSceneBackend("Explicit");
            SceneKit.SetBackend(backend);

            SceneKit.LoadSceneAsync("Gameplay", SceneLoadMode.Additive);

            Assert.AreEqual(1, backend.Requests.Count);
            Assert.AreEqual(0, mProvider.SceneRequests.Count);
        }

        /// <summary>验证 Provider 切换后旧 Handler 仍由创建它的 Provider 卸载。</summary>
        [Test]
        public void ProviderSwitchKeepsExistingSceneOwnership()
        {
            SceneHandler first = SceneKit.LoadSceneAsync("First", SceneLoadMode.Additive);
            var replacement = new TestResourceProvider("Replacement");
            ResKit.SetProvider(replacement);
            SceneKit.LoadSceneAsync("Second", SceneLoadMode.Additive);

            SceneKit.UnloadSceneAsync(first);

            Assert.AreEqual(1, mProvider.UnloadedScenes.Count);
            Assert.AreEqual("First", mProvider.UnloadedScenes[0].SceneName);
            Assert.AreEqual(0, replacement.UnloadedScenes.Count);
        }

        /// <summary>验证无效 Provider 结果进入 Failed，不会被报告为已加载。</summary>
        [Test]
        public void InvalidProviderResultEntersFailedState()
        {
            mProvider.ReturnInvalidScene = true;

            SceneHandler handler = SceneKit.LoadSceneAsync("Missing", SceneLoadMode.Additive);

            Assert.AreEqual(SceneState.Failed, handler.State);
            Assert.IsFalse(SceneKit.IsSceneLoaded("Missing"));
        }

        /// <summary>验证最后一个场景可以进入后端卸载流程。</summary>
        [Test]
        public void LastSceneCanBeUnloaded()
        {
            SceneHandler handler = SceneKit.LoadSceneAsync("Only", SceneLoadMode.Additive);

            SceneKit.UnloadSceneAsync(handler);

            Assert.AreEqual(1, mProvider.UnloadedScenes.Count);
            Assert.AreEqual(SceneState.Unloaded, handler.State);
            Assert.IsFalse(SceneKit.IsSceneLoaded("Only"));
        }

        /// <summary>验证 Single 加载通过原 Handler 后端卸载已替换场景。</summary>
        [Test]
        public void SingleModeUnloadsReplacedScenes()
        {
            SceneHandler first = SceneKit.LoadSceneAsync("First", SceneLoadMode.Additive);

            SceneHandler second = SceneKit.LoadSceneAsync("Second", SceneLoadMode.Single);

            Assert.AreEqual(1, mProvider.UnloadedScenes.Count);
            Assert.AreEqual("First", mProvider.UnloadedScenes[0].SceneName);
            Assert.AreEqual(SceneState.Unloaded, first.State);
            Assert.AreEqual(SceneState.Loaded, second.State);
        }

        /// <summary>验证同步完成的预加载在显式激活前不会改变当前激活场景。</summary>
        [Test]
        public void PreloadedSceneWaitsForExplicitActivation()
        {
            SceneHandler handler = SceneKit.PreloadSceneAsync("Preloaded");

            Assert.IsNull(SceneKit.GetActiveSceneHandler());
            Assert.IsTrue(handler.IsPreloaded);

            SceneKit.ActivatePreloadedScene(handler);

            Assert.AreSame(handler, SceneKit.GetActiveSceneHandler());
            Assert.IsFalse(handler.IsPreloaded);
        }

        /// <summary>验证加载期间的多个卸载请求共享一次后端卸载，并在完成后统一回调。</summary>
        [Test]
        public void UnloadDuringLoadRunsBackendUnloadOnce()
        {
            var backend = new DeferredSceneBackend();
            SceneKit.SetBackend(backend);
            SceneHandler handler = SceneKit.LoadSceneAsync("Deferred", SceneLoadMode.Additive);
            var callbackCount = 0;

            SceneKit.UnloadSceneAsync(handler, () => callbackCount++);
            SceneKit.UnloadSceneAsync(handler, () => callbackCount++);
            var rejectedCallbackCount = 0;
            SceneHandler retry = SceneKit.LoadSceneAsync(
                "Deferred",
                SceneLoadMode.Additive,
                result => { Assert.IsNull(result); rejectedCallbackCount++; });

            Assert.AreEqual(SceneState.Unloading, handler.State);
            Assert.IsFalse(SceneKit.IsSceneLoaded("Deferred"));
            Assert.IsNull(retry);
            Assert.AreEqual(1, rejectedCallbackCount);
            Assert.AreEqual(1, backend.LoadCount);
            Assert.AreEqual(0, backend.UnloadCount);
            Assert.AreEqual(0, callbackCount);

            backend.CompleteLoad();

            Assert.AreEqual(1, backend.UnloadCount);
            Assert.AreEqual(2, callbackCount);
            Assert.AreEqual(SceneState.Unloaded, handler.State);
        }

        /// <summary>验证加载中卸载遇到无效结果时完成逻辑卸载，不调用无句柄的后端卸载。</summary>
        [Test]
        public void UnloadDuringFailedLoadCompletesCallbacks()
        {
            var backend = new DeferredSceneBackend();
            SceneKit.SetBackend(backend);
            SceneHandler handler = SceneKit.LoadSceneAsync("Missing", SceneLoadMode.Additive);
            var callbackCount = 0;

            SceneKit.UnloadSceneAsync(handler, () => callbackCount++);
            backend.CompleteLoad(false);

            Assert.AreEqual(0, backend.UnloadCount);
            Assert.AreEqual(1, callbackCount);
            Assert.AreEqual(SceneState.Unloaded, handler.State);
            Assert.IsNull(SceneKit.GetSceneHandler("Missing"));
        }

        /// <summary>验证预加载尚未挂起或完成时不会提前把无效句柄设为激活场景。</summary>
        [Test]
        public void ActivatingIncompletePreloadWaitsForProviderCompletion()
        {
            var backend = new DeferredSceneBackend();
            SceneKit.SetBackend(backend);
            SceneHandler handler = SceneKit.PreloadSceneAsync("Preloaded");

            SceneKit.ActivatePreloadedScene(handler);

            Assert.IsNull(SceneKit.GetActiveSceneHandler());
            Assert.IsTrue(handler.IsPreloaded);

            backend.CompleteLoad();

            Assert.IsNull(SceneKit.GetActiveSceneHandler());
            Assert.IsTrue(handler.IsPreloaded);

            SceneKit.ActivatePreloadedScene(handler);

            Assert.AreSame(handler, SceneKit.GetActiveSceneHandler());
            Assert.IsFalse(handler.IsPreloaded);
        }

        /// <summary>验证不支持运行中挂起的后端不会被 SceneKit 错误标记为已挂起。</summary>
        [Test]
        public void SuspendLoadReflectsOperationCapability()
        {
            var backend = new DeferredSceneBackend();
            SceneKit.SetBackend(backend);
            SceneHandler handler = SceneKit.LoadSceneAsync("Deferred", SceneLoadMode.Additive);

            SceneKit.SuspendLoad(handler);

            Assert.IsFalse(handler.IsSuspended);
        }

        /// <summary>验证宿主重复报告相同进度时只通知调用方一次，终态仍保证报告 1。</summary>
        [Test]
        public void ProgressCallbackSkipsUnchangedValues()
        {
            var backend = new DeferredSceneBackend();
            SceneKit.SetBackend(backend);
            var progressValues = new List<float>();

            SceneKit.LoadSceneAsync(
                "Deferred",
                SceneLoadMode.Additive,
                null,
                progress => progressValues.Add(progress));
            backend.ReportProgress(0f);
            backend.ReportProgress(0.5f);
            backend.ReportProgress(0.5f);
            backend.CompleteLoad();

            CollectionAssert.AreEqual(new[] { 0f, 0.5f, 1f }, progressValues);
        }

        /// <summary>验证卸载激活场景时事件携带真实旧场景，并在没有替代场景时发布空句柄。</summary>
        [Test]
        public void UnloadingActiveScenesPublishesAccurateTransitionEvents()
        {
            var receivedEvents = new List<ActiveSceneChangedEvent>();
            LinkUnRegister<ActiveSceneChangedEvent> registration = EventKit.Type.Register<ActiveSceneChangedEvent>(receivedEvents.Add);
            try
            {
                SceneHandler first = SceneKit.LoadSceneAsync("First", SceneLoadMode.Additive);
                SceneHandler second = SceneKit.LoadSceneAsync("Second", SceneLoadMode.Additive);
                receivedEvents.Clear();

                SceneKit.UnloadSceneAsync(first);

                Assert.AreEqual(1, receivedEvents.Count);
                Assert.AreEqual(first.Scene, receivedEvents[0].PreviousScene);
                Assert.AreEqual(second.Scene, receivedEvents[0].NewScene);

                SceneKit.UnloadSceneAsync(second);

                Assert.AreEqual(2, receivedEvents.Count);
                Assert.AreEqual(second.Scene, receivedEvents[1].PreviousScene);
                Assert.AreEqual(default(SceneHandle), receivedEvents[1].NewScene);
            }
            finally
            {
                registration.UnRegister();
            }
        }

        /// <summary>验证 SceneHandle 与 SceneLoadResult 在 default 状态下的空安全与值相等语义。</summary>
        [Test]
        public void SceneHandle_And_SceneLoadResult_ValueEqualityAndDefaultSafety()
        {
            SceneHandle defaultHandle = default;
            SceneHandle explicitEmpty = new(null, 0, false);
            SceneHandle validHandle = new("Gameplay", 1, true);

            // 验证 default 句柄 SceneName 永远非空
            Assert.AreEqual(string.Empty, defaultHandle.SceneName);
            Assert.AreEqual(0, defaultHandle.SceneName.Length);
            Assert.IsFalse(defaultHandle.IsValid);

            // 验证值相等性与运算符
            Assert.AreEqual(defaultHandle, explicitEmpty);
            Assert.IsTrue(defaultHandle == explicitEmpty);
            Assert.IsFalse(defaultHandle != explicitEmpty);
            Assert.IsTrue(defaultHandle != validHandle);
            Assert.AreEqual(defaultHandle.GetHashCode(), explicitEmpty.GetHashCode());

            // 验证 SceneLoadResult 值相等性
            SceneLoadResult result1 = new(validHandle);
            SceneLoadResult result2 = new(new SceneHandle("Gameplay", 1, true));
            SceneLoadResult defaultResult = default;
            Assert.IsTrue(result1.Succeeded);
            Assert.IsFalse(defaultResult.Succeeded);
            Assert.AreEqual(result1, result2);
            Assert.IsTrue(result1 == result2);
            Assert.IsFalse(result1 != result2);
            Assert.IsTrue(result1 != defaultResult);
            Assert.AreEqual(result1.GetHashCode(), result2.GetHashCode());
        }

        /// <summary>提供普通资源与场景可选能力的同步测试 Provider。</summary>
        private sealed class TestResourceProvider : IResourceProvider, IResSceneProvider
        {
            /// <summary>创建指定名称的测试 Provider。</summary>
            internal TestResourceProvider(string name)
            {
                ProviderName = name;
            }

            /// <summary>获取测试 Provider 名称。</summary>
            public string ProviderName { get; }

            /// <summary>获取测试场景后端名称。</summary>
            public string SceneBackendName => ProviderName + ".Scene";

            /// <summary>获取测试激活场景。</summary>
            public ResSceneHandle ActiveScene { get; private set; }

            internal List<ResSceneLoadRequest> SceneRequests { get; } = new();
            internal List<ResSceneHandle> UnloadedScenes { get; } = new();
            internal bool ReturnInvalidScene { get; set; }

            /// <summary>测试 Provider 不返回普通资源。</summary>
            public T Load<T>(string path) where T : class => null;

#if YOKIFRAME_UNITASK_SUPPORT
            /// <summary>测试 Provider 异步不返回普通资源。</summary>
            public UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
                => UniTask.FromResult<T>(null);
#else
            /// <summary>测试 Provider 异步不返回普通资源。</summary>
            public Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class
                => Task.FromResult<T>(null);
#endif

            /// <summary>测试资源无需释放。</summary>
            public void Release(object asset)
            {
            }

            /// <summary>同步完成测试场景加载。</summary>
            public IResSceneLoadOperation LoadSceneAsync(
                ResSceneLoadRequest request,
                Action<ResSceneLoadResult> onComplete,
                Action<float> onProgress,
                Action onSuspended)
            {
                SceneRequests.Add(request);
                var operation = new TestResSceneLoadOperation();
                operation.Complete();
                ResSceneHandle handle = new(request.SceneName, request.BuildIndex, !ReturnInvalidScene);
                if (handle.IsValid && (request.Mode == ResSceneLoadMode.Single || !ActiveScene.IsValid))
                {
                    ActiveScene = handle;
                }

                onProgress?.Invoke(1f);
                onComplete?.Invoke(new ResSceneLoadResult(handle));
                return operation;
            }

            /// <summary>记录测试场景卸载。</summary>
            public void UnloadSceneAsync(ResSceneHandle scene, Action onComplete)
            {
                UnloadedScenes.Add(scene);
                if (ActiveScene == scene)
                {
                    ActiveScene = default;
                }

                onComplete?.Invoke();
            }

            /// <summary>设置测试激活场景。</summary>
            public void SetActiveScene(ResSceneHandle scene)
            {
                ActiveScene = scene;
            }

            /// <summary>测试环境立即完成未使用资源卸载。</summary>
            public void UnloadUnusedAssets(Action onComplete)
            {
                onComplete?.Invoke();
            }
        }

        /// <summary>提供显式覆盖行为的 SceneKit 测试后端。</summary>
        private sealed class TestSceneBackend : ISceneBackend
        {
            /// <summary>创建显式测试后端。</summary>
            internal TestSceneBackend(string name)
            {
                BackendName = name;
            }

            /// <inheritdoc />
            public string BackendName { get; }

            /// <inheritdoc />
            public SceneHandle ActiveScene { get; private set; }

            internal List<SceneLoadRequest> Requests { get; } = new();

            /// <inheritdoc />
            public ISceneLoadOperation LoadSceneAsync(
                SceneLoadRequest request,
                Action<SceneLoadResult> onComplete,
                Action<float> onProgress,
                Action onSuspended)
            {
                Requests.Add(request);
                ActiveScene = new SceneHandle(request.SceneName, request.BuildIndex, true);
                onComplete?.Invoke(new SceneLoadResult(ActiveScene));
                return new TestSceneLoadOperation();
            }

            /// <inheritdoc />
            public void UnloadSceneAsync(SceneHandle scene, Action onComplete)
            {
                onComplete?.Invoke();
            }

            /// <inheritdoc />
            public void SetActiveScene(SceneHandle scene)
            {
                ActiveScene = scene;
            }

            /// <inheritdoc />
            public void UnloadUnusedAssets(Action onComplete)
            {
                onComplete?.Invoke();
            }
        }

        /// <summary>提供可控加载完成时机的后端，用于验证加载中卸载的串行化。</summary>
        private sealed class DeferredSceneBackend : ISceneBackend
        {
            private Action<SceneLoadResult> mOnComplete;
            private Action<float> mOnProgress;
            private SceneLoadRequest mRequest;

            /// <inheritdoc />
            public string BackendName => "Deferred";

            /// <inheritdoc />
            public SceneHandle ActiveScene { get; private set; }

            internal int LoadCount { get; private set; }
            internal int UnloadCount { get; private set; }

            /// <inheritdoc />
            public ISceneLoadOperation LoadSceneAsync(
                SceneLoadRequest request,
                Action<SceneLoadResult> onComplete,
                Action<float> onProgress,
                Action onSuspended)
            {
                LoadCount++;
                mRequest = request;
                mOnComplete = onComplete;
                mOnProgress = onProgress;
                onProgress?.Invoke(0f);
                return new TestSceneLoadOperation();
            }

            /// <summary>向当前等待中的加载请求报告指定进度，用于验证 SceneKit 的进度去重。</summary>
            /// <param name="progress">要模拟的加载进度。</param>
            internal void ReportProgress(float progress)
            {
                mOnProgress?.Invoke(progress);
            }

            /// <summary>完成当前等待中的场景加载。</summary>
            internal void CompleteLoad(bool isValid = true)
            {
                ActiveScene = new SceneHandle(mRequest.SceneName, mRequest.BuildIndex, isValid);
                Action<SceneLoadResult> callback = mOnComplete;
                mOnComplete = null;
                mOnProgress = null;
                callback?.Invoke(new SceneLoadResult(ActiveScene));
            }

            /// <inheritdoc />
            public void UnloadSceneAsync(SceneHandle scene, Action onComplete)
            {
                UnloadCount++;
                if (ActiveScene == scene)
                {
                    ActiveScene = default;
                }

                onComplete?.Invoke();
            }

            /// <inheritdoc />
            public void SetActiveScene(SceneHandle scene)
            {
                ActiveScene = scene;
            }

            /// <inheritdoc />
            public void UnloadUnusedAssets(Action onComplete)
            {
                onComplete?.Invoke();
            }
        }
    }
}
