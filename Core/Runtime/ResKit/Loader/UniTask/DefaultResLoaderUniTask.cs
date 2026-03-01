#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace YokiFrame
{
    /// <summary>
    /// 默认 UniTask 加载池
    /// </summary>
    public class DefaultResLoaderUniTaskPool : AbstractResLoaderPool
    {
        protected override IResLoader CreateLoader() => new DefaultResLoaderUniTask(this);
    }

    /// <summary>
    /// 默认 UniTask 加载器（Resources） - 继承 DefaultResLoader，仅扩展 UniTask 异步方法
    /// </summary>
    public class DefaultResLoaderUniTask : DefaultResLoader, IResLoaderUniTask
    {
        public DefaultResLoaderUniTask(IResLoaderPool pool) : base(pool) { }

        public async UniTask<T> LoadUniTaskAsync<T>(string path, CancellationToken cancellationToken = default) where T : Object
        {
            var request = Resources.LoadAsync<T>(path);
            await request.ToUniTask(cancellationToken: cancellationToken);
            mAsset = request.asset;
            ResLoadTracker.OnLoad(this, path, typeof(T), mAsset);
            return mAsset as T;
        }
    }
}
#endif
