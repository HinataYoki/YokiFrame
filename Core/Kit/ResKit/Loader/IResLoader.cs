using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#endif
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 资源加载器接口
    /// </summary>
    public interface IResLoader
    {
        /// <summary>
        /// 同步加载资源
        /// </summary>
        T Load<T>(string path) where T : Object;

        /// <summary>
        /// 异步加载资源
        /// </summary>
        void LoadAsync<T>(string path, Action<T> onComplete) where T : Object;

        /// <summary>
        /// 卸载并回收加载器
        /// </summary>
        void UnloadAndRecycle();
    }

    /// <summary>
    /// 原始文件加载器接口（用于加载非 Unity 资源的原始文件）
    /// </summary>
    public interface IRawFileLoader
    {
        /// <summary>
        /// 同步加载原始文件文本
        /// </summary>
        string LoadRawFileText(string path);

        /// <summary>
        /// 同步加载原始文件字节数据
        /// </summary>
        byte[] LoadRawFileData(string path);

        /// <summary>
        /// 异步加载原始文件文本
        /// </summary>
        void LoadRawFileTextAsync(string path, Action<string> onComplete);

        /// <summary>
        /// 异步加载原始文件字节数据
        /// </summary>
        void LoadRawFileDataAsync(string path, Action<byte[]> onComplete);

        /// <summary>
        /// 获取原始文件的完整路径（用于需要直接访问文件的场景）
        /// </summary>
        string GetRawFilePath(string path);

        /// <summary>
        /// 卸载并回收加载器
        /// </summary>
        void UnloadAndRecycle();
    }

    /// <summary>
    /// 资源加载池接口
    /// </summary>
    public interface IResLoaderPool
    {
        IResLoader Allocate();
        void Recycle(IResLoader loader);
    }

    /// <summary>
    /// 原始文件加载池接口
    /// </summary>
    public interface IRawFileLoaderPool
    {
        IRawFileLoader Allocate();
        void Recycle(IRawFileLoader loader);
    }

    /// <summary>
    /// 抽象加载池基类
    /// </summary>
    public abstract class AbstractResLoaderPool : IResLoaderPool
    {
        private readonly Stack<IResLoader> mPool = new();

        public IResLoader Allocate() => mPool.Count > 0 ? mPool.Pop() : CreateLoader();
        public void Recycle(IResLoader loader) => mPool.Push(loader);

        protected abstract IResLoader CreateLoader();
    }

    /// <summary>
    /// 抽象原始文件加载池基类
    /// </summary>
    public abstract class AbstractRawFileLoaderPool : IRawFileLoaderPool
    {
        private readonly Stack<IRawFileLoader> mPool = new();

        public IRawFileLoader Allocate() => mPool.Count > 0 ? mPool.Pop() : CreateLoader();
        public void Recycle(IRawFileLoader loader) => mPool.Push(loader);

        protected abstract IRawFileLoader CreateLoader();
    }

    /// <summary>
    /// 默认加载池（Resources）
    /// </summary>
    public class DefaultResLoaderPool : AbstractResLoaderPool
    {
        protected override IResLoader CreateLoader() => new DefaultResLoader(this);
    }

    /// <summary>
    /// 默认加载器（Resources）
    /// </summary>
    public class DefaultResLoader : IResLoader
    {
        private readonly IResLoaderPool mPool;
        private Object mAsset;

        public DefaultResLoader(IResLoaderPool pool) => mPool = pool;

        public T Load<T>(string path) where T : Object
        {
            mAsset = Resources.Load<T>(path);
            ResLoadTracker.OnLoad(this, path, typeof(T), mAsset);
            return mAsset as T;
        }

        public void LoadAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            var request = Resources.LoadAsync<T>(path);
            request.completed += _ =>
            {
                mAsset = request.asset;
                ResLoadTracker.OnLoad(this, path, typeof(T), mAsset);
                onComplete?.Invoke(mAsset as T);
            };
        }

        public void UnloadAndRecycle()
        {
            ResLoadTracker.OnUnload(this);
            if (mAsset != null)
            {
                Resources.UnloadAsset(mAsset);
                mAsset = null;
            }
            mPool.Recycle(this);
        }
    }

