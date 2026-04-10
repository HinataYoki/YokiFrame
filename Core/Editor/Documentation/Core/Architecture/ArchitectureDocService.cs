#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 模块中的“实现服务”章节。
    /// </summary>
    internal static class ArchitectureDocService
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "实现服务",
                Description = "继承 AbstractService 实现具体业务服务。服务可通过 GetService<T>() 获取其他服务或模型。",
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
        // 在 OnInit 中获取依赖
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
        var inventoryService = GetService<InventoryService>();
        inventoryService.AddLevelUpReward(mPlayerModel.Level);

        AudioKit.Play(""sfx/levelup"");
    }

    private int GetExpToNextLevel() => mPlayerModel.Level * 100;
}",
                        Explanation = "服务之间通过 GetService 获取依赖，可以降低直接耦合。"
                    }
                }
            };
        }
    }
}
#endif
