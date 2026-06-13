#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FsmKit 消息系统文档
    /// </summary>
    internal static class FsmKitDocMessage
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "状态消息系统",
                Description = "状态机支持向当前状态发送消息，实现状态间通信。状态通过重写 OnMessage<TMsg> 处理消息。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "发送消息",
                        Code = @"// 定义消息类型
public struct DamageMessage
{
    public int Damage;
    public GameObject Source;
}

// 向当前状态发送消息
mFsm.SendMessage(new DamageMessage
{
    Damage = 50,
    Source = attacker
});

// 发送简单消息
mFsm.SendMessage(""OnHit"");
mFsm.SendMessage(100); // int 消息",
                        Explanation = "SendMessage 将消息转发给当前活动状态处理。"
                    },
                    new()
                    {
                        Title = "状态接收消息",
                        Code = @"public class CombatState : AbstractState<PlayerState, PlayerController>
{
    public CombatState(FSM<PlayerState> fsm, PlayerController black)
        : base(fsm, black) { }

    // 重写 OnMessage 处理消息
    protected override void OnMessage<TMsg>(TMsg message)
    {
        switch (message)
        {
            case DamageMessage damage:
                HandleDamage(damage);
                break;
            case string cmd when cmd == ""OnHit"":
                PlayHitAnimation();
                break;
        }
    }

    private void HandleDamage(DamageMessage msg)
    {
        mBlack.Health -= msg.Damage;
        if (mBlack.Health <= 0)
        {
            mFSM.Change(PlayerState.Dead);
        }
    }
}",
                        Explanation = "状态通过重写 OnMessage<TMsg> 方法接收并处理消息。注意：基类中 SendMessage 为非虚方法，应重写 OnMessage。"
                    },
                    new()
                    {
                        Title = "消息系统应用",
                        Code = @"// AI 状态机示例
public class EnemyAI
{
    private FSM<AIState> mFsm;

    public void OnPlayerDetected(Player player)
    {
        mFsm.SendMessage(new PlayerDetectedMsg { Player = player });
    }

    public void OnDamaged(int damage)
    {
        mFsm.SendMessage(new DamageMsg { Amount = damage });
    }
}

// 巡逻状态处理消息
public class PatrolState : AbstractState<AIState, EnemyAI>
{
    public PatrolState(FSM<AIState> fsm, EnemyAI black)
        : base(fsm, black) { }

    protected override void OnMessage<TMsg>(TMsg message)
    {
        if (message is PlayerDetectedMsg)
        {
            mFSM.Change(AIState.Chase);
        }
    }
}",
                        Explanation = "消息系统适合处理外部事件触发的状态转换。使用 SendMessage 发送，OnMessage 接收。"
                    }
                }
            };
        }
    }
}
#endif
