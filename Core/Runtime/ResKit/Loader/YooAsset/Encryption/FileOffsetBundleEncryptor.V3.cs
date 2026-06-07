#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
using System;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// 文件偏移加密器 — 3.x 版本（实现 IBundleEncryptor）
    /// 用于构建时在文件头部填充随机数据，运行时由 FileOffsetDecryption（IBundleOffsetDecryptor）跳过
    /// </summary>
    public sealed class FileOffsetBundleEncryptor : IBundleEncryptor
    {
        private readonly int mOffset;

        public FileOffsetBundleEncryptor(int offset)
        {
            if (offset <= 0) throw new ArgumentException("偏移量必须大于 0", nameof(offset));
            mOffset = offset;
        }

        BundleEncryptResult IBundleEncryptor.Encrypt(BundleEncryptArgs args)
        {
            if (string.IsNullOrEmpty(args.FilePath))
                return new BundleEncryptResult(false, null);

            try
            {
                var data = System.IO.File.ReadAllBytes(args.FilePath);
                var encrypted = new byte[data.Length + mOffset];

                // 填充随机数据到偏移区域
                var noise = new byte[mOffset];
                using (var rng = System.Security.Cryptography.RandomNumberGenerator.Create())
                    rng.GetBytes(noise);
                Buffer.BlockCopy(noise, 0, encrypted, 0, mOffset);
                Buffer.BlockCopy(data, 0, encrypted, mOffset, data.Length);

                return new BundleEncryptResult(true, encrypted);
            }
            catch (Exception ex)
            {
                KitLogger.DebugError($"[FileOffsetBundleEncryptor] 加密失败: {args.FilePath}, 错误: {ex.Message}");
                return new BundleEncryptResult(false, null);
            }
        }
    }
}
#endif
