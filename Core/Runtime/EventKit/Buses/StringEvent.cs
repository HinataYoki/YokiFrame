using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 以字符串为键的兼容事件总线。
    /// </summary>
    /// <remarks>
    /// 字符串事件缺少类型安全和重构保护，新代码应优先使用 TypeEvent 或 EnumEvent。
    /// </remarks>
    [Obsolete("StringEvent 存在类型安全和重构风险，请优先使用 TypeEvent 或 EnumEvent。")]
    public sealed class StringEvent
    {
        private readonly Dictionary<string, EasyEvents> mEventDic = new Dictionary<string, EasyEvents>();

        /// <summary>
        /// 发送无参数字符串事件。
        /// </summary>
        /// <param name="key">字符串事件键。</param>
        public void Send(string key)
        {
            SendCore<object>(key, null, false);
        }

        /// <summary>
        /// 发送带类型负载的字符串事件。
        /// </summary>
        /// <typeparam name="T">事件负载类型。</typeparam>
        /// <param name="key">字符串事件键。</param>
        /// <param name="args">事件负载。</param>
        public void Send<T>(string key, T args)
        {
            SendCore(key, args, true);
        }

        /// <summary>
        /// 发送可变参数字符串事件；仅为兼容旧代码保留。
        /// </summary>
        /// <param name="key">字符串事件键。</param>
        /// <param name="args">可变参数负载。</param>
        [Obsolete("params object[] 会产生分配且缺少类型安全，请使用 Send<T>(string, T)。")]
        public void Send(string key, params object[] args)
        {
            SendCore(key, args, true);
        }

        /// <summary>
        /// 注册无参数字符串事件监听器。
        /// </summary>
        /// <param name="key">字符串事件键。</param>
        /// <param name="onEvent">事件触发时调用的监听器。</param>
        /// <returns>用于注销该监听器的令牌。</returns>
        public LinkUnRegister Register(string key, Action onEvent)
        {
            RequireKey(key);
            if (onEvent == null)
            {
                throw new ArgumentNullException(nameof(onEvent));
            }

            EasyEvents stringEvent = GetOrCreateEvents(key);
            LinkUnRegister token = stringEvent.GetOrAddEvent<EasyEvent>().Register(onEvent);
#if UNITY_EDITOR || (GODOT && TOOLS)
            var registerNotification = new EventKitEditorNotification(
                EventKitEditorNotificationKind.Register,
                key,
                null,
                onEvent);
            if (EasyEventEditorHook.Publish(registerNotification))
            {
                return token.WithEditorUnregisterNotification(new EventKitEditorNotification(
                    EventKitEditorNotificationKind.Unregister,
                    key,
                    null,
                    onEvent));
            }
#endif
            return token;
        }

        /// <summary>
        /// 注册带类型负载的字符串事件监听器。
        /// </summary>
        /// <typeparam name="T">事件负载类型。</typeparam>
        /// <param name="key">字符串事件键。</param>
        /// <param name="onEvent">事件触发时调用的监听器。</param>
        /// <returns>用于注销该监听器的令牌。</returns>
        public LinkUnRegister<T> Register<T>(string key, Action<T> onEvent)
        {
            RequireKey(key);
            if (onEvent == null)
            {
                throw new ArgumentNullException(nameof(onEvent));
            }

            EasyEvents stringEvent = GetOrCreateEvents(key);
            LinkUnRegister<T> token = stringEvent.GetOrAddEvent<EasyEvent<T>>().Register(onEvent);
#if UNITY_EDITOR || (GODOT && TOOLS)
            var registerNotification = new EventKitEditorNotification(
                EventKitEditorNotificationKind.Register,
                key,
                typeof(T),
                onEvent);
            if (EasyEventEditorHook.Publish(registerNotification))
            {
                return token.WithEditorUnregisterNotification(new EventKitEditorNotification(
                    EventKitEditorNotificationKind.Unregister,
                    key,
                    typeof(T),
                    onEvent));
            }
#endif
            return token;
        }

        /// <summary>
        /// 注册可变参数字符串事件监听器；仅为兼容旧代码保留。
        /// </summary>
        /// <param name="key">字符串事件键。</param>
        /// <param name="onEvent">事件触发时调用的监听器。</param>
        /// <returns>用于注销该监听器的令牌。</returns>
        public LinkUnRegister<object[]> Register(string key, Action<object[]> onEvent)
        {
            return Register<object[]>(key, onEvent);
        }

        /// <summary>
        /// 清空绑定到指定字符串键的全部监听器。
        /// </summary>
        /// <param name="key">字符串事件键。</param>
        public void UnRegister(string key)
        {
            RequireKey(key);
            EasyEvents stringEvent;
            if (mEventDic.TryGetValue(key, out stringEvent))
            {
                stringEvent.Clear();
                mEventDic.Remove(key);
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Clear,
                key,
                null,
                null));
#endif
        }

        /// <summary>
        /// 注销一个无参数字符串事件监听器。
        /// </summary>
        /// <param name="key">字符串事件键。</param>
        /// <param name="onEvent">需要移除的监听器。</param>
        public void UnRegister(string key, Action onEvent)
        {
            RequireKey(key);
            EasyEvents stringEvent;
            if (!mEventDic.TryGetValue(key, out stringEvent))
            {
                return;
            }

            EasyEvent easyEvent = stringEvent.GetEvent<EasyEvent>();
            if (easyEvent == null || !easyEvent.UnRegister(onEvent))
            {
                return;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Unregister,
                key,
                null,
                onEvent));
