#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    // FsmKit 文档
    public partial class DocumentationToolPage
    {
        private DocModule CreateFsmKitDoc()
        {
            return new DocModule
            {
                Name = "FsmKit",
                Icon = KitIcons.FSMKIT,
                Category = "CORE KIT",
                Description = "轻量级有限状态机，支持普通状态机和层级状态机。提供状态进入、更新、退出的完整生命周期管理，不依赖 MonoBehaviour。",
                Keywords = new List<string> { "状态机", "FSM", "状态切换", "层级状态" },
                Sections = new List<DocSection>
                {
                    new()
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
}"
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
}"
                            }
                        }
                    },
                    new()
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
}"
                            }
                        }
                    },
                    new()
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
}"
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
                    },
                    new()
                    {
                        Title = "编辑器工具",
                        Description = "FsmKit 提供运行时状态机查看器，可在 YokiFrame Tools 面板中查看所有状态机的状态和转换。",
                        CodeExamples = new List<CodeExample>
                        {
                            new()
                            {
                                Title = "打开状态机查看器",
                                Code = @"// 快捷键：Ctrl+E 打开 YokiFrame Tools 面板
// 选择 FsmKit 标签页

// 功能：
// - 实时查看所有运行中的状态机
// - 显示当前状态和状态历史
// - 查看状态转换记录
// - 支持普通状态机和层级状态机",
                                Explanation = "状态机查看器帮助调试状态逻辑，追踪状态转换流程。"
                            }
                        }
                    }
                }
            };
        }
    }
}
#endif
