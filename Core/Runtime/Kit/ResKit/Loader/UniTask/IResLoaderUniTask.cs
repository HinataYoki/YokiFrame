#if YOKIFRAME_UNITASK_SUPPORT
using System.Threading;
using Cysharp.Threading.Tasks;
using Object = UnityEngine.Object;

namespace YokiFrame
{
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
}
#endif
