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

        #region 密钥生成（版本通用）

        /// <summary>
        /// 获取 XOR 密钥（32 字节）
        /// </summary>
        public byte[] GetXorKey()
        {
            if (mXorKey is null)
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
            if (mAesKey is null) InitAesKeyIV();
            return mAesKey;
        }

        /// <summary>
        /// 获取 AES IV（16 字节）
        /// </summary>
        public byte[] GetAesIV()
        {
            if (mAesIV is null) InitAesKeyIV();
            return mAesIV;
        }

        private void InitAesKeyIV()
        {
            var saltBytes = Encoding.UTF8.GetBytes(AesSalt);
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

#if YOOASSET_3_0_OR_NEWER
        #region 解密服务（3.x — IBundleDecryptor）

        /// <summary>
        /// 自定义解密器工厂（运行时解密，选择 Custom 类型时使用）
        /// </summary>
        public static Func<YooInitConfig, IBundleDecryptor> CustomDecryptorFactory { get; set; }

        /// <summary>
        /// 自定义加密器工厂（构建时加密，选择 Custom 类型时使用）
        /// </summary>
        public static Func<YooInitConfig, IBundleEncryptor> CustomEncryptorFactory { get; set; }

        /// <summary>
        /// 创建 Bundle 解密器实例（运行时 — 3.x 使用 IBundleDecryptor 接口族）
        /// </summary>
        public IBundleDecryptor CreateBundleDecryptor() => EncryptionType switch
        {
            YooEncryptionType.XorStream => new XorStreamDecryption(GetXorKey()),
            YooEncryptionType.FileOffset => new FileOffsetDecryption(FileOffset),
            YooEncryptionType.Aes => new AesDecryption(GetAesKey(), GetAesIV()),
            YooEncryptionType.Custom => CustomDecryptorFactory?.Invoke(this)
                ?? throw new InvalidOperationException("[YooInitConfig] 使用 Custom 加密类型时必须设置 CustomDecryptorFactory"),
            _ => null
        };

        /// <summary>
        /// 创建 Bundle 加密器实例（构建时 — 3.x 使用 IBundleEncryptor）
        /// 返回的加密器可赋值到 BuildParameters.BundleEncryptor 供构建管线使用
        /// </summary>
        public IBundleEncryptor CreateBundleEncryptor() => EncryptionType switch
        {
            YooEncryptionType.XorStream => new XorBundleEncryptor(GetXorKey()),
            YooEncryptionType.FileOffset => new FileOffsetBundleEncryptor(FileOffset),
            YooEncryptionType.Aes => new AesBundleEncryptor(GetAesKey(), GetAesIV()),
            YooEncryptionType.Custom => CustomEncryptorFactory?.Invoke(this)
                ?? throw new InvalidOperationException("[YooInitConfig] 使用 Custom 加密类型时必须设置 CustomEncryptorFactory"),
            _ => null
        };

        #endregion
#else
        #region 加密/解密服务（2.x — IEncryptionServices / IDecryptionServices）

        /// <summary>
        /// 自定义加密服务工厂（选择 Custom 类型时使用）
        /// </summary>
        public static Func<YooInitConfig, IEncryptionServices> CustomEncryptionFactory { get; set; }

        /// <summary>
        /// 自定义解密服务工厂（选择 Custom 类型时使用）
        /// </summary>
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
#endif
    }
}
#endif
