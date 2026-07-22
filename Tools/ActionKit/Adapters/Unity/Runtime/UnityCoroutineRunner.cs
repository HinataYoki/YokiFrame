#if UNITY_5_3_OR_NEWER
using System.Collections;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>复用 Core MonoSingleton 承载 Unity 原生 Coroutine，不建立第二个 ActionKit Tick。</summary>
    [MonoSingletonPath("YokiFrame/ActionKit Coroutine Runner")]
    internal sealed class UnityCoroutineRunner : MonoSingleton<UnityCoroutineRunner>
    {
        /// <summary>在持久宿主上启动一个由 Unity 解释 yield 的 IEnumerator。</summary>
        /// <param name="enumerator">待交给 Unity Coroutine 调度器的枚举器。</param>
        /// <returns>Unity 原生 Coroutine handle。</returns>
        internal Coroutine Run(IEnumerator enumerator) => StartCoroutine(enumerator);

        /// <summary>停止当前 Runner 拥有的原生 Coroutine；重复停止保持无操作。</summary>
        /// <param name="coroutine">待停止的 Unity Coroutine handle。</param>
        internal void Stop(Coroutine coroutine)
        {
            if (coroutine != null) StopCoroutine(coroutine);
        }
    }
}
#endif
