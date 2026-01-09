#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FsmKit 带参数状态机文档
    /// </summary>
    internal static class FsmKitDocArgs
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "带参数状态机",
                Description = "支持在状态启动和切换时传递参数。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "定义带参数状态机",
                        Code = @"// 定义状态参数类型
public class BattleArgs
{
    public Enemy Target;
    public int InitialDamage;
}

// 创建带参数的状态机
var fsm = new FSM<BattleState, BattleArgs>();

// 添加状态
fsm.Add(BattleState.Idle, new IdleState());
fsm.Add(BattleState.Attack, new AttackState());

// 带参数启动
fsm.Start(BattleState.Attack, new BattleArgs 
{ 
    Target = enemy, 
    InitialDamage = 100 
});",
                        Explanation = "FSM<TEnum, TArgs> 支持在启动和切换时传递参数。"
                    },
                    new()
                    {
                        Title = "状态接收参数",
                        Code = @"// 实现 IState<TArgs> 接口
public class AttackState : IState<BattleArgs>
{
    private Enemy mTarget;
    private int mDamage;
    
    // 带参数启动
    public void Start(BattleArgs args)
    {
        mTarget = args.Target;
        mDamage = args.InitialDamage;
        BeginAttack();
    }
    
    // 无参数启动（使用默认值）
    public void Start()
    {
        mTarget = null;
        mDamage = 10;
    }
    
    public bool Condition() => true;
    public void Update() { /* 攻击逻辑 */ }
    public void End() { /* 清理 */ }
    public void Dispose() { }
}",
                        Explanation = "状态需要实现 IState<TArgs> 接口来接收参数。"
                    },
                    new()
                    {
                        Title = "带参数状态切换",
                        Code = @"// 切换状态时传递参数
mFsm.Change(BattleState.Skill, new BattleArgs 
{ 
    Target = currentTarget,
    InitialDamage = skillDamage 
});

// 无参数切换（调用 Start() 无参版本）
mFsm.Change(BattleState.Idle);

// 实际应用：技能状态机
public class SkillState : AbstractState<BattleState, Player, SkillArgs>
{
    public override void Start(SkillArgs args)
    {
        base.Start(args);
        mTarget.PlaySkill(args.SkillId, args.Target);
    }
    
    public override void Update()
    {
        if (mTarget.IsSkillFinished)
        {
            mFsm.Change(BattleState.Idle);
        }
    }
}",
                        Explanation = "Change<TArgs> 方法支持在状态切换时传递新参数。"
                    }
                }
            };
        }
    }
}
#endif
