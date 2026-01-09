#if UNITY_EDITOR
using System;

namespace YokiFrame
{
    /// <summary>
    /// FSM 编辑器调试钩子
    /// </summary>
    public static class FsmEditorHook
    {
        public static Action<IFSM> OnFsmCreated;
        public static Action<IFSM> OnFsmDisposed;
        public static Action<IFSM> OnFsmCleared;
        public static Action<IFSM, string> OnFsmStarted;           // fsm, initialState
        public static Action<IFSM, string, string> OnStateChanged; // fsm, fromState, toState
        public static Action<IFSM, string> OnStateAdded;           // fsm, stateName
        public static Action<IFSM, string> OnStateRemoved;         // fsm, stateName
    }
}
#endif
