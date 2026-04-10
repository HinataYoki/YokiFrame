#if UNITY_EDITOR
namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Canonical editor data channel constants used by officially normalized kits.
    /// </summary>
    /// <remarks>
    /// Only channels that have already been folded into the shared editor communication contract belong here.
    /// Transitional kits such as <c>FsmKit</c>, <c>PoolKit</c>, and <c>ResKit</c> may still keep local channel
    /// constants until their monitor architecture is migrated.
    /// </remarks>
    public static class DataChannels
    {
        #region ActionKit

        /// <summary>
        /// Fired when an action starts executing.
        /// Payload: <c>IAction</c>.
        /// </summary>
        public const string ACTION_STARTED = "ActionKit.ActionStarted";

        /// <summary>
        /// Fired when an action finishes execution.
        /// Payload: <c>IAction</c>.
        /// </summary>
        public const string ACTION_FINISHED = "ActionKit.ActionFinished";

        /// <summary>
        /// Fired when action progress changes.
        /// Payload: <c>(ulong actionId, float progress)</c>.
        /// Throttled subscriptions are recommended.
        /// </summary>
        public const string ACTION_PROGRESS = "ActionKit.ActionProgress";

        #endregion

        #region UIKit

        /// <summary>
        /// Fired after a panel is opened.
        /// Payload: <c>IPanel</c>.
        /// </summary>
        public const string PANEL_OPENED = "UIKit.PanelOpened";

        /// <summary>
        /// Fired after a panel is closed.
        /// Payload: <c>IPanel</c>.
        /// </summary>
        public const string PANEL_CLOSED = "UIKit.PanelClosed";

        /// <summary>
        /// Fired when a panel state changes.
        /// Payload: <c>(IPanel panel, PanelState state)</c>.
        /// </summary>
        public const string PANEL_STATE_CHANGED = "UIKit.PanelStateChanged";

        /// <summary>
        /// Fired when the panel stack changes.
        /// Payload: <c>string stackName</c>.
        /// </summary>
        public const string STACK_CHANGED = "UIKit.StackChanged";

        /// <summary>
        /// Fired when the current focus target changes.
        /// Payload: <c>GameObject</c>.
        /// </summary>
        public const string FOCUS_CHANGED = "UIKit.FocusChanged";

        #endregion

        #region AudioKit

        /// <summary>
        /// Fired when audio playback starts.
        /// Payload: <c>(string path, int channelId, float volume, float pitch, float duration)</c>.
        /// </summary>
        public const string AUDIO_PLAY_STARTED = "AudioKit.PlayStarted";

        /// <summary>
        /// Fired when audio playback stops.
        /// Payload: <c>(string path, int channelId)</c>.
        /// </summary>
        public const string AUDIO_PLAY_STOPPED = "AudioKit.PlayStopped";

        /// <summary>
        /// Fired when channel volume changes.
        /// Payload: <c>(int channel, float volume)</c>.
        /// </summary>
        public const string AUDIO_VOLUME_CHANGED = "AudioKit.VolumeChanged";

        #endregion

        #region BuffKit

        /// <summary>
        /// Fired when a buff is added.
        /// Payload: <c>BuffAddedEvent</c>.
        /// </summary>
        public const string BUFF_ADDED = "BuffKit.BuffAdded";

        /// <summary>
        /// Fired when a buff is removed.
        /// Payload: <c>BuffRemovedEvent</c>.
        /// </summary>
        public const string BUFF_REMOVED = "BuffKit.BuffRemoved";

        /// <summary>
        /// Fired when a buff container is created.
        /// Payload type depends on the bridge implementation.
        /// </summary>
        public const string BUFF_CONTAINER_CREATED = "BuffKit.ContainerCreated";

        /// <summary>
        /// Fired when a buff container is disposed.
        /// Payload type depends on the bridge implementation.
        /// </summary>
        public const string BUFF_CONTAINER_DISPOSED = "BuffKit.ContainerDisposed";

        #endregion

        #region FsmKit

        /// <summary>
        /// Fired when the active FSM list changes.
        /// Payload: <c>IFSM</c>.
        /// </summary>
        public const string FSM_LIST_CHANGED = "FsmKit.FsmListChanged";

        /// <summary>
        /// Fired when an FSM runtime state changes.
        /// Payload: <c>IFSM</c>.
        /// </summary>
        public const string FSM_STATE_CHANGED = "FsmKit.FsmStateChanged";

        /// <summary>
        /// Fired when an FSM transition history entry is appended.
        /// Payload: <c>FsmDebugger.TransitionEntry</c>.
        /// </summary>
        public const string FSM_HISTORY_LOGGED = "FsmKit.HistoryLogged";

        #endregion

        #region EventKit

        /// <summary>
        /// Fired when EventKit triggers an event.
        /// Payload: <c>(string eventType, string eventKey, string argsText)</c>.
        /// </summary>
        public const string EVENT_TRIGGERED = "EventKit.Triggered";

        /// <summary>
        /// Fired when EventKit registers a listener.
        /// Payload: <c>(string category, string listenerName)</c>.
        /// </summary>
        public const string EVENT_REGISTERED = "EventKit.Registered";

        /// <summary>
        /// Fired when EventKit unregisters a listener.
        /// Payload: <c>(string category, string listenerName)</c>.
        /// </summary>
        public const string EVENT_UNREGISTERED = "EventKit.Unregistered";

        #endregion

        #region PoolKit

        /// <summary>
        /// Fired when the pool list changes.
        /// Payload: <c>PoolDebugInfo</c>.
        /// </summary>
        public const string POOL_LIST_CHANGED = "PoolKit.PoolListChanged";

        /// <summary>
        /// Fired when the active objects in a pool change.
        /// Payload: <c>PoolDebugInfo</c>.
        /// </summary>
        public const string POOL_ACTIVE_CHANGED = "PoolKit.PoolActiveChanged";

        /// <summary>
        /// Fired when a pool event log entry is appended.
        /// Payload: <c>PoolEvent</c>.
        /// </summary>
        public const string POOL_EVENT_LOGGED = "PoolKit.PoolEventLogged";

        #endregion

        #region ResKit

        /// <summary>
        /// Fired when the loaded resource list changes.
        /// Payload: <c>int</c>, current loaded count.
        /// </summary>
        public const string RES_LIST_CHANGED = "ResKit.ResListChanged";

        /// <summary>
        /// Fired when a resource unload is detected.
        /// Payload: <c>ResDebugger.UnloadRecord</c>.
        /// </summary>
        public const string RES_UNLOADED = "ResKit.ResUnloaded";

        #endregion
    }
}
#endif
