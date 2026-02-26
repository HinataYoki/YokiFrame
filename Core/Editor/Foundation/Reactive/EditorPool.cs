#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// 可池化对象接口
    /// 实现此接口的对象在归还池时会调用 OnReturn 进行重置
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// 对象归还池时调用，用于重置状态
        /// </summary>
        void OnReturn();
    }

    /// <summary>
    /// 编辑器专用 List 池 - 用于减少 GC 分配
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    public static class ListPool<T>
    {
        private static readonly Stack<List<T>> sPool = new(8);

        /// <summary>
        /// 从池中获取 List
        /// </summary>
        public static List<T> Get()
        {
            return sPool.Count > 0 ? sPool.Pop() : new List<T>(16);
        }

        /// <summary>
        /// 将 List 归还池中
        /// </summary>
        public static void Return(List<T> list)
        {
            if (list is null) return;
            list.Clear();
            sPool.Push(list);
        }

        /// <summary>
        /// 清空对象池
        /// </summary>
        public static void Clear()
        {
            sPool.Clear();
        }

        /// <summary>
        /// 当前池中对象数量
        /// </summary>
        public static int Count => sPool.Count;
    }

    /// <summary>
    /// 编辑器专用泛型对象池 - 用于减少 GC 分配
    /// 适用于需要频繁创建/销毁的对象
    /// </summary>
    /// <typeparam name="T">对象类型，必须是引用类型且有无参构造函数</typeparam>
    public static class EditorPool<T> where T : class, new()
    {
        private static readonly Stack<T> sPool = new(16);

        /// <summary>
        /// 从池中获取对象，如果池为空则创建新对象
        /// </summary>
        /// <returns>可用对象实例</returns>
        public static T Get()
        {
            return sPool.Count > 0 ? sPool.Pop() : new T();
        }

        /// <summary>
        /// 将对象归还池中
        /// 如果对象实现了 IPoolable 接口，会自动调用 OnReturn
        /// </summary>
        /// <param name="item">要归还的对象</param>
        public static void Return(T item)
        {
            if (item is null) return;
            if (item is IPoolable poolable) poolable.OnReturn();
            sPool.Push(item);
        }

        /// <summary>
        /// 清空对象池
        /// </summary>
        public static void Clear()
        {
            sPool.Clear();
        }

        /// <summary>
        /// 当前池中对象数量
        /// </summary>
        public static int Count => sPool.Count;
    }

    /// <summary>
    /// 编辑器专用 Dictionary 池 - 用于减少 GC 分配
    /// </summary>
    /// <typeparam name="TKey">键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    public static class DictionaryPool<TKey, TValue>
    {
        private static readonly Stack<Dictionary<TKey, TValue>> sPool = new(4);

        /// <summary>
        /// 从池中获取 Dictionary
        /// </summary>
        public static Dictionary<TKey, TValue> Get()
        {
            return sPool.Count > 0 ? sPool.Pop() : new Dictionary<TKey, TValue>(16);
        }

        /// <summary>
        /// 将 Dictionary 归还池中
        /// </summary>
        public static void Return(Dictionary<TKey, TValue> dict)
        {
            if (dict is null) return;
            dict.Clear();
            sPool.Push(dict);
        }

        /// <summary>
        /// 清空对象池
        /// </summary>
        public static void Clear()
        {
            sPool.Clear();
        }
    }

    /// <summary>
    /// 编辑器专用 HashSet 池 - 用于减少 GC 分配
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    public static class HashSetPool<T>
    {
        private static readonly Stack<HashSet<T>> sPool = new(4);

        /// <summary>
        /// 从池中获取 HashSet
        /// </summary>
        public static HashSet<T> Get()
        {
            return sPool.Count > 0 ? sPool.Pop() : new HashSet<T>(16);
        }

        /// <summary>
        /// 将 HashSet 归还池中
        /// </summary>
        public static void Return(HashSet<T> set)
        {
            if (set is null) return;
            set.Clear();
            sPool.Push(set);
        }

        /// <summary>
        /// 清空对象池
        /// </summary>
        public static void Clear()
        {
            sPool.Clear();
        }
    }

    /// <summary>
    /// 池化对象包装器 - 使用 using 语句自动归还
    /// </summary>
    /// <typeparam name="T">对象类型</typeparam>
    public readonly struct PooledObject<T> : IDisposable where T : class, new()
    {
        /// <summary>
        /// 池化的对象实例
        /// </summary>
        public readonly T Value;

        public PooledObject(T value)
        {
            Value = value;
        }

        /// <summary>
        /// 释放时自动归还池
        /// </summary>
        public void Dispose()
        {
            EditorPool<T>.Return(Value);
        }

        public static implicit operator T(PooledObject<T> pooled) => pooled.Value;
    }

    /// <summary>
    /// 池化 List 包装器 - 使用 using 语句自动归还
    /// </summary>
    /// <typeparam name="T">元素类型</typeparam>
    public readonly struct PooledList<T> : IDisposable
    {
        /// <summary>
        /// 池化的 List 实例
        /// </summary>
        public readonly List<T> Value;

        public PooledList(List<T> value)
        {
            Value = value;
        }

        /// <summary>
        /// 释放时自动归还池
        /// </summary>
        public void Dispose()
        {
            ListPool<T>.Return(Value);
        }

        public static implicit operator List<T>(PooledList<T> pooled) => pooled.Value;
    }

    /// <summary>
    /// 对象池扩展方法
    /// </summary>
    public static class EditorPoolExtensions
    {
        /// <summary>
        /// 获取池化对象，使用 using 语句自动归还
        /// </summary>
        /// <example>
        /// using var pooled = EditorPoolExtensions.GetPooled&lt;MyClass&gt;();
        /// pooled.Value.DoSomething();
        /// </example>
        public static PooledObject<T> GetPooled<T>() where T : class, new()
        {
            return new PooledObject<T>(EditorPool<T>.Get());
        }

        /// <summary>
        /// 获取池化 List，使用 using 语句自动归还
        /// </summary>
        /// <example>
        /// using var pooled = EditorPoolExtensions.GetPooledList&lt;int&gt;();
        /// pooled.Value.Add(1);
        /// </example>
        public static PooledList<T> GetPooledList<T>()
        {
            return new PooledList<T>(ListPool<T>.Get());
        }
    }
}
#endif
