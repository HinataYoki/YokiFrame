#if UNITY_5_3_OR_NEWER && YOKIFRAME_YOOASSET_SUPPORT && YOKIFRAME_YOOASSET_2_OR_3
using System;
using System.Security.Cryptography;
using System.Text;
using YooAsset;

namespace YokiFrame.Unity
{
    /// <summary>
    /// 将纯数据初始化参数转换为当前 YooAsset 主版本需要的加密与解密服务。
    /// 自定义方案通过显式工厂注入，初始化参数本身不保存行为对象。
    /// </summary>
    public static class YooAssetEncryptionServices
    {
#if YOKIFRAME_YOOASSET_3
        /// <summary>获取或设置 YooAsset V3 自定义 Bundle 解密器工厂。</summary>
        public static Func<YooAssetInitializationOptions, IBundleDecryptor> CustomDecryptorFactory { get; set; }

        /// <summary>获取或设置 YooAsset V3 自定义 Bundle 加密器工厂。</summary>
        public static Func<YooAssetInitializationOptions, IBundleEncryptor> CustomEncryptorFactory { get; set; }

        /// <summary>按初始化参数创建 YooAsset V3 Bundle 解密器。</summary>
        /// <param name="options">初始化与密钥参数。</param>
        /// <returns>匹配方案的解密器；未加密时返回 null。</returns>
        public static IBundleDecryptor CreateBundleDecryptor(YooAssetInitializationOptions options)
        {
            EnsureOptions(options);
            switch (options.EncryptionMode)
            {
                case YooAssetEncryptionMode.XorStream:
                    return new YooAssetXorStreamDecryptor(CreateXorKey(options));
                case YooAssetEncryptionMode.FileOffset:
                    return new YooAssetFileOffsetDecryptor(options.FileOffset);
                case YooAssetEncryptionMode.Aes:
                    CreateAesKeyAndIv(options, out byte[] key, out byte[] iv);
                    return new YooAssetAesDecryptor(key, iv);
                case YooAssetEncryptionMode.Custom:
                    return CustomDecryptorFactory?.Invoke(options)
                        ?? throw new InvalidOperationException(
                            "Custom encryption requires YooAssetEncryptionServices.CustomDecryptorFactory.");
                default:
                    return null;
            }
        }

        /// <summary>按初始化参数创建 YooAsset V3 Bundle 加密器。</summary>
        /// <param name="options">初始化与密钥参数。</param>
        /// <returns>匹配方案的加密器；未加密时返回 null。</returns>
        public static IBundleEncryptor CreateBundleEncryptor(YooAssetInitializationOptions options)
        {
            EnsureOptions(options);
            switch (options.EncryptionMode)
            {
                case YooAssetEncryptionMode.XorStream:
                    return new YooAssetXorBundleEncryptor(CreateXorKey(options));
                case YooAssetEncryptionMode.FileOffset:
                    return new YooAssetFileOffsetBundleEncryptor(options.FileOffset);
                case YooAssetEncryptionMode.Aes:
                    CreateAesKeyAndIv(options, out byte[] key, out byte[] iv);
                    return new YooAssetAesBundleEncryptor(key, iv);
                case YooAssetEncryptionMode.Custom:
                    return CustomEncryptorFactory?.Invoke(options)
                        ?? throw new InvalidOperationException(
                            "Custom encryption requires YooAssetEncryptionServices.CustomEncryptorFactory.");
                default:
                    return null;
            }
        }
#else
        /// <summary>获取或设置 YooAsset V2 自定义解密服务工厂。</summary>
        public static Func<YooAssetInitializationOptions, IDecryptionServices> CustomDecryptionFactory { get; set; }

        /// <summary>获取或设置 YooAsset V2 自定义加密服务工厂。</summary>
        public static Func<YooAssetInitializationOptions, IEncryptionServices> CustomEncryptionFactory { get; set; }

