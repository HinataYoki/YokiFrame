#if YOKIFRAME_UNITASK_SUPPORT
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
    /// 默认 UniTask 原始文件加载器 - 继承 DefaultRawFileLoader，仅扩展 UniTask 异步方法
    /// </summary>
    public class DefaultRawFileLoaderUniTask : DefaultRawFileLoader, IRawFileLoaderUniTask
    {
        public DefaultRawFileLoaderUniTask(IRawFileLoaderPool pool) : base(pool) { }

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
    }
}
#endif
