#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// AES 加密（安全性高，但性能开销大）
    /// </summary>
    public sealed class AesEncryption : IEncryptionServices
    {
        private readonly byte[] mKey;
        private readonly byte[] mIV;

        public AesEncryption(byte[] key, byte[] iv)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
            mIV = iv ?? throw new ArgumentNullException(nameof(iv));
            
            if (mKey.Length != 16 && mKey.Length != 24 && mKey.Length != 32)
                throw new ArgumentException("AES 密钥长度必须为 16/24/32 字节", nameof(key));
            if (mIV.Length != 16)
                throw new ArgumentException("AES IV 长度必须为 16 字节", nameof(iv));
        }

        public EncryptResult Encrypt(EncryptFileInfo fileInfo)
        {
            if (string.IsNullOrEmpty(fileInfo.FileLoadPath))
                return new EncryptResult { Encrypted = false };

            try
            {
                var data = File.ReadAllBytes(fileInfo.FileLoadPath);
                var encrypted = EncryptBytes(data);
                return new EncryptResult { Encrypted = true, EncryptedData = encrypted };
            }
            catch (Exception ex)
            {
                KitLogger.DebugError($"[AesEncryption] 加密失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return new EncryptResult { Encrypted = false };
            }
        }

        private byte[] EncryptBytes(byte[] plain)
        {
            using var aes = Aes.Create();
            aes.Key = mKey;
            aes.IV = mIV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
            cs.Write(plain, 0, plain.Length);
            cs.FlushFinalBlock();
            return ms.ToArray();
        }
    }

    /// <summary>
    /// AES 解密
    /// 注意：需要全量解密到内存，大文件会有性能问题
    /// </summary>
    public sealed class AesDecryption : IDecryptionServices
    {
        private readonly byte[] mKey;
        private readonly byte[] mIV;

        public AesDecryption(byte[] key, byte[] iv)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
            mIV = iv ?? throw new ArgumentNullException(nameof(iv));
            
            if (mKey.Length != 16 && mKey.Length != 24 && mKey.Length != 32)
                throw new ArgumentException("AES 密钥长度必须为 16/24/32 字节", nameof(key));
            if (mIV.Length != 16)
                throw new ArgumentException("AES IV 长度必须为 16 字节", nameof(iv));
        }

        DecryptResult IDecryptionServices.LoadAssetBundle(DecryptFileInfo fileInfo)
        {
            try
            {
                var data = DecryptFile(fileInfo.FileLoadPath);
                if (data == null || data.Length == 0)
                    return new DecryptResult();
                return new DecryptResult { Result = AssetBundle.LoadFromMemory(data, fileInfo.FileLoadCRC) };
            }
            catch (Exception ex)
            {
                KitLogger.DebugError($"[AesDecryption] 加载 AssetBundle 失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return new DecryptResult();
            }
        }

        DecryptResult IDecryptionServices.LoadAssetBundleAsync(DecryptFileInfo fileInfo)
        {
            try
            {
                // 注意：文件读取和解密仍是同步的，仅 AB 加载是异步
                var data = DecryptFile(fileInfo.FileLoadPath);
                if (data == null || data.Length == 0)
                    return new DecryptResult();
                return new DecryptResult { CreateRequest = AssetBundle.LoadFromMemoryAsync(data, fileInfo.FileLoadCRC) };
            }
            catch (Exception ex)
            {
                KitLogger.DebugError($"[AesDecryption] 异步加载 AssetBundle 失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return new DecryptResult();
            }
        }

        byte[] IDecryptionServices.ReadFileData(DecryptFileInfo fileInfo)
            => DecryptFile(fileInfo.FileLoadPath);

        string IDecryptionServices.ReadFileText(DecryptFileInfo fileInfo)
        {
            var bytes = DecryptFile(fileInfo.FileLoadPath);
            return bytes == null || bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
        }

        public DecryptResult LoadAssetBundleFallback(DecryptFileInfo fileInfo)
            => ((IDecryptionServices)this).LoadAssetBundle(fileInfo);

        private byte[] DecryptFile(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    KitLogger.DebugError($"[AesDecryption] 文件不存在: {path}");
                    return Array.Empty<byte>();
                }
                
                var encrypted = File.ReadAllBytes(path);
                if (encrypted.Length == 0)
                {
                    KitLogger.DebugWarning($"[AesDecryption] 文件为空: {path}");
                    return Array.Empty<byte>();
                }
                
                return DecryptBytes(encrypted);
            }
            catch (CryptographicException ex)
            {
                KitLogger.DebugError($"[AesDecryption] 解密失败（密钥可能不匹配）: {path}, 错误: {ex.Message}");
                return Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                KitLogger.DebugError($"[AesDecryption] 读取文件失败: {path}, 错误: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        private byte[] DecryptBytes(byte[] encrypted)
        {
            using var aes = Aes.Create();
            aes.Key = mKey;
            aes.IV = mIV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
            cs.Write(encrypted, 0, encrypted.Length);
            cs.FlushFinalBlock();
            return ms.ToArray();
        }
    }
}
#endif
