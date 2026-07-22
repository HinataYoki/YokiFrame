using System;

namespace YokiFrame
{
    /// <summary>表示调用方独占释放权的一次 ResKit 资源 lease。</summary>
    /// <typeparam name="T">资源对象类型。</typeparam>
    public sealed class ResHandle<T> : IDisposable where T : class
    {
        private readonly ResLease mLease;

        /// <summary>由 ResKit 为一次独立获取创建 handle。</summary>
        internal ResHandle(ResLease lease)
        {
            mLease = lease ?? throw new ArgumentNullException(nameof(lease));
        }

        /// <summary>获取资源路径；当前 lease 已释放时返回 null。</summary>
        public string Path => ResKit.GetLeasePath(mLease);

        /// <summary>获取资源对象类型。</summary>
        public Type AssetType => typeof(T);

        /// <summary>获取当前 lease 的资源；已释放或被 ClearAll 撤销时返回 null。</summary>
        public T Asset => ResKit.GetLeaseAsset<T>(mLease);

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>获取创建底层 entry 的 Provider 名称；当前 lease 已释放时返回 null。</summary>
        public string ProviderName => ResKit.GetLeaseProviderName(mLease);

        /// <summary>获取本次获取的调用来源展示名。</summary>
        public string Source => mLease.Source.Display;

        /// <summary>获取本次获取的调用来源文件。</summary>
        public string SourceFile => mLease.Source.FilePath;

        /// <summary>获取本次获取的调用来源行号。</summary>
        public int SourceLine => mLease.Source.Line;

        /// <summary>获取底层共享 entry 的当前总引用数。</summary>
        public int RefCount => ResKit.GetLeaseRefCount(mLease);
#endif

        /// <summary>获取当前 lease 是否仍持有一个已完成资源。</summary>
        public bool IsDone => Asset != null;

        /// <summary>幂等释放当前 handle 的一次引用。</summary>
        public void Release() => ResKit.ReleaseLease(mLease);

        /// <summary>按 IDisposable 约定释放当前 handle。</summary>
        public void Dispose() => Release();
    }
}
