#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// FsmKit 层级状态机文档
    /// </summary>
    internal static class FsmKitDocHierarchical
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "层级状态机",
                Description = "HierarchicalSM<TEnum> 支持状态嵌套。所有已注册的状态并行运行，通过 Change(id, state) 控制各状态的生命周期。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "创建层级状态机",
                        Code = @"public enum CharacterState
{
    Grounded,
    Airborne,
    Idle,
    Walk,
    Run,
    Jump,
    Fall,
    Combat
}

var hsm = new HierarchicalSM<CharacterState>();

// 添加状态，所有状态并行运行
hsm.Add(CharacterState.Grounded, new GroundedState(hsm, this));
hsm.Add(CharacterState.Airborne, new AirborneState(hsm, this));
hsm.Add(CharacterState.Idle, new IdleState(hsm, this));
hsm.Add(CharacterState.Walk, new WalkState(hsm, this));
hsm.Add(CharacterState.Jump, new JumpState(hsm, this));

// 启动层级状态机
hsm.Start();

// 控制各状态运行/挂起/停止
hsm.Change(CharacterState.Idle, MachineState.Running);
hsm.Change(CharacterState.Walk, MachineState.Suspend);",
                        Explanation = "层级状态机中所有状态并行运行。Change(id, MachineState) 控制每个状态的生命周期状态（Running/Suspend/End）。"
                    },
                    new()
                    {
                        Title = "状态实现",
                        Code = @"public class GroundedState : AbstractState<CharacterState, PlayerController>
{
    public GroundedState(IFSM<CharacterState> fsm, PlayerController black)
        : base(fsm, black) { }

    protected override void OnUpdate()
    {
        if (!IsGrounded())
        {
            // 挂起地面状态，启动空中状态
            mFSM.Change(CharacterState.Grounded, MachineState.Suspend);
            mFSM.Change(CharacterState.Airborne, MachineState.Running);
        }
    }
}",
                        Explanation = "层级状态机中通过 Change(key, MachineState) 控制各并行状态的生命周期。"
                    },
                    new()
                    {
                        Title = "嵌套子状态机",
                        Code = @"// 创建独立的战斗子状态机
var combatFsm = new FSM<CombatState>();
combatFsm.Add(CombatState.Attacking, new AttackingState(combatFsm, this));
combatFsm.Add(CombatState.Blocking, new BlockingState(combatFsm, this));
combatFsm.Add(CombatState.Dodging, new DodgingState(combatFsm, this));

// 将子状态机作为状态添加到层级状态机
hsm.Add(CharacterState.Combat, combatFsm);

// 启动战斗状态
hsm.Change(CharacterState.Idle, MachineState.Suspend);
hsm.Change(CharacterState.Combat, MachineState.Running);",
                        Explanation = "层级状态机支持嵌套 IFSM，通过 MachineState 管理各子状态机的生命周期。"
                    }
                }
            };
        }
    }
}
#endif
