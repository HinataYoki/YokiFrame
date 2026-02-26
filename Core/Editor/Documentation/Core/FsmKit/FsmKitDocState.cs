#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FsmKit 状态类实现文档
    /// </summary>
    internal static class FsmKitDocState
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "状态类实现",
                Description = "继承 AbstractState<TState> 实现具体状态逻辑，通过 FSM 属性访问状态机进行状态切换。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "实现状态类",
                        Code = @"public class IdleState : AbstractState<PlayerState>
{
    public override void OnEnter()
    {
        // 进入状态时调用
        Debug.Log(""进入 Idle 状态"");
        // 播放待机动画等
    }
    
    public override void OnUpdate()
    {
        // 每帧调用，检测输入切换状态
        if (Input.GetKey(KeyCode.W))
        {
            FSM.ChangeState(PlayerState.Walk);
            return;
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            FSM.ChangeState(PlayerState.Jump);
            return;
        }
        if (Input.GetMouseButtonDown(0))
        {
            FSM.ChangeState(PlayerState.Attack);
        }
    }
    
    public override void OnExit()
    {
        // 退出状态时调用
        Debug.Log(""退出 Idle 状态"");
    }
}",
                        Explanation = "FSM 属性由状态机自动注入，可以访问状态机实例进行状态切换。"
                    },
                    new()
                    {
                        Title = "带条件的状态切换",
                        Code = @"public class JumpState : AbstractState<PlayerState>
{
    private float mJumpTimer;
    private const float JUMP_DURATION = 0.5f;
    
    public override void OnEnter()
    {
        mJumpTimer = 0f;
        // 播放跳跃动画，施加跳跃力
    }
    
    public override void OnUpdate()
    {
        mJumpTimer += Time.deltaTime;
        
        // 跳跃结束后自动切换回 Idle
        if (mJumpTimer >= JUMP_DURATION)
        {
            FSM.ChangeState(PlayerState.Idle);
        }
    }
    
    public override void OnExit()
    {
        mJumpTimer = 0f;
    }
}",
                        Explanation = "状态内部可以根据条件自动切换到其他状态。"
                    }
                }
            };
        }
    }
}
#endif
