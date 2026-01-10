using System;
using UnityEngine;

namespace YokiFrame
{
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
}
