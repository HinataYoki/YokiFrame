#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// EventKit 枚举事件文档
    /// </summary>
    internal static class EventKitDocEnum
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "枚举事件（推荐）",
                Description = "使用枚举作为事件键，获得最佳性能和类型安全。避免魔法字符串，编译期检查。内部使用 UnsafeUtility 避免枚举装箱。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "定义事件枚举",
                        Code = @"public enum GameEvent
{
    PlayerDied,
    ScoreChanged,
    LevelCompleted,
    ItemCollected,
    EnemySpawned
}",
                        Explanation = "使用枚举定义事件类型，避免魔法字符串。"
                    },
                    new()
                    {
                        Title = "注册和触发无参事件",
                        Code = @"// 注册无参事件
EventKit.Enum.Register(GameEvent.PlayerDied, OnPlayerDied);

// 触发事件
EventKit.Enum.Send(GameEvent.PlayerDied);

// 注销事件（重要！防止内存泄漏）
EventKit.Enum.UnRegister(GameEvent.PlayerDied, OnPlayerDied);

private void OnPlayerDied()
{
    Debug.Log(""玩家死亡"");
}",
                        Explanation = "务必在对象销毁时注销事件，避免内存泄漏。"
                    },
                    new()
                    {
                        Title = "带参数的事件",
                        Code = @"// 注册带参数事件
EventKit.Enum.Register<GameEvent, int>(GameEvent.ScoreChanged, OnScoreChanged);

// 触发带参数事件
EventKit.Enum.Send(GameEvent.ScoreChanged, 100);

// 多参数事件（使用 params object[]）
EventKit.Enum.Register(GameEvent.ItemCollected, OnItemCollected);
EventKit.Enum.Send(GameEvent.ItemCollected, itemId, count, ""Gold"");

private void OnScoreChanged(int newScore)
{
    scoreText.text = newScore.ToString();
}

private void OnItemCollected(object[] args)
{
    int itemId = (int)args[0];
    int count = (int)args[1];
}",
                        Explanation = "带参数事件支持泛型参数和 params object[] 两种方式。"
                    },
                    new()
                    {
                        Title = "使用 LinkUnRegister 自动注销",
                        Code = @"// Register 返回 LinkUnRegister，可用于链式注销
private LinkUnRegister mUnRegister;

void OnEnable()
{
    mUnRegister = EventKit.Enum.Register(GameEvent.PlayerDied, OnPlayerDied);
}

void OnDisable()
{
    // 方式1：直接调用 UnRegister
    mUnRegister.UnRegister();
    
    // 方式2：使用 UnRegisterWhenGameObjectDestroyed 扩展
    // EventKit.Enum.Register(...).UnRegisterWhenGameObjectDestroyed(gameObject);
}",
                        Explanation = "LinkUnRegister 封装了注销逻辑，避免手动管理回调引用。"
                    }
                }
            };
        }
    }
}
#endif