        /// <summary>按初始化参数创建 YooAsset V2 解密服务。</summary>
        /// <param name="options">初始化与密钥参数。</param>
        /// <returns>匹配方案的解密服务；未加密时返回 null。</returns>
        public static IDecryptionServices CreateDecryptionServices(
            YooAssetInitializationOptions options)
        {
            EnsureOptions(options);
            switch (options.EncryptionMode)
            {
                case YooAssetEncryptionMode.XorStream:
                    return new YooAssetXorStreamDecryptionService(CreateXorKey(options));
                case YooAssetEncryptionMode.FileOffset:
                    return new YooAssetFileOffsetDecryptionService(options.FileOffset);
                case YooAssetEncryptionMode.Aes:
                    CreateAesKeyAndIv(options, out byte[] key, out byte[] iv);
                    return new YooAssetAesDecryptionService(key, iv);
                case YooAssetEncryptionMode.Custom:
                    return CustomDecryptionFactory?.Invoke(options)
                        ?? throw new InvalidOperationException(
                            "Custom encryption requires YooAssetEncryptionServices.CustomDecryptionFactory.");
                default:
                    return null;
            }
        }

        /// <summary>按初始化参数创建 YooAsset V2 构建加密服务。</summary>
        /// <param name="options">初始化与密钥参数。</param>
        /// <returns>匹配方案的加密服务；未加密时返回 null。</returns>
        public static IEncryptionServices CreateEncryptionServices(
            YooAssetInitializationOptions options)
        {
            EnsureOptions(options);
            switch (options.EncryptionMode)
            {
                case YooAssetEncryptionMode.XorStream:
                    return new YooAssetXorStreamEncryptionService(CreateXorKey(options));
                case YooAssetEncryptionMode.FileOffset:
                    return new YooAssetFileOffsetEncryptionService(options.FileOffset);
                case YooAssetEncryptionMode.Aes:
                    CreateAesKeyAndIv(options, out byte[] key, out byte[] iv);
                    return new YooAssetAesEncryptionService(key, iv);
                case YooAssetEncryptionMode.Custom:
                    return CustomEncryptionFactory?.Invoke(options)
                        ?? throw new InvalidOperationException(
                            "Custom encryption requires YooAssetEncryptionServices.CustomEncryptionFactory.");
                default:
                    return null;
            }
        }
#endif

        /// <summary>校验初始化参数实例。</summary>
        private static void EnsureOptions(YooAssetInitializationOptions options)
        {
            if (options == null)
                throw new ArgumentNullException(nameof(options));
        }

        /// <summary>使用 SHA256 从字符串种子生成固定 32 字节 XOR 密钥。</summary>
        private static byte[] CreateXorKey(YooAssetInitializationOptions options)
        {
            if (string.IsNullOrEmpty(options.XorKeySeed))
                throw new InvalidOperationException("XOR key seed cannot be empty.");

            using (SHA256 sha256 = SHA256.Create())
                return sha256.ComputeHash(Encoding.UTF8.GetBytes(options.XorKeySeed));
        }

        /// <summary>使用 PBKDF2 从密码和盐值派生 AES-256 Key 与 128 位 IV。</summary>
        private static void CreateAesKeyAndIv(
            YooAssetInitializationOptions options,
            out byte[] key,
            out byte[] iv)
        {
            if (string.IsNullOrEmpty(options.AesPassword))
                throw new InvalidOperationException("AES password cannot be empty.");

            byte[] salt = CreateAesSalt(options.AesSalt);
            using (Rfc2898DeriveBytes deriveBytes = new(
                options.AesPassword,
                salt,
                10000,
                HashAlgorithmName.SHA256))
            {
                key = deriveBytes.GetBytes(32);
                iv = deriveBytes.GetBytes(16);
            }
        }

        /// <summary>将 AES 盐值补齐到 PBKDF2 所需的最小 8 字节。</summary>
        private static byte[] CreateAesSalt(string value)
        {
            byte[] source = Encoding.UTF8.GetBytes(value ?? string.Empty);
            if (source.Length >= 8)
                return source;

            byte[] padded = new byte[8];
            Buffer.BlockCopy(source, 0, padded, 0, source.Length);
            return padded;
        }
    }
}
#endif
