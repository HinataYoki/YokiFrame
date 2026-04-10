#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SceneKit 基本加载文档
    /// </summary>
    internal static class SceneKitDocBasic
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "基本加载",
                Description = "SceneKit 提供简洁的场景加载 API。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "同步加载",
                        Code = @"// 同步加载场景（仅用于编辑器或特殊场景）
SceneKit.LoadScene(""GameScene"");

// 指定加载模式
SceneKit.LoadScene(""UIScene"", SceneLoadMode.Additive);

// 通过 BuildIndex 加载
SceneKit.LoadScene(1, SceneLoadMode.Single);",
                        Explanation = "同步加载会阻塞主线程，建议仅在编辑器或启动场景使用。"
                    },
                    new()
                    {
                        Title = "异步加载",
                        Code = @"// 异步加载场景
SceneKit.LoadSceneAsync(""GameScene"");

// 带回调的异步加载
SceneKit.LoadSceneAsync(""GameScene"", SceneLoadMode.Single,
    onComplete: handler => Debug.Log($""场景加载完成: {handler.SceneName}""),
    onProgress: progress => Debug.Log($""加载进度: {progress:P0}""));

// 叠加模式加载
SceneKit.LoadSceneAsync(""UIScene"", SceneLoadMode.Additive);

// 通过 BuildIndex 异步加载
SceneKit.LoadSceneAsync(1, SceneLoadMode.Single);"
                    },
                    new()
                    {
                        Title = "带场景数据加载",
                        Code = @"// 定义场景数据
public class BattleSceneData : ISceneData
{
    public int LevelId { get; set; }
    public int Difficulty { get; set; }
}

// 加载时传递数据
var data = new BattleSceneData { LevelId = 1001, Difficulty = 2 };
SceneKit.LoadSceneAsync(""BattleScene"", data: data);

// 在新场景中获取数据
var battleData = SceneKit.GetSceneData<BattleSceneData>();
Debug.Log($""关卡: {battleData.LevelId}, 难度: {battleData.Difficulty}"");"
                    }
                }
            };
        }
    }
}
#endif
