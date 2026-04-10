#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace YokiFrame.EditorTools
{
    /// <summary>
    /// Implemented by pooled objects that need reset logic before returning to the pool.
    /// </summary>
    public interface IPoolable
    {
        /// <summary>
        /// Called before the object is pushed back into the pool.
        /// </summary>
        void OnReturn();
    }

    /// <summary>
    /// Small editor-only list pool used to reduce transient allocations.
    /// </summary>
    /// <typeparam name="T">Element type.</typeparam>
    public static class ListPool<T>
    {
        private static readonly Stack<List<T>> sPool = new(8);

        /// <summary>
        /// Gets a list instance from the pool.
        /// </summary>
        public static List<T> Get()
        {
            return sPool.Count > 0 ? sPool.Pop() : new List<T>(16);
        }

        /// <summary>
        /// Returns a list instance to the pool.
        /// </summary>
        public static void Return(List<T> list)
        {
            if (list is null) return;
            list.Clear();
            sPool.Push(list);
        }

        /// <summary>
        /// Clears all cached list instances.
        /// </summary>
        public static void Clear()
        {
            sPool.Clear();
        }

        /// <summary>
        /// Number of cached list instances.
        /// </summary>
        public static int Count => sPool.Count;
    }

    /// <summary>
    /// Generic editor-only object pool used to reduce transient allocations.
    /// </summary>
    /// <typeparam name="T">Reference type with a parameterless constructor.</typeparam>
    public static class EditorPool<T> where T : class, new()
    {
        private static readonly Stack<T> sPool = new(16);

        /// <summary>
        /// Gets an object from the pool or creates one when the pool is empty.
        /// </summary>
        public static T Get()
        {
            return sPool.Count > 0 ? sPool.Pop() : new T();
        }

        /// <summary>
        /// Returns an object to the pool.
        /// </summary>
        /// <remarks>
        /// If the object implements <see cref="IPoolable"/>, <see cref="IPoolable.OnReturn"/> is invoked first.
        /// </remarks>
        public static void Return(T item)
        {
            if (item is null) return;

            if (item is IPoolable poolable)
            {
                poolable.OnReturn();
            }

            sPool.Push(item);
        }

        /// <summary>
        /// Clears all cached object instances.
        /// </summary>
        public static void Clear()
        {
            sPool.Clear();
        }

        /// <summary>
        /// Number of cached object instances.
        /// </summary>
        public static int Count => sPool.Count;
    }

    /// <summary>
    /// Small editor-only dictionary pool used to reduce transient allocations.
    /// </summary>
    public static class DictionaryPool<TKey, TValue>
    {
        private static readonly Stack<Dictionary<TKey, TValue>> sPool = new(4);

        /// <summary>
        /// Gets a dictionary instance from the pool.
        /// </summary>
        public static Dictionary<TKey, TValue> Get()
        {
            return sPool.Count > 0 ? sPool.Pop() : new Dictionary<TKey, TValue>(16);
        }

        /// <summary>
        /// Returns a dictionary instance to the pool.
        /// </summary>
        public static void Return(Dictionary<TKey, TValue> dict)
        {
            if (dict is null) return;
            dict.Clear();
            sPool.Push(dict);
        }

        /// <summary>
        /// Clears all cached dictionary instances.
        /// </summary>
        public static void Clear()
        {
            sPool.Clear();
        }
    }

    /// <summary>
    /// Small editor-only hash set pool used to reduce transient allocations.
    /// </summary>
    public static class HashSetPool<T>
    {
        private static readonly Stack<HashSet<T>> sPool = new(4);

        /// <summary>
        /// Gets a hash set instance from the pool.
        /// </summary>
        public static HashSet<T> Get()
        {
            return sPool.Count > 0 ? sPool.Pop() : new HashSet<T>(16);
        }

        /// <summary>
        /// Returns a hash set instance to the pool.
        /// </summary>
        public static void Return(HashSet<T> set)
        {
            if (set is null) return;
            set.Clear();
            sPool.Push(set);
        }

        /// <summary>
        /// Clears all cached hash set instances.
        /// </summary>
        public static void Clear()
        {
            sPool.Clear();
        }
    }

    /// <summary>
    /// <see langword="using"/> helper that returns a pooled object automatically.
    /// </summary>
    public readonly struct PooledObject<T> : IDisposable where T : class, new()
    {
        /// <summary>
        /// Pooled object instance.
        /// </summary>
        public readonly T Value;

        /// <summary>
        /// Creates a pooled object wrapper.
        /// </summary>
        public PooledObject(T value)
        {
            Value = value;
        }

        /// <summary>
        /// Returns the wrapped object to the pool.
        /// </summary>
        public void Dispose()
        {
            EditorPool<T>.Return(Value);
        }

        /// <summary>
        /// Implicitly unwraps the pooled value.
        /// </summary>
        public static implicit operator T(PooledObject<T> pooled) => pooled.Value;
    }

    /// <summary>
    /// <see langword="using"/> helper that returns a pooled list automatically.
    /// </summary>
    public readonly struct PooledList<T> : IDisposable
    {
        /// <summary>
        /// Pooled list instance.
        /// </summary>
        public readonly List<T> Value;

        /// <summary>
        /// Creates a pooled list wrapper.
        /// </summary>
        public PooledList(List<T> value)
        {
            Value = value;
        }

        /// <summary>
        /// Returns the wrapped list to the pool.
        /// </summary>
        public void Dispose()
        {
            ListPool<T>.Return(Value);
        }

        /// <summary>
        /// Implicitly unwraps the pooled value.
        /// </summary>
        public static implicit operator List<T>(PooledList<T> pooled) => pooled.Value;
    }

    /// <summary>
    /// Convenience helpers for acquiring pooled editor objects with <see langword="using"/>.
    /// </summary>
    public static class EditorPoolExtensions
    {
        /// <summary>
        /// Gets a pooled object wrapper.
        /// </summary>
        public static PooledObject<T> GetPooled<T>() where T : class, new()
        {
            return new PooledObject<T>(EditorPool<T>.Get());
        }

        /// <summary>
        /// Gets a pooled list wrapper.
        /// </summary>
        public static PooledList<T> GetPooledList<T>()
        {
            return new PooledList<T>(ListPool<T>.Get());
        }
    }
}
#endif
