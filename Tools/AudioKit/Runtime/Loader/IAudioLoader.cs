using System;
using System.Collections.Generic;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 音频加载器接口
    /// </summary>
    public interface IAudioLoader
    {
        /// <summary>
        /// 同步加载音频
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <returns>音频剪辑</returns>
        AudioClip Load(string path);

        /// <summary>
        /// 异步加载音频
        /// </summary>
        /// <param name="path">资源路径</param>
        /// <param name="onComplete">完成回调</param>
        void LoadAsync(string path, Action<AudioClip> onComplete);

        /// <summary>
        /// 卸载并回收加载器
        /// </summary>
        void UnloadAndRecycle();
    }

    /// <summary>
    /// 音频加载池接口
    /// </summary>
    public interface IAudioLoaderPool
    {
        /// <summary>
        /// 分配加载器
        /// </summary>
        IAudioLoader AllocateLoader();

        /// <summary>
        /// 回收加载器
        /// </summary>
        void RecycleLoader(IAudioLoader loader);
    }

    /// <summary>
    /// 抽象音频加载池
    /// </summary>
    public abstract class AbstractAudioLoaderPool : IAudioLoaderPool
    {
        private readonly Stack<IAudioLoader> mLoaderPool = new();

        public IAudioLoader AllocateLoader() =>
            mLoaderPool.Count > 0 ? mLoaderPool.Pop() : CreateAudioLoader();

        public void RecycleLoader(IAudioLoader loader) => mLoaderPool.Push(loader);

        protected abstract IAudioLoader CreateAudioLoader();
    }

    /// <summary>
    /// 默认音频加载池（基于 ResKit）
    /// </summary>
    public class DefaultAudioLoaderPool : AbstractAudioLoaderPool
    {
        protected override IAudioLoader CreateAudioLoader() => new DefaultAudioLoader(this);

        public class DefaultAudioLoader : IAudioLoader
        {
            protected readonly IAudioLoaderPool mLoaderPool;
            protected IResLoader mResLoader;

            public DefaultAudioLoader(IAudioLoaderPool pool) => mLoaderPool = pool;

            public AudioClip Load(string path)
            {
                mResLoader = ResKit.GetLoaderPool().Allocate();
                return mResLoader.Load<AudioClip>(path);
            }

            public void LoadAsync(string path, Action<AudioClip> onComplete)
            {
                mResLoader = ResKit.GetLoaderPool().Allocate();
                mResLoader.LoadAsync<AudioClip>(path, onComplete);
            }

            public void UnloadAndRecycle()
            {
                mResLoader?.UnloadAndRecycle();
                mResLoader = null;
                mLoaderPool.RecycleLoader(this);
            }
        }
    }

#if YOKIFRAME_UNITASK_SUPPORT
    /// <summary>
    /// 支持 UniTask 的音频加载器接口
    /// </summary>
    public interface IAudioLoaderUniTask : IAudioLoader
    {
        /// <summary>
        /// UniTask 异步加载音频
        /// </summary>
        UniTask<AudioClip> LoadUniTaskAsync(string path, CancellationToken cancellationToken = default);
    }

    /// <summary>
    /// 默认 UniTask 音频加载池（基于 ResKit）
    /// </summary>
    public class DefaultAudioLoaderUniTaskPool : AbstractAudioLoaderPool
    {
        protected override IAudioLoader CreateAudioLoader() => new DefaultAudioLoaderUniTask(this);

        /// <summary>
        /// 默认 UniTask 音频加载器 - 继承 DefaultAudioLoader，仅扩展 UniTask 异步方法
        /// </summary>
        public class DefaultAudioLoaderUniTask : DefaultAudioLoaderPool.DefaultAudioLoader, IAudioLoaderUniTask
        {
            public DefaultAudioLoaderUniTask(IAudioLoaderPool pool) : base(pool) { }

            public async UniTask<AudioClip> LoadUniTaskAsync(string path, CancellationToken cancellationToken = default)
            {
                mResLoader ??= ResKit.GetLoaderPool().Allocate();

                if (mResLoader is IResLoaderUniTask uniTaskLoader)
                    return await uniTaskLoader.LoadUniTaskAsync<AudioClip>(path, cancellationToken);

                // 回退：TCS 包装回调
                var tcs = new UniTaskCompletionSource<AudioClip>();
                mResLoader.LoadAsync<AudioClip>(path, a => tcs.TrySetResult(a));
                return await tcs.Task.AttachExternalCancellation(cancellationToken);
            }
        }
    }
#endif
}
