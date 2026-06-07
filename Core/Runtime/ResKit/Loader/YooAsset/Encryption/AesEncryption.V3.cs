#if YOKIFRAME_YOOASSET_SUPPORT && YOOASSET_3_0_OR_NEWER
using System;
using System.IO;
using System.Security.Cryptography;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// AES 整包解密 — 3.x 版本（实现 IBundleMemoryDecryptor）
    /// 注意：全量解密到内存，大 AB 有性能开销
    /// </summary>
    public sealed class AesDecryption : IBundleMemoryDecryptor
    {
        private readonly byte[] mKey;
        private readonly byte[] mIV;

        public AesDecryption(byte[] key, byte[] iv)
        {
            mKey = key ?? throw new ArgumentNullException(nameof(key));
            mIV = iv ?? throw new ArgumentNullException(nameof(iv));

            if (mKey.Length != 16 && mKey.Length != 24 && mKey.Length != 32)
                throw new ArgumentException("AES 密钥长度必须为 16/24/32 字节", nameof(key));
            if (mIV.Length != 16)
                throw new ArgumentException("AES IV 长度必须为 16 字节", nameof(iv));
        }

        byte[] IBundleMemoryDecryptor.GetDecryptedData(BundleDecryptArgs args)
        {
            if (string.IsNullOrEmpty(args.FilePath) || !File.Exists(args.FilePath))
            {
                KitLogger.DebugError($"[AesDecryption] 文件不存在: {args.FilePath}");
                return Array.Empty<byte>();
            }

            try
            {
                var encrypted = File.ReadAllBytes(args.FilePath);
                if (encrypted.Length == 0)
                {
                    KitLogger.DebugWarning($"[AesDecryption] 文件为空: {args.FilePath}");
                    return Array.Empty<byte>();
                }

                using var aes = Aes.Create();
                aes.Key = mKey;
                aes.IV = mIV;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var ms = new MemoryStream();
                using var cs = new CryptoStream(ms, aes.CreateDecryptor(), CryptoStreamMode.Write);
                cs.Write(encrypted, 0, encrypted.Length);
                cs.FlushFinalBlock();
                return ms.ToArray();
            }
            catch (CryptographicException ex)
            {
                KitLogger.DebugError($"[AesDecryption] 解密失败（密钥可能不匹配）: {args.FilePath}, 错误: {ex.Message}");
                return Array.Empty<byte>();
            }
            catch (Exception ex)
            {
                KitLogger.DebugError($"[AesDecryption] 读取文件失败: {args.FilePath}, 错误: {ex.Message}");
                return Array.Empty<byte>();
            }
        }
    }
}
#endif
