#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
using System;
using System.IO;
using System.Security.Cryptography;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// AES 整包加密器 — 3.x 版本（实现 IBundleEncryptor）
    /// 用于构建时加密资源包，运行时由 AesDecryption（IBundleMemoryDecryptor）解密
    /// </summary>
    public sealed class AesBundleEncryptor : IBundleEncryptor
    {
        private readonly byte[] mKey;
        private readonly byte[] mIV;

        public AesBundleEncryptor(byte[] key, byte[] iv)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
            mIV = iv ?? throw new ArgumentNullException(nameof(iv));

            if (mKey.Length != 16 && mKey.Length != 24 && mKey.Length != 32)
                throw new ArgumentException("AES 密钥长度必须为 16/24/32 字节", nameof(key));
            if (mIV.Length != 16)
                throw new ArgumentException("AES IV 长度必须为 16 字节", nameof(iv));
        }

        BundleEncryptResult IBundleEncryptor.Encrypt(BundleEncryptArgs args)
        {
            if (string.IsNullOrEmpty(args.FilePath) || !File.Exists(args.FilePath))
                return new BundleEncryptResult(false, null);

            try
            {
                var data = File.ReadAllBytes(args.FilePath);

                using var aes = Aes.Create();
                aes.Key = mKey;
                aes.IV = mIV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var ms = new MemoryStream();
                using var cs = new CryptoStream(ms, aes.CreateEncryptor(), CryptoStreamMode.Write);
                cs.Write(data, 0, data.Length);
                cs.FlushFinalBlock();

                return new BundleEncryptResult(true, ms.ToArray());
            }
            catch (Exception ex)
            {
                KitLogger.DebugError($"[AesBundleEncryptor] 加密失败: {args.FilePath}, 错误: {ex.Message}");
                return new BundleEncryptResult(false, null);
            }
        }
    }
}
#endif
