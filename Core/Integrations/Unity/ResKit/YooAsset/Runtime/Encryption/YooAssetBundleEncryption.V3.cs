#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3 && YOKIFRAME_YOOASSET_3
using System;
using System.IO;
using System.Security.Cryptography;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>YooAsset V3 XOR 流式解密器。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.XorStream,
        YooAssetEncryptionImplementationRole.Decryption)]
    public sealed class YooAssetXorStreamDecryptor : IBundleStreamDecryptor
    {
        private readonly byte[] mKey;

        /// <summary>创建 XOR 流式解密器。</summary>
        public YooAssetXorStreamDecryptor(byte[] key)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
            if (mKey.Length == 0)
                throw new ArgumentException("XOR key cannot be empty.", nameof(key));
        }

        /// <summary>为当前 Bundle 创建 XOR 解密流。</summary>
        Stream IBundleStreamDecryptor.CreateDecryptionStream(BundleDecryptArgs args)
        {
            return string.IsNullOrEmpty(args.FilePath) || !File.Exists(args.FilePath)
                ? null
                : new YooAssetXorDecryptStreamV3(args.FilePath, mKey);
        }

        /// <summary>返回流式解密需要的缓冲区大小。</summary>
        int IBundleStreamDecryptor.GetBufferSize(BundleDecryptArgs args)
        {
            return 1024;
        }
    }

    /// <summary>YooAsset V3 文件偏移解密器。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.FileOffset,
        YooAssetEncryptionImplementationRole.Decryption)]
    public sealed class YooAssetFileOffsetDecryptor : IBundleOffsetDecryptor
    {
        private readonly long mOffset;

        /// <summary>创建使用正数文件偏移的解密器。</summary>
        public YooAssetFileOffsetDecryptor(int offset)
        {
            if (offset <= 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            mOffset = offset;
        }

        /// <summary>返回当前 Bundle 需要跳过的文件头字节数。</summary>
        long IBundleOffsetDecryptor.GetFileOffset(BundleDecryptArgs args)
        {
            return mOffset;
        }
    }

    /// <summary>YooAsset V3 AES 全量内存解密器。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.Aes,
        YooAssetEncryptionImplementationRole.Decryption)]
    public sealed class YooAssetAesDecryptor : IBundleMemoryDecryptor
    {
        private readonly byte[] mKey;
        private readonly byte[] mIv;

        /// <summary>创建使用 AES Key 和 IV 的解密器。</summary>
        public YooAssetAesDecryptor(byte[] key, byte[] iv)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));
            if (iv == null)
                throw new ArgumentNullException(nameof(iv));
            if (key.Length != 16 && key.Length != 24 && key.Length != 32)
                throw new ArgumentException("AES key must contain 16, 24, or 32 bytes.", nameof(key));
            if (iv.Length != 16)
                throw new ArgumentException("AES IV must contain 16 bytes.", nameof(iv));
            mKey = key;
            mIv = iv;
        }

        /// <summary>读取文件并返回 AES-CBC 解密后的字节。</summary>
        byte[] IBundleMemoryDecryptor.GetDecryptedData(BundleDecryptArgs args)
        {
            if (string.IsNullOrEmpty(args.FilePath) || !File.Exists(args.FilePath))
                return Array.Empty<byte>();

            try
            {
                using (Aes aes = Aes.Create())
                {
                    aes.Key = mKey;
                    aes.IV = mIv;
                    aes.Mode = CipherMode.CBC;
                    aes.Padding = PaddingMode.PKCS7;
                    byte[] encrypted = File.ReadAllBytes(args.FilePath);
                    using (MemoryStream output = new())
                    using (CryptoStream crypto = new(
                        output,
                        aes.CreateDecryptor(),
                        CryptoStreamMode.Write))
                    {
                        crypto.Write(encrypted, 0, encrypted.Length);
                        crypto.FlushFinalBlock();
                        return output.ToArray();
                    }
                }
            }
            catch (Exception exception)
            {
                LogKit.Exception(exception);
                return Array.Empty<byte>();
            }
        }
    }

    /// <summary>YooAsset V3 XOR 构建加密器。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.XorStream,
        YooAssetEncryptionImplementationRole.Encryption)]
    public sealed class YooAssetXorBundleEncryptor : IBundleEncryptor
    {
        private readonly byte[] mKey;

        /// <summary>创建 XOR 构建加密器。</summary>
        public YooAssetXorBundleEncryptor(byte[] key)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
        }

        /// <summary>读取文件并返回 XOR 加密结果。</summary>
        BundleEncryptResult IBundleEncryptor.Encrypt(BundleEncryptArgs args)
        {
            if (string.IsNullOrEmpty(args.FilePath) || !File.Exists(args.FilePath))
                return new BundleEncryptResult(false, null);

            byte[] data = File.ReadAllBytes(args.FilePath);
            for (int index = 0; index < data.Length; index++)
                data[index] ^= mKey[index % mKey.Length];
            return new BundleEncryptResult(true, data);
        }
    }

    /// <summary>YooAsset V3 文件偏移构建加密器。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.FileOffset,
        YooAssetEncryptionImplementationRole.Encryption)]
    public sealed class YooAssetFileOffsetBundleEncryptor : IBundleEncryptor
    {
        private readonly int mOffset;

        /// <summary>创建文件偏移构建加密器。</summary>
        public YooAssetFileOffsetBundleEncryptor(int offset)
        {
            if (offset <= 0)
                throw new ArgumentOutOfRangeException(nameof(offset));
            mOffset = offset;
        }

        /// <summary>在原始文件前插入随机偏移数据。</summary>
        BundleEncryptResult IBundleEncryptor.Encrypt(BundleEncryptArgs args)
        {
            if (string.IsNullOrEmpty(args.FilePath) || !File.Exists(args.FilePath))
                return new BundleEncryptResult(false, null);

            byte[] data = File.ReadAllBytes(args.FilePath);
            byte[] result = new byte[data.Length + mOffset];
            byte[] noise = new byte[mOffset];
            using (RandomNumberGenerator generator = RandomNumberGenerator.Create())
                generator.GetBytes(noise);
            Buffer.BlockCopy(noise, 0, result, 0, noise.Length);
            Buffer.BlockCopy(data, 0, result, mOffset, data.Length);
            return new BundleEncryptResult(true, result);
        }
    }

    /// <summary>YooAsset V3 AES 构建加密器。</summary>
    [YooAssetEncryptionImplementation(
        YooAssetEncryptionMode.Aes,
        YooAssetEncryptionImplementationRole.Encryption)]
    public sealed class YooAssetAesBundleEncryptor : IBundleEncryptor
    {
        private readonly byte[] mKey;
        private readonly byte[] mIv;

        /// <summary>创建 AES 构建加密器。</summary>
        public YooAssetAesBundleEncryptor(byte[] key, byte[] iv)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
            mIv = iv ?? throw new ArgumentNullException(nameof(iv));
        }

        /// <summary>读取文件并返回 AES-CBC 加密结果。</summary>
        BundleEncryptResult IBundleEncryptor.Encrypt(BundleEncryptArgs args)
        {
            if (string.IsNullOrEmpty(args.FilePath) || !File.Exists(args.FilePath))
                return new BundleEncryptResult(false, null);

            using (Aes aes = Aes.Create())
            {
                aes.Key = mKey;
                aes.IV = mIv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;
                using (MemoryStream output = new())
                using (CryptoStream crypto = new(
                    output,
                    aes.CreateEncryptor(),
                    CryptoStreamMode.Write))
                {
                    byte[] data = File.ReadAllBytes(args.FilePath);
                    crypto.Write(data, 0, data.Length);
                    crypto.FlushFinalBlock();
                    return new BundleEncryptResult(true, output.ToArray());
                }
            }
        }
    }

    /// <summary>YooAsset V3 XOR 解密流。</summary>
    internal sealed class YooAssetXorDecryptStreamV3 : FileStream
    {
        private readonly byte[] mKey;

        /// <summary>打开文件并绑定 XOR 密钥。</summary>
        internal YooAssetXorDecryptStreamV3(string path, byte[] key)
            : base(path, FileMode.Open, FileAccess.Read, FileShare.Read)
        {
            mKey = key;
        }

        /// <summary>读取后按当前位置解密缓冲区。</summary>
        public override int Read(byte[] array, int offset, int count)
        {
            long position = Position;
            int read = base.Read(array, offset, count);
            for (int index = 0; index < read; index++)
                array[offset + index] ^= mKey[(int)((position + index) % mKey.Length)];
            return read;
        }
    }
}
#endif
