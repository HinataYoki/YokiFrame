#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Architecture 创建架构文档
    /// </summary>
    internal static class ArchitectureDocCreate
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
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
            };
        }
    }
}
#endif