#endif
        }

        /// <summary>
        /// 注销一个带类型负载的字符串事件监听器。
        /// </summary>
        /// <typeparam name="T">事件负载类型。</typeparam>
        /// <param name="key">字符串事件键。</param>
        /// <param name="onEvent">需要移除的监听器。</param>
        public void UnRegister<T>(string key, Action<T> onEvent)
        {
            RequireKey(key);
            EasyEvents stringEvent;
            if (!mEventDic.TryGetValue(key, out stringEvent))
            {
                return;
            }

            EasyEvent<T> easyEvent = stringEvent.GetEvent<EasyEvent<T>>();
            if (easyEvent == null || !easyEvent.UnRegister(onEvent))
            {
                return;
            }

#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Unregister,
                key,
                typeof(T),
                onEvent));
#endif
        }

        /// <summary>
        /// 注销一个可变参数字符串事件监听器；仅为兼容旧代码保留。
        /// </summary>
        /// <param name="key">字符串事件键。</param>
        /// <param name="onEvent">需要移除的监听器。</param>
        public void UnRegister(string key, Action<object[]> onEvent)
        {
            UnRegister<object[]>(key, onEvent);
        }

        /// <summary>
        /// 清空全部字符串事件监听器。
        /// </summary>
        public void Clear()
        {
            foreach (KeyValuePair<string, EasyEvents> pair in mEventDic)
            {
                pair.Value.Clear();
            }

            mEventDic.Clear();
#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Clear,
                "*",
                null,
                null));
#endif
        }

#if UNITY_EDITOR || (GODOT && TOOLS)
        /// <summary>
        /// 返回全部字符串事件容器，用于编辑器检查或诊断。
        /// </summary>
        /// <returns>字符串事件容器字典。</returns>
        public IReadOnlyDictionary<string, EasyEvents> GetAllEvents()
        {
            return mEventDic;
        }
#endif

        /// <summary>
        /// 执行字符串事件发送，并根据是否存在负载选择对应 EasyEvent 容器。
        /// </summary>
        /// <typeparam name="T">事件负载类型。</typeparam>
        /// <param name="key">字符串事件键。</param>
        /// <param name="args">事件负载。</param>
        /// <param name="hasPayload">是否发送负载事件。</param>
        private void SendCore<T>(
            string key,
            T args,
            bool hasPayload)
        {
            RequireKey(key);
#if UNITY_EDITOR || (GODOT && TOOLS)
            EasyEventEditorHook.Publish(new EventKitEditorNotification(
                EventKitEditorNotificationKind.Send,
                key,
                hasPayload ? typeof(T) : null,
                null));
#endif
            EasyEvents stringEvent;
            if (!mEventDic.TryGetValue(key, out stringEvent))
            {
                return;
            }

            if (hasPayload)
            {
                stringEvent.GetEvent<EasyEvent<T>>()?.Trigger(args);
                return;
            }

            stringEvent.GetEvent<EasyEvent>()?.Trigger();
        }

        /// <summary>
        /// 获取指定字符串键的事件容器；不存在时创建。
        /// </summary>
        /// <param name="key">字符串事件键。</param>
        /// <returns>事件容器集合。</returns>
        private EasyEvents GetOrCreateEvents(string key)
        {
            EasyEvents stringEvent;
            if (!mEventDic.TryGetValue(key, out stringEvent))
            {
                stringEvent = new EasyEvents();
                mEventDic.Add(key, stringEvent);
            }

            return stringEvent;
        }

        /// <summary>
        /// 校验字符串事件键，避免 Dictionary 接收空引用键。
        /// </summary>
        /// <param name="key">字符串事件键。</param>
        private static void RequireKey(string key)
        {
            if (key == null)
            {
                throw new ArgumentNullException(nameof(key));
            }
        }
    }
}
