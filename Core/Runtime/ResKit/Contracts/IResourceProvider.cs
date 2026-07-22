using System.Threading;
#if YOKIFRAME_UNITASK_SUPPORT
using Cysharp.Threading.Tasks;
#else
using System.Threading.Tasks;
#endif

namespace YokiFrame
{
    /// <summary>
    /// 定义宿主资源加载能力；具体资源类型由调用方与宿主 Adapter 共同约定。
    /// </summary>
    public interface IResourceProvider
    {
#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>获取用于诊断和 Workbench 展示的稳定 Provider 名称。</summary>
        string ProviderName { get; }
#endif

        /// <summary>同步加载指定路径和类型的资源；未找到时返回 null。</summary>
        /// <typeparam name="T">调用方期望的资源类型。</typeparam>
        /// <param name="path">Provider 可识别的资源路径或 location。</param>
        /// <returns>加载成功的资源对象；未找到时返回 null。</returns>
        T Load<T>(string path) where T : class;

#if YOKIFRAME_UNITASK_SUPPORT
        /// <summary>异步加载指定路径和类型的资源；取消只影响本次 Provider 调用。</summary>
        /// <typeparam name="T">调用方期望的资源类型。</typeparam>
        /// <param name="path">Provider 可识别的资源路径或 location。</param>
        /// <param name="token">底层加载取消令牌。</param>
        /// <returns>加载成功的资源对象；未找到时返回 null。</returns>
        UniTask<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class;
#else
        /// <summary>异步加载指定路径和类型的资源；取消只影响本次 Provider 调用。</summary>
        /// <typeparam name="T">调用方期望的资源类型。</typeparam>
        /// <param name="path">Provider 可识别的资源路径或 location。</param>
        /// <param name="token">底层加载取消令牌。</param>
        /// <returns>加载成功的资源对象；未找到时返回 null。</returns>
        Task<T> LoadAsync<T>(string path, CancellationToken token = default) where T : class;
#endif

        /// <summary>释放由当前 Provider 创建并转交给 ResKit 所有的底层资源。</summary>
        /// <param name="asset">当前 Provider 曾返回的资源对象。</param>
        void Release(object asset);
    }
}
