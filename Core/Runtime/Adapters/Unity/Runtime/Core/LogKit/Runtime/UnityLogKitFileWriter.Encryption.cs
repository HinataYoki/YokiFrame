#if !GODOT
using System;
using System.Security.Cryptography;
using System.Text;

namespace YokiFrame.Unity
{
    /// <summary>
    /// Unity 日志文件写入器的日志加密辅助逻辑。
    /// </summary>
    public static partial class UnityLogKitFileWriter
    {
        private const int INITIAL_BUFFER_SIZE = 64 * 1024;
        private const string DEFAULT_KEY = "0123456789ABCDEF";
        private const string DEFAULT_IV = "FEDCBA9876543210";

        private static readonly byte[] sKey = Encoding.UTF8.GetBytes(DEFAULT_KEY);
        private static readonly byte[] sIV = Encoding.UTF8.GetBytes(DEFAULT_IV);

        private static byte[] sSharedBuffer;

        /// <summary>
        /// 使用 LogKit 默认密钥加密字符串。
        /// </summary>
        /// <param name="text">要加密的文本。</param>
        /// <returns>加密后的 Base64 文本；加密失败时返回原文本。</returns>
        public static string EncryptString(string text) => EncryptString(text, null);

        /// <summary>
        /// 使用 LogKit 默认密钥解密字符串。
        /// </summary>
        /// <param name="text">要解密的 Base64 文本。</param>
        /// <returns>解密后的文本；解密失败时返回带失败标记的文本。</returns>
        public static string DecryptString(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            try
            {
                using (var aes = CreateAes())
                using (var decryptor = aes.CreateDecryptor())
                {
                    var input = Convert.FromBase64String(text);
                    var output = decryptor.TransformFinalBlock(input, 0, input.Length);
                    return Encoding.UTF8.GetString(output);
                }
            }
            catch
            {
                return "[DECRYPT_FAIL] " + text;
            }
        }

        private static string EncryptString(string text, UnityLogKitOptions options)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;

            try
            {
                using (var aes = CreateAes())
                using (var encryptor = aes.CreateEncryptor())
                {
                    var byteCount = Encoding.UTF8.GetByteCount(text);
                    var buffer = GetSharedBuffer(byteCount);
                    Encoding.UTF8.GetBytes(text, 0, text.Length, buffer, 0);
                    var output = encryptor.TransformFinalBlock(buffer, 0, byteCount);
                    return Convert.ToBase64String(output);
                }
            }
            catch
            {
                return text;
            }
        }

        private static Aes CreateAes()
        {
            var aes = Aes.Create();
            aes.Key = sKey;
            aes.IV = sIV;
            aes.Padding = PaddingMode.PKCS7;
            aes.Mode = CipherMode.CBC;
            return aes;
        }

        private static byte[] GetSharedBuffer(int byteCount)
        {
            lock (sLock)
            {
                if (sSharedBuffer == null)
                    sSharedBuffer = new byte[Math.Max(INITIAL_BUFFER_SIZE, byteCount)];
                if (sSharedBuffer.Length < byteCount)
                    Array.Resize(ref sSharedBuffer, Math.Max(byteCount, sSharedBuffer.Length * 2));

                return sSharedBuffer;
            }
        }
    }
}
#endif
