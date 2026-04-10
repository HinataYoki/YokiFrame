#if UNITY_EDITOR
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Basic FsmKit documentation.
    /// </summary>
    internal static class FsmKitDocBasic
    {
        internal static DocSection CreateSection()
        {
            return new DocSection
            {
                Title = "Basic FSM",
                Description = "Use enums to define states and build a simple, efficient finite state machine with FSM<TState>.",
                CodeExamples = new List<CodeExample>
                {
                    new()
                    {
                        Title = "Define States",
                        Code = @"public enum PlayerState
{
    Idle,
    Walk,
    Run,
    Jump,
    Attack,
    Dead
}",
                        Explanation = "Use enums to define state ids and avoid magic strings."
                    },
                    new()
                    {
                        Title = "Create And Use FSM",
                        Code = @"public class PlayerController
{
    private FSM<PlayerState> mFsm;
    
    public void Init()
    {
        mFsm = new FSM<PlayerState>();
        mFsm.AddState(PlayerState.Idle, new IdleState());
        mFsm.AddState(PlayerState.Walk, new WalkState());
        mFsm.AddState(PlayerState.Run, new RunState());
        mFsm.AddState(PlayerState.Jump, new JumpState());
        mFsm.AddState(PlayerState.Attack, new AttackState());
        mFsm.Start(PlayerState.Idle);
    }
    
    public void Update()
    {
        mFsm.Update();
    }
    
    public PlayerState CurrentState => mFsm.CurrentStateId;
}",
                        Explanation = "FSM instances are driven from Update and expose the current state id."
                    },
                    new()
                    {
                        Title = "Editor Monitor",
                        Code = @"// Open: Tools > YokiFrame > YokiFrame Tools > FsmKit
//
// Main monitor areas:
// - HUD cards: runtime summary
// - State matrix: transition relationships
// - Timeline: transition history
//
// Reactive flow:
// FsmDebugger
//   -> EditorDataBridge.NotifyDataChanged()
//   -> FsmKitToolPage subscriptions
//   -> FsmKitViewModel / page state
//   -> UI refresh
//
// Shared channels:
// DataChannels.FSM_LIST_CHANGED
// DataChannels.FSM_STATE_CHANGED
// DataChannels.FSM_HISTORY_LOGGED",
                        Explanation = "The FsmKit monitor uses shared editor channels instead of per-frame polling for structural data changes."
                    },
                    new()
                    {
                        Title = "Custom Editor Subscription",
                        Code = @"#if UNITY_EDITOR
using YokiFrame.EditorTools;

var subscription = EditorDataBridge.Subscribe<IFSM>(
    DataChannels.FSM_STATE_CHANGED,
    fsm =>
    {
        Debug.Log($""FSM {fsm.Name} changed state to {fsm.CurrentStateId}"");
    });

subscription.Dispose();
#endif",
                        Explanation = "Custom editor tooling can subscribe to the shared FsmKit state-change channel."
                    }
                }
            };
        }
    }
}
#endif
