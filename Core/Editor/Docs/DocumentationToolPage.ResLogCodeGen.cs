#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    // ResKit、LogKit、CodeGenKit 文档
    public partial class DocumentationToolPage
    {
        private DocModule CreateResKitDoc()
        {
            return new DocModule
            {
                Name = "ResKit",
                Icon = "📦",
                Category = "CORE KIT",
                Description = "资源管理工具，提供同步/异步加载、引用计数、资源缓存等功能。支持 UniTask 异步和自定义加载器扩展。",
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "同步加载",
                        Description = "同步加载资源，适合小资源或加载界面。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "基本加载",
                                Code = @"// 加载资源
var prefab = ResKit.Load<GameObject>(""Prefabs/Player"");
var sprite = ResKit.Load<Sprite>(""Sprites/Icon"");
var clip = ResKit.Load<AudioClip>(""Audio/BGM"");

// 加载并实例化
var player = ResKit.Instantiate(""Prefabs/Player"", parent);

// 获取句柄（需要手动管理引用计数）
var handler = ResKit.LoadAsset<GameObject>(""Prefabs/Enemy"");
handler.Retain();  // 增加引用
handler.Release(); // 减少引用，引用为0时自动卸载"
                            }
                        }
                    },
                    new()
                    {
                        Title = "异步加载",
                        Description = "异步加载资源，避免阻塞主线程。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "回调方式",
                                Code = @"// 异步加载
ResKit.LoadAsync<GameObject>(""Prefabs/Boss"", prefab =>
{
    if (prefab != null)
    {
        Instantiate(prefab, spawnPoint);
    }
});

// 异步实例化
ResKit.InstantiateAsync(""Prefabs/Effect"", effect =>
{
    effect.transform.position = targetPos;
}, parent);"
                            },
                            new()
                            {
                                Title = "UniTask 方式",
                                Code = @"#if YOKIFRAME_UNITASK_SUPPORT
// 使用 UniTask 异步加载
var prefab = await ResKit.LoadUniTaskAsync<GameObject>(""Prefabs/Boss"");
var instance = Instantiate(prefab);

// 支持取消
var cts = new CancellationTokenSource();
try
{
    var sprite = await ResKit.LoadUniTaskAsync<Sprite>(""Sprites/Icon"", cts.Token);
}
catch (OperationCanceledException)
{
    Debug.Log(""加载已取消"");
}

// 异步实例化
var player = await ResKit.InstantiateUniTaskAsync(""Prefabs/Player"", parent);
#endif",
                                Explanation = "需要定义 YOKIFRAME_UNITASK_SUPPORT 宏启用 UniTask 支持。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "自定义加载器",
                        Description = "通过实现 IResLoaderPool 接口扩展加载方式，支持 YooAsset、Addressables 等。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "设置自定义加载池",
                                Code = @"// 切换到自定义加载池（如 YooAsset）
ResKit.SetLoaderPool(new YooAssetLoaderPool());

// 获取当前加载池
var pool = ResKit.GetLoaderPool();

// 清理所有缓存
ResKit.ClearAll();"
                            }
                        }
                    }
                }
            };
        }
        
        private DocModule CreateLogKitDoc()
        {
            return new DocModule
            {
                Name = "LogKit",
                Icon = "📝",
                Category = "CORE KIT",
                Description = "日志系统，支持日志级别控制、文件写入、加密存储。后台线程异步写入，不阻塞主线程。",
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "基本使用",
                        Description = "提供 Log、Warning、Error、Exception 四个级别的日志输出。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "输出日志",
                                Code = @"// 普通日志
KitLogger.Log(""游戏启动"");
KitLogger.Log($""玩家等级: {level}"");

// 警告
KitLogger.Warning(""配置文件缺失，使用默认值"");

// 错误
KitLogger.Error(""网络连接失败"");

// 异常
try
{
    // ...
}
catch (Exception ex)
{
    KitLogger.Exception(ex);
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "日志配置",
                        Description = "配置日志级别、文件写入、加密等选项。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "配置选项",
                                Code = @"// 设置日志级别
KitLogger.Level = KitLogger.LogLevel.All;     // 输出所有日志
KitLogger.Level = KitLogger.LogLevel.Warning; // 只输出 Warning 和 Error
KitLogger.Level = KitLogger.LogLevel.Error;   // 只输出 Error
KitLogger.Level = KitLogger.LogLevel.None;    // 关闭所有日志

// 启用文件写入（自动异步写入）
KitLogger.AutoEnableWriteLogToFile = true;

// 启用加密（保护敏感信息）
KitLogger.EnableEncryption = true;

// 编辑器中保存日志
KitLogger.SaveLogInEditor = true;

// 配置限制
KitLogger.MaxQueueSize = 20000;      // 最大队列大小
KitLogger.MaxSameLogCount = 50;      // 相同日志最大重复次数
KitLogger.MaxRetentionDays = 10;     // 日志保留天数
KitLogger.MaxFileBytes = 50 * 1024 * 1024; // 单文件最大 50MB"
                            }
                        }
                    },
                    new()
                    {
                        Title = "编辑器工具",
                        Description = "编辑器菜单提供日志目录打开和解密功能。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "菜单位置",
                                Code = @"// 菜单路径
// YokiFrame > KitLogger > 打开日志目录
// YokiFrame > KitLogger > 解密日志文件

// 日志文件位置
// Application.persistentDataPath/LogFiles/editor.log (编辑器)
// Application.persistentDataPath/LogFiles/player.log (运行时)"
                            }
                        }
                    }
                }
            };
        }
        
        private DocModule CreateCodeGenKitDoc()
        {
            return new DocModule
            {
                Name = "CodeGenKit",
                Icon = "⚙️",
                Category = "CORE KIT",
                Description = "代码生成工具，提供结构化的代码生成 API。支持命名空间、类、方法等代码结构的生成。UIKit 的代码生成基于此实现。",
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "核心概念",
                        Description = "CodeGenKit 使用 ICode 和 ICodeScope 接口构建代码树，最终通过 ICodeWriteKit 输出。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "核心接口",
                                Code = @"// ICode - 代码片段接口
public interface ICode
{
    void Gen(ICodeWriteKit writer);
}

// ICodeScope - 代码作用域接口（包含子代码）
public interface ICodeScope : ICode
{
    List<ICode> Codes { get; set; }
}

// ICodeWriteKit - 代码写入器接口
public interface ICodeWriteKit : IDisposable
{
    int IndentCount { get; set; }
    void WriteFormatLine(string format, params object[] args);
    void WriteLine(string code = null);
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "生成代码",
                        Description = "使用 RootCode 作为根节点，通过链式调用构建代码结构。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "生成类代码",
                                Code = @"var root = new RootCode();

// 添加 using
root.Codes.Add(new UsingCode(""System""));
root.Codes.Add(new UsingCode(""UnityEngine""));
root.Codes.Add(new EmptyLineCode());

// 添加命名空间
root.Namespace(""MyGame"", ns =>
{
    // 添加类
    ns.Class(""PlayerController"", ""MonoBehaviour"", 
        isPartial: true, isStatic: false, cls =>
    {
        // 添加字段
        cls.Codes.Add(new CustomCode(""public float Speed = 5f;""));
        cls.Codes.Add(new CustomCode(""public int Health = 100;""));
    });
});

// 输出到文件
using var writer = new FileCodeWriteKit(filePath);
root.Gen(writer);"
                            },
                            new()
                            {
                                Title = "生成的代码示例",
                                Code = @"using System;
using UnityEngine;

namespace MyGame
{
    public partial class PlayerController : MonoBehaviour
    {
        public float Speed = 5f;
        public int Health = 100;
    }
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "内置代码类型",
                        Description = "CodeGenKit 提供多种内置的代码类型。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "代码类型列表",
                                Code = @"// 基础代码
new UsingCode(""System"");           // using System;
new EmptyLineCode();                 // 空行
new OpenBraceCode();                 // {
new CloseBraceCode();                // }
new CustomCode(""// 注释"");         // 自定义代码

// 作用域代码
new NamespaceCodeScope(""MyGame"");  // namespace MyGame { }
new ClassCodeScope(""MyClass"", ""BaseClass"", isPartial, isStatic);
new CustomCodeScope(""if (condition)""); // 自定义作用域"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
