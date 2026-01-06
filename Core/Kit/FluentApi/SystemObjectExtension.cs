using System;
using System.Collections.Generic;

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

        /// <summary>
        /// 条件执行：当条件为 true 时执行 Action
        /// </summary>
        public static T If<T>(this T self, bool condition, Action<T> onTrue)
        {
            if (condition) onTrue?.Invoke(self);
            return self;
        }

        /// <summary>
        /// 条件执行：根据条件执行不同的 Action
        /// </summary>
        public static T If<T>(this T self, bool condition, Action<T> onTrue, Action<T> onFalse)
        {
            if (condition) onTrue?.Invoke(self);
            else onFalse?.Invoke(self);
            return self;
        }

        /// <summary>
        /// 条件执行：当 Func 返回 true 时执行 Action
        /// </summary>
        public static T If<T>(this T self, Func<T, bool> condition, Action<T> onTrue)
        {
            if (condition(self)) onTrue?.Invoke(self);
            return self;
        }

        #endregion

        #region 空值判断 (仅适用于引用类型)

        /// <summary>
        /// 判断引用类型对象是否为空 (== null)
        /// </summary>
        /// <remarks>仅适用于引用类型，值类型请直接使用 == 比较</remarks>
        public static bool IsNull<T>(this T selfObj) where T : class => selfObj == null;

        /// <summary>
        /// 判断引用类型对象是否不为空 (!= null)
        /// </summary>
        /// <remarks>仅适用于引用类型，值类型请直接使用 != 比较</remarks>
        public static bool IsNotNull<T>(this T selfObj) where T : class => selfObj != null;

        /// <summary>
        /// 如果为 null 则返回默认值
        /// </summary>
        public static T OrDefault<T>(this T self, T defaultValue) where T : class => self ?? defaultValue;

        /// <summary>
        /// 如果为 null 则通过工厂方法创建默认值
        /// </summary>
        public static T OrDefault<T>(this T self, Func<T> defaultFactory) where T : class => self ?? defaultFactory();

        #endregion

        #region 类型转换

        /// <summary>
        /// 安全类型转换 (as 操作符)
        /// </summary>
        public static T As<T>(this object selfObj) where T : class => selfObj as T;

        /// <summary>
        /// 类型判断并转换
        /// </summary>
        public static bool Is<T>(this object selfObj, out T result)
        {
            if (selfObj is T t)
            {
                result = t;
                return true;
            }
            result = default;
            return false;
        }

        #endregion

        #region 集合扩展

        /// <summary>
        /// 安全获取字典值，不存在则返回默认值
        /// </summary>
        public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, TValue defaultValue = default)
        {
            if (self == null) return defaultValue;
            return self.TryGetValue(key, out var value) ? value : defaultValue;
        }

        /// <summary>
        /// 安全获取字典值，不存在则通过工厂创建并添加
        /// </summary>
        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, Func<TValue> factory)
        {
            if (!self.TryGetValue(key, out var value))
            {
                value = factory();
                self.Add(key, value);
            }
            return value;
        }

        /// <summary>
        /// 遍历集合并执行 Action
        /// </summary>
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> self, Action<T> action)
        {
            foreach (var item in self)
            {
                action?.Invoke(item);
            }
            return self;
        }

        /// <summary>
        /// 遍历集合并执行 Action（带索引）
        /// </summary>
        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> self, Action<T, int> action)
        {
            int index = 0;
            foreach (var item in self)
            {
                action?.Invoke(item, index++);
            }
            return self;
        }

        /// <summary>
        /// 安全获取 List 元素，越界返回默认值
        /// </summary>
        public static T GetOrDefault<T>(this IList<T> self, int index, T defaultValue = default)
        {
            return index >= 0 && index < self.Count ? self[index] : defaultValue;
        }

        /// <summary>
        /// 添加元素并返回自身（链式调用）
        /// </summary>
        public static List<T> AddEx<T>(this List<T> self, T item)
        {
            self.Add(item);
            return self;
        }

        /// <summary>
        /// 添加多个元素并返回自身（链式调用）
        /// </summary>
        public static List<T> AddRangeEx<T>(this List<T> self, IEnumerable<T> items)
        {
            self.AddRange(items);
            return self;
        }

        /// <summary>
        /// 移除元素并返回自身（链式调用）
        /// </summary>
        public static List<T> RemoveEx<T>(this List<T> self, T item)
        {
            self.Remove(item);
            return self;
        }

        #endregion

        #region 数值扩展

        /// <summary>
        /// 将值限制在指定范围内
        /// </summary>
        public static int Clamp(this int self, int min, int max) => self < min ? min : (self > max ? max : self);

        /// <summary>
        /// 将值限制在指定范围内
        /// </summary>
        public static float Clamp(this float self, float min, float max) => self < min ? min : (self > max ? max : self);

        /// <summary>
        /// 判断值是否在指定范围内（包含边界）
        /// </summary>
        public static bool InRange(this int self, int min, int max) => self >= min && self <= max;

        /// <summary>
        /// 判断值是否在指定范围内（包含边界）
        /// </summary>
        public static bool InRange(this float self, float min, float max) => self >= min && self <= max;

        #endregion
    }
}
