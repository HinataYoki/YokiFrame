#if UNITY_EDITOR && YOKIFRAME_YOOASSET_SUPPORT && UNITY_2022_1_OR_NEWER
using System.Collections.Generic;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using static YokiFrame.EditorTools.YokiFrameUIComponents;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// YooInitConfig 属性绘制器 - 加密配置 UI
    /// </summary>
    public partial class YooInitConfigDrawer
    {
        #region 加密配置 UI

        /// <summary>
        /// 根据加密类型更新 UI
        /// </summary>
        private void UpdateEncryptionUI(VisualElement container, UnityEditor.SerializedProperty property, YooEncryptionType type)
        {
            container.Clear();

            switch (type)
            {
                case YooEncryptionType.XorStream:
                    container.Add(CreateXorEncryptionUI(property));
                    break;

                case YooEncryptionType.FileOffset:
                    container.Add(CreateFileOffsetUI(property));
                    break;

                case YooEncryptionType.Aes:
                    container.Add(CreateAesEncryptionUI(property));
                    break;

                case YooEncryptionType.None:
                default:
                    container.Add(CreateEncryptionCard(
                        "无加密",
                        "资源将以明文形式存储。",
                        Colors.TextTertiary,
                        null
                    ));
                    break;

                case YooEncryptionType.Custom:
                    container.Add(CreateEncryptionCard(
                        "自定义加密",
                        "需要在代码中设置 YooInitConfig.CustomEncryptionFactory 和 CustomDecryptionFactory 委托。",
                        Colors.BrandPrimary,
                        null
                    ));
                    break;
            }
        }

        /// <summary>
        /// 创建 XOR 加密配置 UI
        /// </summary>
        private VisualElement CreateXorEncryptionUI(UnityEditor.SerializedProperty property)
        {
            var xorContent = new VisualElement();
            var xorSeedField = CreatePropertyField(property, "XorKeySeed", "密钥种子");
            xorContent.Add(xorSeedField);
            
            var xorSeedProp = property.FindPropertyRelative("XorKeySeed");
            xorContent.Add(CreateResetButton("重置为默认密钥", () =>
            {
                xorSeedProp.stringValue = YooInitConfig.DEFAULT_XOR_SEED;
                property.serializedObject.ApplyModifiedProperties();
            }));
            
            var xorValidation = CreateValidationHint();
            xorContent.Add(xorValidation);
            
            xorSeedField.TrackPropertyValue(xorSeedProp, _ => UpdateXorValidation(xorValidation, xorSeedProp.stringValue));
            UpdateXorValidation(xorValidation, xorSeedProp.stringValue);
            
            return CreateEncryptionCard(
                "XOR 流式加密",
                "性能好，安全性适中。密钥种子会通过 SHA256 生成 32 字节密钥。",
                Colors.StatusInfo,
                xorContent
            );
        }

        /// <summary>
        /// 创建文件偏移配置 UI
        /// </summary>
        private VisualElement CreateFileOffsetUI(UnityEditor.SerializedProperty property)
        {
            var offsetContent = new VisualElement();
            offsetContent.Add(CreateOffsetDropdown(property));
            
            var offsetProp = property.FindPropertyRelative("FileOffset");
            offsetContent.Add(CreateResetButton("重置为默认偏移量", () =>
            {
                offsetProp.intValue = 32;
                property.serializedObject.ApplyModifiedProperties();
            }));
            
            return CreateEncryptionCard(
                "文件偏移",
                "仅防止直接打开，无实际加密。适合简单防护场景。",
                Colors.StatusWarning,
                offsetContent
            );
        }

        /// <summary>
        /// 创建 AES 加密配置 UI
        /// </summary>
        private VisualElement CreateAesEncryptionUI(UnityEditor.SerializedProperty property)
        {
            var aesContent = new VisualElement();
            var aesPasswordField = CreatePropertyField(property, "AesPassword", "密码");
            var aesSaltField = CreatePropertyField(property, "AesSalt", "盐值");
            aesContent.Add(aesPasswordField);
            aesContent.Add(aesSaltField);
            
            var aesPasswordProp = property.FindPropertyRelative("AesPassword");
            var aesSaltProp = property.FindPropertyRelative("AesSalt");
            aesContent.Add(CreateResetButton("重置为默认密钥", () =>
            {
                aesPasswordProp.stringValue = YooInitConfig.DEFAULT_AES_PASSWORD;
                aesSaltProp.stringValue = "YokiFram";
                property.serializedObject.ApplyModifiedProperties();
            }));
            
            var aesValidation = CreateValidationHint();
            aesContent.Add(aesValidation);
            
            aesPasswordField.TrackPropertyValue(aesPasswordProp, _ => UpdateAesValidation(aesValidation, aesPasswordProp.stringValue, aesSaltProp.stringValue));
            aesSaltField.TrackPropertyValue(aesSaltProp, _ => UpdateAesValidation(aesValidation, aesPasswordProp.stringValue, aesSaltProp.stringValue));
            UpdateAesValidation(aesValidation, aesPasswordProp.stringValue, aesSaltProp.stringValue);
            
            return CreateEncryptionCard(
                "AES 加密",
                "安全性高，但性能开销大。密码和盐值通过 PBKDF2 派生密钥。",
                Colors.StatusSuccess,
                aesContent
            );
        }

        #endregion

        #region 加密验证

        /// <summary>
        /// 更新 XOR 加密验证提示
        /// </summary>
        private static void UpdateXorValidation(VisualElement hint, string seed)
        {
            if (hint is not Label label) return;

            if (string.IsNullOrEmpty(seed))
            {
                label.text = "密钥种子不能为空";
                label.style.color = new StyleColor(Colors.StatusError);
                label.style.display = DisplayStyle.Flex;
            }
            else if (seed.Length < 8)
            {
                label.text = $"密钥种子长度建议至少 8 个字符（当前 {seed.Length} 个）";
                label.style.color = new StyleColor(Colors.StatusWarning);
                label.style.display = DisplayStyle.Flex;
            }
            else
            {
                label.style.display = DisplayStyle.None;
            }
        }

        /// <summary>
        /// 更新 AES 加密验证提示
        /// </summary>
        private static void UpdateAesValidation(VisualElement hint, string password, string salt)
        {
            if (hint is not Label label) return;

            var errors = new List<string>();

            if (string.IsNullOrEmpty(password))
                errors.Add("密码不能为空");
            else if (password.Length < 8)
                errors.Add($"密码长度建议至少 8 个字符（当前 {password.Length} 个）");

            if (string.IsNullOrEmpty(salt))
                errors.Add("盐值不能为空");
            else
            {
                var saltBytes = System.Text.Encoding.UTF8.GetBytes(salt);
                if (saltBytes.Length < 8)
                    errors.Add($"盐值字节长度不足 8 字节（当前 {saltBytes.Length} 字节）");
            }

            if (errors.Count > 0)
            {
                label.text = string.Join("\n", errors);
                label.style.color = new StyleColor(errors.Exists(e => e.Contains("不能为空")) ? Colors.StatusError : Colors.StatusWarning);
                label.style.display = DisplayStyle.Flex;
            }
            else
            {
                label.style.display = DisplayStyle.None;
            }
        }

        #endregion
    }
}
#endif
