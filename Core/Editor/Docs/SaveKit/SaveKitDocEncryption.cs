#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SaveKit 加密与序列化文档
    /// </summary>
    internal static class SaveKitDocEncryption
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "加密与序列化",
                Description = "支持自定义加密器和序列化器。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "配置加密",
                        Code = @"// 使用 AES 加密（仅加密数据部分，头部不加密）
var encryptor = new AesSaveEncryptor(""your-secret-key"");
SaveKit.SetEncryptor(encryptor);

// 禁用加密
SaveKit.SetEncryptor(null);

// 自定义序列化器（默认使用 JsonUtility）
// 可替换为 MessagePack 等高性能序列化器
SaveKit.SetSerializer(new MessagePackSerializer());",
                        Explanation = "头部不加密便于快速验证文件有效性，数据部分加密保护隐私。"
                    }
                }
            };
        }
    }
}
#endif
