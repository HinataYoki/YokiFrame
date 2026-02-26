#if YOKIFRAME_YOOASSET_SUPPORT
using System.Collections.Generic;
using System.Text;

namespace YokiFrame
{
    /// <summary>
    /// YooInitConfig - 配置验证
    /// </summary>
    public partial class YooInitConfig
    {
        #region 配置验证

        /// <summary>
        /// 验证加密配置是否有效
        /// </summary>
        /// <param name="errorMessage">错误信息</param>
        /// <returns>是否有效</returns>
        public bool ValidateEncryption(out string errorMessage)
        {
            errorMessage = null;

            switch (EncryptionType)
            {
                case YooEncryptionType.None:
                    return true;

                case YooEncryptionType.XorStream:
                    return ValidateXorConfig(out errorMessage);

                case YooEncryptionType.FileOffset:
                    return ValidateFileOffsetConfig(out errorMessage);

                case YooEncryptionType.Aes:
                    return ValidateAesConfig(out errorMessage);

                case YooEncryptionType.Custom:
                    return ValidateCustomConfig(out errorMessage);

                default:
                    errorMessage = $"未知的加密类型: {EncryptionType}";
                    return false;
            }
        }

        /// <summary>
        /// 验证 XOR 配置
        /// </summary>
        private bool ValidateXorConfig(out string errorMessage)
        {
            if (string.IsNullOrEmpty(XorKeySeed))
            {
                errorMessage = "XOR 密钥种子不能为空";
                return false;
            }

            if (XorKeySeed.Length < 8)
            {
                errorMessage = "XOR 密钥种子长度建议至少 8 个字符以确保安全性";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 验证文件偏移配置
        /// </summary>
        private bool ValidateFileOffsetConfig(out string errorMessage)
        {
            if (FileOffset <= 0)
            {
                errorMessage = "文件偏移量必须大于 0";
                return false;
            }

            // 检查是否为 2 的幂
            if ((FileOffset & (FileOffset - 1)) != 0)
            {
                errorMessage = $"文件偏移量 {FileOffset} 不是 2 的幂，建议使用 16/32/64/128/256/512/1024";
                return false;
            }

            if (FileOffset < 16 || FileOffset > 1024)
            {
                errorMessage = $"文件偏移量 {FileOffset} 超出推荐范围 (16-1024)";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 验证 AES 配置
        /// </summary>
        private bool ValidateAesConfig(out string errorMessage)
        {
            if (string.IsNullOrEmpty(AesPassword))
            {
                errorMessage = "AES 密码不能为空";
                return false;
            }

            if (AesPassword.Length < 8)
            {
                errorMessage = "AES 密码长度建议至少 8 个字符以确保安全性";
                return false;
            }

            if (string.IsNullOrEmpty(AesSalt))
            {
                errorMessage = "AES 盐值不能为空";
                return false;
            }

            var saltBytes = Encoding.UTF8.GetBytes(AesSalt);
            if (saltBytes.Length < 8)
            {
                errorMessage = $"AES 盐值字节长度不足 8 字节（当前 {saltBytes.Length} 字节），建议使用至少 8 个 ASCII 字符";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 验证自定义加密配置
        /// </summary>
        private bool ValidateCustomConfig(out string errorMessage)
        {
            if (CustomEncryptionFactory == null && CustomDecryptionFactory == null)
            {
                errorMessage = "使用自定义加密时，必须设置 CustomEncryptionFactory 或 CustomDecryptionFactory";
                return false;
            }

            errorMessage = null;
            return true;
        }

        /// <summary>
        /// 验证整体配置
        /// </summary>
        /// <param name="errors">错误列表</param>
        /// <returns>是否有效</returns>
        public bool Validate(out List<string> errors)
        {
            errors = new();

            // 验证包名
            if (PackageNames is not { Count: > 0 })
            {
                errors.Add("资源包列表不能为空");
            }
            else
            {
                for (int i = 0; i < PackageNames.Count; i++)
                {
                    if (string.IsNullOrWhiteSpace(PackageNames[i]))
                    {
                        errors.Add($"资源包 #{i + 1} 名称不能为空");
                    }
                }

                // 检查重复
                var seen = new HashSet<string>();
                foreach (var name in PackageNames)
                {
                    if (!string.IsNullOrEmpty(name) && !seen.Add(name))
                    {
                        errors.Add($"资源包名称重复: {name}");
                    }
                }
            }

            // 验证加密配置
            if (!ValidateEncryption(out var encryptError))
            {
                errors.Add(encryptError);
            }

            return errors.Count == 0;
        }

        #endregion
    }
}
#endif
