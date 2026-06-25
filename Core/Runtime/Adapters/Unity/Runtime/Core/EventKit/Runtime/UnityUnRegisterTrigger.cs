#if !GODOT
using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    internal abstract class UnityUnRegisterTrigger : MonoBehaviour
    {
        private readonly List<IUnRegister> mUnRegisters = new();

        public T AddUnRegister<T>(T unRegister) where T : IUnRegister
        {
            mUnRegisters.Add(unRegister);
            return unRegister;
        }

        protected void UnRegisterAll()
        {
            for (var i = 0; i < mUnRegisters.Count; i++)
                mUnRegisters[i].UnRegister();

            mUnRegisters.Clear();
        }
    }

    internal sealed class UnityUnRegisterOnDestroyTrigger : UnityUnRegisterTrigger
    {
        private void OnDestroy() => UnRegisterAll();
    }

    internal sealed class UnityUnRegisterOnDisableTrigger : UnityUnRegisterTrigger
    {
        private void OnDisable() => UnRegisterAll();
    }

    /// <summary>
    /// EventKit 的 Unity 生命周期注销扩展。
    /// </summary>
    public static class UnityUnRegisterExtension
    {
        public static T UnRegisterWhenGameObjectDestroyed<T>(this T self, Component component) where T : IUnRegister
        {
            if (component == null)
                return self;

            return self.UnRegisterWhenGameObjectDestroyed(component.gameObject);
        }

        public static T UnRegisterWhenGameObjectDestroyed<T>(this T self, GameObject gameObject) where T : IUnRegister
        {
            if (gameObject == null)
                return self;

            GetOrAddComponent<UnityUnRegisterOnDestroyTrigger>(gameObject).AddUnRegister(self);
            return self;
        }

        public static T UnRegisterWhenDisabled<T>(this T self, Component component) where T : IUnRegister
        {
            if (component == null)
                return self;

            return self.UnRegisterWhenDisabled(component.gameObject);
        }

        public static T UnRegisterWhenDisabled<T>(this T self, GameObject gameObject) where T : IUnRegister
        {
            if (gameObject == null)
                return self;

            GetOrAddComponent<UnityUnRegisterOnDisableTrigger>(gameObject).AddUnRegister(self);
            return self;
        }

        private static TComponent GetOrAddComponent<TComponent>(GameObject gameObject) where TComponent : Component
        {
            var component = gameObject.GetComponent<TComponent>();
            if (component != null)
                return component;

            return gameObject.AddComponent<TComponent>();
        }
    }
}
#endif
