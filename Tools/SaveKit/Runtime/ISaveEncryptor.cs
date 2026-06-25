namespace YokiFrame
{
    /// <summary>
    /// 保存数据加密器接口。
    /// </summary>
    public interface ISaveEncryptor
    {
        /// <summary>
        /// 加密保存数据载荷。
        /// </summary>
        /// <param name="data">原始保存数据字节。</param>
        /// <returns>加密后的保存数据字节。</returns>
        byte[] Encrypt(byte[] data);

        /// <summary>
        /// 解密保存数据载荷。
        /// </summary>
        /// <param name="data">加密后的保存数据字节。</param>
        /// <returns>解密后的原始保存数据字节。</returns>
        byte[] Decrypt(byte[] data);
    }
}
