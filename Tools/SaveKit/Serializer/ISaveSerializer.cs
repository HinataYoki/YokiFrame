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
    }
}
