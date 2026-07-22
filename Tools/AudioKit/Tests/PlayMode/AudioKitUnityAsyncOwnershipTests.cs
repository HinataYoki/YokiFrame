#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using YokiFrame.Unity;

namespace YokiFrame.Tests
{
    /// <summary>验证 Unity AudioKit 异步资源租约在取消、调度失败和释放竞态下保持闭合。</summary>
    public sealed class AudioKitUnityAsyncOwnershipTests
    {
        private const string AUDIO_PATH = "Audio/AsyncOwnership";
        private const string BACKEND_ROOT_NAME = "YokiFrameAudioKit";

        private AudioClip mClip;
        private ControlledAudioLoader mLoader;
        private QueuedSynchronizationContext mBackendContext;
        private UnityAudioKitBackend mBackend;

        /// <summary>创建隔离资源、Loader 和可控 Unity 主线程调度队列。</summary>
        [SetUp]
        public void SetUp()
        {
            AudioKit.Reset();
            AudioKit.ClearResourceLoader();
            DestroyLeakedBackendRoot();
            mClip = AudioClip.Create("AudioKitAsyncOwnership", 64, 1, 44100, false);
            mLoader = new ControlledAudioLoader(mClip);
            AudioKit.SetResourceLoader(mLoader);
            mBackendContext = new QueuedSynchronizationContext();
            mBackend = CreateBackendWithContext(mBackendContext);
        }

        /// <summary>释放测试后端和真实 AudioClip，并清理由失败断言遗留的宿主根对象。</summary>
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            mBackend.Dispose();
            mBackendContext.RunAll();
            AudioKit.Reset();
            AudioKit.ClearResourceLoader();
            if (mClip != default) UnityEngine.Object.Destroy(mClip);
            DestroyLeakedBackendRoot();
            yield return null;
        }

        /// <summary>验证 Loader 返回资源前一刻取消时，Backend 释放新租约且不提交缓存。</summary>
        [UnityTest]
        public IEnumerator CancellationAtLoadReturnReleasesLease()
        {
            using var cancellation = new CancellationTokenSource();
            mLoader.BeforeReturn = cancellation.Cancel;

            Task<bool> pending = mBackend.PreloadAsync(AUDIO_PATH, cancellation.Token);
            mLoader.CompleteLoad();
            yield return WaitUntil(() => pending.IsCompleted, "取消后的预加载任务未进入终态。");

            Assert.IsTrue(pending.IsCanceled);
            AssertReleasedExactlyOnce();
            Assert.AreEqual(0, mBackendContext.PendingCount);
            mBackend.Unload(AUDIO_PATH);
            Assert.AreEqual(1, mLoader.ReleaseCount);
        }

        /// <summary>验证主线程提交已排队后再取消时，dispatch 拒绝缓存并释放新租约。</summary>
        [UnityTest]
        public IEnumerator CancellationBeforeDispatchReleasesLease()
        {
            using var cancellation = new CancellationTokenSource();
            Task<bool> pending = mBackend.PreloadAsync(AUDIO_PATH, cancellation.Token);

            mLoader.CompleteLoad();
            yield return WaitUntil(() => mBackendContext.PendingCount > 0, "缓存提交没有进入主线程队列。");
            cancellation.Cancel();
            mBackendContext.RunAll();
            yield return WaitUntil(() => pending.IsCompleted, "dispatch 取消后任务未进入终态。");

            Assert.IsTrue(pending.IsCanceled);
            AssertReleasedExactlyOnce();
            mBackend.Unload(AUDIO_PATH);
            Assert.AreEqual(1, mLoader.ReleaseCount);
        }

        /// <summary>验证 Dispose 后晚到的资源不会重建缓存、voice 或 Unity 宿主根对象。</summary>
        [UnityTest]
        public IEnumerator LateResultAfterDisposeDoesNotReviveBackend()
        {
            Task<int> pending = mBackend.PlayAsync(
                AUDIO_PATH,
                AudioPlayOptions.Default,
                CancellationToken.None);

            mBackend.Dispose();
            mLoader.CompleteLoad();
            yield return DrainUntilCompleted(pending);
            yield return null;

            Assert.IsTrue(pending.IsFaulted);
            Assert.IsInstanceOf<ObjectDisposedException>(pending.Exception.GetBaseException());
            AssertReleasedExactlyOnce();
            Assert.IsTrue(GameObject.Find(BACKEND_ROOT_NAME) == default);
            var voices = new List<AudioVoiceSnapshot>();
            mBackend.GetActiveVoices(voices);
            Assert.IsEmpty(voices);
        }

