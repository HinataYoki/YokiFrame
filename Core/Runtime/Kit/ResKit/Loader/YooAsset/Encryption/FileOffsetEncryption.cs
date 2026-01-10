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
    /// 文件偏移加密（仅防止直接打开）
    /// </summary>
    public sealed class FileOffsetEncryption : IEncryptionServices
    {
        private readonly int mOffset;

        public FileOffsetEncryption(int offset)
        {
            if (offset <= 0) throw new ArgumentException("偏移量必须大于 0", nameof(offset));
            mOffset = offset;
        }

        public EncryptResult Encrypt(EncryptFileInfo fileInfo)
        {
            if (string.IsNullOrEmpty(fileInfo.FileLoadPath))
                return new EncryptResult { Encrypted = false };

            try
            {
                var data = File.ReadAllBytes(fileInfo.FileLoadPath);
                var encrypted = new byte[data.Length + mOffset];

                // 填充随机数据到偏移区域，增加混淆
                using (var rng = RandomNumberGenerator.Create())
                {
                    var noise = new byte[mOffset];
                    rng.GetBytes(noise);
                    Buffer.BlockCopy(noise, 0, encrypted, 0, mOffset);
                }

                Buffer.BlockCopy(data, 0, encrypted, mOffset, data.Length);
                return new EncryptResult { Encrypted = true, EncryptedData = encrypted };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileOffsetEncryption] 加密失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return new EncryptResult { Encrypted = false };
            }
        }
    }

    /// <summary>
    /// 文件偏移解密
    /// </summary>
    public sealed class FileOffsetDecryption : IDecryptionServices
    {
        private readonly ulong mOffset;
        private readonly int mOffsetInt;

        public FileOffsetDecryption(int offset)
        {
            if (offset <= 0) throw new ArgumentException("偏移量必须大于 0", nameof(offset));
            mOffset = (ulong)offset;
            mOffsetInt = offset;
        }

        DecryptResult IDecryptionServices.LoadAssetBundle(DecryptFileInfo fileInfo)
        {
            try
            {
                return new DecryptResult { Result = AssetBundle.LoadFromFile(fileInfo.FileLoadPath, fileInfo.FileLoadCRC, mOffset) };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileOffsetDecryption] 加载 AssetBundle 失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return new DecryptResult();
            }
        }

        DecryptResult IDecryptionServices.LoadAssetBundleAsync(DecryptFileInfo fileInfo)
        {
            try
            {
                return new DecryptResult { CreateRequest = AssetBundle.LoadFromFileAsync(fileInfo.FileLoadPath, fileInfo.FileLoadCRC, mOffset) };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileOffsetDecryption] 异步加载 AssetBundle 失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return new DecryptResult();
            }
        }

        byte[] IDecryptionServices.ReadFileData(DecryptFileInfo fileInfo)
        {
            try
            {
                var data = File.ReadAllBytes(fileInfo.FileLoadPath);
                
                // 检查数据长度是否足够
                if (data.Length <= mOffsetInt)
                {
                    Debug.LogError($"[FileOffsetDecryption] 文件长度 ({data.Length}) 小于偏移量 ({mOffsetInt}): {fileInfo.FileLoadPath}");
                    return Array.Empty<byte>();
                }
                
                var result = new byte[data.Length - mOffsetInt];
                Buffer.BlockCopy(data, mOffsetInt, result, 0, result.Length);
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[FileOffsetDecryption] 读取文件失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        string IDecryptionServices.ReadFileText(DecryptFileInfo fileInfo)
        {
            var bytes = ((IDecryptionServices)this).ReadFileData(fileInfo);
            return bytes == null || bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
        }

        public DecryptResult LoadAssetBundleFallback(DecryptFileInfo fileInfo)
            => ((IDecryptionServices)this).LoadAssetBundle(fileInfo);
    }
}
#endif
