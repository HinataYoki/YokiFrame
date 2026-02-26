#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// SingletonKit 普通单例文档
    /// </summary>
    internal static class SingletonKitDocNormal
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "普通单例（推荐）",
                Description = "不依赖 Unity 的纯 C# 单例实现，由 SingletonKit<T> 统一管理。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "实现单例",
                        Code = @"public class GameManager : Singleton<GameManager>
{
    public int Score { get; private set; }
    public GameState State { get; private set; }
    
    // 单例初始化回调
    public override void OnSingletonInit()
    {
        Score = 0;
        State = GameState.Menu;
    }
    
    public void AddScore(int value)
    {
        Score += value;
    }
    
    public void ChangeState(GameState newState)
    {
        State = newState;
    }
}

// 使用
GameManager.Instance.AddScore(100);
var state = GameManager.Instance.State;

// 释放单例（可选）
GameManager.Dispose();",
                        Explanation = "推荐使用普通单例，避免依赖 MonoBehaviour 生命周期。"
                    }
                }
            };
        }
    }
}
#endif
