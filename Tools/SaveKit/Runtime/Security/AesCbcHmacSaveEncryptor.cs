using System;
using System.IO;
using System.Security.Cryptography;

namespace YokiFrame
{
    /// <summary>
    /// 使用 PBKDF2、随机 salt/IV、AES-CBC 和 HMAC-SHA256 保护 SaveKit payload。
    /// </summary>
    public sealed class AesCbcHmacSaveEncryptor : ISaveEncryptor
    {
        private const int SALT_BYTES = 16;
        private const int IV_BYTES = 16;
        private const int KEY_BYTES = 64;
        private const int TAG_BYTES = 32;
        private const int ITERATIONS = 100000;
        private const byte FORMAT_VERSION = 1;
        private readonly string mPassword;

        /// <summary>创建基于项目密码的认证加密器。</summary>
        /// <param name="password">项目私有密码。</param>
        public AesCbcHmacSaveEncryptor(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                throw new ArgumentNullException(nameof(password));
            }

            mPassword = password;
        }

        /// <inheritdoc />
        public string EncryptorId
        {
            get { return "aes-cbc-hmac-sha256-v1"; }
        }

        /// <inheritdoc />
        public byte[] Encrypt(byte[] data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var salt = new byte[SALT_BYTES];
            var iv = new byte[IV_BYTES];
            using (var random = RandomNumberGenerator.Create())
            {
                random.GetBytes(salt);
                random.GetBytes(iv);
            }

            DeriveKeys(salt, out var encryptionKey, out var macKey);
            byte[] cipherText;
            using (var aes = CreateAes(encryptionKey, iv))
            using (var output = new MemoryStream())
            using (var crypto = new CryptoStream(output, aes.CreateEncryptor(), CryptoStreamMode.Write))
            {
                crypto.Write(data, 0, data.Length);
                crypto.FlushFinalBlock();
                cipherText = output.ToArray();
            }

            var unsigned = BuildUnsignedPayload(salt, iv, cipherText);
            var tag = ComputeTag(macKey, unsigned);
            var result = new byte[unsigned.Length + tag.Length];
            Buffer.BlockCopy(unsigned, 0, result, 0, unsigned.Length);
            Buffer.BlockCopy(tag, 0, result, unsigned.Length, tag.Length);
            return result;
        }

        /// <inheritdoc />
        public byte[] Decrypt(byte[] data)
        {
            if (data == null || data.Length < 1 + SALT_BYTES + IV_BYTES + TAG_BYTES)
            {
                throw new CryptographicException("Encrypted save payload is truncated.");
            }

            var offset = 0;
            if (data[offset++] != FORMAT_VERSION)
            {
                throw new CryptographicException("Encrypted save payload version is unsupported.");
            }

            var salt = ReadBytes(data, ref offset, SALT_BYTES);
            var iv = ReadBytes(data, ref offset, IV_BYTES);
            var cipherLength = data.Length - offset - TAG_BYTES;
            if (cipherLength <= 0)
            {
                throw new CryptographicException("Encrypted save payload has no ciphertext.");
            }

            var cipherText = ReadBytes(data, ref offset, cipherLength);
            var expectedTag = ReadBytes(data, ref offset, TAG_BYTES);
            DeriveKeys(salt, out var encryptionKey, out var macKey);
            var unsigned = BuildUnsignedPayload(salt, iv, cipherText);
            var actualTag = ComputeTag(macKey, unsigned);
            if (!FixedTimeEquals(expectedTag, actualTag))
            {
                throw new CryptographicException("Encrypted save payload authentication failed.");
            }

            using (var aes = CreateAes(encryptionKey, iv))
            using (var input = new MemoryStream(cipherText, false))
            using (var crypto = new CryptoStream(input, aes.CreateDecryptor(), CryptoStreamMode.Read))
            using (var output = new MemoryStream())
            {
                crypto.CopyTo(output);
                return output.ToArray();
            }
        }

        /// <summary>从项目密码和 salt 派生加密/认证密钥。</summary>
        private void DeriveKeys(byte[] salt, out byte[] encryptionKey, out byte[] macKey)
        {
            using (var derivation = new Rfc2898DeriveBytes(mPassword, salt, ITERATIONS, HashAlgorithmName.SHA256))
            {
                var keys = derivation.GetBytes(KEY_BYTES);
                encryptionKey = new byte[32];
                macKey = new byte[32];
                Buffer.BlockCopy(keys, 0, encryptionKey, 0, 32);
                Buffer.BlockCopy(keys, 32, macKey, 0, 32);
            }
        }

        /// <summary>创建 AES-CBC 加密实例。</summary>
        private static Aes CreateAes(byte[] key, byte[] iv)
        {
            var aes = Aes.Create();
            aes.Key = key;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes;
        }

        /// <summary>构造不含 HMAC 的认证输入。</summary>
        private static byte[] BuildUnsignedPayload(byte[] salt, byte[] iv, byte[] cipherText)
        {
            var result = new byte[1 + salt.Length + iv.Length + cipherText.Length];
            result[0] = FORMAT_VERSION;
            Buffer.BlockCopy(salt, 0, result, 1, salt.Length);
            Buffer.BlockCopy(iv, 0, result, 1 + salt.Length, iv.Length);
            Buffer.BlockCopy(cipherText, 0, result, 1 + salt.Length + iv.Length, cipherText.Length);
            return result;
        }

        /// <summary>计算认证标签。</summary>
        private static byte[] ComputeTag(byte[] macKey, byte[] unsigned)
        {
            using (var hmac = new HMACSHA256(macKey))
            {
                return hmac.ComputeHash(unsigned);
            }
        }

        /// <summary>从输入中读取固定长度字节段。</summary>
        private static byte[] ReadBytes(byte[] source, ref int offset, int length)
        {
            if (length < 0 || offset < 0 || offset + length > source.Length)
            {
                throw new CryptographicException("Encrypted save payload is truncated.");
            }

            var result = new byte[length];
            Buffer.BlockCopy(source, offset, result, 0, length);
            offset += length;
            return result;
        }

        /// <summary>执行不因首个差异提前退出的字节比较。</summary>
        private static bool FixedTimeEquals(byte[] left, byte[] right)
        {
            if (left == null || right == null || left.Length != right.Length)
            {
                return false;
            }

            var difference = 0;
            for (var i = 0; i < left.Length; i++)
            {
                difference |= left[i] ^ right[i];
            }

            return difference == 0;
        }
    }
}
