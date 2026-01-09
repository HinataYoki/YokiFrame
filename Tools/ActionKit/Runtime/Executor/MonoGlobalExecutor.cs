using System;
using System.Collections;

namespace YokiFrame
{
    /// <summary>
    /// ActionKit默认生命周期提供者
    /// </summary>
    [MonoSingletonPath("YokiFrame/ActionKit/Global")]
    internal class MonoGlobalExecutor : MonoUpdateExecutor, ISingleton
    {
        public static MonoGlobalExecutor Instance => SingletonKit<MonoGlobalExecutor>.Instance;

        public static void ExecuteCoroutine(IEnumerator coroutine, Action onFinish)
        {
            Instance.StartCoroutine(DoExecuteCoroutine(coroutine, onFinish));
        }

        static IEnumerator DoExecuteCoroutine(IEnumerator coroutine, Action onFinish)
        {
            yield return coroutine;
            onFinish?.Invoke();
        }

        void ISingleton.OnSingletonInit() { }
    }

}