#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    // Architecture 和核心模块文档
    public partial class DocumentationToolPage
    {
        private DocModule CreateArchitectureDoc()
        {
            return new DocModule
            {
                Name = "Architecture",
                Icon = "🏗️",
                Category = "CORE",
                Description = "YokiFrame 的核心架构系统，提供服务注册、依赖注入和模块化管理。",
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "概述",
                        Description = "Architecture 是整个框架的基础，负责管理所有服务（Service）和数据模型（Model）的生命周期。服务通过依赖注入实现松耦合调用。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "核心接口",
                                Code = @"// IArchitecture - 架构接口
public interface IArchitecture : ICanInit
{
    static IArchitecture Interface { get; }
    void Register<T>(T service) where T : class, IService, new();
    T GetService<T>(bool force = false) where T : class, IService, new();
    IEnumerable<IService> GetAllServices();
}

// IService - 服务接口
public interface IService : ICanInit
{
    IArchitecture Architecture { get; }
    void SetArchitecture(IArchitecture architecture);
}

// IModel - 数据模型接口（支持序列化）
public interface IModel : IService, ISerializable { }

// ICanInit - 初始化生命周期接口
public interface ICanInit : IDisposable
{
    bool Initialized { get; }
    void Init();
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "创建架构",
                        Description = "继承 Architecture<T> 创建项目专属的架构类，在 OnInit 中注册所有服务。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "定义项目架构",
                                Code = @"public class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit()
    {
        // 注册服务（顺序无关，初始化在注册完成后统一执行）
        Register(new PlayerService());
        Register(new InventoryService());
        Register(new BattleService());
        
        // 注册数据模型
        Register(new PlayerModel());
        Register(new SettingsModel());
    }
}",
                                Explanation = "服务在 OnInit 中注册后会统一初始化，确保服务间互相引用时不会拿到空值。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "实现服务",
                        Description = "继承 AbstractService 实现具体的业务服务。服务可通过 GetService<T>() 获取其他服务。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "服务实现示例",
                                Code = @"public class PlayerService : AbstractService
{
    private PlayerModel mPlayerModel;
    
    protected override void OnInit()
    {
        // 在 OnInit 中获取依赖的服务
        mPlayerModel = GetService<PlayerModel>();
    }
    
    public void AddExp(int exp)
    {
        mPlayerModel.Exp += exp;
        if (mPlayerModel.Exp >= GetExpToNextLevel())
        {
            LevelUp();
        }
    }
    
    private void LevelUp()
    {
        mPlayerModel.Level++;
        // 通过 GetService 获取其他服务
        var inventoryService = GetService<InventoryService>();
        inventoryService.AddLevelUpReward(mPlayerModel.Level);
        
        // 使用静态工具类
        AudioKit.Play(""sfx/levelup"");
    }
    
    private int GetExpToNextLevel() => mPlayerModel.Level * 100;
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "实现数据模型",
                        Description = "继承 AbstractModel 实现数据模型，用于存储游戏状态数据。IModel 继承 ISerializable，支持与 SaveKit 集成。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "数据模型示例",
                                Code = @"public class PlayerModel : AbstractModel
{
    public int Level = 1;
    public int Exp = 0;
    public int Gold = 0;
    public List<int> UnlockedSkills = new();
    
    protected override void OnInit()
    {
        // 可以在这里加载初始数据
    }
    
    // 实现 ISerializable（SaveKit 集成需要）
    public override void GetObjectData(SerializationInfo info, StreamingContext context)
    {
        info.AddValue(""Level"", Level);
        info.AddValue(""Exp"", Exp);
        info.AddValue(""Gold"", Gold);
    }
}",
                                Explanation = "数据模型与业务逻辑分离，便于存档和测试。"
                            }
                        }
                    },
                    new()
                    {
                        Title = "使用架构",
                        Description = "通过 Architecture.Interface 访问架构实例，获取服务进行业务操作。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "获取服务",
                                Code = @"// 获取服务实例
var playerService = GameArchitecture.Interface.GetService<PlayerService>();
playerService.AddExp(100);

// 未注册的服务返回 null
var service = GameArchitecture.Interface.GetService<SomeService>();
if (service == null)
{
    Debug.LogWarning(""服务未注册"");
}

// force 参数：未注册时自动创建并注册
var autoService = GameArchitecture.Interface.GetService<SomeService>(force: true);

// 获取所有服务（用于调试或批量操作）
foreach (var svc in GameArchitecture.Interface.GetAllServices())
{
    Debug.Log(svc.GetType().Name);
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "与 SaveKit 集成",
                        Description = "Architecture 中的 IModel 可以通过 SaveKit 自动收集和恢复数据。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "存档集成示例",
                                Code = @"// 保存所有 Model 数据
var saveData = SaveKit.CreateSaveData();
SaveKit.CollectFromArchitecture<GameArchitecture>(saveData);
SaveKit.Save(0, saveData);

// 加载并恢复 Model 数据
var loadedData = SaveKit.Load(0);
if (loadedData != null)
{
    SaveKit.ApplyToArchitecture<GameArchitecture>(loadedData);
}",
                                Explanation = "SaveKit 会自动遍历 Architecture 中所有实现 IModel 的服务进行序列化/反序列化。"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
