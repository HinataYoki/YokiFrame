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
                Description = "YokiFrame 的核心架构系统，提供服务注册和模块化管理。基于 IAccessor 扩展方法模式实现服务间解耦调用。",
                Sections = new List<DocSection>
                {
                    new()
                    {
                        Title = "概述",
                        Description = "Architecture 是整个框架的基础，负责管理所有服务（Service）和数据模型（Model）的生命周期。服务间通过 IAccessor 扩展方法实现松耦合调用。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "核心接口",
                                Code = @"// IAccessor - 服务访问器，通过扩展方法提供跨服务调用
public interface IAccessor
{
    IArchitecture Architecture { get; }
}

// IArchitecture - 架构接口
public interface IArchitecture
{
    bool Initialized { get; }
    void Register<T>(T service) where T : class, IService, new();
    T GetService<T>() where T : class, IService, new();
}

// IService - 服务接口
public interface IService
{
    bool Initialized { get; }
    IArchitecture Architecture { get; }
    void SetArchitecture(IArchitecture architecture);
    void Init();
}

// IModel - 数据模型标记接口
public interface IModel : IService { }"
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
                        Description = "继承 AbstractService 实现具体的业务服务。服务自动实现 IAccessor 接口，可通过扩展方法调用其他服务的功能。",
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
        // 在 OnInit 中获取依赖的服务（仅用于初始化阶段）
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
        // 通过扩展方法调用其他服务（运行时推荐方式）
        this.AddLevelUpReward(mPlayerModel.Level);
        this.PlayAudio(""sfx/levelup"");
    }
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "IAccessor 扩展方法",
                        Description = "服务通过扩展方法暴露功能，其他服务通过 this 调用，实现完全解耦。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "定义扩展方法",
                                Code = @"// InventoryAccessorExtensions.cs
public static class InventoryAccessorExtensions
{
    public static void AddLevelUpReward(this IAccessor self, int level)
    {
        // 内部实现可以访问具体服务或静态工具类
        var inventory = self.Architecture.GetService<InventoryService>();
        inventory.AddItem(1001, level * 10); // 金币奖励
    }
    
    public static int GetItemCount(this IAccessor self, int itemId)
    {
        var inventory = self.Architecture.GetService<InventoryService>();
        return inventory.GetCount(itemId);
    }
}

// AudioAccessorExtensions.cs
public static class AudioAccessorExtensions
{
    public static void PlayAudio(this IAccessor self, string path)
    {
        AudioKit.Play(path);
    }
}",
                                Explanation = "扩展方法让服务间调用变得简洁，且调用方完全不知道具体实现者是谁。"
                            },
                            new()
                            {
                                Title = "在服务中使用",
                                Code = @"public class BattleService : AbstractService
{
    protected override void OnInit() { }
    
    public void OnEnemyKilled(int enemyId)
    {
        // 通过 this 调用扩展方法，IDE 自动补全
        this.AddExp(100);
        this.PlayAudio(""sfx/kill"");
        
        int gold = this.GetItemCount(1001);
        Debug.Log($""当前金币: {gold}"");
    }
}"
                            }
                        }
                    },
                    new()
                    {
                        Title = "实现数据模型",
                        Description = "继承 AbstractModel 实现数据模型，用于存储游戏状态数据。",
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
}"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
