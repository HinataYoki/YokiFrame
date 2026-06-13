#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FsmKit 基础用法文档。
    /// </summary>
    internal static class FsmKitDocBasic
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "基础状态机",
                Description = "用枚举定义状态，通过 FSM<TEnum> 构建简洁高效的有限状态机。",
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
                        Explanation = "用枚举定义状态标识，避免魔法字符串。"
                    },
                    new()
                    {
                        Title = "创建并使用状态机",
                        Code = @"public class PlayerController
{
    private FSM<PlayerState> mFsm;

    public void Init()
    {
        mFsm = new FSM<PlayerState>();
        mFsm.Add(PlayerState.Idle, new IdleState(mFsm, this));
        mFsm.Add(PlayerState.Walk, new WalkState(mFsm, this));
        mFsm.Add(PlayerState.Run, new RunState(mFsm, this));
        mFsm.Add(PlayerState.Jump, new JumpState(mFsm, this));
        mFsm.Add(PlayerState.Attack, new AttackState(mFsm, this));
        mFsm.Start(PlayerState.Idle);
    }

    private void Update()
    {
        mFsm.Update();
    }

    public PlayerState CurrentState => mFsm.CurEnum;
}",
                        Explanation = "通过 Add() 注册状态后调用 Start() 启动。每帧调用 Update() 驱动当前状态的逻辑。CurEnum 获取当前状态枚举。"
                    },
                    new()
                    {
                        Title = "运行时状态切换",
                        Code = @"// 从外部触发状态切换
public void OnJumpPressed()
{
    mFsm.Change(PlayerState.Jump);
}

public void OnAttackPressed()
{
    mFsm.Change(PlayerState.Attack);
}

// 条件切换
public void OnDamaged(int damage)
{
    mCurrentHp -= damage;
    if (mCurrentHp <= 0)
    {
        mFsm.Change(PlayerState.Dead);
    }
}",
                        Explanation = "Change() 方法切换当前状态，自动调用旧状态的 OnExit 和新状态的 OnEnter。"
                    }
                }
            };
        }
    }
}
#endif