#if YOKIFRAME_UNITASK_SUPPORT
    /// <summary>
    /// 支持 UniTask 的资源加载器接口扩展
    /// </summary>
    public interface IResLoaderUniTask : IResLoader
    {
        /// <summary>
        /// [UniTask] 异步加载资源
        /// </summary>
        UniTask<T> LoadUniTaskAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object;
    }

    /// <summary>
    /// 默认 UniTask 加载器（Resources）
    /// </summary>
    public class DefaultResLoaderUniTask : IResLoaderUniTask
    {
        private readonly IResLoaderPool mPool;
        private Object mAsset;

        public DefaultResLoaderUniTask(IResLoaderPool pool) => mPool = pool;

        public T Load<T>(string path) where T : Object
        {
            mAsset = Resources.Load<T>(path);
            ResLoadTracker.OnLoad(this, path, typeof(T), mAsset);
            return mAsset as T;
        }

        public void LoadAsync<T>(string path, Action<T> onComplete) where T : Object
        {
            var request = Resources.LoadAsync<T>(path);
            request.completed += _ =>
            {
                mAsset = request.asset;
                ResLoadTracker.OnLoad(this, path, typeof(T), mAsset);
                onComplete?.Invoke(mAsset as T);
            };
        }

        public async UniTask<T> LoadUniTaskAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object
        {
            var request = Resources.LoadAsync<T>(path);
            await request.ToUniTask(cancellationToken: cancellationToken);
            mAsset = request.asset;
            ResLoadTracker.OnLoad(this, path, typeof(T), mAsset);
            return mAsset as T;
        }

        public void UnloadAndRecycle()
        {
            ResLoadTracker.OnUnload(this);
            if (mAsset != null)
            {
                Resources.UnloadAsset(mAsset);
                mAsset = null;
            }
            mPool.Recycle(this);
        }
    }

    /// <summary>
    /// 默认 UniTask 加载池
    /// </summary>
    public class DefaultResLoaderUniTaskPool : AbstractResLoaderPool
    {
        protected override IResLoader CreateLoader() => new DefaultResLoaderUniTask(this);
    }

    /// <summary>
    /// 支持 UniTask 的原始文件加载器接口扩展
    /// </summary>
    public interface IRawFileLoaderUniTask : IRawFileLoader
    {
        /// <summary>
        /// [UniTask] 异步加载原始文件文本
        /// </summary>
        UniTask<string> LoadRawFileTextUniTaskAsync(string path, CancellationToken cancellationToken = default);

        /// <summary>
        /// [UniTask] 异步加载原始文件字节数据
        /// </summary>
        UniTask<byte[]> LoadRawFileDataUniTaskAsync(string path, CancellationToken cancellationToken = default);
    }
