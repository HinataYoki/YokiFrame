#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
using System;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// 文件偏移解密 — 3.x 版本（实现 IBundleOffsetDecryptor）
    /// </summary>
    public sealed class FileOffsetDecryption : IBundleOffsetDecryptor
    {
        private readonly long mOffset;

        public FileOffsetDecryption(int offset)
        {
            if (offset <= 0) throw new ArgumentException("偏移量必须大于 0", nameof(offset));
            mOffset = offset;
        }

        long IBundleOffsetDecryptor.GetFileOffset(BundleDecryptArgs args)
            => mOffset;
    }
}
#endif
