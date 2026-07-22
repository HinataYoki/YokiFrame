namespace YokiFrame
{
    /// <summary>
    /// SaveKit 模块 payload 序列化契约。payload 版本和迁移策略由具体后端负责。
    /// </summary>
    public interface ISaveSerializer
    {
        /// <summary>获取存档文件中用于匹配后端的稳定格式 ID。</summary>
        string SerializerId { get; }

        /// <summary>序列化强类型模块。</summary>
        /// <typeparam name="T">模块类型。</typeparam>
        /// <param name="data">模块实例。</param>
        /// <returns>模块 payload 字节。</returns>
        byte[] Serialize<T>(T data);

        /// <summary>反序列化强类型模块。</summary>
        /// <typeparam name="T">模块类型。</typeparam>
        /// <param name="bytes">模块 payload 字节。</param>
        /// <returns>模块实例。</returns>
        T Deserialize<T>(byte[] bytes);

        /// <summary>按运行时类型序列化模块。</summary>
        /// <param name="data">模块实例。</param>
        /// <returns>模块 payload 字节。</returns>
        byte[] Serialize(object data);

        /// <summary>验证模块 payload 是否可由当前后端读取，允许后端提前报告迁移失败。</summary>
        /// <param name="moduleId">模块稳定 ID。</param>
        /// <param name="bytes">模块 payload。</param>
        void ValidatePayload(string moduleId, byte[] bytes);

        /// <summary>将 payload 覆盖反序列化到现有模块对象。</summary>
        /// <param name="bytes">模块 payload 字节。</param>
        /// <param name="target">目标模块对象。</param>
        void DeserializeOverwrite(byte[] bytes, object target);
    }
}
