#if !GODOT
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// 将 EventKit 注销令牌绑定到 MonoBehaviour 的 OnDisable，兼容 UIKit 1.0 风格的注册写法。
    /// </summary>
    public static class UnRegisterWhenDisabledExtensions
    {
        /// <summary>
        /// 将注销令牌绑定到 MonoBehaviour 禁用或销毁生命周期。
        /// </summary>
        /// <typeparam name="T">注销令牌类型。</typeparam>
        /// <param name="unRegister">注销令牌。</param>
        /// <param name="owner">生命周期宿主。</param>
        public static void UnRegisterWhenDisabled<T>(this T unRegister, MonoBehaviour owner) where T : IUnRegister
        {
            if (owner == null)
            {
                unRegister.UnRegister();
                return;
            }

            var trigger = owner.GetComponent<UnRegisterOnDisableTrigger>();
            if (trigger == null)
                trigger = owner.gameObject.AddComponent<UnRegisterOnDisableTrigger>();

            trigger.Add(unRegister);
        }

        private sealed class UnRegisterOnDisableTrigger : MonoBehaviour
        {
            private readonly List<IUnRegister> mUnRegisters = new();

            /// <summary>
            /// 添加需要在禁用或销毁时执行的注销令牌。
            /// </summary>
            /// <param name="unRegister">注销令牌。</param>
            public void Add(IUnRegister unRegister)
            {
                if (unRegister != null)
                    mUnRegisters.Add(unRegister);
            }

            private void OnDisable()
            {
                UnRegisterAll();
            }

            private void OnDestroy()
            {
                UnRegisterAll();
            }

            private void UnRegisterAll()
            {
                for (var i = 0; i < mUnRegisters.Count; i++)
                    mUnRegisters[i].UnRegister();

                mUnRegisters.Clear();
            }
        }
    }
}
#endif
