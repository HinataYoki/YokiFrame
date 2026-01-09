using System;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// UI 调试日志系统 - 记录所有生命周期事件
    /// </summary>
    public static class UIDebugLogger
    {
        #region 字段

        private static bool sIsEnabled;
        private static LogLevel sLogLevel = LogLevel.Info;
        private static bool sLogLifecycle = true;
        private static bool sLogStack = true;
        private static bool sLogFocus = true;
        private static bool sLogCache = true;
        private static bool sLogAnimation = true;

        #endregion

        #region 枚举

        /// <summary>
        /// 日志级别
        /// </summary>
        public enum LogLevel
        {
            Verbose,
            Info,
            Warning,
            Error,
            None
        }

        #endregion

        #region 公共属性

        /// <summary>
        /// 是否启用调试日志
        /// </summary>
        public static bool IsEnabled
        {
            get => sIsEnabled;
            set
            {
                if (sIsEnabled != value)
                {
                    sIsEnabled = value;
                    if (value)
                    {
                        SubscribeEvents();
                        Log(LogLevel.Info, "UIKit", "调试日志已启用");
                    }
                    else
                    {
                        UnsubscribeEvents();
                        Log(LogLevel.Info, "UIKit", "调试日志已禁用");
                    }
                }
            }
        }

        /// <summary>
        /// 日志级别
        /// </summary>
        public static LogLevel Level
        {
            get => sLogLevel;
            set => sLogLevel = value;
        }

        /// <summary>
        /// 是否记录生命周期事件
        /// </summary>
        public static bool LogLifecycle
        {
            get => sLogLifecycle;
            set => sLogLifecycle = value;
        }

        /// <summary>
        /// 是否记录栈操作
        /// </summary>
        public static bool LogStack
        {
            get => sLogStack;
            set => sLogStack = value;
        }

        /// <summary>
        /// 是否记录焦点变化
        /// </summary>
        public static bool LogFocus
        {
            get => sLogFocus;
            set => sLogFocus = value;
        }

        /// <summary>
        /// 是否记录缓存操作
        /// </summary>
        public static bool LogCache
        {
            get => sLogCache;
            set => sLogCache = value;
        }

        /// <summary>
        /// 是否记录动画事件
        /// </summary>
        public static bool LogAnimation
        {
            get => sLogAnimation;
            set => sLogAnimation = value;
        }

        #endregion

        #region 公共方法

        /// <summary>
        /// 启用调试日志
        /// </summary>
        public static void Enable(LogLevel level = LogLevel.Info)
        {
            sLogLevel = level;
            IsEnabled = true;
        }

        /// <summary>
        /// 禁用调试日志
        /// </summary>
        public static void Disable()
        {
            IsEnabled = false;
        }

        /// <summary>
        /// 配置日志选项
        /// </summary>
        public static void Configure(bool lifecycle = true, bool stack = true, bool focus = true, bool cache = true, bool animation = true)
        {
            sLogLifecycle = lifecycle;
            sLogStack = stack;
            sLogFocus = focus;
            sLogCache = cache;
            sLogAnimation = animation;
        }

        /// <summary>
        /// 手动记录日志
        /// </summary>
        public static void Log(LogLevel level, string category, string message)
        {
            if (!sIsEnabled || level < sLogLevel) return;
            
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            var formattedMessage = $"[{timestamp}] [UIKit:{category}] {message}";
            
            switch (level)
            {
                case LogLevel.Verbose:
                case LogLevel.Info:
                    Debug.Log(formattedMessage);
                    break;
                case LogLevel.Warning:
                    Debug.LogWarning(formattedMessage);
                    break;
                case LogLevel.Error:
                    Debug.LogError(formattedMessage);
                    break;
            }
        }

        #endregion

        #region 事件订阅

        private static void SubscribeEvents()
        {
            // 生命周期事件
            EventKit.Type.Register<PanelWillShowEvent>(OnPanelWillShow);
            EventKit.Type.Register<PanelDidShowEvent>(OnPanelDidShow);
            EventKit.Type.Register<PanelWillHideEvent>(OnPanelWillHide);
            EventKit.Type.Register<PanelDidHideEvent>(OnPanelDidHide);
            EventKit.Type.Register<PanelFocusEvent>(OnPanelFocus);
            EventKit.Type.Register<PanelBlurEvent>(OnPanelBlur);
            EventKit.Type.Register<PanelResumeEvent>(OnPanelResume);
            
            // 焦点事件
            EventKit.Type.Register<UIFocusChangedEvent>(OnFocusChanged);
            EventKit.Type.Register<UIInputModeChangedEvent>(OnInputModeChanged);
        }

        private static void UnsubscribeEvents()
        {
            // 生命周期事件
            EventKit.Type.UnRegister<PanelWillShowEvent>(OnPanelWillShow);
            EventKit.Type.UnRegister<PanelDidShowEvent>(OnPanelDidShow);
            EventKit.Type.UnRegister<PanelWillHideEvent>(OnPanelWillHide);
            EventKit.Type.UnRegister<PanelDidHideEvent>(OnPanelDidHide);
            EventKit.Type.UnRegister<PanelFocusEvent>(OnPanelFocus);
            EventKit.Type.UnRegister<PanelBlurEvent>(OnPanelBlur);
            EventKit.Type.UnRegister<PanelResumeEvent>(OnPanelResume);
            
            // 焦点事件
            EventKit.Type.UnRegister<UIFocusChangedEvent>(OnFocusChanged);
            EventKit.Type.UnRegister<UIInputModeChangedEvent>(OnInputModeChanged);
        }

        #endregion

        #region 事件处理

        private static void OnPanelWillShow(PanelWillShowEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel?.GetType().Name ?? "Unknown";
            Log(LogLevel.Verbose, "Lifecycle", $"Panel.WillShow: {panelName}");
        }

        private static void OnPanelDidShow(PanelDidShowEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel?.GetType().Name ?? "Unknown";
            Log(LogLevel.Info, "Lifecycle", $"Panel.DidShow: {panelName}");
        }

        private static void OnPanelWillHide(PanelWillHideEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel?.GetType().Name ?? "Unknown";
            Log(LogLevel.Verbose, "Lifecycle", $"Panel.WillHide: {panelName}");
        }

        private static void OnPanelDidHide(PanelDidHideEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel?.GetType().Name ?? "Unknown";
            Log(LogLevel.Info, "Lifecycle", $"Panel.DidHide: {panelName}");
        }

        private static void OnPanelFocus(PanelFocusEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel?.GetType().Name ?? "Unknown";
            Log(LogLevel.Verbose, "Lifecycle", $"Panel.Focus: {panelName}");
        }

        private static void OnPanelBlur(PanelBlurEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel?.GetType().Name ?? "Unknown";
            Log(LogLevel.Verbose, "Lifecycle", $"Panel.Blur: {panelName}");
        }

        private static void OnPanelResume(PanelResumeEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel?.GetType().Name ?? "Unknown";
            Log(LogLevel.Verbose, "Lifecycle", $"Panel.Resume: {panelName}");
        }

        private static void OnFocusChanged(UIFocusChangedEvent evt)
        {
            if (!sLogFocus) return;
            var prevName = evt.Previous != null ? evt.Previous.name : "null";
            var currName = evt.Current != null ? evt.Current.name : "null";
            Log(LogLevel.Verbose, "Focus", $"焦点变化: {prevName} -> {currName}");
        }

        private static void OnInputModeChanged(UIInputModeChangedEvent evt)
        {
            if (!sLogFocus) return;
            Log(LogLevel.Info, "Focus", $"输入模式变化: {evt.Mode}");
        }

        #endregion

        #region 手动日志方法

        /// <summary>
        /// 记录栈操作
        /// </summary>
        internal static void LogStackOperation(string operation, string stackName, IPanel panel)
        {
            if (!sIsEnabled || !sLogStack) return;
            var panelName = panel?.GetType().Name ?? "null";
            Log(LogLevel.Info, "Stack", $"{operation}: {panelName} (栈: {stackName})");
        }

        /// <summary>
        /// 记录缓存操作
        /// </summary>
        internal static void LogCacheOperation(string operation, Type panelType, bool success = true)
        {
            if (!sIsEnabled || !sLogCache) return;
            var typeName = panelType?.Name ?? "Unknown";
            var status = success ? "成功" : "失败";
            Log(LogLevel.Info, "Cache", $"{operation}: {typeName} ({status})");
        }

        /// <summary>
        /// 记录动画事件
        /// </summary>
        internal static void LogAnimationEvent(string eventName, IPanel panel, string animationType)
        {
            if (!sIsEnabled || !sLogAnimation) return;
            var panelName = panel?.GetType().Name ?? "Unknown";
            Log(LogLevel.Verbose, "Animation", $"{eventName}: {panelName} ({animationType})");
        }

        #endregion
    }
}
