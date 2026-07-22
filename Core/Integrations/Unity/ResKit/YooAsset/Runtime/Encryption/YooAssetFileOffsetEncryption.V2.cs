#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3 && !YOKIFRAME_YOOASSET_3
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>YooAsset V2 构建阶段使用的文件头偏移加密服务。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.FileOffset,
        YooAssetEncryptionImplementationRole.Encryption)]
    public sealed class YooAssetFileOffsetEncryptionService : IEncryptionServices
    {
        private readonly int mOffset;

        /// <summary>创建使用正数文件头偏移量的加密服务。</summary>
        public YooAssetFileOffsetEncryptionService(int offset)
        {
            mOffset = ValidateOffset(offset);
        }

        /// <summary>在原始 Bundle 前写入随机字节形成文件偏移。</summary>
        EncryptResult IEncryptionServices.Encrypt(EncryptFileInfo fileInfo)
        {
            if (string.IsNullOrEmpty(fileInfo.FileLoadPath) || !File.Exists(fileInfo.FileLoadPath))
                return new EncryptResult { Encrypted = false };

            try
            {
                byte[] data = File.ReadAllBytes(fileInfo.FileLoadPath);
                byte[] encrypted = new byte[data.Length + mOffset];
                using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
                {
                    byte[] noise = new byte[mOffset];
                    generator.GetBytes(noise);
                    Buffer.BlockCopy(noise, 0, encrypted, 0, noise.Length);
                }
                Buffer.BlockCopy(data, 0, encrypted, mOffset, data.Length);
                return new EncryptResult { Encrypted = true, EncryptedData = encrypted };
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception);
                return new EncryptResult { Encrypted = false };
            }
        }

        /// <summary>校验偏移量必须大于零。</summary>
        internal static int ValidateOffset(int offset)
        {
            if (offset <= 0)
                throw new ArgumentOutOfRangeException(nameof(offset), "File offset must be greater than zero.");
            return offset;
        }
    }

    /// <summary>YooAsset V2 运行时使用的文件偏移解密服务。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.FileOffset,
        YooAssetEncryptionImplementationRole.Decryption)]
    public sealed class YooAssetFileOffsetDecryptionService : IDecryptionServices
    {
        private readonly int mOffset;
        private readonly ulong mAssetBundleOffset;

        /// <summary>创建使用正数文件头偏移量的解密服务。</summary>
        public YooAssetFileOffsetDecryptionService(int offset)
        {
            mOffset = YooAssetFileOffsetEncryptionService.ValidateOffset(offset);
            mAssetBundleOffset = (ulong)offset;
        }

        /// <summary>通过 Unity 文件偏移参数同步加载 AssetBundle。</summary>
        DecryptResult IDecryptionServices.LoadAssetBundle(DecryptFileInfo fileInfo)
        {
            return new DecryptResult
            {
                Result = AssetBundle.LoadFromFile(
                    fileInfo.FileLoadPath,
                    fileInfo.FileLoadCRC,
                    mAssetBundleOffset)
            };
        }

        /// <summary>通过 Unity 文件偏移参数异步加载 AssetBundle。</summary>
        DecryptResult IDecryptionServices.LoadAssetBundleAsync(DecryptFileInfo fileInfo)
        {
            return new DecryptResult
            {
                CreateRequest = AssetBundle.LoadFromFileAsync(
                    fileInfo.FileLoadPath,
                    fileInfo.FileLoadCRC,
                    mAssetBundleOffset)
            };
        }

        /// <summary>后备路径仍使用 Unity 原生文件偏移加载；公开成员兼容早期 2.3 未声明该接口方法的版本。</summary>
        public DecryptResult LoadAssetBundleFallback(DecryptFileInfo fileInfo)
        {
            return ((IDecryptionServices)this).LoadAssetBundle(fileInfo);
        }

        /// <summary>读取文件并剔除随机文件头。</summary>
        byte[] IDecryptionServices.ReadFileData(DecryptFileInfo fileInfo)
        {
            if (string.IsNullOrEmpty(fileInfo.FileLoadPath) || !File.Exists(fileInfo.FileLoadPath))
                return Array.Empty<byte>();

            byte[] data = File.ReadAllBytes(fileInfo.FileLoadPath);
            if (data.Length <= mOffset)
                return Array.Empty<byte>();

            byte[] result = new byte[data.Length - mOffset];
            Buffer.BlockCopy(data, mOffset, result, 0, result.Length);
            return result;
        }

        /// <summary>读取剔除文件头后的 UTF-8 文本。</summary>
        string IDecryptionServices.ReadFileText(DecryptFileInfo fileInfo)
        {
            byte[] data = ((IDecryptionServices)this).ReadFileData(fileInfo);
            return data.Length == 0 ? string.Empty : Encoding.UTF8.GetString(data);
        }
    }
}
#endif
