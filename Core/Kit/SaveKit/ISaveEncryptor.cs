namespace YokiFrame
{
    /// <summary>
    /// 存档加密器接口
    /// 定义数据加密和解密的方法
    /// </summary>
    public interface ISaveEncryptor
    {
        /// <summary>
        /// 加密数据
        /// </summary>
        byte[] Encrypt(byte[] data);

        /// <summary>
        /// 解密数据
        /// </summary>
        byte[] Decrypt(byte[] data);
    }
}
