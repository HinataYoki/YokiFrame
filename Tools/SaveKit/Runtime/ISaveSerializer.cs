namespace YokiFrame
{
    /// <summary>
    /// 保存数据序列化接口。
    /// </summary>
    public interface ISaveSerializer
    {
        /// <summary>
        /// 序列化指定类型的数据。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="data">要序列化的数据。</param>
        /// <returns>序列化后的字节。</returns>
        byte[] Serialize<T>(T data);

        /// <summary>
        /// 反序列化指定类型的数据。
        /// </summary>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <param name="bytes">序列化字节。</param>
        /// <returns>反序列化后的数据。</returns>
        T Deserialize<T>(byte[] bytes);

        /// <summary>
        /// 序列化未知编译期类型的数据。
        /// </summary>
        /// <param name="data">要序列化的数据。</param>
        /// <returns>序列化后的字节。</returns>
        byte[] Serialize(object data);

        /// <summary>
        /// 将序列化字节覆盖反序列化到现有对象。
        /// </summary>
        /// <param name="bytes">序列化字节。</param>
        /// <param name="target">目标对象。</param>
        void DeserializeOverwrite(byte[] bytes, object target);
    }
}
