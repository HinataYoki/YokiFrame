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
                Description = "继承 AbstractState<TEnum, TBlack> 实现具体状态逻辑，通过 mFSM 字段访问状态机进行状态切换。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "实现状态类",
                        Code = @"public class IdleState : AbstractState<PlayerState, PlayerController>
{
    public IdleState(FSM<PlayerState> fsm, PlayerController black)
        : base(fsm, black) { }

    protected override void OnEnter()
    {
        // 进入状态时调用
    }

    protected override void OnUpdate()
    {
        // 每帧调用，检测输入切换状态
        if (Input.GetKey(KeyCode.W))
        {
            mFSM.Change(PlayerState.Walk);
            return;
        }
        if (Input.GetKeyDown(KeyCode.Space))
        {
            mFSM.Change(PlayerState.Jump);
            return;
        }
        if (Input.GetMouseButtonDown(0))
        {
            mFSM.Change(PlayerState.Attack);
        }
    }

    protected override void OnExit()
    {
        // 退出状态时调用
    }
}",
                        Explanation = "mFSM 字段由基类在构造时注入，通过 mFSM.Change() 进行状态切换。"
                    },
                    new()
                    {
                        Title = "带条件的状态切换",
                        Code = @"public class JumpState : AbstractState<PlayerState, PlayerController>
{
    private float mJumpTimer;
    private const float JUMP_DURATION = 0.5f;

    public JumpState(FSM<PlayerState> fsm, PlayerController black)
        : base(fsm, black) { }

    protected override void OnEnter()
    {
        mJumpTimer = 0f;
    }

    protected override void OnUpdate()
    {
        mJumpTimer += Time.deltaTime;

        // 跳跃结束后自动切换
        if (mJumpTimer >= JUMP_DURATION)
        {
            mFSM.Change(PlayerState.Idle);
        }
    }

    protected override void OnExit()
    {
        mJumpTimer = 0f;
    }
}",
                        Explanation = "状态内部可以根据条件通过 mFSM.Change() 自动切换到其他状态。"
                    }
                }
            };
        }
    }
}
#endif
