#if UNITY_EDITOR || GODOT
using System;

namespace YokiFrame
{
    /// <summary>
    /// EventKit 监控桥使用的仅编辑器 hook 面。
    /// 运行时事件系统只会在编辑器内发布到这些委托，使监控桥可以观察注册、注销和发送活动，
    /// 同时不改变 Player 构建行为。
    /// </summary>
    public static class EasyEventEditorHook
    {
        /// <summary>运行时监听器注册时触发。参数：eventType、eventKey、handler。</summary>
        public static Action<string, string, Delegate> OnRegister;

        /// <summary>运行时监听器注销时触发。参数：eventType、eventKey、handler。</summary>
        public static Action<string, string, Delegate> OnUnRegister;

        /// <summary>运行时事件发送时触发。参数：eventType、eventKey、args、sourceFile、sourceLine。</summary>
        public static Action<string, string, object, string, int> OnSend;

        /// <summary>运行时事件清空时触发。参数：eventType、eventKey。</summary>
        public static Action<string, string> OnClear;
    }
}
#endif
