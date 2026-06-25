namespace YokiFrame
{
    /// <summary>
    /// 序列化提供者抽象接口（SaveKit 等使用）
    /// </summary>
    public interface ISerializationProvider
    {
        /// <summary>
        /// 将数据序列化为 JSON 字符串。
        /// </summary>
        /// <param name="data">要序列化的数据。</param>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <returns>序列化后的 JSON 字符串。</returns>
        string Serialize<T>(T data);

        /// <summary>
        /// 将 JSON 字符串反序列化为指定类型。
        /// </summary>
        /// <param name="json">JSON 字符串。</param>
        /// <typeparam name="T">数据类型。</typeparam>
        /// <returns>反序列化后的对象。</returns>
        T Deserialize<T>(string json);
    }
}
