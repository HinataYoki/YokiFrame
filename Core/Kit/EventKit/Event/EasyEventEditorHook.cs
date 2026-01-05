#if UNITY_EDITOR
using System;

namespace YokiFrame
{
    /// <summary>
    /// 编辑器调试钩子（仅供 EventKit 内部使用）
    /// </summary>
    public static class EasyEventEditorHook
    {
        public static Action<Delegate> OnRegister;
        public static Action<Delegate> OnUnRegister;
        public static Action<string, string, object> OnSend; // eventType, eventKey, args
    }
}
#endif
