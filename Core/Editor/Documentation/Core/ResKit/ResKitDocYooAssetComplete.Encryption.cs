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
                Description = "Inspector 会根据加密类型动态显示对应配置项。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "内置加密类型",
                        Code = @"// ---------- XOR 流式加密（推荐）----------
// 性能好，安全性适中
// 密钥种子通过 SHA256 生成 32 字节密钥
var xorConfig = new YooInitConfig
{
    EncryptionType = YooEncryptionType.XorStream,
    XorKeySeed = ""MySecretSeed_2025!@#$""
};

// ---------- 文件偏移 ----------
// 仅防止直接打开，无实际加密
// 适合简单防护场景
var offsetConfig = new YooInitConfig
{
    EncryptionType = YooEncryptionType.FileOffset,
    FileOffset = 64  // 可选：16/32/64/128/256/512/1024（2 的幂）
};

// ---------- AES 加密 ----------
// 安全性高，但性能开销大
// 密码和盐值通过 PBKDF2 派生密钥
var aesConfig = new YooInitConfig
{
    EncryptionType = YooEncryptionType.Aes,
    AesPassword = ""MySecretPassword"",
    AesSalt = ""MySalt88""  // 至少 8 字节
};

// ---------- 获取加密/解密服务 ----------
IEncryptionServices encryptor = config.CreateEncryptionServices();
IDecryptionServices decryptor = config.CreateDecryptionServices();",
                        Explanation = "Inspector 使用 UIToolkit 绘制，FileOffset 使用下拉选择确保值为 2 的幂。"
                    },
                    new()
                    {
                        Title = "自定义加密",
                        Code = @"// ============================================================
// 自定义加密方案
// 通过静态委托注册自定义加密/解密服务
// ============================================================

// ---------- 1. 配置使用自定义加密类型 ----------
var config = new YooInitConfig
{
    EncryptionType = YooEncryptionType.Custom
};

// ---------- 2. 注册自定义加密服务工厂（构建时使用）----------
YooInitConfig.CustomEncryptionFactory = cfg => new MyCustomEncryption();

// ---------- 3. 注册自定义解密服务工厂（运行时使用）----------
YooInitConfig.CustomDecryptionFactory = cfg => new MyCustomDecryption();

// ---------- 4. 初始化 YooAsset ----------
await YooInit.InitAsync(config);",
                        Explanation = "自定义加密需要在调用 InitAsync 或 CreateEncryptionServices/CreateDecryptionServices 前注册工厂委托。"
                    },
                    new()
                    {
                        Title = "自定义加密类实现",
                        Code = @"using System.IO;
using YooAsset;

/// <summary>
/// 自定义加密服务（构建时使用）
/// </summary>
public class MyCustomEncryption : IEncryptionServices
{
    /// <summary>
    /// 加密文件
    /// </summary>
    public EncryptResult Encrypt(EncryptFileInfo fileInfo)
    {
        // 读取原始文件数据
        var fileData = File.ReadAllBytes(fileInfo.FilePath);
        
        // 执行自定义加密算法
        var encryptedData = MyEncryptAlgorithm(fileData);
        
        // 返回加密结果
        return new EncryptResult
        {
            Encrypted = true,
            EncryptedData = encryptedData
        };
    }
    
    private byte[] MyEncryptAlgorithm(byte[] data)
    {
        // 实现自定义加密逻辑
        // 示例：简单的字节翻转
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ 0xFF);
        }
        return result;
    }
}

/// <summary>
/// 自定义解密服务（运行时使用）
/// </summary>
public class MyCustomDecryption : IDecryptionServices
{
    /// <summary>
    /// 获取 Bundle 文件的加载方法
    /// </summary>
    public AssetBundle LoadAssetBundle(DecryptFileInfo fileInfo, out Stream managedStream)
    {
        // 读取加密文件
        var encryptedData = File.ReadAllBytes(fileInfo.FileLoadPath);
        
        // 执行解密
        var decryptedData = MyDecryptAlgorithm(encryptedData);
        
        // 从内存加载 AssetBundle
        managedStream = null;
        return AssetBundle.LoadFromMemory(decryptedData);
    }
    
    /// <summary>
    /// 异步获取 Bundle 文件的加载方法
    /// </summary>
    public AssetBundleCreateRequest LoadAssetBundleAsync(DecryptFileInfo fileInfo, out Stream managedStream)
    {
        var encryptedData = File.ReadAllBytes(fileInfo.FileLoadPath);
        var decryptedData = MyDecryptAlgorithm(encryptedData);
        
        managedStream = null;
        return AssetBundle.LoadFromMemoryAsync(decryptedData);
    }
    
    /// <summary>
    /// 获取原始文件的字节数据
    /// </summary>
    public byte[] ReadFileData(DecryptFileInfo fileInfo)
    {
        var encryptedData = File.ReadAllBytes(fileInfo.FileLoadPath);
        return MyDecryptAlgorithm(encryptedData);
    }
    
    /// <summary>
    /// 获取原始文件的文本数据
    /// </summary>
    public string ReadFileText(DecryptFileInfo fileInfo)
    {
        var data = ReadFileData(fileInfo);
        return System.Text.Encoding.UTF8.GetString(data);
    }
    
    private byte[] MyDecryptAlgorithm(byte[] data)
    {
        // 实现自定义解密逻辑（与加密算法对应）
        var result = new byte[data.Length];
        for (int i = 0; i < data.Length; i++)
        {
            result[i] = (byte)(data[i] ^ 0xFF);
        }
        return result;
    }
}",
                        Explanation = "IEncryptionServices 用于构建时加密资源，IDecryptionServices 用于运行时解密资源。两者的算法必须对应。"
                    },
                    new()
                    {
                        Title = "自定义加密完整流程",
                        Code = @"// ============================================================
// 自定义加密完整流程示例
// ============================================================

public class GameBoot : MonoBehaviour
{
    [SerializeField] private YooInitConfig mConfig;
    
    private async void Start()
    {
        // 1. 注册自定义解密服务（运行时只需要解密）
        YooInitConfig.CustomDecryptionFactory = cfg => new MyCustomDecryption();
        
        // 2. 初始化 YooAsset
        await YooInit.InitAsync(mConfig);
        
        // 3. 正常使用资源加载
        YooInitUIKitExt.ConfigureUIKit();
        YooInitSceneKitExt.ConfigureSceneKit();
    }
}

// ============================================================
// 构建管线中使用自定义加密
// ============================================================
#if UNITY_EDITOR
public static class BuildPipelineHelper
{
    public static void BuildWithCustomEncryption()
    {
        var config = new YooInitConfig
        {
            EncryptionType = YooEncryptionType.Custom
        };
        
        // 注册自定义加密服务
        YooInitConfig.CustomEncryptionFactory = cfg => new MyCustomEncryption();
        
        // 获取加密服务用于构建
        var encryptionServices = config.CreateEncryptionServices();
        
        // 在 YooAsset 构建参数中使用
        var buildParams = new BuildParameters
        {
            // ... 其他参数
            EncryptionServices = encryptionServices
        };
    }
}
#endif",
                        Explanation = "运行时只需注册 CustomDecryptionFactory，构建时需要注册 CustomEncryptionFactory。"
                    }
                }
            };
        }
    }
}
#endif
