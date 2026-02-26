#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit 最佳实践文档
    /// </summary>
    internal static class SceneKitDocBestPractice
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "最佳实践",
                Description = "场景管理的推荐用法。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "场景管理模式",
                        Code = @"// 推荐的场景组织方式
// 1. 启动场景（Bootstrap）- 初始化框架和资源
// 2. 主场景（Main）- 主菜单、大厅等
// 3. 游戏场景（Game）- 实际游戏内容
// 4. UI 场景（UI）- 叠加的 UI 层

// 启动流程示例
public class GameBootstrap : MonoBehaviour
{
    async void Start()
    {
        // 1. 初始化框架
        await InitializeFramework();

        // 2. 预加载常用场景
        SceneKit.PreloadSceneAsync(""MainMenu"");

        // 3. 切换到主菜单
        await SceneKit.SwitchSceneUniTaskAsync(""MainMenu"",
            new FadeTransition(0.5f));
    }
}

// 游戏场景切换示例
public class SceneController
{
    public async UniTask EnterBattle(int levelId)
    {
        // 传递场景数据
        var data = new BattleSceneData { LevelId = levelId };

        // 带过渡效果切换
        await SceneKit.SwitchSceneUniTaskAsync(""Battle"",
            new FadeTransition(),
            data);

        // 叠加 UI 场景
        await SceneKit.LoadSceneUniTaskAsync(""BattleUI"",
            SceneLoadMode.Additive);
    }

    public async UniTask ExitBattle()
    {
        // 卸载 UI 场景
        await SceneKit.UnloadSceneUniTaskAsync(""BattleUI"");

        // 返回主菜单
        await SceneKit.SwitchSceneUniTaskAsync(""MainMenu"",
            new FadeTransition());
    }
}",
                        Explanation = "使用 Single 模式切换主场景，Additive 模式叠加 UI 场景。"
                    }
                }
            };
        }
    }
}
#endif
