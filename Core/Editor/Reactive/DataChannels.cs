#if UNITY_EDITOR
namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 数据通道常量定义
    /// 用于 EditorDataBridge 的通道名，避免魔法字符串
    /// </summary>
    public static class DataChannels
    {
        #region ActionKit 通道

        /// <summary>
        /// Action 开始执行时触发，数据类型：IAction
        /// </summary>
        public const string ACTION_STARTED = "ActionKit.ActionStarted";
        
        /// <summary>
        /// Action 执行完成时触发，数据类型：IAction
        /// </summary>
        public const string ACTION_FINISHED = "ActionKit.ActionFinished";
        
        /// <summary>
        /// Action 进度更新时触发，数据类型：(ulong actionId, float progress)
        /// 建议使用节流订阅
        /// </summary>
        public const string ACTION_PROGRESS = "ActionKit.ActionProgress";

        #endregion

        #region UIKit 通道

        /// <summary>
        /// 面板打开时触发，数据类型：IPanel
        /// </summary>
        public const string PANEL_OPENED = "UIKit.PanelOpened";
        
        /// <summary>
        /// 面板关闭时触发，数据类型：IPanel
        /// </summary>
        public const string PANEL_CLOSED = "UIKit.PanelClosed";
        
        /// <summary>
        /// 面板状态变化时触发，数据类型：(IPanel, PanelState)
        /// </summary>
        public const string PANEL_STATE_CHANGED = "UIKit.PanelStateChanged";
        
        /// <summary>
        /// 堆栈变化时触发，数据类型：string stackName
        /// </summary>
        public const string STACK_CHANGED = "UIKit.StackChanged";
        
        /// <summary>
        /// 焦点变化时触发，数据类型：GameObject
        /// </summary>
        public const string FOCUS_CHANGED = "UIKit.FocusChanged";

        #endregion

        #region AudioKit 通道

        /// <summary>
        /// 音频开始播放时触发
        /// </summary>
        public const string AUDIO_PLAY_STARTED = "AudioKit.PlayStarted";
        
        /// <summary>
        /// 音频停止播放时触发
        /// </summary>
        public const string AUDIO_PLAY_STOPPED = "AudioKit.PlayStopped";
        
        /// <summary>
        /// 音量变化时触发，数据类型：(int channel, float volume)
        /// </summary>
        public const string AUDIO_VOLUME_CHANGED = "AudioKit.VolumeChanged";

        #endregion

        #region BuffKit 通道

        /// <summary>
        /// Buff 添加时触发
        /// </summary>
        public const string BUFF_ADDED = "BuffKit.BuffAdded";
        
        /// <summary>
        /// Buff 移除时触发
        /// </summary>
        public const string BUFF_REMOVED = "BuffKit.BuffRemoved";
        
        /// <summary>
        /// 容器创建时触发
        /// </summary>
        public const string BUFF_CONTAINER_CREATED = "BuffKit.ContainerCreated";
        
        /// <summary>
        /// 容器销毁时触发
        /// </summary>
        public const string BUFF_CONTAINER_DISPOSED = "BuffKit.ContainerDisposed";

        #endregion

        #region EventKit 通道

        /// <summary>
        /// 事件触发时通知
        /// </summary>
        public const string EVENT_TRIGGERED = "EventKit.Triggered";

        /// <summary>
        /// 事件注册时通知
        /// </summary>
        public const string EVENT_REGISTERED = "EventKit.Registered";

        /// <summary>
        /// 事件注销时通知
        /// </summary>
        public const string EVENT_UNREGISTERED = "EventKit.Unregistered";

        #endregion
    }
}
#endif
