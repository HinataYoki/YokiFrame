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
                Description = "HierarchicalSM 支持状态嵌套和状态机嵌套。可以管理 IState 状态和 IFSM 子状态机，父状态的逻辑会在子状态之前执行。",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "创建层级状态机",
                        Code = @"public enum CharacterState
{
    // 父状态
    Grounded,
    Airborne,
    
    // Grounded 的子状态
    Idle,
    Walk,
    Run,
    
    // Airborne 的子状态
    Jump,
    Fall
}

var hsm = new HierarchicalSM<CharacterState>();

// 添加父状态（可以是 IState 或 IFSM）
hsm.AddState(CharacterState.Grounded, new GroundedState());
hsm.AddState(CharacterState.Airborne, new AirborneState());

// 添加子状态（指定父状态）
hsm.AddState(CharacterState.Idle, new IdleState(), CharacterState.Grounded);
hsm.AddState(CharacterState.Walk, new WalkState(), CharacterState.Grounded);
hsm.AddState(CharacterState.Run, new RunState(), CharacterState.Grounded);

hsm.AddState(CharacterState.Jump, new JumpState(), CharacterState.Airborne);
hsm.AddState(CharacterState.Fall, new FallState(), CharacterState.Airborne);

// 启动
hsm.Start(CharacterState.Idle);",
                        Explanation = "层级状态机可以管理 IState 和 IFSM，子状态会继承父状态的行为。"
                    },
                    new()
                    {
                        Title = "父状态实现",
                        Code = @"public class GroundedState : AbstractState<CharacterState>
{
    public override void OnEnter()
    {
        // 所有地面状态共享的进入逻辑
        EnableGroundedPhysics();
    }
    
    public override void OnUpdate()
    {
        // 所有地面状态共享的更新逻辑
        // 例如：检测是否离开地面
        if (!IsGrounded())
        {
            FSM.ChangeState(CharacterState.Fall);
        }
    }
    
    public override void OnExit()
    {
        // 离开地面状态组时调用
    }
}",
                        Explanation = "父状态的 OnUpdate 会在子状态之前执行。"
                    },
                    new()
                    {
                        Title = "嵌套子状态机",
                        Code = @"// 创建独立的战斗状态机
var combatFsm = new FSM<CombatState>();
combatFsm.AddState(CombatState.Attacking, new AttackingState());
combatFsm.AddState(CombatState.Blocking, new BlockingState());
combatFsm.AddState(CombatState.Dodging, new DodgingState());

// 将战斗状态机作为子状态机添加到主状态机
hsm.AddState(CharacterState.Combat, combatFsm, CharacterState.Grounded);

// 切换到战斗状态时，会自动启动子状态机
hsm.ChangeState(CharacterState.Combat);",
                        Explanation = "层级状态机支持嵌套 IFSM，实现复杂的状态层次结构。"
                    }
                }
            };
        }
    }
}
#endif
