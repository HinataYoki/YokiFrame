using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace YokiFrame
{
    public static class Pool
    {
        public static void List<T>(Action<List<T>> list)
        {
            var pooList = ListPool<T>.Get();
            list?.Invoke(pooList);
            ListPool<T>.Release(pooList);
        }

        public static void Dictionary<TKay, TValue>(Action<Dictionary<TKay, TValue>> dic)
        {
            var poolDIc = DictionaryPool<TKay, TValue>.Get();
            dic?.Invoke(poolDIc);
            DictionaryPool<TKay, TValue>.Release(poolDIc);
        }

        public static void Set<T>(Action<HashSet<T>> set)
        {
            var poolSet = HashSetPool<T>.Get();
            set?.Invoke(poolSet);
            HashSetPool<T>.Release(poolSet);
        }
    }
}