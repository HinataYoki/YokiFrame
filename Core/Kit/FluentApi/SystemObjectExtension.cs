using System;

namespace YokiFrame
{
    public static class SystemObjectExtension
    {
        #region Self 链式调用

        /// <summary>
        /// 将自己传到 Action 委托中
        /// </summary>
        public static T Self<T>(this T self, Action<T> onDo)
        {
            onDo?.Invoke(self);
            return self;
        }

        /// <summary>
        /// 将自己传到 Func&lt;T,T&gt; 委托中,然后返回自己
        /// </summary>
        public static T Self<T>(this T self, Func<T, T> onDo)
        {
            return onDo.Invoke(self);
        }

        #endregion

        #region 空值判断 (仅适用于引用类型)

        /// <summary>
        /// 判断引用类型对象是否为空 (== null)
        /// </summary>
        /// <remarks>仅适用于引用类型，值类型请直接使用 == 比较</remarks>
        public static bool IsNull<T>(this T selfObj) where T : class
        {
            return selfObj == null;
        }

        /// <summary>
        /// 判断引用类型对象是否不为空 (!= null)
        /// </summary>
        /// <remarks>仅适用于引用类型，值类型请直接使用 != 比较</remarks>
        public static bool IsNotNull<T>(this T selfObj) where T : class
        {
            return selfObj != null;
        }

        #endregion

        #region 类型转换

        /// <summary>
        /// 安全类型转换 (as 操作符)
        /// </summary>
        public static T As<T>(this object selfObj) where T : class
        {
            return selfObj as T;
        }

        #endregion
    }
}
