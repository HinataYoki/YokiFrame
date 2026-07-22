#if UNITY_5_3_OR_NEWER
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    /// <summary>
    /// EventKit Unity 生命周期注销触发器基类，隐藏在 GameObject 上集中释放注销令牌。
    /// </summary>
    internal abstract class UnityEventKitUnRegisterTrigger : MonoBehaviour
    {
        private readonly List<IUnRegister> mUnRegisters = new List<IUnRegister>();

        /// <summary>
        /// 保存一个注销令牌，并返回原令牌便于链式调用。
        /// </summary>
        /// <typeparam name="T">注销令牌类型。</typeparam>
        /// <param name="unRegister">需要在生命周期触发时注销的令牌。</param>
        /// <returns>传入的注销令牌。</returns>
        public T AddUnRegister<T>(T unRegister) where T : IUnRegister
        {
            mUnRegisters.Add(unRegister);
            return unRegister;
        }

        /// <summary>
        /// 注销当前触发器中保存的全部令牌，并清空列表避免重复执行。
        /// </summary>
        protected void UnRegisterAll()
        {
            for (var index = 0; index < mUnRegisters.Count; index++)
            {
                mUnRegisters[index].UnRegister();
            }

            mUnRegisters.Clear();
        }
    }

    /// <summary>
    /// 在 GameObject 销毁时释放 EventKit 监听器的触发器。
    /// </summary>
    [ExecuteAlways]
    internal sealed class UnityEventKitUnRegisterOnDestroyTrigger : UnityEventKitUnRegisterTrigger
    {
        /// <summary>
        /// Unity 销毁回调，负责释放绑定到当前 GameObject 的全部 EventKit 令牌。
        /// </summary>
        private void OnDestroy()
        {
            UnRegisterAll();
        }
    }

    /// <summary>
    /// 在 GameObject 禁用时释放 EventKit 监听器的触发器。
    /// </summary>
    [ExecuteAlways]
    internal sealed class UnityEventKitUnRegisterOnDisableTrigger : UnityEventKitUnRegisterTrigger
    {
        /// <summary>
        /// Unity 禁用回调，负责释放绑定到当前 GameObject 的全部 EventKit 令牌。
        /// </summary>
        private void OnDisable()
        {
            UnRegisterAll();
        }
    }

    /// <summary>
    /// EventKit 的 Unity 生命周期注销扩展。
    /// </summary>
    public static class UnityEventKitUnRegisterExtensions
    {
        /// <summary>
        /// 把注销令牌绑定到指定 Component 所在 GameObject 的销毁时机。
        /// </summary>
        /// <typeparam name="T">注销令牌类型。</typeparam>
        /// <param name="self">要绑定生命周期的注销令牌。</param>
        /// <param name="component">用于承载销毁触发器的 Component。</param>
        /// <returns>原注销令牌，便于链式调用。</returns>
        public static T UnRegisterWhenGameObjectDestroyed<T>(this T self, Component component) where T : IUnRegister
        {
            if (component == null)
            {
                return self;
            }

            return self.UnRegisterWhenGameObjectDestroyed(component.gameObject);
        }

        /// <summary>
        /// 把注销令牌绑定到指定 GameObject 的销毁时机。
        /// </summary>
        /// <typeparam name="T">注销令牌类型。</typeparam>
        /// <param name="self">要绑定生命周期的注销令牌。</param>
        /// <param name="gameObject">用于承载销毁触发器的 GameObject。</param>
        /// <returns>原注销令牌，便于链式调用。</returns>
        public static T UnRegisterWhenGameObjectDestroyed<T>(this T self, GameObject gameObject) where T : IUnRegister
        {
            if (gameObject == null)
            {
                return self;
            }

            GetOrAddComponent<UnityEventKitUnRegisterOnDestroyTrigger>(gameObject).AddUnRegister(self);
            return self;
        }

        /// <summary>
        /// 把注销令牌绑定到指定 Component 所在 GameObject 的禁用时机。
        /// </summary>
        /// <typeparam name="T">注销令牌类型。</typeparam>
        /// <param name="self">要绑定生命周期的注销令牌。</param>
        /// <param name="component">用于承载禁用触发器的 Component。</param>
        /// <returns>原注销令牌，便于链式调用。</returns>
        public static T UnRegisterWhenDisabled<T>(this T self, Component component) where T : IUnRegister
        {
            if (component == null)
            {
                return self;
            }

            return self.UnRegisterWhenDisabled(component.gameObject);
        }

        /// <summary>
        /// 把注销令牌绑定到指定 GameObject 的禁用时机。
        /// </summary>
        /// <typeparam name="T">注销令牌类型。</typeparam>
        /// <param name="self">要绑定生命周期的注销令牌。</param>
        /// <param name="gameObject">用于承载禁用触发器的 GameObject。</param>
        /// <returns>原注销令牌，便于链式调用。</returns>
        public static T UnRegisterWhenDisabled<T>(this T self, GameObject gameObject) where T : IUnRegister
        {
            if (gameObject == null)
            {
                return self;
            }

            GetOrAddComponent<UnityEventKitUnRegisterOnDisableTrigger>(gameObject).AddUnRegister(self);
            return self;
        }

        /// <summary>
        /// 获取 GameObject 上已有组件，不存在时新增一个。
        /// </summary>
        /// <typeparam name="TComponent">要获取或创建的组件类型。</typeparam>
        /// <param name="gameObject">承载组件的 GameObject。</param>
        /// <returns>已有或新建的组件。</returns>
        private static TComponent GetOrAddComponent<TComponent>(GameObject gameObject) where TComponent : Component
        {
            TComponent component = gameObject.GetComponent<TComponent>();
            if (component != null)
            {
                return component;
            }

            return gameObject.AddComponent<TComponent>();
        }
    }
}
#endif
