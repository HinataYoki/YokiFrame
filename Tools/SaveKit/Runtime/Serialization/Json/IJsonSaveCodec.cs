namespace YokiFrame
{
    /// <summary>
    /// 宿主 JSON 编解码器。Unity Adapter 使用 JsonUtility，其他宿主可提供自己的 JSON 实现。
    /// </summary>
    public interface IJsonSaveCodec
    {
        /// <summary>将强类型对象编码为 JSON。</summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="data">对象实例。</param>
        /// <returns>JSON 文本。</returns>
        string Serialize<T>(T data);

        /// <summary>从 JSON 解码强类型对象。</summary>
        /// <typeparam name="T">对象类型。</typeparam>
        /// <param name="json">JSON 文本。</param>
        /// <returns>对象实例。</returns>
        T Deserialize<T>(string json);

        /// <summary>按运行时类型编码对象。</summary>
        /// <param name="data">对象实例。</param>
        /// <returns>JSON 文本。</returns>
        string Serialize(object data);

        /// <summary>把 JSON 覆盖到现有对象。</summary>
        /// <param name="json">JSON 文本。</param>
        /// <param name="target">目标对象。</param>
        void DeserializeOverwrite(string json, object target);
    }
}
