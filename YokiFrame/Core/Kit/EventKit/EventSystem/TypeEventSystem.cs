using System;

namespace YokiFrame
{
    public class TypeEventSystem
    {
        private readonly EasyEvents mEventDic = new();

        /// <summary>
        /// 触发方法
        /// </summary>
        public void Send<T>(T args = default) => mEventDic.GetEvent<EasyEvent<T>>()?.Trigger(args);
        /// <summary>
        /// 注册方法
        /// </summary>
        /// <returns></returns>
        public LinkUnRegister<T> Register<T>(Action<T> onEvent) => mEventDic.GetOrAddEvent<EasyEvent<T>>().Register(onEvent);
        /// <summary>
        /// 注销方法
        /// </summary>
        public void UnRegister<T>(Action<T> onEvent) => mEventDic.GetEvent<EasyEvent<T>>()?.UnRegister(onEvent);

        public void Clear() => mEventDic.Clear();
    }
}