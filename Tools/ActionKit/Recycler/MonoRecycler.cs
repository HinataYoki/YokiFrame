using System;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 回收队列 - 使用泛型避免装箱
    /// </summary>
    [MonoSingletonPath("YokiFrame/ActionKit/Recycler")]
    internal class MonoRecycler : MonoBehaviour, ISingleton
    {
        /// <summary>
        /// 所有类型的回收队列注册表
        /// </summary>
        private static readonly List<Action> sRecycleActions = new(16);
        private static MonoRecycler Instance => SingletonKit<MonoRecycler>.Instance;

        /// <summary>
        /// 泛型回收队列 - 每种 Action 类型独立队列，避免装箱
        /// </summary>
        private static class RecycleQueue<T>
        {
            private static readonly List<ActionRecycler<T>> sQueue = new(32);
            private static bool sRegistered;

            public static void Add(ActionRecycler<T> recycler)
            {
                // 确保访问 Instance 以初始化 MonoRecycler
                _ = Instance;
                
                if (!sRegistered)
                {
                    sRegistered = true;
                    sRecycleActions.Add(ProcessQueue);
                }
                sQueue.Add(recycler);
            }

            private static void ProcessQueue()
            {
                for (int i = 0; i < sQueue.Count; i++)
                {
                    sQueue[i].Recycle();
                }
                sQueue.Clear();
            }
        }

        /// <summary>
        /// 添加回收任务 - 泛型方法避免装箱
        /// </summary>
        public static void AddRecycleCallback<T>(ActionRecycler<T> recycler)
        {
            RecycleQueue<T>.Add(recycler);
        }

        private void Update()
        {
            if (sRecycleActions.Count > 0)
            {
                for (int i = 0; i < sRecycleActions.Count; i++)
                {
                    sRecycleActions[i]?.Invoke();
                }
            }
        }

        void ISingleton.OnSingletonInit() { }
    }
}