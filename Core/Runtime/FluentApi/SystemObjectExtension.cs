using System;
using System.Collections.Generic;

namespace YokiFrame
{
    public static class SystemObjectExtension
    {
        #region Self 链式调用

        public static T Self<T>(this T self, Action<T> onDo)
        {
            onDo?.Invoke(self);
            return self;
        }

        public static T Self<T>(this T self, Func<T, T> onDo) => onDo.Invoke(self);

        public static T If<T>(this T self, bool condition, Action<T> onTrue)
        {
            if (condition) onTrue?.Invoke(self);
            return self;
        }

        public static T If<T>(this T self, bool condition, Action<T> onTrue, Action<T> onFalse)
        {
            if (condition) onTrue?.Invoke(self);
            else onFalse?.Invoke(self);
            return self;
        }

        public static T If<T>(this T self, Func<T, bool> condition, Action<T> onTrue)
        {
            if (condition(self)) onTrue?.Invoke(self);
            return self;
        }

        #endregion

        #region 空值判断 (仅适用于引用类型)

        public static bool IsNull<T>(this T selfObj) where T : class => selfObj is null;
        public static bool IsNotNull<T>(this T selfObj) where T : class => selfObj is not null;
        public static T OrDefault<T>(this T self, T defaultValue) where T : class => self ?? defaultValue;
        public static T OrDefault<T>(this T self, Func<T> defaultFactory) where T : class => self ?? defaultFactory();

        #endregion

        #region 类型转换

        public static T As<T>(this object selfObj) where T : class => selfObj as T;

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

        public static TValue GetOrDefault<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, TValue defaultValue = default)
        {
            if (self is null) return defaultValue;
            return self.TryGetValue(key, out var value) ? value : defaultValue;
        }

        public static TValue GetOrAdd<TKey, TValue>(this Dictionary<TKey, TValue> self, TKey key, Func<TValue> factory)
        {
            if (!self.TryGetValue(key, out var value))
            {
                value = factory();
                self.Add(key, value);
            }
            return value;
        }

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> self, Action<T> action)
        {
            foreach (var item in self)
                action?.Invoke(item);
            return self;
        }

        public static IEnumerable<T> ForEach<T>(this IEnumerable<T> self, Action<T, int> action)
        {
            int index = 0;
            foreach (var item in self)
                action?.Invoke(item, index++);
            return self;
        }

        public static T GetOrDefault<T>(this IList<T> self, int index, T defaultValue = default)
            => index >= 0 && index < self.Count ? self[index] : defaultValue;

        public static List<T> AddEx<T>(this List<T> self, T item) { self.Add(item); return self; }
        public static List<T> AddRangeEx<T>(this List<T> self, IEnumerable<T> items) { self.AddRange(items); return self; }
        public static List<T> RemoveEx<T>(this List<T> self, T item) { self.Remove(item); return self; }

        #endregion

        #region 数值扩展

        public static int Clamp(this int self, int min, int max) => self < min ? min : (self > max ? max : self);
        public static float Clamp(this float self, float min, float max) => self < min ? min : (self > max ? max : self);
        public static bool InRange(this int self, int min, int max) => self >= min && self <= max;
        public static bool InRange(this float self, float min, float max) => self >= min && self <= max;

        #endregion
    }
}
