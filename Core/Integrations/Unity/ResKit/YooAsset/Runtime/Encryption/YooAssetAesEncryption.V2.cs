#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3 && !YOKIFRAME_YOOASSET_3
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>YooAsset V2 构建阶段使用的 AES-CBC 加密服务。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.Aes,
        YooAssetEncryptionImplementationRole.Encryption)]
    public sealed class YooAssetAesEncryptionService : IEncryptionServices
    {
        private readonly byte[] mKey;
        private readonly byte[] mIv;

        /// <summary>创建使用有效 AES Key 和 IV 的加密服务。</summary>
        public YooAssetAesEncryptionService(byte[] key, byte[] iv)
        {
            ValidateKeyAndIv(key, iv);
            mKey = key;
            mIv = iv;
        }

        /// <summary>读取完整 Bundle 并返回 AES-CBC 加密数据。</summary>
        EncryptResult IEncryptionServices.Encrypt(EncryptFileInfo fileInfo)
        {
            if (string.IsNullOrEmpty(fileInfo.FileLoadPath) || !File.Exists(fileInfo.FileLoadPath))
                return new EncryptResult { Encrypted = false };

            try
            {
                byte[] data = File.ReadAllBytes(fileInfo.FileLoadPath);
                return new EncryptResult
                {
                    Encrypted = true,
                    EncryptedData = Transform(data, mKey, mIv, true)
                };
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception);
                return new EncryptResult { Encrypted = false };
            }
        }

        /// <summary>校验 AES Key 与 IV 长度。</summary>
        internal static void ValidateKeyAndIv(byte[] key, byte[] iv)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (iv == null)
                throw new ArgumentNullException(nameof(iv));
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("AES key must contain 16, 24, or 32 bytes.", nameof(key));
            if (iv.Length != 16)
                throw new ArgumentException("AES IV must contain 16 bytes.", nameof(iv));
        }

        /// <summary>使用 AES-CBC 对完整缓冲区执行加密或解密。</summary>
        internal static byte[] Transform(
            byte[] data,
            byte[] key,
            byte[] iv,
            bool encrypt)
        {
            using (Aes aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using (MemoryStream output = new())
                using (ICryptoTransform transform = encrypt
                           ? aes.CreateEncryptor()
                           : aes.CreateDecryptor())
                using (CryptoStream crypto = new(output, transform, CryptoStreamMode.Write))
                {
                    crypto.Write(data, 0, data.Length);
                    crypto.FlushFinalBlock();
                    return output.ToArray();
                }
            }
        }
    }

    /// <summary>YooAsset V2 运行时使用的 AES-CBC 全量解密服务。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.Aes,
        YooAssetEncryptionImplementationRole.Decryption)]
    public sealed class YooAssetAesDecryptionService : IDecryptionServices
    {
        private readonly byte[] mKey;
        private readonly byte[] mIv;

        /// <summary>创建使用有效 AES Key 和 IV 的解密服务。</summary>
        public YooAssetAesDecryptionService(byte[] key, byte[] iv)
        {
            YooAssetAesEncryptionService.ValidateKeyAndIv(key, iv);
            mKey = key;
            mIv = iv;
        }

        /// <summary>全量解密文件后同步从内存加载 AssetBundle。</summary>
        DecryptResult IDecryptionServices.LoadAssetBundle(DecryptFileInfo fileInfo)
        {
            byte[] data = ReadDecryptedFile(fileInfo.FileLoadPath);
            return data.Length == 0
                ? new DecryptResult()
                : new DecryptResult
                {
                    Result = AssetBundle.LoadFromMemory(data, fileInfo.FileLoadCRC)
                };
        }

        /// <summary>全量解密文件后异步从内存加载 AssetBundle。</summary>
        DecryptResult IDecryptionServices.LoadAssetBundleAsync(DecryptFileInfo fileInfo)
        {
            byte[] data = ReadDecryptedFile(fileInfo.FileLoadPath);
            return data.Length == 0
                ? new DecryptResult()
                : new DecryptResult
                {
                    CreateRequest = AssetBundle.LoadFromMemoryAsync(data, fileInfo.FileLoadCRC)
                };
        }

        /// <summary>后备路径复用同步内存加载；公开成员兼容早期 2.3 未声明该接口方法的版本。</summary>
        public DecryptResult LoadAssetBundleFallback(DecryptFileInfo fileInfo)
        {
            return ((IDecryptionServices)this).LoadAssetBundle(fileInfo);
        }

        /// <summary>读取并返回完整解密字节。</summary>
        byte[] IDecryptionServices.ReadFileData(DecryptFileInfo fileInfo)
        {
            return ReadDecryptedFile(fileInfo.FileLoadPath);
        }

        /// <summary>读取并返回 UTF-8 解密文本。</summary>
        string IDecryptionServices.ReadFileText(DecryptFileInfo fileInfo)
        {
            byte[] data = ReadDecryptedFile(fileInfo.FileLoadPath);
            return data.Length == 0 ? string.Empty : Encoding.UTF8.GetString(data);
        }

        /// <summary>读取目标文件并执行 AES-CBC 解密，失败时记录异常并返回空数组。</summary>
        private byte[] ReadDecryptedFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return Array.Empty<byte>();

            try
            {
                return YooAssetAesEncryptionService.Transform(
                    File.ReadAllBytes(path),
                    mKey,
                    mIv,
                    false);
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception);
                return Array.Empty<byte>();
            }
        }
    }
}
#endif
