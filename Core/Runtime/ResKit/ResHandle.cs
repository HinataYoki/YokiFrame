using System;

namespace YokiFrame
{
    /// <summary>
    /// 表示一个由 ResKit 管理的资源引用。
    /// </summary>
    /// <typeparam name="T">资源对象类型。</typeparam>
    public sealed class ResHandle<T> : IDisposable, IResHandleDebugView, IResHandleInvalidator, IResHandleReleaser where T : class
    {
        internal ResHandle(string path, T asset, string providerName, string source, string sourceFile, int sourceLine)
        {
            Path = path;
            Asset = asset;
            ProviderName = providerName;
            Source = source;
            SourceFile = sourceFile;
            SourceLine = sourceLine;
            RefCount = 1;
            IsDone = true;
        }

        /// <summary>
        /// 资源路径。
        /// </summary>
        public string Path { get; internal set; }

        /// <summary>
        /// 资源对象类型。
        /// </summary>
        public Type AssetType => typeof(T);

        /// <summary>
        /// 已加载的资源对象。
        /// </summary>
        public T Asset { get; internal set; }

        object IResHandleDebugView.AssetObject => Asset;

        /// <summary>
        /// 加载该资源的 Provider 名称。
        /// </summary>
        public string ProviderName { get; internal set; }

        /// <summary>
        /// 资源加载调用来源展示名。
        /// </summary>
        public string Source { get; internal set; }

        /// <summary>
        /// 资源加载调用来源文件。
        /// </summary>
        public string SourceFile { get; internal set; }

        /// <summary>
        /// 资源加载调用来源行号。
        /// </summary>
        public int SourceLine { get; internal set; }

        /// <summary>
        /// 当前资源引用计数。
        /// </summary>
        public int RefCount { get; internal set; }

        /// <summary>
        /// 资源加载是否已完成。
        /// </summary>
        public bool IsDone { get; internal set; }

        /// <summary>
        /// 增加当前资源句柄的引用计数。
        /// </summary>
        public void Retain() => RefCount++;

        /// <summary>
        /// 释放当前资源句柄的一次引用。
        /// </summary>
        public void Release() => ResKit.Release(this);

        /// <summary>
        /// 释放当前资源句柄。
        /// </summary>
        public void Dispose() => Release();

        bool IResHandleReleaser.TryReleaseObject(object asset)
        {
            if (!ReferenceEquals(Asset, asset))
                return false;

            Release();
            return true;
        }

        void IResHandleInvalidator.Invalidate() => Invalidate();

        internal void Invalidate()
        {
            Asset = null;
            Path = null;
            ProviderName = null;
            Source = null;
            SourceFile = null;
            SourceLine = 0;
            IsDone = false;
            RefCount = 0;
        }
    }
}
