#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 实现服务文档
    /// </summary>
    internal static class ArchitectureDocService
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
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
}",
                        Explanation = "服务之间通过 GetService 获取依赖，实现松耦合。"
                    }
                }
            };
        }
    }
}
#endif
