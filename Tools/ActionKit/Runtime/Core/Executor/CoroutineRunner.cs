using System;
using System.Collections;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 协程运行器 - 使用隐藏 GameObject 运行协程
    /// 仅在需要时创建，避免不必要的 MonoBehaviour 开销
    /// </summary>
    [MonoSingletonPath("YokiFrame/ActionKit/CoroutineRunner")]
    internal class CoroutineRunner : MonoBehaviour, ISingleton
    {
        private static CoroutineRunner sInstance;

        public static CoroutineRunner Instance
        {
            get
            {
                if (sInstance == default)
                {
                    sInstance = SingletonKit<CoroutineRunner>.Instance;
                }
                return sInstance;
            }
        }

        public static UnityEngine.Coroutine StartCoroutineStatic(IEnumerator coroutine)
        {
            return Instance.StartCoroutine(coroutine);
        }

        public static void StopCoroutineStatic(UnityEngine.Coroutine coroutine)
        {
            if (sInstance != default && coroutine != null)
            {
                sInstance.StopCoroutine(coroutine);
            }
        }

        void ISingleton.OnSingletonInit() { }
    }
}
