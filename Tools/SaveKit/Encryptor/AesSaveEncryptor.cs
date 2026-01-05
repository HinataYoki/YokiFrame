using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// AES 加密器 - 使用 AES-256-CBC 加密
    /// 默认的加密器实现
    /// </summary>
    public class AesSaveEncryptor : ISaveEncryptor
    {
        private readonly byte[] mKey;
        private readonly byte[] mIV;

        /// <summary>
        /// 使用默认密钥创建加密器
        /// </summary>
        public AesSaveEncryptor() : this("YokiFrameSaveKit2024!@#$")
        {
        }

        /// <summary>
        /// 使用自定义密钥创建加密器
        /// </summary>
        /// <param name="password">密码，将被转换为 256 位密钥</param>
        public AesSaveEncryptor(string password)
        {
            if (string.IsNullOrEmpty(password))
                throw new ArgumentNullException(nameof(password));

            // 使用 SHA256 生成 256 位密钥
            using var sha256 = SHA256.Create();
            mKey = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));

            // 使用 MD5 生成 128 位 IV
            using var md5 = MD5.Create();
            mIV = md5.ComputeHash(Encoding.UTF8.GetBytes(password + "IV"));
        }

        /// <summary>
        /// 使用自定义密钥和 IV 创建加密器
        /// </summary>
        /// <param name="key">256 位密钥 (32 字节)</param>
        /// <param name="iv">128 位 IV (16 字节)</param>
        public AesSaveEncryptor(byte[] key, byte[] iv)
        {
            if (key == null || key.Length != 32)
                throw new ArgumentException("Key must be 32 bytes (256 bits)", nameof(key));
            if (iv == null || iv.Length != 16)
                throw new ArgumentException("IV must be 16 bytes (128 bits)", nameof(iv));

            mKey = key;
            mIV = iv;
        }

        public byte[] Encrypt(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));

            using var aes = Aes.Create();
            aes.Key = mKey;
            aes.IV = mIV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var encryptor = aes.CreateEncryptor();
            using var ms = new MemoryStream();
            using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            {
                cs.Write(data, 0, data.Length);
            }
            return ms.ToArray();
        }

        public byte[] Decrypt(byte[] data)
        {
            if (data == null || data.Length == 0)
                throw new ArgumentException("Data cannot be null or empty", nameof(data));

            using var aes = Aes.Create();
            aes.Key = mKey;
            aes.IV = mIV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var decryptor = aes.CreateDecryptor();
            using var ms = new MemoryStream(data);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var result = new MemoryStream();
            cs.CopyTo(result);
            return result.ToArray();
        }
    }
}
