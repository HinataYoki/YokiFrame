#if UNITY_2022_3_OR_NEWER
using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 维护 UIKit 面板、加载、缓存、栈、层级和模态状态的唯一运行时 owner。
    /// </summary>
    internal sealed partial class UIKitController : IDisposable
    {
        private readonly UIRoot mRoot;
        private readonly Dictionary<Type, PanelEntry> mEntries = new();
        private readonly Dictionary<Type, PanelLoadOperation> mPendingLoads = new();
        private readonly LinkedList<PanelEntry> mReusableLru = new();
        private IPanelLoader mLoader;
        private int mReusableCapacity;
        private long mOpenSequence;
        private long mLoadGeneration;
        private bool mDisposed;

        /// <summary>
        /// Editor 构建提供状态版本实现；Player 中未实现的 partial 调用会被编译器完全移除。
        /// </summary>
        partial void OnStateChanged();

        /// <summary>
        /// 创建 Root 私有控制器并读取 Prefab 序列化的加载与缓存参数。
        /// </summary>
        internal UIKitController(UIRoot root)
        {
            mRoot = root != default ? root : throw new ArgumentNullException(nameof(root));
            mLoader = new ResKitPanelLoader(root.PrefabPathPrefix, root.UseAddressableLocation);
            mReusableCapacity = root.InitialReusableCacheCapacity;
            OnStateChanged();
        }

        internal int ReusableCapacity
        {
            get { return mReusableCapacity; }
            set
            {
                EnsureAvailable();
                mReusableCapacity = Math.Max(0, value);
                TrimReusableCache();
                OnStateChanged();
            }
        }

        /// <summary>
        /// 获取当前 Prefab loader，不触发资源加载。
        /// </summary>
        internal IPanelLoader GetLoader()
        {
            return mLoader;
        }

        /// <summary>
        /// 替换后续物化请求使用的 loader；已有 entry 仍由自己的 lease 释放。
        /// </summary>
        internal void SetLoader(IPanelLoader loader)
        {
            EnsureAvailable();
            mLoader = loader ?? throw new ArgumentNullException(nameof(loader));
            OnStateChanged();
        }

        /// <summary>
        /// Root teardown 时取消 pending 并释放每一个已物化 entry。
        /// </summary>
        public void Dispose()
        {
            if (mDisposed) return;
            mDisposed = true;
            mLoadGeneration++;
            ClearDialogQueue();
            mCurrentDialog = null;
            mDialogProcessing = false;
            PanelLoadOperation[] operations = new PanelLoadOperation[mPendingLoads.Count];
            mPendingLoads.Values.CopyTo(operations, 0);
            for (var operationIndex = 0; operationIndex < operations.Length; operationIndex++)
                CancelLoadOperation(operations[operationIndex]);
            PanelEntry[] entries = new PanelEntry[mEntries.Count];
            mEntries.Values.CopyTo(entries, 0);
            for (var index = 0; index < entries.Length; index++) DisposeEntry(entries[index]);
            mPendingLoads.Clear();
            mReusableLru.Clear();
            OnStateChanged();
        }

        /// <summary>
        /// 校验控制器和 Unity Root 仍可执行变更操作。
        /// </summary>
        private void EnsureAvailable()
        {
            if (mDisposed) throw new ObjectDisposedException(nameof(UIKitController));
            mRoot.AssertMainThread();
        }

        /// <summary>
        /// 取消底层共享加载并立即完成公开等待；隔离自定义 loader 的取消回调异常。
        /// </summary>
        private static void CancelLoadOperation(PanelLoadOperation operation)
        {
            if (operation == null) return;
            try
            {
                operation.Cancel();
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception);
            }

            operation.SetCanceled();
        }

        /// <summary>
        /// 校验公开传入的 Panel 类型可被 UIKit 物化。
        /// </summary>
        private static void ValidatePanelType(Type panelType)
        {
            if (panelType == null) throw new ArgumentNullException(nameof(panelType));
            if (!typeof(UIPanel).IsAssignableFrom(panelType) || panelType.IsAbstract)
                throw new ArgumentException("Panel type must be a non-abstract UIPanel.", nameof(panelType));
        }
    }
}
#endif