        /// <summary>在当前 Unity 主线程上构造后端，但让异步回切进入测试可控队列。</summary>
        private static UnityAudioKitBackend CreateBackendWithContext(SynchronizationContext context)
        {
            SynchronizationContext previous = SynchronizationContext.Current;
            try
            {
                SynchronizationContext.SetSynchronizationContext(context);
                return new UnityAudioKitBackend();
            }
            finally
            {
                SynchronizationContext.SetSynchronizationContext(previous);
            }
        }

        /// <summary>持续排空后端队列直到任务完成，用于观察错误实现可能产生的二次播放 dispatch。</summary>
        private IEnumerator DrainUntilCompleted(Task task)
        {
            for (var frame = 0; frame < 120 && !task.IsCompleted; frame++)
            {
                mBackendContext.RunAll();
                yield return null;
            }

            mBackendContext.RunAll();
            Assert.IsTrue(task.IsCompleted, "异步播放任务未在限定帧数内结束。");
        }

        /// <summary>等待条件在限定帧内成立，避免异步回归测试无界挂起。</summary>
        private static IEnumerator WaitUntil(Func<bool> condition, string timeoutMessage)
        {
            for (var frame = 0; frame < 120; frame++)
            {
                if (condition()) yield break;
                yield return null;
            }

            Assert.Fail(timeoutMessage);
        }

        /// <summary>断言资源只交还原 Loader 一次，且交还对象就是本次 Loader 返回值。</summary>
        private void AssertReleasedExactlyOnce()
        {
            Assert.AreEqual(1, mLoader.ReleaseCount);
            Assert.AreSame(mClip, mLoader.LastReleasedAsset);
        }

        /// <summary>清理同名后端根对象，保证失败用例不会污染后续测试。</summary>
        private static void DestroyLeakedBackendRoot()
        {
            GameObject root = GameObject.Find(BACKEND_ROOT_NAME);
            if (root != default) UnityEngine.Object.DestroyImmediate(root);
        }

        /// <summary>提供可精确控制返回时机和释放次数的 AudioClip Loader。</summary>
        private sealed class ControlledAudioLoader : IAudioResourceLoader
        {
            private readonly AudioClip mAsset;
            private readonly TaskCompletionSource<object> mCompletion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>创建返回指定 AudioClip 的受控 Loader。</summary>
            internal ControlledAudioLoader(AudioClip asset)
            {
                mAsset = asset;
            }

            /// <inheritdoc />
            public string LoaderName => "Unity.AsyncOwnership";

            /// <summary>获取资源返回给 Backend 前执行的竞态钩子。</summary>
            internal Action BeforeReturn { get; set; }

            /// <summary>获取 Loader 收到的 Release 次数。</summary>
            internal int ReleaseCount { get; private set; }

            /// <summary>获取最近一次交还的资源。</summary>
            internal object LastReleasedAsset { get; private set; }

            /// <inheritdoc />
            public T Load<T>(string path) where T : class => mAsset as T;

            /// <inheritdoc />
            public async Task<T> LoadAsync<T>(string path, CancellationToken token) where T : class
            {
                object asset = await mCompletion.Task.ConfigureAwait(false);
                BeforeReturn?.Invoke();
                return asset as T;
            }

            /// <summary>让等待中的异步加载返回真实 AudioClip。</summary>
            internal void CompleteLoad()
            {
                mCompletion.TrySetResult(mAsset);
            }

            /// <inheritdoc />
            public void Release(object asset)
            {
                ReleaseCount++;
                LastReleasedAsset = asset;
            }
        }

        /// <summary>保存 Post 回调并由测试显式决定执行时机的同步上下文。</summary>
        private sealed class QueuedSynchronizationContext : SynchronizationContext
        {
            private readonly ConcurrentQueue<WorkItem> mQueue = new();

            /// <summary>获取当前尚未执行的主线程回调数量。</summary>
            internal int PendingCount => mQueue.Count;

            /// <inheritdoc />
            public override void Post(SendOrPostCallback callback, object state)
            {
                mQueue.Enqueue(new WorkItem(callback, state));
            }

            /// <summary>在当前测试主线程依次执行全部已排队回调。</summary>
            internal void RunAll()
            {
                while (mQueue.TryDequeue(out WorkItem item)) item.Invoke();
            }

            /// <summary>保存一次同步上下文回调及其状态。</summary>
            private readonly struct WorkItem
            {
                private readonly SendOrPostCallback mCallback;
                private readonly object mState;

                /// <summary>创建一个待执行的同步上下文工作项。</summary>
                internal WorkItem(SendOrPostCallback callback, object state)
                {
                    mCallback = callback;
                    mState = state;
                }

                /// <summary>执行当前工作项。</summary>
                internal void Invoke()
                {
                    mCallback(mState);
                }
            }
        }
    }
}
#endif
