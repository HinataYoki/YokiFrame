using System;
using System.Collections.Generic;

namespace YokiFrame
{
    /// <summary>
    /// 使用 Stack&lt;T&gt; 内部池的零分配池工具。
    /// 以纯 C# 实现替代 UnityEngine.Pool（ListPool/DictionaryPool/HashSetPool）。
    /// </summary>
    public static class Pool
    {
        private const int DEFAULT_CAPACITY = 16;

        /// <summary>
        /// 借出一个池化 List&lt;T&gt;，执行 action 后归还。
        /// </summary>
        public static void List<T>(Action<List<T>> action)
        {
            var list = ListPool<T>.Get();
            action?.Invoke(list);
            ListPool<T>.Release(list);
        }

        /// <summary>
        /// 借出一个池化 Dictionary&lt;TKey,TValue&gt;，执行 action 后归还。
        /// </summary>
        public static void Dictionary<TKey, TValue>(Action<Dictionary<TKey, TValue>> action)
        {
            var dic = DictPool<TKey, TValue>.Get();
            action?.Invoke(dic);
            DictPool<TKey, TValue>.Release(dic);
        }

        /// <summary>
        /// 借出一个池化 HashSet&lt;T&gt;，执行 action 后归还。
        /// </summary>
        public static void Set<T>(Action<HashSet<T>> action)
        {
            var set = SetPool<T>.Get();
            action?.Invoke(set);
            SetPool<T>.Release(set);
        }
    }

    /// <summary>
    /// 基于 Stack 的 List&lt;T&gt; 池。
    /// </summary>
    public static class ListPool<T>
    {
        private const int DEFAULT_CAPACITY = 16;
        private static readonly Stack<List<T>> sPool = new(DEFAULT_CAPACITY);
        private static readonly object sLock = new object();

        public static List<T> Get()
        {
            lock (sLock)
            {
                return sPool.Count > 0 ? sPool.Pop() : new List<T>();
            }
        }

        public static void Release(List<T> list)
        {
            if (list is null) return;
            lock (sLock)
            {
                list.Clear();
                sPool.Push(list);
            }
        }

        public static void Clear()
        {
            lock (sLock)
            {
                sPool.Clear();
            }
        }
    }

    /// <summary>
    /// 基于 Stack 的 Dictionary&lt;TKey, TValue&gt; 池。
    /// </summary>
    public static class DictPool<TKey, TValue>
    {
        private const int DEFAULT_CAPACITY = 16;
        private static readonly Stack<Dictionary<TKey, TValue>> sPool = new(DEFAULT_CAPACITY);
        private static readonly object sLock = new object();

        public static Dictionary<TKey, TValue> Get()
        {
            lock (sLock)
            {
                return sPool.Count > 0 ? sPool.Pop() : new Dictionary<TKey, TValue>();
            }
        }

        public static void Release(Dictionary<TKey, TValue> dic)
        {
            if (dic is null) return;
            lock (sLock)
            {
                dic.Clear();
                sPool.Push(dic);
            }
        }

        public static void Clear()
        {
            lock (sLock)
            {
                sPool.Clear();
            }
        }
    }

    /// <summary>
    /// 基于 Stack 的 HashSet&lt;T&gt; 池。
    /// </summary>
    public static class SetPool<T>
    {
        private const int DEFAULT_CAPACITY = 16;
        private static readonly Stack<HashSet<T>> sPool = new(DEFAULT_CAPACITY);
        private static readonly object sLock = new object();

        public static HashSet<T> Get()
        {
            lock (sLock)
            {
                return sPool.Count > 0 ? sPool.Pop() : new HashSet<T>();
            }
        }

        public static void Release(HashSet<T> set)
        {
            if (set is null) return;
            lock (sLock)
            {
                set.Clear();
                sPool.Push(set);
            }
        }

        public static void Clear()
        {
            lock (sLock)
            {
                sPool.Clear();
            }
        }
    }
}
