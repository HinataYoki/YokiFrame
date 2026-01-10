#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.IO;
using System.Text;
using UnityEngine;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// 多字节 XOR 解密文件流
    /// 优点：流式解密，内存占用低，性能好
    /// </summary>
    public sealed class XorBundleStream : FileStream
    {
        private readonly byte[] mKey;
        private long mPosition;

        public XorBundleStream(string path, FileMode mode, FileAccess access, FileShare share, byte[] key)
            : base(path, mode, access, share)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
            if (mKey.Length == 0) throw new ArgumentException("密钥不能为空", nameof(key));
            mPosition = 0;
        }

        public override int Read(byte[] array, int offset, int count)
        {
            int bytesRead = base.Read(array, offset, count);
            int keyLen = mKey.Length;

            for (int i = 0; i < bytesRead; i++)
            {
                array[offset + i] ^= mKey[(mPosition + i) % keyLen];
            }
            mPosition += bytesRead;

            return bytesRead;
        }

        public override int ReadByte()
        {
            int b = base.ReadByte();
            if (b >= 0)
            {
                b ^= mKey[mPosition % mKey.Length];
                mPosition++;
            }
            return b;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            long result = base.Seek(offset, origin);
            mPosition = result;
            return result;
        }

        public override long Position
        {
            get => base.Position;
            set
            {
                base.Position = value;
                mPosition = value;
            }
        }
    }

    /// <summary>
    /// XOR 流式加密
    /// </summary>
    public sealed class XorStreamEncryption : IEncryptionServices
    {
        private readonly byte[] mKey;

        public XorStreamEncryption(byte[] key)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
            if (mKey.Length == 0) throw new ArgumentException("密钥不能为空", nameof(key));
        }

        public EncryptResult Encrypt(EncryptFileInfo fileInfo)
        {
            if (string.IsNullOrEmpty(fileInfo.FileLoadPath))
                return new EncryptResult { Encrypted = false };

            try
            {
                var data = File.ReadAllBytes(fileInfo.FileLoadPath);
                int keyLen = mKey.Length;

                for (int i = 0; i < data.Length; i++)
                {
                    data[i] ^= mKey[i % keyLen];
                }

                return new EncryptResult { Encrypted = true, EncryptedData = data };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XorStreamEncryption] 加密失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return new EncryptResult { Encrypted = false };
            }
        }
    }

    /// <summary>
    /// XOR 流式解密
    /// </summary>
    public sealed class XorStreamDecryption : IDecryptionServices
    {
        private readonly byte[] mKey;
        private const uint BUFFER_SIZE = 1024;

        public XorStreamDecryption(byte[] key)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
            if (mKey.Length == 0) throw new ArgumentException("密钥不能为空", nameof(key));
        }

        DecryptResult IDecryptionServices.LoadAssetBundle(DecryptFileInfo fileInfo)
        {
            try
            {
                var stream = new XorBundleStream(fileInfo.FileLoadPath, FileMode.Open, FileAccess.Read, FileShare.Read, mKey);
                return new DecryptResult
                {
                    ManagedStream = stream,
                    Result = AssetBundle.LoadFromStream(stream, fileInfo.FileLoadCRC, BUFFER_SIZE)
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XorStreamDecryption] 加载 AssetBundle 失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return new DecryptResult();
            }
        }

        DecryptResult IDecryptionServices.LoadAssetBundleAsync(DecryptFileInfo fileInfo)
        {
            try
            {
                var stream = new XorBundleStream(fileInfo.FileLoadPath, FileMode.Open, FileAccess.Read, FileShare.Read, mKey);
                return new DecryptResult
                {
                    ManagedStream = stream,
                    CreateRequest = AssetBundle.LoadFromStreamAsync(stream, fileInfo.FileLoadCRC, BUFFER_SIZE)
                };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XorStreamDecryption] 异步加载 AssetBundle 失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return new DecryptResult();
            }
        }

        byte[] IDecryptionServices.ReadFileData(DecryptFileInfo fileInfo)
        {
            try
            {
                var data = File.ReadAllBytes(fileInfo.FileLoadPath);
                int keyLen = mKey.Length;
                for (int i = 0; i < data.Length; i++)
                {
                    data[i] ^= mKey[i % keyLen];
                }
                return data;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XorStreamDecryption] 读取文件失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return Array.Empty<byte>();
            }
        }

        string IDecryptionServices.ReadFileText(DecryptFileInfo fileInfo)
        {
            var bytes = ((IDecryptionServices)this).ReadFileData(fileInfo);
            return bytes == null || bytes.Length == 0 ? string.Empty : Encoding.UTF8.GetString(bytes);
        }

        public DecryptResult LoadAssetBundleFallback(DecryptFileInfo fileInfo)
        {
            try
            {
                var data = ((IDecryptionServices)this).ReadFileData(fileInfo);
                if (data == null || data.Length == 0)
                    return new DecryptResult();
                return new DecryptResult { Result = AssetBundle.LoadFromMemory(data) };
            }
            catch (Exception ex)
            {
                Debug.LogError($"[XorStreamDecryption] Fallback 加载失败: {fileInfo.FileLoadPath}, 错误: {ex.Message}");
                return new DecryptResult();
            }
        }
    }
}
#endif
