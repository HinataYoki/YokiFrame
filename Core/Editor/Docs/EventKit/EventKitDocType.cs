#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// EventKit 类型事件文档
    /// </summary>
    internal static class EventKitDocType
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "类型事件",
                Description = "使用类型作为事件键，适合需要传递复杂数据结构的场景。类型本身就是事件标识。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "定义事件数据结构",
                        Code = @"// 推荐使用结构体（小于16字节）减少 GC
public struct PlayerDiedEvent
{
    public int PlayerId;
    public Vector3 Position;
}

public struct DamageEvent
{
    public int SourceId;
    public int TargetId;
    public float Damage;
    public DamageType Type;
}

public enum DamageType { Physical, Magic, True }",
                        Explanation = "结构体避免堆分配，减少 GC 压力。"
                    },
                    new()
                    {
                        Title = "使用类型事件",
                        Code = @"// 注册
EventKit.Type.Register<PlayerDiedEvent>(OnPlayerDied);
EventKit.Type.Register<DamageEvent>(OnDamage);

// 触发
EventKit.Type.Send(new PlayerDiedEvent 
{ 
    PlayerId = 1, 
    Position = transform.position 
});

EventKit.Type.Send(new DamageEvent
{
    SourceId = attackerId,
    TargetId = targetId,
    Damage = 50f,
    Type = DamageType.Physical
});

// 注销
EventKit.Type.UnRegister<PlayerDiedEvent>(OnPlayerDied);

private void OnPlayerDied(PlayerDiedEvent evt)
{
    Debug.Log($""玩家 {evt.PlayerId} 在 {evt.Position} 死亡"");
}",
                        Explanation = "类型事件的类型本身就是事件标识，无需额外定义 key。"
                    }
                }
            };
        }
    }
}
#endif
