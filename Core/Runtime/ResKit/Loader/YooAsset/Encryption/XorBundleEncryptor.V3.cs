#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
using System;
using System.IO;
using System.Security.Cryptography;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// XOR 流式加密器 — 3.x 版本（实现 IBundleEncryptor）
    /// 用于构建时加密资源包，运行时由 XorStreamDecryption（IBundleStreamDecryptor）解密
    /// </summary>
    public sealed class XorBundleEncryptor : IBundleEncryptor
    {
        private readonly byte[] mKey;

        public XorBundleEncryptor(byte[] key)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
            if (mKey.Length == 0) throw new ArgumentException("密钥不能为空", nameof(key));
        }

        BundleEncryptResult IBundleEncryptor.Encrypt(BundleEncryptArgs args)
        {
            if (string.IsNullOrEmpty(args.FilePath) || !File.Exists(args.FilePath))
                return new BundleEncryptResult(false, null);

            try
            {
                var data = File.ReadAllBytes(args.FilePath);
                int keyLen = mKey.Length;

                for (int i = 0; i < data.Length; i++)
                    data[i] ^= mKey[i % keyLen];

                return new BundleEncryptResult(true, data);
            }
            catch (Exception ex)
            {
                KitLogger.DebugError($"[XorBundleEncryptor] 加密失败: {args.FilePath}, 错误: {ex.Message}");
                return new BundleEncryptResult(false, null);
            }
        }
    }
}
#endif
