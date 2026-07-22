using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace YokiFrame.Tests
{
    /// <summary>
    /// 为 UIKit EditMode 测试提供可计数、可门控的内存 Prefab loader。
    /// </summary>
    internal sealed class UIKitTestPanelLoader : IPanelLoader, IDisposable
    {
        private readonly Dictionary<Type, GameObject> mPrefabs = new();
        private TaskCompletionSource<bool> mAsyncGate;
        private bool mDisposed;

        /// <summary>获取同步底层加载次数。</summary>
        internal int SyncLoadCount { get; private set; }

        /// <summary>获取异步底层加载次数。</summary>
        internal int AsyncLoadCount { get; private set; }

        /// <summary>获取已创建的独占 lease 数量。</summary>
        internal int LeaseCount { get; private set; }

        /// <summary>获取已释放的独占 lease 数量。</summary>
        internal int LeaseDisposeCount { get; private set; }

        /// <summary>
        /// 获取或设置测试 loader 的可寻址 location 模式；内存测试 Prefab 不依赖实际资源路径。
        /// </summary>
        public bool UseAddressableLocation { get; set; }

        /// <summary>获取或设置下一次异步加载开始时执行的一次性重入回调。</summary>
        internal Action AsyncLoadStarted { get; set; }

        /// <summary>
        /// 同步返回指定面板类型对应的内存 Prefab lease。
        /// </summary>
        /// <param name="panelType">需要物化的具体面板类型。</param>
        /// <returns>当前调用独占的测试 lease。</returns>
        public IPanelPrefabLease Load(Type panelType)
        {
            ThrowIfDisposed();
            SyncLoadCount++;
            return CreateLease(panelType);
        }

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>
        /// 等待可控 gate 后返回指定面板类型的内存 Prefab lease。
        /// </summary>
        /// <param name="panelType">需要物化的具体面板类型。</param>
        /// <param name="cancellationToken">共享底层加载的取消令牌。</param>
        /// <returns>当前底层加载独占的测试 lease。</returns>
        public async UniTask<IPanelPrefabLease> LoadAsync(
            Type panelType,
            CancellationToken cancellationToken = default)
#else
        /// <summary>
        /// 等待可控 gate 后返回指定面板类型的内存 Prefab lease。
        /// </summary>
        /// <param name="panelType">需要物化的具体面板类型。</param>
        /// <param name="cancellationToken">共享底层加载的取消令牌。</param>
        /// <returns>当前底层加载独占的测试 lease。</returns>
        public async Task<IPanelPrefabLease> LoadAsync(
            Type panelType,
            CancellationToken cancellationToken = default)
#endif
        {
            ThrowIfDisposed();
            AsyncLoadCount++;
            Action loadStarted = AsyncLoadStarted;
            AsyncLoadStarted = null;
            if (loadStarted != null) loadStarted();
            TaskCompletionSource<bool> gate = mAsyncGate;
            if (gate != null) await gate.Task;
            cancellationToken.ThrowIfCancellationRequested();
            return CreateLease(panelType);
        }

        /// <summary>
        /// 让下一次真实异步底层加载等待显式完成，用于稳定观察 single-flight。
        /// </summary>
        internal void BeginAsyncGate()
        {
            ThrowIfDisposed();
            if (mAsyncGate != null) throw new InvalidOperationException("An async test gate is already active.");
            mAsyncGate = new TaskCompletionSource<bool>();
        }

        /// <summary>
        /// 完成当前异步加载 gate，使共享物化继续在 Unity 测试上下文执行。
        /// </summary>
        internal void CompleteAsyncGate()
        {
            TaskCompletionSource<bool> gate = mAsyncGate;
            mAsyncGate = null;
            if (gate == null) throw new InvalidOperationException("No async test gate is active.");
            gate.TrySetResult(true);
        }

        /// <summary>
        /// 释放门控任务和全部内存 Prefab；受管实例应由 UIRoot 先行释放。
        /// </summary>
        public void Dispose()
        {
            if (mDisposed) return;
            mDisposed = true;
            TaskCompletionSource<bool> gate = mAsyncGate;
            mAsyncGate = null;
            if (gate != null) gate.TrySetResult(true);
            foreach (GameObject prefab in mPrefabs.Values)
            {
                if (prefab != default) UnityObject.DestroyImmediate(prefab);
            }

            mPrefabs.Clear();
        }

        /// <summary>
        /// 为一次底层加载创建独占 lease，并记录释放回调。
        /// </summary>
        /// <param name="panelType">需要加载的具体面板类型。</param>
        /// <returns>持有共享测试 Prefab 的独占 lease。</returns>
        private IPanelPrefabLease CreateLease(Type panelType)
        {
            GameObject prefab = GetOrCreatePrefab(panelType);
            LeaseCount++;
            return new UIKitTestPanelPrefabLease(
                "Tests/UIKit/" + panelType.FullName,
                prefab,
                OnLeaseDisposed);
        }

        /// <summary>
        /// 获取或创建指定类型的禁用 RectTransform Prefab 对象。
        /// </summary>
        /// <param name="panelType">Prefab 必须携带的 UIPanel 组件类型。</param>
        /// <returns>可由 UIKit 实例化的内存 Prefab。</returns>
        private GameObject GetOrCreatePrefab(Type panelType)
        {
            if (panelType == null) throw new ArgumentNullException(nameof(panelType));
            if (mPrefabs.TryGetValue(panelType, out GameObject existing) && existing != default) return existing;
            var prefab = new GameObject(panelType.Name + ".Prefab", typeof(RectTransform));
            Component component = prefab.AddComponent(panelType);
            if (component == default)
            {
                UnityObject.DestroyImmediate(prefab);
                throw new InvalidOperationException("Unable to add test panel component " + panelType.FullName + ".");
            }

            prefab.SetActive(false);
            mPrefabs.Add(panelType, prefab);
            return prefab;
        }

        /// <summary>
        /// 记录一个独占 lease 的首次释放。
        /// </summary>
        private void OnLeaseDisposed()
        {
            LeaseDisposeCount++;
        }

        /// <summary>
        /// 拒绝在测试 loader 清理后继续创建资源所有权。
        /// </summary>
        private void ThrowIfDisposed()
        {
            if (mDisposed) throw new ObjectDisposedException(nameof(UIKitTestPanelLoader));
        }

        /// <summary>
        /// 表示测试 loader 返回的一次独占且幂等的 Prefab 获取。
        /// </summary>
        private sealed class UIKitTestPanelPrefabLease : IPanelPrefabLease
        {
            private readonly Action mDisposedCallback;
            private GameObject mPrefab;
            private bool mDisposed;

            /// <summary>
            /// 创建一个持有共享测试 Prefab 的独占 lease。
            /// </summary>
            /// <param name="location">用于诊断的稳定资源位置。</param>
            /// <param name="prefab">当前 lease 暴露的测试 Prefab。</param>
            /// <param name="disposedCallback">首次释放时执行的计数回调。</param>
            internal UIKitTestPanelPrefabLease(string location, GameObject prefab, Action disposedCallback)
            {
                Location = location;
                mPrefab = prefab;
                mDisposedCallback = disposedCallback;
            }

            /// <inheritdoc />
            public string Location { get; }

            /// <inheritdoc />
            public GameObject Prefab => mDisposed ? null : mPrefab;

            /// <summary>
            /// 幂等释放当前 lease 的资源所有权，不销毁 loader 共享的测试 Prefab。
            /// </summary>
            public void Dispose()
            {
                if (mDisposed) return;
                mDisposed = true;
                mPrefab = null;
                if (mDisposedCallback != null) mDisposedCallback();
            }
        }
    }
}
