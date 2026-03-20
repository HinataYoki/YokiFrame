namespace YokiFrame
{
    /// <summary>
    /// 存档序列化器接口
    /// 定义数据序列化和反序列化的方法
    /// </summary>
    public interface ISaveSerializer
    {
        /// <summary>
        /// 序列化对象为字节数组
        /// </summary>
        byte[] Serialize<T>(T data);

        /// <summary>
        /// 反序列化字节数组为对象
        /// </summary>
        T Deserialize<T>(byte[] bytes);

        /// <summary>
        /// 序列化对象为字节数组（非泛型，供运行时 Type 场景使用）
        /// </summary>
        byte[] Serialize(object data);

        /// <summary>
        /// 将字节数组反序列化并覆盖到已有对象上（供 Architecture 集成等场景使用）
        /// </summary>
        void DeserializeOverwrite(byte[] bytes, object target);
    }
}
