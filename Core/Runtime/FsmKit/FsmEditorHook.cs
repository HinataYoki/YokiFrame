#if UNITY_EDITOR || GODOT
using System;

namespace YokiFrame
{
    /// <summary>
    /// FSM 编辑器调试 hook，仅在编辑器中编译。
    /// </summary>
    public static class FsmEditorHook
    {
        public static Action<IFSM> OnFsmCreated;
        public static Action<IFSM> OnFsmDisposed;
        public static Action<IFSM> OnFsmCleared;
        public static Action<IFSM, string> OnFsmStarted;           // fsm、初始状态
        public static Action<IFSM, string, string> OnStateChanged; // fsm、来源状态、目标状态
        public static Action<IFSM, string> OnStateAdded;           // fsm、状态名
        public static Action<IFSM, string> OnStateRemoved;         // fsm、状态名
    }
}
#endif
