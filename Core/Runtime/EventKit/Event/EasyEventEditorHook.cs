#if UNITY_EDITOR
using System;

namespace YokiFrame
{
    /// <summary>
    /// Editor-only hook surface used by EventKit monitor bridges.
    /// </summary>
    /// <remarks>
    /// Runtime event systems publish to these delegates only inside the editor so monitor bridges can observe
    /// registration, unregistration, and send activity without changing player-build behavior.
    /// </remarks>
    public static class EasyEventEditorHook
    {
        /// <summary>
        /// Invoked when a runtime listener is registered.
        /// </summary>
        public static Action<Delegate> OnRegister;

        /// <summary>
        /// Invoked when a runtime listener is unregistered.
        /// </summary>
        public static Action<Delegate> OnUnRegister;

        /// <summary>
        /// Invoked when a runtime event is sent.
        /// Parameters are <c>eventType</c>, <c>eventKey</c>, and <c>args</c>.
        /// </summary>
        public static Action<string, string, object> OnSend;
    }
}
#endif
