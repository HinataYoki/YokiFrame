using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace YokiFrame
{
    public static class Pool
    {
        public static void List<T>(Action<List<T>> list)
        {
            var poolList = ListPool<T>.Get();
            list?.Invoke(poolList);
            ListPool<T>.Release(poolList);
        }

        public static void Dictionary<TKey, TValue>(Action<Dictionary<TKey, TValue>> dic)
        {
            var poolDic = DictionaryPool<TKey, TValue>.Get();
            dic?.Invoke(poolDic);
            DictionaryPool<TKey, TValue>.Release(poolDic);
        }

        public static void Set<T>(Action<HashSet<T>> set)
        {
            var poolSet = HashSetPool<T>.Get();
            set?.Invoke(poolSet);
            HashSetPool<T>.Release(poolSet);
        }
    }
}