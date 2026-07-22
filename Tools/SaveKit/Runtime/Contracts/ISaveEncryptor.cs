namespace YokiFrame
{
    /// <summary>
    /// SaveKit payload 加密契约。实现必须同时提供机密性和完整性保护。
    /// </summary>
    public interface ISaveEncryptor
    {
        /// <summary>获取稳定的加密算法 ID。</summary>
        string EncryptorId { get; }

        /// <summary>加密 payload。</summary>
        /// <param name="data">原始 payload。</param>
        /// <returns>加密后的 payload。</returns>
        byte[] Encrypt(byte[] data);

        /// <summary>解密并验证 payload。</summary>
        /// <param name="data">加密 payload。</param>
        /// <returns>解密后的 payload。</returns>
        byte[] Decrypt(byte[] data);
    }
}
