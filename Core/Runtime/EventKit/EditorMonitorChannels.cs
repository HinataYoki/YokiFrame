namespace YokiFrame
{
    /// <summary>
    /// 编辑器监控通道常量，用于跨层通信。
    /// 放在运行时程序集内，使运行时发布者和 Editor 面板共享同一组键。
    /// </summary>
    public static class EditorMonitorChannels
    {
        #region ActionKit
        public const string ACTION_STARTED = "ActionKit.ActionStarted";
        public const string ACTION_FINISHED = "ActionKit.ActionFinished";
        public const string ACTION_PROGRESS = "ActionKit.ActionProgress";
        #endregion

        #region UIKit
        public const string PANEL_OPENED = "UIKit.PanelOpened";
        public const string PANEL_CLOSED = "UIKit.PanelClosed";
        public const string PANEL_STATE_CHANGED = "UIKit.PanelStateChanged";
        public const string STACK_CHANGED = "UIKit.StackChanged";
        public const string FOCUS_CHANGED = "UIKit.FocusChanged";
        #endregion

        #region AudioKit
        public const string AUDIO_PLAY_STARTED = "AudioKit.PlayStarted";
        public const string AUDIO_PLAY_STOPPED = "AudioKit.PlayStopped";
        public const string AUDIO_VOLUME_CHANGED = "AudioKit.VolumeChanged";
        #endregion

        #region EventKit
        public const string EVENT_TRIGGERED = "EventKit.Triggered";
        public const string EVENT_REGISTERED = "EventKit.Registered";
        public const string EVENT_UNREGISTERED = "EventKit.Unregistered";
        #endregion

        #region FsmKit
        public const string FSM_LIST_CHANGED = "FsmKit.FsmListChanged";
        public const string FSM_STATE_CHANGED = "FsmKit.FsmStateChanged";
        public const string FSM_HISTORY_LOGGED = "FsmKit.HistoryLogged";
        #endregion

        #region PoolKit
        public const string POOL_LIST_CHANGED = "PoolKit.PoolListChanged";
        public const string POOL_ACTIVE_CHANGED = "PoolKit.PoolActiveChanged";
        public const string POOL_EVENT_LOGGED = "PoolKit.PoolEventLogged";
        #endregion

        #region ResKit
        public const string RES_LIST_CHANGED = "ResKit.ResListChanged";
        public const string RES_UNLOADED = "ResKit.ResUnloaded";
        #endregion
    }
}
