#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// ResKit YooAsset 完整初始化示例文档 - 加密配置
    /// </summary>
    internal static partial class ResKitDocYooAssetComplete
    {
        /// <summary>
        /// YooInit 加密配置
        /// </summary>
        internal static DocSection CreateEncryptionSection()
        {
            return new DocSection
            {
                Title = "  YooInit 加密配置",
                Description = "YooInitConfig 内置 XOR / FileOffset / AES 三种加密方案（版本无关），Inspector 会根据加密类型动态显示对应配置项。自定义加密的 API 在 2.x 和 3.x 中不同。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "内置加密类型（版本无关）",
                        Code = @"// ---------- XOR 流式加密（推荐）----------
// 性能好，安全性适中，密钥种子通过 SHA256 生成 32 字节密钥
var xorConfig = new YooInitConfig
{
    EncryptionType = YooEncryptionType.XorStream,
    XorKeySeed = ""MySecretSeed_2025!@#$""
};

// ---------- 文件偏移 ----------
// 仅防止直接打开，无实际加密，适合简单防护
var offsetConfig = new YooInitConfig
{
    EncryptionType = YooEncryptionType.FileOffset,
    FileOffset = 64  // 可选：16/32/64/128/256/512/1024
};

// ---------- AES 加密 ----------
// 安全性高，性能开销较大，密码和盐值通过 PBKDF2 派生密钥
var aesConfig = new YooInitConfig
{
    EncryptionType = YooEncryptionType.Aes,
    AesPassword = ""MySecretPassword"",
    AesSalt = ""MySalt88""  // 至少 8 字节
};

// 以上三种方案直接传入 InitAsync 即可，无需手写加密类
await YooInit.InitAsync(xorConfig);",
                        Explanation = "内置方案无需关心 YooAsset 版本差异，YooInitConfig 自动处理 2.x 和 3.x 的加密服务创建。"
                    },
                    new()
                    {
                        Title = "自定义加密（YooAsset 3.x — IBundleEncryptor / IBundleDecryptor）",
                        Code = @"// 3.x 使用 IBundleEncryptor（构建时）和 IBundleDecryptor（运行时）
var config = new YooInitConfig
{
    EncryptionType = YooEncryptionType.Custom
};

// 注册自定义解密器工厂（运行时）
YooInitConfig.CustomDecryptorFactory = cfg => new MyBundleDecryptor();

// 注册自定义加密器工厂（构建时，仅 Editor 需要）
YooInitConfig.CustomEncryptorFactory = cfg => new MyBundleEncryptor();

await YooInit.InitAsync(config);

// ---------- 自定义解密器实现示例 ----------
public class MyBundleDecryptor : IBundleDecryptor
{
    public AssetBundle LoadAssetBundle(DecryptFileInfo fileInfo, out Stream managedStream)
    {
        var data = File.ReadAllBytes(fileInfo.FileLoadPath);
        DecryptInPlace(data);
        managedStream = null;
        return AssetBundle.LoadFromMemory(data);
    }

    public AssetBundleCreateRequest LoadAssetBundleAsync(
        DecryptFileInfo fileInfo, out Stream managedStream)
    {
        var data = File.ReadAllBytes(fileInfo.FileLoadPath);
        DecryptInPlace(data);
        managedStream = null;
        return AssetBundle.LoadFromMemoryAsync(data);
    }

    public byte[] ReadFileData(DecryptFileInfo fileInfo)
    {
        var data = File.ReadAllBytes(fileInfo.FileLoadPath);
        DecryptInPlace(data);
        return data;
    }

    public string ReadFileText(DecryptFileInfo fileInfo)
        => Encoding.UTF8.GetString(ReadFileData(fileInfo));

    private void DecryptInPlace(byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
            data[i] ^= 0xFF;
    }
}",
                        Explanation = "3.x 工厂属性名为 CustomDecryptorFactory / CustomEncryptorFactory，接口名为 IBundleDecryptor / IBundleEncryptor。注意与 2.x 的命名区别。"
                    },
                    new()
                    {
                        Title = "自定义加密（YooAsset 2.3.x — IEncryptionServices / IDecryptionServices）",
                        Code = @"// 2.3.x 使用 IEncryptionServices（构建时）和 IDecryptionServices（运行时）
var config = new YooInitConfig
{
    EncryptionType = YooEncryptionType.Custom
};

// 注册自定义解密服务工厂（运行时）
YooInitConfig.CustomDecryptionFactory = cfg => new MyDecryptionService();

// 注册自定义加密服务工厂（构建时，仅 Editor 需要）
YooInitConfig.CustomEncryptionFactory = cfg => new MyEncryptionService();

await YooInit.InitAsync(config);

// ---------- 自定义加密服务实现示例 ----------
public class MyEncryptionService : IEncryptionServices
{
    public EncryptResult Encrypt(EncryptFileInfo fileInfo)
    {
        var data = File.ReadAllBytes(fileInfo.FilePath);
        for (int i = 0; i < data.Length; i++)
            data[i] ^= 0xFF;
        return new EncryptResult { Encrypted = true, EncryptedData = data };
    }
}

public class MyDecryptionService : IDecryptionServices
{
    public AssetBundle LoadAssetBundle(DecryptFileInfo fileInfo,
        out Stream managedStream)
    {
        var data = File.ReadAllBytes(fileInfo.FileLoadPath);
        DecryptInPlace(data);
        managedStream = null;
        return AssetBundle.LoadFromMemory(data);
    }

    public AssetBundleCreateRequest LoadAssetBundleAsync(
        DecryptFileInfo fileInfo, out Stream managedStream)
    {
        var data = File.ReadAllBytes(fileInfo.FileLoadPath);
        DecryptInPlace(data);
        managedStream = null;
        return AssetBundle.LoadFromMemoryAsync(data);
    }

    public byte[] ReadFileData(DecryptFileInfo fileInfo)
    {
        var data = File.ReadAllBytes(fileInfo.FileLoadPath);
        DecryptInPlace(data);
        return data;
    }

    public string ReadFileText(DecryptFileInfo fileInfo)
        => Encoding.UTF8.GetString(ReadFileData(fileInfo));

    private void DecryptInPlace(byte[] data)
    {
        for (int i = 0; i < data.Length; i++)
            data[i] ^= 0xFF;
    }
}",
                        Explanation = "2.3.x 工厂属性名为 CustomEncryptionFactory / CustomDecryptionFactory，接口名为 IEncryptionServices / IDecryptionServices。与 3.x 的命名不同但功能对等。"
                    }
                }
            };
        }
    }
}
#endif
