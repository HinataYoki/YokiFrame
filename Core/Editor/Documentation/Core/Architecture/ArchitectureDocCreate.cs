#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 模块中的“创建架构”章节。
    /// </summary>
    internal static class ArchitectureDocCreate
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "创建架构",
                Description = "继承 Architecture<T> 创建项目专属架构类，并在 OnInit 中注册需要的服务与模型。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "定义项目架构",
                        Code = @"public class GameArchitecture : Architecture<GameArchitecture>
{
    protected override void OnInit()
    {
        // 注册服务，初始化会在全部注册完成后统一执行
        Register(new PlayerService());
        Register(new InventoryService());
        Register(new BattleService());

        // 注册数据模型
        Register(new PlayerModel());
        Register(new SettingsModel());
    }
}",
                        Explanation = "服务与模型在 OnInit 中注册后会统一初始化，避免服务之间相互获取时出现空引用。"
                    }
                }
            };
        }
    }
}
#endif
