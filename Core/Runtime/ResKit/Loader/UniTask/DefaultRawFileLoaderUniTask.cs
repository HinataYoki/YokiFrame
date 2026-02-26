#if YOKIFRAME_UNITASK_SUPPORT
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace YokiFrame
{
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
            if (mTextAsset != default)
            {
                Resources.UnloadAsset(mTextAsset);
                mTextAsset = null;
            }
            mPool.Recycle(this);
        }
    }
}
#endif
