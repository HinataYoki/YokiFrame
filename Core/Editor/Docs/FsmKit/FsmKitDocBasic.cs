#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FsmKit 基础状态机文档
    /// </summary>
    internal static class FsmKitDocBasic
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "基础状态机",
                Description = "使用枚举定义状态，创建简单高效的状态机。FSM<TState> 是最常用的状态机类型。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "定义状态枚举",
                        Code = @"public enum PlayerState
{
    Idle,
    Walk,
    Run,
    Jump,
    Attack,
    Dead
}",
                        Explanation = "使用枚举定义状态，避免魔法字符串。"
                    },
                    new()
                    {
                        Title = "创建和使用状态机",
                        Code = @"public class PlayerController
{
    private FSM<PlayerState> mFsm;
    
    public void Init()
    {
        // 创建状态机
        mFsm = new FSM<PlayerState>();
        
        // 添加状态
        mFsm.AddState(PlayerState.Idle, new IdleState());
        mFsm.AddState(PlayerState.Walk, new WalkState());
        mFsm.AddState(PlayerState.Run, new RunState());
        mFsm.AddState(PlayerState.Jump, new JumpState());
        mFsm.AddState(PlayerState.Attack, new AttackState());
        
        // 启动状态机（进入初始状态）
        mFsm.Start(PlayerState.Idle);
    }
    
    public void Update()
    {
        // 驱动状态机更新
        mFsm.Update();
    }
    
    public PlayerState CurrentState => mFsm.CurrentStateId;
}",
                        Explanation = "状态机需要在 Update 中手动驱动更新。"
                    }
                }
            };
        }
    }
}
#endif
