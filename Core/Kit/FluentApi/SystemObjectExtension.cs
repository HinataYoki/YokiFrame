using System;

namespace YokiFrame
{
    public static class SystemObjectExtension
    {
        /// <summary>
        /// 将自己传到 Action 委托中
        /// </summary>
        public static T Self<T>(this T self, Action<T> onDo)
        {
            onDo?.Invoke(self);
            return self;
        }

        /// <summary>
        /// 将自己传到 Func<T,T> 委托中,然后返回自己
        /// </summary>
        public static T Self<T>(this T self, Func<T, T> onDo)
        {
            return onDo.Invoke(self);
        }

        /// <summary>
        /// ==判断是否为空
        /// </summary>
        public static bool IsNull<T>(this T selfObj) where T : class
        {
            return null == selfObj;
        }

        /// <summary>
        /// !=判断不是为空
        /// </summary>
        public static bool IsNotNull<T>(this T selfObj) where T : class
        {
            return null != selfObj;
        }

        /// <summary>
        /// 转型
        /// </summary>
        public static T As<T>(this object selfObj) where T : class
        {
            return selfObj as T;
        }
    }
}
