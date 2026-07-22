namespace YokiFrame
{
    /// <summary>
    /// 定义 Architecture 可管理服务的基础契约。
    /// </summary>
    public interface IService : ICanInit
    {
        /// <summary>
        /// 获取当前服务所属的架构容器。
        /// </summary>
        IArchitecture Architecture { get; }

        /// <summary>
        /// 注入服务所属架构；该方法由 Architecture 注册流程调用。
        /// </summary>
        /// <param name="architecture">服务所属架构。</param>
        void SetArchitecture(IArchitecture architecture);
    }
}
