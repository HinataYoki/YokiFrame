using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// 基于 AES-CBC 的保存数据加密器。
    /// </summary>
    public sealed class AesSaveEncryptor : ISaveEncryptor
    {
        private const int AES_KEY_LENGTH = 32;
        private const int AES_IV_LENGTH = 16;

        private readonly byte[] key;
        private readonly byte[] iv;

        /// <summary>
        /// 禁止使用默认密码创建 AES 保存数据加密器。
        /// </summary>
        [Obsolete("Use AesSaveEncryptor(string password) or AesSaveEncryptor(byte[] key, byte[] iv) with a project-specific secret.", false)]
        public AesSaveEncryptor()
            : this(GetMissingProjectSecret())
        {
        }

        /// <summary>
        /// 使用密码派生密钥和 IV 创建 AES 保存数据加密器。
        /// </summary>
        /// <param name="password">用于派生密钥和 IV 的密码。</param>
        public AesSaveEncryptor(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
            using (SHA256 sha256 = SHA256.Create())
            {
                key = sha256.ComputeHash(passwordBytes);
            }

            using (MD5 md5 = MD5.Create())
            {
                iv = md5.ComputeHash(Encoding.UTF8.GetBytes(password + "IV"));
            }
        }

        /// <summary>
        /// 使用显式密钥和 IV 创建 AES 保存数据加密器。
        /// </summary>
        /// <param name="key">32 字节 AES-256 密钥。</param>
        /// <param name="iv">16 字节 AES IV。</param>
        public AesSaveEncryptor(byte[] key, byte[] iv)
        {
            if (key == null || key.Length != AES_KEY_LENGTH)
            {
                throw new ArgumentException("Key must be 32 bytes (256 bits).", nameof(key));
            }

            if (iv == null || iv.Length != AES_IV_LENGTH)
            {
                throw new ArgumentException("IV must be 16 bytes (128 bits).", nameof(iv));
            }

            this.key = CopyBytes(key);
            this.iv = CopyBytes(iv);
        }

        /// <inheritdoc />
        public byte[] Encrypt(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Data cannot be null or empty.", nameof(data));
            }

            using (Aes aes = CreateAes())
            using (ICryptoTransform encryptor = aes.CreateEncryptor())
            using (MemoryStream stream = new MemoryStream())
            {
                using (CryptoStream cryptoStream = new CryptoStream(stream, encryptor, CryptoStreamMode.Write))
                {
                    cryptoStream.Write(data, 0, data.Length);
                }

                return stream.ToArray();
            }
        }

        /// <inheritdoc />
        public byte[] Decrypt(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                throw new ArgumentException("Data cannot be null or empty.", nameof(data));
            }

            using (Aes aes = CreateAes())
            using (ICryptoTransform decryptor = aes.CreateDecryptor())
            using (MemoryStream inputStream = new MemoryStream(data))
            using (CryptoStream cryptoStream = new CryptoStream(inputStream, decryptor, CryptoStreamMode.Read))
            using (MemoryStream resultStream = new MemoryStream())
            {
                cryptoStream.CopyTo(resultStream);
                return resultStream.ToArray();
            }
        }

        private Aes CreateAes()
        {
            Aes aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes;
        }

        private static string GetMissingProjectSecret()
        {
            throw new InvalidOperationException("AesSaveEncryptor requires a project-specific password, key, or IV.");
        }

        private static byte[] CopyBytes(byte[] source)
        {
            byte[] copy = new byte[source.Length];
            Buffer.BlockCopy(source, 0, copy, 0, source.Length);
            return copy;
        }
    }
}
