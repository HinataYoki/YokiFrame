#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
using System;
using System.IO;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// XOR 流式解密 — 3.x 版本（实现 IBundleStreamDecryptor）
    /// </summary>
    public sealed class XorStreamDecryption : IBundleStreamDecryptor
    {
        private readonly byte[] mKey;

        public XorStreamDecryption(byte[] key)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
            if (mKey.Length == 0) throw new ArgumentException("密钥不能为空", nameof(key));
        }

        Stream IBundleStreamDecryptor.CreateDecryptionStream(BundleDecryptArgs args)
        {
            if (string.IsNullOrEmpty(args.FilePath) || !File.Exists(args.FilePath))
                return null;

            return new XorDecryptStream(args.FilePath, mKey);
        }

        int IBundleStreamDecryptor.GetBufferSize(BundleDecryptArgs args)
            => 1024;
    }

    /// <summary>
    /// XOR 解密文件流
    /// </summary>
    internal sealed class XorDecryptStream : FileStream
    {
        private readonly byte[] mKey;
        private long mPosition;

        public XorDecryptStream(string path, byte[] key)
            : base(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, FileOptions.SequentialScan)
        {
            mKey = key;
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
}
#endif
