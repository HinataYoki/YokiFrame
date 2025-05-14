using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 回收队列
    /// </summary>
    [MonoSingletonPath("YokiFrame/ActionKit/Recycler")]
    internal class MonoRecycler : MonoBehaviour, ISingleton
    {
        /// <summary>
        /// 延迟回收队列
        /// </summary>
        private readonly List<IActionRecycler> recycList = new();
        private static MonoRecycler Instance => SingletonKit<MonoRecycler>.Instance;

        public static void AddRecycleCallback(IActionRecycler actionQueueCallback)
        {
            Instance.recycList.Add(actionQueueCallback);
        }

        private void Update()
        {
            if (recycList.Count > 0)
            {
                foreach (var recycle in recycList)
                {
                    recycle.Recycle();
                }
                recycList.Clear();
            }
        }

        void ISingleton.OnSingletonInit() { }
    }
}