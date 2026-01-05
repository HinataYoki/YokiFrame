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
                Description = "YokiFrame 的核心架构系统，提供服务注册、依赖注入和模块化管理。基于 IoC 容器设计，实现业务逻辑与 Unity 引擎解耦。",
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "概述",
                        Description = "Architecture 是整个框架的基础，负责管理所有服务（Service）和数据模型（Model）的生命周期。通过依赖注入实现模块间的松耦合。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "核心接口",
                                Code = @"// IArchitecture - 架构接口
public interface IArchitecture : ICanDispose
{
    void Register<T>(T service) where T : class, IService, new();
    T GetService<T>(bool force = false) where T : class, IService, new();
}

// IService - 服务接口
public interface IService : ICanDispose
{
    IArchitecture Architecture { get; }
    T GetService<T>() where T : class, IService, new();
}

// IModel - 数据模型接口（支持序列化）
public interface IModel : IService, ISerializable { }"
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
                        Description = "继承 AbstractService 实现具体的业务服务，通过 GetService<T>() 获取其他服务。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "服务实现示例",
                                Code = @"public class PlayerService : AbstractService
{
    private PlayerModel mPlayerModel;
    private InventoryService mInventory;
    
    protected override void OnInit()
    {
        // 在 OnInit 中获取依赖的服务
        mPlayerModel = GetService<PlayerModel>();
        mInventory = GetService<InventoryService>();
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
        mInventory.AddReward(mPlayerModel.Level);
    }
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "实现数据模型",
                        Description = "继承 AbstractModel 实现数据模型，支持序列化以便存档。",
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

// 强制获取（如果未注册则自动创建并注册）
var battleService = GameArchitecture.Interface.GetService<BattleService>(force: true);

// 获取所有指定类型的服务
var models = new List<IModel>();
GameArchitecture.Interface.GetServicesByType(ref models);"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
