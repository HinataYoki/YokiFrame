namespace YokiFrame
{
    /// <summary>
    /// 所有 YokiFrame 单例实例的公共契约。
    /// </summary>
    public interface ISingleton
    {
        /// <summary>
        /// 单例实例创建并完成缓存后调用一次。
        /// </summary>
        void OnSingletonInit();
    }
}
