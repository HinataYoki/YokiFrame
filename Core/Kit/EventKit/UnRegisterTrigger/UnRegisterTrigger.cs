using System.Collections.Generic;
using UnityEngine;

namespace YokiFrame
{
    // TODO：无法避免的装箱损耗
    public abstract class UnRegisterTrigger<T> : MonoBehaviour where T : IUnRegister
    {
        private readonly HashSet<T> mUnRegisters = new();

        public T AddUnRegister(T unRegister)
        {
            mUnRegisters.Add(unRegister);
            return unRegister;
        }

        public void RemoveUnRegister(T unRegister)
            => mUnRegisters.Remove(unRegister);

        protected void UnRegister()
        {
            foreach (var unRegister in mUnRegisters)
            {
                unRegister.UnRegister();
            }

            mUnRegisters.Clear();
        }
    }

    public static class UnRegisterExtension
    {
        static T GetOrAddComponent<T>(GameObject gameObject) where T : Component
        {
            var trigger = gameObject.GetComponent<T>();

            if (!trigger)
            {
                trigger = gameObject.AddComponent<T>();
            }

            return trigger;
        }


        public static T UnRegisterWhenGameObjectDestroyed<T>(this T self, Component component) where T : IUnRegister =>
            self.UnRegisterWhenGameObjectDestroyed(component.gameObject);
        public static T UnRegisterWhenGameObjectDestroyed<T>(this T self, GameObject gameObject) where T : IUnRegister
        {
            GetOrAddComponent<UnRegisterOnDestroyTrigger>(gameObject).AddUnRegister(self);
            return self;
        }

        public static T UnRegisterWhenDisabled<T>(this T self, Component component) where T : IUnRegister =>
            self.UnRegisterWhenDisabled(component.gameObject);
        public static T UnRegisterWhenDisabled<T>(this T self, GameObject gameObject) where T : IUnRegister
        {
            GetOrAddComponent<UnRegisterOnDisableTrigger>(gameObject).AddUnRegister(self);
            return self;
        }
    }
}