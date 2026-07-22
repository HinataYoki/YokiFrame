#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3 && !YOKIFRAME_YOOASSET_3
using System;
using System.IO;
using System.Text;
using UnityEngine;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>YooAsset V2 构建阶段使用的 XOR 字节加密服务。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.XorStream,
        YooAssetEncryptionImplementationRole.Encryption)]
    public sealed class YooAssetXorStreamEncryptionService : IEncryptionServices
    {
        private readonly byte[] mKey;

        /// <summary>创建使用指定非空密钥的 XOR 加密服务。</summary>
        public YooAssetXorStreamEncryptionService(byte[] key)
        {
            mKey = ValidateKey(key);
        }

        /// <summary>读取 Bundle 文件并使用循环密钥执行原地 XOR。</summary>
        EncryptResult IEncryptionServices.Encrypt(EncryptFileInfo fileInfo)
        {
            if (string.IsNullOrEmpty(fileInfo.FileLoadPath) || !File.Exists(fileInfo.FileLoadPath))
                return new EncryptResult { Encrypted = false };

            try
            {
                byte[] data = File.ReadAllBytes(fileInfo.FileLoadPath);
                ApplyXor(data, 0, data.Length, 0L, mKey);
                return new EncryptResult { Encrypted = true, EncryptedData = data };
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception);
                return new EncryptResult { Encrypted = false };
            }
        }

        /// <summary>校验并返回调用方提供的 XOR 密钥。</summary>
        private static byte[] ValidateKey(byte[] key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Length == 0)
                throw new ArgumentException("XOR key cannot be empty.", nameof(key));
            return key;
        }

        /// <summary>对指定缓冲区范围应用带绝对位置的循环 XOR。</summary>
        internal static void ApplyXor(
            byte[] buffer,
            int offset,
            int count,
            long position,
            byte[] key)
        {
            for (int index = 0; index < count; index++)
                buffer[offset + index] ^= key[(int)((position + index) % key.Length)];
        }
    }

    /// <summary>YooAsset V2 运行时使用的 XOR 流式解密服务。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.XorStream,
        YooAssetEncryptionImplementationRole.Decryption)]
    public sealed class YooAssetXorStreamDecryptionService : IDecryptionServices
    {
        private const uint BUFFER_SIZE = 1024;

        private readonly byte[] mKey;

        /// <summary>创建使用指定非空密钥的 XOR 解密服务。</summary>
        public YooAssetXorStreamDecryptionService(byte[] key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (key.Length == 0)
                throw new ArgumentException("XOR key cannot be empty.", nameof(key));
            mKey = key;
        }

        /// <summary>通过托管解密流同步加载 AssetBundle。</summary>
        DecryptResult IDecryptionServices.LoadAssetBundle(DecryptFileInfo fileInfo)
        {
            YooAssetXorDecryptStream stream = CreateStream(fileInfo.FileLoadPath);
            return stream == null
                ? new DecryptResult()
                : new DecryptResult
                {
                    ManagedStream = stream,
                    Result = AssetBundle.LoadFromStream(stream, fileInfo.FileLoadCRC, BUFFER_SIZE)
                };
        }

        /// <summary>通过托管解密流异步加载 AssetBundle。</summary>
        DecryptResult IDecryptionServices.LoadAssetBundleAsync(DecryptFileInfo fileInfo)
        {
            YooAssetXorDecryptStream stream = CreateStream(fileInfo.FileLoadPath);
            return stream == null
                ? new DecryptResult()
                : new DecryptResult
                {
                    ManagedStream = stream,
                    CreateRequest = AssetBundle.LoadFromStreamAsync(
                        stream,
                        fileInfo.FileLoadCRC,
                        BUFFER_SIZE)
                };
        }

        /// <summary>在流加载失败时使用全量内存数据作为后备；公开成员兼容早期 2.3 未声明该接口方法的版本。</summary>
        public DecryptResult LoadAssetBundleFallback(DecryptFileInfo fileInfo)
        {
            byte[] data = ((IDecryptionServices)this).ReadFileData(fileInfo);
            return data.Length == 0
                ? new DecryptResult()
                : new DecryptResult { Result = AssetBundle.LoadFromMemory(data, fileInfo.FileLoadCRC) };
        }

        /// <summary>读取并解密完整文件字节。</summary>
        byte[] IDecryptionServices.ReadFileData(DecryptFileInfo fileInfo)
        {
            if (string.IsNullOrEmpty(fileInfo.FileLoadPath) || !File.Exists(fileInfo.FileLoadPath))
                return Array.Empty<byte>();

            byte[] data = File.ReadAllBytes(fileInfo.FileLoadPath);
            YooAssetXorStreamEncryptionService.ApplyXor(data, 0, data.Length, 0L, mKey);
            return data;
        }

        /// <summary>读取并以 UTF-8 解码完整解密文本。</summary>
        string IDecryptionServices.ReadFileText(DecryptFileInfo fileInfo)
        {
            byte[] data = ((IDecryptionServices)this).ReadFileData(fileInfo);
            return data.Length == 0 ? string.Empty : Encoding.UTF8.GetString(data);
        }

        /// <summary>为存在的文件创建 XOR 解密流。</summary>
        private YooAssetXorDecryptStream CreateStream(string path)
        {
            return string.IsNullOrEmpty(path) || !File.Exists(path)
                ? null
                : new YooAssetXorDecryptStream(path, mKey);
        }
    }

    /// <summary>按文件绝对位置执行循环 XOR 的只读 FileStream。</summary>
    internal sealed class YooAssetXorDecryptStream : FileStream
    {
        private readonly byte[] mKey;

        /// <summary>打开目标文件并保存解密密钥。</summary>
        internal YooAssetXorDecryptStream(string path, byte[] key)
            : base(path, FileMode.Open, FileAccess.Read, FileShare.Read)
        {
            mKey = key;
        }

        /// <summary>读取文件后按读取起点解密当前缓冲区。</summary>
        public override int Read(byte[] array, int offset, int count)
        {
            long start = Position;
            int bytesRead = base.Read(array, offset, count);
            YooAssetXorStreamEncryptionService.ApplyXor(
                array,
                offset,
                bytesRead,
                start,
                mKey);
            return bytesRead;
        }

        /// <summary>读取并解密单个字节。</summary>
        public override int ReadByte()
        {
            long position = Position;
            int value = base.ReadByte();
            return value < 0 ? value : value ^ mKey[(int)(position % mKey.Length)];
        }
    }
}
#endif
