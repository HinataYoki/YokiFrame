using System;
using System.Collections.Generic;
using Unity.Collections.LowLevel.Unsafe;

namespace YokiFrame
{
    public class EnumEventSystem
    {
        private readonly Dictionary<(Type, int), EasyEvents> mEventDic = new();

        private void GetEvents<TEnum>(TEnum key, out EasyEvents enumEvent) where TEnum : Enum, IConvertible
        {
            var type = typeof(TEnum);
#if UNITY_2021_1_OR_NEWER
            var intKey = UnsafeUtility.As<TEnum, int>(ref key);
#else
            var intKey = key.ToInt32(null);
#endif
            if (mEventDic.TryGetValue((type, intKey), out enumEvent))
            {
                enumEvent = new();
                mEventDic.Add((type, intKey), enumEvent);
            }
        }
        /// <summary>
        /// 触发无参方法
        /// </summary>
        public void Send<TEnum>(TEnum key) where TEnum : Enum
        {
            GetEvents(key, out var enumEvent);
            enumEvent.GetEvent<EasyEvent>()?.Trigger();
        }
        /// <summary>
        /// 触发有参方法
        /// </summary>
        public void Send<TEnum, TArgs>(TEnum key, TArgs args) where TEnum : Enum
        {
            GetEvents(key, out var enumEvent);
            enumEvent.GetEvent<EasyEvent<TArgs>>()?.Trigger(args);
        }
        /// <summary>
        /// 触发可变参数方法
        /// </summary>
        public void Send<TEnum>(TEnum key, params object[] args) where TEnum : Enum => Send<TEnum, object[]>(key, args);

        /// <summary>
        /// 注册无参方法
        /// </summary>
        public LinkUnRegister Register<TEnum>(TEnum key, Action onEvent) where TEnum : Enum
        {
            GetEvents(key, out var enumEvent);
            return enumEvent.GetOrAddEvent<EasyEvent>().Register(onEvent);
        }
        /// <summary>
        /// 注册有参方法
        /// </summary>
        public LinkUnRegister<TArgs> Register<TEnum, TArgs>(TEnum key, Action<TArgs> onEvent) where TEnum : Enum
        {
            GetEvents(key, out var enumEvent);
            return enumEvent.GetOrAddEvent<EasyEvent<TArgs>>().Register(onEvent);
        }
        /// <summary>
        /// 注册可变参数方法
        /// </summary>
        public LinkUnRegister<object[]> Register<TEnum>(TEnum key, Action<object[]> onEvent) where TEnum : Enum => Register<TEnum, object[]>(key, onEvent);

        /// <summary>
        /// 注销此枚举所有方法
        /// </summary>
        public void UnRegister<TEnum>(TEnum key) where TEnum : Enum
        {
            GetEvents(key, out var enumEvent);
            enumEvent.Clear();
        }
        public void UnRegister<TEnum>(TEnum key, Action onEvent) where TEnum : Enum
        {
            GetEvents(key, out var enumEvent);
            enumEvent.GetEvent<EasyEvent>()?.UnRegister(onEvent);
        }
        public void UnRegister<TEnum, TArgs>(TEnum key, Action<TArgs> onEvent) where TEnum : Enum
        {
            GetEvents(key, out var enumEvent);
            enumEvent.GetEvent<EasyEvent<TArgs>>()?.UnRegister(onEvent);
        }
        public void UnRegister<TEnum>(TEnum key, Action<object[]> onEvent) where TEnum : Enum => UnRegister<TEnum, object[]>(key, onEvent);

        public void Clear() => mEventDic.Clear();
    }
}