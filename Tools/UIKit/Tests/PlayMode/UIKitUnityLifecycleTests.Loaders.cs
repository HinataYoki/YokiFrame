#if UNITY_2022_3_OR_NEWER
using System;
using System.Threading;
using System.Threading.Tasks;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 提供 UIKit Unity 生命周期测试共用的可控 loader 与 lease。
    /// </summary>
    public sealed partial class UIKitUnityLifecycleTests
    {
        /// <summary>提供立即完成的测试 loader，并统计 lease 释放次数。</summary>
        private sealed class ImmediatePanelLoader : IPanelLoader
        {
            private readonly GameObject mPrefab;

            /// <summary>创建绑定指定模板的 loader。</summary>
            internal ImmediatePanelLoader(GameObject prefab)
            {
                mPrefab = prefab;
            }

            internal int ReleaseCount { get; private set; }

            /// <summary>获取或设置测试 loader 的可寻址 location 模式。</summary>
            public bool UseAddressableLocation { get; set; }

            /// <summary>返回一个新的独占 lease。</summary>
            public IPanelPrefabLease Load(Type panelType)
            {
                return new TestPanelLease(panelType.Name, mPrefab, CountRelease);
            }

#if YOKIFRAME_UNITASK_SUPPORT
            /// <summary>以已完成 UniTask 返回一个新的独占 lease。</summary>
            public UniTask<IPanelPrefabLease> LoadAsync(
                Type panelType,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return UniTask.FromResult(Load(panelType));
            }
#else
            /// <summary>以已完成 Task 返回一个新的独占 lease。</summary>
            public Task<IPanelPrefabLease> LoadAsync(
                Type panelType,
                CancellationToken cancellationToken = default)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.FromResult(Load(panelType));
            }
#endif

            /// <summary>记录一个 lease 的首次释放。</summary>
            private void CountRelease()
            {
                ReleaseCount++;
            }
        }

        /// <summary>使用可由后台线程完成的 Task 驱动测试 single-flight。</summary>
        private sealed class DeferredPanelLoader : IPanelLoader
        {
            private readonly GameObject mPrefab;
            private readonly TaskCompletionSource<IPanelPrefabLease> mCompletion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>创建绑定指定模板的延迟 loader。</summary>
            internal DeferredPanelLoader(GameObject prefab)
            {
                mPrefab = prefab;
            }

            internal int AsyncLoadCount { get; private set; }

            /// <summary>获取或设置测试 loader 的可寻址 location 模式。</summary>
            public bool UseAddressableLocation { get; set; }

            /// <summary>同步路径直接返回 lease，本测试不会调用该路径。</summary>
            public IPanelPrefabLease Load(Type panelType)
            {
                return new TestPanelLease(panelType.Name, mPrefab, static () => { });
            }

#if YOKIFRAME_UNITASK_SUPPORT
            /// <summary>先切到线程池，再返回由测试显式完成且不捕获 Unity 上下文的 UniTask。</summary>
            public async UniTask<IPanelPrefabLease> LoadAsync(
                Type panelType,
                CancellationToken cancellationToken = default)
            {
                AsyncLoadCount++;
                await UniTask.SwitchToThreadPool();
                return await mCompletion.Task
                    .AsUniTask(useCurrentSynchronizationContext: false)
                    .AttachExternalCancellation(cancellationToken);
            }
#else
            /// <summary>返回由测试显式完成的 Task。</summary>
            public async Task<IPanelPrefabLease> LoadAsync(
                Type panelType,
                CancellationToken cancellationToken = default)
            {
                AsyncLoadCount++;
                Task completed = await Task.WhenAny(mCompletion.Task, Task.Delay(Timeout.Infinite, cancellationToken));
                cancellationToken.ThrowIfCancellationRequested();
                return await (Task<IPanelPrefabLease>)completed;
            }
#endif

            /// <summary>从任意线程完成底层资源任务，不执行 Unity API。</summary>
            internal void Complete()
            {
                mCompletion.TrySetResult(new TestPanelLease("Deferred", mPrefab, static () => { }));
            }
        }

        /// <summary>模拟忽略共享取消令牌的底层 loader，用于验证 Root 销毁后的迟到终态。</summary>
        private sealed class IgnoringCancellationPanelLoader : IPanelLoader
        {
            private readonly GameObject mPrefab;
            private readonly TaskCompletionSource<IPanelPrefabLease> mCompletion =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            /// <summary>创建绑定模板且不会响应取消令牌的延迟 loader。</summary>
            internal IgnoringCancellationPanelLoader(GameObject prefab)
            {
                mPrefab = prefab;
            }

            internal int AsyncLoadCount { get; private set; }
            internal int LeaseReleaseCount { get; private set; }

            /// <summary>获取或设置测试 loader 的可寻址 location 模式。</summary>
            public bool UseAddressableLocation { get; set; }

            /// <summary>同步路径返回独占 lease；本测试只走异步路径。</summary>
            public IPanelPrefabLease Load(Type panelType)
            {
                return CreateLease(panelType);
            }

#if YOKIFRAME_UNITASK_SUPPORT
            /// <summary>忽略取消令牌并等待显式完成且不捕获 Unity 上下文的 UniTask loader。</summary>
            public async UniTask<IPanelPrefabLease> LoadAsync(
                Type panelType,
                CancellationToken cancellationToken = default)
            {
                AsyncLoadCount++;
                return await mCompletion.Task.AsUniTask(useCurrentSynchronizationContext: false);
            }
#else
            /// <summary>忽略取消令牌并等待显式完成的 Task loader。</summary>
            public async Task<IPanelPrefabLease> LoadAsync(
                Type panelType,
                CancellationToken cancellationToken = default)
            {
                AsyncLoadCount++;
                return await mCompletion.Task;
            }
#endif

            /// <summary>完成迟到底层加载，使旧 flight 进入 lease 清理路径。</summary>
            internal void Complete()
            {
                mCompletion.TrySetResult(new TestPanelLease(
                    "IgnoringCancellation",
                    mPrefab,
                    () => LeaseReleaseCount++));
            }

            /// <summary>以指定异常完成迟到 loader，用于验证取消后的诊断保留。</summary>
            /// <param name="exception">loader 返回的失败。</param>
            internal void Fail(Exception exception)
            {
                mCompletion.TrySetException(exception);
            }

            /// <summary>返回一个在释放时抛出指定异常的迟到 lease。</summary>
            /// <param name="exception">lease 释放异常。</param>
            internal void CompleteWithThrowingLease(Exception exception)
            {
                mCompletion.TrySetResult(new TestPanelLease(
                    "ThrowingRelease",
                    mPrefab,
                    () => throw exception));
            }

            /// <summary>为一次异步加载创建独占测试 lease。</summary>
            private IPanelPrefabLease CreateLease(Type panelType)
            {
                return new TestPanelLease(panelType.Name, mPrefab, () => LeaseReleaseCount++);
            }
        }

        /// <summary>测试用幂等 Prefab lease。</summary>
        private sealed class TestPanelLease : IPanelPrefabLease
        {
            private readonly Action mOnDispose;
            private bool mDisposed;

            /// <summary>创建绑定模板和释放回调的 lease。</summary>
            internal TestPanelLease(string location, GameObject prefab, Action onDispose)
            {
                Location = location;
                Prefab = prefab;
                mOnDispose = onDispose;
            }

            public string Location { get; }
            public GameObject Prefab { get; }

            /// <summary>仅在第一次调用时报告释放。</summary>
            public void Dispose()
            {
                if (mDisposed) return;
                mDisposed = true;
                mOnDispose();
            }
        }
    }
}
#endif
