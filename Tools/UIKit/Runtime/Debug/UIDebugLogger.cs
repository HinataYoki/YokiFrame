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
        /// <param name="level">日志级别</param>
        /// <param name="category">分类</param>
        /// <param name="message">消息内容</param>
        public static void Log(LogLevel level, string category, string message)
        {
            if (!sIsEnabled || level < sLogLevel) return;
            
            var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("[");
                sb.Append(timestamp);
                sb.Append("] [UIKit:");
                sb.Append(category);
                sb.Append("] ");
                sb.Append(message);
                var formattedMessage = sb.ToString();
                
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
#else
            var formattedMessage = "[" + timestamp + "] [UIKit:" + category + "] " + message;
            
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
#endif
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
            var panelName = evt.Panel != default ? evt.Panel.GetType().Name : "Unknown";
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("Panel.WillShow: ");
                sb.Append(panelName);
                Log(LogLevel.Verbose, "Lifecycle", sb.ToString());
            }
#else
            Log(LogLevel.Verbose, "Lifecycle", "Panel.WillShow: " + panelName);
#endif
        }

        private static void OnPanelDidShow(PanelDidShowEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel != default ? evt.Panel.GetType().Name : "Unknown";
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("Panel.DidShow: ");
                sb.Append(panelName);
                Log(LogLevel.Info, "Lifecycle", sb.ToString());
            }
#else
            Log(LogLevel.Info, "Lifecycle", "Panel.DidShow: " + panelName);
#endif
        }

        private static void OnPanelWillHide(PanelWillHideEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel != default ? evt.Panel.GetType().Name : "Unknown";
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("Panel.WillHide: ");
                sb.Append(panelName);
                Log(LogLevel.Verbose, "Lifecycle", sb.ToString());
            }
#else
            Log(LogLevel.Verbose, "Lifecycle", "Panel.WillHide: " + panelName);
#endif
        }

        private static void OnPanelDidHide(PanelDidHideEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel != default ? evt.Panel.GetType().Name : "Unknown";
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("Panel.DidHide: ");
                sb.Append(panelName);
                Log(LogLevel.Info, "Lifecycle", sb.ToString());
            }
#else
            Log(LogLevel.Info, "Lifecycle", "Panel.DidHide: " + panelName);
#endif
        }

        private static void OnPanelFocus(PanelFocusEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel != default ? evt.Panel.GetType().Name : "Unknown";
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("Panel.Focus: ");
                sb.Append(panelName);
                Log(LogLevel.Verbose, "Lifecycle", sb.ToString());
            }
#else
            Log(LogLevel.Verbose, "Lifecycle", "Panel.Focus: " + panelName);
#endif
        }

        private static void OnPanelBlur(PanelBlurEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel != default ? evt.Panel.GetType().Name : "Unknown";
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("Panel.Blur: ");
                sb.Append(panelName);
                Log(LogLevel.Verbose, "Lifecycle", sb.ToString());
            }
#else
            Log(LogLevel.Verbose, "Lifecycle", "Panel.Blur: " + panelName);
#endif
        }

        private static void OnPanelResume(PanelResumeEvent evt)
        {
            if (!sLogLifecycle) return;
            var panelName = evt.Panel != default ? evt.Panel.GetType().Name : "Unknown";
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("Panel.Resume: ");
                sb.Append(panelName);
                Log(LogLevel.Verbose, "Lifecycle", sb.ToString());
            }
#else
            Log(LogLevel.Verbose, "Lifecycle", "Panel.Resume: " + panelName);
#endif
        }

        private static void OnFocusChanged(UIFocusChangedEvent evt)
        {
            if (!sLogFocus) return;
            var prevName = evt.Previous != null ? evt.Previous.name : "null";
            var currName = evt.Current != null ? evt.Current.name : "null";
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("焦点变化: ");
                sb.Append(prevName);
                sb.Append(" -> ");
                sb.Append(currName);
                Log(LogLevel.Verbose, "Focus", sb.ToString());
            }
#else
            Log(LogLevel.Verbose, "Focus", "焦点变化: " + prevName + " -> " + currName);
#endif
        }

        private static void OnInputModeChanged(UIInputModeChangedEvent evt)
        {
            if (!sLogFocus) return;
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append("输入模式变化: ");
                sb.Append(evt.Mode.ToString());
                Log(LogLevel.Info, "Focus", sb.ToString());
            }
#else
            Log(LogLevel.Info, "Focus", "输入模式变化: " + evt.Mode.ToString());
#endif
        }

        #endregion

        #region 手动日志方法

        /// <summary>
        /// 记录栈操作
        /// </summary>
        /// <param name="operation">操作名称</param>
        /// <param name="stackName">栈名称</param>
        /// <param name="panel">面板</param>
        internal static void LogStackOperation(string operation, string stackName, IPanel panel)
        {
            if (!sIsEnabled || !sLogStack) return;
            var panelName = panel != default ? panel.GetType().Name : "null";
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append(operation);
                sb.Append(": ");
                sb.Append(panelName);
                sb.Append(" (栈: ");
                sb.Append(stackName);
                sb.Append(")");
                Log(LogLevel.Info, "Stack", sb.ToString());
            }
#else
            Log(LogLevel.Info, "Stack", operation + ": " + panelName + " (栈: " + stackName + ")");
#endif
        }

        /// <summary>
        /// 记录缓存操作
        /// </summary>
        /// <param name="operation">操作名称</param>
        /// <param name="panelType">面板类型</param>
        /// <param name="success">是否成功</param>
        internal static void LogCacheOperation(string operation, Type panelType, bool success = true)
        {
            if (!sIsEnabled || !sLogCache) return;
            var typeName = panelType != default ? panelType.Name : "Unknown";
            var status = success ? "成功" : "失败";
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append(operation);
                sb.Append(": ");
                sb.Append(typeName);
                sb.Append(" (");
                sb.Append(status);
                sb.Append(")");
                Log(LogLevel.Info, "Cache", sb.ToString());
            }
#else
            Log(LogLevel.Info, "Cache", operation + ": " + typeName + " (" + status + ")");
#endif
        }

        /// <summary>
        /// 记录动画事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="panel">面板</param>
        /// <param name="animationType">动画类型</param>
        internal static void LogAnimationEvent(string eventName, IPanel panel, string animationType)
        {
            if (!sIsEnabled || !sLogAnimation) return;
            var panelName = panel != default ? panel.GetType().Name : "Unknown";
#if YOKIFRAME_ZSTRING_SUPPORT
            using (var sb = Cysharp.Text.ZString.CreateStringBuilder())
            {
                sb.Append(eventName);
                sb.Append(": ");
                sb.Append(panelName);
                sb.Append(" (");
                sb.Append(animationType);
                sb.Append(")");
                Log(LogLevel.Verbose, "Animation", sb.ToString());
            }
#else
            Log(LogLevel.Verbose, "Animation", eventName + ": " + panelName + " (" + animationType + ")");
#endif
        }

        #endregion
    }
}