#endif

    /// <summary>
    /// 默认原始文件加载池（基于 Resources/TextAsset）
    /// </summary>
    public class DefaultRawFileLoaderPool : AbstractRawFileLoaderPool
    {
        protected override IRawFileLoader CreateLoader() => new DefaultRawFileLoader(this);
    }

    /// <summary>
    /// 默认原始文件加载器（基于 Resources/TextAsset）
    /// 注意：Resources 方式要求文件放在 Resources 文件夹下，且以 .txt/.bytes 等扩展名存储
    /// </summary>
    public class DefaultRawFileLoader : IRawFileLoader
    {
        private readonly IRawFileLoaderPool mPool;
        private TextAsset mTextAsset;

        public DefaultRawFileLoader(IRawFileLoaderPool pool) => mPool = pool;

        public string LoadRawFileText(string path)
        {
            mTextAsset = Resources.Load<TextAsset>(path);
            return mTextAsset != null ? mTextAsset.text : null;
        }

        public byte[] LoadRawFileData(string path)
        {
            mTextAsset = Resources.Load<TextAsset>(path);
            return mTextAsset != null ? mTextAsset.bytes : null;
        }

        public void LoadRawFileTextAsync(string path, Action<string> onComplete)
        {
            var request = Resources.LoadAsync<TextAsset>(path);
            request.completed += _ =>
            {
                mTextAsset = request.asset as TextAsset;
                onComplete?.Invoke(mTextAsset != null ? mTextAsset.text : null);
            };
        }

        public void LoadRawFileDataAsync(string path, Action<byte[]> onComplete)
        {
            var request = Resources.LoadAsync<TextAsset>(path);
            request.completed += _ =>
            {
                mTextAsset = request.asset as TextAsset;
                onComplete?.Invoke(mTextAsset != null ? mTextAsset.bytes : null);
            };
        }

        public string GetRawFilePath(string path)
        {
            // Resources 方式无法获取实际文件路径，返回 null
            // 如需文件路径，请使用 StreamingAssets 或 YooAsset 扩展
            return null;
        }

        public void UnloadAndRecycle()
        {
            if (mTextAsset != null)
            {
                Resources.UnloadAsset(mTextAsset);
                mTextAsset = null;
            }
            mPool.Recycle(this);
        }
    }

#if YOKIFRAME_UNITASK_SUPPORT
    /// <summary>
    /// 默认 UniTask 原始文件加载池
    /// </summary>
    public class DefaultRawFileLoaderUniTaskPool : AbstractRawFileLoaderPool
    {
        protected override IRawFileLoader CreateLoader() => new DefaultRawFileLoaderUniTask(this);
    }

    /// <summary>
    /// 默认 UniTask 原始文件加载器（基于 Resources/TextAsset）
    /// </summary>
    public class DefaultRawFileLoaderUniTask : IRawFileLoaderUniTask
    {
        private readonly IRawFileLoaderPool mPool;
        private TextAsset mTextAsset;

        public DefaultRawFileLoaderUniTask(IRawFileLoaderPool pool) => mPool = pool;

        public string LoadRawFileText(string path)
        {
            mTextAsset = Resources.Load<TextAsset>(path);
            return mTextAsset != null ? mTextAsset.text : null;
        }

        public byte[] LoadRawFileData(string path)
        {
            mTextAsset = Resources.Load<TextAsset>(path);
            return mTextAsset != null ? mTextAsset.bytes : null;
        }

        public void LoadRawFileTextAsync(string path, Action<string> onComplete)
        {
            var request = Resources.LoadAsync<TextAsset>(path);
            request.completed += _ =>
            {
                mTextAsset = request.asset as TextAsset;
                onComplete?.Invoke(mTextAsset != null ? mTextAsset.text : null);
            };
        }

        public void LoadRawFileDataAsync(string path, Action<byte[]> onComplete)
        {
            var request = Resources.LoadAsync<TextAsset>(path);
            request.completed += _ =>
            {
                mTextAsset = request.asset as TextAsset;
                onComplete?.Invoke(mTextAsset != null ? mTextAsset.bytes : null);
            };
        }

        public async UniTask<string> LoadRawFileTextUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            var request = Resources.LoadAsync<TextAsset>(path);
            await request.ToUniTask(cancellationToken: cancellationToken);
            mTextAsset = request.asset as TextAsset;
            return mTextAsset != null ? mTextAsset.text : null;
        }

        public async UniTask<byte[]> LoadRawFileDataUniTaskAsync(string path, CancellationToken cancellationToken = default)
        {
            var request = Resources.LoadAsync<TextAsset>(path);
            await request.ToUniTask(cancellationToken: cancellationToken);
            mTextAsset = request.asset as TextAsset;
            return mTextAsset != null ? mTextAsset.bytes : null;
        }

        public string GetRawFilePath(string path) => null;

        public void UnloadAndRecycle()
        {
            if (mTextAsset != null)
            {
                Resources.UnloadAsset(mTextAsset);
                mTextAsset = null;
            }
            mPool.Recycle(this);
        }
    }
#endif
}
