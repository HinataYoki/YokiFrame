#if YOKIFRAME_YOOASSET_SUPPORT
using System;
using System.Security.Cryptography;
using System.Text;
using YooAsset;

namespace YokiFrame
{
    /// <summary>
    /// YooInitConfig - 加密相关
    /// </summary>
    public partial class YooInitConfig
    {
        #region 内部缓存

        private byte[] mXorKey;
        private byte[] mAesKey;
        private byte[] mAesIV;

        #endregion

        #region 密钥生成

        /// <summary>
        /// 获取 XOR 密钥（32 字节）
        /// </summary>
        public byte[] GetXorKey()
        {
            if (mXorKey == null)
            {
                using var sha = SHA256.Create();
                mXorKey = sha.ComputeHash(Encoding.UTF8.GetBytes(XorKeySeed));
            }
            return mXorKey;
        }

        /// <summary>
        /// 获取 AES Key（32 字节）
        /// </summary>
        public byte[] GetAesKey()
        {
            if (mAesKey == null) InitAesKeyIV();
            return mAesKey;
        }

        /// <summary>
        /// 获取 AES IV（16 字节）
        /// </summary>
        public byte[] GetAesIV()
        {
            if (mAesIV == null) InitAesKeyIV();
            return mAesIV;
        }

        private void InitAesKeyIV()
        {
            var saltBytes = Encoding.UTF8.GetBytes(AesSalt);
            // 确保盐值至少 8 字节
            if (saltBytes.Length < 8)
            {
                var padded = new byte[8];
                Array.Copy(saltBytes, padded, saltBytes.Length);
                saltBytes = padded;
            }

            using var deriveBytes = new Rfc2898DeriveBytes(
                AesPassword,
                saltBytes,
                iterations: 10000,
                HashAlgorithmName.SHA256);

            mAesKey = deriveBytes.GetBytes(32);
            mAesIV = deriveBytes.GetBytes(16);
        }

        /// <summary>
        /// 重置密钥缓存（修改密钥参数后调用）
        /// </summary>
        public void ResetKeyCache()
        {
            mXorKey = null;
            mAesKey = null;
            mAesIV = null;
        }

        #endregion

        #region 服务创建

        /// <summary>
        /// 自定义加密服务工厂（选择 Custom 类型时使用）
        /// </summary>
        /// <example>
        /// YooInitConfig.CustomEncryptionFactory = config => new MyCustomEncryption();
        /// </example>
        public static Func<YooInitConfig, IEncryptionServices> CustomEncryptionFactory { get; set; }

        /// <summary>
        /// 自定义解密服务工厂（选择 Custom 类型时使用）
        /// </summary>
        /// <example>
        /// YooInitConfig.CustomDecryptionFactory = config => new MyCustomDecryption();
        /// </example>
        public static Func<YooInitConfig, IDecryptionServices> CustomDecryptionFactory { get; set; }

        /// <summary>
        /// 创建解密服务实例
        /// </summary>
        public IDecryptionServices CreateDecryptionServices() => EncryptionType switch
        {
            YooEncryptionType.XorStream => new XorStreamDecryption(GetXorKey()),
            YooEncryptionType.FileOffset => new FileOffsetDecryption(FileOffset),
            YooEncryptionType.Aes => new AesDecryption(GetAesKey(), GetAesIV()),
            YooEncryptionType.Custom => CustomDecryptionFactory?.Invoke(this) 
                ?? throw new InvalidOperationException("[YooInitConfig] 使用 Custom 加密类型时必须设置 CustomDecryptionFactory"),
            _ => null
        };

        /// <summary>
        /// 创建加密服务实例
        /// </summary>
        public IEncryptionServices CreateEncryptionServices() => EncryptionType switch
        {
            YooEncryptionType.XorStream => new XorStreamEncryption(GetXorKey()),
            YooEncryptionType.FileOffset => new FileOffsetEncryption(FileOffset),
            YooEncryptionType.Aes => new AesEncryption(GetAesKey(), GetAesIV()),
            YooEncryptionType.Custom => CustomEncryptionFactory?.Invoke(this)
                ?? throw new InvalidOperationException("[YooInitConfig] 使用 Custom 加密类型时必须设置 CustomEncryptionFactory"),
            _ => null
        };

        #endregion
    }
}
#endif
