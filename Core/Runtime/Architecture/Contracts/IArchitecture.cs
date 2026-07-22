using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 定义 Architecture 容器的注册、查询和诊断枚举能力。
    /// </summary>
    public interface IArchitecture : ICanInit
    {
        /// <summary>
        /// 注册一个服务实例；重复注册同一类型时会释放旧实例。
        /// </summary>
        /// <typeparam name="T">服务类型。</typeparam>
        /// <param name="service">服务实例。</param>
        void Register<T>(T service) where T : class, IService, new();

        /// <summary>
        /// 获取已注册服务；未注册且 force 为 true 时会创建、注册并初始化服务。
        /// 同一服务类型的并发强制请求共享一次创建，期间完成的显式注册优先。
        /// </summary>
        /// <typeparam name="T">服务类型。</typeparam>
        /// <param name="force">是否在缺失时强制创建服务。</param>
        /// <returns>已注册或新创建的服务；缺失且不强制创建时返回 null。</returns>
        T GetService<T>(bool force = false) where T : class, IService, new();

        /// <summary>
        /// 获取当前架构中全部已注册服务的快照。
        /// </summary>
        /// <returns>服务快照集合；调用方修改集合不会影响架构内部状态。</returns>
        IEnumerable<IService> GetAllServices();
    }
}
