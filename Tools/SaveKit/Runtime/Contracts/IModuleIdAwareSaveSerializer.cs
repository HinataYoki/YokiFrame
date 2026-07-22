namespace YokiFrame
{
    /// <summary>
    /// 可感知模块稳定 ID 的序列化器扩展契约。
    /// SaveKit 会在模块使用显式 ID 时优先调用该契约，以便后端按 ID 执行迁移。
    /// </summary>
    public interface IModuleIdAwareSaveSerializer
    {
        /// <summary>按模块 ID 反序列化 payload。</summary>
        /// <typeparam name="T">模块类型。</typeparam>
        /// <param name="moduleId">模块稳定 ID。</param>
        /// <param name="bytes">模块 payload。</param>
        /// <returns>反序列化后的模块。</returns>
        T Deserialize<T>(string moduleId, byte[] bytes);

        /// <summary>按模块 ID 把 payload 覆盖到已有模块对象。</summary>
        /// <param name="moduleId">模块稳定 ID。</param>
        /// <param name="bytes">模块 payload。</param>
        /// <param name="target">目标模块对象。</param>
        void DeserializeOverwrite(string moduleId, byte[] bytes, object target);
    }
}
