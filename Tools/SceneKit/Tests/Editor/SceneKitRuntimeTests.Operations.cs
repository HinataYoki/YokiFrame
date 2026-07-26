using System;
using NUnit.Framework;

namespace YokiFrame.Tests
{
    /// <summary>承载 SceneKit Runtime 测试复用的同步操作桩，避免测试主文件承担无关实现细节。</summary>
    public sealed partial class SceneKitRuntimeTests
    {
        /// <summary>验证卸载挂起预加载会先恢复操作，并在完成后合并为一次后端卸载。</summary>
        [Test]
        public void UnloadSuspendedPreloadResumesAndUnloadsOnce()
        {
            SceneHandler activeHandler = SceneKit.LoadSceneAsync("Active", SceneLoadMode.Additive);
            var backend = new SuspendedSceneBackend();
            SceneKit.SetBackend(backend);
            SceneHandler handler = SceneKit.PreloadSceneAsync("Preloaded");
            var callbackCount = 0;

            SceneKit.UnloadSceneAsync(handler, () => callbackCount++);
            SceneKit.UnloadSceneAsync(handler, () => callbackCount++);

            Assert.AreEqual(SceneState.Unloading, handler.State);
            Assert.AreEqual(1, backend.Operation.ResumeCount);
            Assert.IsFalse(backend.Operation.IsSuspended);
            backend.CompleteLoad();

            Assert.AreEqual(1, backend.UnloadCount);
            Assert.AreEqual(2, callbackCount);
            Assert.AreEqual(SceneState.Unloaded, handler.State);
            Assert.AreSame(activeHandler, SceneKit.GetActiveSceneHandler());
            Assert.IsFalse(SceneKit.IsTransitioning);
        }

        /// <summary>验证已有激活场景时，挂起预加载的激活意图会在加载完成后执行。</summary>
        [Test]
        public void ActivateSuspendedAdditivePreloadBecomesActiveAfterCompletion()
        {
            SceneHandler previous = SceneKit.LoadSceneAsync("Active", SceneLoadMode.Additive);
            var backend = new SuspendedSceneBackend();
            SceneKit.SetBackend(backend);
            SceneHandler handler = SceneKit.PreloadSceneAsync("Preloaded");

            SceneKit.ActivatePreloadedScene(handler);

            Assert.AreEqual(1, backend.Operation.ResumeCount);
            Assert.AreSame(previous, SceneKit.GetActiveSceneHandler());
            backend.CompleteLoad();

            Assert.AreEqual(SceneState.Loaded, handler.State);
            Assert.AreSame(handler, SceneKit.GetActiveSceneHandler());
            Assert.AreEqual(handler.Scene, backend.ActiveScene);
            Assert.AreEqual(1, backend.SetActiveCount);
            Assert.IsFalse(handler.IsPreloaded);
        }

        /// <summary>表示测试 Provider 的同步已完成操作。</summary>
        private sealed class TestResSceneLoadOperation : IResSceneLoadOperation
        {
            /// <inheritdoc />
            public float Progress { get; private set; }

            /// <inheritdoc />
            public bool IsSuspended { get; private set; }

            /// <summary>标记操作完成。</summary>
            internal void Complete()
            {
                Progress = 1f;
            }

            /// <inheritdoc />
            public void SuspendLoad() => IsSuspended = true;

            /// <inheritdoc />
            public void ResumeLoad() => IsSuspended = false;

            /// <inheritdoc />
            public void Recycle()
            {
                Progress = 0f;
                IsSuspended = false;
            }
        }

        /// <summary>表示显式 SceneKit 测试后端的空操作。</summary>
        private sealed class TestSceneLoadOperation : ISceneLoadOperation
        {
            /// <inheritdoc />
            public float Progress => 1f;

            /// <inheritdoc />
            public bool IsSuspended => false;

            /// <inheritdoc />
            public void SuspendLoad()
            {
            }

            /// <inheritdoc />
            public void ResumeLoad()
            {
            }

            /// <inheritdoc />
            public void Recycle()
            {
            }
        }

        /// <summary>提供会真实保持挂起状态的可控场景后端。</summary>
        private sealed class SuspendedSceneBackend : ISceneBackend
        {
            private Action<SceneLoadResult> mOnComplete;
            private SceneLoadRequest mRequest;

            /// <inheritdoc />
            public string BackendName => "Suspended";

            /// <inheritdoc />
            public SceneHandle ActiveScene { get; private set; }

            /// <summary>获取当前可控挂起操作。</summary>
            internal SuspendedSceneLoadOperation Operation { get; private set; }

            /// <summary>获取后端卸载次数。</summary>
            internal int UnloadCount { get; private set; }

            /// <summary>获取后端激活次数。</summary>
            internal int SetActiveCount { get; private set; }

            /// <inheritdoc />
            public ISceneLoadOperation LoadSceneAsync(
                SceneLoadRequest request,
                Action<SceneLoadResult> onComplete,
                Action<float> onProgress,
                Action onSuspended)
            {
                mRequest = request;
                mOnComplete = onComplete;
                Operation = new SuspendedSceneLoadOperation();
                onProgress?.Invoke(Operation.Progress);
                onSuspended?.Invoke();
                return Operation;
            }

            /// <summary>在操作已恢复后完成场景加载，模拟宿主异步加载的下一帧终态。</summary>
            internal void CompleteLoad()
            {
                if (Operation.IsSuspended)
                {
                    throw new InvalidOperationException("挂起操作必须先恢复才能完成加载。");
                }

                SceneHandle scene = new(mRequest.SceneName, mRequest.BuildIndex, true);
                Action<SceneLoadResult> callback = mOnComplete;
                mOnComplete = null;
                callback?.Invoke(new SceneLoadResult(scene));
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
                SetActiveCount++;
                ActiveScene = scene;
            }

            /// <inheritdoc />
            public void UnloadUnusedAssets(Action onComplete)
            {
                onComplete?.Invoke();
            }
        }

        /// <summary>记录真实挂起、恢复和回收状态的可控加载操作。</summary>
        private sealed class SuspendedSceneLoadOperation : ISceneLoadOperation
        {
            /// <inheritdoc />
            public float Progress { get; private set; } = 0.9f;

            /// <inheritdoc />
            public bool IsSuspended { get; private set; } = true;

            /// <summary>获取有效恢复次数。</summary>
            internal int ResumeCount { get; private set; }

            /// <inheritdoc />
            public void SuspendLoad()
            {
                IsSuspended = true;
            }

            /// <inheritdoc />
            public void ResumeLoad()
            {
                if (!IsSuspended)
                {
                    return;
                }

                ResumeCount++;
                IsSuspended = false;
                Progress = 1f;
            }

            /// <inheritdoc />
            public void Recycle()
            {
                IsSuspended = false;
                Progress = 0f;
            }
        }
    }
}
