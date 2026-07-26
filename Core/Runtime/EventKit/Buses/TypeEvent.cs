using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 以事件负载类型为键的事件总线。
    /// </summary>
    public sealed class TypeEvent
    {
        private readonly EasyEvents mEventDic = new EasyEvents();

        /// <summary>
        /// 按负载类型发送事件。
        /// </summary>
        /// <typeparam name="T">事件负载类型。</typeparam>
        /// <param name="args">事件负载。</param>
        public void Send<T>(T args = default)
        {
#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Send,
                typeof(T),
                typeof(T),
                null));
#endif
            EasyEvent<T> easyEvent = mEventDic.GetEvent<EasyEvent<T>>();
            easyEvent?.Trigger(args);
        }

        /// <summary>
        /// 注册类型事件监听器。
        /// </summary>
        /// <typeparam name="T">事件负载类型。</typeparam>
        /// <param name="onEvent">事件触发时调用的监听器。</param>
        /// <returns>用于注销该监听器的令牌。</returns>
        public LinkUnRegister<T> Register<T>(Action<T> onEvent)
        {
            if (onEvent == null)
            {
                throw new ArgumentNullException(nameof(onEvent));
            }

            LinkUnRegister<T> token = mEventDic.GetOrAddEvent<EasyEvent<T>>().Register(onEvent);
#if UNITY_EDITOR || (GODOT && TOOLS)
            var registerNotification = new EventKitEditorNotification(
                EventKitEditorNotificationKind.Register,
                typeof(T),
                typeof(T),
                onEvent);
            if (EasyEventEditorHook.Publish(registerNotification))
            {
                return token.WithEditorUnregisterNotification(new EventKitEditorNotification(
                    EventKitEditorNotificationKind.Unregister,
                    typeof(T),
                    typeof(T),
                    onEvent));
            }
#endif
            return token;
        }

        /// <summary>
        /// 注销一个类型事件监听器。
        /// </summary>
        /// <typeparam name="T">事件负载类型。</typeparam>
        /// <param name="onEvent">需要移除的监听器。</param>
        public void UnRegister<T>(Action<T> onEvent)
        {
            EasyEvent<T> easyEvent = mEventDic.GetEvent<EasyEvent<T>>();
            if (easyEvent == null || !easyEvent.UnRegister(onEvent))
            {
                return;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Unregister,
                typeof(T),
                typeof(T),
                onEvent));
#endif
        }

        /// <summary>
        /// 清空全部已注册类型事件。
        /// </summary>
        public void Clear()
        {
            mEventDic.Clear();
#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Clear,
                (Type)null,
                null,
                null));
#endif
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 返回全部已注册类型事件，用于编辑器检查或诊断。
        /// </summary>
        /// <returns>事件容器字典。</returns>
        public IReadOnlyDictionary<Type, IEasyEvent> GetAllEvents()
        {
            return mEventDic.GetAllEvents();
        }
#endif

    }
}
