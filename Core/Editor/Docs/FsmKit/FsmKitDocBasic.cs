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
                    },
                    new()
                    {
                        Title = "编辑器监控面板",
                        Code = @"// FsmKit 监控面板采用响应式架构
// 打开方式：Tools > YokiFrame > YokiFrame Tools > FsmKit

// 监控面板功能：
// - HUD 卡片：显示状态机总数、活跃数、状态转换次数
// - 状态矩阵：可视化状态转换关系
// - 时间线：记录状态转换历史

// 响应式数据流：
// FsmDebugger (运行时)
//   → EditorDataBridge.NotifyDataChanged()
//   → FsmKitToolPage 订阅回调
//   → FsmKitViewModel 更新
//   → UI 自动刷新

// 通道定义：
// CHANNEL_FSM_STATE_CHANGED - 状态变化通知
// CHANNEL_FSM_REGISTERED    - 状态机注册通知",
                        Explanation = "监控面板使用响应式架构，仅在状态变化时更新 UI，避免轮询开销。"
                    },
                    new()
                    {
                        Title = "自定义监控扩展",
                        Code = @"// 订阅状态机状态变化（编辑器代码）
#if UNITY_EDITOR
using YokiFrame.EditorTools;

// 订阅状态变化
var subscription = EditorDataBridge.Subscribe<FsmStateChangedData>(
    DataChannels.CHANNEL_FSM_STATE_CHANGED,
    data => 
    {
        Debug.Log($""状态机 {data.FsmName}: {data.FromState} → {data.ToState}"");
    });

// 取消订阅
subscription.Dispose();
#endif",
                        Explanation = "可通过 EditorDataBridge 订阅状态机数据变化，实现自定义监控逻辑。"
                    }
                }
            };
        }
    }
}
#endif
